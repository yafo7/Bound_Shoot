using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace BoundShoot.Tests {
 public class ForestRulesPlayTests {
  LocalMatchRuntime runtime;TrainingRuntime training;GameObject cameraObject;
  [UnityTest]public IEnumerator ResizeSwapAndAutomaticRevivalUseAuthoritativeState(){
   if(Object.FindObjectOfType<Bootstrap>()==null)new GameObject("Forest rules bootstrap").AddComponent<Bootstrap>();yield return null;yield return null;
   runtime=new GameObject("Forest rules").AddComponent<LocalMatchRuntime>();yield return runtime.Begin("forest_test",6,2,true,"127.0.0.1",17910);
   Assert.IsTrue(runtime.SetTeamSize(3));Assert.AreEqual(6,runtime.World.Players.Count);Assert.AreEqual(4,runtime.World.RopeCount);
   Assert.IsTrue(runtime.SetTeamSize(4));Assert.AreEqual(8,runtime.World.Players.Count);Assert.IsTrue(runtime.SetTeamSize(3));
   var world=runtime.World;var human=world.Players.Single(p=>!p.IsBot.Value);world.RequestSeatServerRpc(1,1);Assert.AreEqual(1,human.Team.Value);Assert.AreEqual(1,human.Seat.Value);
   foreach(var p in world.Players)p.IsBot.Value=false;
   Time.timeScale=8;runtime.StartRound();float until=Time.realtimeSinceStartup+10;while(world.Phase.Value!=2&&Time.realtimeSinceStartup<until)yield return null;Assert.AreEqual(2,world.Phase.Value);
   human.ProtectionUntil=0;human.Hurt(999,null);Vector2 deathPosition=human.Body.position;yield return new WaitForSeconds(4);Assert.IsFalse(human.Alive);yield return new WaitForSeconds(1.2f);Assert.IsTrue(human.Alive);Assert.Less(Vector2.Distance(deathPosition,human.Body.position),.2f);
   human.ProtectionUntil=0;human.Hurt(999,null);human.DeathAt=Time.time-6;
   foreach(var p in world.Players.Where(p=>p.Team.Value==human.Team.Value&&p.Alive)){p.ProtectionUntil=0;p.Hurt(999,null);}
   yield return new WaitForFixedUpdate();yield return null;Assert.AreEqual(3,world.Phase.Value);Assert.IsFalse(human.Alive,"Full wipe must win over a due revival");
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
  [UnityTearDown]public IEnumerator Cleanup(){Time.timeScale=1;if(runtime!=null)yield return runtime.Stop();if(training!=null)yield return training.Stop();if(cameraObject!=null)Object.Destroy(cameraObject);}
 }
}
