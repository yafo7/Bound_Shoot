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
  UiKit ui;LobbyRealtime realtime;Text notice,hud;InputField username,localAddress;
  public int SelectedSkin {get;private set;}public int LocalTeamSize=2;int localPhase=-1;string localRoster="";
  bool settings;public bool InputBlocked=>settings||!(Screen=="Training"||Screen=="Battle"||Screen=="LocalBattle");
  string lastRoomJson="",attemptedMatch="",message="",playerName="";bool busy,connecting,resultLoading;
  MatchData match;ResultData result;float nextProfile,nextRealtimeRetry;bool disposed;
  void Start(){
   Instance=this;SelectedSkin=Mathf.Clamp(PlayerPrefs.GetInt("BoundShoot.Skin",0),0,7);Api=gameObject.AddComponent<HttpApi>();
   Api.BaseUrl=NetworkSettings.Current.BackendUrl;Api.SessionExpired+=Expired;
#if UNITY_EDITOR || DEVELOPMENT_BUILD || BOUND_SHOOT_QA
   string overrideUrl=GetArg("-backend");if(!string.IsNullOrEmpty(overrideUrl))Api.BaseUrl=overrideUrl;
#endif
   realtime=gameObject.AddComponent<LobbyRealtime>();realtime.OnEvent=e=>{if(e.room!=null&&Room!=null&&e.room.id==Room.id)ReceiveRoom(e.room);};
   var go=new GameObject("UI",typeof(RectTransform));ui=go.AddComponent<UiKit>();ui.Initialize();
   Bootstrap.Network.OnClientConnectedCallback+=Connected;Bootstrap.Network.OnClientDisconnectCallback+=Disconnected;
   Redraw();StartCoroutine(Poll());
   // Every process owns its session. Four windows must never inherit one shared account.
  }
  public static string GetArg(string flag){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,flag);return i>=0&&i+1<args.Length?args[i+1]:null;}
  void SignedIn(AuthData auth){
   Api.SetTokens(auth);User=auth.user;Room=auth.room;lastRoomJson="";Screen=Room==null?"Auth":"Room";message="";Redraw();
   realtime.Connect(Api.BaseUrl,Api.AccessToken);StartCoroutine(Resume());
  }
  IEnumerator Resume(){
   string uid=User?.id;
   yield return Api.Request<UserProfile>("GET","/api/users/me",null,p=>{if(User?.id!=uid)return;User=p;if(!string.IsNullOrEmpty(p.roomId))StartCoroutine(LoadRoom(p.roomId));},Error);
  }
  IEnumerator LoadRoom(string rid){string uid=User?.id;yield return Api.Request<RoomData>("GET","/api/rooms/"+rid,null,r=>{if(User!=null&&User.id==uid)ReceiveRoom(r);},Error);}
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
     if(!realtime.Connected&&!realtime.Connecting&&Time.unscaledTime>=nextRealtimeRetry){nextRealtimeRetry=Time.unscaledTime+5;realtime.Connect(Api.BaseUrl,Api.AccessToken);}
     if(Room!=null){string roomId=Room.id,uid=User.id;yield return Api.Request<RoomData>("GET","/api/rooms/"+roomId,null,r=>{if(User?.id==uid&&Room?.id==roomId)ReceiveRoom(r);},e=>{
      if(User?.id!=uid||Room?.id!=roomId)return;
      if(e.Contains("房间不存在")||e.Contains("不在此房间")){Expired();}else Error(e);
     });}
     if(Screen=="Lobby"&&Time.unscaledTime>=nextProfile){string uid=User?.id;nextProfile=Time.unscaledTime+10;yield return Api.Request<UserProfile>("GET","/api/users/me",null,p=>{if(User?.id==uid)User=p;},Error);}
    }
    yield return new WaitForSecondsRealtime(1);
   }
  }
  void Run<T>(string method,string path,object body,Action<T> done){
   if(busy)return;busy=true;message="";StartCoroutine(Request());
   IEnumerator Request(){yield return Api.Request<T>(method,path,body,v=>{busy=false;done(v);},Error);busy=false;}
  }
  void Authenticate(){
   string name=username.text.Trim();
   EnterName(name);
  }
  public void EnterName(string name){
   name=(name??"").Trim();
   if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^[a-zA-Z0-9_\u4e00-\u9fff]{1,24}$")){Error("名字需为1–24位汉字、字母、数字或下划线");return;}
   playerName=name;message="";Screen="Menu";Redraw();
  }
  void JoinBattle()=>Run<AuthData>("POST","/api/auth/quick",new NameBody{name=playerName},SignedIn);
  public void StartTraining(){if(!busy)StartCoroutine(EnterTraining());}
  IEnumerator EnterTraining(){
   busy=true;message="";Screen="TrainingStarting";Redraw();
   if(User!=null||Bootstrap.Network.IsListening){busy=false;Screen="Menu";Error("请先退出当前对局");Redraw();yield break;}
   var runtime=gameObject.AddComponent<TrainingRuntime>();yield return runtime.Begin(playerName,SelectedSkin);
   if(runtime.Ready){Screen="Training";busy=false;Redraw();}
   else{string error=runtime.Error;yield return runtime.Stop();busy=false;Screen="Menu";message=error;Redraw();}
  }
  public void LeaveTraining(){if(!busy)StartCoroutine(ExitTraining());}
  public void SelectCharacter(int skin){SelectedSkin=Mathf.Clamp(skin,0,7);PlayerPrefs.SetInt("BoundShoot.Skin",SelectedSkin);Redraw();}
  public void StartLocalMatch(bool host=true){if(!busy)StartCoroutine(EnterLocal(host));}
  IEnumerator EnterLocal(bool host){
   if(User!=null||Bootstrap.Network.IsListening){Error("请先退出当前房间");yield break;}
   string address=localAddress==null?"127.0.0.1":localAddress.text.Trim();
   busy=true;Screen="LocalStarting";Redraw();localPhase=-1;
   var runtime=gameObject.AddComponent<LocalMatchRuntime>();yield return runtime.Begin(playerName,SelectedSkin,LocalTeamSize,host,address);
   if(!string.IsNullOrEmpty(runtime.Error)||!runtime.Ready){message=runtime.Error;yield return runtime.Stop();Screen="Menu";}
   else Screen="LocalRoom";
   busy=false;Redraw();
  }
  public void LeaveLocalMatch(){if(!busy)StartCoroutine(ExitLocal());}
  IEnumerator ExitLocal(){busy=true;Screen="Menu";if(LocalMatchRuntime.Instance!=null)yield return LocalMatchRuntime.Instance.Stop();busy=false;localPhase=-1;Redraw();}
  IEnumerator ExitTraining(){
   busy=true;Screen="Menu";
   if(TrainingRuntime.Instance!=null)yield return TrainingRuntime.Instance.Stop();
   busy=false;Redraw();
  }
  IEnumerator ConnectBattle(){
   if(connecting)yield break;connecting=true;attemptedMatch=ActiveMatch;
   string uid=User?.id,mid=ActiveMatch;
   Screen="Connecting";Redraw();
   yield return Api.Request<MatchData>("GET","/api/matches/"+mid,null,v=>match=v,Error);
   if(User?.id!=uid||ActiveMatch!=mid||Screen!="Connecting"){connecting=false;yield break;}
   TicketData ticket=null;yield return Api.Request<TicketData>("POST","/api/matches/"+mid+"/ticket",null,v=>ticket=v,Error);
   if(User?.id!=uid||ActiveMatch!=mid||Screen!="Connecting"){connecting=false;yield break;}
   if(ticket==null){connecting=false;Screen="Room";Redraw();yield break;}
   if(Bootstrap.Network.IsListening){Bootstrap.Network.Shutdown();yield return null;yield return new WaitForSecondsRealtime(.25f);}
   if(User?.id!=uid||ActiveMatch!=mid||Screen!="Connecting"){connecting=false;yield break;}
   Bootstrap.Network.GetComponent<UnityTransport>().SetConnectionData(ticket.host,(ushort)ticket.port);
   Bootstrap.Network.NetworkConfig.ConnectionData=Encoding.UTF8.GetBytes(ticket.ticket);
   bool success=Bootstrap.Network.StartClient();if(!success){connecting=false;Screen="Room";Error("连接失败，请重试");Redraw();yield break;}
   float deadline=Time.unscaledTime+NetworkSettings.Current.ConnectTimeoutSeconds;while(connecting&&Time.unscaledTime<deadline)yield return null;
   if(connecting){Bootstrap.Network.Shutdown();connecting=false;Screen="Room";message="连接超时，请重试";Redraw();}
  }
  void Connected(ulong cid){
   // NGO 1.8 invokes this with the server sender id when scene management is disabled.
   if(LocalMatchRuntime.Instance!=null||Bootstrap.Network.IsServer||!Bootstrap.Network.IsConnectedClient)return;connecting=false;Screen="Battle";message="";Redraw();
  }
  void Disconnected(ulong cid){
   if(Screen=="Result"||disposed)return;
   if(Screen=="Battle"||Screen=="Connecting"){
    connecting=false;Screen="Room";message=string.IsNullOrEmpty(Bootstrap.Network.DisconnectReason)?"连接已断开":Bootstrap.Network.DisconnectReason;Redraw();
   }
  }
  IEnumerator LoadResult(){
   resultLoading=true;ResultData data=null;
   yield return Api.Request<ResultData>("GET","/api/matches/"+ActiveMatch+"/result",null,v=>data=v,Error);
   resultLoading=false;
   if(data!=null&&(data.status=="Finished"||data.status=="Aborted")){
    result=data;Screen="Result";connecting=false;Bootstrap.Network.Shutdown();Redraw();
   }
  }
  void Update(){
   if(Input.GetKeyDown(KeyCode.Escape)){settings=!settings;Redraw();}
   var localSession=LocalMatchRuntime.Instance;
   if(localSession!=null&&!busy){
    if(!string.IsNullOrEmpty(localSession.Error)){message=localSession.Error;LeaveLocalMatch();return;}
    if(localSession.Ready&&localPhase!=localSession.World.Phase.Value){localPhase=localSession.World.Phase.Value;Screen=localPhase==0?"LocalRoom":localPhase==3?"LocalResult":"LocalBattle";Redraw();}
    if(Screen=="LocalRoom"){
     string roster=string.Join("|",NetPlayer.All.Select(p=>p.UserId.Value.ToString()+p.DisplayName.Value.ToString()+p.Skin.Value+p.IsBot.Value));
     if(roster!=localRoster){localRoster=roster;Redraw();}
    }
   }
   if(Screen=="Auth"&&username!=null&&(Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.KeypadEnter)))Authenticate();
   if((Screen!="Battle"&&Screen!="Training"&&Screen!="LocalBattle")||hud==null)return;var world=BattleWorld.Instance;if(world==null){hud.text="加载中…";return;}
   var local=NetPlayer.All.Find(p=>p.IsOwner&&p.IsClient&&!p.IsBot.Value&&p.TrainingRole==0);
   string phase=world.Phase.Value==0?"等待玩家":world.Phase.Value==1?"倒计时":"剩余时间";
   var c=GameConfig.Current;
   string equipped=local==null?"":(c.Weapon((WeaponKind)local.Weapon.Value)?.name??"空手");
   string carried=local==null?"":(c.Item((ItemKind)local.Item.Value)?.name??"无");
   hud.text=(Screen=="Training"?"训练房    队友 "+TrainingRuntime.Instance.Teammates+"/3":phase+"  "+Mathf.CeilToInt(world.Clock.Value)+"s     蓝 "+NetPlayer.All.Count(p=>p.Team.Value==0&&p.Alive)+" : "+NetPlayer.All.Count(p=>p.Team.Value==1&&p.Alive)+" 红")+"\n"+
    (local==null?"加载中…":"生命 "+local.Health.Value+"/"+local.MaxHealth.Value+"    护盾 "+local.Shield.Value+"    "+equipped+"    E："+carried+"    K/D "+local.Kills.Value+"/"+local.Deaths.Value);
  }
  void Redraw(){
   if(ui==null)return;ui.Clear();notice=null;hud=null;
   if(Screen=="Battle"||Screen=="Training"||Screen=="LocalBattle"){
    ui.Box(26,20,1548,110,new Color(.055f,.08f,.12f,.92f));hud=ui.Text("",50,28,1500,96,24);
    float foot=Mathf.Max(826,ui.Root.rect.height-74);
    if(Screen=="Training"){
     ui.Box(26,foot,1548,64,UiKit.Panel);
     ui.Button("增加队友",50,foot+9,230,42,()=>{TrainingRuntime.Instance.AddTeammate();Redraw();}).interactable=TrainingRuntime.Instance.Teammates<3;
     ui.Button("移除队友",300,foot+9,230,42,()=>{TrainingRuntime.Instance.RemoveTeammate();Redraw();},UiKit.Soft).interactable=TrainingRuntime.Instance.Teammates>0;
     ui.Button("重置训练",550,foot+9,230,42,()=>TrainingRuntime.Instance.ResetRoom(),UiKit.Soft);
     ui.Button("返回",1345,foot+9,180,42,LeaveTraining,UiKit.Soft);
    }else ui.Button("离开",1445,foot+9,100,36,()=>{if(LocalMatchRuntime.Instance!=null)LeaveLocalMatch();else DisconnectBattle();},UiKit.Soft);
    ui.Button("设置",1160,foot+9,110,36,()=>{settings=true;Redraw();},UiKit.Soft);if(settings)SettingsPanel();return;
   }
   ui.Backdrop(UiKit.Ink);
   ui.Text("派对射击",64,42,700,60,42);
   if(Screen=="Auth")AuthScreen();
   if(Screen=="Menu"){
    ui.Text(playerName,70,140,1450,50,32);
    for(int i=0;i<8;i++){
     int skin=i;float x=70+(i%4)*240,y=230+(i/4)*235;
     var card=ui.Button(SelectedSkin==i?"已选择":"选择",x,y+155,210,45,()=>SelectCharacter(skin),SelectedSkin==i?Draw2D.Gold:UiKit.Soft);
     ui.Picture(VegetableArt.Get("portrait"+i),x+35,y,140,130);ui.Text(VegetableArt.Name(i),x,y+120,210,35,22,null,null,TextAnchor.MiddleCenter);
    }
    ui.Box(1080,220,440,525,UiKit.Panel);ui.Text("本地对战",1105,235,390,45,30);
    ui.Button(LocalTeamSize+"v"+LocalTeamSize,1105,295,390,50,()=>{LocalTeamSize=LocalTeamSize==2?4:2;Redraw();},UiKit.Soft);
    ui.Button("创建房间",1105,360,390,58,()=>StartLocalMatch());
    localAddress=ui.Field("房主地址",1105,435,390,value:PlayerPrefs.GetString("BoundShoot.LocalAddress","127.0.0.1"));
    ui.Button("加入房间",1105,505,390,54,()=>{PlayerPrefs.SetString("BoundShoot.LocalAddress",localAddress.text.Trim());StartLocalMatch(false);},UiKit.Soft);
    ui.Button("训练房",1105,580,390,54,StartTraining,UiKit.Soft);
    ui.Button("云端对战（待更新）",1105,660,390,45,JoinBattle,UiKit.Soft).interactable=false;
    ui.Button("更换名字",70,755,250,50,()=>{Screen="Auth";message="";Redraw();},UiKit.Soft);
    ui.Button("设置",350,755,250,50,()=>{settings=true;Redraw();},UiKit.Soft);
   }
   if(Screen=="LocalStarting")ui.Text("进入房间…",100,260,1000,80,38);
   if(Screen=="LocalRoom"){
    ui.Text("丰收野地  ·  "+(NetPlayer.All.Count/2)+"v"+(NetPlayer.All.Count/2),90,160,1200,60,36);
    for(int team=0;team<2;team++){
     float x=90+team*740;ui.Box(x,260,650,420,UiKit.Panel);ui.Text(team==0?"蓝队":"红队",x+25,270,600,50,30,team==0?Draw2D.Blue:Draw2D.Red);
     foreach(var p in NetPlayer.All.Where(p=>p.Team.Value==team).OrderBy(p=>p.Seat.Value)){
      float y=330+p.Seat.Value*82;ui.Picture(VegetableArt.Get("portrait"+p.Skin.Value),x+30,y,65,65);
      ui.Text(p.DisplayName.Value.ToString()+(p.IsBot.Value?" · AI":""),x+120,y,500,36,26);ui.Text(VegetableArt.Name(p.Skin.Value),x+120,y+34,500,30,20,UiKit.Soft);
     }
    }
    ui.Button("开始对战",100,730,600,65,()=>LocalMatchRuntime.Instance.StartRound()).interactable=LocalMatchRuntime.Instance.IsHost;
    ui.Button("离开",1120,730,300,65,LeaveLocalMatch,UiKit.Soft);
   }
   if(Screen=="LocalResult")LocalResultScreen();
   if(Screen=="Room")RoomScreen();
   if(Screen=="Connecting")ui.Text("连接中…",100,270,1000,80,40);
   if(Screen=="TrainingStarting")ui.Text("进入训练房…",100,270,1000,80,40);
   if(Screen=="Result")ResultScreen();
   notice=ui.Text(message,68,827,1460,52,22,new Color(1,.65f,.42f));
   if(settings)SettingsPanel();
  }
  void SettingsPanel(){
   ui.Box(0,0,1600,900,new Color(0,0,0,.6f));ui.Box(500,210,600,475,UiKit.Panel);ui.Text("设置",545,235,510,60,36);
   ui.Button("音效："+(PlayerPrefs.GetInt("BoundShoot.Sound",1)==1?"开":"关"),545,330,510,55,()=>{PlayerPrefs.SetInt("BoundShoot.Sound",1-PlayerPrefs.GetInt("BoundShoot.Sound",1));Redraw();},UiKit.Soft);
   ui.Button("镜头震动："+(PlayerPrefs.GetInt("BoundShoot.Shake",1)==1?"开":"关"),545,405,510,55,()=>{PlayerPrefs.SetInt("BoundShoot.Shake",1-PlayerPrefs.GetInt("BoundShoot.Shake",1));Redraw();},UiKit.Soft);
   ui.Button("返回游戏",545,495,510,60,()=>{settings=false;Redraw();});
   ui.Button("退出游戏",545,585,510,50,()=>{
#if UNITY_EDITOR
    UnityEditor.EditorApplication.isPlaying=false;
#else
    Application.Quit();
#endif
   },UiKit.Soft);
  }
  void AuthScreen(){
   ui.Box(460,230,680,390,UiKit.Panel);
   ui.Text("输入名字",500,258,600,60,34);
   username=ui.Field("名字",500,354,600,value:playerName);username.characterLimit=24;
   ui.Button("进入",500,456,600,64,Authenticate);
  }
  void LocalResultScreen(){
   var world=BattleWorld.Instance;if(world==null)return;int winner=world.Winner.Value;
   ui.Text(winner<0?"平局":winner==0?"蓝队胜利":"红队胜利",90,165,1350,80,52,winner<0?Draw2D.Gold:winner==0?Draw2D.Blue:Draw2D.Red);
   ui.Text("玩家",100,270,650,45,26);ui.Text("击杀            死亡            伤害",800,270,600,45,26);
   int i=0;foreach(var p in NetPlayer.All.OrderBy(p=>p.Team.Value).ThenBy(p=>p.Seat.Value)){
    ui.Text((p.Team.Value==0?"蓝":"红")+(p.Seat.Value+1)+"  "+p.DisplayName.Value,100,330+i*43,700,40,25,p.Team.Value==0?Draw2D.Blue:Draw2D.Red);
    ui.Text(p.Kills.Value+"              "+p.Deaths.Value+"              "+p.DamageDone.Value,800,330+i*43,600,40,25);i++;
   }
   ui.Button("再来一局",100,755,600,60,()=>LocalMatchRuntime.Instance.Restart()).interactable=LocalMatchRuntime.Instance.IsHost;
   ui.Button("返回",1120,755,300,60,LeaveLocalMatch,UiKit.Soft);
  }
  void RoomScreen(){
   if(Room==null)return;
   ui.Text("2v2 房间",68,190,1000,60,34);
   ui.Text(Room.status=="Waiting"?User.username:Room.status=="Starting"?"进入对局…":"对局中",68,255,1000,50,23,UiKit.Soft);
   for(int t=0;t<2;t++){
    int team=t;float x=t==0?68:550;ui.Box(x,330,450,420,UiKit.Panel);ui.Text(t==0?"蓝队":"红队",x+24,347,400,50,29,t==0?Draw2D.Blue:Draw2D.Red);
    for(int s=0;s<Room.size;s++){
     int seat=s;var m=Room.members.FirstOrDefault(p=>p.team==team&&p.seat==seat);
     string text=(team==0?"A":"B")+(s+1)+"   "+(m==null?"AI":m.username+(m.isBot==1?"  [AI]":m.userId==User.id?"  ●":""));
     var b=ui.Button(text,x+24,410+s*78,402,62,()=>Run<RoomData>("PUT","/api/rooms/"+Room.id+"/seat",new SeatBody{team=team,seat=seat},r=>{lastRoomJson="";ReceiveRoom(r);}),m?.userId==User.id?Draw2D.Gold:UiKit.Soft);
     b.interactable=Room.status=="Waiting"&&(m==null||m.isBot==1||m.userId==User.id);
     if(s<Room.size-1)ui.Text("│",x+40,470+s*78,100,19,22,t==0?Draw2D.Blue:Draw2D.Red);
    }
   }
   ui.Box(1040,330,460,420,UiKit.Panel);ui.Text(GameConfig.Current.Map(Room.mapId)?.name??Room.mapId,1068,350,400,52,30);
   if(Room.hostId==User.id&&Room.status=="Waiting"&&GameConfig.Current.maps.Count(m=>m.available)>1){
    ui.Button("切换地图",1068,526,392,36,()=>{
     var maps=GameConfig.Current.maps.Where(m=>m.available).ToArray();int index=Array.FindIndex(maps,m=>m.id==Room.mapId);
     Run<RoomData>("PUT","/api/rooms/"+Room.id+"/config",new RoomConfigBody{size=Room.size,mapId=maps[(index+1)%maps.Length].id},r=>{lastRoomJson="";ReceiveRoom(r);});
    },UiKit.Soft);
   }
   ui.Button("开始游戏",1068,643,392,70,()=>Run<MatchData>("POST","/api/rooms/"+Room.id+"/start",null,m=>{ActiveMatch=m.id;message="";StartCoroutine(LoadRoom(Room.id));})).interactable=Room.status=="Waiting";
   for(int i=0;i<4;i++){int skin=i;ui.Button((User.skin==i?"● ":"")+"外观 "+(i+1),1068+(i%2)*202,430+(i/2)*64,190,50,()=>Run<UserProfile>("PATCH","/api/users/me/cosmetic",new SkinBody{skin=skin},p=>{User=p;Redraw();}),UiKit.Soft).interactable=Room.status=="Waiting";}
   ui.Button("退出",68,770,300,54,()=>Run<Empty>("POST","/api/rooms/"+Room.id+"/leave",null,_=>Logout()),UiKit.Soft).interactable=Room.status=="Waiting";
   if(Room.status=="InGame")ui.Button("重新连接",750,770,300,54,()=>{attemptedMatch="";StartCoroutine(ConnectBattle());},Draw2D.Blue);
  }
  void ResultScreen(){
   if(result==null)return;
   if(result.status=="Aborted")ui.Text("对局已中止",68,230,1400,100,54);
   else{
    var r=result.result;ui.Text(r.winner<0?"平局":r.winner==0?"蓝队胜利":"红队胜利",68,200,1400,100,56,r.winner==0?Draw2D.Blue:r.winner==1?Draw2D.Red:Draw2D.Gold);
    ui.Text((GameConfig.Current.Map(result.mapId)?.name??"")+"    /    "+Mathf.RoundToInt(r.duration)+" 秒",72,303,1400,55,25,UiKit.Soft);
    ui.Box(68,390,1432,50,UiKit.Panel);ui.Text("玩家 / 队伍与位置",92,390,800,50);ui.Text("击杀          死亡          伤害",1060,390,420,50);
    for(int i=0;i<r.players.Length;i++){
     var p=r.players[i];var m=match?.players?.FirstOrDefault(m=>m.userId==p.userId)??Room?.members.FirstOrDefault(m=>m.userId==p.userId);
     ui.Text((m?.username??p.userId)+"   "+(m?.team==0?"蓝 A":"红 B")+((m?.seat??0)+1),92,450+i*37,900,36,23,m?.team==0?Draw2D.Blue:Draw2D.Red);
     ui.Text(p.kills+"                 "+p.deaths+"                 "+p.damage,1070,450+i*37,420,36,23);
    }
   }
   ui.Button("返回房间",68,770,480,64,ReturnToRoom);
  }
  void ReturnToRoom(){ActiveMatch="";attemptedMatch="";match=null;result=null;Screen=Room==null?"Auth":"Room";lastRoomJson="";Redraw();}
  void DisconnectBattle(){connecting=false;Bootstrap.Network.Shutdown();Screen=Room==null?"Auth":"Room";message="";Redraw();}
  void Expired(){realtime.Disconnect();Bootstrap.Network.Shutdown();User=null;Room=null;ActiveMatch="";attemptedMatch="";busy=connecting=false;Screen="Auth";message="请重新登录";Redraw();}
  void Logout(){Run<Empty>("POST","/api/auth/logout",null,_=>{Api.Forget();realtime.Disconnect();User=null;Room=null;ActiveMatch="";attemptedMatch="";Screen="Menu";Redraw();});}
  void OnDestroy(){disposed=true;if(Api!=null)Api.SessionExpired-=Expired;if(Bootstrap.Network!=null){Bootstrap.Network.OnClientConnectedCallback-=Connected;Bootstrap.Network.OnClientDisconnectCallback-=Disconnected;}if(ui!=null)Destroy(ui.gameObject);}
 }
}
