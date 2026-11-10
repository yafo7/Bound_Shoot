using System.Collections.Generic;
using UnityEngine;
namespace BoundShoot {
 public static class CyberArt {
  static readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
  static Sprite launcher,rocket,greatsword;
  static Sprite GreatswordSprite(){
   if(greatsword!=null)return greatsword;
   var texture=new Texture2D(40,16,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};
   var edge=new Color32(32,22,35,255);
   for(int y=0;y<16;y++)for(int x=0;x<40;x++){
    int height=x<34?4:Mathf.Max(0,(39-x)/2);
    Color color=Color.clear;
    if(x>=10&&x<39&&Mathf.Abs(y-7)<=height)color=edge;
    if(x>=11&&x<37&&Mathf.Abs(y-7)<height)color=y>=7?new Color32(227,242,249,255):new Color32(107,146,177,255);
    if(x>=2&&x<=10&&y>=5&&y<=9)color=edge;
    if(x>=3&&x<=9&&y>=6&&y<=8)color=new Color32(142,78,46,255);
    if(x>=9&&x<=12&&y>=1&&y<=13)color=edge;
    if(x>=10&&x<=11&&y>=2&&y<=12)color=new Color32(248,190,74,255);
    texture.SetPixel(x,y,color);
   }
   texture.Apply(false,true);
   return greatsword=Sprite.Create(texture,new Rect(0,0,40,16),Vector2.one*.5f,40);
  }
  static Sprite RocketSprite(bool projectile){
   var cached=projectile?rocket:launcher;
   if(cached!=null)return cached;
   var texture=new Texture2D(40,16,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};
   var outline=new Color(.16f,.19f,.15f);
   for(int y=0;y<16;y++)for(int x=0;x<40;x++){
    Color color=Color.clear;
    if(projectile){
     if(x>=6&&x<34&&Mathf.Abs(y-7)<=(x<26?3:(34-x)/2))color=x<12?new Color(1,.54f,.12f):new Color(.78f,.8f,.69f);
     if(x>=12&&x<=16&&(y==3||y==11))color=outline;
    }else{
     if((x>=2&&x<=38&&y>=6&&y<=13)||(x>=12&&x<=17&&y>=1&&y<=6))color=outline;
     if(x>=3&&x<=37&&y>=7&&y<=12)color=x<=7||x>=33?new Color(.35f,.39f,.31f):y>=10?new Color(.66f,.73f,.42f):new Color(.39f,.48f,.23f);
     if(x>=27&&x<=29&&y>=7&&y<=12)color=new Color(.93f,.66f,.18f);
    }
    texture.SetPixel(x,y,color);
   }
   texture.Apply();
   cached=Sprite.Create(texture,new Rect(0,0,40,16),Vector2.one*.5f,40);
   if(projectile)rocket=cached;else launcher=cached;
   return cached;
  }
  public static Sprite Get(string name){
   if(name=="weapon6")return GreatswordSprite();
   if(name=="weapon5"||name=="rocket-projectile")return RocketSprite(name=="rocket-projectile");
   if(name=="item9"||name=="item10")return ThrowableVisuals.Icon(name=="item9");
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
