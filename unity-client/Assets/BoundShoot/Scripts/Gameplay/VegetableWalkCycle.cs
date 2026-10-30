using UnityEngine;
namespace BoundShoot {
 // Render frames need not coincide with physics/network position samples.
 public sealed class VegetableWalkCycle {
  public const float FramesPerSecond=12,StopGraceSeconds=.1f;
  Vector2 previous;bool initialized;float stillFor,phase;
  public bool Moving {get;private set;}
  public int Frame=>Mathf.FloorToInt(phase)%6;
  public void Reset(Vector2 position){previous=position;initialized=true;Moving=false;stillFor=StopGraceSeconds;phase=0;}
  public void Advance(Vector2 position,float deltaTime){
   if(!initialized){Reset(position);return;}
   float distance=Vector2.Distance(position,previous);previous=position;
   // A respawn/teleport is not a step. Avoid advancing through a stalled frame.
   if(distance>2){Reset(position);return;}
   float dt=Mathf.Clamp(deltaTime,0,.1f);
   bool sampledMotion=distance>Mathf.Max(.0001f,dt*.04f);
   if(sampledMotion){if(!Moving)phase=0;Moving=true;stillFor=0;}
   else stillFor+=dt;
   if(stillFor>=StopGraceSeconds)Moving=false;
   if(Moving)phase=Mathf.Repeat(phase+dt*FramesPerSecond,6);
  }
 }
}
