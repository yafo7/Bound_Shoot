#if BOUND_SHOOT_QA
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace BoundShoot {
 // Compiled only by BuildQaClient. Drives the normal UI; never writes server state.
 public sealed class QaClientDriver:MonoBehaviour {
  [Serializable] class Plan {public string username,work,mapId="Depot_01";public int index,humans=1,size=4;}
  [Serializable] class Report {public string stage,username,matchId,error;public int players,owned,bots,botDamage,games,rounds,kills,deaths;public float duration;public bool passed;}
  public static QaClientDriver Active;public bool Combat {get;private set;}
  Plan plan;GameClient client;bool failed;float deadline;Report report=new Report();
  string ReportPath=>Path.Combine(plan.work,"client-"+plan.index+".report.json");
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Launch(){if(!string.IsNullOrEmpty(GameClient.GetArg("-qaPlan")))new GameObject("QA").AddComponent<QaClientDriver>();}
  IEnumerator Start(){
   plan=JsonUtility.FromJson<Plan>(File.ReadAllText(GameClient.GetArg("-qaPlan")));
   Active=this;deadline=Time.realtimeSinceStartup+480;Application.logMessageReceived+=OnLog;
   yield return Until(()=>GameClient.Instance!=null,20,"title");
   client=GameClient.Instance;
   Click("进入游戏");yield return null;
   if(client.Screen!="Auth"||FindObjectsOfType<InputField>().Length!=1)throw new Exception("Startup did not show name-only entry");
   yield return Capture("name-entry");
   Field("输入名字").text=plan.username;Click("确认进入");
   yield return Until(()=>client.Screen=="Menu",10,"mode-selection");client.SelectMode("Online");
   yield return Until(()=>client.Screen=="Lobby",25,"lobby");
   yield return Capture("lobby");
   string roomFile=Path.Combine(plan.work,"room-code.txt");
   if(plan.index==0){
    Click("创建房间");yield return Until(()=>client.Screen=="Room",15,"created");
    Click(plan.size+"v"+plan.size);yield return Until(()=>client.Room.size==plan.size&&FindObjectsOfType<Button>().Any(b=>b.GetComponentInChildren<Text>().text=="选择地图"&&b.interactable),15,"size-synced");
    Click("选择地图");Click(GameConfig.Current.Map(plan.mapId).name);Click("选用地图");
    yield return Until(()=>client.Room.mapId==plan.mapId,15,"map-synced");
    File.WriteAllText(roomFile+".tmp",client.Room.code);File.Move(roomFile+".tmp",roomFile);
   }else{
    yield return Until(()=>File.Exists(roomFile),40,"wait-room-code");
    Field("6位数字").text=File.ReadAllText(roomFile);Click("按房间号加入");
   }
   yield return Until(()=>client.Screen=="Room",25,"joined");
   if(client.User.username!=plan.username)throw new Exception("A window inherited another account");
   yield return Until(()=>client.Room.members.Count(m=>m.isBot==0)==plan.humans,40,"shared-room");
   if(client.Room.size!=plan.size||client.Room.mapId!=plan.mapId||client.Room.members.Length!=plan.size*2||client.Room.members.Count(m=>m.isBot==1)!=plan.size*2-plan.humans)throw new Exception("Incorrect room/map/AI fill");
   yield return Capture("room");
   string previousMatch="";
   for(int round=1;round<=2;round++){
    Combat=false;
    yield return Until(()=>client.Screen=="Room"&&client.Room.status=="Waiting",25,"room-"+round);
    File.WriteAllText(Path.Combine(plan.work,"returned-"+round+"-"+plan.index),"ready");
    yield return Until(()=>Enumerable.Range(0,plan.humans).All(i=>File.Exists(Path.Combine(plan.work,"returned-"+round+"-"+i))),40,"all-in-room-"+round);
    Click("准备");
    yield return Until(()=>client.Screen=="Battle"&&BattleWorld.Instance!=null&&BattleWorld.Instance.IsOnlineWarmup&&BattleWorld.Instance.Phase.Value==2,100,"warmup-"+round);
    yield return Capture("warmup-"+round);
    File.WriteAllText(Path.Combine(plan.work,"warmup-"+round+"-"+plan.index),"ready");
    if(plan.index==0){
     yield return Until(()=>Enumerable.Range(0,plan.humans).All(i=>File.Exists(Path.Combine(plan.work,"warmup-"+round+"-"+i)))&&FindObjectsOfType<Button>().Any(b=>b.GetComponentInChildren<Text>().text=="开始正式对战"&&b.interactable),30,"warmup-ready-"+round);
     Click("开始正式对战");
    }
    yield return Until(()=>BattleWorld.Instance!=null&&!BattleWorld.Instance.IsOnlineWarmup&&client.Screen=="Battle"&&NetPlayer.All.Count==plan.size*2&&NetPlayer.All.Count(p=>p.IsOwner)==1,30,"battle-"+round);
    Combat=true;
    if(BattleWorld.Instance.MapKey.Value.ToString()!=plan.mapId)throw new Exception("Battle map differs from lobby selection");
    if(client.ActiveMatch==previousMatch)throw new Exception("Rematch reused the previous match");
    previousMatch=client.ActiveMatch;report.matchId=previousMatch;report.players=NetPlayer.All.Count;report.owned=NetPlayer.All.Count(p=>p.IsOwner);report.bots=NetPlayer.All.Count(p=>p.IsBot.Value);Save("synced-"+round);
    if(report.bots!=plan.size*2-plan.humans)throw new Exception("Battle AI roster does not match room");
    yield return new WaitForSecondsRealtime(1);
    yield return Capture("battle-"+round);
    yield return Until(()=>client.Screen=="Result",220,"result-"+round);
    ResultData result=null;string error=null;
    yield return client.Api.Request<ResultData>("GET","/api/matches/"+previousMatch+"/result",null,v=>result=v,e=>error=e);
    if(result==null||result.status!="Finished"||result.result.players.Length!=plan.size*2||result.mapId!=plan.mapId)throw new Exception(error??"Match did not finish normally");
    report.rounds=round;report.duration=result.result.duration;report.kills=result.result.players.Sum(p=>p.kills);report.deaths=result.result.players.Sum(p=>p.deaths);
    var botIds=client.Room.members.Where(m=>m.isBot==1).Select(m=>m.userId).ToArray();
    report.botDamage+=result.result.players.Where(p=>botIds.Contains(p.userId)).Sum(p=>p.damage);
    yield return Capture("result-"+round);
    Click("返回房间");yield return Until(()=>client.Screen=="Room"&&client.Room.status=="Waiting",20,"returned-"+round);
   }
   UserProfile profile=null;
   yield return client.Api.Request<UserProfile>("GET","/api/users/me",null,v=>profile=v,e=>{});
   if(profile==null||profile.games!=2)throw new Exception("Result was not persisted exactly twice");
   Click("返回大厅");yield return Until(()=>client.Screen=="Lobby"&&client.Room==null,15,"left-room");
   Click("返回主菜单");yield return Until(()=>client.Screen=="Menu"&&client.User==null,15,"logout");
   report.games=profile.games;report.passed=true;Save("passed");Application.Quit(0);
  }
  IEnumerator Until(Func<bool> condition,float seconds,string stage){
   Save(stage);float until=Time.realtimeSinceStartup+seconds;
   while(!condition()){
    if(failed)yield break;
    if(Time.realtimeSinceStartup>until){
     string notice=client==null?"":(string)typeof(GameClient).GetField("message",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(client);
     Fail("Timeout: "+stage+"; screen="+client?.Screen+"; notice="+notice+"; endpoint="+client?.Api?.BaseUrl);yield break;
    }
    yield return null;
   }
  }
  IEnumerator Capture(string label){
   if(plan.index!=0)yield break;
   yield return new WaitForEndOfFrame();
   var camera=Camera.main;var canvas=FindObjectOfType<Canvas>();
   if(camera==null||canvas==null)throw new Exception("Missing camera/canvas for UI review");
   var mode=canvas.renderMode;var previous=RenderTexture.active;var rt=new RenderTexture(1600,900,24);
   camera.targetTexture=rt;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=5;
   Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;
   var png=new Texture2D(1600,900,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,1600,900),0,0);png.Apply();
   File.WriteAllBytes(Path.Combine(plan.work,label+".png"),png.EncodeToPNG());
   camera.targetTexture=null;canvas.renderMode=mode;RenderTexture.active=previous;Destroy(rt);Destroy(png);
   yield return null;
  }
  static InputField Field(string hint)=>FindObjectsOfType<InputField>().First(f=>((Text)f.placeholder).text==hint);
  static void Click(string label){var b=FindObjectsOfType<Button>().First(b=>b.GetComponentInChildren<Text>().text==label);if(!b.interactable)throw new Exception("Disabled button: "+label);b.onClick.Invoke();}
  void Save(string stage){if(failed&&stage!="failed")return;report.stage=stage;report.username=plan.username;File.WriteAllText(ReportPath,JsonUtility.ToJson(report,true));}
  void Fail(string error){if(failed)return;failed=true;report.error=error;Save("failed");Application.Quit(7);}
  void OnLog(string text,string trace,LogType type){if(type==LogType.Exception||type==LogType.Error)Fail(text);}
  void Update(){if(plan!=null&&!failed&&Time.realtimeSinceStartup>deadline)Fail("Scenario deadline");}
  void OnDestroy(){Application.logMessageReceived-=OnLog;if(Active==this)Active=null;}
  public void Input(NetPlayer player,out Vector2 movement,out Vector2 aim,out bool fire){
   movement=Vector2.zero;aim=player.Team.Value==0?Vector2.right:Vector2.left;fire=false;
   if(!Combat)return;
   Vector2 pos=player.Position.Value;bool blue=player.Team.Value==0;
   Vector2 target=player.Weapon.Value==0?new Vector2(blue?-9:9,0):Mathf.Abs(pos.x)>6?new Vector2(blue?-6:6,0):new Vector2(blue?-1:1,player.Seat.Value*.9f);
   movement=Vector2.ClampMagnitude(target-pos,1);
   var enemy=NetPlayer.All.FirstOrDefault(p=>p.Team.Value!=player.Team.Value&&p.Alive);
   if(enemy!=null)aim=enemy.Position.Value-pos;
   fire=player.Weapon.Value!=0;
  }
 }
}
#endif
