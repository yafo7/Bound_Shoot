using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
namespace BoundShoot {
 public class ServerRuntime:MonoBehaviour {
  public static ServerRuntime Instance;
  public HttpApi Api;LaunchConfig launch;BattleWorld world;
  readonly Dictionary<ulong,RoomMember> approved=new Dictionary<ulong,RoomMember>();
  readonly HashSet<string> pendingUsers=new HashSet<string>();
  readonly HashSet<ulong> closedClients=new HashSet<ulong>();
  bool ending,finishing;
  string Path(string suffix)=>"/internal/matches/"+launch.matchId+"/"+suffix;
  IEnumerator Start(){
   Instance=this;string[] args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-matchFile");
   if(at<0||at+1>=args.Length){Debug.LogError("Missing -matchFile");Application.Quit(2);yield break;}
   try{launch=JsonUtility.FromJson<LaunchConfig>(File.ReadAllText(args[at+1]));}catch(Exception){Debug.LogError("Cannot read server launch manifest");Application.Quit(2);yield break;}
   Api=gameObject.AddComponent<HttpApi>();Api.BaseUrl=launch.backendUrl;Api.Secret=launch.secret;
   ServerRoster roster=null;string error=null;
   yield return Api.Request<ServerRoster>("POST",Path("register"),null,v=>roster=v,e=>error=e);
   if(roster==null){Debug.LogError("Server registration failed: "+error);Application.Quit(2);yield break;}
   var net=Bootstrap.Network;net.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1",(ushort)launch.port,"0.0.0.0");
   net.ConnectionApprovalCallback+=Approve;net.OnClientConnectedCallback+=Connected;net.OnClientDisconnectCallback+=Disconnected;
   if(!net.StartServer()){Abort("UDP port bind failed");yield break;}
   world=Instantiate(Resources.Load<GameObject>("NetWorld")).GetComponent<BattleWorld>();world.SelectedMap=roster.mapId;world.NetworkObject.Spawn();world.Setup(roster);
   world.OnResult+=result=>StartCoroutine(Submit(result));
   bool ready=false;yield return Api.Request<Empty>("POST",Path("ready"),null,v=>ready=true,e=>error=e);
   if(!ready){Abort(error);yield break;}
   Debug.Log("BOUND_SERVER_READY port="+launch.port+" players="+roster.players.Length);
   StartCoroutine(Heartbeat());
  }
  void Approve(NetworkManager.ConnectionApprovalRequest request,NetworkManager.ConnectionApprovalResponse response){
   closedClients.Remove(request.ClientNetworkId);
   response.Pending=true;response.CreatePlayerObject=false;
   if(ending||request.Payload==null||request.Payload.Length>512){response.Approved=false;response.Pending=false;return;}
   string ticket=Encoding.UTF8.GetString(request.Payload);
   StartCoroutine(Validate(request.ClientNetworkId,ticket,response));
  }
  IEnumerator Validate(ulong cid,string ticket,NetworkManager.ConnectionApprovalResponse response){
   RoomMember member=null;
   yield return Api.Request<RoomMember>("POST",Path("consume-ticket"),new TicketBody{ticket=ticket},v=>member=v,e=>{},false);
   if(member==null||world==null||ending||finishing||closedClients.Contains(cid)){response.Approved=false;response.Reason="无法加入对局";response.Pending=false;yield break;}
   var player=world.Find(member.userId);
   if(player==null||player.IsBot.Value||player.Connected.Value||pendingUsers.Contains(member.userId)){
    response.Approved=false;response.Reason="该账号已经连接战斗";response.Pending=false;yield break;
   }
   pendingUsers.Add(member.userId);approved[cid]=member;response.Approved=true;response.Pending=false;
  }
  void Connected(ulong cid){
   if(approved.TryGetValue(cid,out var member)){
    pendingUsers.Remove(member.userId);world.Attach(cid,member);Debug.Log("BOUND_PLAYER_CONNECTED seat="+member.team+":"+member.seat);
   }
  }
  void Disconnected(ulong cid){
   closedClients.Add(cid);
   if(approved.TryGetValue(cid,out var m)){pendingUsers.Remove(m.userId);approved.Remove(cid);world?.Detach(cid,m.userId);}
  }
  IEnumerator Heartbeat(){
   float lastSuccess=Time.realtimeSinceStartup;
   while(!ending&&!finishing){
    bool success=false;
    yield return Api.Request<Empty>("POST",Path("heartbeat"),new HeartbeatBody{playing=world.Phase.Value==2},v=>success=true,e=>{},false);
    if(finishing||ending)yield break;
    if(success)lastSuccess=Time.realtimeSinceStartup;
    else if(Time.realtimeSinceStartup-lastSuccess>=20){Abort("后端心跳超时");yield break;}
    yield return new WaitForSecondsRealtime(success?5:1);
   }
  }
  IEnumerator Submit(BattleResult result){
   if(ending)yield break;
   finishing=true;
   // Freeze gameplay immediately, but retry the idempotent authoritative write.
   bool saved=false;
   for(int attempt=0;attempt<5&&!saved;attempt++){
    yield return Api.Request<Empty>("POST",Path("result"),result,v=>saved=true,e=>{},false);
    if(!saved)yield return new WaitForSecondsRealtime(1);
   }
   ending=true;
   Debug.Log(saved?"BOUND_RESULT_SAVED":"BOUND_RESULT_SAVE_FAILED");
   yield return new WaitForSecondsRealtime(4);Bootstrap.Network.Shutdown();Application.Quit(saved?0:3);
  }
  public void Abort(string reason){if(ending)return;ending=true;Debug.LogError("Battle aborted: "+reason);StartCoroutine(AbortRoutine());}
  IEnumerator AbortRoutine(){
   if(Api!=null&&launch!=null)yield return Api.Request<Empty>("POST",Path("abort"),null,v=>{},e=>{},false);
   Bootstrap.Network.Shutdown();Application.Quit(2);
  }
 }
}
