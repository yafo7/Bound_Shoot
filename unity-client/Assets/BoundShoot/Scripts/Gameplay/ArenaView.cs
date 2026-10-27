using System.Collections.Generic;
using UnityEngine;
namespace BoundShoot {
 public static class Draw2D {
  static Sprite square,circle;static Material line;
  public static Sprite Square {get {if(square==null){square=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),Vector2.one*.5f,1);}return square;}}
  public static Sprite Circle {get {
   if(circle==null){
    var t=new Texture2D(64,64,TextureFormat.RGBA32,false);t.filterMode=FilterMode.Bilinear;
    for(int x=0;x<64;x++)for(int y=0;y<64;y++)t.SetPixel(x,y,new Color(1,1,1,Vector2.Distance(new Vector2(x,y),new Vector2(31.5f,31.5f))<31?1:0));
    t.Apply();circle=Sprite.Create(t,new Rect(0,0,64,64),Vector2.one*.5f,64);
   }return circle;}}
  public static SpriteRenderer Shape(Transform parent,string name,Vector2 position,Vector2 size,Color color,bool round=false,int order=0){
   var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=new Vector3(size.x,size.y,1);
   var r=go.AddComponent<SpriteRenderer>();r.sprite=round?Circle:Square;r.color=color;r.sortingOrder=order;return r;
  }
  public static LineRenderer Line(Transform parent,Color color,float width,int order=2) {
   if(line==null)line=new Material(Shader.Find("Sprites/Default"));
   var go=new GameObject("Rope");go.transform.SetParent(parent,false);var r=go.AddComponent<LineRenderer>();
   r.sharedMaterial=line;r.startColor=r.endColor=color;r.startWidth=r.endWidth=width;r.sortingOrder=order;r.positionCount=0;r.useWorldSpace=true;return r;
  }
  public static readonly Color Blue=new Color(.24f,.65f,1),Red=new Color(1,.36f,.38f),Gold=new Color(1,.79f,.3f);
 }
 public class ArenaView:MonoBehaviour {
  public MapDefinition Map;
  public void Build(MapDefinition map) {
   Map=map;
   if(Application.isBatchMode){
    foreach(var b in map.blocks)Wall(b.center,b.size);
    Wall(new Vector2(0,map.halfHeight+.5f),new Vector2(map.halfWidth*2+2,1));Wall(new Vector2(0,-map.halfHeight-.5f),new Vector2(map.halfWidth*2+2,1));
    Wall(new Vector2(-map.halfWidth-.5f,0),new Vector2(1,map.halfHeight*2));Wall(new Vector2(map.halfWidth+.5f,0),new Vector2(1,map.halfHeight*2));return;
   }
   Draw2D.Shape(transform,"Floor",Vector2.zero,new Vector2(map.halfWidth*2,map.halfHeight*2),new Color(.13f,.19f,.23f),false,-10);
   Draw2D.Shape(transform,"CombatRing",Vector2.zero,Vector2.one*18,new Color(.17f,.24f,.27f),true,-9);
   for(int x=-(int)map.halfWidth;x<=map.halfWidth;x+=2)Draw2D.Shape(transform,"Grid",new Vector2(x,0),new Vector2(.025f,map.halfHeight*2),new Color(.19f,.26f,.29f),false,-8);
   for(int y=-(int)map.halfHeight;y<=map.halfHeight;y+=2)Draw2D.Shape(transform,"Grid",new Vector2(0,y),new Vector2(map.halfWidth*2,.025f),new Color(.19f,.26f,.29f),false,-8);
   foreach(var p in map.revivals){
    Draw2D.Shape(transform,"Revival zone",p,new Vector2(4,3),new Color(.14f,.5f,.39f),false,-7);
    Draw2D.Shape(transform,"Cross",p,new Vector2(1.3f,.3f),new Color(.5f,1,.76f),false,-6);
    Draw2D.Shape(transform,"Cross",p,new Vector2(.3f,1.3f),new Color(.5f,1,.76f),false,-6);
   }
   foreach(var p in map.supplies)Draw2D.Shape(transform,"Supply pad",p,Vector2.one*1.25f,new Color(.36f,.32f,.2f),true,-6);
   Draw2D.Shape(transform,"Blue spawn",new Vector2(-13,0),new Vector2(3.5f,15),new Color(.13f,.25f,.37f),false,-7);
   Draw2D.Shape(transform,"Red spawn",new Vector2(13,0),new Vector2(3.5f,15),new Color(.36f,.18f,.23f),false,-7);
   foreach(var b in map.blocks)Wall(b.center,b.size);
   Wall(new Vector2(0,map.halfHeight+.5f),new Vector2(map.halfWidth*2+2,1));
   Wall(new Vector2(0,-map.halfHeight-.5f),new Vector2(map.halfWidth*2+2,1));
   Wall(new Vector2(-map.halfWidth-.5f,0),new Vector2(1,map.halfHeight*2));
   Wall(new Vector2(map.halfWidth+.5f,0),new Vector2(1,map.halfHeight*2));
  }
  void Wall(Vector2 p,Vector2 size){
   if(Application.isBatchMode){
    var wall=new GameObject("Wall");wall.transform.SetParent(transform,false);wall.transform.localPosition=p;wall.layer=8;wall.AddComponent<BoxCollider2D>().size=size;return;
   }
   Draw2D.Shape(transform,"Wall shadow",p+new Vector2(.13f,-.15f),size,new Color(.07f,.1f,.13f),false,-2);
   var r=Draw2D.Shape(transform,"Wall",p,size,new Color(.45f,.52f,.55f),false,4);
   r.gameObject.layer=8;r.gameObject.AddComponent<BoxCollider2D>();
   Draw2D.Shape(transform,"Wall top",p+new Vector2(0,.12f),size-new Vector2(.18f,.18f),new Color(.55f,.62f,.63f),false,5);
  }
 }
}
