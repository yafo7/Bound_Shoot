using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace BoundShoot.Tests {
 public class KnockbackPlayTests {
  TrainingRuntime runtime;NetPlayer shooter,target;GameObject wall;bool originalFriendlyFire;int originalBaseHealth;
  int SniperDamage=>GameConfig.Current.Weapon(WeaponKind.Sniper).damage;
  [UnitySetUp] public IEnumerator Setup(){
   if(Object.FindObjectOfType<Bootstrap>()==null)new GameObject("Knockback bootstrap").AddComponent<Bootstrap>();
   yield return null;yield return null;
   originalBaseHealth=GameConfig.Current.baseHealth;GameConfig.Current.baseHealth=30;
   runtime=new GameObject("Knockback practice").AddComponent<TrainingRuntime>();yield return runtime.Begin("knockback",0);
   originalFriendlyFire=GameConfig.Current.friendlyFire;GameConfig.Current.friendlyFire=false;
   var world=runtime.World;world.enabled=false;runtime.enabled=false;
   shooter=world.Players.Single(p=>p.TrainingRole==0);target=world.Players.First(p=>p.TrainingRole==2);
   foreach(var p in world.Players){p.enabled=false;p.Body.simulated=false;p.GetComponent<CircleCollider2D>().enabled=false;}
   shooter.GetComponent<CircleCollider2D>().enabled=true;target.GetComponent<CircleCollider2D>().enabled=true;
   target.Body.simulated=true;target.Body.mass=1;target.Body.drag=0;
   ResetTarget();shooter.Equip(WeaponKind.Sniper);shooter.Aim=Vector2.right;
  }
  void ResetTarget(float x=-7.1f){
   target.Resurrect();target.ProtectionUntil=0;Place(shooter,new Vector2(-8,-6));Place(target,new Vector2(x,-6));
   foreach(var partner in runtime.World.Players.Where(p=>p!=target&&p!=shooter&&p.Team.Value==target.Team.Value))Place(partner,new Vector2(x,-3));
   foreach(var p in runtime.World.Players)runtime.World.ResetRopesForTeleport(p);
   shooter.NextShot=0;Physics2D.SyncTransforms();
  }
  static void Place(NetPlayer p,Vector2 at){p.Body.position=at;p.transform.position=at;p.Body.velocity=Vector2.zero;}
  void Shoot(){shooter.NextShot=0;Assert.IsTrue(runtime.World.TryAttack(shooter));}
  [UnityTest] public IEnumerator CloseHitHonorsShieldProtectionDeathAndNonSniperRules(){
   target.Shield.Value=1;Shoot();
   Assert.AreEqual(30-SniperDamage+1,target.Health.Value);Assert.AreEqual(0,target.Shield.Value);Assert.Greater(target.Body.velocity.x,5.9f);
   ResetTarget();target.Shield.Value=SniperDamage;Shoot();
   Assert.AreEqual(30,target.Health.Value,"Fully absorbed damage still causes knockback");Assert.Greater(target.Body.velocity.x,5.9f);
   ResetTarget();target.ProtectionUntil=Time.time+2;Shoot();
   Assert.AreEqual(30,target.Health.Value);Assert.AreEqual(Vector2.zero,target.Body.velocity);
   ResetTarget();target.Health.Value=1;Shoot();
   Assert.IsFalse(target.Alive);Assert.Greater(target.Body.velocity.x,5.9f,"Lethal hit moves the corpse after input reset");
   float before=target.Body.position.x;yield return new WaitForFixedUpdate();yield return null;
   Assert.Greater(target.Body.position.x,before,"Impulse must move a real Rigidbody2D");
   ResetTarget();shooter.Equip(WeaponKind.AK);Shoot();Assert.AreEqual(28,target.Health.Value);Assert.AreEqual(Vector2.zero,target.Body.velocity);
   ResetTarget();target.Team.Value=shooter.Team.Value;target.Hurt(4,shooter,Vector2.right*6);
   Assert.AreEqual(30,target.Health.Value);Assert.AreEqual(Vector2.zero,target.Body.velocity,"This step must not enable friendly fire");
  }
  [UnityTest] public IEnumerator FlyingBulletKeepsLaunchImpulseAfterShooterSwitchesWeapon(){
   ResetTarget(-4);shooter.Aim=Vector2.right;Shoot();shooter.Equip(WeaponKind.Pistol);
   // Let the actual battle loop advance the projectile; only the target is simulated.
   runtime.World.enabled=true;float until=Time.time+1;
   while(target.Health.Value==30&&Time.time<until)yield return new WaitForFixedUpdate();
   runtime.World.enabled=false;
   Assert.AreEqual(30-SniperDamage,target.Health.Value);Assert.Greater(target.Body.velocity.x,0);
   Assert.Greater(target.Body.position.x,-4);
   Assert.AreEqual(-6,target.Body.position.y,.05f,"Slack rope must not contribute sideways motion to the shot check");
  }
  [UnityTest] public IEnumerator ConfiguredDaggerImpulseUsesTheSameDamageAndProtectionRules(){
   var weapon=GameConfig.Current.Weapon(WeaponKind.Dagger);float previous=weapon.knockbackImpulse;
   try{
    weapon.knockbackImpulse=2;ResetTarget();shooter.Equip(WeaponKind.Dagger);Shoot();
    Assert.AreEqual(24,target.Health.Value);Assert.AreEqual(2,target.Body.velocity.x,.001f);
    ResetTarget();target.ProtectionUntil=Time.time+2;Shoot();Assert.AreEqual(30,target.Health.Value);Assert.AreEqual(Vector2.zero,target.Body.velocity);
   }finally{weapon.knockbackImpulse=previous;}yield return null;
  }
  [UnityTest] public IEnumerator KnockbackCollidesWithWallAndTautRopeOpposesIt(){
   wall=new GameObject("Knockback blocking wall");wall.layer=8;wall.transform.position=new Vector2(-6,-6);wall.AddComponent<BoxCollider2D>().size=new Vector2(.5f,3);
   ResetTarget();Shoot();float start=target.Body.position.x;
   for(int i=0;i<20;i++)yield return new WaitForFixedUpdate();
   Assert.Greater(target.Body.position.x,start);Assert.LessOrEqual(target.Body.position.x,-6.65f,"Body must stop before the wall rather than teleport through it");
   Object.DestroyImmediate(wall);wall=null;
   ResetTarget();shooter.Body.simulated=true;shooter.Body.mass=1;shooter.Body.drag=0;
   Place(shooter,new Vector2(-13.5f,-6));Place(target,new Vector2(-7,-6));Physics2D.SyncTransforms();
   target.Hurt(4,shooter,Vector2.right*6);float initial=target.Body.velocity.x;
   RopePhysics.Apply(shooter.Body,target.Body,new[]{shooter.Body.position,target.Body.position},5,90,12,200);
   yield return new WaitForFixedUpdate();yield return null;
   Assert.Less(target.Body.velocity.x,initial,"Existing rope must oppose outward knockback");Assert.Greater(shooter.Body.velocity.x,0);
  }
  static void Invoke(BattleWorld world,string method,params object[] args)=>typeof(BattleWorld).GetMethod(method,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(world,args);
  [UnityTest] public IEnumerator FriendlyCloseHitsHonorToggleShieldProtectionSelfAndStatistics(){
   target.Team.Value=shooter.Team.Value;target.Shield.Value=5;Shoot();Assert.AreEqual(30,target.Health.Value);Assert.AreEqual(5,target.Shield.Value);Assert.AreEqual(Vector2.zero,target.Body.velocity);
   GameConfig.Current.friendlyFire=true;Shoot();Assert.AreEqual(30-SniperDamage+5,target.Health.Value);Assert.AreEqual(0,target.Shield.Value);Assert.Greater(target.Body.velocity.x,5.9f);Assert.AreEqual(0,shooter.DamageDone.Value);
   ResetTarget();target.ProtectionUntil=Time.time+2;Shoot();Assert.AreEqual(30,target.Health.Value);Assert.AreEqual(Vector2.zero,target.Body.velocity);
   ResetTarget();target.Health.Value=1;Shoot();Assert.IsFalse(target.Alive);Assert.AreEqual(1,target.Deaths.Value);Assert.AreEqual(0,shooter.Kills.Value);Assert.AreEqual(0,shooter.DamageDone.Value);
   int hp=shooter.Health.Value;shooter.Hurt(999,shooter,Vector2.right*6);Assert.AreEqual(hp,shooter.Health.Value);yield return null;
  }
  [UnityTest] public IEnumerator FriendlyProjectileBlocksOnlyWhenEnabledAndStopsBeforeEnemy(){
   target.Team.Value=shooter.Team.Value;ResetTarget(-5);
   var enemy=runtime.World.Players.First(p=>p!=target&&p.TrainingRole==2);enemy.enabled=false;enemy.Body.simulated=true;enemy.GetComponent<CircleCollider2D>().enabled=true;
   enemy.Resurrect();enemy.ProtectionUntil=0;Place(enemy,new Vector2(-3,-6));Physics2D.SyncTransforms();
   Shoot();Invoke(runtime.World,"TickBullets",.2f);Assert.AreEqual(30,target.Health.Value);Assert.AreEqual(30-SniperDamage,enemy.Health.Value);
   GameConfig.Current.friendlyFire=true;ResetTarget(-5);enemy.Resurrect();enemy.ProtectionUntil=0;Place(enemy,new Vector2(-3,-6));Physics2D.SyncTransforms();
   Shoot();Invoke(runtime.World,"TickBullets",.2f);Assert.AreEqual(30-SniperDamage,target.Health.Value);Assert.AreEqual(30,enemy.Health.Value);
   yield return null;
  }
  [UnityTest] public IEnumerator FriendlyDaggerAndBombRespectToggleAndBombNeverHurtsOwner(){
   target.Team.Value=shooter.Team.Value;shooter.Equip(WeaponKind.Dagger);Shoot();Assert.AreEqual(30,target.Health.Value);
   GameConfig.Current.friendlyFire=true;Shoot();Assert.AreEqual(24,target.Health.Value);
   foreach(bool enabled in new[]{false,true}){
    GameConfig.Current.friendlyFire=enabled;ResetTarget();int selfHp=shooter.Health.Value;
    Assert.IsTrue(runtime.World.TryCollectItem(shooter,ItemKind.Bomb));shooter.SelectNextSlot();Assert.IsTrue(runtime.World.TryUseSelected(shooter));
    var bombs=(System.Collections.Generic.List<BombState>)typeof(BattleWorld).GetField("bombs",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(runtime.World);
    bombs.Single().explodeAt=0;Invoke(runtime.World,"TickBombs");Assert.AreEqual(enabled?22:30,target.Health.Value);Assert.AreEqual(selfHp,shooter.Health.Value);
   }Assert.AreEqual(0,shooter.DamageDone.Value);yield return null;
  }
  [UnityTearDown] public IEnumerator Cleanup(){GameConfig.Current.baseHealth=originalBaseHealth;GameConfig.Current.friendlyFire=originalFriendlyFire;if(wall!=null)Object.DestroyImmediate(wall);if(runtime!=null)yield return runtime.Stop();}
 }
}
