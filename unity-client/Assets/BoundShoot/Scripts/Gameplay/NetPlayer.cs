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
  public NetworkVariable<bool> IsBot=new NetworkVariable<bool>();
  public Rigidbody2D Body {get;private set;}
  public Vector2 Move,Aim=Vector2.right;public bool Fire,Use;
  public float LastInput,NextShot,ProtectionUntil,DashUntil,DisconnectAt=-1;
  // Local training role: 0 controlled player, 1 passive teammate, 2 target. Never an AI.
  public int TrainingRole {get;private set;}
  public Vector2 DashDirection;
  float nextInput;bool pendingUse;uint sequence,lastSequence;CyberPlayerView art;TextMesh label;
  readonly SnapshotBuffer2D snapshots=new SnapshotBuffer2D();
  public bool Alive=>Health.Value>0;
  public override void OnNetworkSpawn(){
   All.Add(this);Body=GetComponent<Rigidbody2D>();Body.gravityScale=0;Body.freezeRotation=true;Body.drag=2;Body.simulated=IsServer;
   Body.interpolation=IsServer&&IsClient?RigidbodyInterpolation2D.Interpolate:RigidbodyInterpolation2D.None;
   GetComponent<CircleCollider2D>().radius=.42f;
   if((IsServer&&!IsClient)||(Application.isBatchMode&&!Draw2D.RenderInBatch))return;
   art=gameObject.AddComponent<CyberPlayerView>();art.Build();
   var go=new GameObject("Name");go.transform.SetParent(transform,false);go.transform.localPosition=new Vector3(0,1.15f,0);
   label=go.AddComponent<TextMesh>();label.font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","Noto Sans CJK SC","Arial"},36);label.fontSize=36;label.characterSize=.10f;label.anchor=TextAnchor.MiddleCenter;
   label.GetComponent<MeshRenderer>().sharedMaterial=label.font.material;
   label.GetComponent<MeshRenderer>().sortingOrder=12;
   if(!IsServer)transform.position=Position.Value;
  }
  public override void OnNetworkDespawn(){All.Remove(this);snapshots.Clear();}
  public void Initialize(RoomMember m,Vector2 spawn) {
   UserId.Value=m.userId;DisplayName.Value=m.username;Team.Value=m.team;Seat.Value=m.seat;Skin.Value=m.skin;
   IsBot.Value=m.isBot==1;Connected.Value=IsBot.Value;
   TrainingRole=m.trainingRole;
   Health.Value=MaxHealth.Value=GameConfig.Current.baseHealth;Position.Value=spawn;Body.position=spawn;transform.position=spawn;
  }
  void Update(){
   if(!IsSpawned)return;
   if((IsServer&&!IsClient)||(Application.isBatchMode&&!Draw2D.RenderInBatch))return;
   float renderAngle=Angle.Value;
   if(!IsServer){
    snapshots.Push(SnapshotTime.Value,Position.Value,Angle.Value);
    if(!IsOwner&&snapshots.Evaluate(NetworkManager.ServerTime.Time-NetworkSettings.Current.InterpolationSeconds,NetworkSettings.Current.ExtrapolationSeconds,out var renderPosition,out var interpolatedAngle)){
     transform.position=renderPosition;renderAngle=interpolatedAngle;
    }else transform.position=Vector2.Lerp(transform.position,Position.Value,1-Mathf.Exp(-18*Time.deltaTime));
   }
   visualAngle=renderAngle;
   label.text=DisplayName.Value.ToString()+(IsBot.Value?" · AI":Connected.Value?"":" · 离线")+(Alive?"":Revive.Value>0?" · 复活中":" · 倒地");
   label.transform.rotation=Quaternion.identity;
   if(!IsOwner||!IsClient||IsBot.Value||!Connected.Value||TrainingRole!=0)return;
   bool focused=Application.isFocused&&(GameClient.Instance==null||!GameClient.Instance.InputBlocked);
   if(focused&&Input.GetKeyDown(KeyCode.E))pendingUse=true;
   if(Time.unscaledTime<nextInput)return;nextInput=Time.unscaledTime+1f/GameConfig.Current.physicsHz;
   var mouse=Camera.main.ScreenToWorldPoint(Input.mousePosition);var aim=(Vector2)(mouse-transform.position);
   bool over=EventSystem.current!=null&&EventSystem.current.IsPointerOverGameObject();
   Vector2 movement=focused?new Vector2(Input.GetAxisRaw("Horizontal"),Input.GetAxisRaw("Vertical")):Vector2.zero;
   bool fire=focused&&Input.GetMouseButton(0)&&!over;
   if(!focused)pendingUse=false;
#if BOUND_SHOOT_QA
   if(QaClientDriver.Active!=null)QaClientDriver.Active.Input(this,out movement,out aim,out fire);
#endif
   SubmitInput(movement,aim.normalized,fire,pendingUse);pendingUse=false;
  }
  float visualAngle;
  void LateUpdate(){if(IsSpawned&&art!=null)art.Show(Team.Value,Weapon.Value,visualAngle,Alive,Shield.Value>0);}
  public void SubmitInput(Vector2 move,Vector2 aim,bool fire,bool use){if(IsOwner&&IsClient&&!IsBot.Value)InputServerRpc(move,aim,fire,use,++sequence,InputEpoch.Value);}
  [ServerRpc]
  void InputServerRpc(Vector2 move,Vector2 aim,bool fire,bool use,uint seq,uint epoch){
   if(!Connected.Value||epoch!=InputEpoch.Value||!Finite(move)||!Finite(aim)||seq<=lastSequence)return;
   lastSequence=seq;Move=Vector2.ClampMagnitude(move,1);Aim=aim.sqrMagnitude>.01f?aim.normalized:Aim;Fire=fire;Use|=use;LastInput=Time.time;
  }
  static bool Finite(Vector2 p)=>!float.IsNaN(p.x)&&!float.IsNaN(p.y)&&!float.IsInfinity(p.x)&&!float.IsInfinity(p.y);
  public void ResetInput(){Move=Vector2.zero;Fire=false;Use=false;pendingUse=false;lastSequence=0;LastInput=0;if(IsServer)InputEpoch.Value++;}
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
   if(!IsServer||!Alive||Weapon.Value==(int)kind)return false;
   var w=GameConfig.Current.Weapon(kind);if(w==null)return false;
   Weapon.Value=(int)kind;MaxHealth.Value=w.maxHealth;Health.Value=Mathf.Min(Health.Value,w.maxHealth);
   NextShot=Mathf.Max(NextShot,Time.time+w.cooldown);return true;
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
