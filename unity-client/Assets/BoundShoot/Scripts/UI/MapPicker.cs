using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace BoundShoot {
 public partial class GameClient {
  const int MapsPerPage=6;
  bool mapPickerOpen;
  string mapCandidate;
  int mapPage;

  bool CanPickMap=>Screen=="LocalRoom"
   ?LocalMatchRuntime.Instance!=null&&LocalMatchRuntime.Instance.IsHost&&LocalMatchRuntime.Instance.Ready&&LocalMatchRuntime.Instance.World.Phase.Value==0
   :Screen=="Room"&&Room!=null&&User!=null&&Room.hostId==User.id&&Room.status=="Waiting";

  void OpenMapPicker(string current){
   if(!CanPickMap||busy)return;
   var maps=GameConfig.Current.maps.Where(m=>m.available).ToArray();
   if(maps.Length==0)return;
   int index=Mathf.Max(0,Array.FindIndex(maps,m=>m.id==current));
   mapCandidate=maps[index].id;
   mapPage=index/MapsPerPage;
   mapPickerOpen=true;
   Redraw();
  }

  void ApplyMapSelection(){
   if(!CanPickMap||busy||GameConfig.Current.Map(mapCandidate)==null)return;
   if(Screen=="LocalRoom"){
    if(!LocalMatchRuntime.Instance.SelectMap(mapCandidate)){Error("暂时无法更换地图");return;}
    mapPickerOpen=false;
    Redraw();
    return;
   }
   mapPickerOpen=false;
   Redraw();
   Run<RoomData>("PUT","/api/rooms/"+Room.id+"/config",
    new RoomConfigBody{size=Room.size,mapId=mapCandidate},
    room=>{lastRoomJson="";ReceiveRoom(room);});
  }

  void DrawMapPicker(){
   var maps=GameConfig.Current.maps.Where(m=>m.available).ToArray();
   var selected=maps.FirstOrDefault(m=>m.id==mapCandidate);
   if(selected==null)return;
   ui.Box(0,0,1600,900,new Color(0,0,0,.7f));
   var panel=ui.Box(250,105,1100,690,UiKit.Panel);
   ui.Text("选择地图",35,20,760,52,35,null,panel);
   ui.Button("关闭",910,20,150,50,()=>{mapPickerOpen=false;Redraw();},UiKit.Soft,panel);

   int first=mapPage*MapsPerPage;
   for(int i=first;i<Mathf.Min(first+MapsPerPage,maps.Length);i++){
    var map=maps[i];
    ui.Button(map.name,35,100+(i-first)*63,320,54,()=>{mapCandidate=map.id;Redraw();},
     map.id==selected.id?Draw2D.Gold:UiKit.Soft,panel);
   }
   if(maps.Length>MapsPerPage){
    ui.Button("上一页",35,493,145,45,()=>{mapPage--;Redraw();},UiKit.Soft,panel).interactable=mapPage>0;
    ui.Button("下一页",210,493,145,45,()=>{mapPage++;Redraw();},UiKit.Soft,panel).interactable=first+MapsPerPage<maps.Length;
   }

   var rules=selected.objectives??new ObjectiveRules();
   var zones=rules.zones??Array.Empty<ControlZone>();
   ui.Text(selected.name,395,82,650,42,30,null,panel);
   DrawMapPreview(selected,rules,panel);
   ui.Text("热点 "+zones.Length+"     占领 "+rules.captureHeat+"%     平均热度目标 "+rules.winningHeat+"%",395,503,665,52,23,null,panel);
   ui.Text("热力区",430,455,130,35,20,Draw2D.Gold,panel);
   ui.Text("蓝队重生",575,455,150,35,20,Draw2D.Blue,panel);
   ui.Text("红队重生",755,455,150,35,20,Draw2D.Red,panel);
   var apply=ui.Button("选用地图",780,605,280,55,ApplyMapSelection,Draw2D.Gold,panel);
   apply.interactable=!busy&&(Screen!="LocalRoom"||!LocalMatchRuntime.Instance.HasPendingSeats);
  }

  void DrawMapPreview(MapDefinition map,ObjectiveRules rules,Transform parent){
   var preview=ui.Box(395,132,665,315,UiKit.Ink,parent);
   float scale=Mathf.Min(625/(2*map.halfWidth),282/(2*map.halfHeight));
   var ground=map.groundColor.a>0?map.groundColor:new Color(.28f,.36f,.26f);
   ui.Box(332.5f-map.halfWidth*scale,157.5f-map.halfHeight*scale,map.halfWidth*2*scale,map.halfHeight*2*scale,ground,preview);
   Vector2 Point(Vector2 world)=>new Vector2(332.5f+world.x*scale,157.5f-world.y*scale);
   void Dot(Vector2 world,float radius,Color color){
    var point=Point(world);
    var image=ui.Picture(Draw2D.Circle,point.x-radius,point.y-radius,radius*2,radius*2,preview);
    image.color=color;
   }
   foreach(var zone in rules.zones??Array.Empty<ControlZone>()){
    if(zone!=null)Dot(zone.center,zone.radius*scale,new Color(1,.78f,.24f,.65f));
   }
   foreach(var block in map.blocks??Array.Empty<BlockDefinition>()){
    var center=Point(block.center);
    var size=block.size*scale;
    ui.Box(center.x-size.x*.5f,center.y-size.y*.5f,size.x,size.y,new Color(.15f,.19f,.15f),preview);
   }
   if(map.blueSpawns?.Length>0)Dot(rules.customRespawns?rules.blueRespawn:map.blueSpawns[0],6,Draw2D.Blue);
   if(map.redSpawns?.Length>0)Dot(rules.customRespawns?rules.redRespawn:map.redSpawns[0],6,Draw2D.Red);
  }
 }
}
