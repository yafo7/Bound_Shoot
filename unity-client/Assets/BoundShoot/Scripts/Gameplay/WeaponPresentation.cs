using System;
using UnityEngine;
namespace BoundShoot {
 [Serializable] public sealed class WeaponVisualProfile {
  public int weapon;public Vector2 size,grip,muzzle,flashSize;public float recoil,trailLength;
  public Vector2 GripPosition(bool flipped){float barrelHeight=(muzzle.y-grip.y)*size.y;return WeaponPresentation.Hand+new Vector2(0,flipped?barrelHeight:-barrelHeight);}
  public Vector2 Centre(float kick,bool flipped){
   var offset=Vector2.Scale(Vector2.one*.5f-grip,size);if(flipped)offset.y=-offset.y;
   return GripPosition(flipped)+offset-Vector2.right*kick;
  }
  public Vector2 MuzzleLocal(bool flipped){
   var offset=Vector2.Scale(muzzle-grip,size);if(flipped)offset.y=-offset.y;
   return GripPosition(flipped)+offset;
  }
 }
 [Serializable] sealed class WeaponVisualCatalog {public WeaponVisualProfile[] weapons;}
 public static class WeaponPresentation {
  public static readonly Vector2 Pivot=Vector2.zero,Hand=new Vector2(.5f,0);
  static WeaponVisualCatalog catalog;
  public static WeaponVisualProfile Get(int weapon){
   if(catalog==null)catalog=JsonUtility.FromJson<WeaponVisualCatalog>(Resources.Load<TextAsset>("WeaponPresentation").text);
   return Array.Find(catalog.weapons,w=>w.weapon==weapon);
  }
  public static Vector2 Rotate(Vector2 value,float angle){float a=angle*Mathf.Deg2Rad;return new Vector2(value.x*Mathf.Cos(a)-value.y*Mathf.Sin(a),value.x*Mathf.Sin(a)+value.y*Mathf.Cos(a));}
  public static Vector2 Muzzle(int weapon,Vector2 actorPosition,float angle){var profile=Get(weapon);return actorPosition+Pivot+(profile==null?Vector2.zero:Rotate(profile.MuzzleLocal(Mathf.Cos(angle*Mathf.Deg2Rad)<0),angle));}
 }
}
