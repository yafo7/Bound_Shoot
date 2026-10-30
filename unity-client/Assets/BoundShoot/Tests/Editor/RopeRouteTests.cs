using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace BoundShoot.Tests {
 public class RopeRouteTests {
  static RopePath Geometry()=>new RopePath(new MapDefinition{blocks=new[]{new BlockDefinition{center=Vector2.zero,size=Vector2.one*2}}});
  static void ClearSegments(RopePath geometry,Vector2[] path){Assert.GreaterOrEqual(path.Length,2);for(int i=1;i<path.Length;i++)Assert.IsTrue(geometry.Visible(path[i-1],path[i]),"Segment crosses a cover");}
  [Test] public void TinyMotionDoesNotFlipToOtherSideOfCover(){
   var geometry=Geometry();var route=new RopeRoute();
   var before=route.Update(geometry,new Vector2(-3,.02f),new Vector2(3,.02f));Assert.Greater(before.Max(p=>p.y),1);
   Assert.Less(geometry.Solve(new Vector2(-3,-.02f),new Vector2(3,-.02f)).Min(p=>p.y),-1,"Control: old shortest path changes sides");
   var after=route.Update(geometry,new Vector2(-3,-.02f),new Vector2(3,-.02f));ClearSegments(geometry,after);
   Assert.Greater(after.Max(p=>p.y),1);Assert.Greater(after.Min(p=>p.y),-.03f);
  }
  [Test] public void DirectVisibilityDoesNotUnwrapAcrossEntireObstacle(){
   var geometry=Geometry();var route=new RopeRoute();route.Update(geometry,new Vector2(-3,.2f),new Vector2(3,.2f));
   Vector2[] path=null;
   for(int i=1;i<=100;i++){float y=Mathf.Lerp(.2f,-2.2f,i/100f);path=route.Update(geometry,new Vector2(-3,y),new Vector2(3,y));ClearSegments(geometry,path);}
   Assert.IsTrue(geometry.Visible(new Vector2(-3,-2.2f),new Vector2(3,-2.2f)));
   Assert.Greater(path.Max(p=>p.y),1,"Rope must remain around its original upper side");Assert.Greater(RopePath.Length(path),9);
  }
  [Test] public void ReturningAroundOriginalSideReleasesBends(){
   var geometry=Geometry();var route=new RopeRoute();route.Update(geometry,new Vector2(-3,.2f),new Vector2(3,.2f));
   Vector2[] path=null;for(int i=1;i<=100;i++){float y=Mathf.Lerp(.2f,2.2f,i/100f);path=route.Update(geometry,new Vector2(-3,y),new Vector2(3,y));ClearSegments(geometry,path);}
   Assert.AreEqual(2,path.Length);Assert.AreEqual(6,RopePath.Length(path),.001f);
  }
  [Test] public void ShortcutCannotSweepOverObstacleWithAllThreeEdgesClear(){
   var geometry=Geometry();var a=new Vector2(-3,-2);var bend=new Vector2(0,4);var b=new Vector2(3,-2);
   Assert.IsTrue(geometry.Visible(a,b));Assert.IsTrue(geometry.Visible(a,bend));Assert.IsTrue(geometry.Visible(bend,b));
   Assert.IsFalse(geometry.CanShortcut(a,bend,b));
  }
  [Test] public void IndependentRopesKeepDifferentSidesAndResetIsExplicit(){
   var geometry=Geometry();var upper=new RopeRoute();var lower=new RopeRoute();
   upper.Update(geometry,new Vector2(-3,.1f),new Vector2(3,.1f));lower.Update(geometry,new Vector2(-3,-.1f),new Vector2(3,-.1f));
   Assert.Greater(upper.Update(geometry,new Vector2(-3,0),new Vector2(3,0)).Max(p=>p.y),1);
   Assert.Less(lower.Update(geometry,new Vector2(-3,0),new Vector2(3,0)).Min(p=>p.y),-1);
   upper.Clear();Assert.Less(upper.Update(geometry,new Vector2(-3,-.1f),new Vector2(3,-.1f)).Min(p=>p.y),-1);
  }
  [Test] public void ImpossiblePathNeverFallsBackToStraightLineThroughCover(){Assert.IsEmpty(Geometry().Solve(Vector2.zero,new Vector2(3,0)));}
  [Test] public void TemporaryOverlapDoesNotEraseWinding(){
   var geometry=Geometry();var route=new RopeRoute();route.Update(geometry,new Vector2(-3,.02f),new Vector2(3,.02f));
   var held=route.Update(geometry,Vector2.zero,new Vector2(3,-.02f));ClearSegments(geometry,held);
   var recovered=route.Update(geometry,new Vector2(-3,-.02f),new Vector2(3,-.02f));ClearSegments(geometry,recovered);Assert.Greater(recovered.Max(p=>p.y),1);
  }
  [Test] public void ForceStillPullsTowardOriginalBendAfterCrossingSymmetryLine(){
   var geometry=Geometry();var route=new RopeRoute();route.Update(geometry,new Vector2(-3,.02f),new Vector2(3,.02f));
   var one=new GameObject("Rope A");var two=new GameObject("Rope B");var old=Physics2D.simulationMode;
   try{
    Physics2D.simulationMode=SimulationMode2D.Script;var a=one.AddComponent<Rigidbody2D>();var b=two.AddComponent<Rigidbody2D>();a.gravityScale=b.gravityScale=0;
    a.position=new Vector2(-3,-.02f);b.position=new Vector2(3,-.02f);
    var path=route.Update(geometry,a.position,b.position);RopePhysics.Apply(a,b,path,5,45,6,100);Physics2D.Simulate(1f/30);
    Assert.Greater(a.velocity.y,0);Assert.Greater(b.velocity.y,0);Assert.Greater(a.velocity.x,0);Assert.Less(b.velocity.x,0);
   }finally{Physics2D.simulationMode=old;Object.DestroyImmediate(one);Object.DestroyImmediate(two);}
  }
 }
}
