using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace BoundShoot {
 // Local presentation only; all landings, lifetimes and damage come from the authority.
 public sealed class ThrowableVisuals:MonoBehaviour {
  sealed class View {public GameObject root;public SpriteRenderer projectile,fill;public LineRenderer edge;public SpriteRenderer[] wisps;}
  readonly Dictionary<ulong,View> views=new Dictionary<ulong,View>();
  readonly Dictionary<ulong,LineRenderer> allies=new Dictionary<ulong,LineRenderer>();
  LineRenderer preview,arc;static Sprite smokeIcon,fireIcon,flame;
  public bool PreviewVisible=>preview!=null&&preview.positionCount>0;
  public Vector2 PreviewLanding {get;private set;}
  static Sprite Flame {
   get {
    if(flame!=null)return flame;
    var t=new Texture2D(24,40,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,name="Ground flame"};
    for(int y=0;y<40;y++)for(int x=0;x<24;x++){
     float h=y/39f,centre=11.5f+Mathf.Sin(h*7)*h*4,width=Mathf.Sin(Mathf.Pow(h,.6f)*Mathf.PI)*10;
     float d=Mathf.Abs(x-centre);Color c=Color.clear;
     if(d<=width){c=new Color(1,.3f,.035f);if(d<width*.72f&&h<.76f)c=new Color(1,.66f,.07f);if(d<width*.36f&&h<.48f)c=new Color(1,.94f,.43f);}
     t.SetPixel(x,y,c);
    }t.Apply();flame=Sprite.Create(t,new Rect(0,0,24,40),Vector2.one*.5f,40);return flame;
   }
  }
  public static Sprite Icon(bool smoke){
   if(smoke?smokeIcon!=null:fireIcon!=null)return smoke?smokeIcon:fireIcon;
   var texture=new Texture2D(32,40,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,name=smoke?"Smoke grenade icon":"Incendiary bottle icon"};
   for(int y=0;y<40;y++)for(int x=0;x<32;x++){
    Color colour=Color.clear;bool body=smoke?x>=7&&x<=24&&y>=4&&y<=28:x>=9&&x<=22&&y>=3&&y<=23;
    if(body){bool edge=x==(smoke?7:9)||x==(smoke?24:22)||y<=5||y==(smoke?28:23);colour=edge?new Color(.16f,.21f,.23f):smoke?new Color(.68f,.8f,.8f):new Color(.78f,.32f,.12f);if(x>=11&&x<=13)colour=smoke?new Color(.9f,.96f,.95f):new Color(1,.66f,.22f);if(y>=12&&y<=18)colour=smoke?new Color(.35f,.44f,.46f):new Color(1,.8f,.4f);}
    if(smoke&&x>=11&&x<=20&&y>=29&&y<=33)colour=new Color(.25f,.3f,.32f);
    if(!smoke&&x>=13&&x<=18&&y>=24&&y<=31)colour=new Color(.4f,.3f,.18f);
    if(!smoke&&y>=30&&y<=37&&Mathf.Abs(x-17)<(38-y)*.5f)colour=new Color(1,.76f,.15f);
    texture.SetPixel(x,y,colour);
   }texture.Apply();var sprite=Sprite.Create(texture,new Rect(0,0,32,40),Vector2.one*.5f,40);sprite.name=smoke?"item9":"item10";if(smoke)smokeIcon=sprite;else fireIcon=sprite;return sprite;
  }
  static void Ring(LineRenderer line,Vector2 centre,float radius){line.positionCount=65;for(int i=0;i<=64;i++){float a=i*Mathf.PI/32;line.SetPosition(i,centre+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius);}}
  View Create(ThrowableState state){
   bool smoke=state.kind==(int)ItemKind.Smoke;var v=new View{root=new GameObject(state.rocket?"Rocket":smoke?"Smoke area":"Fire area")};v.root.transform.SetParent(transform,false);
   v.fill=Draw2D.Shape(v.root.transform,"Area footprint",Vector2.zero,Vector2.one,Color.white,true,4);
   v.projectile=CyberArt.Place(v.root.transform,state.rocket?"rocket-projectile":smoke?"item9":"item10",Vector2.zero,Vector2.one*.65f,20);
   if(state.rocket){v.fill.enabled=false;v.wisps=System.Array.Empty<SpriteRenderer>();return v;}
   v.edge=Draw2D.Line(v.root.transform,smoke?new Color(.8f,.9f,.94f,.6f):new Color(1,.57f,.13f,.85f),.045f,5);
   v.wisps=new SpriteRenderer[16];for(int i=0;i<v.wisps.Length;i++){v.wisps[i]=Draw2D.Shape(v.root.transform,smoke?"Smoke wisp":"Flame",Vector2.zero,Vector2.one,Color.white,true,5);if(!smoke)v.wisps[i].sprite=Flame;}
   return v;
  }
  public void Render(BattleWorld world){
   double now=world.AreaTime;var active=world.Phase.Value==2?world.Throwables.Where(s=>now<s.endAt).ToArray():System.Array.Empty<ThrowableState>();
   foreach(var id in views.Keys.Where(id=>!active.Any(s=>s.id==id)).ToArray()){Destroy(views[id].root);views.Remove(id);}
   foreach(var a in active){
    if(!views.TryGetValue(a.id,out var v)){v=Create(a);views.Add(a.id,v);}bool airborne=now<a.landAt,smoke=a.kind==(int)ItemKind.Smoke;
    v.projectile.enabled=airborne;v.fill.enabled=!airborne;if(v.edge!=null)v.edge.enabled=!airborne;
    if(airborne){float t=Mathf.Clamp01((float)((now-a.launchedAt)/(a.landAt-a.launchedAt)));v.projectile.transform.position=Vector2.Lerp(a.start,a.landing,t)+Vector2.up*(4*t*(1-t)*2.3f);Vector2 tangent=a.landing-a.start+Vector2.up*(9.2f*(1-2*t));v.projectile.transform.rotation=Quaternion.Euler(0,0,a.rocket?Mathf.Atan2(tangent.y,tangent.x)*Mathf.Rad2Deg:t*320);}
    if(a.rocket)continue;
    float fade=Mathf.Clamp01((float)(a.endAt-now)/.6f);v.fill.transform.position=a.landing;v.fill.transform.localScale=Vector3.one*a.radius*2;
    v.fill.color=smoke?new Color(.57f,.65f,.68f,.85f*fade):new Color(.95f,.23f,.035f,.45f*fade);if(!airborne)Ring(v.edge,a.landing,a.radius);
    for(int i=0;i<v.wisps.Length;i++){
     var w=v.wisps[i];w.enabled=!airborne;if(airborne)continue;
     float angle=i*2.4f+(float)now*(smoke?.09f:.15f),distance=a.radius*Mathf.Sqrt((i+.5f)/v.wisps.Length)*.7f;
     Vector2 p=a.landing+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*distance;w.transform.position=p;
     float pulse=.85f+.15f*Mathf.Sin((float)now*(smoke?1.6f:7)+i*1.7f);
     w.transform.localScale=smoke?Vector3.one*a.radius*.65f*pulse:new Vector3(.95f,1.2f*pulse,1);
     w.color=smoke?new Color(.72f,.78f,.8f,.35f*fade):new Color(1,1,1,fade);
    }
   }
   var viewer=world.LocalViewer;
   foreach(var pair in allies)pair.Value.positionCount=0;
   if(viewer!=null)foreach(var p in NetPlayer.All.Where(p=>p.NetworkManager==world.NetworkManager&&p.Team.Value==viewer.Team.Value)){
    if(!world.SmokeBlocks(p.transform.position,p.transform.position))continue;
    if(!allies.TryGetValue(p.NetworkObjectId,out var ring)){ring=Draw2D.Line(transform,p==viewer?Draw2D.Gold:Draw2D.Blue,.055f,16);ring.name="Ally smoke outline";allies[p.NetworkObjectId]=ring;}
    Ring(ring,(Vector2)p.transform.position+Vector2.up*.15f,.8f);
   }
   if(preview==null){preview=Draw2D.Line(transform,Color.white,.055f,18);preview.name="Throw landing preview";arc=Draw2D.Line(transform,new Color(1,1,1,.5f),.035f,18);arc.name="Throw arc preview";}
   var kind=viewer==null?ItemKind.None:(ItemKind)viewer.ItemAt(viewer.SelectedSlot.Value);
   bool rocket=viewer!=null&&viewer.SelectedSlot.Value==0&&viewer.Weapon.Value==(int)WeaponKind.RocketLauncher;
   if(viewer==null||!viewer.Alive||world.Phase.Value!=2||(!rocket&&!ThrowableRules.IsThrowable(kind))||(GameClient.Instance!=null&&GameClient.Instance.InputBlocked)){preview.positionCount=arc.positionCount=0;return;}
   Vector2 aim=viewer.Aim;if(Camera.main!=null)aim=(Vector2)Camera.main.ScreenToWorldPoint(Input.mousePosition)-(Vector2)viewer.transform.position;
   PreviewLanding=rocket?world.RocketLanding(viewer.transform.position,aim,aim.magnitude):world.ThrowLanding(viewer.transform.position,aim);float radius=rocket?GameConfig.Current.rocketBlastRadius:kind==ItemKind.Smoke?GameConfig.Current.smokeRadius:GameConfig.Current.fireRadius;
   preview.startColor=preview.endColor=kind==ItemKind.Smoke?new Color(.85f,.95f,1,.85f):new Color(1,.65f,.15f,.85f);Ring(preview,PreviewLanding,radius);
   arc.positionCount=25;for(int i=0;i<25;i++){float t=i/24f;arc.SetPosition(i,Vector2.Lerp(viewer.transform.position,PreviewLanding,t)+Vector2.up*(4*t*(1-t)*2.3f));}
  }
  public void Clear(){foreach(var v in views.Values)if(v.root!=null)Destroy(v.root);views.Clear();foreach(var ring in allies.Values)if(ring!=null)Destroy(ring.gameObject);allies.Clear();if(preview!=null)preview.positionCount=0;if(arc!=null)arc.positionCount=0;}
 }
}
