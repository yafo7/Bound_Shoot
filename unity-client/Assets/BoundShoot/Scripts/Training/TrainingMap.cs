using System;
using System.Linq;
using UnityEngine;
namespace BoundShoot {
 public static class TrainingMap {
  public const string WarmupId="Warmup_Online";
  public static MapDefinition Warmup {get;}=CreateWarmup();
  static MapDefinition CreateWarmup(){
   var map=Create();
   map.id=WarmupId;
   map.name="联机热身场";
   return map;
  }
  public static MapDefinition Create(){
   var weapons=new[]{WeaponKind.Sniper,WeaponKind.Pistol,WeaponKind.AK,WeaponKind.RocketLauncher,WeaponKind.Greatsword}
    .Select((kind,i)=>new PickupDefinition{position=new Vector2(-13,7-i*4),weapon=kind,respawnSeconds=2}).ToArray();
   var items=Enumerable.Range(1,10).Select((kind,i)=>new PickupDefinition{position=new Vector2(-6+(i%4)*4,8-(i/4)*3),item=(ItemKind)kind,respawnSeconds=2}).ToArray();
   var zones=Array.Empty<RevivalDefinition>();
   var drops=weapons.Concat(items).ToArray();
   return new MapDefinition{id="Training_Local",name="训练房",available=true,layoutVersion=1,halfWidth=18,halfHeight=12,
    groundColor=new Color(.40f,.54f,.25f),floors=Array.Empty<FloorDefinition>(),
    blueSpawns=Enumerable.Range(0,4).Select(i=>new Vector2(-12+i*2,-8)).ToArray(),
    redSpawns=new[]{new Vector2(12,1),new Vector2(12,5),new Vector2(14,7),new Vector2(14,10)},
    blocks=new[]{new BlockDefinition{center=new Vector2(-2,0),size=new Vector2(1.6f,2)},new BlockDefinition{center=new Vector2(3,0),size=new Vector2(1.6f,2)}},
    pickups=drops,supplies=drops.Select(p=>p.position).ToArray(),revivalZones=zones,revivals=zones.Select(z=>z.center).ToArray()};
  }
 }
}
