using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
namespace BoundShoot {
 public sealed class TrainingRuntime:MonoBehaviour {
  public static TrainingRuntime Instance;public BattleWorld World {get;private set;}
  public bool Ready {get;private set;}public string Error {get;private set;}
  readonly Dictionary<NetPlayer,float> targetDeaths=new Dictionary<NetPlayer,float>();
  NetworkManager network;
  public int Teammates=>World==null?0:World.Players.Count(p=>p.TrainingRole==1);
  public IEnumerator Begin(string name,int skin=0){
   Instance=this;network=Bootstrap.Network;
   if(network.IsListening){Error="请先退出当前对局";yield break;}
   network.ConnectionApprovalCallback+=Approve;
   network.NetworkConfig.ConnectionData=Array.Empty<byte>();
   network.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1",0,"127.0.0.1");
   if(!network.StartHost()){Error="训练房启动失败";yield break;}
   var map=TrainingMap.Create();map.theme="meadow";var human=new RoomMember{userId=Guid.NewGuid().ToString(),username=name,skin=skin,team=0,seat=0};
   var roster=new ServerRoster{mapId=map.id,players=new[]{human,
    new RoomMember{userId=Guid.NewGuid().ToString(),username="靶子 1",team=1,seat=0,trainingRole=2},
    new RoomMember{userId=Guid.NewGuid().ToString(),username="靶子 2",team=1,seat=1,trainingRole=2}}};
   World=Instantiate(Resources.Load<GameObject>("NetWorld")).GetComponent<BattleWorld>();
   World.Training=true;World.MapOverride=map;World.SelectedMap=map.id;World.NetworkObject.Spawn();World.Setup(roster);
   World.Attach(NetworkManager.ServerClientId,human);Ready=true;
   yield return null;
  }
  void Approve(NetworkManager.ConnectionApprovalRequest request,NetworkManager.ConnectionApprovalResponse response){
   response.CreatePlayerObject=false;response.Approved=request.ClientNetworkId==NetworkManager.ServerClientId;response.Pending=false;
  }
  void FixedUpdate(){
   if(!Ready||World==null||!network.IsServer)return;
   foreach(var p in World.Players){
    if(p.TrainingRole==0)continue;
    p.Move=Vector2.zero;p.Fire=p.Use=false;p.DashUntil=0;p.LastInput=Time.time;
    if(p.TrainingRole==2){
     if(!p.Alive){
      if(!targetDeaths.ContainsKey(p))targetDeaths[p]=Time.time;
      if(Time.time-targetDeaths[p]>=2){p.Resurrect();p.ProtectionUntil=0;p.Body.position=World.MapOverride.redSpawns[p.Seat.Value];p.Body.velocity=Vector2.zero;World.ResetRopesForTeleport(p);targetDeaths.Remove(p);}
     }else targetDeaths.Remove(p);
    }
   }
  }
  public void AddTeammate(){if(Ready)World.AddTrainingTeammate();}
  public void RemoveTeammate(){if(Ready)World.RemoveTrainingTeammate();}
  public void ResetRoom(){if(Ready){World.ResetTraining();targetDeaths.Clear();}}
  public IEnumerator Stop(){
   Ready=false;if(network!=null){network.ConnectionApprovalCallback-=Approve;network.Shutdown();float end=Time.realtimeSinceStartup+5;while((network.IsListening||network.ShutdownInProgress)&&Time.realtimeSinceStartup<end)yield return null;}
   yield return null;if(Instance==this)Instance=null;Destroy(this);
  }
  void OnDestroy(){if(network!=null)network.ConnectionApprovalCallback-=Approve;if(Instance==this)Instance=null;}
 }
}
