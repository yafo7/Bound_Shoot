using NUnit.Framework;
using UnityEngine;
namespace BoundShoot.Tests {
 public class CombatPresentationTests {
  static ProjectileState Sample(ulong id,float x=0)=>new ProjectileState{id=id,weapon=3,position=new Vector2(x,0),direction=Vector2.right,speed=24,remaining=15,bornAt=0};
  [Test] public void FifteenHzSamplesProduceContinuousSixtyHzTrajectory(){
   var track=new ProjectileTrack();track.Sample(Sample(1),0);float previous=0;
   for(int i=1;i<=60;i++){
    double time=i/60.0;
    if(i%4==0)track.Sample(Sample(1,(float)time*24),time);
    float x=track.Evaluate(time).x;Assert.AreEqual(.4f,x-previous,.001f,"Frame "+i);previous=x;
   }
  }
  [Test] public void ExtrapolationStopsAtBudgetOrProjectileRange(){
   var track=new ProjectileTrack();var state=Sample(1);track.Sample(state,10);
   Assert.AreEqual(3.6f,track.Evaluate(99).x,.001f);
   state.remaining=.2f;track.Sample(state,10);Assert.AreEqual(.2f,track.Evaluate(11).x,.001f);
   Assert.AreEqual(0,track.Evaluate(9).x,.001f);
  }
  [Test] public void DeletedArrayEntryCannotStealAnotherProjectileViewOrResurrect(){
   var go=new GameObject("Projectile identity test");try{
    var views=go.AddComponent<ProjectileVisuals>();views.Reconcile(new[]{Sample(11),Sample(12,6)},0);views.Render(0);
    views.Reconcile(new[]{Sample(12,7)},.05);views.Render(.05);
    Assert.IsFalse(views.TryGetPosition(11,out _));Assert.IsTrue(views.TryGetPosition(12,out var position));Assert.AreEqual(7,position.x,.001f);
    views.Spawn(Sample(11),0);Assert.AreEqual(1,views.ActiveCount);
    views.Reconcile(new[]{Sample(11)},.01);Assert.AreEqual(1,views.ActiveCount,"Old snapshots cannot roll the set back");
    views.Clear();Assert.AreEqual(0,views.ActiveCount);
   }finally{Object.DestroyImmediate(go);}
  }
  [Test] public void ProjectileVisualCannotExtrapolateThroughCover(){
   var wall=new GameObject("Wall");var go=new GameObject("Projectile cover test");try{
    wall.layer=8;wall.transform.position=new Vector2(1,0);wall.AddComponent<BoxCollider2D>().size=new Vector2(.2f,2);Physics2D.SyncTransforms();
    var views=go.AddComponent<ProjectileVisuals>();views.Spawn(Sample(1),0);views.Render(.1);
    foreach(var renderer in go.GetComponentsInChildren<SpriteRenderer>())Assert.IsFalse(renderer.enabled);
   }finally{Object.DestroyImmediate(go);Object.DestroyImmediate(wall);}
  }
  [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
  public void MuzzleMatchesRenderedSpriteAtEveryAimAngle(int weapon){
   var profile=WeaponPresentation.Get(weapon);Assert.IsNotNull(profile);
   Assert.GreaterOrEqual(profile.Centre(0,false).x-profile.size.x*.5f,-.08f,"Right-facing weapon must sit in front of the face");
   var go=new GameObject("Weapon pose");try{
    for(int angle=-180;angle<=180;angle+=15){
     bool flipped=Mathf.Cos(angle*Mathf.Deg2Rad)<0;go.transform.position=(Vector3)WeaponPresentation.Pivot;go.transform.rotation=Quaternion.Euler(0,0,angle);
     Vector2 centre=profile.Centre(0,flipped),tip=Vector2.Scale(profile.muzzle-Vector2.one*.5f,profile.size);if(flipped)tip.y=-tip.y;
     Vector2 visibleTip=go.transform.TransformPoint(centre+tip);
     Assert.Less(Vector2.Distance(visibleTip,WeaponPresentation.Muzzle(weapon,Vector2.zero,angle)),.0001f);
     Vector2 aim=WeaponPresentation.Rotate(Vector2.right,angle),muzzle=WeaponPresentation.Muzzle(weapon,Vector2.zero,angle);
     Assert.Less(Mathf.Abs(aim.x*muzzle.y-aim.y*muzzle.x),.0001f,"Muzzle and trajectory must share the mouse aim axis, including left-facing shots");
    }
   }finally{Object.DestroyImmediate(go);}
  }
  [Test] public void RopeTensionFeedbackDoesNotChangeTheRoute(){
   var go=new GameObject("Rope display test");try{
    var view=new RopeLineView(go.transform);var points=new[]{Vector2.zero,new Vector2(1,1),Vector2.right*2};var original=(Vector2[])points.Clone();
    view.Show(points,0,.5f);float slack=view.Line.startWidth;view.Show(points,0,1.2f);
    Assert.Greater(view.Line.startWidth,slack);Assert.AreEqual(3,view.Line.positionCount);CollectionAssert.AreEqual(original,points);
    Assert.AreEqual((Vector3)points[0],view.Line.GetPosition(0));Assert.AreEqual((Vector3)points[2],view.Line.GetPosition(2));
    view.Show(null,0,0);Assert.AreEqual(0,view.Line.positionCount);
   }finally{Object.DestroyImmediate(go);}
  }
 }
}
