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
  [UnityTest]public IEnumerator DedicatedWorldUsesCatalogMapWithoutLocalOverride(){
   if(Object.FindObjectOfType<Bootstrap>()==null)new GameObject("Dedicated roster bootstrap").AddComponent<Bootstrap>();yield return null;yield return null;
   Bootstrap.Network.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1",17941,"127.0.0.1");
   Assert.IsTrue(Bootstrap.Network.StartServer());
   var world=Object.Instantiate(Resources.Load<GameObject>("NetWorld")).GetComponent<BattleWorld>();
   world.SelectedMap="Depot_01";world.NetworkObject.Spawn();
   var roster=Enumerable.Range(0,8).Select(i=>new RoomMember{userId=Guid.NewGuid().ToString(),username="server_"+i,team=i/4,seat=i%4,skin=i,isBot=i==0?0:1}).ToArray();
   world.Setup(new ServerRoster{mapId="Depot_01",players=roster});
   Assert.AreEqual(8,world.Players.Count);Assert.AreEqual(7,world.Players.Count(p=>p.IsBot.Value));Assert.AreEqual(4,world.TeamSize.Value);
   var map=GameConfig.Current.Map("Depot_01");
   foreach(var p in world.Players)Assert.AreEqual((p.Team.Value==0?map.blueSpawns:map.redSpawns)[p.Seat.Value],p.Body.position);
   Assert.AreEqual(map.objectives.zones.Length,world.Objectives.zones.Length);yield return null;
  }
  [UnityTest] public IEnumerator ManualRoomsSurviveNinetySecondWaitBeforeFirstStartAndRematch(){
   if(Object.FindObjectOfType<Bootstrap>()==null)new GameObject("Wait regression bootstrap").AddComponent<Bootstrap>();yield return null;yield return null;
   foreach(int size in new[]{2,4}){
    var runtime=new GameObject("Long waiting room").AddComponent<LocalMatchRuntime>();yield return runtime.Begin("wait_test",0,size,true,"127.0.0.1",(ushort)(17882+size));
    var world=runtime.World;
    for(int round=0;round<2;round++){
     Time.timeScale=20;float before=Time.time;yield return new WaitForSeconds(90);Assert.GreaterOrEqual(Time.time-before,89.999f);
     Assert.AreEqual(0,world.Phase.Value);runtime.StartRound();yield return WaitFor(()=>world.Phase.Value==2);Time.timeScale=1;
     for(int zone=0;zone<world.ZoneHeat.Count;zone++)world.ZoneHeat[zone]=100;
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
   string originalMap=world.MapKey.Value.ToString();
   string newMap=teamSize==2?"Orchard_01":teamSize==3?"TwinGates_01":"Depot_01";
   Assert.IsFalse(runtime.SelectMap("unknown_map"));
   Assert.IsTrue(runtime.SelectMap(newMap));
   var layout=GameConfig.Current.Map(newMap);
   Assert.AreEqual(layout.pickups.Length,world.Supplies.Count);
   Assert.AreEqual(layout.objectives.zones.Length,world.Objectives.zones.Length);
   Assert.IsTrue(world.Players.All(p=>p.Body.position==(p.Team.Value==0?layout.blueSpawns:layout.redSpawns)[p.Seat.Value]));
   yield return WaitFor(()=>peers.All(peer=>peer.SpawnManager.SpawnedObjectsList
    .Select(n=>n.GetComponent<BattleWorld>()).Any(w=>w!=null&&w.MapKey.Value.ToString()==newMap&&w.Objectives.zones.Length==layout.objectives.zones.Length)));
   Assert.IsTrue(runtime.SelectMap(originalMap));
   runtime.StartRound();yield return WaitFor(()=>world.Phase.Value==2);
   Assert.IsFalse(runtime.SelectMap(newMap));
   // Verify actual client-to-server input, not a direct write into server movement.
   var clientPeer=peers[0];yield return WaitFor(()=>clientPeer.SpawnManager.SpawnedObjectsList.OfType<NetworkObject>().Any(n=>n.GetComponent<NetPlayer>()!=null&&n.IsOwner));
   var clientPlayer=clientPeer.SpawnManager.SpawnedObjectsList.Select(n=>n.GetComponent<NetPlayer>()).First(p=>p!=null&&p.IsOwner);
   string uid=clientPlayer.UserId.Value.ToString();var authority=world.Find(uid);Vector2 before=authority.Body.position;
   for(int i=0;i<15;i++){clientPlayer.SubmitInput(Vector2.right,Vector2.right,false,false);yield return new WaitForFixedUpdate();}
   Assert.Greater(authority.Body.position.x,before.x+.2f);
   // Switching preserves HP, shield and cooldown; same weapon is a no-op.
   authority.Health.Value=25;authority.Shield.Value=1;Assert.IsTrue(authority.Equip(WeaponKind.AK));Assert.AreEqual(25,authority.Health.Value);
   float cooldown=authority.NextShot;Assert.IsTrue(authority.Equip(WeaponKind.Sniper));Assert.AreEqual(25,authority.Health.Value);Assert.AreEqual(GameConfig.Current.baseHealth,authority.MaxHealth.Value);Assert.GreaterOrEqual(authority.NextShot,cooldown);
   Assert.IsTrue(authority.Equip(WeaponKind.AK));Assert.AreEqual(25,authority.Health.Value);Assert.AreEqual(GameConfig.Current.baseHealth,authority.MaxHealth.Value);Assert.AreEqual(1,authority.Shield.Value);Assert.IsFalse(authority.Equip(WeaponKind.AK));
   authority.Ammo.Value=1;authority.Aim=Vector2.up;authority.NextShot=0;
   yield return WaitFor(()=>clientPlayer.Ammo.Value==1&&clientPlayer.Weapon.Value==(int)WeaponKind.AK);
   Assert.IsTrue(world.TryAttack(authority));
   yield return WaitFor(()=>clientPlayer.Weapon.Value==(int)WeaponKind.Dagger&&clientPlayer.Ammo.Value==0);
   Assert.AreEqual(25,clientPlayer.Health.Value);Assert.AreEqual(GameConfig.Current.baseHealth,clientPlayer.MaxHealth.Value);
   Assert.IsTrue(world.TryCollectItem(authority,ItemKind.Heal));Assert.IsTrue(world.TryCollectItem(authority,ItemKind.Bomb));Assert.IsTrue(world.TryCollectItem(authority,ItemKind.Shield));
   yield return WaitFor(()=>clientPlayer.InventoryCount==3);clientPlayer.SubmitInput(Vector2.zero,Vector2.right,false,true);
   yield return WaitFor(()=>clientPlayer.SelectedSlot.Value==1);Assert.AreEqual(25,authority.Health.Value);
   clientPlayer.SubmitInput(Vector2.zero,Vector2.right,true,false,true);
   int expectedHeal=Mathf.Min(GameConfig.Current.baseHealth,25+GameConfig.Current.healAmount);
   yield return WaitFor(()=>clientPlayer.SelectedSlot.Value==0&&clientPlayer.InventoryCount==2&&clientPlayer.Health.Value==expectedHeal);
   clientPlayer.SubmitInput(Vector2.zero,Vector2.right,true,false);yield return new WaitForFixedUpdate();Assert.IsFalse(authority.Fire);
   clientPlayer.SubmitInput(Vector2.zero,Vector2.right,false,false);
   // Verify throwable input and full flight/area snapshot state on real clients.
   authority.ClearInventory();var remoteWorld=clientPeer.SpawnManager.SpawnedObjectsList.Select(n=>n.GetComponent<BattleWorld>()).First(w=>w!=null);
   foreach(var kind in new[]{ItemKind.Smoke,ItemKind.Incendiary}){
    Assert.IsTrue(world.TryCollectItem(authority,kind));yield return WaitFor(()=>clientPlayer.ItemAt(1)==(int)kind);
    clientPlayer.SubmitInput(Vector2.zero,Vector2.up,false,true);yield return WaitFor(()=>clientPlayer.SelectedSlot.Value==1);
    clientPlayer.SubmitInput(Vector2.zero,Vector2.up,true,false,true);yield return WaitFor(()=>clientPlayer.InventoryCount==0&&remoteWorld.Throwables.Any(a=>a.kind==(int)kind));
    var serverArea=world.Throwables.Last();var remoteArea=remoteWorld.Throwables.Single(a=>a.id==serverArea.id);Assert.AreEqual(serverArea.landing,remoteArea.landing);Assert.AreEqual(serverArea.landAt,remoteArea.landAt,.001);Assert.AreEqual(serverArea.endAt,remoteArea.endAt,.001);
    clientPlayer.SubmitInput(Vector2.zero,Vector2.up,false,false);
   }
   yield return WaitFor(()=>remoteWorld.Throwables.All(a=>a.Active(remoteWorld.AreaTime)));
   BattleResult result=null;world.OnResult+=r=>result=r;
   for(int zone=0;zone<world.ZoneHeat.Count;zone++)world.ZoneHeat[zone]=100;
   yield return WaitFor(()=>world.Phase.Value==3);Assert.IsNotNull(result);Assert.AreEqual(0,result.winner);Assert.AreEqual(teamSize*2,result.players.Length);
   yield return WaitFor(()=>peers.All(p=>p.SpawnManager.SpawnedObjectsList.Select(n=>n.GetComponent<BattleWorld>()).Any(w=>w!=null&&w.Phase.Value==3)));
   runtime.Restart();Assert.AreEqual(0,world.Throwables.Count);Assert.AreEqual(0,world.Phase.Value);Assert.IsTrue(world.Players.All(p=>p.Alive&&p.Weapon.Value==(int)WeaponKind.Dagger&&p.Deaths.Value==0));
   runtime.StartRound();yield return WaitFor(()=>world.Phase.Value==2);Assert.AreEqual(teamSize*2,Bootstrap.Network.ConnectedClients.Count);
   foreach(var peer in peers){Bootstrap.Network.DisconnectClient(peer.LocalClientId);yield return WaitFor(()=>!peer.IsConnectedClient);peer.Shutdown(true);yield return WaitFor(()=>!peer.IsListening&&!peer.ShutdownInProgress);}
   yield return new WaitForSecondsRealtime(.15f);
   yield return runtime.Stop();Assert.IsFalse(Bootstrap.Network.IsListening);
  }
  static IEnumerator WaitFor(Func<bool> predicate){float end=Time.realtimeSinceStartup+15;while(!predicate()&&Time.realtimeSinceStartup<end)yield return null;Assert.IsTrue(predicate(),"Local networking timed out");}
  [UnityTearDown] public IEnumerator Cleanup(){Time.timeScale=1;foreach(var peer in peers)if(peer!=null){peer.Shutdown(true);yield return WaitFor(()=>!peer.IsListening&&!peer.ShutdownInProgress);Object.Destroy(peer.gameObject);yield return null;}peers.Clear();if(LocalMatchRuntime.Instance!=null)yield return LocalMatchRuntime.Instance.Stop();if(Bootstrap.Network!=null&&Bootstrap.Network.IsListening){Bootstrap.Network.Shutdown();yield return WaitFor(()=>!Bootstrap.Network.IsListening&&!Bootstrap.Network.ShutdownInProgress);}}
 }
}
