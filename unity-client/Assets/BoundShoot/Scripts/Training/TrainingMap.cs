using System;
using System.Linq;
using UnityEngine;
namespace BoundShoot {
 public static class TrainingMap {
  public static MapDefinition Create(){
   var weapons=Enumerable.Range(1,4).Select((kind,i)=>new PickupDefinition{position=new Vector2(-13,7-i*4),weapon=(WeaponKind)kind,respawnSeconds=2}).ToArray();
   var items=Enumerable.Range(1,8).Select((kind,i)=>new PickupDefinition{position=new Vector2(-6+(i%4)*4,8-(i/4)*3),item=(ItemKind)kind,respawnSeconds=2}).ToArray();
   var zones=new[]{new RevivalDefinition{center=new Vector2(9,-8),size=new Vector2(4,3)}};
   var drops=weapons.Concat(items).ToArray();
   return new MapDefinition{id="Training_Local",name="训练房",available=true,layoutVersion=1,halfWidth=18,halfHeight=12,
    groundColor=new Color(.13f,.19f,.23f),floors=Array.Empty<FloorDefinition>(),
    blueSpawns=Enumerable.Range(0,4).Select(i=>new Vector2(-12+i*2,-8)).ToArray(),
    redSpawns=new[]{new Vector2(12,1),new Vector2(12,5),new Vector2(14,7),new Vector2(14,10)},
    blocks=new[]{new BlockDefinition{center=new Vector2(-2,0),size=new Vector2(1.6f,2)},new BlockDefinition{center=new Vector2(3,0),size=new Vector2(1.6f,2)}},
    pickups=drops,supplies=drops.Select(p=>p.position).ToArray(),revivalZones=zones,revivals=zones.Select(z=>z.center).ToArray()};
  }
 }
}
