using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace BoundShoot {
 public partial class BattleWorld {
  static readonly ObjectiveRules defaults=new ObjectiveRules();
  public ObjectiveRules Objectives=>map?.objectives??defaults;
  public readonly NetworkVariable<float> BlueScore=new NetworkVariable<float>();
  public readonly NetworkVariable<float> RedScore=new NetworkVariable<float>();
  public NetworkList<float> ZoneHeat;
  void Awake(){ZoneHeat=new NetworkList<float>();}
  public readonly NetworkVariable<float> BlueRespawnIn=new NetworkVariable<float>();
  public readonly NetworkVariable<float> RedRespawnIn=new NetworkVariable<float>();
  readonly Dictionary<NetPlayer,NetPlayer> rescueTargets=new Dictionary<NetPlayer,NetPlayer>();
  readonly HashSet<NetPlayer> claimedBodies=new HashSet<NetPlayer>();
  readonly float[] wipeEnds={-1,-1};
  readonly List<SpriteRenderer> controlViews=new List<SpriteRenderer>();

  public NetPlayer RescueTarget(NetPlayer rescuer){
   if(!rescuer.Alive||!rescuer.Connected.Value||Phase.Value!=2)return null;
   NetPlayer nearest=null;
   float limit=Objectives.rescueDistance*Objectives.rescueDistance;
   foreach(var candidate in IsServer?Players:NetPlayer.All){
    if(candidate.NetworkManager!=NetworkManager||candidate.Team.Value!=rescuer.Team.Value||candidate.Alive||!candidate.Connected.Value)continue;
    Vector2 from=IsServer?rescuer.Body.position:(Vector2)rescuer.transform.position;
    Vector2 to=IsServer?candidate.Body.position:(Vector2)candidate.transform.position;
    float distance=(from-to).sqrMagnitude;
    if(distance>limit||!paths.Visible(from,to))continue;
    nearest=candidate;
    limit=distance;
   }
   return nearest;
  }

  bool IsRescuing(NetPlayer p)=>p.RescueHeld&&RescueTarget(p)!=null;

  void TickRescues(float dt){
   claimedBodies.Clear();
   foreach(var rescuer in Players){
    var target=rescuer.RescueHeld?RescueTarget(rescuer):null;
    if(target==null||!claimedBodies.Add(target)){
     rescuer.RescueProgress.Value=0;
     rescueTargets.Remove(rescuer);
     continue;
    }
    if(!rescueTargets.TryGetValue(rescuer,out var previous)||previous!=target){
     rescueTargets[rescuer]=target;
     rescuer.RescueProgress.Value=0;
    }
    rescuer.RescueProgress.Value+=dt;
    if(rescuer.RescueProgress.Value<Objectives.rescueSeconds)continue;
    target.Resurrect();
    target.Health.Value=Mathf.Max(1,Mathf.RoundToInt(target.MaxHealth.Value*Objectives.rescueHealth));
    rescuer.Rescues.Value++;
    rescuer.RescueProgress.Value=0;
    rescueTargets.Remove(rescuer);
    FeedbackClientRpc(target.Body.position,8);
   }
  }

  void TickControlZones(float dt){
   var zones=Objectives.zones??Array.Empty<ControlZone>();
   float blue=0,red=0;
   int count=0;
   for(int i=0;i<zones.Length;i++){
    var zone=zones[i];
    if(zone==null)continue;
    int difference=0;
    foreach(var p in Players){
     if(p.Alive&&p.Connected.Value&&(p.Body.position-zone.center).sqrMagnitude<=zone.radius*zone.radius)
      difference+=p.Team.Value==0?1:-1;
    }
    float heat=Mathf.Clamp(ZoneHeat[i]+difference*zone.pointsPerPlayer*dt,-100,100);
    if(ZoneHeat[i]!=heat)ZoneHeat[i]=heat;
    blue+=Mathf.Max(0,heat);
    red+=Mathf.Max(0,-heat);
    count++;
   }
   BlueScore.Value=count==0?0:blue/count;
   RedScore.Value=count==0?0:red/count;
  }

  void TickWipes(){
   var config=GameConfig.Current;
   float wipeSeconds=config.overrideTeamWipeSeconds?config.reviveSeconds:Objectives.wipeSeconds;
   for(int team=0;team<2;team++){
    var timer=team==0?BlueRespawnIn:RedRespawnIn;
    bool hasMembers=false,anyAlive=false;
    foreach(var p in Players){
     if(p.Team.Value!=team||!p.Connected.Value||(Training&&p.TrainingRole==2))continue;
     hasMembers=true;
     anyAlive|=p.Alive;
    }
    if(!hasMembers||anyAlive){wipeEnds[team]=-1;timer.Value=0;continue;}
    if(wipeEnds[team]<0)wipeEnds[team]=Time.time+wipeSeconds;
    timer.Value=Mathf.Max(0,wipeEnds[team]-Time.time);
    if(timer.Value>0)continue;
    var seats=team==0?map.blueSpawns:map.redSpawns;
    var anchor=team==0?Objectives.blueRespawn:Objectives.redRespawn;
    foreach(var p in Players){
     if(p.Team.Value!=team||!p.Connected.Value||(Training&&p.TrainingRole==2))continue;
     Vector2 spawn=seats[p.Seat.Value];
     if(Objectives.customRespawns)spawn=anchor+(spawn-seats[0]);
     p.Body.position=spawn;p.Body.velocity=Vector2.zero;p.transform.position=spawn;p.Position.Value=spawn;
     p.Resurrect();
    }
    foreach(var rope in ropes)if(rope.a.Team.Value==team)rope.ResetRoute();
    wipeEnds[team]=-1;
   }
  }

  void ResetObjectives(){
   BlueScore.Value=RedScore.Value=BlueRespawnIn.Value=RedRespawnIn.Value=0;
   wipeEnds[0]=wipeEnds[1]=-1;
   rescueTargets.Clear();
   ZoneHeat.Clear();
   foreach(var zone in Objectives.zones??Array.Empty<ControlZone>())ZoneHeat.Add(0);
  }

  void RenderControlZones(){
   var zones=Objectives.zones??Array.Empty<ControlZone>();
   if(Training||IsOnlineWarmup||MapKey.Value.ToString()=="Training_Local")return;
   for(int i=controlViews.Count;i<zones.Length;i++)
    controlViews.Add(Draw2D.Shape(transform,"Control zone",Vector2.zero,Vector2.one,new Color(1,.74f,.18f,.26f),true,1));
   for(int i=0;i<zones.Length;i++){
    var zone=zones[i];
    if(zone==null)continue;
    controlViews[i].transform.localPosition=zone.center;
    controlViews[i].transform.localScale=Vector3.one*(zone.radius*2);
    float heat=i<ZoneHeat.Count?ZoneHeat[i]:0;
    Color tint=Mathf.Abs(heat)<.01f?Draw2D.Gold:heat>0?Draw2D.Blue:Draw2D.Red;
    tint.a=Mathf.Abs(heat)>=Objectives.captureHeat?.38f:.22f;
    controlViews[i].color=tint;
   }
  }
 }
}
