using System;
using System.Linq;
using BoundShoot.Editor;
using NUnit.Framework;
using UnityEngine;
namespace BoundShoot.Tests {
 public class ThrowableTests {
  MapDefinition Map()=>new MapDefinition{halfWidth=20,halfHeight=20,blocks=Array.Empty<BlockDefinition>()};
  [Test] public void FixedLengthThrowCrossesInterveningCover(){
   var map=Map();map.blocks=new[]{new BlockDefinition{center=new Vector2(4,0),size=Vector2.one*2}};
   Assert.AreEqual(new Vector2(8,0),ThrowableRules.Landing(map,Vector2.zero,Vector2.right*100,8));
  }
  [Test] public void InvalidLandingShortensOnSameRayAndStaysInsideMap(){
   var map=Map();map.blocks=new[]{new BlockDefinition{center=new Vector2(8,0),size=Vector2.one*2}};
   var landing=ThrowableRules.Landing(map,Vector2.zero,Vector2.right,8);Assert.LessOrEqual(landing.x,6.85f);Assert.Greater(landing.x,6.7f);Assert.AreEqual(0,landing.y);
   map.blocks=Array.Empty<BlockDefinition>();map.halfWidth=5;landing=ThrowableRules.Landing(map,Vector2.zero,Vector2.one,8);Assert.That(landing.x,Is.InRange(4.7f,4.8f));Assert.AreEqual(landing.x,landing.y);
  }
  [Test] public void SmokeOccludesBothDirectionsIncludingObserverInsideAndExpires(){
   var area=new ThrowableState{kind=(int)ItemKind.Smoke,landing=Vector2.zero,radius=3,landAt=1,endAt=9};var areas=new[]{area};
   Assert.IsFalse(ThrowableRules.SmokeBlocks(areas,Vector2.left*6,Vector2.right*6,.99));
   Assert.IsTrue(ThrowableRules.SmokeBlocks(areas,Vector2.left*6,Vector2.right*6,1));Assert.IsTrue(ThrowableRules.SmokeBlocks(areas,Vector2.right*6,Vector2.left*6,1));
   Assert.IsTrue(ThrowableRules.SmokeBlocks(areas,Vector2.zero,Vector2.right*6,2));Assert.IsFalse(ThrowableRules.SmokeBlocks(areas,new Vector2(-6,4),new Vector2(6,4),2));
   Assert.IsFalse(ThrowableRules.SmokeBlocks(areas,Vector2.zero,Vector2.right*6,9));area.kind=(int)ItemKind.Incendiary;Assert.IsFalse(ThrowableRules.SmokeBlocks(areas,Vector2.zero,Vector2.right,2));
  }
  [Test] public void ThrowablesHaveDistinctIconsAndAppearInMediumAndHighSupplies(){
   var c=JsonUtility.FromJson<GameConfig>(Resources.Load<TextAsset>("GameConfig").text);
   foreach(var kind in new[]{ItemKind.Smoke,ItemKind.Incendiary}){
    Assert.IsTrue(c.Item(kind).enabled);Assert.AreEqual(0,c.Item(kind).weight);Assert.IsNotNull(CyberArt.Get("item"+(int)kind));Assert.AreEqual(c.Item(kind).name,LevelLayoutIO.ItemName(kind));
    Assert.IsTrue(c.supplyPools.Where(p=>p.tier!=SupplyTier.Common).All(p=>p.entries.Single(e=>e.item==kind).weight>0));Assert.IsTrue(TrainingMap.Create().pickups.Any(p=>p.item==kind));
   }Assert.AreNotSame(CyberArt.Get("item9"),CyberArt.Get("item10"));
  }
  [Test] public void InvalidThrowAndBurnNumbersAreRejected(){
   var values=BalanceConfigIO.Read();Assert.IsEmpty(BalanceConfigIO.Validate(values));values.throwDistance=float.NaN;values.fireSeconds=0;values.fireFriendlyDamagePerSecond=-1;Assert.GreaterOrEqual(BalanceConfigIO.Validate(values).Count,3);
  }
 }
}
