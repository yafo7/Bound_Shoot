using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.Collections;
using UnityEngine;

namespace BoundShoot {
 [Serializable] public class PickupState {public Vector2 position;public int weapon,item;public bool active;public float respawnAt;}
 [Serializable] public class ProjectileState {public Vector2 position,direction;public float remaining,speed;public int damage,ownerSeat,team;[NonSerialized]public NetPlayer owner;}
 [Serializable] public class BombState {public Vector2 position;public float explodeAt;public int team;[NonSerialized]public NetPlayer owner;}
 [Serializable] public class RopeState {public int team,seatA,seatB;public Vector2[] points;}
 [Serializable] public class WorldSnapshot {public PickupState[] pickups;public ProjectileState[] bullets;public BombState[] bombs;public RopeState[] ropes;}
 public class BattleWorld:NetworkBehaviour {
  public static BattleWorld Instance;
  public string SelectedMap="Arena_01";
  public NetworkVariable<FixedString64Bytes> MapKey=new NetworkVariable<FixedString64Bytes>(new FixedString64Bytes("Arena_01"));
  public NetworkVariable<int> Phase=new NetworkVariable<int>(),Winner=new NetworkVariable<int>(-1);
  public NetworkVariable<float> Clock=new NetworkVariable<float>();
  public ServerRoster Roster;public readonly List<NetPlayer> Players=new List<NetPlayer>();
  readonly List<RopeLink> ropes=new List<RopeLink>();
  readonly List<PickupState> pickups=new List<PickupState>();
  readonly List<ProjectileState> bullets=new List<ProjectileState>();
  readonly List<BombState> bombs=new List<BombState>();
  readonly List<SpriteRenderer> pickupViews=new List<SpriteRenderer>(),bulletViews=new List<SpriteRenderer>(),bombViews=new List<SpriteRenderer>();
  readonly List<LineRenderer> ropeViews=new List<LineRenderer>();
  RopeState[] renderedRopes;
  RopePath paths;MapDefinition map;float startAt,roundAt,nextSnapshot;
  float[] shortenUntil=new float[2],extendUntil=new float[2];float elasticUntil;
  bool resultSent;float[] disconnectedProtection;
  public event Action<BattleResult> OnResult;
  public override void OnNetworkSpawn(){
   Instance=this;if(IsServer)MapKey.Value=SelectedMap;map=GameConfig.Current.Map(MapKey.Value.ToString());
   new GameObject("Arena").AddComponent<ArenaView>().transform.SetParent(transform,false);
   GetComponentInChildren<ArenaView>().Build(map);paths=new RopePath(map);
  }
  public override void OnNetworkDespawn(){if(Instance==this)Instance=null;}
  public void Setup(ServerRoster roster){
   Roster=roster;map=GameConfig.Current.Map(roster.mapId);
   foreach(var m in roster.players){
    var p=Instantiate(Resources.Load<GameObject>("NetPlayer")).GetComponent<NetPlayer>();
    p.NetworkObject.Spawn();p.NetworkObject.DontDestroyWithOwner=true;
    Players.Add(p);p.Initialize(m,(m.team==0?map.blueSpawns:map.redSpawns)[m.seat]);p.DisconnectAt=-1;
   }
   for(int team=0;team<2;team++){
    var members=Players.Where(p=>p.Team.Value==team).OrderBy(p=>p.Seat.Value).ToArray();
    for(int i=1;i<members.Length;i++)ropes.Add(new RopeLink(members[i-1],members[i],transform));
   }
   for(int i=0;i<map.supplies.Length;i++){
    // Both approach lanes guarantee all four weapon choices before the first respawn.
    int local=i<5?i:i<10?i-5:4;
    pickups.Add(new PickupState{position=map.supplies[i],weapon=local<4?local+1:0,item=local<4?0:(int)ItemKind.Mystery,active=true});
   }
   Clock.Value=GameConfig.Current.roundSeconds;startAt=Time.time;
  }
  public NetPlayer Find(string uid)=>Players.Find(p=>p.UserId.Value.ToString()==uid);
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
   if(Phase.Value==0){
    if(Players.All(p=>p.Connected.Value)){Phase.Value=1;roundAt=Time.time+c.countdownSeconds;}
    else if(Time.time-startAt>60){ServerRuntime.Instance?.Abort("玩家连接超时");resultSent=true;}
   }
   if(Phase.Value==1){Clock.Value=Mathf.Max(0,roundAt-Time.time);if(Time.time>=roundAt){Phase.Value=2;roundAt=Time.time;}}
   bool playing=Phase.Value==2;
   if(playing)Clock.Value=Mathf.Max(0,c.roundSeconds-(Time.time-roundAt));
   foreach(var p in Players){
    if(p.DisconnectAt>=0&&Time.time-p.DisconnectAt>c.disconnectGrace&&p.Alive){p.ProtectionUntil=0;p.Hurt(999,null);}
    p.Tick(dt,playing);
    if(!playing)continue;
    if(p.Alive){if(p.Fire)Attack(p);if(p.Use){p.Use=false;UseItem(p);}}
    else if(p.Connected.Value)RevivePlayer(p,dt);
   }
   foreach(var r in ropes){
    int t=r.a.Team.Value;
    float scale=BattleRules.RopeScale(Time.time<shortenUntil[t],Time.time<extendUntil[t],c);
    r.Apply(paths,c.ropeLength*scale,c.ropeSpring*(Time.time<elasticUntil?c.elasticScale:1),c.ropeDamping,c.ropeMaxForce*(Time.time<elasticUntil?c.elasticScale:1));
    r.Show(r.path);
   }
   if(playing){
    TickPickups();TickBullets(dt);TickBombs();
    int ba=Players.Count(p=>p.Team.Value==0&&p.Alive),ra=Players.Count(p=>p.Team.Value==1&&p.Alive);
    if(ba==0||ra==0||Clock.Value<=0)Finish();
   }
   if(Time.time>=nextSnapshot){nextSnapshot=Time.time+1f/c.snapshotHz;BroadcastSnapshot();}
  }
  void Attack(NetPlayer p){
   if(Time.time<p.NextShot)return;
   var w=GameConfig.Current.Weapon((WeaponKind)p.Weapon.Value);
   // Empty hands have no combat attack: weapons must be found in the supply area.
   if(w==null)return;p.NextShot=Time.time+w.cooldown;
   if(w.kind==WeaponKind.Dagger){
    foreach(var target in Players){
     Vector2 delta=target.Body.position-p.Body.position;
     if(target.Team.Value!=p.Team.Value&&target.Alive&&delta.magnitude<=w.range&&Vector2.Dot(delta.normalized,p.Aim)>.35f&&paths.Visible(p.Body.position,target.Body.position))
      target.Hurt(w.damage,p);
    }
   }else bullets.Add(new ProjectileState{position=p.Body.position+p.Aim*.55f,direction=p.Aim,remaining=w.range,speed=w.bulletSpeed,damage=w.damage,owner=p,team=p.Team.Value,ownerSeat=p.Seat.Value});
   ShotClientRpc(p.Body.position,p.Aim,p.Team.Value);
  }
  [ClientRpc]void ShotClientRpc(Vector2 p,Vector2 aim,int team){
   if(Application.isBatchMode)return;
   var flash=Draw2D.Shape(transform,"Muzzle flash",p+aim*.7f,Vector2.one*.3f,Draw2D.Gold,true,15);
   Destroy(flash.gameObject,.07f);
  }
  void TickBullets(float dt){
   for(int i=bullets.Count-1;i>=0;i--){
    var b=bullets[i];float distance=Mathf.Min(b.remaining,b.speed*dt);Vector2 end=b.position+b.direction*distance;
    // One ordered sweep resolves wall and hostile hits, preventing tunneling.
    var hits=Physics2D.RaycastAll(b.position,b.direction,distance);
    bool remove=false;
    foreach(var h in hits){
     if(h.collider.gameObject.layer==8){remove=true;break;}
     var p=h.collider.GetComponent<NetPlayer>();
     if(p!=null&&p.Alive&&p.Team.Value!=b.team){p.Hurt(b.damage,b.owner);remove=true;break;}
    }
    b.position=end;b.remaining-=distance;
    if(remove||b.remaining<=0)bullets.RemoveAt(i);
   }
  }
  void TickPickups(){
   var c=GameConfig.Current;
   foreach(var drop in pickups){
    if(!drop.active){
     if(Time.time<drop.respawnAt)continue;
     drop.active=true;
     if(UnityEngine.Random.value<.55f){drop.weapon=UnityEngine.Random.Range(1,5);drop.item=0;}
     else{drop.weapon=0;drop.item=(int)(UnityEngine.Random.value<.4f?ItemKind.Mystery:RandomItem());}
    }
    foreach(var p in Players){
     if(!p.Alive||!p.Connected.Value||Vector2.Distance(p.Body.position,drop.position)>c.pickupRadius)continue;
     if(drop.weapon>0&&!p.Equip((WeaponKind)drop.weapon))continue;
     if(drop.item>0)ApplyItem(p,(ItemKind)drop.item);
     drop.active=false;drop.respawnAt=Time.time+c.pickupRespawn;break;
    }
   }
  }
  ItemKind RandomItem(){
   var pool=GameConfig.Current.items.Where(i=>i.enabled&&i.weight>0&&i.kind!=ItemKind.Mystery&&i.kind!=ItemKind.None).ToArray();
   float total=pool.Sum(i=>i.weight),roll=UnityEngine.Random.value*total;
   foreach(var i in pool){roll-=i.weight;if(roll<=0)return i.kind;}return pool.Length>0?pool[pool.Length-1].kind:ItemKind.None;
  }
  public void ApplyItem(NetPlayer p,ItemKind kind){
   var c=GameConfig.Current;if(kind==ItemKind.Mystery)kind=RandomItem();
   var definition=c.Item(kind);if(definition==null||!definition.enabled)return;
   float expiry=Time.time+definition.duration;
   switch(kind){
    case ItemKind.Shorten:shortenUntil[1-p.Team.Value]=expiry;break;
    case ItemKind.Extend:extendUntil[p.Team.Value]=expiry;break;
    case ItemKind.Elastic:elasticUntil=expiry;break;
    case ItemKind.Shield:p.Shield.Value=Mathf.Min(c.shieldCap,p.Shield.Value+1);break;
    case ItemKind.Heal:p.Health.Value=Mathf.Min(p.MaxHealth.Value,p.Health.Value+1);break;
    case ItemKind.Dash:case ItemKind.Bomb:p.Item.Value=(int)kind;break;
   }
  }
  void UseItem(NetPlayer p){
   var c=GameConfig.Current;
   switch((ItemKind)p.Item.Value){
    case ItemKind.Dash:p.DashDirection=p.Move.sqrMagnitude>.01f?p.Move.normalized:p.Aim;p.DashUntil=Time.time+c.dashSeconds;break;
    case ItemKind.Bomb:bombs.Add(new BombState{position=p.Body.position,team=p.Team.Value,owner=p,explodeAt=Time.time+c.bombFuse});break;
   }p.Item.Value=0;
  }
  void TickBombs(){
   var c=GameConfig.Current;
   for(int i=bombs.Count-1;i>=0;i--){
    var bomb=bombs[i];if(Time.time<bomb.explodeAt)continue;
    foreach(var p in Players)if(p.Alive&&p.Team.Value!=bomb.team&&Vector2.Distance(p.Body.position,bomb.position)<=c.bombRadius&&paths.Visible(p.Body.position,bomb.position))p.Hurt(c.bombDamage,bomb.owner);
    ExplodeClientRpc(bomb.position,c.bombRadius);bombs.RemoveAt(i);
   }
  }
  [ClientRpc]void ExplodeClientRpc(Vector2 position,float radius){
   if(Application.isBatchMode)return;
   var r=Draw2D.Shape(transform,"Explosion",position,Vector2.one*radius*2,new Color(1,.5f,.2f,.5f),true,14);Destroy(r.gameObject,.2f);
  }
  void RevivePlayer(NetPlayer p,float dt){
   bool inside=map.revivals.Any(v=>Mathf.Abs(p.Body.position.x-v.x)<2&&Mathf.Abs(p.Body.position.y-v.y)<1.5f);
   p.Revive.Value=BattleRules.ReviveProgress(p.Revive.Value,inside,dt);
   if(p.Revive.Value>=GameConfig.Current.reviveSeconds)p.Resurrect();
  }
  void Finish(){
   resultSent=true;Phase.Value=3;
   Winner.Value=BattleRules.Winner(Players.Count(p=>p.Team.Value==0&&p.Alive),Players.Count(p=>p.Team.Value==1&&p.Alive),Players.Where(p=>p.Team.Value==0&&p.Alive).Sum(p=>p.Health.Value),Players.Where(p=>p.Team.Value==1&&p.Alive).Sum(p=>p.Health.Value));
   var result=new BattleResult{winner=Winner.Value,duration=Time.time-roundAt,players=Players.Select(p=>new PlayerResult{userId=p.UserId.Value.ToString(),kills=p.Kills.Value,deaths=p.Deaths.Value,damage=p.DamageDone.Value}).ToArray()};
   OnResult?.Invoke(result);
  }
  void BroadcastSnapshot(){
   var snapshot=new WorldSnapshot{pickups=pickups.ToArray(),bullets=bullets.ToArray(),bombs=bombs.ToArray(),ropes=ropes.Select(r=>new RopeState{team=r.a.Team.Value,seatA=r.a.Seat.Value,seatB=r.b.Seat.Value,points=r.path}).ToArray()};
   string json=JsonUtility.ToJson(snapshot);SnapshotClientRpc(json);
  }
  [ClientRpc]void SnapshotClientRpc(string json){
   if(IsServer)return;
   var state=JsonUtility.FromJson<WorldSnapshot>(json);
   renderedRopes=state.ropes;
   SyncIcons(pickupViews,state.pickups.Length,"Supply",.65f,3);
   for(int i=0;i<state.pickups.Length;i++){
    var p=state.pickups[i];var view=pickupViews[i];view.enabled=p.active;view.transform.position=p.position;
    view.color=p.weapon>0?Draw2D.Gold:new Color(.67f,.47f,1);
    var label=view.GetComponentInChildren<TextMesh>();if(label==null){
     var go=new GameObject("Label");go.transform.SetParent(view.transform,false);go.transform.localPosition=new Vector3(0,0,-.01f);
     label=go.AddComponent<TextMesh>();label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.fontSize=40;label.characterSize=.13f;label.anchor=TextAnchor.MiddleCenter;
     label.GetComponent<MeshRenderer>().sharedMaterial=label.font.material;label.GetComponent<MeshRenderer>().sortingOrder=6;
    }label.gameObject.SetActive(p.active);label.text=p.weapon>0?new[]{"","S","P","A","K"}[p.weapon]:p.item==(int)ItemKind.Mystery?"?":new[]{"","↓","↑","+","D","B","!","H","?"}[p.item];
   }
   SyncIcons(bulletViews,state.bullets.Length,"Bullet",.16f,11);
   for(int i=0;i<state.bullets.Length;i++){bulletViews[i].transform.position=state.bullets[i].position;bulletViews[i].color=state.bullets[i].team==0?Draw2D.Blue:Draw2D.Red;}
   SyncIcons(bombViews,state.bombs.Length,"Bomb",.55f,7);
   for(int i=0;i<state.bombs.Length;i++){bombViews[i].transform.position=state.bombs[i].position;bombViews[i].color=Color.Lerp(Draw2D.Gold,Draw2D.Red,Mathf.PingPong(Time.time*4,1));}
   while(ropeViews.Count<state.ropes.Length)ropeViews.Add(Draw2D.Line(transform,Color.white,.095f));
   for(int i=0;i<state.ropes.Length;i++){
    var r=state.ropes[i];var line=ropeViews[i];line.startColor=line.endColor=r.team==0?Draw2D.Blue:Draw2D.Red;
    if(r.points==null)continue;line.positionCount=r.points.Length;for(int j=0;j<r.points.Length;j++)line.SetPosition(j,r.points[j]);
   }
  }
  void SyncIcons(List<SpriteRenderer> views,int count,string label,float size,int order){
   while(views.Count<count)views.Add(Draw2D.Shape(transform,label,Vector2.zero,Vector2.one*size,Color.white,true,order));
   for(int i=0;i<views.Count;i++)views[i].gameObject.SetActive(i<count);
  }
  void LateUpdate(){
   if(IsServer||renderedRopes==null)return;
   for(int i=0;i<renderedRopes.Length&&i<ropeViews.Count;i++){
    var rope=renderedRopes[i];var line=ropeViews[i];if(line.positionCount<2)continue;
    var a=NetPlayer.All.Find(p=>p.Team.Value==rope.team&&p.Seat.Value==rope.seatA);
    var b=NetPlayer.All.Find(p=>p.Team.Value==rope.team&&p.Seat.Value==rope.seatB);
    if(a!=null)line.SetPosition(0,a.transform.position);if(b!=null)line.SetPosition(line.positionCount-1,b.transform.position);
   }
  }
 }
}
