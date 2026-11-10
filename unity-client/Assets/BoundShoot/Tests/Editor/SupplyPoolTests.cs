using System;
using System.IO;
using System.Linq;
using BoundShoot.Authoring;
using BoundShoot.Editor;
using NUnit.Framework;
using UnityEngine;
namespace BoundShoot.Tests {
 public class SupplyPoolTests {
  GameConfig Config()=>JsonUtility.FromJson<GameConfig>(Resources.Load<TextAsset>("GameConfig").text);
  [Test] public void ApprovedProbabilitiesHaveExactWeightedIntervals(){
   var c=Config();Assert.IsEmpty(SupplyPools.Validate(c.supplyPools,c));
   for(int i=0;i<3;i++){
    var pool=c.supplyPools[i];Assert.AreEqual(new[]{15,25,40}[i],pool.respawnSeconds);
    var available=pool.entries.Where(e=>SupplyPools.Available(e,c)).ToArray();
    float total=available.Sum(e=>e.weight),offset=0;
    foreach(var entry in available){
     Assert.AreSame(entry,SupplyPools.Select(pool,c,(offset+entry.weight/2)/total));
     Assert.AreNotEqual(WeaponKind.Dagger,entry.weapon);
     offset+=entry.weight;
    }
   }
   var rare=c.supplyPools[2].entries;
   foreach(var weapon in new[]{WeaponKind.Sniper,WeaponKind.RocketLauncher,WeaponKind.Greatsword})
    Assert.Greater(rare.Single(e=>e.weapon==weapon).weight,rare.Single(e=>e.weapon==WeaponKind.AK).weight);
  }
  [Test] public void TierPoolRefreshesBothUncollectedAndCollectedItems(){
   var c=Config();var pool=c.supplyPools[1];foreach(var e in pool.entries)e.weight=e.weapon==WeaponKind.AK?100:0;
   var state=MapContent.CreatePickup(new PickupDefinition{supplyTier=SupplyTier.Medium},c);Assert.AreEqual(3,state.weapon);Assert.AreEqual(2,state.supplyTier);Assert.AreEqual(25,state.respawnSeconds);
   foreach(var e in pool.entries)e.weight=e.item==ItemKind.Heal?100:0;
   float deadline=state.respawnAt;
   Assert.IsFalse(MapContent.TryRespawn(state,deadline-.1f,()=>ItemKind.Bomb,c));Assert.AreEqual(3,state.weapon);
   Assert.IsTrue(MapContent.TryRespawn(state,deadline,()=>ItemKind.Bomb,c));Assert.AreEqual((int)ItemKind.Heal,state.item);
   MapContent.ScheduleRespawn(state,100);Assert.IsFalse(MapContent.TryRespawn(state,124.9f,()=>ItemKind.Bomb,c));Assert.IsTrue(MapContent.TryRespawn(state,125,()=>ItemKind.Bomb,c));Assert.AreEqual(0,state.weapon);Assert.AreEqual(7,state.item);
   var once=MapContent.CreatePickup(new PickupDefinition{supplyTier=SupplyTier.High,overrideRespawn=true,respawnSeconds=0},c);MapContent.ScheduleRespawn(once,10);Assert.IsFalse(MapContent.TryRespawn(once,10000,()=>ItemKind.Heal,c));
   var custom=MapContent.CreatePickup(new PickupDefinition{supplyTier=SupplyTier.Common,overrideRespawn=true,respawnSeconds=2},c);Assert.AreEqual(2,custom.respawnSeconds);
  }
  [Test] public void InvalidPoolWeightsKindsAndEmptyPoolsAreRejected(){
   var c=Config();c.supplyPools[0].entries[0].weight=float.NaN;c.supplyPools[1].entries[1].weight=-2;c.supplyPools[2].respawnSeconds=float.PositiveInfinity;
   Assert.GreaterOrEqual(SupplyPools.Validate(c.supplyPools,c).Count,3);
   c=Config();foreach(var e in c.supplyPools[0].entries)e.weight=0;Assert.IsNotEmpty(SupplyPools.Validate(c.supplyPools,c));
   c=Config();c.supplyPools[0].entries[0].weapon=WeaponKind.Pistol;Assert.IsNotEmpty(SupplyPools.Validate(c.supplyPools,c));
   c=Config();c.Item(ItemKind.Heal).enabled=false;Assert.IsNotEmpty(SupplyPools.Validate(c.supplyPools,c));
  }
  [Test] public void EditorLayoutRoundTripsTiersColorsAndPerPointTiming(){
   var c=Config();var root=new GameObject("Tier layout").AddComponent<LevelLayout>();
   try{
    LevelLayoutIO.Populate(root,c.maps[0]);
    for(int i=1;i<=3;i++){var piece=LevelLayoutIO.Add(root,LevelPieceKind.Pickup,new Vector2(i,1),Vector2.one);piece.supplyTier=(SupplyTier)i;piece.overrideRespawn=i==3;piece.respawnSeconds=7;
     Assert.AreEqual(SupplyPools.ColorOf(piece.supplyTier),LevelLayoutGizmos.ColorOf(piece));
    }
    var map=LevelLayoutIO.Collect(root);Assert.IsEmpty(LevelMapValidation.Validate(map,c));LevelLayoutIO.Populate(root,map);
    var points=root.GetComponentsInChildren<LevelPiece>().Where(p=>p.supplyTier!=SupplyTier.None&&p.transform.localPosition.y==1&&p.transform.localPosition.x>=1&&p.transform.localPosition.x<=3).OrderBy(p=>p.supplyTier).ToArray();Assert.AreEqual(3,points.Length);Assert.IsTrue(points[2].overrideRespawn);Assert.AreEqual(7,points[2].respawnSeconds);
    Assert.IsFalse(points[0].overrideRespawn);
    var copy=JsonUtility.FromJson<WorldSnapshot>(JsonUtility.ToJson(new WorldSnapshot{pickups=new[]{MapContent.CreatePickup(map.pickups.Last(),c)}}));Assert.AreEqual(3,copy.pickups[0].supplyTier);
   }finally{UnityEngine.Object.DestroyImmediate(root.gameObject);}
  }
  [Test] public void PoolSavePreservesLatestMapBalanceAndRejectsConflictsOrInvalidData(){
   string file=Path.Combine(Path.GetTempPath(),"bound-pool-"+Guid.NewGuid().ToString("N")+".json");
   try{
    var c=Config();File.WriteAllText(file,JsonUtility.ToJson(c));var draft=Config().supplyPools;string original=SupplyPoolConfigIO.Fingerprint(draft);
    c.friendlyFire=true;c.baseHealth=70;c.maps[0].name="新地图名";File.WriteAllText(file,JsonUtility.ToJson(c));draft[0].respawnSeconds=19;draft[0].entries[0].weight=35;
    SupplyPoolConfigIO.Save(file,draft,original);var saved=LevelLayoutIO.Read(file);Assert.AreEqual(70,saved.baseHealth);Assert.IsTrue(saved.friendlyFire);Assert.AreEqual("新地图名",saved.maps[0].name);Assert.AreEqual(19,saved.supplyPools[0].respawnSeconds);
    string before=File.ReadAllText(file);Assert.Throws<InvalidOperationException>(()=>SupplyPoolConfigIO.Save(file,draft,original));Assert.AreEqual(before,File.ReadAllText(file));
    original=SupplyPoolConfigIO.Fingerprint(draft);draft[0].entries[0].weight=-1;Assert.Throws<InvalidOperationException>(()=>SupplyPoolConfigIO.Save(file,draft,original));Assert.AreEqual(before,File.ReadAllText(file));
   }finally{if(File.Exists(file))File.Delete(file);}
  }
 }
}
