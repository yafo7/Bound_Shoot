using System.Collections.Generic;
using UnityEngine;
namespace BoundShoot {
 // Display only. Never writes a rendered bend back into the physical rope route.
 public sealed class RopeLineView {
  public readonly LineRenderer Line;readonly LineRenderer[] pull=new LineRenderer[2];readonly List<SpriteRenderer> bends=new List<SpriteRenderer>();
  public RopeLineView(Transform parent){
   Line=Draw2D.Line(parent,Color.white,.06f,5);
   for(int i=0;i<pull.Length;i++)pull[i]=Draw2D.Line(Line.transform,Color.white,.035f,9);
  }
  public static float Width(float tension)=>Mathf.Lerp(.055f,.095f,Mathf.Clamp01((tension-.65f)/.55f));
  public void Show(Vector2[] points,int team,float tension){
   bool visible=points!=null&&points.Length>=2;Line.positionCount=visible?points.Length:0;
   Color colour=Color.Lerp(team==0?Draw2D.Blue:Draw2D.Red,Color.white,Mathf.Lerp(.02f,.45f,Mathf.Clamp01(tension-.6f)));
   Line.startColor=Line.endColor=colour;Line.startWidth=Line.endWidth=Width(tension);
   if(visible)for(int i=0;i<points.Length;i++)Line.SetPosition(i,points[i]);
   int corners=visible?Mathf.Max(0,points.Length-2):0;
   while(bends.Count<corners)bends.Add(Draw2D.Shape(Line.transform,"Rope bend",Vector2.zero,Vector2.one*.12f,Color.white,false,9));
   for(int i=0;i<bends.Count;i++){var bend=bends[i];bend.enabled=i<corners;if(!bend.enabled)continue;bend.transform.position=points[i+1];bend.transform.rotation=Quaternion.Euler(0,0,45);bend.color=colour;}
   for(int i=0;i<pull.Length;i++){
    var arrow=pull[i];bool show=visible&&tension>=.9f;arrow.positionCount=show?3:0;if(!show)continue;
    Vector2 start=i==0?points[0]:points[points.Length-1],next=i==0?points[1]:points[points.Length-2];
    Vector2 direction=(next-start).normalized,tip=start+direction*.34f,side=Vector2.Perpendicular(direction)*.1f;
    arrow.SetPosition(0,tip-direction*.15f+side);arrow.SetPosition(1,tip);arrow.SetPosition(2,tip-direction*.15f-side);arrow.startColor=arrow.endColor=colour;
   }
  }
 }
}
