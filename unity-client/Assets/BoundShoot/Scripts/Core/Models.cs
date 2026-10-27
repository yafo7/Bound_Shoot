using System;
using UnityEngine;

namespace BoundShoot {
 [Serializable] public class ApiError {public string code,message;}
 [Serializable] public class Envelope<T> {public bool ok; public T data; public ApiError error;}
 [Serializable] public class Empty {}
 [Serializable] public class UserProfile {public string id,username,displayName,roomId;public int skin,games,wins,kills,deaths;}
 [Serializable] public class AuthData {public string accessToken,refreshToken;public UserProfile user;}
 [Serializable] public class Credentials {public string username,password;}
 [Serializable] public class RefreshBody {public string refreshToken;}
 [Serializable] public class SkinBody {public int skin;}
 [Serializable] public class JoinBody {public string code;}
 [Serializable] public class SeatBody {public int team,seat;}
 [Serializable] public class ReadyBody {public bool ready;}
 [Serializable] public class RoomConfigBody {public int size;public string mapId;}
 [Serializable] public class RoomMember {public string userId,username;public int skin,team,seat,ready;}
 [Serializable] public class RoomData {public string id,code,hostId,mapId,status,matchId;public int size;public RoomMember[] members;}
 [Serializable] public class MatchData {public string id,roomId,mapId,status,host;public int port;public RoomMember[] players;}
 [Serializable] public class TicketData {public string matchId,ticket,host;public int port;}
 [Serializable] public class LaunchConfig {public string matchId,secret,backendUrl;public int port;}
 [Serializable] public class ServerRoster {public string matchId,mapId;public int port,reconnectSeconds;public RoomMember[] players;}
 [Serializable] public class TicketBody {public string ticket;}
 [Serializable] public class HeartbeatBody {public bool playing;}
 [Serializable] public class PlayerResult {public string userId;public int kills,deaths,damage;}
 [Serializable] public class BattleResult {public int winner;public float duration;public PlayerResult[] players;}
 [Serializable] public class ResultData {public string status,mapId;public BattleResult result;}
 [Serializable] public class HistoryData {public HistoryRow[] matches;}
 [Serializable] public class HistoryRow {public string id,mapId,status;public int winner,team,kills,deaths,damage;public float duration;}
 [Serializable] public class RealtimeEvent {public string @event;public RoomData room;}
 public enum WeaponKind {None,Sniper,Pistol,AK,Dagger}
 public enum ItemKind {None,Shorten,Extend,Shield,Dash,Bomb,Elastic,Heal,Mystery}
 [Serializable] public class WeaponDefinition {
  public string name;public WeaponKind kind;public int maxHealth,damage;public float range,cooldown,bulletSpeed;
 }
 [Serializable] public class ItemDefinition {
  public string name;public ItemKind kind;public bool enabled=true;public float weight=1,duration=10;
 }
 [Serializable] public class BlockDefinition {public Vector2 center,size;}
 [Serializable] public class MapDefinition {
  public string id,name;public bool available;public float halfWidth,halfHeight;
  public Vector2[] blueSpawns,redSpawns,supplies,revivals;public BlockDefinition[] blocks;
 }
 [Serializable] public class GameConfig {
  public int baseHealth=3,roundSeconds=180,shieldCap=1,bombDamage=2;
  public float moveSpeed=5,acceleration=14,ropeLength=5,ropeSpring=45,ropeDamping=6,ropeMaxForce=100,
   shortenScale=.65f,extendScale=1.5f,elasticScale=3,buffSeconds=10,reviveSeconds=5,
   protectionSeconds=2,bombFuse=3,bombRadius=2.5f,dashSpeed=14,dashSeconds=.18f,
   pickupRadius=.75f,pickupRespawn=10,inputTimeout=.3f,disconnectGrace=20;
  public int countdownSeconds=3,physicsHz=30,snapshotHz=15;
  public WeaponDefinition[] weapons;public ItemDefinition[] items;public MapDefinition[] maps;
  public WeaponDefinition Weapon(WeaponKind k){return Array.Find(weapons,w=>w.kind==k);}
  public MapDefinition Map(string id){return Array.Find(maps,m=>m.id==id&&m.available);}
  public ItemDefinition Item(ItemKind k){return Array.Find(items,i=>i.kind==k);}
  static GameConfig current;
  public static GameConfig Current {get {if(current==null)current=JsonUtility.FromJson<GameConfig>(Resources.Load<TextAsset>("GameConfig").text);return current;}}
 }
 public static class BattleRules {
  public static void Damage(ref int health,ref int shield,int amount){
   int absorbed=Math.Min(shield,Math.Max(0,amount));shield-=absorbed;health=Math.Max(0,health-Math.Max(0,amount-absorbed));
  }
  public static bool CanEquip(bool alive,WeaponKind current){return alive&&current==WeaponKind.None;}
  public static int Winner(int blueAlive,int redAlive,int blueHealth,int redHealth){
   if(blueAlive==0&&redAlive==0)return -1;
   if(blueAlive==0)return 1;if(redAlive==0)return 0;
   if(blueAlive!=redAlive)return blueAlive>redAlive?0:1;
   if(blueHealth!=redHealth)return blueHealth>redHealth?0:1;return -1;
  }
  public static float RopeScale(bool shortened,bool extended,GameConfig c){return(shortened?c.shortenScale:1)*(extended?c.extendScale:1);}
  public static float ReviveProgress(float old,bool inside,float dt){return inside?old+dt:0;}
 }
}
