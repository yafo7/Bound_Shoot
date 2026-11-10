using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace BoundShoot {
 public partial class BattleWorld {
  readonly List<ThrowableState> throwables=new List<ThrowableState>();
  readonly Dictionary<NetPlayer,float> burnFractions=new Dictionary<NetPlayer,float>();
  ThrowableState[] receivedThrowables=Array.Empty<ThrowableState>();
  ulong nextThrowableId;ThrowableVisuals throwableVisuals;
  public IReadOnlyList<ThrowableState> Throwables=>IsServer?(IReadOnlyList<ThrowableState>)throwables:receivedThrowables;
  public double AreaTime=>NetworkManager.ServerTime.Time;
  public NetPlayer LocalViewer=>NetPlayer.All.FirstOrDefault(p=>p!=null&&p.NetworkManager==NetworkManager&&p.IsOwner&&!p.IsBot.Value&&p.Connected.Value&&p.TrainingRole==0);
  public Vector2 ThrowLanding(Vector2 start,Vector2 aim)=>ThrowableRules.Landing(map,start,aim,GameConfig.Current.throwDistance);
  void Throw(NetPlayer p,ItemKind kind){
   var c=GameConfig.Current;double now=AreaTime;bool smoke=kind==ItemKind.Smoke;
   throwables.Add(new ThrowableState{id=++nextThrowableId,kind=(int)kind,team=p.Team.Value,owner=p,start=p.Body.position,landing=ThrowLanding(p.Body.position,p.Aim),
    launchedAt=now,landAt=now+c.throwFlightSeconds,endAt=now+c.throwFlightSeconds+(smoke?c.smokeSeconds:c.fireSeconds),radius=smoke?c.smokeRadius:c.fireRadius});
  }
  public Vector2 RocketLanding(Vector2 start,Vector2 aim,float distance){
   return ThrowableRules.Landing(map,start,aim,Mathf.Clamp(distance,0,GameConfig.Current.Weapon(WeaponKind.RocketLauncher).range));
  }
  void LaunchRocket(NetPlayer p,WeaponDefinition weapon){
   double now=AreaTime,impact=now+GameConfig.Current.rocketFlightSeconds;
   Vector2 start=p.Body.position;
   throwables.Add(new ThrowableState{
    id=++nextThrowableId,rocket=true,owner=p,team=p.Team.Value,damage=weapon.damage,start=start,
    landing=RocketLanding(start,p.Aim,p.AimDistance),launchedAt=now,landAt=impact,endAt=impact,
    radius=GameConfig.Current.rocketBlastRadius
   });
  }
  void ResolveRocket(ThrowableState rocket){
   foreach(var p in Players){
    if(!CanHit(p,rocket.team,rocket.owner)||(p.Body.position-rocket.landing).sqrMagnitude>rocket.radius*rocket.radius)continue;
    if(paths.Visible(rocket.landing,p.Body.position))p.Hurt(rocket.damage,rocket.owner);
   }
   ExplodeClientRpc(rocket.landing,rocket.radius);
  }
  void ClearThrowables(){throwables.Clear();receivedThrowables=Array.Empty<ThrowableState>();burnFractions.Clear();throwableVisuals?.Clear();}
  void TickThrowables(float dt){
   double now=AreaTime;var c=GameConfig.Current;
   foreach(var p in Players){
    if(!p.Alive||!p.Connected.Value||Time.time<p.ProtectionUntil){burnFractions.Remove(p);continue;}
    ThrowableState source=null;float maximum=0;
    foreach(var a in throwables){
     if(a.kind!=(int)ItemKind.Incendiary||now<a.landAt||now-dt>=a.endAt||(p.Body.position-a.landing).sqrMagnitude>a.radius*a.radius||!paths.Visible(a.landing,p.Body.position))continue;
     float seconds=(float)Math.Max(0,Math.Min(now,a.endAt)-Math.Max(now-dt,a.landAt));
     float damage=seconds*(a.team==p.Team.Value?c.fireFriendlyDamagePerSecond:c.fireDamagePerSecond);
     if(damage>maximum){maximum=damage;source=a;}
    }
    if(source==null){burnFractions.Remove(p);continue;}
    burnFractions.TryGetValue(p,out float fraction);fraction+=maximum;
    int amount=Mathf.FloorToInt(fraction+.00001f);burnFractions[p]=Mathf.Max(0,fraction-amount);
    if(amount>0)p.HurtFromFire(amount,source.owner,source.team);
   }
   foreach(var a in throwables)if(a.rocket&&now>=a.landAt)ResolveRocket(a);
   throwables.RemoveAll(a=>now>=a.endAt);
  }
  public bool SmokeBlocks(Vector2 start,Vector2 target)=>Phase.Value==2&&ThrowableRules.SmokeBlocks(Throwables,start,target,AreaTime);
  public bool CanSee(NetPlayer observer,NetPlayer target)=>observer==null||target==null||observer.Team.Value==target.Team.Value||!SmokeBlocks(observer.Body.position,target.Body.position);
  public bool CanSeeFromLocal(NetPlayer target){var local=LocalViewer;return local==null||local.Team.Value==target.Team.Value||!SmokeBlocks(local.transform.position,target.transform.position);}
  public bool LocalEffectVisible(Vector2 point){var local=LocalViewer;return local==null||!SmokeBlocks(local.transform.position,point);}
  public bool RopeVisible(Vector2[] points,int team){
   var local=LocalViewer;if(local==null||local.Team.Value==team||points==null)return true;
   for(int i=0;i<points.Length;i++)if(SmokeBlocks(local.transform.position,points[i])||(i>0&&SmokeBlocks(points[i-1],points[i])))return false;
   return true;
  }
  void RenderThrowables(){
   if(throwableVisuals==null){throwableVisuals=new GameObject("Throwable areas").AddComponent<ThrowableVisuals>();throwableVisuals.transform.SetParent(transform,false);}
   throwableVisuals.Render(this);
  }
 }
}
