using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
namespace BoundShoot {
 [RequireComponent(typeof(NetworkObject),typeof(Rigidbody2D),typeof(CircleCollider2D))]
 public class NetPlayer:NetworkBehaviour {
  public static readonly System.Collections.Generic.List<NetPlayer> All=new System.Collections.Generic.List<NetPlayer>();
  public NetworkVariable<FixedString64Bytes> UserId=new NetworkVariable<FixedString64Bytes>();
  public NetworkVariable<FixedString128Bytes> DisplayName=new NetworkVariable<FixedString128Bytes>();
  public NetworkVariable<int> Team=new NetworkVariable<int>(),Seat=new NetworkVariable<int>(),Skin=new NetworkVariable<int>();
  public NetworkVariable<int> Health=new NetworkVariable<int>(3),MaxHealth=new NetworkVariable<int>(3),Shield=new NetworkVariable<int>();
  public NetworkVariable<int> Weapon=new NetworkVariable<int>(),Item=new NetworkVariable<int>(),Kills=new NetworkVariable<int>(),Deaths=new NetworkVariable<int>(),DamageDone=new NetworkVariable<int>();
  public NetworkVariable<Vector2> Position=new NetworkVariable<Vector2>();
  public NetworkVariable<float> Angle=new NetworkVariable<float>(),Revive=new NetworkVariable<float>();
  public NetworkVariable<double> SnapshotTime=new NetworkVariable<double>();
  public NetworkVariable<uint> InputEpoch=new NetworkVariable<uint>();
  public NetworkVariable<bool> Connected=new NetworkVariable<bool>();
  public Rigidbody2D Body {get;private set;}
  public Vector2 Move,Aim=Vector2.right;public bool Fire,Use;
  public float LastInput,NextShot,ProtectionUntil,DashUntil,DisconnectAt=-1;
  public Vector2 DashDirection;
  float nextInput;uint sequence,lastSequence;SpriteRenderer body,hat,nose,shieldRing;TextMesh label;
  readonly SnapshotBuffer2D snapshots=new SnapshotBuffer2D();
  public bool Alive=>Health.Value>0;
  public override void OnNetworkSpawn(){
   All.Add(this);Body=GetComponent<Rigidbody2D>();Body.gravityScale=0;Body.freezeRotation=true;Body.drag=2;Body.simulated=IsServer;
   GetComponent<CircleCollider2D>().radius=.42f;
   if(IsServer&&!IsClient)return;
   body=Draw2D.Shape(transform,"Body",Vector2.zero,Vector2.one*.85f,Color.white,true,8);
   hat=Draw2D.Shape(transform,"Costume",new Vector2(0,.16f),Vector2.one*.42f,Color.white,Skin.Value%2==0,9);
   nose=Draw2D.Shape(transform,"Aim",new Vector2(.45f,0),new Vector2(.55f,.2f),Draw2D.Gold,false,10);
   shieldRing=Draw2D.Shape(transform,"Shield",Vector2.zero,Vector2.one*1.12f,new Color(.5f,.9f,1,.3f),true,7);
   var go=new GameObject("Name");go.transform.SetParent(transform,false);go.transform.localPosition=new Vector3(0,.85f,0);
   label=go.AddComponent<TextMesh>();label.font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","Noto Sans CJK SC","Arial"},36);label.fontSize=36;label.characterSize=.14f;label.anchor=TextAnchor.MiddleCenter;
   label.GetComponent<MeshRenderer>().sharedMaterial=label.font.material;
   label.GetComponent<MeshRenderer>().sortingOrder=12;
   if(!IsServer)transform.position=Position.Value;
  }
  public override void OnNetworkDespawn(){All.Remove(this);snapshots.Clear();}
  public void Initialize(RoomMember m,Vector2 spawn) {
   UserId.Value=m.userId;DisplayName.Value=m.username;Team.Value=m.team;Seat.Value=m.seat;Skin.Value=m.skin;
   Health.Value=MaxHealth.Value=GameConfig.Current.baseHealth;Position.Value=spawn;Body.position=spawn;transform.position=spawn;
  }
  void Update(){
   if(!IsSpawned)return;
   if(IsServer&&!IsClient)return;
   float renderAngle=Angle.Value;
   if(!IsServer){
    snapshots.Push(SnapshotTime.Value,Position.Value,Angle.Value);
    if(!IsOwner&&snapshots.Evaluate(NetworkManager.ServerTime.Time-NetworkSettings.Current.InterpolationSeconds,NetworkSettings.Current.ExtrapolationSeconds,out var renderPosition,out var interpolatedAngle)){
     transform.position=renderPosition;renderAngle=interpolatedAngle;
    }else transform.position=Vector2.Lerp(transform.position,Position.Value,1-Mathf.Exp(-18*Time.deltaTime));
   }
   Color team=Team.Value==0?Draw2D.Blue:Draw2D.Red;
   body.color=Alive?team:new Color(.33f,.37f,.4f);hat.color=new[]{new Color(1,.8f,.32f),new Color(.75f,.5f,1),new Color(.4f,1,.75f),new Color(1,.55f,.8f)}[Mathf.Clamp(Skin.Value,0,3)];
   nose.transform.localRotation=Quaternion.Euler(0,0,renderAngle);nose.transform.localPosition=new Vector2(Mathf.Cos(renderAngle*Mathf.Deg2Rad),Mathf.Sin(renderAngle*Mathf.Deg2Rad))*.45f;
   shieldRing.enabled=Shield.Value>0;hat.enabled=Alive;
   label.text=DisplayName.Value.ToString()+(Connected.Value?"":" [离线]")+"\n"+(Alive?Health.Value+"/"+MaxHealth.Value:Revive.Value>0?("复活 "+Mathf.CeilToInt(GameConfig.Current.reviveSeconds-Revive.Value)+"s"):"倒地");
   label.transform.rotation=Quaternion.identity;
   if(!IsOwner||!IsClient||!Connected.Value)return;
   if(Application.isFocused&&Input.GetKeyDown(KeyCode.E))Use=true;
   if(Time.unscaledTime<nextInput)return;nextInput=Time.unscaledTime+1f/GameConfig.Current.physicsHz;
   var mouse=Camera.main.ScreenToWorldPoint(Input.mousePosition);var aim=(Vector2)(mouse-transform.position);
   bool over=EventSystem.current!=null&&EventSystem.current.IsPointerOverGameObject();
   Vector2 movement=Application.isFocused?new Vector2(Input.GetAxisRaw("Horizontal"),Input.GetAxisRaw("Vertical")):Vector2.zero;
   bool fire=Application.isFocused&&Input.GetMouseButton(0)&&!over;
   if(!Application.isFocused)Use=false;
#if BOUND_SHOOT_QA
   if(QaClientDriver.Active!=null)QaClientDriver.Active.Input(this,out movement,out aim,out fire);
#endif
   InputServerRpc(movement,aim.normalized,fire,Use,++sequence,InputEpoch.Value);Use=false;
  }
  [ServerRpc]
  void InputServerRpc(Vector2 move,Vector2 aim,bool fire,bool use,uint seq,uint epoch){
   if(!Connected.Value||epoch!=InputEpoch.Value||!Finite(move)||!Finite(aim)||seq<=lastSequence)return;
   lastSequence=seq;Move=Vector2.ClampMagnitude(move,1);Aim=aim.sqrMagnitude>.01f?aim.normalized:Aim;Fire=fire;Use|=use;LastInput=Time.time;
  }
  static bool Finite(Vector2 p)=>!float.IsNaN(p.x)&&!float.IsNaN(p.y)&&!float.IsInfinity(p.x)&&!float.IsInfinity(p.y);
  public void ResetInput(){Move=Vector2.zero;Fire=false;Use=false;lastSequence=0;LastInput=0;if(IsServer)InputEpoch.Value++;}
  public void Tick(float dt,bool playing){
   if(!IsServer)return;
   var c=GameConfig.Current;
   if(Time.time-LastInput>c.inputTimeout||!Connected.Value){Move=Vector2.zero;Fire=false;Use=false;}
   if(Alive&&playing&&Connected.Value){
    Vector2 target=Time.time<DashUntil?DashDirection*c.dashSpeed:Move*c.moveSpeed;
    Body.AddForce((target-Body.velocity)*c.acceleration);
    Angle.Value=Mathf.Atan2(Aim.y,Aim.x)*Mathf.Rad2Deg;
   }else Body.velocity*=Mathf.Exp(-dt*1.2f);
   Body.mass=Alive?1:1.6f;
   if(Body.velocity.magnitude>c.dashSpeed*1.5f)Body.velocity=Body.velocity.normalized*c.dashSpeed*1.5f;
   Position.Value=Body.position;
   SnapshotTime.Value=NetworkManager.ServerTime.Time;
  }
  public bool Equip(WeaponKind kind){
   if(!IsServer||!BattleRules.CanEquip(Alive,(WeaponKind)Weapon.Value))return false;
   var w=GameConfig.Current.Weapon(kind);if(w==null)return false;
   Weapon.Value=(int)kind;Health.Value=MaxHealth.Value=w.maxHealth;return true;
  }
  public void Hurt(int amount,NetPlayer source){
   if(!IsServer||!Alive||Time.time<ProtectionUntil)return;
   if(source!=null&&source.Team.Value==Team.Value)return;
   int hp=Health.Value,shield=Shield.Value;int before=hp+shield;
   BattleRules.Damage(ref hp,ref shield,amount);Shield.Value=shield;Health.Value=hp;
   if(source!=null)source.DamageDone.Value+=before-hp-shield;
   if(hp<=0){Deaths.Value++;Weapon.Value=Item.Value=Shield.Value=0;Revive.Value=0;DashUntil=0;ResetInput();if(source!=null)source.Kills.Value++;}
  }
  public void Resurrect(){
   Health.Value=MaxHealth.Value=GameConfig.Current.baseHealth;Weapon.Value=Item.Value=Shield.Value=0;Revive.Value=0;
   ProtectionUntil=Time.time+GameConfig.Current.protectionSeconds;DashUntil=0;ResetInput();
  }
 }
}
