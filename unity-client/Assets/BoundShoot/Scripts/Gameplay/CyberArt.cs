using System.Collections.Generic;
using UnityEngine;
namespace BoundShoot {
 public static class CyberArt {
  static readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
  public static Sprite Get(string name){
   var vegetable=VegetableArt.Get(name);if(vegetable!=null)return vegetable;
   if(sprites.Count==0)foreach(var s in Resources.LoadAll<Sprite>("Art/Slices"))sprites[s.name]=s;
   return sprites.TryGetValue(name,out var result)?result:null;
  }
  public static SpriteRenderer Place(Transform parent,string name,Vector2 position,Vector2 size,int order,Color? tint=null){
   var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;
   var r=go.AddComponent<SpriteRenderer>();Set(r,name,size);r.sortingOrder=order;r.color=tint??Color.white;return r;
  }
  public static void Set(SpriteRenderer r,string name,Vector2 size,bool preserveAspect=false){
   r.sprite=Get(name);if(r.sprite==null)return;var b=r.sprite.bounds.size;
   float x=size.x/b.x,y=size.y/b.y;if(preserveAspect)x=y=Mathf.Min(x,y);
   r.transform.localScale=new Vector3(x,y,1);
  }
  public static string Pickup(int weapon,int item)=>weapon>0?"weapon"+weapon:"item"+item;
 }
}
