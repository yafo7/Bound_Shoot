using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace BoundShoot {
 public static class FieldMap {
  public const string Id="Field_01";
  public static MapDefinition Create(){
   var walls=new List<BlockDefinition>();
   // Mirrored staging corners leave a wide, readable route into the centre.
   foreach(int side in new[]{-1,1}){
    walls.Add(Block(new Vector2(side*25,5),new Vector2(3,3),0));
    walls.Add(Block(new Vector2(side*21,-6),new Vector2(3,4),1));
    walls.Add(Block(new Vector2(side*12,8),new Vector2(3,5),0));
    walls.Add(Block(new Vector2(side*10,-9),new Vector2(4,3),1));
    walls.Add(Block(new Vector2(side*5,2),new Vector2(3,3),2));
    walls.Add(Block(new Vector2(side*5,-4),new Vector2(2,3),0));
    walls.Add(Block(new Vector2(side*18,20),new Vector2(3,3),4));
    walls.Add(Block(new Vector2(side*19,-20),new Vector2(3,3),4));
   }
   walls.Add(Block(new Vector2(0,13),new Vector2(4,2),1));
   walls.Add(Block(new Vector2(0,-14),new Vector2(4,2),2));
   var pickups=new List<PickupDefinition>();
   foreach(int side in new[]{-1,1}){
    for(int i=0;i<4;i++)pickups.Add(new PickupDefinition{position=new Vector2(side*28,-4.5f+i*3),weapon=(WeaponKind)(i%2==0?2:3),respawnSeconds=10});
    pickups.Add(new PickupDefinition{position=new Vector2(side*16,13),weapon=WeaponKind.Sniper,respawnSeconds=12});
    pickups.Add(new PickupDefinition{position=new Vector2(side*15,-14),weapon=WeaponKind.Dagger,respawnSeconds=10});
    for(int i=0;i<4;i++)pickups.Add(new PickupDefinition{position=new Vector2(side*(7+(i%2)*9),i<2?17:-18),item=(ItemKind)(i+1+(side>0?4:0)),respawnSeconds=12});
   }
   return new MapDefinition{id=Id,name="丰收野地",available=true,theme="meadow",layoutVersion=1,halfWidth=40,halfHeight=32,
    blueSpawns=Enumerable.Range(0,4).Select(i=>new Vector2(-31,-4.5f+i*3)).ToArray(),
    redSpawns=Enumerable.Range(0,4).Select(i=>new Vector2(31,-4.5f+i*3)).ToArray(),
    blocks=walls.ToArray(),floors=new FloorDefinition[0],groundColor=new Color(.43f,.57f,.31f),pickups=pickups.ToArray(),supplies=pickups.Select(p=>p.position).ToArray(),
    revivalZones=new[]{new RevivalDefinition{center=new Vector2(-17,0),size=new Vector2(4,4)},new RevivalDefinition{center=new Vector2(17,0),size=new Vector2(4,4)}},revivals=new[]{new Vector2(-17,0),new Vector2(17,0)}};
  }
  static BlockDefinition Block(Vector2 center,Vector2 size,int visual)=>new BlockDefinition{center=center,size=size,visual=visual};
 }
}
