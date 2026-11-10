using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace BoundShoot.Tests {
 public class ThrowablePlayTests {
  TrainingRuntime runtime;NetPlayer player,enemy,ally;bool oldRender,oldFriendly;BattleWorld World=>runtime.World;
  [UnitySetUp]public IEnumerator Setup(){
   oldRender=Draw2D.RenderInBatch;Draw2D.RenderInBatch=true;oldFriendly=GameConfig.Current.friendlyFire;GameConfig.Current.friendlyFire=false;
   if(Object.FindObjectOfType<Bootstrap>()==null)new GameObject("Throwable bootstrap").AddComponent<Bootstrap>();yield return null;yield return null;
   if(Camera.main==null){var camera=new GameObject("Throwable test camera");camera.tag="MainCamera";camera.AddComponent<Camera>().orthographic=true;camera.transform.position=Vector3.back*20;}
   if(Object.FindObjectOfType<AudioListener>()==null)Camera.main.gameObject.AddComponent<AudioListener>();
   var client=GameClient.Instance;client.EnterName("throwable_test");client.StartTraining();yield return Wait(()=>client.Screen=="Training");runtime=TrainingRuntime.Instance;
   ally=World.AddTrainingTeammate();runtime.enabled=false;World.enabled=false;foreach(var p in World.Players){p.enabled=false;p.Body.simulated=false;p.ProtectionUntil=0;p.Shield.Value=0;p.Health.Value=100;}
   player=World.Players.Single(p=>p.TrainingRole==0);enemy=World.Players.First(p=>p.TrainingRole==2);Move(player,new Vector2(-8,-6));Move(enemy,new Vector2(0,-6));Move(ally,new Vector2(1,-6));
  }
  static void Move(NetPlayer p,Vector2 point){p.Body.position=point;p.transform.position=point;p.Position.Value=point;}
  void Invoke(object target,string method,params object[] args)=>target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);
  ThrowableState Throw(ItemKind kind){Assert.IsTrue(World.TryCollectItem(player,kind));player.SelectNextSlot();player.Aim=Vector2.right;Assert.IsTrue(World.TryUseSelected(player));return World.Throwables.Last();}
  void Land(ThrowableState a){a.landAt=World.AreaTime-1;a.endAt=World.AreaTime+20;}
  void Burn(float dt=.5f)=>Invoke(World,"TickThrowables",dt);
  [UnityTest]public IEnumerator StoredThrowableUsesOnceFliesFixedDistanceAndResets(){
   World.TryCollectItem(player,ItemKind.Smoke);Assert.AreEqual(0,World.Throwables.Count);player.SubmitInput(Vector2.zero,Vector2.right,false,true);
   player.SubmitInput(Vector2.zero,Vector2.right,true,false,true);Assert.AreEqual(1,World.Throwables.Count);var a=World.Throwables[0];
   Assert.AreEqual(new Vector2(0,-6),a.landing);Assert.AreEqual(.65,a.landAt-a.launchedAt,.001);Assert.AreEqual(8,a.endAt-a.landAt,.001);Assert.AreEqual(0,player.InventoryCount);Assert.AreEqual(0,player.SelectedSlot.Value);
   player.SubmitInput(Vector2.zero,Vector2.right,true,false,true);Assert.AreEqual(1,World.Throwables.Count);Assert.IsFalse(player.Fire);Assert.IsFalse(World.SmokeBlocks(player.Body.position,enemy.Body.position));
   Land(a);Assert.IsTrue(World.SmokeBlocks(player.Body.position,enemy.Body.position));World.ResetTraining();Assert.AreEqual(0,World.Throwables.Count);yield return null;
  }
  [UnityTest]public IEnumerator FireHasReducedFriendlyAndSelfDamageWithoutStackingOrTeamCredit(){
   var a=Throw(ItemKind.Incendiary);var b=Throw(ItemKind.Incendiary);Land(a);Land(b);Move(player,new Vector2(-1,-6));
   Burn();Burn();Assert.AreEqual(90,enemy.Health.Value);Assert.AreEqual(98,ally.Health.Value);Assert.AreEqual(98,player.Health.Value);Assert.AreEqual(10,player.DamageDone.Value);
   Move(enemy,new Vector2(6,-6));Burn();Assert.AreEqual(90,enemy.Health.Value,"Leaving fire must stop damage immediately");
   player.Hurt(10,ally);Assert.AreEqual(97,player.Health.Value,"Ordinary attacks still obey global friendly-fire off");yield return null;
  }
  [UnityTest]public IEnumerator FireRespectsCoverShieldProtectionAndExpires(){
   var a=Throw(ItemKind.Incendiary);Land(a);a.landing=new Vector2(-3.5f,0);Move(enemy,new Vector2(-.5f,0));Burn(1);Assert.AreEqual(100,enemy.Health.Value,"Wall blocks fire damage");
   a.landing=new Vector2(0,-6);Move(enemy,a.landing);enemy.Shield.Value=5;enemy.ProtectionUntil=Time.time+10;Burn(.5f);Assert.AreEqual(5,enemy.Shield.Value);Assert.AreEqual(100,enemy.Health.Value);
   enemy.ProtectionUntil=0;Burn(.5f);Assert.AreEqual(0,enemy.Shield.Value);Assert.AreEqual(100,enemy.Health.Value);Burn(.5f);Assert.AreEqual(95,enemy.Health.Value);
   a.endAt=World.AreaTime-.1;Burn(.05f);Assert.AreEqual(95,enemy.Health.Value);Assert.AreEqual(0,World.Throwables.Count);yield return null;
  }
  [UnityTest]public IEnumerator SmokeHidesEnemyBodyNameHealthAndRopeButKeepsAlliesAndBullets(){
   var a=Throw(ItemKind.Smoke);Land(a);Move(ally,new Vector2(0,-5));Assert.IsFalse(World.CanSee(player,enemy));Assert.IsFalse(World.CanSee(enemy,player));Assert.IsTrue(World.CanSee(player,ally));
   Invoke(enemy,"LateUpdate");Invoke(ally,"LateUpdate");Assert.IsTrue(enemy.FogHidden);Assert.IsTrue(enemy.GetComponentsInChildren<Renderer>().All(r=>r.forceRenderingOff));Assert.IsFalse(ally.FogHidden);
   Assert.IsFalse(World.RopeVisible(new[]{enemy.Body.position,new Vector2(0,-4)},enemy.Team.Value));Assert.IsTrue(World.RopeVisible(new[]{player.Body.position,ally.Body.position},player.Team.Value));
   enemy.Body.simulated=true;Physics2D.SyncTransforms();player.Equip(WeaponKind.Sniper);player.NextShot=0;player.Aim=Vector2.right;Assert.IsTrue(World.TryAttack(player));Invoke(World,"TickBullets",.25f);Assert.AreEqual(100-GameConfig.Current.Weapon(WeaponKind.Sniper).damage,enemy.Health.Value,"Smoke must not block bullets");
   a.endAt=World.AreaTime-.1;Invoke(enemy,"LateUpdate");Assert.IsFalse(enemy.FogHidden);Assert.IsTrue(enemy.GetComponentsInChildren<Renderer>().All(r=>!r.forceRenderingOff));yield return null;
  }
  [UnityTest]public IEnumerator BotDropsTargetInSmokeAndPreviewOnlyShowsForSelectedThrowable(){
   var bot=new BotController(enemy,World.MapOverride,World.Players,World.Supplies.ToList());enemy.Equip(WeaponKind.Sniper);bot.Tick(true);Assert.IsTrue(enemy.Fire);
   var smoke=Throw(ItemKind.Smoke);Land(smoke);bot.Tick(true);Assert.IsFalse(enemy.Fire);Assert.IsNull(typeof(BotController).GetField("enemy",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(bot));
   World.TryCollectItem(player,ItemKind.Incendiary);player.SelectNextSlot();Invoke(World,"LateUpdate");var visuals=World.GetComponentInChildren<ThrowableVisuals>();Assert.IsTrue(visuals.PreviewVisible);
   Assert.IsTrue(World.TryUseSelected(player));Invoke(World,"LateUpdate");Assert.IsFalse(visuals.PreviewVisible);yield return null;
  }
  [UnityTest]public IEnumerator RocketCrossesCoverAndExplodesAfterLastRoundWithFriendlyFireRules(){
   Move(player,new Vector2(-8,0));Move(enemy,new Vector2(-.5f,0));Move(ally,new Vector2(-.5f,.8f));
   var sheltered=World.Players.Last(p=>p.TrainingRole==2);
   Move(sheltered,new Vector2(-3,0));
   player.Equip(WeaponKind.RocketLauncher);Assert.AreEqual(5,player.Ammo.Value);
   player.Ammo.Value=1;player.NextShot=0;
   player.SubmitInput(Vector2.zero,new Vector2(7.5f,0),true,false,true);
   var rocket=World.Throwables.Single();
   Assert.AreEqual(new Vector2(-.5f,0),rocket.landing);
   Assert.AreEqual(.8,rocket.landAt-rocket.launchedAt,.001);
   Assert.AreEqual((int)WeaponKind.Dagger,player.Weapon.Value);
   Burn();Assert.AreEqual(100,enemy.Health.Value);
   rocket.landAt=rocket.endAt=World.AreaTime-.1;
   Burn();Assert.AreEqual(75,enemy.Health.Value);Assert.AreEqual(100,ally.Health.Value);Assert.AreEqual(100,sheltered.Health.Value);
   GameConfig.Current.friendlyFire=true;
   player.Equip(WeaponKind.RocketLauncher);player.NextShot=0;
   player.SubmitInput(Vector2.zero,new Vector2(7.5f,0),true,false,true);
   rocket=World.Throwables.Single();Move(player,new Vector2(0,0));
   rocket.landAt=rocket.endAt=World.AreaTime-.1;
   Burn();Assert.AreEqual(50,enemy.Health.Value);Assert.AreEqual(75,ally.Health.Value);Assert.AreEqual(100,player.Health.Value);
   Assert.AreEqual(0,World.Throwables.Count);
   yield return null;
  }
  static IEnumerator Wait(Func<bool> ready){float end=Time.realtimeSinceStartup+10;while(!ready()&&Time.realtimeSinceStartup<end)yield return null;Assert.IsTrue(ready());}
  [UnityTearDown]public IEnumerator Cleanup(){GameConfig.Current.friendlyFire=oldFriendly;if(GameClient.Instance!=null){GameClient.Instance.ReturnToMainMenu();yield return Wait(()=>GameClient.Instance.Screen=="Menu"&&!Bootstrap.Network.IsListening);}Draw2D.RenderInBatch=oldRender;}
 }
}
