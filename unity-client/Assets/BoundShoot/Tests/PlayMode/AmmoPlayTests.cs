using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace BoundShoot.Tests {
 public class AmmoPlayTests {
  TrainingRuntime runtime;NetPlayer player,target;BattleWorld World=>runtime.World;
  List<ProjectileState> Bullets=>(List<ProjectileState>)typeof(BattleWorld).GetField("bullets",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(World);
  static void Place(NetPlayer p,Vector2 at){p.Body.position=at;p.transform.position=at;p.Body.velocity=Vector2.zero;}
  [UnitySetUp] public IEnumerator Setup(){
   if(Object.FindObjectOfType<Bootstrap>()==null)new GameObject("Ammo bootstrap").AddComponent<Bootstrap>();yield return null;yield return null;
   runtime=new GameObject("Ammo practice").AddComponent<TrainingRuntime>();yield return runtime.Begin("ammo",0);runtime.enabled=false;World.enabled=false;
   player=World.Players.Single(p=>p.TrainingRole==0);target=World.Players.First(p=>p.TrainingRole==2);
   foreach(var p in World.Players){p.enabled=false;p.Body.simulated=false;p.GetComponent<CircleCollider2D>().enabled=false;}
   Place(player,new Vector2(-8,-6));Place(target,new Vector2(-3,-6));target.Body.simulated=true;target.GetComponent<CircleCollider2D>().enabled=true;target.ProtectionUntil=0;Physics2D.SyncTransforms();player.Aim=Vector2.right;
  }
  [UnityTest] public IEnumerator FinalRoundKeepsGunDamageAndImpulseAfterAutomaticKnifeFallback(){
   player.Equip(WeaponKind.Sniper);player.Health.Value=75;player.Shield.Value=3;player.Ammo.Value=1;player.NextShot=0;
   int before=target.Health.Value;Assert.IsTrue(World.TryAttack(player));Assert.AreEqual(0,player.Ammo.Value);Assert.AreEqual((int)WeaponKind.Dagger,player.Weapon.Value);
   Assert.AreEqual(75,player.Health.Value);Assert.AreEqual(GameConfig.Current.baseHealth,player.MaxHealth.Value);Assert.AreEqual(3,player.Shield.Value);
   Assert.AreEqual((int)WeaponKind.Sniper,Bullets.Single().weapon);Assert.IsFalse(World.TryAttack(player),"Fallback must retain attack cooldown");
   typeof(BattleWorld).GetMethod("TickBullets",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(World,new object[]{.2f});
   Assert.AreEqual(before-GameConfig.Current.Weapon(WeaponKind.Sniper).damage,target.Health.Value);Assert.Greater(target.Body.velocity.x,0);Assert.IsEmpty(Bullets);yield return null;
  }
  [UnityTest] public IEnumerator EveryGunConsumesItsCapacityAndSameGunPickupRefillsWithoutStacking(){
   foreach(var gun in new[]{WeaponKind.Sniper,WeaponKind.Pistol,WeaponKind.AK}){
    player.Equip(gun);var definition=GameConfig.Current.Weapon(gun);Assert.AreEqual(definition.ammoCapacity,player.Ammo.Value);Assert.IsFalse(player.Equip(gun),"Full gun should not consume a redundant pickup");
    player.Aim=Vector2.left;player.NextShot=Time.time+10;Assert.IsFalse(World.TryAttack(player));Assert.AreEqual(definition.ammoCapacity,player.Ammo.Value);
    player.NextShot=0;Assert.IsTrue(World.TryAttack(player));Assert.AreEqual(definition.ammoCapacity-1,player.Ammo.Value);uint version=player.EquipmentVersion.Value;
    Assert.IsTrue(player.Equip(gun));Assert.AreEqual(definition.ammoCapacity,player.Ammo.Value);Assert.Greater(player.EquipmentVersion.Value,version);
    for(int remaining=definition.ammoCapacity;remaining>0;remaining--){player.NextShot=0;Assert.IsTrue(World.TryAttack(player));Assert.AreEqual(remaining-1,player.Ammo.Value);if(remaining>1)Assert.AreEqual((int)gun,player.Weapon.Value);}
    Assert.AreEqual((int)WeaponKind.Dagger,player.Weapon.Value);Bullets.Clear();
   }yield return null;
  }
  [UnityTest] public IEnumerator SpawnReviveAndTrainingResetGiveAnUnlimitedKnife(){
   Assert.IsTrue(World.Players.All(p=>p.Weapon.Value==(int)WeaponKind.Dagger&&p.Ammo.Value==0));
   for(int i=0;i<70;i++){player.NextShot=0;Assert.IsTrue(World.TryAttack(player));}Assert.AreEqual((int)WeaponKind.Dagger,player.Weapon.Value);Assert.AreEqual(0,player.Ammo.Value);
   player.Equip(WeaponKind.AK);player.ProtectionUntil=0;player.Hurt(9999,null);Assert.AreEqual(0,player.Ammo.Value);player.Resurrect();Assert.AreEqual((int)WeaponKind.Dagger,player.Weapon.Value);Assert.AreEqual(GameConfig.Current.baseHealth,player.Health.Value);
   player.Equip(WeaponKind.Pistol);World.ResetTraining();Assert.IsTrue(World.Players.All(p=>p.Weapon.Value==(int)WeaponKind.Dagger&&p.Ammo.Value==0));yield return null;
  }
  [UnityTearDown] public IEnumerator Cleanup(){if(runtime!=null)yield return runtime.Stop();}
 }
}
