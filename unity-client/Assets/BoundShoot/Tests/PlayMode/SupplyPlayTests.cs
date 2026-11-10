using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace BoundShoot.Tests {
 public class SupplyPlayTests {
  TrainingRuntime runtime;bool oldRender;SupplyPoolDefinition[] oldPools;
  static void Invoke(BattleWorld world,string method)=>typeof(BattleWorld).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(world,null);
  [UnitySetUp] public IEnumerator Setup(){
   oldRender=Draw2D.RenderInBatch;Draw2D.RenderInBatch=true;oldPools=GameConfig.Current.supplyPools;
   GameConfig.Current.supplyPools=JsonUtility.FromJson<GameConfig>(JsonUtility.ToJson(GameConfig.Current)).supplyPools;
   foreach(var pool in GameConfig.Current.supplyPools)foreach(var e in pool.entries)e.weight=e.weapon==WeaponKind.AK?100:0;
   if(Object.FindObjectOfType<Bootstrap>()==null)new GameObject("Supply bootstrap").AddComponent<Bootstrap>();yield return null;yield return null;
   runtime=new GameObject("Supply practice").AddComponent<TrainingRuntime>();yield return runtime.Begin("supplies",0);
   runtime.enabled=false;runtime.World.enabled=false;foreach(var p in runtime.World.Players){p.enabled=false;p.Body.simulated=false;}
  }
  [UnityTest] public IEnumerator TierPointsResetPickupRespawnAndRenderFromAuthoritativeSnapshots(){
   var world=runtime.World;var definitions=Enumerable.Range(1,3).Select(i=>new PickupDefinition{position=new Vector2(-11+i*3,-4),supplyTier=(SupplyTier)i}).ToArray();
   world.MapOverride.pickups=world.MapOverride.pickups.Concat(definitions).ToArray();
   var supplies=(List<PickupState>)world.Supplies;foreach(var definition in definitions)supplies.Add(MapContent.CreatePickup(definition));
   world.ResetTraining();var tiers=supplies.Where(p=>p.supplyTier>0).ToArray();Assert.AreEqual(3,tiers.Length);Assert.IsTrue(tiers.All(p=>p.active&&p.weapon==(int)WeaponKind.AK));
   Invoke(world,"BroadcastSnapshot");yield return null;
   var markers=world.GetComponentsInChildren<SpriteRenderer>().Where(p=>p.name=="Supply tier"&&p.enabled).OrderBy(p=>p.transform.position.x).ToArray();Assert.AreEqual(3,markers.Length);
   for(int i=0;i<3;i++){var color=SupplyPools.ColorOf((SupplyTier)(i+1));Assert.AreEqual(color.r,markers[i].color.r,.001f);Assert.AreEqual(color.b,markers[i].color.b,.001f);}
   var human=world.Players.Single(p=>p.TrainingRole==0);human.Body.position=tiers[0].position;human.transform.position=tiers[0].position;Invoke(world,"TickPickups");
   Assert.AreEqual((int)WeaponKind.AK,human.Weapon.Value);Assert.IsFalse(tiers[0].active);Assert.AreEqual(Time.time+15,tiers[0].respawnAt,.05f);
   human.Body.position=new Vector2(-12,-8);human.transform.position=human.Body.position;Invoke(world,"BroadcastSnapshot");Assert.Less(markers[0].color.a,.3f);
   foreach(var e in GameConfig.Current.supplyPools[0].entries)e.weight=e.item==ItemKind.Heal?100:0;
   tiers[0].respawnAt=Time.time-1;Invoke(world,"TickPickups");Assert.IsTrue(tiers[0].active);Assert.AreEqual((int)ItemKind.Heal,tiers[0].item);Assert.AreEqual(0,tiers[0].weapon);
   Assert.AreEqual((int)WeaponKind.AK,tiers[1].weapon,"Uncollected points keep their contents");
   world.ResetTraining();Assert.IsTrue(tiers.All(p=>p.active));Assert.AreEqual((int)ItemKind.Heal,tiers[0].item);Assert.AreEqual((int)WeaponKind.AK,tiers[1].weapon);
  }
  [UnityTearDown] public IEnumerator Cleanup(){if(runtime!=null)yield return runtime.Stop();GameConfig.Current.supplyPools=oldPools;Draw2D.RenderInBatch=oldRender;}
 }
}
