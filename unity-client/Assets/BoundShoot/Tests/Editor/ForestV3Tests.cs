using System.Linq;
using NUnit.Framework;
using UnityEngine;
using BoundShoot.Editor;
using BoundShoot.Authoring;
namespace BoundShoot.Tests {
 public class ForestV3Tests {
  [Test]public void ForestHasTraversablePlateauAndNoRevivalPoint(){
   var map=ForestMap.Create();Assert.IsEmpty(map.revivalZones);Assert.IsEmpty(LevelMapValidation.Validate(map,GameConfig.Current));
   var path=new RopePath(map);Assert.IsTrue(path.Visible(new Vector2(-21,0),new Vector2(-15,0)),"Main stair is open");Assert.IsFalse(path.Visible(new Vector2(-21,5),new Vector2(-15,5)),"Cliff side must block");
   Assert.AreEqual(0,ForestMap.SurfaceAt(map,new Vector2(-25,0)));Assert.AreEqual(1,ForestMap.SurfaceAt(map,Vector2.zero));Assert.AreEqual(2,ForestMap.SurfaceAt(map,new Vector2(-18,0)));
  }
  [Test]public void ForestEditorRoundTripPreservesPlateau(){var go=new GameObject();try{var root=go.AddComponent<LevelLayout>();LevelLayoutIO.Populate(root,ForestMap.Create());var result=LevelLayoutIO.Collect(root);Assert.IsNotNull(result.plateau);CollectionAssert.AreEqual(new[]{-10f,0,10f},result.plateau.stairs);Assert.IsEmpty(result.revivalZones);}finally{Object.DestroyImmediate(go);}}
  [Test]public void ForestSourceSpritesAreAvailable(){foreach(string key in new[]{"grass","tuft","cliff","tree","rock","heart","heartEmpty"})Assert.IsNotNull(ForestArt.Get(key),key);}
  [Test]public void InvalidPlateauCannotBeSaved(){var map=ForestMap.Create();map.plateau.stairWidth=float.NaN;Assert.IsNotEmpty(LevelMapValidation.Validate(map,GameConfig.Current));}
  [Test]public void RopeCanAcceleratePassivePartner(){
   var one=new GameObject();var two=new GameObject();var mode=Physics2D.simulationMode;try{Physics2D.simulationMode=SimulationMode2D.Script;var a=one.AddComponent<Rigidbody2D>();var b=two.AddComponent<Rigidbody2D>();one.AddComponent<CircleCollider2D>();two.AddComponent<CircleCollider2D>();a.gravityScale=b.gravityScale=0;b.position=Vector2.right*6;Physics2D.SyncTransforms();
    RopePhysics.Apply(a,b,new[]{Vector2.zero,Vector2.right*6},5,90,12,200);Assert.IsTrue(Physics2D.Simulate(.033f));
    Assert.Less(b.velocity.x,0);Assert.Greater(a.velocity.x,0);
   }finally{Object.DestroyImmediate(one);Object.DestroyImmediate(two);Physics2D.simulationMode=mode;}
  }
 }
}
