using System;
using System.Collections;
using System.Linq;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;
namespace BoundShoot {
 public partial class GameClient:MonoBehaviour {
  public static GameClient Instance;public HttpApi Api;public RoomData Room;public UserProfile User;
  public string Screen="Title";public string ActiveMatch="";
  UiKit ui;LobbyRealtime realtime;Text notice,hud;InputField username,localAddress;
  public int SelectedSkin {get;private set;}public int LocalTeamSize=2;int localPhase=-1;string localRoster="";
  bool settings,returningToMenu;public bool InputBlocked=>returningToMenu||settings||!(Screen=="Training"||Screen=="Battle"||Screen=="LocalBattle");
  string lastRoomJson="",attemptedMatch="",message="",playerName="";bool busy,connecting,resultLoading;
  MatchData match;ResultData result;float nextProfile,nextRealtimeRetry;bool disposed;
  AudioSource musicSource;
  AudioClip menuMusic,battleMusic;
  void Start(){
   Instance=this;SelectedSkin=Mathf.Clamp(PlayerPrefs.GetInt("BoundShoot.Skin",0),0,7);Api=gameObject.AddComponent<HttpApi>();
   Api.BaseUrl=NetworkSettings.Current.BackendUrl;Api.SessionExpired+=Expired;
#if UNITY_EDITOR || DEVELOPMENT_BUILD || BOUND_SHOOT_QA
   string overrideUrl=GetArg("-backend");if(!string.IsNullOrEmpty(overrideUrl))Api.BaseUrl=overrideUrl;
#endif
   realtime=gameObject.AddComponent<LobbyRealtime>();realtime.OnEvent=e=>{if(e.room!=null&&Room!=null&&e.room.id==Room.id)ReceiveRoom(e.room);};
   var go=new GameObject("UI",typeof(RectTransform));ui=go.AddComponent<UiKit>();ui.Initialize();
   Bootstrap.Network.OnClientConnectedCallback+=Connected;Bootstrap.Network.OnClientDisconnectCallback+=Disconnected;
   if(!Application.isBatchMode){
    menuMusic=Resources.Load<AudioClip>("ForestAssembly");
    battleMusic=Resources.Load<AudioClip>("RopeRush");
    musicSource=gameObject.AddComponent<AudioSource>();
    musicSource.playOnAwake=false;
    musicSource.loop=true;
    UpdateMusic();
   }
   Redraw();StartCoroutine(Poll());
   // Every process owns its session. Four windows must never inherit one shared account.
  }
  public static string GetArg(string flag){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,flag);return i>=0&&i+1<args.Length?args[i+1]:null;}
  void SignedIn(AuthData auth){
   lobby=null;lobbyPage=0;lastLobbyJson="";fetchingLobby=false;
   Api.SetTokens(auth);User=auth.user;Room=string.IsNullOrEmpty(auth.room?.id)?null:auth.room;lastRoomJson="";Screen=Room==null?"Lobby":"Room";message="";Redraw();
   nextLobbyRefresh=0;
   if(Room!=null)ReceiveRoom(Room);
   realtime.Connect(Api.BaseUrl,Api.AccessToken);StartCoroutine(Resume());
  }
  IEnumerator Resume(){
   string uid=User?.id;
   yield return Api.Request<UserProfile>("GET","/api/users/me",null,p=>{if(User?.id!=uid)return;User=p;if(!string.IsNullOrEmpty(p.roomId))StartCoroutine(LoadRoom(p.roomId));},Error);
  }
  IEnumerator LoadRoom(string rid){string uid=User?.id;yield return Api.Request<RoomData>("GET","/api/rooms/"+rid,null,r=>{if(User!=null&&User.id==uid)ReceiveRoom(r);},Error);}
  void Error(string text){message=text;busy=false;if(Screen=="LobbyConnecting"){Screen="Menu";Redraw();}if(notice!=null)notice.text=text;}
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
     if(Screen=="Lobby"&&Time.unscaledTime>=nextLobbyRefresh)yield return FetchLobby();
    }
    yield return new WaitForSecondsRealtime(1);
   }
  }
  void Run<T>(string method,string path,object body,Action<T> done){
   if(busy)return;busy=true;message="";if(Screen=="Room")Redraw();StartCoroutine(Request());
   IEnumerator Request(){yield return Api.Request<T>(method,path,body,v=>{busy=false;done(v);},Error);busy=false;if(Screen=="Room")Redraw();}
  }
  void Authenticate(){
   string name=username.text.Trim();
   EnterName(name);
  }
  public void EnterName(string name){
   name=(name??"").Trim();
   if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^[a-zA-Z0-9_\u4e00-\u9fff]{1,24}$")){Error("名字需为1–24位汉字、字母、数字或下划线");return;}
   playerName=name;PlayerPrefs.SetString("BoundShoot.Name",name);message="";Screen="Menu";Redraw();
  }
  void JoinBattle(){
   if(busy)return;
   Screen="LobbyConnecting";Redraw();
   Run<AuthData>("POST","/api/auth/guest",new NameBody{name=playerName,skin=SelectedSkin},SignedIn);
  }
  public void StartTraining(){if(!busy)StartCoroutine(EnterTraining());}
  IEnumerator EnterTraining(){
   busy=true;message="";Screen="TrainingStarting";Redraw();
   if(User!=null||Bootstrap.Network.IsListening){busy=false;Screen="Menu";Error("请先退出当前对局");Redraw();yield break;}
   var runtime=gameObject.AddComponent<TrainingRuntime>();yield return runtime.Begin(playerName,SelectedSkin,TrainingSize);
   if(runtime.Ready){Screen="Training";busy=false;Redraw();}
   else{string error=runtime.Error;yield return runtime.Stop();busy=false;Screen="Menu";message=error;Redraw();}
  }
  public void LeaveTraining(){if(!busy)StartCoroutine(ExitTraining());}
  public void SelectCharacter(int skin){if(Screen=="Auth"&&username!=null)playerName=username.text;SelectedSkin=Mathf.Clamp(skin,0,7);PlayerPrefs.SetInt("BoundShoot.Skin",SelectedSkin);Redraw();}
  public void StartLocalMatch(bool host=true,ushort port=7779){if(!busy)StartCoroutine(EnterLocal(host,port));}
  IEnumerator EnterLocal(bool host,ushort port){
   if(User!=null||Bootstrap.Network.IsListening){Error("请先退出当前房间");yield break;}
   string address=localAddress==null?"127.0.0.1":localAddress.text.Trim();
   busy=true;Screen="LocalStarting";Redraw();localPhase=-1;
   var runtime=gameObject.AddComponent<LocalMatchRuntime>();yield return runtime.Begin(playerName,SelectedSkin,LocalTeamSize,host,address,port,sessionMode!="Online");
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
  public void ReturnToMainMenu(){
   if(returningToMenu)return;
   StopAllCoroutines();StartCoroutine(ReturnToMenu());
  }
  IEnumerator ReturnToMenu(){
   returningToMenu=true;busy=true;settings=false;connecting=false;
   bool online=User!=null&&!string.IsNullOrEmpty(Api.AccessToken);string waitingRoom=Room?.status=="Waiting"?Room.id:null;
   string entered=Screen=="Auth"&&username!=null?username.text:playerName;
   if(System.Text.RegularExpressions.Regex.IsMatch(entered??"","^[a-zA-Z0-9_\\u4e00-\\u9fff]{1,24}$")){playerName=entered;PlayerPrefs.SetString("BoundShoot.Name",playerName);}
   if(string.IsNullOrWhiteSpace(playerName))playerName=PlayerPrefs.GetString("BoundShoot.Name","玩家");
   User=null;Room=null;ActiveMatch=attemptedMatch="";match=null;result=null;resultLoading=false;realtime.Disconnect();
   Screen="Returning";message="";Redraw();
   if(TrainingRuntime.Instance!=null)yield return TrainingRuntime.Instance.Stop();
   if(LocalMatchRuntime.Instance!=null)yield return LocalMatchRuntime.Instance.Stop();
   if(Bootstrap.Network.IsListening||Bootstrap.Network.ShutdownInProgress){Bootstrap.Network.Shutdown();while(Bootstrap.Network.IsListening||Bootstrap.Network.ShutdownInProgress)yield return null;}
   bool cleanupFailed=false;
   if(online){
    if(waitingRoom!=null)yield return Api.Request<Empty>("POST","/api/rooms/"+waitingRoom+"/leave",null,_=>{},_=>cleanupFailed=true,false);
    if(!string.IsNullOrEmpty(Api.AccessToken))yield return Api.Request<Empty>("POST","/api/auth/logout",null,_=>{},_=>cleanupFailed=true,false);
   }
   Api.Forget();localPhase=-1;localRoster=lastRoomJson="";nextProfile=nextRealtimeRetry=0;fetchingLobby=false;lobby=null;lastLobbyJson="";
   message=cleanupFailed?"已返回主菜单，在线会话清理失败":"";Screen="Menu";returningToMenu=false;busy=false;Redraw();StartCoroutine(Poll());
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
   if(returningToMenu||LocalMatchRuntime.Instance!=null||Bootstrap.Network.IsServer||!Bootstrap.Network.IsConnectedClient)return;connecting=false;Screen="Battle";message="";Redraw();
  }
  void Disconnected(ulong cid){
   if(returningToMenu||Screen=="Result"||disposed)return;
   if(Screen=="Battle"||Screen=="Connecting"){
    connecting=false;Screen="Room";message=string.IsNullOrEmpty(Bootstrap.Network.DisconnectReason)?"连接已断开":Bootstrap.Network.DisconnectReason;Redraw();
   }
  }
  IEnumerator LoadResult(){
   resultLoading=true;ResultData data=null;
   yield return Api.Request<ResultData>("GET","/api/matches/"+ActiveMatch+"/result",null,v=>data=v,Error);
   resultLoading=false;
   if(data!=null&&(data.status=="Finished"||data.status=="Aborted")){
    result=data;resultDetails=false;Screen="Result";connecting=false;Bootstrap.Network.Shutdown();Redraw();
   }
  }
  void Update(){
   UpdateMusic();
   if(Screen=="Battle"&&showingOnlineWarmup!=(BattleWorld.Instance!=null&&BattleWorld.Instance.IsOnlineWarmup))Redraw();
   if(!returningToMenu&&Input.GetKeyDown(KeyCode.Escape)){settings=!settings;Redraw();}
   var localSession=LocalMatchRuntime.Instance;
   if(localSession!=null&&!busy){
    if(!string.IsNullOrEmpty(localSession.Error)){message=localSession.Error;LeaveLocalMatch();return;}
    if(localSession.Ready&&localPhase!=localSession.World.Phase.Value){localPhase=localSession.World.Phase.Value;Screen=localPhase==0?"LocalRoom":localPhase==3?"LocalResult":"LocalBattle";if(localPhase==3)resultDetails=false;Redraw();}
    if(localSession.Ready&&Screen=="LocalRoom"){
     string roster=localSession.World.MapKey.Value.ToString()+"|"+string.Join("|",NetPlayer.All.Select(p=>p.UserId.Value.ToString()+p.DisplayName.Value.ToString()+p.Skin.Value+p.IsBot.Value+p.Team.Value+p.Seat.Value+p.Connected.Value));
     if(roster!=localRoster){localRoster=roster;Redraw();}
    }
   }
   if(Screen=="Auth"&&username!=null&&(Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.KeypadEnter)))Authenticate();
   UpdateForestHud();
  }
  void UpdateMusic(){
   if(musicSource==null)return;
   var world=BattleWorld.Instance;
   bool fighting=(Screen=="Battle"||Screen=="LocalBattle")&&world!=null&&world.Phase.Value==2&&!world.IsOnlineWarmup;
   var clip=fighting?battleMusic:menuMusic;
   musicSource.volume=(fighting?.28f:.34f)*PlayerPrefs.GetFloat("BoundShoot.MusicVolume",PlayerPrefs.GetInt("BoundShoot.Sound",1));
   if(musicSource.clip==clip)return;
   musicSource.clip=clip;
   if(clip!=null)musicSource.Play();
  }
  void Redraw()=>DrawForestUI();
  void ResultScreen(){
   if(result==null)return;
   if(result.status=="Aborted")ui.Text("对局已中止",68,230,1400,100,54);
   else if(result.result!=null)
    DrawMatchResult(result.result.winner,result.result.players??System.Array.Empty<PlayerResult>(),match?.players??Room?.members??System.Array.Empty<RoomMember>(),User?.id);
   ui.Button("返回房间",68,770,480,64,ReturnToRoom);
  }
  void ReturnToRoom(){ActiveMatch="";attemptedMatch="";match=null;result=null;Screen=Room==null?"Lobby":"Room";lastRoomJson="";Redraw();}
  void DisconnectBattle(){connecting=false;Bootstrap.Network.Shutdown();Screen=Room==null?"Auth":"Room";message="";Redraw();}
  void Expired(){if(returningToMenu)return;realtime.Disconnect();Bootstrap.Network.Shutdown();User=null;Room=null;ActiveMatch="";attemptedMatch="";busy=connecting=false;Screen="Auth";message="请重新登录";Redraw();}
  void Logout(){Run<Empty>("POST","/api/auth/logout",null,_=>{Api.Forget();realtime.Disconnect();User=null;Room=null;ActiveMatch="";attemptedMatch="";Screen="Menu";Redraw();});}
  void OnDestroy(){disposed=true;if(Api!=null)Api.SessionExpired-=Expired;if(Bootstrap.Network!=null){Bootstrap.Network.OnClientConnectedCallback-=Connected;Bootstrap.Network.OnClientDisconnectCallback-=Disconnected;}if(ui!=null)Destroy(ui.gameObject);}
 }
}
