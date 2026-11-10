using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.Collections;
using UnityEngine;

namespace BoundShoot {
 [Serializable] public class PickupState {public Vector2 position;public int weapon,item,supplyTier;public bool active;public float respawnAt;public float respawnSeconds;public bool randomizeOnRespawn;}
 [Serializable] public class ProjectileState {public ulong id;public double bornAt;public Vector2 position,direction;public float remaining,speed;public int weapon,damage,ownerSeat,team;[NonSerialized]public NetPlayer owner;[NonSerialized]public float knockbackImpulse;}
 [Serializable] public class BombState {public Vector2 position;public float explodeAt;public int team;[NonSerialized]public NetPlayer owner;}
 [Serializable] public class RopeState {public ulong playerA,playerB;public int team,seatA,seatB;public float tension;public Vector2[] points;}
 [Serializable] public class WorldSnapshot {public uint round;public double at;public PickupState[] pickups;public ProjectileState[] bullets;public BombState[] bombs;public RopeState[] ropes;public ThrowableState[] throwables;}
 public partial class BattleWorld:NetworkBehaviour {
  public static BattleWorld Instance;
  public string SelectedMap="Arena_01";
  public bool Training,ManualStart;[NonSerialized] public MapDefinition MapOverride;
  public bool IsOnlineWarmup=>MapKey.Value.ToString()==TrainingMap.WarmupId;
  float warmupDisconnectedAt=-1;
  [ServerRpc(RequireOwnership=false)]
  public void StartOnlineBattleServerRpc(ServerRpcParams rpc=default){
   if(!IsOnlineWarmup||Phase.Value!=2||Roster==null||Players.Any(p=>!p.Connected.Value))return;
   var sender=Players.Find(p=>!p.IsBot.Value&&p.OwnerClientId==rpc.Receive.SenderClientId);
   if(sender==null||sender.UserId.Value.ToString()!=Roster.warmupHostId)return;
   if(GameConfig.Current.Map(Roster.mapId)==null)return;
   MapKey.Value=new FixedString64Bytes(Roster.mapId);
   pickups.Clear();
   foreach(var definition in MapContent.Pickups(map,GameConfig.Current.pickupRespawn))pickups.Add(MapContent.CreatePickup(definition));
   ResetState();
   foreach(var p in Players)bots[p]=new BotController(p,map,Players,pickups);
   nextSnapshot=0;
   Clock.Value=GameConfig.Current.countdownSeconds;
   Phase.Value=1;
   roundAt=Time.time+Clock.Value;
  }
  public int RopeCount=>ropes.Count;
  public void BeginLocalRound(){if(IsServer&&ManualStart&&Phase.Value==0&&Players.All(p=>p.Connected.Value)){ManualStart=false;Clock.Value=GameConfig.Current.countdownSeconds;Phase.Value=1;roundAt=Time.time+GameConfig.Current.countdownSeconds;}}
  [ServerRpc(RequireOwnership=false)] public void LeaveLocalServerRpc(ServerRpcParams rpc=default){if(LocalMatchRuntime.Instance!=null&&rpc.Receive.SenderClientId!=NetworkManager.ServerClientId)NetworkManager.DisconnectClient(rpc.Receive.SenderClientId);}
  public IReadOnlyList<PickupState> Supplies=>pickups;
  public NetworkVariable<FixedString64Bytes> MapKey=new NetworkVariable<FixedString64Bytes>(new FixedString64Bytes("Arena_01"));
  public NetworkVariable<int> Phase=new NetworkVariable<int>(),Winner=new NetworkVariable<int>(-1);
  public NetworkVariable<float> Clock=new NetworkVariable<float>();
  public NetworkVariable<uint> RoundId=new NetworkVariable<uint>(1);
  public NetworkVariable<int> TeamSize=new NetworkVariable<int>(2);
  public ServerRoster Roster;public readonly List<NetPlayer> Players=new List<NetPlayer>();
  readonly List<RopeLink> ropes=new List<RopeLink>();
  readonly List<PickupState> pickups=new List<PickupState>();
  readonly List<ProjectileState> bullets=new List<ProjectileState>();
  readonly List<BombState> bombs=new List<BombState>();
  readonly Dictionary<NetPlayer,BotController> bots=new Dictionary<NetPlayer,BotController>();
  readonly List<SpriteRenderer> pickupViews=new List<SpriteRenderer>(),pickupMarkers=new List<SpriteRenderer>(),bombViews=new List<SpriteRenderer>();
  readonly List<RopeLineView> ropeViews=new List<RopeLineView>();
  ProjectileVisuals projectileVisuals;ulong nextShotId;
  readonly Dictionary<ulong,ulong> presentedShots=new Dictionary<ulong,ulong>();
  RopeState[] renderedRopes;
  ArenaView arena;
  RopePath paths;MapDefinition map;float startAt,roundAt,nextSnapshot;
  float[] shortenUntil=new float[2],extendUntil=new float[2];float elasticUntil;
  bool resultSent;
  public int VisibleProjectileCount=>projectileVisuals==null?0:projectileVisuals.ActiveCount;
  public bool CanRender=>IsClient&&(!Application.isBatchMode||Draw2D.RenderInBatch);
  public event Action<BattleResult> OnResult;
  public override void OnNetworkSpawn(){
   Instance=this;if(IsServer)MapKey.Value=SelectedMap;map=MapOverride??GameConfig.Current.Map(MapKey.Value.ToString());
   BuildArena();
   MapKey.OnValueChanged+=MapChanged;
   RoundId.OnValueChanged+=RoundChanged;Phase.OnValueChanged+=PhaseChanged;
   if(CanRender){projectileVisuals=new GameObject("Continuous projectiles").AddComponent<ProjectileVisuals>();projectileVisuals.transform.SetParent(transform,false);projectileVisuals.Initialize(this);}
  }
  public override void OnNetworkDespawn(){MapKey.OnValueChanged-=MapChanged;RoundId.OnValueChanged-=RoundChanged;Phase.OnValueChanged-=PhaseChanged;ClearPresentation();if(Instance==this)Instance=null;}
  void BuildArena(){
   if(arena!=null){arena.gameObject.SetActive(false);Destroy(arena.gameObject);}
   arena=new GameObject("Arena").AddComponent<ArenaView>();
   arena.transform.SetParent(transform,false);
   arena.Build(map);
   paths=new RopePath(map);
  }
  void MapChanged(FixedString64Bytes previous,FixedString64Bytes current){
   var next=GameConfig.Current.Map(current.ToString());
   if(next==null)return;
   map=next;
   foreach(var view in controlViews){view.gameObject.SetActive(false);Destroy(view.gameObject);}
   controlViews.Clear();
   foreach(var view in pickupViews)view.gameObject.SetActive(false);
   foreach(var view in pickupMarkers)view.gameObject.SetActive(false);
   foreach(var view in bombViews)view.gameObject.SetActive(false);
   foreach(var view in ropeViews)view.Show(null,0,0);
   renderedRopes=null;
   BuildArena();
  }
  internal bool SelectLocalMap(string id){
   if(!IsServer||Training||!ManualStart||Phase.Value!=0||Roster==null)return false;
   var next=GameConfig.Current.Map(id);
   if(next==null)return false;
   if(MapKey.Value.ToString()==id)return true;
   MapOverride=next;
   SelectedMap=Roster.mapId=id;
   MapKey.Value=new FixedString64Bytes(id);
   pickups.Clear();
   foreach(var definition in MapContent.Pickups(map,GameConfig.Current.pickupRespawn))pickups.Add(MapContent.CreatePickup(definition));
   ResetState();
   foreach(var p in Players)bots[p]=new BotController(p,map,Players,pickups);
   Clock.Value=0;
   nextSnapshot=0;
   return true;
  }
  void ClearPresentation(){projectileVisuals?.Clear();presentedShots.Clear();ClearThrowables();}
  void RoundChanged(uint oldRound,uint newRound){ClearPresentation();}
  void PhaseChanged(int previous,int current){if(current==0||current==3)ClearPresentation();}
  public void Setup(ServerRoster roster){
   Roster=roster;map=MapOverride??GameConfig.Current.Map(IsOnlineWarmup?TrainingMap.WarmupId:roster.mapId);TeamSize.Value=Mathf.Clamp(roster.players.Count(p=>p.team==0),2,4);
   foreach(var m in roster.players){
    SpawnPlayer(m);
   }
   RebuildRopes();
   foreach(var definition in MapContent.Pickups(map,GameConfig.Current.pickupRespawn))pickups.Add(MapContent.CreatePickup(definition));
   ResetObjectives();Clock.Value=0;startAt=Time.time;
   if(Training){Phase.Value=2;Clock.Value=0;}
  }
  NetPlayer SpawnPlayer(RoomMember member){
   var p=Instantiate(Resources.Load<GameObject>("NetPlayer")).GetComponent<NetPlayer>();
   p.NetworkObject.Spawn();p.NetworkObject.DontDestroyWithOwner=true;
   Players.Add(p);p.AuthorityWorld=this;p.Initialize(member,(member.team==0?map.blueSpawns:map.redSpawns)[member.seat]);p.DisconnectAt=-1;
   if(Training&&member.trainingRole>0)p.Connected.Value=true;
   if(!Training)bots[p]=new BotController(p,map,Players,pickups);
   return p;
  }
  void RebuildRopes(){
   foreach(var rope in ropes)if(rope.line!=null)Destroy(rope.line.gameObject);
   ropes.Clear();
   for(int team=0;team<2;team++){
    var members=Players.Where(p=>p.Team.Value==team).OrderBy(p=>p.Seat.Value).ToArray();
    for(int i=1;i<members.Length;i++)ropes.Add(new RopeLink(members[i-1],members[i],transform));
   }
  }
  public NetPlayer AddTrainingTeammate(){
   if(!Training||!IsServer)return null;
   int count=Players.Count(p=>p.TrainingRole==1);if(count>=3)return null;
   var member=new RoomMember{userId=Guid.NewGuid().ToString(),username="队友 "+(count+1),team=0,seat=count+1,skin=(count+1)%4,trainingRole=1};
   var p=SpawnPlayer(member);RebuildRopes();return p;
  }
  public void RemoveTrainingTeammate(){
   if(!Training||!IsServer)return;
   var p=Players.Where(v=>v.TrainingRole==1).OrderByDescending(v=>v.Seat.Value).FirstOrDefault();if(p==null)return;
   Players.Remove(p);p.NetworkObject.Despawn();RebuildRopes();
  }
  public void ResetTraining(){
   if(!Training||!IsServer)return;
   ResetState();
  }
  public void RestartLocalRound(){
   if(!IsServer||Training||Phase.Value!=3)return;
   ResetState();resultSent=false;Winner.Value=-1;Clock.Value=0;startAt=Time.time;ManualStart=true;Phase.Value=0;
  }
  void ResetState(){
   RoundId.Value++;ClearPresentation();ResetObjectives();
   Array.Clear(shortenUntil,0,2);Array.Clear(extendUntil,0,2);elasticUntil=0;bullets.Clear();bombs.Clear();
   foreach(var p in Players){
    p.Resurrect();p.ProtectionUntil=0;p.SpeedBoostUntil=0;p.NextShot=0;p.Kills.Value=p.Deaths.Value=p.DamageDone.Value=p.Rescues.Value=p.Assists.Value=0;p.ResetInput();
    var spawn=(p.Team.Value==0?map.blueSpawns:map.redSpawns)[p.Seat.Value];p.Body.position=spawn;p.transform.position=spawn;p.Position.Value=spawn;p.Body.velocity=Vector2.zero;
   }
   var definitions=MapContent.Pickups(map,GameConfig.Current.pickupRespawn);
   foreach(var rope in ropes)rope.ResetRoute();
   for(int i=0;i<pickups.Count;i++)pickups[i]=MapContent.CreatePickup(definitions[i]);
  }
  public NetPlayer Find(string uid)=>Players.Find(p=>p.UserId.Value.ToString()==uid);
  public bool ConfigureTeamSize(int size,bool fillAI){
   if(!IsServer||Phase.Value!=0||size<2||size>4||Players.Any(p=>!p.IsBot.Value&&p.Seat.Value>=size))return false;
   foreach(var p in Players.Where(p=>p.Seat.Value>=size).ToArray()){Players.Remove(p);bots.Remove(p);p.NetworkObject.Despawn();}
   for(int t=0;t<2;t++)for(int s=0;s<size;s++)if(!Players.Any(p=>p.Team.Value==t&&p.Seat.Value==s))SpawnPlayer(new RoomMember{userId=Guid.NewGuid().ToString(),username="菜园搭档 "+(t*4+s+1),team=t,seat=s,skin=t*4+s,isBot=1});
   TeamSize.Value=size;SetBotFill(fillAI);RefreshRoster();RebuildRopes();return true;
  }
  public void SetBotFill(bool enabled){if(!IsServer||Phase.Value!=0)return;foreach(var p in Players.Where(p=>p.IsBot.Value)){p.Connected.Value=enabled;p.ResetInput();}}
  void RefreshRoster(){Roster.players=Players.Select(p=>new RoomMember{userId=p.UserId.Value.ToString(),username=p.DisplayName.Value.ToString(),team=p.Team.Value,seat=p.Seat.Value,skin=p.Skin.Value,isBot=p.IsBot.Value?1:0,trainingRole=p.TrainingRole}).ToArray();}
  [ServerRpc(RequireOwnership=false)]public void RequestSeatServerRpc(int team,int seat,ServerRpcParams rpc=default){
   if(Phase.Value!=0||team<0||team>1||seat<0||seat>=TeamSize.Value)return;
   var actor=Players.Find(p=>!p.IsBot.Value&&p.OwnerClientId==rpc.Receive.SenderClientId);var target=Players.Find(p=>p.Team.Value==team&&p.Seat.Value==seat);
   if(actor==null||target==null||!target.IsBot.Value||LocalMatchRuntime.Instance!=null&&LocalMatchRuntime.Instance.HasPendingSeats)return;
   target.Team.Value=actor.Team.Value;target.Seat.Value=actor.Seat.Value;actor.Team.Value=team;actor.Seat.Value=seat;
   foreach(var p in new[]{actor,target}){Vector2 point=(p.Team.Value==0?map.blueSpawns:map.redSpawns)[p.Seat.Value];p.Body.position=point;p.transform.position=point;p.Position.Value=point;p.Body.velocity=Vector2.zero;p.ResetInput();}
   RefreshRoster();RebuildRopes();
  }
  public void ResetRopesForTeleport(NetPlayer player){foreach(var rope in ropes)if(rope.a==player||rope.b==player)rope.ResetRoute();}
  public void Attach(ulong clientId,RoomMember member){
   var p=Find(member.userId);if(p==null)return;
   p.NetworkObject.ChangeOwnership(clientId);p.Connected.Value=true;p.DisconnectAt=-1;p.ResetInput();
  }
  public void Detach(ulong clientId,string userId=null){
   foreach(var p in Players)if(userId!=null?p.UserId.Value.ToString()==userId:p.OwnerClientId==clientId){
    p.Connected.Value=false;p.DisconnectAt=Time.time;p.ResetInput();p.NetworkObject.RemoveOwnership();
   }
  }
  void FixedUpdate(){
   if(!IsServer||Roster==null||resultSent)return;
   float dt=Time.fixedDeltaTime;var c=GameConfig.Current;
   bool warmup=IsOnlineWarmup;
   if(Phase.Value==0){
    if(!ManualStart&&Players.All(p=>p.Connected.Value)){Phase.Value=warmup?2:1;roundAt=Time.time+c.countdownSeconds;}
    else if(!ManualStart&&Time.time-startAt>60){ServerRuntime.Instance?.Abort("玩家连接超时");resultSent=true;return;}
   }
   if(Phase.Value==1){Clock.Value=Mathf.Max(0,roundAt-Time.time);if(Time.time>=roundAt){Phase.Value=2;roundAt=Time.time;}}
   bool playing=Phase.Value==2;
   if(playing&&warmup){
    if(Players.All(p=>p.Connected.Value))warmupDisconnectedAt=-1;
    else if(warmupDisconnectedAt<0)warmupDisconnectedAt=Time.time;
    else if(Time.time-warmupDisconnectedAt>60){ServerRuntime.Instance?.Abort("热身玩家连接超时");resultSent=true;return;}
   }
   foreach(var p in Players){
    if(!warmup&&p.IsBot.Value&&bots.TryGetValue(p,out var bot))bot.Tick(playing);
    if(p.DisconnectAt>=0&&Time.time-p.DisconnectAt>c.disconnectGrace&&p.Alive){p.ProtectionUntil=0;p.Hurt(999,null);}
    p.Tick(dt,playing);
    if(!playing)continue;
    if(p.Alive&&p.Fire)TryAttack(p);
   }
   foreach(var r in ropes){
    int t=r.a.Team.Value;
    float scale=BattleRules.RopeScale(Time.time<shortenUntil[t],Time.time<extendUntil[t],c);
    float strength=Time.time<elasticUntil?c.elasticScale:1;
    if(r.a.SpeedBoosted||r.b.SpeedBoosted)strength*=c.speedBoostPullMultiplier;
    r.Apply(paths,c.ropeLength*scale,c.ropeSpring*strength,c.ropeDamping,c.ropeMaxForce*strength);
   }
   if(playing){
    TickBullets(dt);TickBombs();TickThrowables(dt);
    TickRescues(dt);
    if(!Training&&!warmup){
     TickControlZones(dt);
     if(BlueScore.Value>=Objectives.winningHeat||RedScore.Value>=Objectives.winningHeat){Finish();return;}
    }
    TickWipes();
    TickPickups();
   }
   if(Time.time>=nextSnapshot){nextSnapshot=Time.time+1f/c.snapshotHz;BroadcastSnapshot();}
  }
  static bool CanHit(NetPlayer target,int sourceTeam,NetPlayer owner)=>target!=null&&target.Alive&&BattleRules.CanHit(sourceTeam,target.Team.Value,target==owner,GameConfig.Current.friendlyFire);
  public bool TryAttack(NetPlayer p){
   if(!IsServer||Phase.Value!=2||p==null||!Players.Contains(p)||!p.Alive||!p.Connected.Value||IsRescuing(p)||p.SelectedSlot.Value!=0||Time.time<p.NextShot)return false;
   var w=GameConfig.Current.Weapon((WeaponKind)p.Weapon.Value);
   // The final projectile retains its gun definition even after the weapon is exhausted.
   if(w==null)return false;
   bool melee=BattleRules.IsMelee(w.kind);
   if(!melee&&p.Ammo.Value<=0){p.GiveDefaultKnife();return false;}
   p.NextShot=Time.time+w.cooldown;p.ProtectionUntil=0;
   if(w.kind==WeaponKind.RocketLauncher){
    LaunchRocket(p,w);
    SpendAmmo(p);
    return true;
   }
   if(melee){
    foreach(var target in Players){
     Vector2 delta=target.Body.position-p.Body.position;
     if(CanHit(target,p.Team.Value,p)&&delta.magnitude<=w.range&&Vector2.Dot(delta.normalized,p.Aim)>.35f&&paths.Visible(p.Body.position,target.Body.position))
      target.Hurt(w.damage,p,p.Aim.normalized*w.knockbackImpulse);
    }
   }
   ulong shotId=++nextShotId;float angle=Mathf.Atan2(p.Aim.y,p.Aim.x)*Mathf.Rad2Deg;
   Vector2 muzzle=WeaponPresentation.Muzzle((int)w.kind,p.Body.position,angle);
   double firedAt=NetworkManager.ServerTime.Time;
   bool launched=!melee&&paths.Visible(p.Body.position,muzzle);
   if(!melee){
    // A long barrel must not skip an enemy standing between the body and muzzle.
    Vector2 muzzleDelta=muzzle-p.Body.position;bool closeHit=false;
    foreach(var hit in Physics2D.RaycastAll(p.Body.position,muzzleDelta.normalized,muzzleDelta.magnitude)){
     if(hit.collider.gameObject.layer==8){launched=false;break;}
     var closeTarget=hit.collider.GetComponent<NetPlayer>();
     if(CanHit(closeTarget,p.Team.Value,p)){closeTarget.Hurt(w.damage,p,p.Aim.normalized*w.knockbackImpulse);HitClientRpc(hit.point,1);launched=false;closeHit=true;break;}
    }
    // Large visual guns must not spawn projectiles beyond a wall at the muzzle.
    if(!launched){if(!closeHit){var wall=Physics2D.Linecast(p.Body.position,muzzle,1<<8);if(wall.collider!=null)HitClientRpc(wall.point,0);}}
    else bullets.Add(new ProjectileState{id=shotId,bornAt=firedAt,weapon=(int)w.kind,position=muzzle,direction=p.Aim,remaining=w.range,speed=w.bulletSpeed,damage=w.damage,owner=p,team=p.Team.Value,ownerSeat=p.Seat.Value,knockbackImpulse=w.knockbackImpulse});
   }
   ShotClientRpc(RoundId.Value,p.NetworkObjectId,shotId,(int)w.kind,p.Team.Value,p.Body.position,muzzle,p.Aim,launched,firedAt);
   if(!melee)SpendAmmo(p);
   return true;
  }
  void SpendAmmo(NetPlayer p){
   if(--p.Ammo.Value!=0)return;
   p.GiveDefaultKnife();
   p.NextShot=Mathf.Max(p.NextShot,Time.time+GameConfig.Current.Weapon(WeaponKind.Dagger).cooldown);
  }
  [ClientRpc]void ShotClientRpc(uint round,ulong actorId,ulong shotId,int weapon,int team,Vector2 position,Vector2 muzzle,Vector2 aim,bool launched,double firedAt){
   if(!CanRender||round!=RoundId.Value||Phase.Value==3)return;
   if(presentedShots.TryGetValue(actorId,out ulong previous)&&shotId<=previous)return;presentedShots[actorId]=shotId;
   NetPlayer actor=null;if(NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(actorId,out var actorObject))actor=actorObject.GetComponent<NetPlayer>();
   var view=actor==null?null:actor.GetComponent<CyberPlayerView>();
   float angle=Mathf.Atan2(aim.y,aim.x)*Mathf.Rad2Deg;
   Vector2 visibleMuzzle=view==null?muzzle:view.Muzzle(weapon,angle);
   bool visibleActor=actor==null?LocalEffectVisible(position):CanSeeFromLocal(actor);
   if(visibleActor){
   if(BattleRules.IsMelee((WeaponKind)weapon)){view?.Slash(angle,weapon==(int)WeaponKind.Greatsword?2.6f:1.1f);GardenFx.Tone(0,.13f,position);}
   else{
    view?.Kick(weapon,angle);var profile=WeaponPresentation.Get(weapon);
    // A blocked muzzle can kick but must not flash through the cover.
    if(launched&&!Physics2D.Linecast(actor==null?position:(Vector2)actor.transform.position,visibleMuzzle,1<<8))GardenFx.Emit(visibleMuzzle,0,3,.08f,profile.flashSize,angle);
    GardenFx.Tone(weapon,.18f,position);
   }
   }
   if(launched){var definition=GameConfig.Current.Weapon((WeaponKind)weapon);projectileVisuals.Spawn(new ProjectileState{id=shotId,bornAt=firedAt,weapon=weapon,team=team,position=muzzle,direction=aim,speed=definition.bulletSpeed,remaining=definition.range},firedAt);}
  }
  [ClientRpc]void ProjectileEndedClientRpc(uint round,ulong id){if(CanRender&&round==RoundId.Value)projectileVisuals.Retire(id);}
  [ClientRpc]void HitClientRpc(Vector2 position,int type){if((Application.isBatchMode&&!Draw2D.RenderInBatch)||!LocalEffectVisible(position))return;GardenFx.Emit(position,type==0?6:9,3,.22f,Vector2.one*.75f);GardenFx.Tone(5,.13f,position);}
  [ClientRpc]void ItemClientRpc(Vector2 position,int kind){if((Application.isBatchMode&&!Draw2D.RenderInBatch)||!LocalEffectVisible(position))return;GardenFx.Emit(position,kind==(int)ItemKind.Dash?18:kind==(int)ItemKind.Shield?20:19,1,.3f,Vector2.one*.8f);GardenFx.Tone(6,.18f,position);}
  [ClientRpc]internal void FeedbackClientRpc(Vector2 position,int tone){
   if(CanRender&&LocalEffectVisible(position))GardenFx.Tone(tone,.75f,position);
  }
  internal void AnnounceKill(NetPlayer killer,NetPlayer victim){
   KillNoticeClientRpc(RoundId.Value,killer.DisplayName.Value.ToString(),killer.Team.Value,victim.DisplayName.Value.ToString(),victim.Team.Value);
  }
  [ClientRpc]void KillNoticeClientRpc(uint round,string killer,int killerTeam,string victim,int victimTeam){
   if(round==RoundId.Value&&Phase.Value==2)GameClient.Instance?.RecordKill(this,killer,killerTeam,victim,victimTeam);
  }
  void TickBullets(float dt){
   for(int i=bullets.Count-1;i>=0;i--){
    var b=bullets[i];float distance=Mathf.Min(b.remaining,b.speed*dt);Vector2 end=b.position+b.direction*distance;
    // One ordered sweep resolves wall and hostile hits, preventing tunneling.
    var hits=Physics2D.RaycastAll(b.position,b.direction,distance);
    bool remove=false;
    foreach(var h in hits){
     if(h.collider.gameObject.layer==8){HitClientRpc(h.point,0);remove=true;break;}
     var p=h.collider.GetComponent<NetPlayer>();
     if(CanHit(p,b.team,b.owner)){p.Hurt(b.damage,b.owner,b.direction.normalized*b.knockbackImpulse);HitClientRpc(h.point,1);remove=true;break;}
    }
    b.position=end;b.remaining-=distance;
    if(remove||b.remaining<=0){ProjectileEndedClientRpc(RoundId.Value,b.id);bullets.RemoveAt(i);}
   }
  }
  void TickPickups(){
   var c=GameConfig.Current;
   var switched=new HashSet<NetPlayer>();
   foreach(var drop in pickups){
    MapContent.TryRespawn(drop,Time.time,RandomItem);
    if(!drop.active)continue;
    foreach(var p in Players.OrderBy(p=>(p.Body.position-drop.position).sqrMagnitude).ThenBy(p=>p.UserId.Value.ToString(),StringComparer.Ordinal)){
     if(Training&&p.TrainingRole!=0)continue;
     if(!p.Alive||!p.Connected.Value||Vector2.Distance(p.Body.position,drop.position)>c.pickupRadius)continue;
     if(drop.weapon>0&&switched.Contains(p))continue;
     if(drop.weapon>0&&!p.Equip((WeaponKind)drop.weapon))continue;
     if(drop.weapon>0)switched.Add(p);
     if(drop.item>0&&!TryCollectItem(p,(ItemKind)drop.item))continue;
    FeedbackClientRpc(p.Body.position,9);MapContent.ScheduleRespawn(drop,Time.time);break;
    }
   }
  }
  ItemKind RandomItem(){
   var pool=GameConfig.Current.items.Where(i=>i.enabled&&i.weight>0&&i.kind!=ItemKind.Mystery&&i.kind!=ItemKind.None).ToArray();
   float total=pool.Sum(i=>i.weight),roll=UnityEngine.Random.value*total;
   foreach(var i in pool){roll-=i.weight;if(roll<=0)return i.kind;}return pool.Length>0?pool[pool.Length-1].kind:ItemKind.None;
  }
  public bool TryCollectItem(NetPlayer p,ItemKind kind){
   if(!IsServer||p==null||!p.Alive||!p.Connected.Value||p.InventoryCount>=3)return false;
   if(kind==ItemKind.Mystery)kind=RandomItem();return p.TryStoreItem(kind);
  }
  public bool ApplyItem(NetPlayer p,ItemKind kind){
   if(!IsServer||p==null||!p.Alive)return false;
   var c=GameConfig.Current;var definition=c.Item(kind);if(definition==null||!definition.enabled)return false;
   float expiry=Time.time+definition.duration;
   switch(kind){
    case ItemKind.Shorten:shortenUntil[1-p.Team.Value]=expiry;break;
    case ItemKind.Extend:extendUntil[p.Team.Value]=expiry;break;
    case ItemKind.Elastic:elasticUntil=expiry;break;
    case ItemKind.Shield:p.Shield.Value=Mathf.Min(c.shieldCap,p.Shield.Value+c.shieldAmount);break;
    case ItemKind.Heal:p.Health.Value=Mathf.Min(p.MaxHealth.Value,p.Health.Value+c.healAmount);break;
    case ItemKind.Dash:p.SpeedBoostUntil=Time.time+c.speedBoostSeconds;break;
    case ItemKind.Smoke:case ItemKind.Incendiary:Throw(p,kind);break;
    case ItemKind.Bomb:bombs.Add(new BombState{position=p.Body.position,team=p.Team.Value,owner=p,explodeAt=Time.time+c.bombFuse});break;
    default:return false;
   }return true;
  }
  public bool TryUseSelected(NetPlayer p){
   if(!IsServer||Phase.Value!=2||p==null||!Players.Contains(p)||!p.Alive||!p.Connected.Value||IsRescuing(p)||p.SelectedSlot.Value==0)return false;
   int item=p.ItemAt(p.SelectedSlot.Value);if(!ApplyItem(p,(ItemKind)item))return false;
   ItemClientRpc(p.Body.position,item);p.ConsumeSelectedItem();return true;
  }
  void TickBombs(){
   var c=GameConfig.Current;
   for(int i=bombs.Count-1;i>=0;i--){
    var bomb=bombs[i];if(Time.time<bomb.explodeAt)continue;
    foreach(var p in Players)if(CanHit(p,bomb.team,bomb.owner)&&Vector2.Distance(p.Body.position,bomb.position)<=c.bombRadius&&paths.Visible(p.Body.position,bomb.position))p.Hurt(c.bombDamage,bomb.owner);
    ExplodeClientRpc(bomb.position,c.bombRadius);bombs.RemoveAt(i);
   }
  }
  [ClientRpc]void ExplodeClientRpc(Vector2 position,float radius){
   if(Application.isBatchMode&&!Draw2D.RenderInBatch)return;
   GardenFx.Emit(position,12,6,.4f,Vector2.one*radius*2);GardenFx.Tone(4,.45f,position);
   if(Camera.main!=null&&Vector2.Distance(Camera.main.transform.position,position)<12)MeadowCamera.Instance?.Impulse(.25f);
  }
  void Finish(){
   resultSent=true;Phase.Value=3;
   float difference=BlueScore.Value-RedScore.Value;
   Winner.Value=Mathf.Abs(difference)<.001f?-1:difference>0?0:1;
   var result=new BattleResult{winner=Winner.Value,duration=Time.time-roundAt,players=Players.Select(p=>new PlayerResult{userId=p.UserId.Value.ToString(),kills=p.Kills.Value,deaths=p.Deaths.Value,assists=p.Assists.Value,damage=p.DamageDone.Value}).ToArray()};
   OnResult?.Invoke(result);
  }
  void BroadcastSnapshot(){
   var snapshot=new WorldSnapshot{round=RoundId.Value,at=NetworkManager.ServerTime.Time,pickups=pickups.ToArray(),bullets=bullets.ToArray(),bombs=bombs.ToArray(),throwables=throwables.ToArray(),ropes=ropes.Select(r=>new RopeState{playerA=r.a.NetworkObjectId,playerB=r.b.NetworkObjectId,team=r.a.Team.Value,seatA=r.a.Seat.Value,seatB=r.b.Seat.Value,tension=r.Tension,points=r.path}).ToArray()};
   string json=JsonUtility.ToJson(snapshot);SnapshotClientRpc(json);
  }
  [ClientRpc]void SnapshotClientRpc(string json){
   var state=JsonUtility.FromJson<WorldSnapshot>(json);
   if(state.round!=RoundId.Value)return;
   receivedThrowables=Phase.Value==2?(state.throwables??Array.Empty<ThrowableState>()):Array.Empty<ThrowableState>();
   if(!CanRender)return;
   if(Phase.Value!=3)projectileVisuals.Reconcile(state.bullets,state.at);
   renderedRopes=state.ropes;
   SyncIcons(pickupViews,state.pickups.Length,"Supply",.65f,3);
   SyncIcons(pickupMarkers,state.pickups.Length,"Supply tier",1.45f,2);
   for(int i=0;i<state.pickups.Length;i++){
    var p=state.pickups[i];var marker=pickupMarkers[i];marker.enabled=p.supplyTier>0;marker.transform.position=p.position;
    var tint=SupplyPools.ColorOf((SupplyTier)p.supplyTier);tint.a=p.active?.65f:.22f;marker.color=tint;
    var view=pickupViews[i];view.enabled=p.active;view.transform.position=p.position;
    view.color=Color.white;CyberArt.Set(view,CyberArt.Pickup(p.weapon,p.item),p.weapon>0?new Vector2(1.2f,.65f):Vector2.one*.8f,true);
   }
   SyncIcons(bombViews,state.bombs.Length,"Bomb",.55f,7);
   for(int i=0;i<state.bombs.Length;i++){bombViews[i].transform.position=state.bombs[i].position;CyberArt.Set(bombViews[i],"item5",Vector2.one*.7f,true);bombViews[i].color=Color.Lerp(Color.white,Draw2D.Red,Mathf.PingPong(Time.time*4,1)*.4f);}
   if(IsServer)return; // Host training already renders authoritative rope lines.
   while(ropeViews.Count<state.ropes.Length)ropeViews.Add(new RopeLineView(transform));
   for(int i=state.ropes.Length;i<ropeViews.Count;i++)ropeViews[i].Show(null,0,0);
  }
  void SyncIcons(List<SpriteRenderer> views,int count,string label,float size,int order){
   while(views.Count<count)views.Add(Draw2D.Shape(transform,label,Vector2.zero,Vector2.one*size,Color.white,true,order));
   for(int i=0;i<views.Count;i++)views[i].gameObject.SetActive(i<count);
  }
  void LateUpdate(){
   if(!CanRender)return;
   RenderThrowables();
   RenderControlZones();
   if(IsServer){foreach(var rope in ropes){var points=paths.Deform(rope.path,rope.a.transform.position,rope.b.transform.position);rope.Show(RopeVisible(points,rope.a.Team.Value)?points:null);}return;}
   if(renderedRopes==null)return;
   for(int i=0;i<renderedRopes.Length&&i<ropeViews.Count;i++){
    var rope=renderedRopes[i];var view=ropeViews[i];
    if(rope.points==null||rope.points.Length<2||!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(rope.playerA,out var a)||!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(rope.playerB,out var b)){view.Show(null,rope.team,0);continue;}
    var visiblePath=paths.Deform(rope.points,a.transform.position,b.transform.position);
    view.Show(RopeVisible(visiblePath,rope.team)?visiblePath:null,rope.team,rope.tension);
   }
  }
 }
}
