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
   player.LastInput=Time.time;player.Use=false;
   if(!playing||!player.Alive){player.Move=Vector2.zero;player.Fire=false;return;}
   var pos=player.Body.position;var c=GameConfig.Current;
   if(Time.time>=nextDecision){
    nextDecision=Time.time+.25f;
    enemy=players.Where(p=>p.Team.Value!=player.Team.Value&&p.Alive).OrderBy(p=>(p.Body.position-pos).sqrMagnitude).FirstOrDefault();
    destination=pos;
    var partner=players.FirstOrDefault(p=>p!=player&&p.Team.Value==player.Team.Value);
    if(partner!=null&&!partner.Alive&&MapContent.Revivals(map).Length>0)
     destination=MapContent.Revivals(map).OrderBy(v=>(v.center-partner.Body.position).sqrMagnitude).First().center;
    else if(player.Weapon.Value==0){
     var supply=pickups.Where(p=>p.active&&p.weapon>0).OrderBy(p=>Vector2.Distance(p.position,pos)+(p.weapon==(player.Seat.Value==0?2:3)?0:2)).FirstOrDefault();
     destination=supply!=null?supply.position:partner!=null?partner.Body.position:pos;
    }else if(enemy!=null){
     var weapon=c.Weapon((WeaponKind)player.Weapon.Value);
     float range=weapon.kind==WeaponKind.Dagger ? .8f : Mathf.Clamp(weapon.range*.65f,2,7);
     Vector2 direction=(enemy.Body.position-pos).normalized;
     destination=enemy.Body.position-direction*range;
     if(!visibility.Visible(pos,enemy.Body.position))destination=enemy.Body.position;
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
    player.Fire=weapon!=null&&delta.magnitude<weapon.range&&visibility.Visible(pos,enemy.Body.position);
    if(Time.time>=nextUse&&player.Item.Value!=0){
     bool use=(ItemKind)player.Item.Value==ItemKind.Bomb?delta.magnitude<c.bombRadius*.9f:movement.sqrMagnitude>.5f&&delta.magnitude>4;
     if(use){player.Use=true;nextUse=Time.time+1;}
    }
   }
  }
 }
}
