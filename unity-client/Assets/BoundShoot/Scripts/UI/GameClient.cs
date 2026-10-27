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
  UiKit ui;LobbyRealtime realtime;Text notice,hud;InputField username,password,confirm,code;
  string lastRoomJson="",attemptedMatch="",message="";bool busy,register,connecting,resultLoading;
  MatchData match;ResultData result;float nextProfile,nextRealtimeRetry;bool disposed;
  void Start(){
   Instance=this;Api=gameObject.AddComponent<HttpApi>();
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
   Api.SetTokens(auth);User=auth.user;register=false;Screen="Lobby";message="";Redraw();
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
      if(e.Contains("房间不存在")||e.Contains("不在此房间")){Room=null;Screen="Lobby";Redraw();}else Error(e);
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
   if(register&&password.text!=confirm.text){Error("两次密码不一致");return;}
   Run<AuthData>("POST",register?"/api/auth/register":"/api/auth/login",new Credentials{username=username.text,password=password.text},SignedIn);
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
   if(Bootstrap.Network.IsServer||!Bootstrap.Network.IsConnectedClient)return;connecting=false;Screen="Battle";message="";Redraw();
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
   if(Screen!="Battle"||hud==null)return;var world=BattleWorld.Instance;if(world==null){hud.text="加载中…";return;}
   var local=NetPlayer.All.Find(p=>p.IsOwner&&p.IsClient);
   string phase=world.Phase.Value==0?"等待玩家":world.Phase.Value==1?"倒计时":"剩余时间";
   var c=GameConfig.Current;
   string equipped=local==null?"":(c.Weapon((WeaponKind)local.Weapon.Value)?.name??"空手");
   string carried=local==null?"":(c.Item((ItemKind)local.Item.Value)?.name??"无");
   hud.text=phase+"  "+Mathf.CeilToInt(world.Clock.Value)+"s     蓝 "+NetPlayer.All.Count(p=>p.Team.Value==0&&p.Alive)+" : "+NetPlayer.All.Count(p=>p.Team.Value==1&&p.Alive)+" 红\n"+
    (local==null?"加载中…":"生命 "+local.Health.Value+"/"+local.MaxHealth.Value+"    护盾 "+local.Shield.Value+"    "+equipped+"    E："+carried+"    K/D "+local.Kills.Value+"/"+local.Deaths.Value);
  }
  void Redraw(){
   if(ui==null)return;ui.Clear();notice=null;hud=null;
   if(Screen=="Battle"){
    ui.Box(26,20,1548,110,new Color(.055f,.08f,.12f,.92f));hud=ui.Text("",50,28,1500,96,24);
    float foot=Mathf.Max(826,ui.Root.rect.height-74);
    ui.Button("离开",1445,foot+9,100,36,DisconnectBattle,UiKit.Soft);return;
   }
   ui.Backdrop(UiKit.Ink);
   ui.Text("派对射击",64,42,700,60,42);
   if(Screen=="Auth")AuthScreen();
   if(Screen=="Lobby")LobbyScreen();
   if(Screen=="Room")RoomScreen();
   if(Screen=="Connecting")ui.Text("连接中…",100,270,1000,80,40);
   if(Screen=="Result")ResultScreen();
   notice=ui.Text(message,68,827,1460,52,22,new Color(1,.65f,.42f));
  }
  void AuthScreen(){
   ui.Box(460,190,680,590,UiKit.Panel);
   ui.Text(register?"注册":"登录",500,210,600,60,34);
   username=ui.Field("用户名",500,300,600);
   password=ui.Field("密码",500,373,600,true);
   confirm=register?ui.Field("确认密码",500,446,600,true):null;
   ui.Button(register?"注册":"登录",500,register?536:459,600,64,Authenticate);
   ui.Button(register?"返回登录":"注册账号",500,register?621:544,600,52,()=>{register=!register;message="";Redraw();},UiKit.Soft);
  }
  void LobbyScreen(){
   if(User==null)return;
   ui.Box(68,210,700,560,UiKit.Panel);ui.Text(User.username,104,240,600,64,38);
   ui.Text("对局 "+User.games+"     胜利 "+User.wins+"     K/D "+User.kills+"/"+User.deaths,108,320,600,60,24,UiKit.Soft);
   ui.Text("外观",108,416,600,50,26);
   for(int i=0;i<4;i++){int skin=i;ui.Button((User.skin==i?"● ":"")+"外观 "+(i+1),108+i*155,488,140,80,()=>Run<UserProfile>("PATCH","/api/users/me/cosmetic",new SkinBody{skin=skin},p=>{User=p;Redraw();}),new[]{Draw2D.Gold,new Color(.75f,.5f,1),new Color(.4f,1,.75f),new Color(1,.55f,.8f)}[i]);}
   ui.Button("创建房间",850,240,650,90,()=>Run<RoomData>("POST","/api/rooms",null,r=>{lastRoomJson="";ReceiveRoom(r);}));
   code=ui.Field("输入 6 位房间号",850,390,650);
   ui.Button("加入房间",850,470,650,70,()=>Run<RoomData>("POST","/api/rooms/join",new JoinBody{code=code.text.Trim()},r=>{lastRoomJson="";ReceiveRoom(r);}),Draw2D.Blue);
   ui.Button("最近战绩",850,596,310,60,()=>Run<HistoryData>("GET","/api/users/me/matches",null,h=>Error(h.matches.Length==0?"暂无战绩":string.Join("   |   ",h.matches.Take(3).Select(m=>(m.status=="Aborted"?"中止":m.winner<0?"平局":m.winner==m.team?"胜利":"失败")+" K/D "+m.kills+"/"+m.deaths)))) ,UiKit.Soft);
   ui.Button("退出账号",1190,596,310,60,Logout,UiKit.Soft);
  }
  void RoomScreen(){
   if(Room==null)return;
   ui.Text("房间 "+Room.code+"   /   "+Room.size+"v"+Room.size,68,190,1000,60,34);
   ui.Text(Room.status=="Waiting"?"准备中":Room.status=="Starting"?"连接中…":"对局中",68,255,1000,50,23,UiKit.Soft);
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
   if(Room.hostId==User.id&&Room.status=="Waiting"){
    ui.Button("切换地图",1068,526,392,36,()=>{
     var maps=GameConfig.Current.maps.Where(m=>m.available).ToArray();int index=Array.FindIndex(maps,m=>m.id==Room.mapId);
     Run<RoomData>("PUT","/api/rooms/"+Room.id+"/config",new RoomConfigBody{size=Room.size,mapId=maps[(index+1)%maps.Length].id},r=>{lastRoomJson="";ReceiveRoom(r);});
    },UiKit.Soft);
    ui.Button("2v2",1068,572,120,46,()=>Configure(2),UiKit.Soft);
    ui.Button("3v3",1204,572,120,46,()=>Configure(3),UiKit.Soft);
    ui.Button("4v4",1340,572,120,46,()=>Configure(4),UiKit.Soft);
    ui.Button("开始对战",1068,643,392,70,()=>Run<MatchData>("POST","/api/rooms/"+Room.id+"/start",null,m=>{ActiveMatch=m.id;message="连接中…";StartCoroutine(LoadRoom(Room.id));}));
   }else ui.Text(Room.status=="Waiting"?"等待房主":"连接中…",1068,607,400,80,25,Draw2D.Gold);
   ui.Button(self?.ready==1?"取消准备":"准备",68,770,330,54,()=>Run<RoomData>("PUT","/api/rooms/"+Room.id+"/ready",new ReadyBody{ready=self?.ready!=1},r=>{lastRoomJson="";ReceiveRoom(r);}),self?.ready==1?UiKit.Soft:Draw2D.Gold).interactable=Room.status=="Waiting";
   ui.Button("离开房间",420,770,300,54,()=>Run<Empty>("POST","/api/rooms/"+Room.id+"/leave",null,_=>{Room=null;ActiveMatch="";Screen="Lobby";Redraw();}),UiKit.Soft).interactable=Room.status=="Waiting";
   if(Room.status=="InGame")ui.Button("重新连接",750,770,300,54,()=>{attemptedMatch="";StartCoroutine(ConnectBattle());},Draw2D.Blue);
  }
  void Configure(int size)=>Run<RoomData>("PUT","/api/rooms/"+Room.id+"/config",new RoomConfigBody{size=size,mapId=Room.mapId},r=>{lastRoomJson="";ReceiveRoom(r);});
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
  void ReturnToRoom(){ActiveMatch="";attemptedMatch="";match=null;result=null;Screen=Room==null?"Lobby":"Room";lastRoomJson="";Redraw();}
  void DisconnectBattle(){connecting=false;Bootstrap.Network.Shutdown();Screen=Room==null?"Lobby":"Room";message="";Redraw();}
  void Expired(){realtime.Disconnect();Bootstrap.Network.Shutdown();User=null;Room=null;ActiveMatch="";attemptedMatch="";busy=connecting=false;Screen="Auth";message="请重新登录";Redraw();}
  void Logout(){Run<Empty>("POST","/api/auth/logout",null,_=>{Api.Forget();realtime.Disconnect();User=null;Room=null;Screen="Auth";Redraw();});}
  void OnDestroy(){disposed=true;if(Api!=null)Api.SessionExpired-=Expired;if(Bootstrap.Network!=null){Bootstrap.Network.OnClientConnectedCallback-=Connected;Bootstrap.Network.OnClientDisconnectCallback-=Disconnected;}if(ui!=null)Destroy(ui.gameObject);}
 }
}
