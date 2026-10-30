using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace BoundShoot {
 public partial class GameClient {
  string sessionMode="AI";public int TrainingSize;Image[] hearts;Image gunIcon,itemIcon;Text shieldText,reviveText,equipmentText,weaponName,itemLabel,countdownText;int drawnMaxHealth=-1;
  RectTransform weaponPopup,itemBadge;NetPlayer hudPlayer;readonly WeaponPickupNotice weaponNotice=new WeaponPickupNotice();
  public void OpenIdentity(){playerName=PlayerPrefs.GetString("BoundShoot.Name",playerName);Screen="Auth";Redraw();}
  public void SelectMode(string mode){
   sessionMode=mode;message="";
   if(mode=="Training"){TrainingSize=LocalTeamSize;Screen="TrainingRoom";Redraw();}
   else if(mode=="Online"){Screen="OnlineMenu";Redraw();}
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
   if(ui==null)return;ui.Clear();notice=null;hud=null;hearts=null;drawnMaxHealth=-1;
   if(Screen=="Battle"||Screen=="LocalBattle"||Screen=="Training"){DrawBattleUI();if(settings)SettingsPanel();return;}
   hudPlayer=null;weaponNotice.Clear();
   ForestPage(Screen=="Title"?"":Screen=="Auth"?"选择你的搭档":"派对射击");
   switch(Screen){
    case "Title":
     ui.Text("派对射击",300,230,1000,140,90,UiKit.White,null,TextAnchor.MiddleCenter);ui.Text("BOUND SHOOT",400,370,800,70,36,UiKit.Soft,null,TextAnchor.MiddleCenter);
     ui.Button("进入游戏",560,520,480,76,OpenIdentity);ui.Button("设置",560,620,225,56,()=>{settings=true;Redraw();},UiKit.Soft);ui.Button("退出",815,620,225,56,Quit,UiKit.Soft);
     for(int i=0;i<8;i++)ui.Picture(VegetableArt.Get("portrait"+i),330+i*120,725,95,105);break;
    case "Auth":AuthScreen();break;
    case "Menu":ModeScreen();break;
    case "OnlineMenu":OnlineScreen();break;
    case "TrainingRoom":TrainingRoomScreen();break;
    case "LocalRoom":LocalRoomScreen();break;
    case "LocalResult":LocalResultScreen();break;
    case "Room":RoomScreen();break;
    case "Result":ResultScreen();break;
    default:ui.Text("进入房间…",400,360,800,100,38,UiKit.White,null,TextAnchor.MiddleCenter);break;
   }
   notice=ui.Text(message,250,842,1100,40,22,new Color(1,.75f,.45f),null,TextAnchor.MiddleCenter);
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
  }
  void OnlineScreen(){
   ui.Box(460,235,680,460,UiKit.Panel);ui.Text("联机房间",500,260,600,60,36,null,null,TextAnchor.MiddleCenter);
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
   var world=LocalMatchRuntime.Instance?.World??BattleWorld.Instance;var local=SessionPlayers().FirstOrDefault(p=>p.IsOwner&&!p.IsBot.Value&&p.TrainingRole==0);
   ui.Box(605,22,390,74,new Color(.07f,.12f,.1f,.88f));hud=ui.Text("",620,27,360,60,26,null,null,TextAnchor.MiddleCenter);
   countdownText=ui.Text("",650,300,300,200,110,UiKit.White,null,TextAnchor.MiddleCenter);
   drawnMaxHealth=local==null?GameConfig.Current.baseHealth:local.MaxHealth.Value;hearts=new Image[Mathf.Max(1,drawnMaxHealth)];int heartRows=(hearts.Length+4)/5;
   ui.Box(1170,20,400,Mathf.Max(155,heartRows*49+78),new Color(.07f,.12f,.1f,.9f));if(local!=null)ui.Picture(VegetableArt.Get("portrait"+local.Skin.Value),1184,31,66,70);
   for(int i=0;i<hearts.Length;i++)hearts[i]=ui.Picture(ForestArt.Get("heart"),1260+(i%5)*49,35+(i/5)*49,44,44);
   shieldText=ui.Text("",1190,40+heartRows*49,350,45,24,new Color(.6f,.85f,1));reviveText=ui.Text("",1260,40+heartRows*49,280,45,24,UiKit.White);
   weaponPopup=ui.Box(1180,620,390,210,new Color(.07f,.12f,.1f,.92f));weaponPopup.name="Weapon pickup notice";
   gunIcon=ui.Picture(null,20,15,125,50,weaponPopup);weaponName=ui.Text("",165,15,200,50,29,UiKit.White,weaponPopup);
   equipmentText=ui.Text("",20,78,350,120,22,UiKit.White,weaponPopup);weaponPopup.gameObject.SetActive(false);
   itemBadge=ui.Box(1180,848,390,45,new Color(.07f,.12f,.1f,.88f));itemIcon=ui.Picture(null,14,4,36,36,itemBadge);ui.Text("E",65,0,40,45,23,UiKit.Soft,itemBadge);itemLabel=ui.Text("",115,0,260,45,23,UiKit.White,itemBadge);itemBadge.gameObject.SetActive(false);
   ui.Button("设置",35,28,110,42,()=>{settings=true;Redraw();},UiKit.Soft);
   if(Screen=="Training"){
    ui.Button("＋伙伴",35,755,160,50,()=>{TrainingRuntime.Instance.AddTeammate();Redraw();},UiKit.Soft).interactable=TrainingRuntime.Instance.Teammates<3;
    ui.Button("－伙伴",210,755,160,50,()=>{TrainingRuntime.Instance.RemoveTeammate();Redraw();},UiKit.Soft);
    ui.Button("重置",385,755,130,50,()=>TrainingRuntime.Instance.ResetRoom(),UiKit.Soft);ui.Button("返回",35,820,160,48,LeaveTraining,UiKit.Soft);
   }else ui.Button("离开",35,820,130,48,()=>{if(LocalMatchRuntime.Instance!=null)LeaveLocalMatch();else DisconnectBattle();},UiKit.Soft);
   UpdateForestHud();
  }
  void UpdateForestHud(){
   if(hud==null||!(Screen=="Training"||Screen=="LocalBattle"||Screen=="Battle"))return;
   var world=LocalMatchRuntime.Instance?.World??BattleWorld.Instance;if(world==null)return;var players=SessionPlayers();var local=players.FirstOrDefault(p=>p.IsOwner&&!p.IsBot.Value&&p.TrainingRole==0);
   hud.text=Screen=="Training"?"训练房":players.Count(p=>p.Team.Value==0&&p.Alive)+"   :   "+players.Count(p=>p.Team.Value==1&&p.Alive)+"    "+Mathf.CeilToInt(world.Clock.Value)+"s";
   if(world.Phase.Value==1)hud.text=Mathf.CeilToInt(world.Clock.Value).ToString();
   countdownText.text=world.Phase.Value==1?Mathf.CeilToInt(world.Clock.Value).ToString():"";
   if(local==null)return;if(local.MaxHealth.Value!=drawnMaxHealth){Redraw();return;}
   for(int i=0;i<hearts.Length;i++){hearts[i].sprite=ForestArt.Get(i<local.Health.Value?"heart":"heartEmpty");hearts[i].color=i<local.Health.Value?Color.white:new Color(.65f,.68f,.64f);}
   shieldText.text=local.Shield.Value>0?"◆  护盾 "+local.Shield.Value:"";
   reviveText.text=local.Alive?"":"复活  "+Mathf.CeilToInt(Mathf.Max(0,GameConfig.Current.reviveSeconds-local.Revive.Value))+"s";
   if(hudPlayer!=local){hudPlayer=local;weaponNotice.Clear();}
   weaponNotice.Observe(local.Weapon.Value,Time.unscaledTime);weaponPopup.gameObject.SetActive(weaponNotice.Visible(Time.unscaledTime));
   var weapon=GameConfig.Current.Weapon((WeaponKind)local.Weapon.Value);gunIcon.sprite=CyberArt.Get("weapon"+local.Weapon.Value);gunIcon.enabled=gunIcon.sprite!=null;
   weaponName.text=weapon?.name??"";
   equipmentText.text=weapon==null?"":"伤害 "+weapon.damage+"     射程 "+weapon.range.ToString("0.##")+"\n间隔 "+weapon.cooldown.ToString("0.##")+"s     "+(weapon.kind==WeaponKind.Dagger?"近战":"弹速 "+weapon.bulletSpeed.ToString("0.##"))+"\n生命上限 "+weapon.maxHealth;
   itemIcon.sprite=CyberArt.Get("item"+local.Item.Value);itemIcon.enabled=local.Item.Value>0&&itemIcon.sprite!=null;
   itemBadge.gameObject.SetActive(local.Item.Value>0);itemLabel.text=GameConfig.Current.Item((ItemKind)local.Item.Value)?.name??"";
  }
  void LocalResultScreen(){
   var world=LocalMatchRuntime.Instance.World;int winner=world.Winner.Value;ui.Text(winner<0?"平局":winner==0?"蓝队胜利":"红队胜利",300,178,1000,75,46,winner<0?Draw2D.Gold:winner==0?Draw2D.Blue:Draw2D.Red,null,TextAnchor.MiddleCenter);
   ui.Text("玩家",280,285,600,40,26);ui.Text("击杀       倒地       伤害",900,285,450,40,25);
   int i=0;foreach(var p in SessionPlayers().OrderBy(p=>p.Team.Value).ThenBy(p=>p.Seat.Value)){float y=340+i++*43;ui.Picture(VegetableArt.Get("portrait"+p.Skin.Value),240,y,38,40);ui.Text(p.DisplayName.Value.ToString(),300,y,550,40,25,p.Team.Value==0?Draw2D.Blue:Draw2D.Red);ui.Text(p.Kills.Value+"           "+p.Deaths.Value+"           "+p.DamageDone.Value,920,y,430,40,25);}
   ui.Button("再来一局",560,760,480,64,LocalMatchRuntime.Instance.Restart).interactable=LocalMatchRuntime.Instance.IsHost;ui.Button("返回",190,760,250,64,LeaveLocalMatch,UiKit.Soft);
  }
  void SettingsPanel(){
   ui.Box(0,0,1600,900,new Color(0,0,0,.72f));ui.Box(480,215,640,480,UiKit.Panel);ui.Text("设置",540,245,520,65,38,null,null,TextAnchor.MiddleCenter);
   ui.Button("音效："+(PlayerPrefs.GetInt("BoundShoot.Sound",1)==1?"开":"关"),550,355,500,60,()=>{PlayerPrefs.SetInt("BoundShoot.Sound",1-PlayerPrefs.GetInt("BoundShoot.Sound",1));Redraw();},UiKit.Soft);
   ui.Button("返回游戏",550,470,500,65,()=>{settings=false;Redraw();});ui.Button("退出游戏",550,575,500,55,Quit,UiKit.Soft);
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
