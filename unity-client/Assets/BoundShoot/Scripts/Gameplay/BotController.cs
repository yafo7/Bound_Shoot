using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BoundShoot {
 // Server-only decisions feed the same movement, pickup, weapon and damage rules as humans.
 public sealed class BotController {
  readonly NetPlayer player;readonly MapDefinition map;readonly List<NetPlayer> players;
  readonly List<PickupState> pickups;readonly RopePath navigation,visibility;
  Vector2 destination;Vector2[] path;int waypoint;float nextDecision,nextPath,nextUse;
  NetPlayer enemy;
  public BotController(NetPlayer player,MapDefinition map,List<NetPlayer> players,List<PickupState> pickups){
   this.player=player;this.map=map;this.players=players;this.pickups=pickups;
   visibility=new RopePath(map);
   // Navigation needs body clearance; rope geometry alone has a much thinner radius.
   navigation=new RopePath(new MapDefinition{blocks=map.blocks.Select(b=>new BlockDefinition{center=b.center,size=b.size+Vector2.one*.9f}).ToArray()});
  }
  public void Tick(bool playing){
   player.RescueHeld=false;player.LastInput=Time.time;player.SelectedSlot.Value=0;
   if(!playing||!player.Alive){player.Move=Vector2.zero;player.Fire=false;return;}
   var pos=player.Body.position;var c=GameConfig.Current;
   if(player.AuthorityWorld.RescueTarget(player)!=null){
    player.RescueHeld=true;player.Move=Vector2.zero;player.Fire=false;return;
   }
   if(enemy!=null&&!player.AuthorityWorld.CanSee(player,enemy)){enemy=null;destination=pos;path=null;nextDecision=0;}
   if(Time.time>=nextDecision){
    nextDecision=Time.time+.25f;
    enemy=players.Where(p=>p.Team.Value!=player.Team.Value&&p.Alive&&player.AuthorityWorld.CanSee(player,p)).OrderBy(p=>(p.Body.position-pos).sqrMagnitude).FirstOrDefault();
    destination=pos;
    var partner=players.Where(p=>p!=player&&p.Team.Value==player.Team.Value&&Mathf.Abs(p.Seat.Value-player.Seat.Value)==1).OrderBy(p=>p.IsBot.Value?1:0).ThenBy(p=>(p.Body.position-pos).sqrMagnitude).FirstOrDefault();
    if(player.Weapon.Value==0||player.Weapon.Value==(int)WeaponKind.Dagger){
     var supply=pickups.Where(p=>p.active&&p.weapon>0&&p.weapon!=(int)WeaponKind.Dagger).OrderBy(p=>Vector2.Distance(p.position,pos)+(p.weapon==(player.Seat.Value==0?2:3)?0:2)).FirstOrDefault();
     destination=supply!=null?supply.position:enemy!=null?enemy.Body.position:partner!=null?partner.Body.position:pos;
    }else if(enemy!=null){
     var weapon=c.Weapon((WeaponKind)player.Weapon.Value);
     float range=BattleRules.IsMelee(weapon.kind)?weapon.range*.72f:Mathf.Clamp(weapon.range*.65f,2,7);
     Vector2 direction=(enemy.Body.position-pos).normalized;
     destination=enemy.Body.position-direction*range;
     if(!visibility.Visible(pos,enemy.Body.position))destination=enemy.Body.position;
    }
    var world=player.AuthorityWorld;
    var zone=(world.Objectives.zones??System.Array.Empty<ControlZone>())
     .Where((z,i)=>z!=null&&(i>=world.ZoneHeat.Count||world.ZoneHeat[i]*(player.Team.Value==0?1:-1)<100))
     .OrderBy(z=>(z.center-pos).sqrMagnitude).FirstOrDefault();
    if(zone!=null&&(player.Weapon.Value!=(int)WeaponKind.Dagger||destination==pos)){
     if((pos-zone.center).sqrMagnitude<=zone.radius*zone.radius*.8f)destination=pos;
     else destination=zone.center+Vector2.ClampMagnitude(destination-zone.center,zone.radius*.7f);
    }
    // Stay with a human teammate instead of stretching their rope across the arena.
    if(partner!=null&&partner.Alive&&!partner.IsBot.Value){
     Vector2 delta=destination-partner.Body.position;
     if(delta.magnitude>c.ropeLength*.8f)destination=partner.Body.position+delta.normalized*c.ropeLength*.8f;
    }
    destination=new Vector2(Mathf.Clamp(destination.x,-map.halfWidth+.7f,map.halfWidth-.7f),Mathf.Clamp(destination.y,-map.halfHeight+.7f,map.halfHeight-.7f));
   }
   if(Time.time>=nextPath){nextPath=Time.time+.35f;path=navigation.Solve(pos,destination);waypoint=1;}
   Vector2 movement=Vector2.zero;
   if(path!=null){
    while(waypoint<path.Length&&Vector2.Distance(pos,path[waypoint])<.1f)waypoint++;
    if(waypoint<path.Length)movement=Vector2.ClampMagnitude((path[waypoint]-pos)*3,1);
   }
   player.Move=movement;
   player.Fire=false;
   if(enemy!=null&&enemy.Alive){
    Vector2 delta=enemy.Body.position-pos;
    // Small lead and bounded aim error keep the bots useful without perfect aim.
    Vector2 lead=enemy.Body.velocity*.08f;float error=Mathf.Sin(Time.time*2.3f+player.Seat.Value+player.Team.Value*2)*.08f;
    player.Aim=(delta+lead+Vector2.Perpendicular(delta.normalized)*error).normalized;
    var weapon=c.Weapon((WeaponKind)player.Weapon.Value);
    if(weapon!=null&&weapon.kind==WeaponKind.RocketLauncher){
     Vector2 target=delta+enemy.Body.velocity*c.rocketFlightSeconds*.5f;
     player.Aim=target.normalized;
     player.AimDistance=target.magnitude;
     Vector2 landing=player.AuthorityWorld.RocketLanding(pos,player.Aim,player.AimDistance);
     player.Fire=(landing-enemy.Body.position).sqrMagnitude<c.rocketBlastRadius*c.rocketBlastRadius&&visibility.Visible(landing,enemy.Body.position);
    }else player.Fire=weapon!=null&&delta.magnitude<weapon.range&&visibility.Visible(pos,enemy.Body.position);

   }
   if(Time.time>=nextUse){
    float distance=enemy!=null&&enemy.Alive?Vector2.Distance(enemy.Body.position,pos):float.PositiveInfinity;
    for(int slot=1;slot<=3;slot++){
     var kind=(ItemKind)player.ItemAt(slot);bool use=kind==ItemKind.Heal?player.Health.Value<player.MaxHealth.Value:
      kind==ItemKind.Shield?player.Shield.Value<c.shieldCap:kind==ItemKind.Bomb?distance<c.bombRadius*.9f:
      ThrowableRules.IsThrowable(kind)?enemy!=null&&Vector2.Distance(player.AuthorityWorld.ThrowLanding(pos,player.Aim),enemy.Body.position)<(kind==ItemKind.Smoke?c.smokeRadius:c.fireRadius)*.8f:
      kind==ItemKind.Dash?movement.sqrMagnitude>.5f&&distance>4&&!float.IsInfinity(distance):
      (kind==ItemKind.Shorten||kind==ItemKind.Extend||kind==ItemKind.Elastic)&&!float.IsInfinity(distance);
     if(!use)continue;player.SelectedSlot.Value=slot;player.Fire=false;player.AuthorityWorld.TryUseSelected(player);nextUse=Time.time+1;break;
    }
   }
  }
 }
}
