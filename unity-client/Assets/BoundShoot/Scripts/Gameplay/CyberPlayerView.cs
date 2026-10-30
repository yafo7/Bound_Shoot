using UnityEngine;
namespace BoundShoot {
 // Name retained for existing serialized preview scenes; rendering now uses vegetables.
 public sealed class CyberPlayerView:MonoBehaviour {
  SpriteRenderer body,gun,teamMarker,shadow,healthBar;LineRenderer shield;
  Transform aimPivot;LineRenderer slash;float phaseAt,hitUntil,recoil,slashUntil,shotAngle,shotPoseUntil;bool wasAlive=true;int lastHealth=-1;
  public int KickCount {get;private set;}public int LastShotWeapon {get;private set;}
  readonly VegetableWalkCycle walkCycle=new VegetableWalkCycle();
  public void Build(){
   body=CyberArt.Place(transform,"portrait0",new Vector2(0,.27f),Vector2.one*1.65f,8);
   aimPivot=new GameObject("Weapon pivot").transform;aimPivot.SetParent(transform,false);
   aimPivot.localPosition=WeaponPresentation.Pivot;
   gun=CyberArt.Place(aimPivot,"weapon3",new Vector2(.72f,0),new Vector2(1.65f,.65f),10);
   teamMarker=Draw2D.Shape(transform,"Team marker",new Vector2(0,-.5f),new Vector2(.75f,.09f),Draw2D.Blue,false,7);
   shadow=Draw2D.Shape(transform,"Foot shadow",new Vector2(0,-.42f),new Vector2(1,.3f),new Color(.08f,.13f,.07f,.3f),true,6);
   Draw2D.Shape(transform,"Health background",new Vector2(0,.95f),new Vector2(.85f,.10f),new Color(.12f,.15f,.1f),false,13);
   healthBar=Draw2D.Shape(transform,"Health",new Vector2(0,.95f),new Vector2(.8f,.055f),new Color(.65f,.9f,.3f),false,14);
   shield=Draw2D.Line(transform,new Color(.5f,.8f,1,.8f),.035f,12);
   slash=Draw2D.Line(transform,Color.white,.07f,14);
   walkCycle.Reset(transform.position);
  }
  public void Kick(int weapon,float angle){LastShotWeapon=weapon;KickCount++;recoil=WeaponPresentation.Get(weapon)?.recoil??.1f;shotAngle=angle;shotPoseUntil=Time.time+.08f;}
  public Vector2 Muzzle(int weapon,float angle)=>WeaponPresentation.Muzzle(weapon,transform.position,angle);
  public void Slash(float angle){
   slashUntil=Time.time+.14f;shotPoseUntil=slashUntil;shotAngle=angle;slash.positionCount=13;
   for(int i=0;i<13;i++){float a=angle-55+i*110/12f;slash.SetPosition(i,transform.position+(Vector3)WeaponPresentation.Rotate(Vector2.right*1.1f,a));}
  }
  public void Show(int team,int weapon,float angle,bool alive,bool hasShield){
   var player=GetComponent<NetPlayer>();ShowCharacter(player==null?0:player.Skin.Value,team,weapon,angle,alive,hasShield,player==null?-1:player.Health.Value);
  }
  public void ShowCharacter(int skin,int team,int weapon,float angle,bool alive,bool hasShield,int health=-1){
   var player=GetComponent<NetPlayer>();
   if(body==null)Build();skin=Mathf.Clamp(skin,0,7);
   if(Time.time<shotPoseUntil)angle=shotAngle;
   walkCycle.Advance(transform.position,Time.deltaTime);bool moving=walkCycle.Moving;
   int direction=Mathf.Sin(angle*Mathf.Deg2Rad)>.55f?2:Mathf.Sin(angle*Mathf.Deg2Rad)<-.55f?0:1;
   if(alive!=wasAlive){wasAlive=alive;phaseAt=Time.time;}
   if(lastHealth>=0&&health>=0&&health<lastHealth){hitUntil=Time.time+.1f;GardenFx.Emit(transform.position,9,3,.3f,Vector2.one*.85f);if(player!=null&&player.IsOwner&&!player.IsBot.Value)MeadowCamera.Instance?.Impulse(.13f);}
   lastHealth=health;
   healthBar.enabled=health>=0;healthBar.transform.localScale=new Vector3(.8f*(player==null?1:Mathf.Clamp01(health/(float)Mathf.Max(1,player.MaxHealth.Value))),.055f,1);
   int frame=moving?direction*6+walkCycle.Frame:18+direction*2+(Mathf.FloorToInt(Time.time*2)%2);
   float elapsed=Time.time-phaseAt;
   if(!alive)frame=26+Mathf.Min(3,Mathf.FloorToInt(elapsed*12));
   else if(phaseAt>0&&elapsed<.4f)frame=30+Mathf.Min(3,Mathf.FloorToInt(elapsed*10));
   else if(Time.time<hitUntil)frame=Time.time<hitUntil-.05f?24:25;
   CyberArt.Set(body,"veg"+skin+"_f"+frame.ToString("00"),Vector2.one*1.9f,true);
   // Standing frames share a foot pivot; special poses retain their canvas pivot.
   body.transform.localPosition=new Vector2(0,frame<24?-.42f:.27f);
   body.flipX=Mathf.Cos(angle*Mathf.Deg2Rad)<0;body.color=Time.time<hitUntil?new Color(1,.65f,.6f):Color.white;
   teamMarker.color=team==0?Draw2D.Blue:Draw2D.Red;
   gun.enabled=alive&&weapon>0;recoil=Mathf.MoveTowards(recoil,0,Time.deltaTime*3);
   if(weapon>0){var profile=WeaponPresentation.Get(weapon);CyberArt.Set(gun,"weapon"+weapon,profile.size);gun.transform.localPosition=profile.Centre(recoil,body.flipX);gun.flipY=body.flipX;gun.sortingOrder=direction==2?7:10;}
   aimPivot.localRotation=Quaternion.Euler(0,0,angle);
   if(Time.time>=slashUntil||!alive)slash.positionCount=0;else slash.startColor=slash.endColor=new Color(1,.98f,.8f,Mathf.Clamp01((slashUntil-Time.time)/.14f));
   shield.positionCount=hasShield&&alive?33:0;
   for(int i=0;i<shield.positionCount;i++){float a=i/32f*Mathf.PI*2;shield.SetPosition(i,transform.position+new Vector3(Mathf.Cos(a)*.72f,Mathf.Sin(a)*.82f,0));}
   if(moving&&alive&&Time.time>=nextStep){nextStep=Time.time+.24f;GardenFx.Emit(transform.position+Vector3.down*.4f,21,1,.25f,Vector2.one*.35f);}
  }
  float nextStep;
 }
}
