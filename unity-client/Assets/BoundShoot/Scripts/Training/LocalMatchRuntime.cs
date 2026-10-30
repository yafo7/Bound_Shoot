using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
namespace BoundShoot {
 [Serializable] public sealed class LocalIdentity {public string name;public int skin;}
 public sealed class LocalMatchRuntime:MonoBehaviour {
  public static LocalMatchRuntime Instance;BattleWorld hostWorld;public BattleWorld World=>hostWorld!=null?hostWorld:BattleWorld.Instance;
  public string Error {get;private set;}public bool Ready=>World!=null&&World.IsSpawned;
  public bool IsHost=>network!=null&&network.IsHost;public int TeamSize=>Ready?World.TeamSize.Value:initialSize;int initialSize;bool fillAI=true;
  public bool HasPendingSeats=>pending.Count>0;
  public bool CanStart=>IsHost&&Ready&&!HasPendingSeats&&World.Phase.Value==0&&World.Players.All(p=>p.Connected.Value);
  NetworkManager network;readonly Dictionary<ulong,RoomMember> pending=new Dictionary<ulong,RoomMember>();
  readonly Dictionary<ulong,string> clients=new Dictionary<ulong,string>();
  public IEnumerator Begin(string name,int skin,int teamSize,bool host,string address="127.0.0.1",ushort port=7779,bool aiFill=true){
   Instance=this;initialSize=teamSize;fillAI=aiFill;network=Bootstrap.Network;
   if(network.IsListening){Error="请先退出当前对局";yield break;}
   network.OnClientDisconnectCallback+=Disconnected;
   network.OnClientConnectedCallback+=Connected;
   if(host)network.ConnectionApprovalCallback+=Approve;
   network.NetworkConfig.ConnectionData=Encoding.UTF8.GetBytes(JsonUtility.ToJson(new LocalIdentity{name=name,skin=skin}));
   network.GetComponent<UnityTransport>().SetConnectionData(address,port,host?"0.0.0.0":null);
   if(host){
    if(teamSize<2||teamSize>4){Error="人数设置无效";yield break;}
    if(!network.StartHost()){Error="本地房间启动失败，端口可能被占用";yield break;}
    var roster=new List<RoomMember>();
    for(int t=0;t<2;t++)for(int s=0;s<teamSize;s++)roster.Add(new RoomMember{userId=Guid.NewGuid().ToString(),username=t==0&&s==0?name:"菜园搭档 "+(t*4+s+1),team=t,seat=s,skin=t==0&&s==0?skin:t*4+s,isBot=t==0&&s==0?0:1});
    var map=GameConfig.Current.Map(ForestMap.Id)??ForestMap.Create();var world=Instantiate(Resources.Load<GameObject>("NetWorld")).GetComponent<BattleWorld>();
    hostWorld=world;world.MapOverride=map;world.SelectedMap=map.id;world.ManualStart=true;world.NetworkObject.Spawn();world.Setup(new ServerRoster{mapId=map.id,players=roster.ToArray()});
    world.Attach(NetworkManager.ServerClientId,roster[0]);clients[NetworkManager.ServerClientId]=roster[0].userId;
    world.SetBotFill(fillAI);
   }else{
    if(!network.StartClient()){Error="无法连接本地房间";yield break;}
    float timeout=Time.realtimeSinceStartup+10;
    while(!Ready&&network.IsListening&&Time.realtimeSinceStartup<timeout)yield return null;
    if(!Ready)Error=string.IsNullOrEmpty(network.DisconnectReason)?"连接超时，请确认房间已创建":network.DisconnectReason;
   }
   yield return null;
  }
  void Approve(NetworkManager.ConnectionApprovalRequest request,NetworkManager.ConnectionApprovalResponse response){
   response.CreatePlayerObject=false;response.Pending=false;
   if(request.ClientNetworkId==NetworkManager.ServerClientId){response.Approved=true;return;}
   if(World==null||World.Phase.Value!=0){response.Approved=false;response.Reason="对局已开始";return;}
   LocalIdentity identity=null;try{identity=JsonUtility.FromJson<LocalIdentity>(Encoding.UTF8.GetString(request.Payload));}catch{}
   if(identity==null||!System.Text.RegularExpressions.Regex.IsMatch(identity.name??"","^[a-zA-Z0-9_\u4e00-\u9fff]{1,24}$")){response.Approved=false;response.Reason="名字无效";return;}
   var reserved=pending.Values.Select(v=>v.userId).ToArray();
   var slot=World.Players.Where(p=>p.IsBot.Value&&!reserved.Contains(p.UserId.Value.ToString())).OrderBy(p=>p.Seat.Value).ThenBy(p=>p.Team.Value).FirstOrDefault();
   if(slot==null){response.Approved=false;response.Reason="房间已满";return;}
   var member=World.Roster.players.First(m=>m.userId==slot.UserId.Value.ToString());
   pending[request.ClientNetworkId]=new RoomMember{userId=member.userId,username=identity.name,skin=Mathf.Clamp(identity.skin,0,7),team=member.team,seat=member.seat};response.Approved=true;
  }
  void Connected(ulong client){
   if(!network.IsServer||!pending.TryGetValue(client,out var member))return;pending.Remove(client);
   var player=World.Find(member.userId);player.DisplayName.Value=member.username;player.Skin.Value=member.skin;player.IsBot.Value=false;
   World.Attach(client,member);clients[client]=member.userId;
   var entry=World.Roster.players.First(m=>m.userId==member.userId);entry.username=member.username;entry.skin=member.skin;entry.isBot=0;
  }
  void Disconnected(ulong client){
   pending.Remove(client);
   if(network.IsServer&&clients.TryGetValue(client,out var id)&&World!=null){
    var player=World.Find(id);if(player!=null){player.NetworkObject.RemoveOwnership();player.IsBot.Value=true;player.Connected.Value=World.Phase.Value!=0||fillAI;player.DisconnectAt=-1;player.ResetInput();}
    clients.Remove(client);
   }else if(!network.IsServer&&client==network.LocalClientId)Error=string.IsNullOrEmpty(network.DisconnectReason)?"房主已离开":network.DisconnectReason;
  }
  public void StartRound(){if(CanStart)World.BeginLocalRound();}
  public bool SetTeamSize(int size)=>IsHost&&Ready&&!HasPendingSeats&&World.ConfigureTeamSize(size,fillAI);
  public void FillWithAI(){if(IsHost&&Ready){fillAI=true;World.SetBotFill(true);}}
  public void Restart(){if(IsHost&&Ready)World.RestartLocalRound();}
  public IEnumerator Stop(){
   if(network!=null){
    network.ConnectionApprovalCallback-=Approve;network.OnClientConnectedCallback-=Connected;network.OnClientDisconnectCallback-=Disconnected;
    if(network.IsHost){foreach(ulong client in network.ConnectedClientsIds.ToArray())if(client!=NetworkManager.ServerClientId)network.DisconnectClient(client);yield return new WaitForSecondsRealtime(.15f);}
    else if(network.IsConnectedClient&&World!=null){World.LeaveLocalServerRpc();float leaveBy=Time.realtimeSinceStartup+2;while(network.IsConnectedClient&&Time.realtimeSinceStartup<leaveBy)yield return null;}
    network.Shutdown();float end=Time.realtimeSinceStartup+5;while((network.IsListening||network.ShutdownInProgress)&&Time.realtimeSinceStartup<end)yield return null;
   }
   yield return null;if(Instance==this)Instance=null;Destroy(this);
  }
  void OnDestroy(){if(network!=null){network.ConnectionApprovalCallback-=Approve;network.OnClientConnectedCallback-=Connected;network.OnClientDisconnectCallback-=Disconnected;}if(Instance==this)Instance=null;}
 }
}
