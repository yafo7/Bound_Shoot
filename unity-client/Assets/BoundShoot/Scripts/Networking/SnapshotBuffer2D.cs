using System;
using System.Collections.Generic;
using UnityEngine;
namespace BoundShoot {
 // Timestamped authority samples; render time advances independently of arrival bursts.
 public sealed class SnapshotBuffer2D {
  struct Sample {public double time;public Vector2 position;public float angle;}
  readonly List<Sample> samples=new List<Sample>(64);
  public void Clear()=>samples.Clear();
  public void Push(double time,Vector2 position,float angle) {
   if(double.IsNaN(time)||double.IsInfinity(time)||float.IsNaN(position.x)||float.IsNaN(position.y)||float.IsInfinity(position.x)||float.IsInfinity(position.y)||float.IsNaN(angle)||float.IsInfinity(angle))return;
   if(samples.Count>0&&time<=samples[samples.Count-1].time)return;
   samples.Add(new Sample{time=time,position=position,angle=angle});
   if(samples.Count>64)samples.RemoveAt(0);
  }
  public bool Evaluate(double time,double extrapolationLimit,out Vector2 position,out float angle) {
   position=Vector2.zero;angle=0;if(samples.Count==0)return false;
   while(samples.Count>2&&samples[1].time<=time)samples.RemoveAt(0);
   var first=samples[0];position=first.position;angle=first.angle;
   if(time<=first.time)return true;
   for(int i=1;i<samples.Count;i++){
    var next=samples[i];
    if(time<=next.time){
     float alpha=Mathf.Clamp01((float)((time-first.time)/(next.time-first.time)));
     position=Vector2.Lerp(first.position,next.position,alpha);angle=Mathf.LerpAngle(first.angle,next.angle,alpha);return true;
    }first=next;
   }
   var last=samples[samples.Count-1];position=last.position;angle=last.angle;
   if(samples.Count>=2){
    var prev=samples[samples.Count-2];double span=last.time-prev.time;
    if(span>0){
     double extra=Math.Max(0,Math.Min(Math.Max(0,extrapolationLimit),time-last.time));
     position+=(last.position-prev.position)*(float)(extra/span);
    }
   }
   return true;
  }
 }
}
