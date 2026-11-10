using System.Linq;
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
  public NetworkVariable<int> Health=new NetworkVariable<int>(30),MaxHealth=new NetworkVariable<int>(30),Shield=new NetworkVariable<int>();
  public NetworkVariable<int> Ammo=new NetworkVariable<int>();
  public NetworkVariable<uint> EquipmentVersion=new NetworkVariable<uint>();
  public NetworkVariable<int> ItemA=new NetworkVariable<int>(),ItemB=new NetworkVariable<int>(),ItemC=new NetworkVariable<int>(),SelectedSlot=new NetworkVariable<int>();
  public NetworkVariable<int> Weapon=new NetworkVariable<int>(),Kills=new NetworkVariable<int>(),Deaths=new NetworkVariable<int>(),DamageDone=new NetworkVariable<int>();
  public NetworkVariable<Vector2> Position=new NetworkVariable<Vector2>();
  public NetworkVariable<float> Angle=new NetworkVariable<float>(),Revive=new NetworkVariable<float>();
  public NetworkVariable<double> SnapshotTime=new NetworkVariable<double>();
  public NetworkVariable<uint> InputEpoch=new NetworkVariable<uint>();
  public NetworkVariable<bool> Connected=new NetworkVariable<bool>();
  public NetworkVariable<bool> IsBot=new NetworkVariable<bool>();
  public Rigidbody2D Body {get;private set;}
  public bool RescueHeld;
  public readonly NetworkVariable<float> RescueProgress=new NetworkVariable<float>();
  public readonly NetworkVariable<int> Rescues=new NetworkVariable<int>();
  public readonly NetworkVariable<int> Assists=new NetworkVariable<int>();
  readonly System.Collections.Generic.HashSet<NetPlayer> damageContributors=new System.Collections.Generic.HashSet<NetPlayer>();
  public float AimDistance;
  public Vector2 Move,Aim=Vector2.right;public bool Fire;
  public float LastInput,NextShot,ProtectionUntil,SpeedBoostUntil,DisconnectAt=-1,DeathAt=-1;
  public BattleWorld AuthorityWorld;public NetworkVariable<int> Surface=new NetworkVariable<int>();
  // Local training role: 0 controlled player, 1 passive teammate, 2 target. Never an AI.
  public int TrainingRole {get;private set;}
  public bool SpeedBoosted=>Alive&&Time.time<SpeedBoostUntil;
  float nextInput;bool requireFireRelease;uint sequence,lastSequence;CyberPlayerView art;TextMesh label;
  readonly SnapshotBuffer2D snapshots=new SnapshotBuffer2D();
  public bool Alive=>Health.Value>0;
  public override void OnNetworkSpawn(){
   All.Add(this);Body=GetComponent<Rigidbody2D>();Body.gravityScale=0;Body.freezeRotation=true;Body.drag=2;Body.simulated=IsServer;
   Body.interpolation=IsServer&&IsClient?RigidbodyInterpolation2D.Interpolate:RigidbodyInterpolation2D.None;
   GetComponent<CircleCollider2D>().radius=.46f;
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
   TrainingRole=m.trainingRole;ClearInventory();GiveDefaultKnife();
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
   label.text=DisplayName.Value.ToString()+(IsBot.Value?" · AI":Connected.Value?"":" · 离线")+(Alive?"":" · 倒地");
   label.transform.rotation=Quaternion.identity;
   if(!IsOwner||!IsClient||IsBot.Value||!Connected.Value||TrainingRole!=0)return;
   bool focused=Application.isFocused&&(GameClient.Instance==null||!GameClient.Instance.InputBlocked);
   bool over=EventSystem.current!=null&&EventSystem.current.IsPointerOverGameObject();
   bool cycle=focused&&Input.GetKeyDown(KeyCode.E);
   bool press=focused&&!over&&Input.GetMouseButtonDown(0);
   bool release=Input.GetMouseButtonUp(0);
   if(Time.unscaledTime<nextInput&&!cycle&&!press&&!release)return;nextInput=Time.unscaledTime+1f/GameConfig.Current.physicsHz;
   var mouse=Camera.main.ScreenToWorldPoint(Input.mousePosition);var aim=(Vector2)(mouse-transform.position);
   Vector2 movement=focused?new Vector2(Input.GetAxisRaw("Horizontal"),Input.GetAxisRaw("Vertical")):Vector2.zero;
   bool fire=focused&&Input.GetMouseButton(0)&&!over;
#if BOUND_SHOOT_QA
   if(QaClientDriver.Active!=null)QaClientDriver.Active.Input(this,out movement,out aim,out fire);
#endif
   SubmitInput(movement,aim,fire,cycle,press,!Input.GetMouseButton(0),focused&&Input.GetKey(KeyCode.F));
  }
  float visualAngle;Renderer[] fogRenderers;public bool FogHidden {get;private set;}
  void LateUpdate(){
   if(!IsSpawned||art==null)return;
   if(AuthorityWorld==null)AuthorityWorld=NetworkManager.SpawnManager.SpawnedObjectsList.Select(n=>n.GetComponent<BattleWorld>()).FirstOrDefault(w=>w!=null);
   FogHidden=AuthorityWorld!=null&&!AuthorityWorld.CanSeeFromLocal(this);
   art.Show(Team.Value,Weapon.Value,visualAngle,Alive,Shield.Value>0);
   if(fogRenderers==null)fogRenderers=GetComponentsInChildren<Renderer>();foreach(var r in fogRenderers)if(r!=null)r.forceRenderingOff=FogHidden;
  }
  public void SubmitInput(Vector2 move,Vector2 aim,bool fire,bool cycle,bool press=false,bool? released=null,bool rescue=false){if(IsOwner&&IsClient&&!IsBot.Value)InputServerRpc(move,aim,fire,cycle,press,released??!fire,rescue,++sequence,InputEpoch.Value);}
  [ServerRpc(RequireOwnership=false)]
  void InputServerRpc(Vector2 move,Vector2 aim,bool fire,bool cycle,bool press,bool released,bool rescue,uint seq,uint epoch,ServerRpcParams rpc=default){
   // Ownership can change while a final input packet is in flight on disconnect.
   if(rpc.Receive.SenderClientId!=OwnerClientId||IsBot.Value||!Connected.Value||epoch!=InputEpoch.Value||!Finite(move)||!Finite(aim)||seq<=lastSequence)return;
   lastSequence=seq;AimDistance=aim.magnitude;Move=Vector2.ClampMagnitude(move,1);Aim=aim.sqrMagnitude>.01f?aim.normalized:Aim;LastInput=Time.time;
   if(released)requireFireRelease=false;
   Fire=fire&&!requireFireRelease&&SelectedSlot.Value==0;
   if(!Alive||AuthorityWorld==null||AuthorityWorld.Phase.Value!=2){Fire=false;return;}
   RescueHeld=rescue;
   if(AuthorityWorld.RescueTarget(this)!=null&&RescueHeld){Fire=false;return;}
   if(cycle){SelectNextSlot();Fire=false;}
   if(press&&!requireFireRelease){
    if(SelectedSlot.Value==0)AuthorityWorld.TryAttack(this);
    else{Fire=false;if(AuthorityWorld.TryUseSelected(this))requireFireRelease=fire;}
   }
  }
  static bool Finite(Vector2 p)=>!float.IsNaN(p.x)&&!float.IsNaN(p.y)&&!float.IsInfinity(p.x)&&!float.IsInfinity(p.y);
  public void ResetInput(){RescueHeld=false;if(IsServer)RescueProgress.Value=0;AimDistance=0;Move=Vector2.zero;Fire=false;requireFireRelease=false;lastSequence=0;LastInput=0;if(IsServer)InputEpoch.Value++;}
  public void Tick(float dt,bool playing){
   if(!IsServer)return;
   var c=GameConfig.Current;
   if(Time.time-LastInput>c.inputTimeout||!Connected.Value){Move=Vector2.zero;Fire=false;RescueHeld=false;}
   if(Alive&&playing&&Connected.Value){
    bool rescuing=RescueHeld&&AuthorityWorld!=null&&AuthorityWorld.RescueTarget(this)!=null;
    if(rescuing)Fire=false;
    float speed=c.moveSpeed*(SpeedBoosted?c.speedBoostMultiplier:1);
    Vector2 target=rescuing?Vector2.zero:Move*speed;
    Body.AddForce((target-Body.velocity)*(Move.sqrMagnitude>.01f?c.acceleration:2.5f));
    Angle.Value=Mathf.Atan2(Aim.y,Aim.x)*Mathf.Rad2Deg;
   }
   Body.mass=1;Body.drag=!Alive?c.downedDrag:Move.sqrMagnitude>.01f?1:.6f;
   float velocityLimit=Mathf.Max(21,c.moveSpeed*c.speedBoostMultiplier*1.5f);
   if(Body.velocity.magnitude>velocityLimit)Body.velocity=Body.velocity.normalized*velocityLimit;
   Position.Value=Body.position;
   if(AuthorityWorld!=null)Surface.Value=ForestMap.SurfaceAt(AuthorityWorld.MapOverride??c.Map(AuthorityWorld.MapKey.Value.ToString()),Body.position);
   SnapshotTime.Value=NetworkManager.ServerTime.Time;
  }
  public bool Equip(WeaponKind kind){
   if(!IsServer||!Alive)return false;
   var w=GameConfig.Current.Weapon(kind);if(w==null)return false;
   if(Weapon.Value==(int)kind&&(BattleRules.IsMelee(kind)||Ammo.Value>=w.ammoCapacity))return false;
   Weapon.Value=(int)kind;Ammo.Value=BattleRules.IsMelee(kind)?0:w.ammoCapacity;EquipmentVersion.Value++;
   NextShot=Mathf.Max(NextShot,Time.time+w.cooldown);return true;
  }
  public int ItemAt(int slot)=>slot==1?ItemA.Value:slot==2?ItemB.Value:slot==3?ItemC.Value:0;
  public int InventoryCount=>(ItemA.Value>0?1:0)+(ItemB.Value>0?1:0)+(ItemC.Value>0?1:0);
  void SetItem(int slot,int kind){if(slot==1)ItemA.Value=kind;else if(slot==2)ItemB.Value=kind;else if(slot==3)ItemC.Value=kind;}
  public bool TryStoreItem(ItemKind kind){
   if(!IsServer||!Alive||kind==ItemKind.None||kind==ItemKind.Mystery||GameConfig.Current.Item(kind)?.enabled!=true)return false;
   for(int slot=1;slot<=3;slot++)if(ItemAt(slot)==0){SetItem(slot,(int)kind);return true;}return false;
  }
  public void SelectNextSlot(){
   if(!IsServer||!Alive)return;for(int step=1;step<=4;step++){int slot=(SelectedSlot.Value+step)%4;if(slot==0||ItemAt(slot)>0){SelectedSlot.Value=slot;Fire=false;return;}}
  }
  public void ConsumeSelectedItem(){if(!IsServer)return;SetItem(SelectedSlot.Value,0);SelectedSlot.Value=0;Fire=false;}
  public void ClearInventory(){if(!IsServer)return;ItemA.Value=ItemB.Value=ItemC.Value=0;SelectedSlot.Value=0;}
  public void GiveDefaultKnife(){if(!IsServer)return;Weapon.Value=(int)WeaponKind.Dagger;Ammo.Value=0;EquipmentVersion.Value++;}
  public void Hurt(int amount,NetPlayer source,Vector2 knockbackImpulse=default(Vector2)){
   if(!IsServer||!Alive||Time.time<ProtectionUntil)return;
   if(source!=null&&!BattleRules.CanHit(source.Team.Value,Team.Value,source==this,GameConfig.Current.friendlyFire))return;
   TakeDamage(amount,source,source!=null?source.Team.Value:-1,knockbackImpulse);
  }
  internal void HurtFromFire(int amount,NetPlayer source,int sourceTeam){TakeDamage(amount,source,sourceTeam,Vector2.zero);}
  void TakeDamage(int amount,NetPlayer source,int sourceTeam,Vector2 knockbackImpulse){
   if(!IsServer||!Alive||Time.time<ProtectionUntil)return;
   bool enemy=source!=null&&sourceTeam!=Team.Value;
   int hp=Health.Value,shield=Shield.Value;int before=hp+shield;
   BattleRules.Damage(ref hp,ref shield,amount);if(hp+shield<before)RescueProgress.Value=0;Shield.Value=shield;Health.Value=hp;
   if(enemy&&before>hp+shield){
    source.DamageDone.Value+=before-hp-shield;
    damageContributors.Add(source);
   }
   if(hp<=0){
    Deaths.Value++;Weapon.Value=Ammo.Value=Shield.Value=0;ClearInventory();Revive.Value=0;DeathAt=Time.time;SpeedBoostUntil=0;ResetInput();
    if(enemy){
     source.Kills.Value++;
     foreach(var contributor in damageContributors)
      if(contributor!=null&&contributor.IsSpawned&&contributor!=source&&contributor.Team.Value==sourceTeam)contributor.Assists.Value++;
     AuthorityWorld?.AnnounceKill(source,this);
     AuthorityWorld?.FeedbackClientRpc(Body.position,7);
    }
    damageContributors.Clear();
   }
   // Apply only accepted damage (including absorbed shield damage), after death resets.
   // Authority physics, walls and the existing rope forces resolve the displacement.
   if(before>hp+shield&&Finite(knockbackImpulse)&&knockbackImpulse.sqrMagnitude>0)
    Body.AddForce(knockbackImpulse,ForceMode2D.Impulse);
  }
  public void Resurrect(){
   damageContributors.Clear();
   Health.Value=MaxHealth.Value=GameConfig.Current.baseHealth;GiveDefaultKnife();ClearInventory();Shield.Value=0;Revive.Value=0;DeathAt=-1;
   ProtectionUntil=Time.time+GameConfig.Current.protectionSeconds;SpeedBoostUntil=0;ResetInput();
  }
 }
}
