using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace BoundShoot {
 public partial class GameClient {
  LobbyData lobby;
  int lobbyPage;
  float nextLobbyRefresh;
  bool fetchingLobby;
  string joinCode="",lastLobbyJson="";

  IEnumerator FetchLobby(){
   if(fetchingLobby||User==null||Screen!="Lobby")yield break;
   fetchingLobby=true;nextLobbyRefresh=Time.unscaledTime+5;
   string uid=User.id;int page=lobbyPage;
   yield return Api.Request<LobbyData>("GET","/api/rooms?page="+page,null,data=>{
    if(User?.id!=uid||Screen!="Lobby"||lobbyPage!=page)return;
    if(page>0&&page*6>=data.total){lobbyPage=Mathf.Max(0,(data.total-1)/6);nextLobbyRefresh=0;return;}
    lobby=data;string json=JsonUtility.ToJson(data);
    if(json!=lastLobbyJson){lastLobbyJson=json;Redraw();}
   },e=>{if(User?.id==uid&&Screen=="Lobby")Error(e);});
   fetchingLobby=false;
  }

  void LobbyScreen(){
   ui.Text("联机大厅",220,185,700,58,36);
   ui.Text(playerName,1010,190,330,50,25,UiKit.Soft,null,TextAnchor.MiddleRight);
   var rows=lobby?.rooms??System.Array.Empty<LobbyRoom>();
   if(rows.Length==0)ui.Text(lobby==null?"加载房间…":"暂无房间",100,400,950,80,30,UiKit.Soft,null,TextAnchor.MiddleCenter);
   for(int i=0;i<rows.Length;i++){
    var row=rows[i];float y=270+i*77;
    ui.Box(90,y,980,68,UiKit.Panel);
    ui.Text(row.code+"  ·  "+row.hostName,110,y+3,410,32,23);
    ui.Text((GameConfig.Current.Map(row.mapId)?.name??row.mapId)+"  ·  "+row.size+"v"+row.size,110,y+34,490,30,20,UiKit.Soft);
    ui.Text(row.humans+" / "+row.size*2+" 人",595,y+12,175,42,24);
    bool joinable=row.status=="Waiting"&&row.humans<row.size*2;
    ui.Button(row.status=="Waiting"?(joinable?"加入":"已满"):"对局中",805,y+10,240,48,()=>JoinRoom(row.code),UiKit.Soft).interactable=joinable&&!busy;
   }
   ui.Box(1120,270,390,460,UiKit.Panel);
   ui.Button("创建房间",1145,295,340,68,()=>Run<RoomData>("POST","/api/rooms",null,EnterOnlineRoom));
   ui.Text("房间号",1145,400,340,35,24);
   var code=ui.Field("6位数字",1145,448,340,value:joinCode);code.characterLimit=6;code.contentType=InputField.ContentType.IntegerNumber;
   code.onValueChanged.AddListener(value=>joinCode=value);
   ui.Button("按房间号加入",1145,525,340,60,()=>JoinRoom(joinCode),UiKit.Soft);
   ui.Button("刷新",1145,630,340,54,()=>{nextLobbyRefresh=0;StartCoroutine(FetchLobby());},UiKit.Soft);
   ui.Button("返回主菜单",90,765,300,60,ReturnToMainMenu,UiKit.Soft);
   if(lobby!=null&&lobby.total>6){
    ui.Button("上一页",450,765,180,54,()=>ChangeLobbyPage(-1),UiKit.Soft).interactable=lobbyPage>0;
    ui.Text((lobbyPage+1)+" / "+Mathf.CeilToInt(lobby.total/6f),640,770,180,45,23,null,null,TextAnchor.MiddleCenter);
    ui.Button("下一页",835,765,180,54,()=>ChangeLobbyPage(1),UiKit.Soft).interactable=(lobbyPage+1)*6<lobby.total;
   }
  }
  void ChangeLobbyPage(int delta){lobbyPage+=delta;nextLobbyRefresh=0;StartCoroutine(FetchLobby());}
  void JoinRoom(string code){
   if(!System.Text.RegularExpressions.Regex.IsMatch(code??"","^[0-9]{6}$")){Error("请输入6位房间号");return;}
   Run<RoomData>("POST","/api/rooms/join",new JoinBody{code=code},EnterOnlineRoom);
  }
  void EnterOnlineRoom(RoomData room){lastRoomJson="";Screen="Room";message="";ReceiveRoom(room);}
  void LeaveOnlineRoom(){
   if(Room==null||Room.status!="Waiting")return;
   Run<Empty>("POST","/api/rooms/"+Room.id+"/leave",null,_=>{
    Room=null;lastRoomJson="";ActiveMatch=attemptedMatch="";match=null;result=null;
    Screen="Lobby";nextLobbyRefresh=0;lastLobbyJson="";Redraw();
   });
  }
  void ConfigureOnlineSize(int size){
   if(Room==null||Room.size==size)return;
   Run<RoomData>("PUT","/api/rooms/"+Room.id+"/config",new RoomConfigBody{size=size,mapId=Room.mapId},EnterOnlineRoom);
  }

  void RoomScreen(){
   if(Room==null||User==null)return;
   bool waiting=Room.status=="Waiting",host=Room.hostId==User.id;
   ui.Text("房间 "+Room.code+"  ·  "+Room.size+"v"+Room.size,220,185,1100,58,34);
   ui.Text(waiting?"全员准备后进入热身场":Room.status=="Starting"?"正在进入热身场…":"对局中",220,245,1120,44,23,UiKit.Soft);
   for(int t=0;t<2;t++){
    int team=t;float x=90+t*490;
    ui.Box(x,315,460,400,UiKit.Panel);ui.Text(t==0?"蓝队":"红队",x+20,328,420,45,28,t==0?Draw2D.Blue:Draw2D.Red);
    for(int s=0;s<Room.size;s++){
     int seat=s;var m=Room.members.FirstOrDefault(p=>p.team==team&&p.seat==seat);float y=390+s*77;
     ui.Button("",x+15,y,430,67,()=>Run<RoomData>("PUT","/api/rooms/"+Room.id+"/seat",new SeatBody{team=team,seat=seat},EnterOnlineRoom),m?.userId==User.id?new Color(.31f,.43f,.28f):UiKit.Panel).interactable=waiting&&!busy&&(m==null||m.isBot==1);
     ui.Picture(VegetableArt.Get("portrait"+(m?.skin??0)),x+25,y+3,58,58);
     string label=m?.isBot==1?" · AI":(m?.userId==Room.hostId?" · 房主":"")+(waiting&&m?.ready==1?" · 已准备":"");
     ui.Text((m?.username??"AI")+label,x+100,y+10,325,48,23);
    }
   }
   ui.Box(1090,315,420,400,UiKit.Panel);
   ui.Text(GameConfig.Current.Map(Room.mapId)?.name??Room.mapId,1110,330,380,48,28);
   ui.Button("选择地图",1115,402,370,54,()=>OpenMapPicker(Room.mapId),UiKit.Soft).interactable=waiting&&host&&!busy;
   for(int size=2;size<=4;size++){
    int n=size;ui.Button(n+"v"+n,1115+(n-2)*126,485,118,50,()=>ConfigureOnlineSize(n),Room.size==n?Draw2D.Gold:UiKit.Soft).interactable=waiting&&host&&!busy;
   }
   ui.Text("空位自动补齐 AI",1115,552,370,42,22,UiKit.Soft);
   if(waiting){
    bool ready=Room.members.Any(m=>m.userId==User.id&&m.ready==1);
    if(host&&Room.members.All(m=>m.ready==1))
     ui.Button("重试进入热身场",1115,690,370,48,()=>Run<MatchData>("POST","/api/rooms/"+Room.id+"/start",null,m=>{ActiveMatch=m.id;StartCoroutine(LoadRoom(Room.id));})).interactable=!busy;
    ui.Button(ready?"取消准备":"准备",1115,623,370,64,()=>Run<RoomData>("PUT","/api/rooms/"+Room.id+"/ready",new ReadyBody{ready=!ready},ReceiveRoom)).interactable=!busy;
   }
   ui.Button("返回大厅",90,765,300,60,LeaveOnlineRoom,UiKit.Soft).interactable=waiting&&!busy;
   if(Room.status=="InGame")ui.Button("重新连接",1115,765,370,60,()=>{attemptedMatch="";StartCoroutine(ConnectBattle());},Draw2D.Blue);
  }
 }
}
