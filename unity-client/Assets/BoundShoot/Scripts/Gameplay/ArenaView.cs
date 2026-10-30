using System.Collections.Generic;
using UnityEngine;
namespace BoundShoot {
 public static class Draw2D {
  public static bool RenderInBatch;
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
  public static readonly Color Blue=new Color(0,.88f,1),Red=new Color(1,.08f,.65f),Gold=new Color(1,.79f,.3f);
 }
 public class ArenaView:MonoBehaviour {
  public MapDefinition Map;
  public void Build(MapDefinition map) {
   Map=map;
   if(Application.isBatchMode&&!Draw2D.RenderInBatch){
    foreach(var b in map.blocks)Wall(b.center,b.size);
    Wall(new Vector2(0,map.halfHeight+.5f),new Vector2(map.halfWidth*2+2,1));Wall(new Vector2(0,-map.halfHeight-.5f),new Vector2(map.halfWidth*2+2,1));
    Wall(new Vector2(-map.halfWidth-.5f,0),new Vector2(1,map.halfHeight*2));Wall(new Vector2(map.halfWidth+.5f,0),new Vector2(1,map.halfHeight*2));return;
   }
   if(map.theme=="meadow"){BuildMeadow(map);return;}
   Draw2D.Shape(transform,"Floor base",Vector2.zero,new Vector2(map.halfWidth*2,map.halfHeight*2),new Color(.10f,.15f,.23f),false,-11);
   for(float x=-map.halfWidth;x<map.halfWidth;x+=6)for(float y=-map.halfHeight;y<map.halfHeight;y+=6){
    var size=new Vector2(Mathf.Min(6,map.halfWidth-x),Mathf.Min(6,map.halfHeight-y));
    CyberArt.Place(transform,"floor",new Vector2(x,y)+size*.5f,size,-10,new Color(.58f,.65f,.76f));
   }
   if(map.layoutVersion>=1&&map.floors!=null)foreach(var floor in map.floors)Draw2D.Shape(transform,"Floor patch",floor.center,floor.size,floor.color,false,-9);
   foreach(var zone in MapContent.Revivals(map)){
    var p=zone.center;Draw2D.Shape(transform,"Revival zone",p,zone.size,new Color(0,.75f,.8f,.12f),false,-7);
    CyberArt.Place(transform,"revival",p,Vector2.one*1.8f,-6);
   }
   foreach(var b in map.blocks)Wall(b.center,b.size);
   Wall(new Vector2(0,map.halfHeight+.5f),new Vector2(map.halfWidth*2+2,1));
   Wall(new Vector2(0,-map.halfHeight-.5f),new Vector2(map.halfWidth*2+2,1));
   Wall(new Vector2(-map.halfWidth-.5f,0),new Vector2(1,map.halfHeight*2));
   Wall(new Vector2(map.halfWidth+.5f,0),new Vector2(1,map.halfHeight*2));
  }
  void Wall(Vector2 p,Vector2 size){
   if(Application.isBatchMode&&!Draw2D.RenderInBatch){
    var wall=new GameObject("Wall");wall.transform.SetParent(transform,false);wall.transform.localPosition=p;wall.layer=8;wall.AddComponent<BoxCollider2D>().size=size;return;
   }
   Draw2D.Shape(transform,"Wall shadow",p+new Vector2(.13f,-.15f),size,new Color(.07f,.1f,.13f),false,-2);
   var wallObject=new GameObject("Wall collision");wallObject.transform.SetParent(transform,false);wallObject.transform.localPosition=p;wallObject.layer=8;wallObject.AddComponent<BoxCollider2D>().size=size;
   if(Mathf.Max(size.x,size.y)>20){
    Draw2D.Shape(transform,"Railing",p,size,new Color(.055f,.085f,.13f),false,4);
    bool horizontal=size.x>size.y;float length=horizontal?size.x:size.y;
    for(float t=-length*.5f+1;t<length*.5f;t+=3)Draw2D.Shape(transform,"Rail light",p+(horizontal?new Vector2(t,0):new Vector2(0,t)),horizontal?new Vector2(.65f,.09f):new Vector2(.09f,.65f),Draw2D.Blue,false,5);
   }else CyberArt.Place(transform,size.x>size.y*1.5f?"barrier":"cover",p,size,4);
  }
  void BuildMeadow(MapDefinition map){
   Draw2D.Shape(transform,"Meadow base",Vector2.zero,new Vector2(map.halfWidth*2,map.halfHeight*2),new Color(.43f,.56f,.3f),false,-15);
   for(float x=-map.halfWidth;x<map.halfWidth;x+=8)for(float y=-map.halfHeight;y<map.halfHeight;y+=8){
    var size=new Vector2(Mathf.Min(8,map.halfWidth-x),Mathf.Min(8,map.halfHeight-y));
    CyberArt.Place(transform,"floor",new Vector2(x,y)+size*.5f,size*1.025f,-14,new Color(.83f,.87f,.78f));
   }
   for(float x=-map.halfWidth+1;x<map.halfWidth;x+=1.5f){
    float bend=Mathf.Sin(x*.13f)*1.5f;
    CyberArt.Place(transform,"dirt",new Vector2(x,bend),new Vector2(2.3f,4.8f),-12,new Color(.9f,.89f,.8f));
    foreach(int side in new[]{-1,1})CyberArt.Place(transform,"dirt",new Vector2(x,side*16+bend*.5f),new Vector2(2.3f,3.4f),-12,new Color(.9f,.89f,.8f));
   }
   for(float x=-map.halfWidth+6;x<map.halfWidth;x+=12)for(float y=-map.halfHeight+3;y<map.halfHeight;y+=8){
    if(Mathf.Abs(y)<4||Mathf.Abs(Mathf.Abs(y)-16)<3)continue;
    CyberArt.Place(transform,"grass",new Vector2(x,y),new Vector2(.7f,.5f),-10,new Color(.85f,.9f,.72f));
   }
   foreach(var floor in map.floors??new FloorDefinition[0])Draw2D.Shape(transform,"Designer floor patch",floor.center,floor.size,floor.color,false,-11);
   foreach(var zone in MapContent.Revivals(map)){Draw2D.Shape(transform,"Revival area",zone.center,zone.size,new Color(.6f,.9f,.55f,.25f),false,-8);CyberArt.Place(transform,"revival",zone.center,Vector2.one*1.9f,-7);}
   foreach(var block in map.blocks){
    var go=new GameObject("Obstacle");go.transform.SetParent(transform,false);go.transform.localPosition=block.center;go.layer=8;go.AddComponent<BoxCollider2D>().size=block.size;
    Draw2D.Shape(transform,"Ground shadow",block.center+new Vector2(.1f,-.12f),block.size,new Color(.1f,.14f,.09f,.22f),true,-4);
    string key=new[]{"stone","mound","crate","fence","trunk"}[Mathf.Clamp(block.visual,0,4)];
    if(block.visual==0&&block.size.y>block.size.x*1.4f){
     CyberArt.Place(transform,key,block.center+Vector2.up*block.size.y*.23f,new Vector2(block.size.x,block.size.y*.54f),4);
     CyberArt.Place(transform,key,block.center-Vector2.up*block.size.y*.23f,new Vector2(block.size.x,block.size.y*.54f),5);
    }else CyberArt.Place(transform,key,block.center,block.size,4);
    if(block.visual==4){var canopy=CyberArt.Place(transform,"canopy",block.center+Vector2.up*1.1f,block.size*2.1f,20);canopy.gameObject.AddComponent<TreeCanopyView>();}
   }
   for(int side=-1;side<=1;side+=2){
    Boundary(new Vector2(0,side*(map.halfHeight+.5f)),new Vector2(map.halfWidth*2+2,1));
    Boundary(new Vector2(side*(map.halfWidth+.5f),0),new Vector2(1,map.halfHeight*2));
   }
  }
  void Boundary(Vector2 p,Vector2 size){var go=new GameObject("Field boundary");go.transform.SetParent(transform,false);go.transform.localPosition=p;go.layer=8;go.AddComponent<BoxCollider2D>().size=size;Draw2D.Shape(transform,"Hedge",p,size,new Color(.18f,.31f,.16f),false,4);}
 }
}
