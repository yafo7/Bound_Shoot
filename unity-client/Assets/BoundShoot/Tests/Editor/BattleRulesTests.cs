using NUnit.Framework;
using UnityEngine;
namespace BoundShoot.Tests {
 public class BattleRulesTests {
  [Test] public void ShieldAbsorbsBeforeIntegerHealth(){int hp=3,s=1;BattleRules.Damage(ref hp,ref s,2);Assert.AreEqual(2,hp);Assert.AreEqual(0,s);BattleRules.Damage(ref hp,ref s,9);Assert.AreEqual(0,hp);}
  [Test] public void LivingPlayersCanSwitchWeaponsWithoutDying(){foreach(WeaponKind w in new[]{WeaponKind.None,WeaponKind.AK,WeaponKind.Sniper,WeaponKind.Pistol,WeaponKind.Dagger})Assert.IsTrue(BattleRules.CanEquip(true,w));Assert.IsFalse(BattleRules.CanEquip(false,WeaponKind.None));}
  [Test] public void WinnerHonorsWipeThenSurvivorsThenHealthThenDraw(){Assert.AreEqual(1,BattleRules.Winner(0,1,0,1));Assert.AreEqual(0,BattleRules.Winner(2,1,2,5));Assert.AreEqual(1,BattleRules.Winner(1,1,2,3));Assert.AreEqual(-1,BattleRules.Winner(1,1,3,3));Assert.AreEqual(-1,BattleRules.Winner(0,0,0,0));}
  [Test] public void ReviveRequiresContinuousFiveSeconds(){float t=0;for(int i=0;i<4;i++)t=BattleRules.ReviveProgress(t,true,1);Assert.AreEqual(4,t);Assert.AreEqual(0,BattleRules.ReviveProgress(t,false,.01f));Assert.AreEqual(5,BattleRules.ReviveProgress(t,true,1));}
  [Test] public void RopeModifiersRestoreBaseAndDoNotAccumulate(){var c=new GameConfig();Assert.AreEqual(.975f,BattleRules.RopeScale(true,true,c),.001f);Assert.AreEqual(1,BattleRules.RopeScale(false,false,c));}
  [Test] public void RopeBendsAroundCoverAndUnwrapsInClearSpace(){
   var map=new MapDefinition{blocks=new[]{new BlockDefinition{center=Vector2.zero,size=Vector2.one*2}}};
   var paths=new RopePath(map);var path=paths.Solve(new Vector2(-3,0),new Vector2(3,0));
   Assert.Greater(path.Length,2);Assert.Greater(RopePath.Length(path),6);
   for(int i=1;i<path.Length;i++)Assert.IsTrue(paths.Visible(path[i-1],path[i]));
   Assert.AreEqual(2,paths.Solve(new Vector2(-3,3),new Vector2(3,3)).Length);
  }
  [Test] public void TripledHealthPreservesWeaponDamage(){var c=GameConfig.Current;Assert.AreEqual(9,c.baseHealth);Assert.AreEqual(3,c.Weapon(WeaponKind.Sniper).maxHealth);Assert.AreEqual(9,c.Weapon(WeaponKind.Pistol).maxHealth);Assert.AreEqual(9,c.Weapon(WeaponKind.AK).maxHealth);Assert.AreEqual(15,c.Weapon(WeaponKind.Dagger).maxHealth);Assert.AreEqual(4,c.Weapon(WeaponKind.Sniper).damage);Assert.AreEqual(2,c.Weapon(WeaponKind.AK).damage);}
  [Test] public void TautRopeAppliesRealForceToLivingBodyAndHeavierCorpse(){
   var one=new GameObject("Alive");var two=new GameObject("Corpse");var old=Physics2D.simulationMode;
   try{
    Physics2D.simulationMode=SimulationMode2D.Script;var a=one.AddComponent<Rigidbody2D>();var b=two.AddComponent<Rigidbody2D>();
    a.gravityScale=b.gravityScale=0;b.mass=1.6f;a.position=new Vector2(-4,0);b.position=new Vector2(4,0);
    RopePhysics.Apply(a,b,new[]{a.position,b.position},5,45,6,100);Assert.IsTrue(Physics2D.Simulate(1f/30));
    Assert.Greater(a.velocity.x,0);Assert.Less(b.velocity.x,0);Assert.Greater(a.velocity.magnitude,b.velocity.magnitude);
    Assert.Less(Vector2.Distance(a.position,b.position),8);Assert.Greater(Vector2.Distance(a.position,b.position),5);
   }finally{Physics2D.simulationMode=old;Object.DestroyImmediate(one);Object.DestroyImmediate(two);}
  }
 }
}
