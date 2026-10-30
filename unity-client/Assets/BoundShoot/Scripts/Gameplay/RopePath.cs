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
  // A visible chord alone is insufficient: the moving chord must not sweep
  // across a cover enclosed by the old bend and its replacement.
  public bool CanShortcut(Vector2 a,Vector2 bend,Vector2 b){
   if(!Visible(a,b))return false;
   if(Mathf.Abs(Cross(bend-a,b-a))<.000001f)return true;
   foreach(var block in blocks){
    var r=new Rect(block.xMin+.005f,block.yMin+.005f,block.width-.01f,block.height-.01f);
    if(Intersects(a,bend,r)||Intersects(bend,b,r)||Intersects(a,b,r))return false;
    if(InTriangle(new Vector2(r.xMin,r.yMin),a,bend,b)||InTriangle(new Vector2(r.xMin,r.yMax),a,bend,b)||
       InTriangle(new Vector2(r.xMax,r.yMin),a,bend,b)||InTriangle(new Vector2(r.xMax,r.yMax),a,bend,b))return false;
   }
   return true;
  }
  static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
  static bool InTriangle(Vector2 p,Vector2 a,Vector2 b,Vector2 c){
   float x=Cross(b-a,p-a),y=Cross(c-b,p-b),z=Cross(a-c,p-c);
   return (x>=0&&y>=0&&z>=0)||(x<=0&&y<=0&&z<=0);
  }
  public Vector2[] Deform(Vector2[] previous,Vector2 a,Vector2 b){
   if(previous==null||previous.Length<2)return Solve(a,b);
   var points=new List<Vector2>{a};points.AddRange(previous);points.Add(b);
   // Endpoint motion can step across an expanded corner even when the body
   // itself clears the cover. Repair only that local segment, never reroute
   // the whole rope into a different winding around an obstacle.
   for(int i=1;i<points.Count;i++)if(!Visible(points[i-1],points[i])){
    var repair=Solve(points[i-1],points[i]);if(repair.Length<2)return Array.Empty<Vector2>();
    for(int j=1;j<repair.Length-1;j++)points.Insert(i++,repair[j]);
   }
   int at=1;
   while(at<points.Count-1){
    if(CanShortcut(points[at-1],points[at],points[at+1])){points.RemoveAt(at);at=Mathf.Max(1,at-1);}
    else at++;
   }
   return points.ToArray();
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
   if(prev[target]<0)return Array.Empty<Vector2>(); // Never fall back to a segment through a wall.
   var path=new List<Vector2>();for(int p=target;p>=0;p=prev[p]){path.Add(points[p]);if(p==0)break;}
   path.Reverse();return path.ToArray();
  }
  public static float Length(Vector2[] path){float result=0;for(int i=1;i<path.Length;i++)result+=Vector2.Distance(path[i-1],path[i]);return result;}
 }
 public class RopeLink {
  public NetPlayer a,b;public Vector2[] path;readonly RopeLineView view;public LineRenderer line=>view?.Line;
  public float Tension {get;private set;}
  readonly RopeRoute route=new RopeRoute();
  bool taut;float nextSound;
  public RopeLink(NetPlayer a,NetPlayer b,Transform parent){this.a=a;this.b=b;if(!Application.isBatchMode||Draw2D.RenderInBatch)view=new RopeLineView(parent);}
  public void Apply(RopePath solver,float maxLength,float spring,float damping,float maxForce){
   path=route.Update(solver,a.Body.position,b.Body.position);
   RopePhysics.Apply(a.Body,b.Body,path,maxLength,spring,damping,maxForce);
   Tension=maxLength>.001f?RopePath.Length(path)/maxLength:0;bool tight=Tension>1;
   if(tight&&!taut&&Time.time>nextSound){GardenFx.Tone(5,.08f,(a.Body.position+b.Body.position)*.5f);nextSound=Time.time+.5f;}taut=tight;
  }
  public void ResetRoute(){route.Clear();path=null;Tension=0;view?.Show(null,a.Team.Value,0);}
  public void Show(Vector2[] points){
   view?.Show(points,a.Team.Value,Tension);
  }
 }
 public static class RopePhysics {
  public static void Apply(Rigidbody2D a,Rigidbody2D b,Vector2[] path,float maxLength,float spring,float damping,float maxForce){
   if(path==null||path.Length<2)return;
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
