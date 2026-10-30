using UnityEngine;
namespace BoundShoot {
 // One instance per physical rope; navigation may still use stateless Solve.
 public sealed class RopeRoute {
  Vector2[] path;
  public Vector2[] Update(RopePath geometry,Vector2 a,Vector2 b){
   var candidate=geometry.Deform(path,a,b);
   // Temporary collider overlap must not erase the original winding and let
   // the next valid frame choose the opposite side of a cover.
   if(candidate.Length>=2)path=candidate;
   return path??System.Array.Empty<Vector2>();
  }
  public void Clear(){path=null;}
 }
}
