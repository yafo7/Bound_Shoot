using System;
using System.Collections.Generic;
using UnityEngine;
namespace BoundShoot {
 [Serializable] public sealed class ThrowableState {
  public bool rocket;[NonSerialized]public int damage;
  public ulong id;public int kind,team;public Vector2 start,landing;public double launchedAt,landAt,endAt;public float radius;
  [NonSerialized]public NetPlayer owner;
  public bool Active(double now)=>now>=landAt&&now<endAt;
 }
 public static class ThrowableRules {
  public static bool IsThrowable(ItemKind kind)=>kind==ItemKind.Smoke||kind==ItemKind.Incendiary;
  // Search backwards along the same ray. Intermediate walls do not obstruct the arc.
  public static Vector2 Landing(MapDefinition map,Vector2 start,Vector2 aim,float distance){
   Vector2 direction=aim.sqrMagnitude>.0001f?aim.normalized:Vector2.right;
   int steps=Mathf.Max(1,Mathf.CeilToInt(distance/.05f));
   for(int i=0;i<=steps;i++){
    Vector2 p=start+direction*(distance*(1-i/(float)steps));
    if(Mathf.Abs(p.x)>map.halfWidth-.2f||Mathf.Abs(p.y)>map.halfHeight-.2f)continue;
    bool blocked=false;foreach(var b in map.blocks??Array.Empty<BlockDefinition>())if(Mathf.Abs(p.x-b.center.x)<=b.size.x*.5f+.15f&&Mathf.Abs(p.y-b.center.y)<=b.size.y*.5f+.15f){blocked=true;break;}
    if(!blocked)return p;
   }return start;
  }
  public static bool Intersects(Vector2 a,Vector2 b,Vector2 centre,float radius){
   Vector2 delta=b-a;float t=delta.sqrMagnitude<.000001f?0:Mathf.Clamp01(Vector2.Dot(centre-a,delta)/delta.sqrMagnitude);
   return (a+delta*t-centre).sqrMagnitude<=radius*radius;
  }
  public static bool SmokeBlocks(IEnumerable<ThrowableState> areas,Vector2 a,Vector2 b,double now){
   foreach(var area in areas)if(area.kind==(int)ItemKind.Smoke&&area.Active(now)&&Intersects(a,b,area.landing,area.radius))return true;return false;
  }
 }
}
