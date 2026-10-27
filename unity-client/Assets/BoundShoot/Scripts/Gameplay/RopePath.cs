using System;
using System.Collections.Generic;
using UnityEngine;
namespace BoundShoot {
 // A visibility graph around expanded rectangular covers. No rigidbody chain or client physics.
 public class RopePath {
  readonly List<Vector2> corners=new List<Vector2>();
  readonly List<Rect> blocks=new List<Rect>();
  readonly float[,] graph;
  public RopePath(MapDefinition map){
   foreach(var b in map.blocks){
    var r=new Rect(b.center-b.size*.5f-Vector2.one*.12f,b.size+Vector2.one*.24f);blocks.Add(r);
    corners.Add(new Vector2(r.xMin,r.yMin));corners.Add(new Vector2(r.xMin,r.yMax));
    corners.Add(new Vector2(r.xMax,r.yMax));corners.Add(new Vector2(r.xMax,r.yMin));
   }
   graph=new float[corners.Count,corners.Count];
   for(int i=0;i<corners.Count;i++)for(int j=0;j<corners.Count;j++)graph[i,j]=Visible(corners[i],corners[j])?Vector2.Distance(corners[i],corners[j]):float.PositiveInfinity;
  }
  public bool Visible(Vector2 a,Vector2 b){
   foreach(var block in blocks){
    var r=new Rect(block.xMin+.005f,block.yMin+.005f,block.width-.01f,block.height-.01f);
    if(Intersects(a,b,r))return false;
   }return true;
  }
  static bool Intersects(Vector2 a,Vector2 b,Rect r){
   float lo=0,hi=1;Vector2 delta=b-a;
   for(int axis=0;axis<2;axis++){
    float p=axis==0?a.x:a.y,d=axis==0?delta.x:delta.y,min=axis==0?r.xMin:r.yMin,max=axis==0?r.xMax:r.yMax;
    if(Mathf.Abs(d)<.0001f){if(p<=min||p>=max)return false;}
    else{
     float t1=(min-p)/d,t2=(max-p)/d;if(t1>t2){float tmp=t1;t1=t2;t2=tmp;}
     lo=Mathf.Max(lo,t1);hi=Mathf.Min(hi,t2);if(lo>=hi)return false;
    }
   }return hi>0&&lo<1;
  }
  public Vector2[] Solve(Vector2 a,Vector2 b){
   if(Visible(a,b))return new[]{a,b};
   int n=corners.Count+2,target=n-1;var dist=new float[n];var prev=new int[n];var used=new bool[n];var points=new Vector2[n];
   points[0]=a;points[target]=b;for(int i=0;i<corners.Count;i++)points[i+1]=corners[i];
   for(int i=0;i<n;i++){dist[i]=float.PositiveInfinity;prev[i]=-1;}dist[0]=0;
   for(int step=0;step<n;step++){
    int at=-1;for(int i=0;i<n;i++)if(!used[i]&&(at<0||dist[i]<dist[at]))at=i;
    if(at<0||float.IsPositiveInfinity(dist[at]))break;if(at==target)break;used[at]=true;
    for(int j=0;j<n;j++){
     if(used[j]||at==j)continue;float weight;
     if(at>0&&at<target&&j>0&&j<target)weight=graph[at-1,j-1];
     else weight=Visible(points[at],points[j])?Vector2.Distance(points[at],points[j]):float.PositiveInfinity;
     if(dist[at]+weight<dist[j]){dist[j]=dist[at]+weight;prev[j]=at;}
    }
   }
   if(prev[target]<0)return new[]{a,b}; // Overlap/depenetration lasts at most a physics step.
   var path=new List<Vector2>();for(int p=target;p>=0;p=prev[p]){path.Add(points[p]);if(p==0)break;}
   path.Reverse();return path.ToArray();
  }
  public static float Length(Vector2[] path){float result=0;for(int i=1;i<path.Length;i++)result+=Vector2.Distance(path[i-1],path[i]);return result;}
 }
 public class RopeLink {
  public NetPlayer a,b;public Vector2[] path;public readonly LineRenderer line;
  public RopeLink(NetPlayer a,NetPlayer b,Transform parent){this.a=a;this.b=b;if(!Application.isBatchMode)line=Draw2D.Line(parent,a.Team.Value==0?Draw2D.Blue:Draw2D.Red,.095f);}
  public void Apply(RopePath solver,float maxLength,float spring,float damping,float maxForce){
   path=solver.Solve(a.Body.position,b.Body.position);
   RopePhysics.Apply(a.Body,b.Body,path,maxLength,spring,damping,maxForce);
  }
  public void Show(Vector2[] points){
   if(points==null||line==null)return;line.positionCount=points.Length;
   for(int i=0;i<points.Length;i++)line.SetPosition(i,new Vector3(points[i].x,points[i].y,0));
  }
 }
 public static class RopePhysics {
  public static void Apply(Rigidbody2D a,Rigidbody2D b,Vector2[] path,float maxLength,float spring,float damping,float maxForce){
   float extension=RopePath.Length(path)-maxLength;if(extension<=0)return;
   Vector2 da=(path[1]-path[0]).normalized,db=(path[path.Length-2]-path[path.Length-1]).normalized;
   float outward=-Vector2.Dot(a.velocity,da)-Vector2.Dot(b.velocity,db);
   float force=Mathf.Clamp(extension*spring+outward*damping,0,maxForce);
   a.AddForce(da*force);b.AddForce(db*force);
   // Remove separating velocity at a taut rope; never teleport colliding players.
   if(extension>.25f){
    float va=Vector2.Dot(a.velocity,da),vb=Vector2.Dot(b.velocity,db);
    if(va<0)a.velocity-=da*va*.8f;if(vb<0)b.velocity-=db*vb*.8f;
   }
  }
 }
}
