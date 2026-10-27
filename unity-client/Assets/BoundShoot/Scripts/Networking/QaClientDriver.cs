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
  [Serializable] class Plan {public string username,password,work;public int index;}
  [Serializable] class Report {public string stage,username,matchId,error;public int players,owned,games,rounds,kills,deaths;public float duration;public bool passed;}
  public static QaClientDriver Active;public bool Combat {get;private set;}
  Plan plan;GameClient client;bool failed;float deadline;Report report=new Report();
  string ReportPath=>Path.Combine(plan.work,"client-"+plan.index+".report.json");
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Launch(){if(!string.IsNullOrEmpty(GameClient.GetArg("-qaPlan")))new GameObject("QA").AddComponent<QaClientDriver>();}
  IEnumerator Start(){
   plan=JsonUtility.FromJson<Plan>(File.ReadAllText(GameClient.GetArg("-qaPlan")));
   Active=this;deadline=Time.realtimeSinceStartup+360;Application.logMessageReceived+=OnLog;
   yield return Until(()=>GameClient.Instance!=null&&FindObjectsOfType<InputField>().Length==2,20,"initial-login");
   client=GameClient.Instance;
   if(client.Screen!="Auth"||FindObjectsOfType<InputField>().Length!=2)throw new Exception("Startup did not show direct login");
   yield return Capture("login");
   Click("注册账号");Field("用户名").text=plan.username;Field("密码").text=plan.password;Field("确认密码").text=plan.password;Click("注册");
   yield return Until(()=>client.Screen=="Lobby",25,"registered");
   Click("退出账号");yield return Until(()=>client.Screen=="Auth",20,"signed-out");
   Field("用户名").text=plan.username;Field("密码").text=plan.password;Click("登录");
   yield return Until(()=>client.Screen=="Lobby",25,"signed-in");
   if(client.User.username!=plan.username)throw new Exception("A window inherited another account");
   if(plan.index==0){Click("创建房间");yield return Until(()=>client.Room!=null,20,"room-created");File.WriteAllText(Path.Combine(plan.work,"room.txt"),client.Room.code);}
   else{
    string roomFile=Path.Combine(plan.work,"room.txt");yield return Until(()=>File.Exists(roomFile),40,"waiting-room-code");
    Field("输入 6 位房间号").text=File.ReadAllText(roomFile);Click("加入房间");yield return Until(()=>client.Room!=null,20,"joined");
   }
   string previousMatch="";
   for(int round=1;round<=2;round++){
    Combat=round==1;
    yield return Until(()=>client.Screen=="Room"&&client.Room.status=="Waiting",25,"room-"+round);
    Click("准备");
    if(plan.index==0){yield return Until(()=>client.Room.members.Length==4&&client.Room.members.All(m=>m.ready==1),40,"all-ready-"+round);Click("开始对战");}
    yield return Until(()=>client.Screen=="Battle"&&NetPlayer.All.Count==4&&NetPlayer.All.Count(p=>p.IsOwner)==1,100,"battle-"+round);
    if(client.ActiveMatch==previousMatch)throw new Exception("Rematch reused the previous match");
    previousMatch=client.ActiveMatch;report.matchId=previousMatch;report.players=NetPlayer.All.Count;report.owned=NetPlayer.All.Count(p=>p.IsOwner);Save("synced-"+round);
    yield return Capture("battle-"+round);
    yield return Until(()=>client.Screen=="Result",220,"result-"+round);
    ResultData result=null;string error=null;
    yield return client.Api.Request<ResultData>("GET","/api/matches/"+previousMatch+"/result",null,v=>result=v,e=>error=e);
    if(result==null||result.status!="Finished"||result.result.players.Length!=4)throw new Exception(error??"Match did not finish normally");
    report.rounds=round;report.duration=result.result.duration;report.kills=result.result.players.Sum(p=>p.kills);report.deaths=result.result.players.Sum(p=>p.deaths);
    if(round==1&&(report.kills==0||report.deaths==0))throw new Exception("Combat round had no authoritative kills");
    if(round==2&&report.duration<GameConfig.Current.roundSeconds-1)throw new Exception("Timer round ended early");
    yield return Capture("result-"+round);
    Click("返回房间");yield return Until(()=>client.Screen=="Room"&&client.Room.members.First(m=>m.userId==client.User.id).ready==0,20,"returned-"+round);
   }
   UserProfile profile=null;
   yield return client.Api.Request<UserProfile>("GET","/api/users/me",null,v=>profile=v,e=>{});
   if(profile==null||profile.games!=2)throw new Exception("Result was not persisted exactly twice");
   report.games=profile.games;report.passed=true;Save("passed");Application.Quit(0);
  }
  IEnumerator Until(Func<bool> condition,float seconds,string stage){
   Save(stage);float until=Time.realtimeSinceStartup+seconds;
   while(!condition()){
    if(failed)yield break;
    if(Time.realtimeSinceStartup>until){Fail("Timeout: "+stage);yield break;}
    yield return null;
   }
  }
  IEnumerator Capture(string label){
   if(plan.index!=0)yield break;
   yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(plan.work,label+".png"));yield return new WaitForSecondsRealtime(.2f);
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
   Vector2 target=player.Weapon.Value==0?new Vector2(blue?-9:9,player.Seat.Value==0?-3:0):Mathf.Abs(pos.x)>6?new Vector2(blue?-6:6,0):new Vector2(blue?-1:1,player.Seat.Value*.9f);
   movement=Vector2.ClampMagnitude(target-pos,1);
   var enemy=NetPlayer.All.FirstOrDefault(p=>p.Team.Value!=player.Team.Value&&p.Alive);
   if(enemy!=null)aim=enemy.Position.Value-pos;
   fire=player.Weapon.Value!=0;
  }
 }
}
#endif
