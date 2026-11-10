using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace BoundShoot {
 public partial class GameClient {
  RectTransform heatPanel,heatBlue,heatRed,reviveOverlay;
  RectTransform liveStats;
  Text killFeed;
  readonly System.Collections.Generic.Queue<(string text,float expires)> killNotices=new System.Collections.Generic.Queue<(string,float)>();
  BattleWorld feedWorld;
  uint feedRound;
  string liveStatsKey;
  bool resultDetails;
  Text heatSummary,battleStats,rescuePrompt;
  Image rescueRing;
  static Sprite rescueRingSprite;
  readonly System.Collections.Generic.List<Text> heatLabels=new System.Collections.Generic.List<Text>();
  readonly System.Collections.Generic.List<RectTransform> heatFills=new System.Collections.Generic.List<RectTransform>();
  RectTransform healthFill;
  readonly Image[] slotBorders=new Image[4];
  static readonly Color HudInk=new Color32(19,31,27,255), HudEdge=new Color32(23,17,25,255);
  static readonly Color HudGold=new Color32(250,205,91,255), HudMetal=new Color32(105,95,108,255);
  static readonly System.Collections.Generic.Dictionary<string,Sprite> HudIcons=new System.Collections.Generic.Dictionary<string,Sprite>();
  RectTransform HudBlock(float x,float y,float w,float h,Color color,Transform parent=null,bool interactive=false){
   var rect=ui.Box(x,y,w,h,color,parent);
   rect.GetComponent<Outline>().enabled=false;
   rect.GetComponent<Image>().raycastTarget=interactive;
   return rect;
  }
  RectTransform HudFrame(float x,float y,float w,float h,Color trim,bool corners=false,Transform parent=null){
   var frame=HudBlock(x,y,w,h,HudEdge,parent);
   HudBlock(3,3,w-6,h-6,trim,frame);
   HudBlock(7,7,w-14,h-14,HudInk,frame);
   if(corners)for(int i=0;i<4;i++){
    bool right=(i&1)!=0,bottom=(i&2)!=0;
    float cx=right?w-23:3,cy=bottom?h-23:3;
    HudBlock(cx,bottom?cy+14:cy,20,6,HudGold,frame);
    HudBlock(right?cx+14:cx,cy,6,20,HudGold,frame);
    HudBlock(right?cx+10:cx+6,bottom?cy+10:cy+6,4,4,new Color32(189,112,48,255),frame);
   }
   return frame;
  }
  Image HudIcon(string key,float x,float y,float w,float h,Transform parent){
   if(!HudIcons.TryGetValue(key,out var sprite)){
    string[] rows;
    switch(key){
     case "gear":rows=new[]{"...##...","..#++#..",".##++##.","#+#..#+#","#+#..#+#",".##++##.","..#++#..","...##..."};break;
     case "exit":rows=new[]{"..######","..#+++##","..#+++##",".##+++##","####+o##",".##+++##","..#+++##","..######"};break;
     case "clock":rows=new[]{"########",".oooooo.",".#++++#.","..#++#..","...##...","..#++#..",".#oooo#.","########"};break;
     case "swords":rows=new[]{"++....++","+++..+++",".++++++.","..++++..","...++...","o.++++.o",".o+..+o.","o.o..o.o"};break;
     default:rows=new[]{".####.","##++##","#++.+#","#+..+#","#++.+#","##++##",".####."};break;
    }
    var texture=new Texture2D(rows[0].Length,rows.Length,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
    for(int row=0;row<rows.Length;row++)for(int col=0;col<rows[row].Length;col++){
     char pixel=rows[row][col];
     texture.SetPixel(col,rows.Length-1-row,pixel=='#'?HudEdge:pixel=='o'?HudGold:pixel=='+'?(key=="exit"?new Color32(150,89,61,255):new Color32(214,219,223,255)):Color.clear);
    }
    texture.Apply(false,true);
    sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),Vector2.one*.5f);
    HudIcons.Add(key,sprite);
   }
   return ui.Picture(sprite,x,y,w,h,parent);
  }
  Button HudButton(string label,string icon,float x,float y,System.Action action){
   var button=ui.Button("",x,y,174,68,action,HudEdge);
   var face=HudBlock(4,4,166,58,new Color32(167,177,158,255),button.transform);
   button.targetGraphic=face.GetComponent<Image>();
   HudBlock(0,0,166,4,new Color32(222,225,205,255),face);
   HudBlock(0,52,166,6,new Color32(101,110,101,255),face);
   HudIcon(icon,13,12,34,34,face);
   ui.Text(label,59,0,98,56,25,HudEdge,face,TextAnchor.MiddleCenter);
   return button;
  }
  bool showingOnlineWarmup;
  Button warmupStart;
  string sessionMode="AI";public int TrainingSize;Image gunIcon;readonly Image[] slotBackgrounds=new Image[4],slotIcons=new Image[4];readonly Text[] slotLabels=new Text[4];Text ammoText,healthText,shieldText,reviveText,equipmentText,weaponName,countdownText;
  RectTransform weaponPopup;NetPlayer hudPlayer;readonly WeaponPickupNotice weaponNotice=new WeaponPickupNotice();
  public void OpenIdentity(){playerName=PlayerPrefs.GetString("BoundShoot.Name",playerName);Screen="Auth";Redraw();}
  public void SelectMode(string mode){
   sessionMode=mode;message="";
   if(mode=="Training"){TrainingSize=LocalTeamSize;Screen="TrainingRoom";Redraw();}
   else if(mode=="Online")JoinBattle();
   else StartLocalMatch();
  }
  public void SetRoomSize(int size){
   if(size<2||size>4)return;
   var runtime=LocalMatchRuntime.Instance;
   if(runtime!=null){if(!runtime.SetTeamSize(size)){Error("请先调整超出人数的位置");return;}}
   LocalTeamSize=size;if(Screen=="TrainingRoom")TrainingSize=size;Redraw();
  }
  NetPlayer[] SessionPlayers()=>NetPlayer.All.Where(p=>p.IsSpawned&&p.NetworkManager==Bootstrap.Network).ToArray();
  void ForestPage(string title){
   ui.Backdrop(UiKit.Ink);
   foreach(int side in new[]{0,1}){ui.Picture(ForestArt.Get("tree"),side==0?-95:1350,490,350,270);ui.Picture(ForestArt.Get("treeSmall"),side==0?0:1400,100,200,200);}
   ui.Text(title,200,56,1200,75,42,UiKit.White,null,TextAnchor.MiddleCenter);
   ui.Box(650,146,300,3,UiKit.Soft);
  }
  void DrawForestUI(){
   if(ui==null)return;ui.Clear();notice=null;hud=null;healthText=null;
   if(Screen=="Battle"||Screen=="LocalBattle"||Screen=="Training"){mapPickerOpen=false;DrawBattleUI();if(settings)SettingsPanel();return;}
   hudPlayer=null;weaponNotice.Clear();
   ForestPage(Screen=="Title"?"":Screen=="Auth"?"选择你的搭档":"绳紧兵大作战");
   switch(Screen){
    case "Title":
     ui.Text("绳紧兵大作战",300,230,1000,140,90,UiKit.White,null,TextAnchor.MiddleCenter);ui.Text("BOUND SHOOT",400,370,800,70,36,UiKit.Soft,null,TextAnchor.MiddleCenter);
     ui.Button("进入游戏",560,520,480,76,OpenIdentity);ui.Button("设置",560,620,225,56,()=>{settings=true;Redraw();},UiKit.Soft);ui.Button("退出",815,620,225,56,Quit,UiKit.Soft);
     for(int i=0;i<8;i++)ui.Picture(VegetableArt.Get("portrait"+i),330+i*120,725,95,105);break;
    case "Auth":AuthScreen();break;
    case "Menu":ModeScreen();break;
    case "OnlineMenu":OnlineScreen();break;
    case "Lobby":LobbyScreen();break;
    case "LobbyConnecting":ui.Text("正在连接大厅…",400,360,800,100,38,UiKit.White,null,TextAnchor.MiddleCenter);break;
    case "TrainingRoom":TrainingRoomScreen();break;
    case "LocalRoom":LocalRoomScreen();break;
    case "LocalResult":LocalResultScreen();break;
    case "Room":RoomScreen();break;
    case "Result":ResultScreen();break;
    case "Returning":ui.Text("正在离开…",400,360,800,100,38,UiKit.White,null,TextAnchor.MiddleCenter);break;
    default:ui.Text("进入房间…",400,360,800,100,38,UiKit.White,null,TextAnchor.MiddleCenter);break;
   }
   notice=ui.Text(message,250,842,1100,40,22,new Color(1,.75f,.45f),null,TextAnchor.MiddleCenter);
   if(CanPickMap&&mapPickerOpen)DrawMapPicker();
   else mapPickerOpen=false;
   if(settings)SettingsPanel();
  }
  void AuthScreen(){
   ui.Box(180,220,360,500,UiKit.Panel);ui.Picture(VegetableArt.Get("portrait"+SelectedSkin),230,255,260,265);
   ui.Text(VegetableArt.Name(SelectedSkin),210,535,300,45,28,null,null,TextAnchor.MiddleCenter);
   username=ui.Field("输入名字",210,615,300,value:playerName);username.characterLimit=24;
   for(int i=0;i<8;i++){
    int skin=i;float x=630+(i%4)*185,y=250+(i/4)*220;
    ui.Button("",x,y,160,190,()=>SelectCharacter(skin),SelectedSkin==i?Draw2D.Gold:UiKit.Panel);
    ui.Picture(VegetableArt.Get("portrait"+i),x+25,y+12,110,123);ui.Text(VegetableArt.Name(i),x,y+140,160,38,22,SelectedSkin==i?UiKit.Ink:UiKit.White,null,TextAnchor.MiddleCenter);
   }
   ui.Button("返回",180,765,250,60,()=>{Screen="Title";Redraw();},UiKit.Soft);ui.Button("确认进入",630,765,715,60,Authenticate);
  }
  void ModeScreen(){
   string[] titles={"训练房","本地AI对战","联机对战"},sub={"练习 · 静止伙伴","单人也能开战","和朋友一起"};
   for(int i=0;i<3;i++){
    int option=i;float x=170+i*430;ui.Button("",x,260,400,420,()=>SelectMode(option==0?"Training":option==1?"AI":"Online"),UiKit.Panel);
    if(i==0){ui.Picture(ForestArt.Get("heart"),x+150,305,100,100);ui.Picture(CyberArt.Get("weapon2"),x+95,405,210,90);}
    else{ui.Picture(VegetableArt.Get("portrait"+(i==1?6:0)),x+65,310,125,150);ui.Picture(VegetableArt.Get("portrait"+(i==1?7:1)),x+205,310,125,150);}
    ui.Text(titles[i],x,510,400,60,34,UiKit.White,null,TextAnchor.MiddleCenter);ui.Text(sub[i],x,585,400,45,23,UiKit.Soft,null,TextAnchor.MiddleCenter);
   }
   ui.Picture(VegetableArt.Get("portrait"+SelectedSkin),620,718,65,70);ui.Text(playerName,705,725,300,55,26);
   ui.Button("修改角色",170,765,280,58,()=>{Screen="Auth";Redraw();},UiKit.Soft);ui.Button("设置",1150,765,280,58,()=>{settings=true;Redraw();},UiKit.Soft);
   ui.Button("局域网联机",620,805,360,38,()=>{sessionMode="Online";Screen="OnlineMenu";Redraw();},UiKit.Soft);
  }
  void OnlineScreen(){
   ui.Box(460,235,680,460,UiKit.Panel);ui.Text("局域网房间",500,260,600,60,36,null,null,TextAnchor.MiddleCenter);
   ui.Button("创建房间",510,350,580,66,()=>StartLocalMatch());
   localAddress=ui.Field("房主地址",510,455,580,value:PlayerPrefs.GetString("BoundShoot.LocalAddress","127.0.0.1"));
   ui.Button("加入房间",510,555,580,66,()=>{PlayerPrefs.SetString("BoundShoot.LocalAddress",localAddress.text.Trim());StartLocalMatch(false);});
   ui.Button("返回",560,755,480,60,()=>{Screen="Menu";Redraw();},UiKit.Soft);
  }
  void SizeButtons(float y,bool enabled){for(int size=2;size<=4;size++){int value=size;ui.Button(size+"v"+size,530+(size-2)*190,y,160,50,()=>SetRoomSize(value),LocalTeamSize==size?Draw2D.Gold:UiKit.Soft).interactable=enabled;}}
  void TrainingRoomScreen(){
   ui.Text("训练房",300,177,1000,60,34,null,null,TextAnchor.MiddleCenter);
   for(int t=0;t<2;t++){
    float x=190+t*650;ui.Box(x,260,570,385,UiKit.Panel);ui.Text(t==0?"伙伴":"靶场",x+25,275,520,42,28,t==0?Draw2D.Blue:Draw2D.Red);
    for(int seat=0;seat<TrainingSize;seat++){
     float y=330+seat*73;ui.Picture(VegetableArt.Get("portrait"+(t==0&&seat==0?SelectedSkin:t*4+seat)),x+30,y,54,60);
     ui.Text(t==0&&seat==0?playerName:t==0?"伙伴 "+seat:"靶子 "+(seat+1),x+115,y,420,50,26);
     if(t==0&&seat<TrainingSize-1)ui.Text("│",x+52,y+48,35,25,20,Draw2D.Blue);
    }
   }
   SizeButtons(680,true);ui.Button("开始训练",560,760,480,64,StartTraining);ui.Button("返回",190,760,250,64,()=>{Screen="Menu";Redraw();},UiKit.Soft);
  }
  void LocalRoomScreen(){
   var runtime=LocalMatchRuntime.Instance;if(runtime==null||!runtime.Ready)return;var players=SessionPlayers();int size=runtime.TeamSize;LocalTeamSize=size;
   ui.Text((GameConfig.Current.Map(runtime.World.MapKey.Value.ToString())?.name??"林间高台")+"  ·  "+size+"v"+size,300,175,1000,60,34,null,null,TextAnchor.MiddleCenter);
   if(runtime.IsHost)ui.Button("选择地图",1190,180,220,54,()=>OpenMapPicker(runtime.World.MapKey.Value.ToString()),UiKit.Soft).interactable=!runtime.HasPendingSeats;
   for(int t=0;t<2;t++){
    float x=190+t*650;ui.Box(x,260,570,385,UiKit.Panel);ui.Text(t==0?"蓝队":"红队",x+25,272,520,42,28,t==0?Draw2D.Blue:Draw2D.Red);
    foreach(var p in players.Where(p=>p.Team.Value==t).OrderBy(p=>p.Seat.Value)){
     int team=t,seat=p.Seat.Value;float y=325+seat*75;
     ui.Button("",x+20,y,530,67,()=>runtime.World.RequestSeatServerRpc(team,seat),p.IsOwner&&!p.IsBot.Value?new Color(.31f,.43f,.28f):UiKit.Panel).interactable=p.IsBot.Value;
     ui.Picture(VegetableArt.Get("portrait"+p.Skin.Value),x+30,y+3,58,58);
     string name=p.IsBot.Value?(p.Connected.Value?p.DisplayName.Value+" · AI":"空位"):p.DisplayName.Value.ToString();ui.Text(name,x+110,y+5,410,50,25);
     if(seat<size-1)ui.Text("│",x+50,y+53,30,23,18,t==0?Draw2D.Blue:Draw2D.Red);
    }
   }
   SizeButtons(678,runtime.IsHost);
   ui.Button("开始对战",560,760,480,64,runtime.StartRound).interactable=runtime.CanStart;
   ui.Button("离开",190,760,250,64,LeaveLocalMatch,UiKit.Soft);
   if(runtime.IsHost&&sessionMode=="Online")ui.Button("补齐AI",1160,760,250,64,()=>{runtime.FillWithAI();Redraw();},UiKit.Soft);
  }
  void DrawBattleUI(){
   var world=LocalMatchRuntime.Instance?.World??BattleWorld.Instance;
   showingOnlineWarmup=Screen=="Battle"&&world!=null&&world.IsOnlineWarmup;
   warmupStart=null;
   if(showingOnlineWarmup){
    var guide=ui.Box(35,95,385,178,UiKit.Panel);
    ui.Text("WASD／方向键 移动\n鼠标瞄准 · 左键攻击或使用道具\n踩过物资拾取 · E 切换道具\n绳索连接队友 · 长按 F 救援",14,10,357,155,21,UiKit.White,guide);
    if(User!=null&&Room?.hostId==User.id)
     warmupStart=ui.Button("开始正式对战",35,755,245,52,()=>world.StartOnlineBattleServerRpc());
   }
   reviveOverlay=HudBlock(0,0,1600,900,new Color(.25f,.25f,.25f,.77f));
   reviveOverlay.SetAsFirstSibling();
   reviveOverlay.anchorMin=Vector2.zero;
   reviveOverlay.anchorMax=Vector2.one;
   reviveOverlay.offsetMin=reviveOverlay.offsetMax=Vector2.zero;
   reviveOverlay.gameObject.SetActive(false);
   var reviveMessage=ui.Text("等待重生点复活中",0,0,500,64,34,UiKit.White,reviveOverlay,TextAnchor.MiddleCenter);
   reviveMessage.rectTransform.anchorMin=reviveMessage.rectTransform.anchorMax=new Vector2(.5f,.44f);
   reviveMessage.rectTransform.pivot=new Vector2(.5f,.5f);
   reviveMessage.rectTransform.anchoredPosition=Vector2.zero;
   var score=HudFrame(480,22,640,112,HudMetal);
   foreach(int x in new[]{-9,625})for(int y=-24;y<98;y+=18)HudIcon("chain",x,y,22,25,score);
   hud=ui.Text("",20,8,600,94,29,null,score,TextAnchor.MiddleCenter);
   heatPanel=HudBlock(18,9,604,94,Color.clear,score);
   heatLabels.Clear();heatFills.Clear();
   heatSummary=ui.Text("",0,44,604,26,19,null,heatPanel,TextAnchor.MiddleCenter);
   var heatTrack=HudBlock(0,75,604,13,HudEdge,heatPanel);
   heatBlue=HudBlock(0,0,0,13,Draw2D.Blue,heatTrack);
   heatRed=HudBlock(604,0,0,13,Draw2D.Red,heatTrack);
   heatRed.pivot=new Vector2(1,1);
   countdownText=ui.Text("",650,300,300,200,110,UiKit.White,null,TextAnchor.MiddleCenter);
   var health=HudFrame(1185,18,397,192,new Color32(154,57,48,255),true);
   ui.Picture(ForestArt.Get("heart"),56,27,48,44,health);
   healthText=ui.Text("",119,16,238,64,38,null,health);healthText.name="Health value";
   healthText.resizeTextForBestFit=true;healthText.resizeTextMinSize=20;healthText.resizeTextMaxSize=36;
   var meter=HudFrame(22,82,353,52,HudGold,false,health);
   var track=HudBlock(8,8,337,36,new Color32(47,34,45,255),meter);
   healthFill=HudBlock(0,0,337,36,new Color32(192,40,41,255),track);
   var highlight=HudBlock(0,0,327,7,new Color32(249,100,77,255),healthFill);
   highlight.anchorMax=new Vector2(1,1);highlight.sizeDelta=new Vector2(0,7);
   shieldText=ui.Text("",28,132,130,28,18,new Color(.6f,.85f,1),health);shieldText.name="Shield value";
   battleStats=ui.Text("",27,151,343,29,22,UiKit.White,health);
   rescuePrompt=ui.Text("按 F 救助",0,0,128,32,21,UiKit.White,null,TextAnchor.MiddleCenter);
   rescuePrompt.gameObject.SetActive(false);
   rescueRing=ui.Picture(RescueRingSprite(),0,0,50,50,ui.Root);
   rescueRing.type=Image.Type.Filled;
   rescueRing.fillMethod=Image.FillMethod.Radial360;
   rescueRing.fillOrigin=2;
   rescueRing.color=new Color(.30f,1f,.39f);
   rescueRing.gameObject.SetActive(false);
   weaponPopup=HudFrame(1148,432,434,205,HudMetal,true);weaponPopup.name="Weapon pickup notice";
   gunIcon=ui.Picture(null,20,15,125,50,weaponPopup);weaponName=ui.Text("",165,15,200,50,29,UiKit.White,weaponPopup);
   equipmentText=ui.Text("",20,78,350,120,22,UiKit.White,weaponPopup);weaponPopup.gameObject.SetActive(false);
   var inventory=HudFrame(1148,652,434,230,new Color32(117,91,69,255),true);inventory.name="Inventory bar";
   ui.Text("E 切换  ·  左键使用",26,17,382,34,20,UiKit.Soft,inventory);
   HudBlock(21,159,392,3,new Color32(107,107,65,255),inventory);
   var ammoPanel=HudBlock(21,166,392,43,HudEdge,inventory);
   HudBlock(3,3,386,37,HudInk,ammoPanel);
   ammoText=ui.Text("",18,0,358,43,25,UiKit.White,ammoPanel);ammoText.name="Ammo value";
   for(int slot=0;slot<4;slot++){
    var cell=HudBlock(21+slot*99,54,95,94,HudEdge,inventory);cell.name="Inventory slot "+slot;
    slotBorders[slot]=cell.GetComponent<Image>();
    slotBackgrounds[slot]=HudBlock(3,3,89,88,UiKit.Panel,cell).GetComponent<Image>();
    slotIcons[slot]=ui.Picture(null,10,8,75,47,cell);slotLabels[slot]=ui.Text("",3,56,89,29,17,UiKit.White,cell,TextAnchor.MiddleCenter);
   }
   HudButton("设置","gear",24,22,()=>{settings=true;Redraw();});
   if(Screen=="Training"){
    ui.Button("＋伙伴",35,755,160,50,()=>{TrainingRuntime.Instance.AddTeammate();Redraw();},UiKit.Soft).interactable=TrainingRuntime.Instance.Teammates<3;
    ui.Button("－伙伴",210,755,160,50,()=>{TrainingRuntime.Instance.RemoveTeammate();Redraw();},UiKit.Soft);
    ui.Button("重置",385,755,130,50,()=>TrainingRuntime.Instance.ResetRoom(),UiKit.Soft);
   }
   if(!showingOnlineWarmup){
    var controls=HudFrame(24,816,465,66,HudMetal);
    ui.Text("WASD／方向键 移动 · 鼠标瞄准\n左键攻击／使用 · E 切换 · 长按 F 救助",14,8,437,50,18,UiKit.White,controls);
   }
   if(Screen=="Battle"||Screen=="LocalBattle"){
    killFeed=ui.Text("",1160,218,415,148,21,UiKit.White,null,TextAnchor.UpperRight);
    liveStats=HudFrame(260,172,1080,482,HudMetal,true);
    liveStats.gameObject.SetActive(false);
    liveStatsKey=null;
   }else{
    killFeed=null;
    liveStats=null;
   }
   UpdateForestHud();
  }
  void UpdateForestHud(){
   if(hud==null||!(Screen=="Training"||Screen=="LocalBattle"||Screen=="Battle"))return;
   var world=LocalMatchRuntime.Instance?.World??BattleWorld.Instance;if(world==null)return;var players=SessionPlayers();var local=players.FirstOrDefault(p=>p.IsOwner&&!p.IsBot.Value&&p.TrainingRole==0);
   UpdateMatchHud(world,players,local);
   if(warmupStart!=null)warmupStart.interactable=world.Phase.Value==2&&players.Length==world.TeamSize.Value*2&&players.All(p=>p.Connected.Value);
   bool showScore=!showingOnlineWarmup&&Screen!="Training";
   heatPanel.gameObject.SetActive(showScore);
   if(showScore)UpdateHeatHud(world);
   hud.text=showingOnlineWarmup?(world.Phase.Value==0?"等待玩家进入":"联机热身场"):Screen=="Training"?"训练房":"";
   countdownText.text=world.Phase.Value==1?Mathf.CeilToInt(world.Clock.Value).ToString():"";
   if(local==null){reviveOverlay.gameObject.SetActive(false);UpdateRescueHud(null,world);return;}
   healthText.text=local.Health.Value+"/"+local.MaxHealth.Value;
   healthFill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,337*Mathf.Clamp01((float)local.Health.Value/Mathf.Max(1,local.MaxHealth.Value)));
   healthText.color=local.Alive?UiKit.White:UiKit.Soft;
   shieldText.text=local.Shield.Value>0?"◆  护盾 "+local.Shield.Value:"";
   float respawn=local.Team.Value==0?world.BlueRespawnIn.Value:world.RedRespawnIn.Value;
   reviveOverlay.gameObject.SetActive(!local.Alive&&respawn>0);
   battleStats.text="击杀 "+local.Kills.Value+"     救助 "+local.Rescues.Value;
   UpdateRescueHud(local,world);
   if(hudPlayer!=local){hudPlayer=local;weaponNotice.Clear();}
   weaponNotice.Observe(local.Weapon.Value,Time.unscaledTime,local.EquipmentVersion.Value);weaponPopup.gameObject.SetActive(weaponNotice.Visible(Time.unscaledTime));
   var weapon=GameConfig.Current.Weapon((WeaponKind)local.Weapon.Value);gunIcon.sprite=CyberArt.Get("weapon"+local.Weapon.Value);gunIcon.enabled=gunIcon.sprite!=null;
   weaponName.text=weapon?.name??"";
   ammoText.text=!local.Alive?"—":weapon==null?"":weapon.name+"  "+(BattleRules.IsMelee(weapon.kind)?"无限":local.Ammo.Value+"/"+weapon.ammoCapacity);
   equipmentText.text=weapon==null?"":"伤害 "+weapon.damage+"     射程 "+weapon.range.ToString("0.##")+"\n间隔 "+weapon.cooldown.ToString("0.##")+"s     "+(weapon.kind==WeaponKind.RocketLauncher?"爆炸半径 "+GameConfig.Current.rocketBlastRadius.ToString("0.##"):BattleRules.IsMelee(weapon.kind)?"近战":"弹速 "+weapon.bulletSpeed.ToString("0.##"));
   for(int slot=0;slot<4;slot++){
    int kind=slot==0?local.Weapon.Value:local.ItemAt(slot);bool selected=local.Alive&&local.SelectedSlot.Value==slot;
    slotBorders[slot].color=selected?HudGold:HudEdge;
    slotBackgrounds[slot].color=selected?new Color32(48,63,41,255):UiKit.Panel;slotIcons[slot].sprite=kind>0?CyberArt.Get((slot==0?"weapon":"item")+kind):null;slotIcons[slot].enabled=kind>0&&slotIcons[slot].sprite!=null;
    slotLabels[slot].text=kind==0?"空":slot==0?weapon?.name??"":GameConfig.Current.Item((ItemKind)kind)?.name??"";slotLabels[slot].color=selected?HudGold:UiKit.White;
   }
  }
  void UpdateHeatHud(BattleWorld world){
   int count=world.ZoneHeat.Count;
   if(heatLabels.Count!=count){
    foreach(var label in heatLabels)Destroy(label.transform.parent.gameObject);
    heatLabels.Clear();heatFills.Clear();
    float width=604f/Mathf.Max(1,count);
    for(int i=0;i<count;i++){
     var cell=HudBlock(i*width,0,width-4,41,HudEdge,heatPanel);
     heatLabels.Add(ui.Text("",0,0,width-4,29,count>4?14:19,null,cell,TextAnchor.MiddleCenter));
     heatFills.Add(HudBlock(0,32,0,7,Draw2D.Blue,cell));
    }
   }
   for(int i=0;i<count;i++){
    float heat=world.ZoneHeat[i];
    bool captured=Mathf.Abs(heat)>=world.Objectives.captureHeat;
    Color tint=heat>0?Draw2D.Blue:heat<0?Draw2D.Red:UiKit.White;
    heatLabels[i].color=tint;
    heatLabels[i].text=(i+1)+"区 "+Mathf.FloorToInt(Mathf.Abs(heat))+"%"+(captured?" ✓":"");
    heatFills[i].GetComponent<Image>().color=tint;
    heatFills[i].SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,(604f/count-4)*Mathf.Abs(heat)/100);
   }
   heatSummary.text="蓝 "+Mathf.FloorToInt(world.BlueScore.Value)+"%    目标 "+world.Objectives.winningHeat.ToString("0.#")+"%    红 "+Mathf.FloorToInt(world.RedScore.Value)+"%";
   heatBlue.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,604*world.BlueScore.Value/100);
   heatRed.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,604*world.RedScore.Value/100);
  }
  static Sprite RescueRingSprite(){
   if(rescueRingSprite!=null)return rescueRingSprite;
   const int size=48;
   var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear};
   for(int y=0;y<size;y++)for(int x=0;x<size;x++){
    float radius=new Vector2(x-23.5f,y-23.5f).magnitude;
    texture.SetPixel(x,y,radius>=17&&radius<=22?Color.white:Color.clear);
   }
   texture.Apply(false,true);
   return rescueRingSprite=Sprite.Create(texture,new Rect(0,0,size,size),Vector2.one*.5f);
  }
  void UpdateRescueHud(NetPlayer local,BattleWorld world){
   var target=local==null?null:world.RescueTarget(local);
   rescuePrompt.gameObject.SetActive(false);
   rescueRing.gameObject.SetActive(false);
   if(target==null||Camera.main==null||target.FogHidden)return;
   var screen=Camera.main.WorldToScreenPoint(target.transform.position);
   if(screen.z<=0||!RectTransformUtility.ScreenPointToLocalPointInRectangle(ui.Root,screen,null,out var point)||!ui.Root.rect.Contains(point))return;
   var offset=point-new Vector2(ui.Root.rect.xMin,ui.Root.rect.yMax);
   float progress=Mathf.Clamp01(local.RescueProgress.Value/world.Objectives.rescueSeconds);
   rescuePrompt.gameObject.SetActive(progress<=0);
   rescueRing.gameObject.SetActive(progress>0);
   rescuePrompt.rectTransform.anchoredPosition=offset+new Vector2(-64,95);
   rescueRing.rectTransform.anchoredPosition=offset+new Vector2(35,25);
   rescueRing.fillAmount=progress;
  }
  internal void RecordKill(BattleWorld world,string killer,int killerTeam,string victim,int victimTeam){
   SyncKillFeedRound(world);
   if(killNotices.Count>=4)killNotices.Dequeue();
   string ColoredName(string name,int team)=>"<color="+(team==0?"#61C8FF":"#FF7272")+">"+name+"</color>";
   killNotices.Enqueue((ColoredName(killer,killerTeam)+"  击杀  "+ColoredName(victim,victimTeam),Time.unscaledTime+5));
  }
  void SyncKillFeedRound(BattleWorld world){
   if(feedWorld==world&&feedRound==world.RoundId.Value)return;
   killNotices.Clear();
   feedWorld=world;
   feedRound=world.RoundId.Value;
  }
  void UpdateMatchHud(BattleWorld world,NetPlayer[] players,NetPlayer local){
   if(killFeed==null||liveStats==null)return;
   SyncKillFeedRound(world);
   while(killNotices.Count>0&&killNotices.Peek().expires<=Time.unscaledTime)killNotices.Dequeue();
   killFeed.text=string.Join("\n",killNotices.Select(notice=>notice.text));
   bool show=!settings&&Application.isFocused&&Input.GetKey(KeyCode.Tab);
   liveStats.gameObject.SetActive(show);
   if(!show)return;
   string key=string.Join("|",players.Select(p=>p.UserId.Value+":"+p.Team.Value+":"+p.Seat.Value+":"+p.Kills.Value+":"+p.Deaths.Value+":"+p.Assists.Value+":"+p.DamageDone.Value));
   if(liveStatsKey==key)return;
   liveStatsKey=key;
   // Keep the decorative frame and replace only the table.
   var old=liveStats.Find("战绩表");
   if(old!=null){old.gameObject.SetActive(false);Destroy(old.gameObject);}
   var table=HudBlock(20,15,1040,452,Color.clear,liveStats);
   table.name="战绩表";
   DrawStatsTable(CurrentStats(players),CurrentMembers(players),local?.UserId.Value.ToString(),table,0,0,1040);
  }
  static PlayerResult[] CurrentStats(NetPlayer[] players)=>players.Select(p=>new PlayerResult{
   userId=p.UserId.Value.ToString(),kills=p.Kills.Value,deaths=p.Deaths.Value,assists=p.Assists.Value,damage=p.DamageDone.Value
  }).ToArray();
  static RoomMember[] CurrentMembers(NetPlayer[] players)=>players.Select(p=>new RoomMember{
   userId=p.UserId.Value.ToString(),username=p.DisplayName.Value.ToString(),team=p.Team.Value,seat=p.Seat.Value,skin=p.Skin.Value
  }).ToArray();
  void DrawStatsTable(PlayerResult[] stats,RoomMember[] members,string ownId,Transform parent,float x,float y,float width){
   string[] headings={"击杀 K","死亡 D","助攻 A","伤害"};
   float col=width*.112f,first=width-4*col;
   ui.Text("玩家",x+18,y,first-18,38,22,UiKit.Soft,parent);
   for(int i=0;i<headings.Length;i++)ui.Text(headings[i],x+first+i*col,y,col,38,20,UiKit.Soft,parent,TextAnchor.MiddleCenter);
   int row=0;
   foreach(var p in stats.OrderBy(p=>members.FirstOrDefault(m=>m.userId==p.userId)?.team??2).ThenBy(p=>members.FirstOrDefault(m=>m.userId==p.userId)?.seat??0)){
    var member=members.FirstOrDefault(m=>m.userId==p.userId);
    bool own=p.userId==ownId;
    float top=y+42+row++*43;
    var background=HudBlock(x,top,width,40,own?new Color32(88,69,35,255):UiKit.Panel,parent);
    if(own)HudBlock(0,0,5,40,HudGold,background);
    if(member!=null)ui.Picture(VegetableArt.Get("portrait"+member.skin),14,3,38,34,background);
    ui.Text((member?.username??p.userId)+(own?"  ·  你":""),62,0,first-68,40,22,own?HudGold:member?.team==0?Draw2D.Blue:Draw2D.Red,background);
    int[] values={p.kills,p.deaths,p.assists,p.damage};
    for(int i=0;i<values.Length;i++)ui.Text(values[i].ToString(),first+i*col,0,col,40,22,own?HudGold:UiKit.White,background,TextAnchor.MiddleCenter);
   }
  }
  void DrawMatchResult(int winner,PlayerResult[] stats,RoomMember[] members,string ownId){
   ui.Text(winner<0?"平局":winner==0?"蓝队胜利":"红队胜利",300,171,1000,65,44,winner<0?HudGold:winner==0?Draw2D.Blue:Draw2D.Red,null,TextAnchor.MiddleCenter);
   if(resultDetails){
    DrawStatsTable(stats,members,ownId,ui.Root,160,260,1280);
   }else{
    var best=stats.OrderByDescending(p=>p.kills).ThenByDescending(p=>p.assists).ThenByDescending(p=>p.damage).ThenBy(p=>p.deaths).ThenBy(p=>p.userId,System.StringComparer.Ordinal).FirstOrDefault();
    if(best!=null){
     var member=members.FirstOrDefault(m=>m.userId==best.userId);
     var card=HudFrame(460,260,680,385,HudGold,true);
     ui.Text("MVP",32,19,616,58,46,HudGold,card,TextAnchor.MiddleCenter);
     if(member!=null)ui.Picture(VegetableArt.Get("portrait"+member.skin),250,84,180,150,card);
     ui.Text(member?.username??best.userId,35,235,610,55,40,UiKit.White,card,TextAnchor.MiddleCenter);
     ui.Text("击杀 "+best.kills+"    死亡 "+best.deaths+"    助攻 "+best.assists+"    伤害 "+best.damage,30,307,620,46,23,HudGold,card,TextAnchor.MiddleCenter);
    }
   }
   ui.Button(resultDetails?"上一页 · MVP":"下一页 · 全员战绩",1030,760,380,64,()=>{resultDetails=!resultDetails;Redraw();});
  }
  void LocalResultScreen(){
   var world=LocalMatchRuntime.Instance.World;
   var players=SessionPlayers();
   DrawMatchResult(world.Winner.Value,CurrentStats(players),CurrentMembers(players),players.FirstOrDefault(p=>p.IsOwner&&!p.IsBot.Value)?.UserId.Value.ToString());
   ui.Button("再来一局",560,760,480,64,LocalMatchRuntime.Instance.Restart).interactable=LocalMatchRuntime.Instance.IsHost;ui.Button("返回",190,760,250,64,LeaveLocalMatch,UiKit.Soft);
  }
  void SettingsPanel(){
   ui.Box(0,0,1600,900,new Color(0,0,0,.72f));ui.Box(480,180,640,560,UiKit.Panel);ui.Text("设置",540,245,520,65,38,null,null,TextAnchor.MiddleCenter);
   VolumeSlider("背景音乐","BoundShoot.MusicVolume",323);
   VolumeSlider("音效","BoundShoot.EffectsVolume",380);
   ui.Button("返回游戏",550,450,500,60,()=>{settings=false;Redraw();});
   ui.Button("回到主菜单",550,535,500,60,ReturnToMainMenu,UiKit.Soft);ui.Button("退出游戏",550,620,500,55,Quit,UiKit.Soft);
  }
  void VolumeSlider(string label,string preference,float y){
   ui.Text(label,550,y,126,36,22);
   var track=ui.Box(690,y+9,280,18,UiKit.Ink);
   var range=HudBlock(10,0,260,18,Color.clear,track);
   var handle=HudBlock(0,-7,20,32,HudGold,range);
   handle.pivot=new Vector2(.5f,.5f);
   handle.anchoredPosition=new Vector2(0,-9);
   var valueLabel=ui.Text("",990,y,72,36,20);
   var slider=track.gameObject.AddComponent<Slider>();
   slider.handleRect=handle;
   slider.targetGraphic=handle.GetComponent<Image>();
   slider.SetValueWithoutNotify(PlayerPrefs.GetFloat(preference,PlayerPrefs.GetInt("BoundShoot.Sound",1)));
   valueLabel.text=Mathf.RoundToInt(slider.value*100)+"%";
   slider.onValueChanged.AddListener(value=>{
    PlayerPrefs.SetFloat(preference,value);
    valueLabel.text=Mathf.RoundToInt(value*100)+"%";
   });
  }
  static void Quit(){
#if UNITY_EDITOR
   UnityEditor.EditorApplication.isPlaying=false;
#else
   Application.Quit();
#endif
  }
 }
}
