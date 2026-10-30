using System.Collections.Generic;
using UnityEngine;
namespace BoundShoot {
 // Immutable samples separate rendering from authority's mutable projectile list.
 public sealed class ProjectileTrack {
  public const double MaxExtrapolation=.15;
  public Vector2 Position,Direction;public float Speed,Remaining;public double SampleTime,BornAt;
  public void Sample(ProjectileState state,double at){Position=state.position;Direction=state.direction;Speed=state.speed;Remaining=state.remaining;SampleTime=at;BornAt=state.bornAt;}
  public Vector2 Evaluate(double at){float distance=Mathf.Min(Remaining,Speed*(float)System.Math.Max(0,System.Math.Min(MaxExtrapolation,at-SampleTime)));return Position+Direction*distance;}
 }
 public sealed class ProjectileVisuals:MonoBehaviour {
  sealed class View {public readonly ProjectileTrack track=new ProjectileTrack();public SpriteRenderer head,trail;public int weapon,team;}
  readonly Dictionary<ulong,View> active=new Dictionary<ulong,View>();readonly Stack<View> pool=new Stack<View>();
  readonly HashSet<ulong> retired=new HashSet<ulong>();readonly Queue<ulong> retiredOrder=new Queue<ulong>();
  readonly HashSet<ulong> observed=new HashSet<ulong>();readonly List<ulong> removed=new List<ulong>();
  BattleWorld world;double lastSnapshot=double.NegativeInfinity;
  public int ActiveCount=>active.Count;
  public void Initialize(BattleWorld owner){world=owner;}
  public bool TryGetPosition(ulong id,out Vector2 position){if(active.TryGetValue(id,out var v)){position=v.head.transform.position;return true;}position=default;return false;}
  View Acquire(){
   if(pool.Count>0)return pool.Pop();
   return new View{head=Draw2D.Shape(transform,"Projectile head",Vector2.zero,new Vector2(.21f,.075f),Color.white,false,12),trail=Draw2D.Shape(transform,"Projectile trail",Vector2.zero,Vector2.one,Color.white,false,11)};
  }
  public void Spawn(ProjectileState state,double at){
   if(retired.Contains(state.id)||active.ContainsKey(state.id))return;
   var view=Acquire();view.weapon=state.weapon;view.team=state.team;view.track.Sample(state,at);active.Add(state.id,view);
  }
  public void Reconcile(ProjectileState[] states,double at){
   if(at<lastSnapshot)return;lastSnapshot=at;observed.Clear();
   foreach(var state in states){observed.Add(state.id);Spawn(state,at);if(active.TryGetValue(state.id,out var view))view.track.Sample(state,at);}
   removed.Clear();foreach(var entry in active)if(!observed.Contains(entry.Key)&&entry.Value.track.BornAt<=at)removed.Add(entry.Key);
   foreach(ulong id in removed)Retire(id);
  }
  public void Retire(ulong id){
   if(active.TryGetValue(id,out var view)){view.head.enabled=view.trail.enabled=false;active.Remove(id);pool.Push(view);}
   if(retired.Add(id))retiredOrder.Enqueue(id);
   while(retiredOrder.Count>2048)retired.Remove(retiredOrder.Dequeue());
  }
  public void Clear(){
   foreach(var view in active.Values){view.head.enabled=view.trail.enabled=false;pool.Push(view);}active.Clear();retired.Clear();retiredOrder.Clear();lastSnapshot=double.NegativeInfinity;
  }
  void Update(){if(world==null||!world.IsSpawned)return;Render(world.NetworkManager.ServerTime.Time);}
  public void Render(double now){
   foreach(var view in active.Values){
    var track=view.track;Vector2 position=track.Evaluate(now),delta=position-track.Position;
    // Never extrapolate an authoritative projectile through visible cover.
    var obstacle=delta.sqrMagnitude>.000001f?Physics2D.Raycast(track.Position,delta.normalized,delta.magnitude,1<<8):default;
    bool visible=obstacle.collider==null;view.head.enabled=view.trail.enabled=visible;if(!visible)continue;
    float angle=Mathf.Atan2(track.Direction.y,track.Direction.x)*Mathf.Rad2Deg;
    Color colour=view.team==0?Draw2D.Blue:Draw2D.Red;view.head.color=Color.Lerp(colour,Color.white,.65f);
    view.head.transform.position=position;view.head.transform.rotation=Quaternion.Euler(0,0,angle);
    float length=WeaponPresentation.Get(view.weapon)?.trailLength??.25f;
    // Clip the trail against cover too; it must not appear on a wall's far side.
    var trailHit=Physics2D.Raycast(position,-track.Direction,length,1<<8);if(trailHit.collider!=null)length=trailHit.distance;
    view.trail.transform.position=position-track.Direction*length*.5f;view.trail.transform.rotation=view.head.transform.rotation;
    view.trail.transform.localScale=new Vector3(length,.04f,1);view.trail.color=new Color(colour.r,colour.g,colour.b,.45f);
   }
  }
 }
}
