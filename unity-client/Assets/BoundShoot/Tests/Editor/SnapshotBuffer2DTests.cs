using NUnit.Framework;
using UnityEngine;
namespace BoundShoot.Tests {
 public class SnapshotBuffer2DTests {
  [Test] public void ArrivalBurstsRenderOnServerTimeline(){
   var b=new SnapshotBuffer2D();b.Push(1,new Vector2(0,0),350);b.Push(2,new Vector2(10,0),10);
   Assert.IsTrue(b.Evaluate(1.5,.1,out var p,out var angle));Assert.AreEqual(5,p.x,.001f);Assert.AreEqual(0,Mathf.DeltaAngle(angle,0),.001f);
  }
  [Test] public void DuplicateAndOldStatesCannotReplaceNewerAuthority(){
   var b=new SnapshotBuffer2D();b.Push(1,Vector2.zero,0);b.Push(2,Vector2.right,10);b.Push(2,Vector2.one*999,99);b.Push(.5f,Vector2.one*-999,99);
   b.Evaluate(2,0,out var p,out var angle);Assert.AreEqual(Vector2.right,p);Assert.AreEqual(10,angle);
  }
  [Test] public void MissingPacketsHaveBoundedExtrapolation(){
   var b=new SnapshotBuffer2D();b.Push(1,Vector2.zero,0);b.Push(2,Vector2.right*10,0);
   b.Evaluate(100,.1,out var p,out var angle);Assert.AreEqual(11,p.x,.001f);
   b.Evaluate(100,0,out p,out angle);Assert.AreEqual(10,p.x,.001f);
  }
  [Test] public void ReconnectionClearsPriorTimeline(){
   var b=new SnapshotBuffer2D();b.Push(100,Vector2.one*100,0);b.Clear();
   Assert.IsFalse(b.Evaluate(1,.1,out var p,out var angle));b.Push(1,Vector2.right,90);
   Assert.IsTrue(b.Evaluate(1,0,out p,out angle));Assert.AreEqual(Vector2.right,p);
  }
 }
}
