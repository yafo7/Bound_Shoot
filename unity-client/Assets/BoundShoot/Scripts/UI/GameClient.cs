using System;
using System.Collections;
using System.Linq;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;
namespace BoundShoot {
 public class GameClient:MonoBehaviour {
  public static GameClient Instance;public HttpApi Api;public RoomData Room;public UserProfile User;
  public string Screen="Auth";public string ActiveMatch="";
  UiKit ui;LobbyRealtime realtime;Text notice,hud;InputField username,password,confirm,address,code;
  string lastRoomJson="",attemptedMatch="",message="";bool busy,register,remember=true,connecting,resultLoading;
  MatchData match;ResultData result;float nextProfile;bool disposed;
  public AutomationPlan Automation;
  IEnumerator Start(){
   Instance=this;Api=gameObject.AddComponent<HttpApi>();
   Api.BaseUrl=PlayerPrefs.GetString("BackendUrl","http://127.0.0.1:3000");
   string overrideUrl=GetArg("-backend");if(!string.IsNullOrEmpty(overrideUrl))Api.BaseUrl=overrideUrl;
   realtime=gameObject.AddComponent<LobbyRealtime>();realtime.OnEvent=e=>{if(e.room!=null)ReceiveRoom(e.room);};
   var go=new GameObject("UI",typeof(RectTransform));ui=go.AddComponent<UiKit>();ui.Initialize();
   Bootstrap.Network.OnClientConnectedCallback+=Connected;Bootstrap.Network.OnClientDisconnectCallback+=Disconnected;
   Redraw();StartCoroutine(Poll());
   string planPath=GetArg("-testPlan");
   if(!string.IsNullOrEmpty(planPath)){
    Automation=JsonUtility.FromJson<AutomationPlan>(System.IO.File.ReadAllText(planPath));Api.BaseUrl=Automation.backendUrl;
    if(!string.IsNullOrEmpty(Automation.screenshot)){
     yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(System.IO.Path.ChangeExtension(Automation.screenshot,"login.png"));yield return new WaitForSecondsRealtime(.2f);
    }
    yield return Api.Request<AuthData>("POST","/api/auth/login",new Credentials{username=Automation.username,password=Automation.password},SignedIn,Error);
    yield break;
   }
   string saved=PlayerPrefs.GetString(Api.CredentialKey,"");
   if(!string.IsNullOrEmpty(saved)){
    Api.Remember=true;Api.RefreshToken=saved;
    yield return Api.Request<AuthData>("POST","/api/auth/refresh",new RefreshBody{refreshToken=saved},SignedIn,e=>{Api.Forget();Error(e);},false);
   }
  }
  public static string GetArg(string flag){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,flag);return i>=0&&i+1<args.Length?args[i+1]:null;}
  void SignedIn(AuthData auth){
   Api.SetTokens(auth);User=auth.user;Screen="Lobby";message="";Redraw();
   if(!string.IsNullOrEmpty(Automation?.screenshot))StartCoroutine(CaptureLobbyAndResume());
   else {realtime.Connect(Api.BaseUrl,Api.AccessToken);StartCoroutine(Resume());}
  }
  IEnumerator CaptureLobbyAndResume(){
   yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(System.IO.Path.ChangeExtension(Automation.screenshot,"lobby.png"));yield return new WaitForSecondsRealtime(.2f);
   realtime.Connect(Api.BaseUrl,Api.AccessToken);yield return Resume();
  }
  IEnumerator Resume(){
   yield return Api.Request<UserProfile>("GET","/api/users/me",null,p=>{User=p;if(!string.IsNullOrEmpty(p.roomId))StartCoroutine(LoadRoom(p.roomId));},Error);
  }
  IEnumerator LoadRoom(string rid){yield return Api.Request<RoomData>("GET","/api/rooms/"+rid,null,ReceiveRoom,Error);}
  void Error(string text){message=text;busy=false;if(notice!=null)notice.text=text;}
  void ReceiveRoom(RoomData room){
   Room=room;
   if(Screen=="Lobby"||Screen=="Auth")Screen="Room";
   if(Screen=="Room"){
    string json=JsonUtility.ToJson(room);
    if(json!=lastRoomJson){lastRoomJson=json;Redraw();}
   }
   if((room.status=="InGame"||room.status=="Starting")&&!string.IsNullOrEmpty(room.matchId)){
    ActiveMatch=room.matchId;
    if(room.status=="InGame"&&attemptedMatch!=ActiveMatch&&!connecting)StartCoroutine(ConnectBattle());
   }else if(!string.IsNullOrEmpty(ActiveMatch)&&Screen!="Result"&&!resultLoading)StartCoroutine(LoadResult());
  }
  IEnumerator Poll(){
   while(!disposed){
    if(User!=null){
     if(Room!=null)yield return Api.Request<RoomData>("GET","/api/rooms/"+Room.id,null,ReceiveRoom,e=>{
      if(e.Contains("房间不存在")||e.Contains("不在此房间")){Room=null;Screen="Lobby";Redraw();}else Error(e);
     });
     if(Screen=="Lobby"&&Time.unscaledTime>=nextProfile){nextProfile=Time.unscaledTime+10;yield return Api.Request<UserProfile>("GET","/api/users/me",null,p=>User=p,Error);}
    }
    yield return new WaitForSecondsRealtime(1);
   }
  }
  void Run<T>(string method,string path,object body,Action<T> done){
   if(busy)return;busy=true;message="";StartCoroutine(Request());
   IEnumerator Request(){yield return Api.Request<T>(method,path,body,v=>{busy=false;done(v);},Error);busy=false;}
  }
  void Authenticate(){
   Api.BaseUrl=address.text.Trim().TrimEnd('/');if(!Uri.TryCreate(Api.BaseUrl,UriKind.Absolute,out var uri)||(uri.Scheme!="http"&&uri.Scheme!="https")){Error("请输入完整的 http:// 或 https:// 地址");return;}
   PlayerPrefs.SetString("BackendUrl",Api.BaseUrl);Api.Remember=remember;
   if(register&&password.text!=confirm.text){Error("两次密码不一致");return;}
   Run<AuthData>("POST",register?"/api/auth/register":"/api/auth/login",new Credentials{username=username.text,password=password.text},SignedIn);
  }
  IEnumerator ConnectBattle(){
   if(connecting)yield break;connecting=true;attemptedMatch=ActiveMatch;
   if(!string.IsNullOrEmpty(Automation?.screenshot)){
    Screen="Room";Redraw();yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(System.IO.Path.ChangeExtension(Automation.screenshot,"room.png"));yield return new WaitForSecondsRealtime(.2f);
   }
   Screen="Connecting";Redraw();
   yield return Api.Request<MatchData>("GET","/api/matches/"+ActiveMatch,null,v=>match=v,Error);
   TicketData ticket=null;yield return Api.Request<TicketData>("POST","/api/matches/"+ActiveMatch+"/ticket",null,v=>ticket=v,Error);
   if(ticket==null){connecting=false;Screen="Room";Redraw();yield break;}
   if(Bootstrap.Network.IsListening){Bootstrap.Network.Shutdown();yield return null;yield return new WaitForSecondsRealtime(.25f);}
   Bootstrap.Network.GetComponent<UnityTransport>().SetConnectionData(ticket.host,(ushort)ticket.port);
   Bootstrap.Network.NetworkConfig.ConnectionData=Encoding.UTF8.GetBytes(ticket.ticket);
   bool success=Bootstrap.Network.StartClient();if(!success){connecting=false;Screen="Room";Error("无法启动客户端连接");Redraw();yield break;}
   float deadline=Time.unscaledTime+20;while(connecting&&Time.unscaledTime<deadline)yield return null;
   if(connecting){Bootstrap.Network.Shutdown();connecting=false;Screen="Room";message="连接战斗超时，可以重试";Redraw();}
  }
  void Connected(ulong cid){
   // NGO 1.8 invokes this with the server sender id when scene management is disabled.
   if(Bootstrap.Network.IsServer||!Bootstrap.Network.IsConnectedClient)return;connecting=false;Screen="Battle";message="";Redraw();
   if(Automation!=null)StartCoroutine(AutomationReport());
  }
  void Disconnected(ulong cid){
   if(Screen=="Result"||disposed)return;
   if(Screen=="Battle"||Screen=="Connecting"){
    connecting=false;Screen="Room";message=string.IsNullOrEmpty(Bootstrap.Network.DisconnectReason)?"与战斗服务器断开，可在宽限期内重连":Bootstrap.Network.DisconnectReason;Redraw();
   }
  }
  IEnumerator LoadResult(){
   resultLoading=true;ResultData data=null;
   yield return Api.Request<ResultData>("GET","/api/matches/"+ActiveMatch+"/result",null,v=>data=v,Error);
   resultLoading=false;
   if(data!=null&&(data.status=="Finished"||data.status=="Aborted")){
    result=data;Screen="Result";connecting=false;Bootstrap.Network.Shutdown();Redraw();
    if(Automation?.autoReturn==true)StartCoroutine(AutoReturn());
   }
  }
  void Update(){
   if(Screen!="Battle"||hud==null)return;var world=BattleWorld.Instance;if(world==null){hud.text="正在同步战斗世界…";return;}
   var local=NetPlayer.All.Find(p=>p.IsOwner&&p.IsClient);
   string phase=world.Phase.Value==0?"等待所有玩家加载":world.Phase.Value==1?"开战倒计时":"剩余时间";
   var c=GameConfig.Current;
   string equipped=local==null?"":(c.Weapon((WeaponKind)local.Weapon.Value)?.name??"空手");
   string carried=local==null?"":(c.Item((ItemKind)local.Item.Value)?.name??"无");
   hud.text=phase+"  "+Mathf.CeilToInt(world.Clock.Value)+"s     蓝 "+NetPlayer.All.Count(p=>p.Team.Value==0&&p.Alive)+" : "+NetPlayer.All.Count(p=>p.Team.Value==1&&p.Alive)+" 红\n"+
    (local==null?"同步玩家…":"生命 "+local.Health.Value+"/"+local.MaxHealth.Value+"    护盾 "+local.Shield.Value+"    "+equipped+"    E 道具："+carried+"    K/D "+local.Kills.Value+"/"+local.Deaths.Value);
  }
  void Redraw(){
   if(ui==null)return;ui.Clear();notice=null;hud=null;
   if(Screen=="Battle"){
    ui.Box(26,20,1548,110,new Color(.055f,.08f,.12f,.92f));hud=ui.Text("",50,28,1500,96,24);
    float foot=Mathf.Max(826,ui.Root.rect.height-74);
    ui.Box(26,foot,1548,54,new Color(.055f,.08f,.12f,.9f));ui.Text("WASD 移动  /  鼠标瞄准与开火  /  E 使用道具     ·     保持队形，拖回队友",48,foot,1320,54,21,UiKit.Soft);
    ui.Button("断开",1445,foot+9,100,36,()=>Bootstrap.Network.Shutdown(),UiKit.Soft);return;
   }
   ui.Backdrop(UiKit.Ink);
   ui.Text("BOUND / SHOOT",64,42,700,60,42);ui.Text("派 对 射 击     /     绳索连接，合作求生",68,105,900,44,23,UiKit.Soft);
   ui.Text("基础原型  0.1",1290,58,240,44,21,Draw2D.Gold,align:TextAnchor.MiddleRight);
   if(Screen=="Auth")AuthScreen();
   if(Screen=="Lobby")LobbyScreen();
   if(Screen=="Room")RoomScreen();
   if(Screen=="Connecting"){ui.Text("连接竞技场",100,270,1000,80,48);ui.Text("正在验证本局身份并加载服务器世界…",104,365,1300,80,28,UiKit.Soft);}
   if(Screen=="Result")ResultScreen();
   notice=ui.Text(message,68,827,1460,52,22,new Color(1,.65f,.42f));
  }
  void AuthScreen(){
   ui.Box(68,208,720,570,UiKit.Panel);
   ui.Text("同一根绳上的队友。",104,246,640,80,42);
   ui.Text("抢夺补给  /  拖动尸体  /  争取复活\n\n四人加入同一房间，选择连接顺序，\n完成准备后进入独立服务器。",108,350,600,250,28,UiKit.Soft);
   ui.Text("01 — 协作比枪法更重要",108,687,600,50,25,Draw2D.Gold);
   ui.Text(register?"注册账号":"登录账号",880,200,600,60,34);
   address=ui.Field("后端地址",880,277,610,false,Api.BaseUrl);
   username=ui.Field("用户名（3–24 个字符）",880,350,610);
   password=ui.Field("密码（至少 8 位）",880,423,610,true);
   confirm=register?ui.Field("确认密码",880,496,610,true):null;
   ui.Button(remember?"☑ 记住登录":"□ 记住登录",880,register?569:503,280,44,()=>{remember=!remember;Redraw();},UiKit.Soft);
   ui.Button(register?"创建账号并进入":"进入大厅",880,register?632:580,610,64,Authenticate);
   ui.Button(register?"已有账号，返回登录":"还没有账号？注册",880,register?711:665,610,52,()=>{register=!register;message="";Redraw();},UiKit.Soft);
  }
  void LobbyScreen(){
   if(User==null)return;
   ui.Box(68,210,700,560,UiKit.Panel);ui.Text(User.username,104,240,600,64,38);
   ui.Text("对局 "+User.games+"     胜利 "+User.wins+"     K/D "+User.kills+"/"+User.deaths,108,320,600,60,24,UiKit.Soft);
   ui.Text("选择外观 · 仅改变造型",108,416,600,50,26);
   for(int i=0;i<4;i++){int skin=i;ui.Button((User.skin==i?"● ":"")+"外观 "+(i+1),108+i*155,488,140,80,()=>Run<UserProfile>("PATCH","/api/users/me/cosmetic",new SkinBody{skin=skin},p=>{User=p;Redraw();}),new[]{Draw2D.Gold,new Color(.75f,.5f,1),new Color(.4f,1,.75f),new Color(1,.55f,.8f)}[i]);}
   ui.Text("基础生命 3  /  捡取武器后锁定至死亡",108,635,600,70,23,UiKit.Soft);
   ui.Button("创建房间",850,240,650,90,()=>Run<RoomData>("POST","/api/rooms",null,r=>{lastRoomJson="";ReceiveRoom(r);}));
   code=ui.Field("输入 6 位房间号",850,390,650);
   ui.Button("加入房间",850,470,650,70,()=>Run<RoomData>("POST","/api/rooms/join",new JoinBody{code=code.text.Trim()},r=>{lastRoomJson="";ReceiveRoom(r);}),Draw2D.Blue);
   ui.Button("查看最近战绩",850,596,310,60,()=>Run<HistoryData>("GET","/api/users/me/matches",null,h=>Error(h.matches.Length==0?"暂无战绩":string.Join("   |   ",h.matches.Take(3).Select(m=>m.status+" K/D "+m.kills+"/"+m.deaths)))) ,UiKit.Soft);
   ui.Button("退出账号",1190,596,310,60,Logout,UiKit.Soft);
  }
  void RoomScreen(){
   if(Room==null)return;
   ui.Text("房间 "+Room.code+"   /   "+Room.size+"v"+Room.size,68,190,1000,60,34);
   ui.Text(Room.status=="Waiting"?"选择绳索位置 → 全员准备 → 房主开战":"服务器状态："+Room.status,68,255,1000,50,23,UiKit.Soft);
   var self=Room.members.FirstOrDefault(m=>m.userId==User.id);
   for(int t=0;t<2;t++){
    int team=t;float x=t==0?68:550;ui.Box(x,330,450,420,UiKit.Panel);ui.Text(t==0?"蓝队":"红队",x+24,347,400,50,29,t==0?Draw2D.Blue:Draw2D.Red);
    for(int s=0;s<Room.size;s++){
     int seat=s;var m=Room.members.FirstOrDefault(p=>p.team==team&&p.seat==seat);
     string text=(team==0?"A":"B")+(s+1)+"   "+(m==null?"空位":m.username+(m.userId==Room.hostId?"  ♛":"")+(m.ready==1?"  ✓":""));
     var b=ui.Button(text,x+24,410+s*78,402,62,()=>Run<RoomData>("PUT","/api/rooms/"+Room.id+"/seat",new SeatBody{team=team,seat=seat},r=>{lastRoomJson="";ReceiveRoom(r);}),m?.userId==User.id?Draw2D.Gold:UiKit.Soft);
     b.interactable=Room.status=="Waiting"&&(m==null||m.userId==User.id);
     if(s<Room.size-1)ui.Text("│",x+40,470+s*78,100,19,22,t==0?Draw2D.Blue:Draw2D.Red);
    }
   }
   ui.Box(1040,330,460,420,UiKit.Panel);ui.Text(GameConfig.Current.Map(Room.mapId)?.name??Room.mapId,1068,350,400,52,30);
   ui.Text("经典竞技场\n左右补给 · 上下复活\n180 秒 / 整队全灭立即结束",1068,411,400,140,23,UiKit.Soft);
   if(Room.hostId==User.id&&Room.status=="Waiting"){
    ui.Button("地图："+Room.mapId+"  ›",1068,526,392,36,()=>{
     var maps=GameConfig.Current.maps.Where(m=>m.available).ToArray();int index=Array.FindIndex(maps,m=>m.id==Room.mapId);
     Run<RoomData>("PUT","/api/rooms/"+Room.id+"/config",new RoomConfigBody{size=Room.size,mapId=maps[(index+1)%maps.Length].id},r=>{lastRoomJson="";ReceiveRoom(r);});
    },UiKit.Soft);
    ui.Button("2v2",1068,572,120,46,()=>Configure(2),UiKit.Soft);
    ui.Button("3v3",1204,572,120,46,()=>Configure(3),UiKit.Soft);
    ui.Button("4v4",1340,572,120,46,()=>Configure(4),UiKit.Soft);
    ui.Button("开始对战",1068,643,392,70,()=>Run<MatchData>("POST","/api/rooms/"+Room.id+"/start",null,m=>{ActiveMatch=m.id;message="等待游戏服务器就绪";StartCoroutine(LoadRoom(Room.id));}));
   }else ui.Text(Room.status=="Waiting"?"等待房主开始":"正在连接独立服务器",1068,607,400,80,25,Draw2D.Gold);
   ui.Button(self?.ready==1?"取消准备":"准备",68,770,330,54,()=>Run<RoomData>("PUT","/api/rooms/"+Room.id+"/ready",new ReadyBody{ready=self?.ready!=1},r=>{lastRoomJson="";ReceiveRoom(r);}),self?.ready==1?UiKit.Soft:Draw2D.Gold).interactable=Room.status=="Waiting";
   ui.Button("离开房间",420,770,300,54,()=>Run<Empty>("POST","/api/rooms/"+Room.id+"/leave",null,_=>{Room=null;ActiveMatch="";Screen="Lobby";Redraw();}),UiKit.Soft).interactable=Room.status=="Waiting";
   if(Room.status=="InGame")ui.Button("重新连接",750,770,300,54,()=>{attemptedMatch="";StartCoroutine(ConnectBattle());},Draw2D.Blue);
  }
  void Configure(int size)=>Run<RoomData>("PUT","/api/rooms/"+Room.id+"/config",new RoomConfigBody{size=size,mapId=Room.mapId},r=>{lastRoomJson="";ReceiveRoom(r);});
  void ResultScreen(){
   if(result==null)return;
   if(result.status=="Aborted"){ui.Text("对战已中止",68,230,1400,100,54);ui.Text("服务器异常或加载超时，本局不计战绩。",72,360,1400,80,28,UiKit.Soft);}
   else{
    var r=result.result;ui.Text(r.winner<0?"平局":r.winner==0?"蓝队胜利":"红队胜利",68,200,1400,100,56,r.winner==0?Draw2D.Blue:r.winner==1?Draw2D.Red:Draw2D.Gold);
    ui.Text(result.mapId+"    /    "+Mathf.RoundToInt(r.duration)+" 秒",72,303,1400,55,25,UiKit.Soft);
    ui.Box(68,390,1432,50,UiKit.Panel);ui.Text("玩家 / 队伍与位置",92,390,800,50);ui.Text("击杀          死亡          伤害",1060,390,420,50);
    for(int i=0;i<r.players.Length;i++){
     var p=r.players[i];var m=match?.players?.FirstOrDefault(m=>m.userId==p.userId)??Room?.members.FirstOrDefault(m=>m.userId==p.userId);
     ui.Text((m?.username??p.userId)+"   "+(m?.team==0?"蓝 A":"红 B")+((m?.seat??0)+1),92,450+i*37,900,36,23,m?.team==0?Draw2D.Blue:Draw2D.Red);
     ui.Text(p.kills+"                 "+p.deaths+"                 "+p.damage,1070,450+i*37,420,36,23);
    }
   }
   ui.Button("返回房间",68,770,480,64,ReturnToRoom);
  }
  void ReturnToRoom(){ActiveMatch="";attemptedMatch="";match=null;result=null;Screen=Room==null?"Lobby":"Room";lastRoomJson="";Redraw();}
  IEnumerator AutoReturn(){
   yield return new WaitForEndOfFrame();
   if(!string.IsNullOrEmpty(Automation?.screenshot))ScreenCapture.CaptureScreenshot(System.IO.Path.ChangeExtension(Automation.screenshot,"result.png"));
   yield return new WaitForSecondsRealtime(1);if(Screen=="Result")ReturnToRoom();
  }
  void Logout(){Run<Empty>("POST","/api/auth/logout",null,_=>{Api.Forget();realtime.Disconnect();User=null;Room=null;Screen="Auth";Redraw();});}
  IEnumerator AutomationReport(){
   yield return new WaitForSecondsRealtime(2);
   Debug.Log("BOUND_CLIENT_SYNC players="+NetPlayer.All.Count+" local="+NetPlayer.All.Count(p=>p.IsOwner));
   if(!string.IsNullOrEmpty(Automation.reportFile))System.IO.File.WriteAllText(Automation.reportFile,JsonUtility.ToJson(new AutomationReportData{matchId=ActiveMatch,players=NetPlayer.All.Count,owned=NetPlayer.All.Count(p=>p.IsOwner),phase=BattleWorld.Instance?.Phase.Value??-1,positions=NetPlayer.All.Select(p=>p.Position.Value).ToArray()}));
   if(!string.IsNullOrEmpty(Automation.screenshot))ScreenCapture.CaptureScreenshot(Automation.screenshot);
   yield return new WaitForSecondsRealtime(Automation.runSeconds);
   var p=NetPlayer.All.Find(p=>p.IsOwner);
   Debug.Log("BOUND_CLIENT_DONE "+(p==null?"despawned":("position="+p.Position.Value+" hp="+p.Health.Value+" kills="+p.Kills.Value)));
   if(Automation.quitAfter)Application.Quit();
  }
  void OnDestroy(){disposed=true;if(Bootstrap.Network!=null){Bootstrap.Network.OnClientConnectedCallback-=Connected;Bootstrap.Network.OnClientDisconnectCallback-=Disconnected;}if(ui!=null)Destroy(ui.gameObject);}
 }
 [Serializable] public class AutomationPlan {public string backendUrl,username,password,screenshot,reportFile;public float runSeconds=12;public bool quitAfter=true,combat=true,autoReturn;}
 [Serializable] public class AutomationReportData {public string matchId;public int players,owned,phase;public Vector2[] positions;}
}
