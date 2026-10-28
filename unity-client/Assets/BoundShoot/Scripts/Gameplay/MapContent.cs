using System;
using System.Linq;
using UnityEngine;
namespace BoundShoot {
 public static class MapContent {
  public static readonly Color DefaultGround=new Color(.13f,.19f,.23f);
  public static PickupDefinition[] Pickups(MapDefinition map,float legacyRespawn=10){
   if(map.layoutVersion>=1)return map.pickups??Array.Empty<PickupDefinition>();
   return (map.supplies??Array.Empty<Vector2>()).Select((p,i)=>{
    int local=i<5?i:i<10?i-5:4;
    return new PickupDefinition{position=p,weapon=local<4?(WeaponKind)(local+1):WeaponKind.None,
     item=local<4?ItemKind.None:ItemKind.Mystery,respawnSeconds=legacyRespawn,randomizeOnRespawn=true};
   }).ToArray();
  }
  public static RevivalDefinition[] Revivals(MapDefinition map)=>map.layoutVersion>=1?map.revivalZones??Array.Empty<RevivalDefinition>():(map.revivals??Array.Empty<Vector2>()).Select(p=>new RevivalDefinition{center=p,size=new Vector2(4,3)}).ToArray();
  public static bool InRevival(MapDefinition map,Vector2 position)=>Revivals(map).Any(r=>Mathf.Abs(position.x-r.center.x)<r.size.x*.5f&&Mathf.Abs(position.y-r.center.y)<r.size.y*.5f);
  public static Color Ground(MapDefinition map)=>map.groundColor.a>0?map.groundColor:DefaultGround;
  public static PickupState CreatePickup(PickupDefinition definition)=>new PickupState{position=definition.position,
   weapon=(int)definition.weapon,item=(int)definition.item,active=true,respawnSeconds=definition.respawnSeconds,randomizeOnRespawn=definition.randomizeOnRespawn};
  public static void ScheduleRespawn(PickupState pickup,float now){pickup.active=false;pickup.respawnAt=pickup.respawnSeconds>0?now+pickup.respawnSeconds:0;}
  public static bool TryRespawn(PickupState pickup,float now,Func<ItemKind> randomItem){
   if(pickup.active||pickup.respawnSeconds<=0||now<pickup.respawnAt)return false;
   pickup.active=true;
   if(pickup.randomizeOnRespawn){
    if(UnityEngine.Random.value<.55f){pickup.weapon=UnityEngine.Random.Range(1,5);pickup.item=0;}
    else{pickup.weapon=0;pickup.item=(int)(UnityEngine.Random.value<.4f?ItemKind.Mystery:randomItem());}
   }
   return true;
  }
 }
}
