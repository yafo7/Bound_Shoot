using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace BoundShoot {
 public static class ForestMap {
  public const string Id="Forest_01";
  public static int SurfaceAt(MapDefinition map,Vector2 p){
   var h=map?.plateau;if(h==null||h.size.x<=0||h.size.y<=0||h.stairs==null)return 0;
   if(Mathf.Abs(Mathf.Abs(p.x-h.center.x)-h.size.x*.5f)<1.8f&&h.stairs.Any(y=>Mathf.Abs(p.y-y)<h.stairWidth*.5f))return 2;
   return Mathf.Abs(p.x-h.center.x)<h.size.x*.5f&&Mathf.Abs(p.y-h.center.y)<h.size.y*.5f?1:0;
  }
  public static MapDefinition Create(){
   var blocks=new List<BlockDefinition>();
   foreach(int side in new[]{-1,1}){
    foreach(var band in new[]{new Vector2(-16,-13),new Vector2(-7,-3),new Vector2(3,7),new Vector2(13,16)})blocks.Add(new BlockDefinition{center=new Vector2(side*18,(band.x+band.y)*.5f),size=new Vector2(1.1f,band.y-band.x),visual=5});
    blocks.Add(new BlockDefinition{center=new Vector2(0,side*16),size=new Vector2(37,1.1f),visual=5});
    blocks.Add(new BlockDefinition{center=new Vector2(side*6,5),size=new Vector2(2,2),visual=0});
    blocks.Add(new BlockDefinition{center=new Vector2(side*7,-6),size=new Vector2(2,2),visual=2});
    blocks.Add(new BlockDefinition{center=new Vector2(side*25,9),size=new Vector2(1.4f,1.4f),visual=4});
    blocks.Add(new BlockDefinition{center=new Vector2(side*25,-13),size=new Vector2(1.4f,1.4f),visual=4});
   }
   var drops=new List<PickupDefinition>();
   foreach(int side in new[]{-1,1}){
    for(int i=0;i<4;i++)drops.Add(new PickupDefinition{position=new Vector2(side*26,-4.5f+i*3),weapon=(WeaponKind)(i%2==0?2:3),respawnSeconds=8});
    drops.Add(new PickupDefinition{position=new Vector2(side*10,11),weapon=WeaponKind.Sniper,respawnSeconds=12});
    drops.Add(new PickupDefinition{position=new Vector2(side*10,-10),weapon=WeaponKind.Dagger,respawnSeconds=10});
    for(int i=0;i<4;i++)drops.Add(new PickupDefinition{position=new Vector2(side*(3+i*3),i%2==0?8:-11),item=(ItemKind)(i==3?8:i+3),respawnSeconds=12});
   }
   return new MapDefinition{id=Id,name="林间高台",theme="forest",available=true,layoutVersion=1,halfWidth=36,halfHeight=24,
    plateau=new PlateauDefinition{center=Vector2.zero,size=new Vector2(36,32),stairs=new[]{-10f,0,10f},stairWidth=6},
    blueSpawns=Enumerable.Range(0,4).Select(i=>new Vector2(-29,-4.5f+i*3)).ToArray(),redSpawns=Enumerable.Range(0,4).Select(i=>new Vector2(29,-4.5f+i*3)).ToArray(),
    blocks=blocks.ToArray(),floors=new FloorDefinition[0],pickups=drops.ToArray(),supplies=drops.Select(p=>p.position).ToArray(),revivals=new Vector2[0],revivalZones=new RevivalDefinition[0],groundColor=new Color(.40f,.54f,.25f)};
  }
 }
}
