using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace BoundShoot.Tests {
 public class ForestRulesPlayTests {
  LocalMatchRuntime runtime;TrainingRuntime training;GameObject cameraObject;
  Unity.Netcode.NetworkManager warmupNetwork;
  [UnityTest]public IEnumerator OnlineWarmupRequiresTheRoomHostAndClearsPracticeState(){
   if(Object.FindObjectOfType<Bootstrap>()==null)new GameObject("Warmup bootstrap").AddComponent<Bootstrap>();
   yield return null;
   yield return null;
   warmupNetwork=Bootstrap.Network;
   warmupNetwork.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>().SetConnectionData("127.0.0.1",17921);
   Assert.IsTrue(warmupNetwork.StartHost());
   var world=Object.Instantiate(Resources.Load<GameObject>("NetWorld")).GetComponent<BattleWorld>();
   world.SelectedMap=TrainingMap.WarmupId;
   world.NetworkObject.Spawn();
   var human=new RoomMember{userId="warmup-player",username="Player",team=0,seat=0};
   var roster=new ServerRoster{mapId="Depot_01",warmupHostId="other-player",players=new[]{
    human,new RoomMember{userId="warmup-bot",username="Bot",team=1,seat=0,isBot=1}
   }};
   world.Setup(roster);
   yield return new WaitForFixedUpdate();
   Assert.AreEqual(0,world.Phase.Value);
   world.Attach(warmupNetwork.LocalClientId,human);
   yield return new WaitForFixedUpdate();
   yield return null;
   Assert.AreEqual(2,world.Phase.Value);
   Assert.AreEqual(5,world.Supplies.Count(p=>p.weapon!=0));
   Assert.AreEqual(10,world.Supplies.Count(p=>p.item!=0));
   world.StartOnlineBattleServerRpc();
   Assert.IsTrue(world.IsOnlineWarmup);
   var player=world.Find(human.userId);
   player.Equip(WeaponKind.RocketLauncher);
   player.Health.Value=1;
   player.Kills.Value=3;
   roster.warmupHostId=human.userId;
   world.StartOnlineBattleServerRpc();
   Assert.AreEqual("Depot_01",world.MapKey.Value.ToString());
   Assert.AreEqual(1,world.Phase.Value);
   Assert.AreEqual((int)WeaponKind.Dagger,player.Weapon.Value);
   Assert.AreEqual(player.MaxHealth.Value,player.Health.Value);
   Assert.AreEqual(0,player.Kills.Value);
  }
  [UnityTest]public IEnumerator ResizeSwapRescueAndWipeRespawnUseAuthoritativeState(){
   if(Object.FindObjectOfType<Bootstrap>()==null)new GameObject("Forest rules bootstrap").AddComponent<Bootstrap>();yield return null;yield return null;
   runtime=new GameObject("Forest rules").AddComponent<LocalMatchRuntime>();yield return runtime.Begin("forest_test",6,2,true,"127.0.0.1",17910);
   Assert.IsTrue(runtime.SetTeamSize(3));Assert.AreEqual(6,runtime.World.Players.Count);Assert.AreEqual(4,runtime.World.RopeCount);
   Assert.IsTrue(runtime.SetTeamSize(4));Assert.AreEqual(8,runtime.World.Players.Count);Assert.IsTrue(runtime.SetTeamSize(3));
   var world=runtime.World;var human=world.Players.Single(p=>!p.IsBot.Value);world.RequestSeatServerRpc(1,1);Assert.AreEqual(1,human.Team.Value);Assert.AreEqual(1,human.Seat.Value);
   foreach(var p in world.Players)p.IsBot.Value=false;
   Time.timeScale=8;runtime.StartRound();float until=Time.realtimeSinceStartup+10;while(world.Phase.Value!=2&&Time.realtimeSinceStartup<until)yield return null;Assert.AreEqual(2,world.Phase.Value);
   var team=world.Players.Where(p=>p.Team.Value==human.Team.Value).ToArray();
   var rescuer=team.First(p=>p!=human);
   human.ProtectionUntil=0;human.Hurt(999,null);
   yield return new WaitForSeconds(5.2f);Assert.IsFalse(human.Alive);
   var corpse=human.Body.position;
   rescuer.Body.position=corpse+Vector2.right*.85f;rescuer.Body.velocity=Vector2.zero;
   world.ResetRopesForTeleport(rescuer);
   float rescueUntil=Time.time+3;
   while(!human.Alive&&Time.time<rescueUntil){
    rescuer.RescueHeld=true;rescuer.LastInput=Time.time;
    yield return new WaitForFixedUpdate();
   }
   Assert.IsTrue(human.Alive);
   Assert.AreEqual(Mathf.RoundToInt(human.MaxHealth.Value*.5f),human.Health.Value);
   var config=GameConfig.Current;
   bool previousOverride=config.overrideTeamWipeSeconds;
   float previousDelay=config.reviveSeconds;
   try{
    config.overrideTeamWipeSeconds=true;
    config.reviveSeconds=2;
    foreach(var p in team){p.ProtectionUntil=0;p.Hurt(999,null);}
    yield return new WaitForSeconds(1);Assert.AreEqual(2,world.Phase.Value);Assert.IsTrue(team.All(p=>!p.Alive));
    yield return new WaitForSeconds(1.3f);Assert.IsTrue(team.All(p=>p.Alive&&p.Health.Value==p.MaxHealth.Value));
   }finally{
    config.overrideTeamWipeSeconds=previousOverride;
    config.reviveSeconds=previousDelay;
   }
   Assert.AreEqual(2,world.Phase.Value);
   var zone=world.Objectives.zones[0];
   var blue=world.Players.Where(p=>p.Team.Value==0).ToArray();
   var red=world.Players.Where(p=>p.Team.Value==1).ToArray();
   foreach(var p in blue)p.Body.position=zone.center;
   foreach(var p in red)p.Body.position=zone.center;
   red[0].ProtectionUntil=0;red[0].Hurt(999,null);
   float blueBefore=world.BlueScore.Value,redBefore=world.RedScore.Value;
   var scoreTick=typeof(BattleWorld).GetMethod("TickControlZones",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
   scoreTick.Invoke(world,new object[]{1f});
   Assert.AreEqual(blueBefore+zone.pointsPerPlayer/world.ZoneHeat.Count,world.BlueScore.Value,.001f);
   Assert.AreEqual(redBefore,world.RedScore.Value,.001f);
   foreach(var p in blue)p.Body.position=zone.center+Vector2.up*(zone.radius+1);
   red[0].Resurrect();
   float heatBefore=world.ZoneHeat[0];
   scoreTick.Invoke(world,new object[]{1f});
   Assert.Less(world.ZoneHeat[0],heatBefore,"Red pressure must remove blue heat from the same hotspot.");
  }
  [UnityTest]public IEnumerator PassiveLivingAndDeadPartnersCanBeDraggedAndCameraStaysCentred(){
   if(Object.FindObjectOfType<Bootstrap>()==null)new GameObject("Drag bootstrap").AddComponent<Bootstrap>();yield return null;yield return null;
   training=new GameObject("Drag practice").AddComponent<TrainingRuntime>();yield return training.Begin("drag_test",0,2);
   var world=training.World;var leader=world.Players.Single(p=>p.TrainingRole==0);var partner=world.Players.Single(p=>p.TrainingRole==1);leader.enabled=false;
   foreach(var p in world.Players.Where(p=>p.Team.Value==0)){p.Body.position=new Vector2(p==leader?-6:-10,-6);p.transform.position=p.Body.position;world.ResetRopesForTeleport(p);}
   float before=partner.Body.position.x;for(int i=0;i<60;i++){leader.Move=Vector2.right;leader.LastInput=Time.time;yield return new WaitForFixedUpdate();}
   Assert.Greater(partner.Body.position.x-before,2,"An idle live teammate must follow a moving leader");
   partner.ProtectionUntil=0;partner.Hurt(999,null);before=partner.Body.position.x;
   for(int i=0;i<45;i++){leader.Move=Vector2.right;leader.LastInput=Time.time;yield return new WaitForFixedUpdate();}
   Assert.IsFalse(partner.Alive);Assert.Greater(partner.Body.position.x-before,1,"The corpse remains a draggable chain member");
   world.enabled=false;foreach(var p in world.Players)p.Body.simulated=false;
   var camera=Camera.main;if(camera==null){cameraObject=new GameObject("Centre verification camera");cameraObject.tag="MainCamera";camera=cameraObject.AddComponent<Camera>();camera.orthographic=true;}
   if(camera.GetComponent<MeadowCamera>()==null)camera.gameObject.AddComponent<MeadowCamera>();
   foreach(var position in new[]{new Vector2(-17,-11),new Vector2(17,11)}){
    leader.transform.position=position;yield return null;yield return null;var view=camera.WorldToViewportPoint(MeadowCamera.Anchor(leader));Assert.AreEqual(.5f,view.x,.0001f);Assert.AreEqual(.5f,view.y,.0001f);
   }
  }
  [UnityTearDown]public IEnumerator Cleanup(){Time.timeScale=1;if(warmupNetwork!=null){warmupNetwork.Shutdown();yield return null;warmupNetwork=null;}if(runtime!=null)yield return runtime.Stop();if(training!=null)yield return training.Stop();if(cameraObject!=null)Object.Destroy(cameraObject);}
 }
}
