using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace BoundShoot.Tests {
 public class LocalMatchTests {
  readonly List<NetworkManager> peers=new List<NetworkManager>();
  [UnityTest] public IEnumerator ManualRoomsSurviveNinetySecondWaitBeforeFirstStartAndRematch(){
   if(Object.FindObjectOfType<Bootstrap>()==null)new GameObject("Wait regression bootstrap").AddComponent<Bootstrap>();yield return null;yield return null;
   foreach(int size in new[]{2,4}){
    var runtime=new GameObject("Long waiting room").AddComponent<LocalMatchRuntime>();yield return runtime.Begin("wait_test",0,size,true,"127.0.0.1",(ushort)(17882+size));
    var world=runtime.World;
    for(int round=0;round<2;round++){
     Time.timeScale=20;float before=Time.time;yield return new WaitForSeconds(90);Assert.GreaterOrEqual(Time.time-before,90);
     Assert.AreEqual(0,world.Phase.Value);runtime.StartRound();yield return WaitFor(()=>world.Phase.Value==2);Time.timeScale=1;
     foreach(var p in world.Players.Where(p=>p.Team.Value==1)){p.ProtectionUntil=0;p.Hurt(999,null);}
     yield return WaitFor(()=>world.Phase.Value==3);if(round==0)runtime.Restart();
    }
    yield return runtime.Stop();
   }
  }
  [UnityTest] public IEnumerator AiTeamsPlayAnEntireRoundFromSpawn(){
   if(Object.FindObjectOfType<Bootstrap>()==null)new GameObject("AI match bootstrap").AddComponent<Bootstrap>();yield return null;yield return null;
   var runtime=new GameObject("AI complete round").AddComponent<LocalMatchRuntime>();yield return runtime.Begin("yafo777",6,2,true,"127.0.0.1",17881);
   var world=runtime.World;foreach(var p in world.Players)p.IsBot.Value=true;
   BattleResult result=null;world.OnResult+=r=>result=r;runtime.StartRound();Time.timeScale=8;
   float until=Time.realtimeSinceStartup+35;
   while(world.Phase.Value!=3&&Time.realtimeSinceStartup<until)yield return null;
   Time.timeScale=1;Assert.AreEqual(3,world.Phase.Value,"AI round did not finish");Assert.IsNotNull(result);Assert.AreEqual(4,result.players.Length);
   Assert.IsTrue(result.players.Any(p=>p.damage>0),"Teams never engaged");Assert.Greater(result.duration,5);
   yield return runtime.Stop();
  }
  [UnityTest] public IEnumerator FourNetworkPeersCanPlaySettleAndReplay(){yield return Exercise(2,17879);}
  [UnityTest] public IEnumerator SixNetworkPeersCanPlaySettleAndReplay(){yield return Exercise(3,17886);}
  [UnityTest] public IEnumerator EightNetworkPeersCanPlaySettleAndReplay(){yield return Exercise(4,17880);}
  IEnumerator Exercise(int teamSize,ushort port){
   if(Object.FindObjectOfType<Bootstrap>()==null)new GameObject("Local test bootstrap").AddComponent<Bootstrap>();yield return null;yield return null;
   var runtime=new GameObject("Local test session").AddComponent<LocalMatchRuntime>();
   yield return runtime.Begin("yafo777",7,teamSize,true,"127.0.0.1",port);Assert.IsNull(runtime.Error);Assert.IsTrue(runtime.Ready);
   var world=runtime.World;Assert.AreEqual(teamSize*2,world.Players.Count);Assert.AreEqual((teamSize-1)*2,world.RopeCount);Assert.AreEqual(0,world.Phase.Value);
   for(int i=1;i<teamSize*2;i++){
    var go=new GameObject("Real network peer "+i);var utp=go.AddComponent<UnityTransport>();var peer=go.AddComponent<NetworkManager>();
    peer.NetworkConfig=new NetworkConfig{NetworkTransport=utp,EnableSceneManagement=false,ConnectionApproval=true,ForceSamePrefabs=true,TickRate=30};
    peer.AddNetworkPrefab(Resources.Load<GameObject>("NetPlayer"));peer.AddNetworkPrefab(Resources.Load<GameObject>("NetWorld"));
    peer.NetworkConfig.ConnectionData=Encoding.UTF8.GetBytes(JsonUtility.ToJson(new LocalIdentity{name="peer"+i,skin=i}));
    utp.SetConnectionData("127.0.0.1",port);peers.Add(peer);Assert.IsTrue(peer.StartClient());
    yield return WaitFor(()=>peer.IsConnectedClient);yield return null;
   }
   Assert.AreEqual(teamSize*2,Bootstrap.Network.ConnectedClients.Count);Assert.IsTrue(world.Players.All(p=>!p.IsBot.Value));
   CollectionAssert.AreEquivalent(Enumerable.Range(1,teamSize*2-1).Concat(new[]{7}),world.Players.Select(p=>p.Skin.Value));
   runtime.StartRound();yield return WaitFor(()=>world.Phase.Value==2);
   // Verify actual client-to-server input, not a direct write into server movement.
   var clientPeer=peers[0];yield return WaitFor(()=>clientPeer.SpawnManager.SpawnedObjectsList.OfType<NetworkObject>().Any(n=>n.GetComponent<NetPlayer>()!=null&&n.IsOwner));
   var clientPlayer=clientPeer.SpawnManager.SpawnedObjectsList.Select(n=>n.GetComponent<NetPlayer>()).First(p=>p!=null&&p.IsOwner);
   string uid=clientPlayer.UserId.Value.ToString();var authority=world.Find(uid);Vector2 before=authority.Body.position;
   for(int i=0;i<15;i++){clientPlayer.SubmitInput(Vector2.right,Vector2.right,false,false);yield return new WaitForFixedUpdate();}
   Assert.Greater(authority.Body.position.x,before.x+.2f);
   // Switching preserves HP, shield and cooldown; same weapon is a no-op.
   authority.Health.Value=7;authority.Shield.Value=1;Assert.IsTrue(authority.Equip(WeaponKind.AK));Assert.AreEqual(7,authority.Health.Value);
   float cooldown=authority.NextShot;Assert.IsTrue(authority.Equip(WeaponKind.Sniper));Assert.AreEqual(3,authority.Health.Value);Assert.GreaterOrEqual(authority.NextShot,cooldown);
   Assert.IsTrue(authority.Equip(WeaponKind.AK));Assert.AreEqual(3,authority.Health.Value);Assert.AreEqual(1,authority.Shield.Value);Assert.IsFalse(authority.Equip(WeaponKind.AK));
   BattleResult result=null;world.OnResult+=r=>result=r;
   foreach(var player in world.Players.Where(p=>p.Team.Value==1)){player.ProtectionUntil=0;player.Hurt(999,null);}
   yield return WaitFor(()=>world.Phase.Value==3);Assert.IsNotNull(result);Assert.AreEqual(0,result.winner);Assert.AreEqual(teamSize*2,result.players.Length);
   yield return WaitFor(()=>peers.All(p=>p.SpawnManager.SpawnedObjectsList.Select(n=>n.GetComponent<BattleWorld>()).Any(w=>w!=null&&w.Phase.Value==3)));
   runtime.Restart();Assert.AreEqual(0,world.Phase.Value);Assert.IsTrue(world.Players.All(p=>p.Alive&&p.Weapon.Value==0&&p.Deaths.Value==0));
   runtime.StartRound();yield return WaitFor(()=>world.Phase.Value==2);Assert.AreEqual(teamSize*2,Bootstrap.Network.ConnectedClients.Count);
   foreach(var peer in peers){Bootstrap.Network.DisconnectClient(peer.LocalClientId);yield return WaitFor(()=>!peer.IsConnectedClient);peer.Shutdown(true);yield return WaitFor(()=>!peer.IsListening&&!peer.ShutdownInProgress);}
   yield return new WaitForSecondsRealtime(.15f);
   yield return runtime.Stop();Assert.IsFalse(Bootstrap.Network.IsListening);
  }
  static IEnumerator WaitFor(Func<bool> predicate){float end=Time.realtimeSinceStartup+15;while(!predicate()&&Time.realtimeSinceStartup<end)yield return null;Assert.IsTrue(predicate(),"Local networking timed out");}
  [UnityTearDown] public IEnumerator Cleanup(){Time.timeScale=1;foreach(var peer in peers)if(peer!=null){peer.Shutdown(true);yield return WaitFor(()=>!peer.IsListening&&!peer.ShutdownInProgress);Object.Destroy(peer.gameObject);yield return null;}peers.Clear();if(LocalMatchRuntime.Instance!=null)yield return LocalMatchRuntime.Instance.Stop();if(Bootstrap.Network!=null&&Bootstrap.Network.IsListening){Bootstrap.Network.Shutdown();yield return WaitFor(()=>!Bootstrap.Network.IsListening&&!Bootstrap.Network.ShutdownInProgress);}}
 }
}
