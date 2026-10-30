using NUnit.Framework;
using UnityEngine;
namespace BoundShoot.Tests {
 public class VegetableWalkTests {
  [TestCase(60)] [TestCase(120)] [TestCase(144)]
  public void ThirtyHzMovementNeverFlashesIdleBetweenRenderFrames(int renderHz){
   var cycle=new VegetableWalkCycle();cycle.Reset(Vector2.zero);
   for(int i=1;i<=renderHz*2;i++){
    float time=i/(float)renderHz;float physicsTime=Mathf.Floor(time*30+.0001f)/30;
    cycle.Advance(new Vector2(physicsTime*4,0),1f/renderHz);
    if(time>.05f)Assert.IsTrue(cycle.Moving,"Idle flash at render frame "+i);
   }
  }
  [Test] public void StopReturnsToIdleAndTeleportDoesNotStartWalking(){
   var cycle=new VegetableWalkCycle();cycle.Reset(Vector2.zero);
   cycle.Advance(new Vector2(.1f,0),1f/60);Assert.IsTrue(cycle.Moving);
   for(int i=0;i<8;i++)cycle.Advance(new Vector2(.1f,0),1f/60);
   Assert.IsFalse(cycle.Moving);
   cycle.Advance(new Vector2(10,0),1f/60);Assert.IsFalse(cycle.Moving);
  }
  [TestCase(30)] [TestCase(60)] [TestCase(144)]
  public void WalkCadenceDoesNotDependOnRenderingRate(int renderHz){
   var cycle=new VegetableWalkCycle();cycle.Reset(Vector2.zero);
   for(int i=1;i<=renderHz/4;i++)cycle.Advance(new Vector2(i*4f/renderHz,0),1f/renderHz);
   // A quarter second is three frames of the six-frame, 12 fps walk loop.
   Assert.That(cycle.Frame,Is.InRange(2,3));
  }
  [Test] public void StandingFramesHaveRegisteredFeetAndConstantCanvas(){
   for(int skin=0;skin<8;skin++){
    var first=VegetableArt.Get("veg"+skin+"_f00");var pixels=first.texture.GetPixels32();
    for(int frame=0;frame<24;frame++){
     var sprite=VegetableArt.Get("veg"+skin+"_f"+frame.ToString("00"));
     Assert.AreEqual(first.rect.size,sprite.rect.size);
     var r=sprite.rect;int bottom=int.MaxValue,left=int.MaxValue,right=-1;
     for(int y=(int)r.yMin;y<r.yMax;y++)for(int x=(int)r.xMin;x<r.xMax;x++)if(pixels[y*sprite.texture.width+x].a>180){bottom=Mathf.Min(bottom,y);left=Mathf.Min(left,x);right=Mathf.Max(right,x);}
     Assert.AreEqual(bottom-r.yMin,sprite.pivot.y,.01f,sprite.name+" foot pivot");
     Assert.AreEqual((left+right+1)*.5f-r.xMin,sprite.pivot.x,.01f,sprite.name+" centre pivot");
    }
   }
  }
 }
}
