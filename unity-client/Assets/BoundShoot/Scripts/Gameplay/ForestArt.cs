using System.Collections.Generic;
using UnityEngine;
namespace BoundShoot {
 public static class ForestArt {
  static readonly Dictionary<string,Sprite> cache=new Dictionary<string,Sprite>();
  public static Sprite Get(string key){
   if(cache.TryGetValue(key,out var sprite))return sprite;
   string sheet="tileset_village_abandoned";Rect rect;
   switch(key){
    case "grass":sheet="tileset_floor";rect=new Rect(176,192,16,16);break;
    case "tuft":sheet="tileset_floor";rect=new Rect(192,192,16,16);break;
    case "dirt":sheet="tileset_floor";rect=new Rect(16,128,16,16);break;
    case "cliff":sheet="tileset_floor";rect=new Rect(16,128,16,16);break;
    case "tree":rect=new Rect(64,96,32,32);break;
    case "treeSmall":rect=new Rect(64,96,32,32);break;
    case "rock":rect=new Rect(128,48,32,32);break;
    case "stump":rect=new Rect(96,128,16,16);break;
    case "log":rect=new Rect(144,112,32,32);break;
    case "heart":sheet="heart";rect=new Rect(64,0,16,16);break;
    case "heartEmpty":sheet="heart";rect=new Rect(0,0,16,16);break;
    default:return null;
   }
   var texture=Resources.Load<Texture2D>("Forest/"+sheet);if(texture==null)return null;
   sprite=Sprite.Create(texture,new Rect(rect.x,texture.height-rect.y-rect.height,rect.width,rect.height),Vector2.one*.5f,16,0,SpriteMeshType.FullRect);sprite.name="forest_"+key;cache[key]=sprite;return sprite;
  }
  public static SpriteRenderer Place(Transform parent,string key,Vector2 position,Vector2 size,int order,Color? tint=null){var go=new GameObject("Forest "+key);go.transform.SetParent(parent,false);go.transform.localPosition=position;var r=go.AddComponent<SpriteRenderer>();r.sprite=Get(key);r.sortingOrder=order;r.color=tint??Color.white;if(r.sprite!=null){var b=r.sprite.bounds.size;go.transform.localScale=new Vector3(size.x/b.x,size.y/b.y,1);}return r;}
 }
 public sealed class ForestView:MonoBehaviour {
  public void Build(MapDefinition map){
   Draw2D.Shape(transform,"Forest ground",Vector2.zero,new Vector2(map.halfWidth*2+56,map.halfHeight*2+48),MapContent.Ground(map),false,-30);
   // The full sprite sheet contains opaque terrain: do not stamp its backgrounds as tufts.
   foreach(int side in new[]{-1,1})foreach(float y in new[]{-10f,0,10f}){
    Draw2D.Shape(transform,"Forest approach",new Vector2(side*24,y),new Vector2(13,3.5f),new Color(.62f,.64f,.36f),false,-27);
    for(int i=0;i<8;i++)Draw2D.Shape(transform,"Path uneven edge",new Vector2(side*(18.5f+i*1.5f),y+(i%2==0?1.75f:-1.75f)),new Vector2(1.2f,.35f),new Color(.62f,.64f,.36f),false,-27);
   }
   var h=map.plateau;if(h!=null){
    Draw2D.Shape(transform,"Plateau contact shadow",h.center+Vector2.down*.55f,h.size+Vector2.one*1.4f,new Color(.18f,.28f,.1f,.55f),false,-26);
    Draw2D.Shape(transform,"Upper lawn",h.center,h.size,new Color(.52f,.65f,.31f),false,-25);
    foreach(var size in new[]{new Vector2(16,11),new Vector2(13,15),new Vector2(9,17)})Draw2D.Shape(transform,"Upper clearing",h.center,size,new Color(.66f,.67f,.40f),false,-24);
    foreach(int side in new[]{-1,1})foreach(float y in h.stairs){
     Vector2 p=new Vector2(h.center.x+side*h.size.x*.5f,y);Draw2D.Shape(transform,"Stair shadow",p,new Vector2(4.25f,h.stairWidth),new Color(.31f,.30f,.18f),false,-23);
     for(int step=0;step<6;step++){
      var q=p+Vector2.right*((step-2.5f)*.625f);Draw2D.Shape(transform,"Stone stair",q,new Vector2(.53f,h.stairWidth-.4f),new Color(.72f,.62f,.42f),false,-21);
      Draw2D.Shape(transform,"Stair highlight",q+Vector2.right*.19f,new Vector2(.10f,h.stairWidth-.4f),new Color(.85f,.76f,.51f),false,-20);
     }
    }
   }
   foreach(var floor in map.floors??new FloorDefinition[0])Draw2D.Shape(transform,"Designer forest floor",floor.center,floor.size,floor.color,false,-23);
   foreach(var block in map.blocks){
    var collision=new GameObject("Forest obstacle");collision.layer=8;collision.transform.SetParent(transform,false);collision.transform.localPosition=block.center;collision.AddComponent<BoxCollider2D>().size=block.size;
    if(block.visual==5){
     bool horizontal=block.size.x>block.size.y;float length=horizontal?block.size.x:block.size.y;
     Draw2D.Shape(transform,"Cliff dark edge",block.center+Vector2.down*.15f,block.size+Vector2.one*.25f,new Color(.24f,.30f,.14f),false,1);
     for(float t=-length*.5f;t<length*.5f;t+=1){var p=block.center+(horizontal?Vector2.right:Vector2.up)*(t+.5f);ForestArt.Place(transform,"cliff",p,Vector2.one,2,new Color(.90f,.85f,.72f));}
     Vector2 lip=horizontal?Vector2.up*.43f:Vector2.right*(block.center.x<0?.43f:-.43f);
     Draw2D.Shape(transform,"Grass cliff lip",block.center+lip,horizontal?new Vector2(block.size.x,.22f):new Vector2(.22f,block.size.y),new Color(.38f,.51f,.21f),false,3);
    }else{
     Draw2D.Shape(transform,"Contact shadow",block.center+Vector2.down*.25f,block.size*1.15f,new Color(.16f,.25f,.09f,.35f),true,2);
     if(block.visual==4){ForestArt.Place(transform,"stump",block.center,Vector2.one*1.4f,4);var canopy=ForestArt.Place(transform,"tree",block.center+Vector2.up*1.05f,new Vector2(3,3),18,new Color(.85f,.94f,.82f));canopy.gameObject.AddComponent<TreeCanopyView>();}
     else ForestArt.Place(transform,block.visual<=1?"rock":block.visual==3?"log":"stump",block.center+Vector2.up*.25f,block.size*1.2f,4,block.visual==1?new Color(.85f,.70f,.55f):Color.white);
    }
   }
   foreach(int side in new[]{-1,1}){
    Draw2D.Shape(transform,"Spawn clearing",new Vector2(side*(map.halfWidth-7),0),new Vector2(7,14),new Color(.65f,.70f,.39f,.25f),true,-24);
    for(float y=-map.halfHeight-13;y<map.halfHeight+14;y+=2.3f)ForestArt.Place(transform,"tree",new Vector2(side*(map.halfWidth+2+(Mathf.Sin(y)*.5f)),y),new Vector2(3.5f,3.5f),18,new Color(.77f,.9f,.77f));
    for(float x=-map.halfWidth-18;x<map.halfWidth+19;x+=2.5f)ForestArt.Place(transform,"tree",new Vector2(x,side*(map.halfHeight+2)),new Vector2(3.5f,3.5f),18,new Color(.77f,.9f,.77f));
    Boundary(new Vector2(side*(map.halfWidth+.5f),0),new Vector2(1,map.halfHeight*2+2));Boundary(new Vector2(0,side*(map.halfHeight+.5f)),new Vector2(map.halfWidth*2+2,1));
   }
   for(float x=-map.halfWidth-14;x<map.halfWidth+15;x+=2.75f)for(float y=-map.halfHeight-12;y<map.halfHeight+13;y+=3.25f){
    int n=Mathf.Abs(Mathf.RoundToInt(x*37+y*13));if(n%3!=0)continue;var p=new Vector2(x+(n%5)*.2f,y);if(h!=null&&ForestMap.SurfaceAt(map,p)==2)continue;
    var color=new Color(.28f,.43f,.19f,.45f);for(int blade=0;blade<3;blade++)Draw2D.Shape(transform,"Quiet grass",p+new Vector2((blade-1)*.13f,blade==1?.06f:0),new Vector2(.065f,blade==1?.24f:.15f),color,false,-22);
   }
  }
  void Boundary(Vector2 p,Vector2 size){var go=new GameObject("Forest boundary");go.layer=8;go.transform.SetParent(transform,false);go.transform.localPosition=p;go.AddComponent<BoxCollider2D>().size=size;}
 }
}
