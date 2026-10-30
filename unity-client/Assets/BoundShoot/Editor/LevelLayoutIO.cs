using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BoundShoot.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BoundShoot.Editor {
 public static class LevelLayoutIO {
  public const string ConfigPath="Assets/BoundShoot/Resources/GameConfig.json";
  public const string SceneFolder="Assets/BoundShoot/Levels";
  public static GameConfig Read(string file=ConfigPath)=>JsonUtility.FromJson<GameConfig>(File.ReadAllText(file));
  public static LevelLayout Create(MapDefinition map,string sceneName="关卡编辑"){
   var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
   var go=new GameObject(sceneName);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);
   go.tag="EditorOnly";var root=go.AddComponent<LevelLayout>();Populate(root,map);return root;
  }
  public static void Populate(LevelLayout root,MapDefinition map){
   Undo.RegisterFullObjectHierarchyUndo(root.gameObject,"载入关卡");
   foreach(Transform child in root.transform.Cast<Transform>().ToArray())Undo.DestroyObjectImmediate(child.gameObject);
   root.transform.position=Vector3.zero;root.transform.rotation=Quaternion.identity;root.transform.localScale=Vector3.one;
   root.mapId=map.id;root.mapName=map.name;root.available=map.available;root.theme=map.theme;
   root.plateau=map.plateau==null?null:JsonUtility.FromJson<PlateauDefinition>(JsonUtility.ToJson(map.plateau));
   root.dimensions=new Vector2(map.halfWidth*2,map.halfHeight*2);root.groundColor=MapContent.Ground(map);
   root.sourceMapId=map.id;root.sourceMapJson=JsonUtility.ToJson(map);root.newMap=false;
   if(map.floors!=null)foreach(var p in map.floors){var piece=Add(root,LevelPieceKind.Floor,p.center,p.size);piece.color=p.color;}
   foreach(var p in map.blocks??Array.Empty<BlockDefinition>())Add(root,LevelPieceKind.Obstacle,p.center,p.size).visual=p.visual;
   for(int team=0;team<2;team++){
    var positions=team==0?map.blueSpawns:map.redSpawns;
    for(int seat=0;seat<(positions?.Length??0);seat++){var piece=Add(root,LevelPieceKind.Spawn,positions[seat],Vector2.one);piece.team=team;piece.seat=seat;Rename(piece);}
   }
   foreach(var p in MapContent.Pickups(map,Read().pickupRespawn)){
    var piece=Add(root,LevelPieceKind.Pickup,p.position,Vector2.one);piece.weapon=p.weapon;piece.item=p.item;
    piece.respawnSeconds=p.respawnSeconds;piece.randomizeOnRespawn=p.randomizeOnRespawn;Rename(piece);
   }
   foreach(var p in MapContent.Revivals(map))Add(root,LevelPieceKind.Revival,p.center,p.size);
   EditorUtility.SetDirty(root);EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
  }
  public static LevelPiece Add(LevelLayout root,LevelPieceKind kind,Vector2 position,Vector2 size){
   var go=new GameObject();Undo.RegisterCreatedObjectUndo(go,"放置关卡物件");
   go.transform.SetParent(root.transform,false);go.transform.localPosition=position;go.transform.localScale=new Vector3(size.x,size.y,1);
   var piece=go.AddComponent<LevelPiece>();piece.kind=kind;Rename(piece);return piece;
  }
  public static string Label(LevelPiece piece){
   switch(piece.kind){
    case LevelPieceKind.Floor:return "地面";
    case LevelPieceKind.Obstacle:return "障碍";
    case LevelPieceKind.Spawn:return (piece.team==0?"蓝 A":"红 B")+(piece.seat+1)+" 出生点";
    case LevelPieceKind.Revival:return "复活区";
    default:return piece.weapon!=WeaponKind.None?WeaponName(piece.weapon):ItemName(piece.item);
   }
  }
  public static string WeaponName(WeaponKind k)=>new[]{"无","狙击枪","手枪","AK","匕首"}[Mathf.Clamp((int)k,0,4)];
  public static string ItemName(ItemKind k)=>new[]{"无","绳索收缩","绳索延长","护盾","冲刺装置","炸弹","弹性强化","血包","问号盒"}[Mathf.Clamp((int)k,0,8)];
  public static void Rename(LevelPiece p)=>p.gameObject.name=Label(p);
  public static MapDefinition Collect(LevelLayout root){
   var parts=root.GetComponentsInChildren<LevelPiece>(false);
   var pickups=parts.Where(p=>p.kind==LevelPieceKind.Pickup).Select(p=>new PickupDefinition{position=p.transform.localPosition,
    weapon=p.weapon,item=p.item,respawnSeconds=p.respawnSeconds,randomizeOnRespawn=p.randomizeOnRespawn}).ToArray();
   var zones=parts.Where(p=>p.kind==LevelPieceKind.Revival).Select(p=>new RevivalDefinition{center=p.transform.localPosition,size=p.transform.localScale}).ToArray();
   Vector2[] Spawns(int team)=>parts.Where(p=>p.kind==LevelPieceKind.Spawn&&p.team==team).OrderBy(p=>p.seat).Select(p=>(Vector2)p.transform.localPosition).ToArray();
   return new MapDefinition{layoutVersion=1,id=root.mapId,name=root.mapName,available=root.available,halfWidth=root.dimensions.x*.5f,halfHeight=root.dimensions.y*.5f,
     groundColor=root.groundColor,theme=root.theme,plateau=root.theme=="forest"&&root.plateau!=null&&root.plateau.size.sqrMagnitude>0?root.plateau:null,blueSpawns=Spawns(0),redSpawns=Spawns(1),pickups=pickups,supplies=pickups.Select(p=>p.position).ToArray(),
    revivalZones=zones,revivals=zones.Select(p=>p.center).ToArray(),
    blocks=parts.Where(p=>p.kind==LevelPieceKind.Obstacle).Select(p=>new BlockDefinition{center=p.transform.localPosition,size=p.transform.localScale,visual=p.visual}).ToArray(),
    floors=parts.Where(p=>p.kind==LevelPieceKind.Floor).Select(p=>new FloorDefinition{center=p.transform.localPosition,size=p.transform.localScale,color=p.color}).ToArray()};
  }
  public static List<string> Validate(LevelLayout root){
   var issues=LevelMapValidation.Validate(Collect(root),Read());
   if(root.transform.position!=Vector3.zero||root.transform.rotation!=Quaternion.identity||root.transform.localScale!=Vector3.one)issues.Add("关卡根节点必须保持原点、零旋转、缩放1。");
   var pieces=root.GetComponentsInChildren<LevelPiece>(false);
   foreach(var p in pieces){
    if(p.transform.parent!=root.transform)issues.Add(p.name+"必须直接放在关卡根节点下。");
    if(p.transform.localRotation!=Quaternion.identity||Mathf.Abs(p.transform.localPosition.z)>.001f||Mathf.Abs(p.transform.localScale.z-1)>.001f)issues.Add(p.name+"只支持XY平面、零旋转，Z缩放必须为1。");
   }
   for(int team=0;team<2;team++)for(int seat=0;seat<4;seat++)if(pieces.Count(p=>p.kind==LevelPieceKind.Spawn&&p.team==team&&p.seat==seat)!=1)issues.Add((team==0?"蓝":"红")+"队座位"+(seat+1)+"必须且只能有一个出生点。");
   return issues;
  }
  public static void Save(LevelLayout root,string path=ConfigPath){
   var issues=Validate(root);if(issues.Count>0)throw new InvalidOperationException(string.Join("\n",issues));
   SaveMap(path,Collect(root),root.sourceMapId,root.sourceMapJson,root.newMap);
   Undo.RecordObject(root,"保存关卡配置");root.sourceMapId=root.mapId;root.sourceMapJson=JsonUtility.ToJson(Collect(root));root.newMap=false;
   EditorUtility.SetDirty(root);EditorSceneManager.MarkSceneDirty(root.gameObject.scene);AssetDatabase.Refresh();
  }
  // Reload the latest config so independent weapon/item tuning is never overwritten by an old editor snapshot.
  public static void SaveMap(string path,MapDefinition map,string sourceId,string sourceJson,bool isNew){
   var latest=Read(path);var issues=LevelMapValidation.Validate(map,latest);
   if(issues.Count>0)throw new InvalidOperationException(string.Join("\n",issues));
   var maps=latest.maps.ToList();int source=maps.FindIndex(m=>m.id==sourceId),target=maps.FindIndex(m=>m.id==map.id);
   if(isNew){if(target>=0)throw new InvalidOperationException("地图ID已存在，请使用新ID。");maps.Add(map);}
   else{
    if(source<0||JsonUtility.ToJson(maps[source])!=sourceJson)throw new InvalidOperationException("这张地图已被其他修改更新，请先重新载入，避免覆盖同伴的关卡。");
    if(target>=0&&target!=source)throw new InvalidOperationException("地图ID已存在。");
    if(sourceId!=map.id)throw new InvalidOperationException("已有地图请保持ID不变；要另存地图请使用复制为新关卡。");
    maps[source]=map;
   }
   latest.maps=maps.ToArray();string text=JsonUtility.ToJson(latest,true)+"\n";
   string full=Path.GetFullPath(path),temp=full+".level-editor-tmp";
   try{
    File.WriteAllText(temp,text,new UTF8Encoding(false));
    if(File.Exists(full))File.Replace(temp,full,null);else File.Move(temp,full);
   }finally{if(File.Exists(temp))File.Delete(temp);}
  }
 }
 public static class LevelMapValidation {
  static bool Finite(float n)=>!float.IsNaN(n)&&!float.IsInfinity(n);
  public static List<string> Validate(MapDefinition map,GameConfig config){
   var errors=new List<string>();
   if(string.IsNullOrWhiteSpace(map.id)||!Regex.IsMatch(map.id,"^[A-Za-z][A-Za-z0-9_]{0,47}$"))errors.Add("地图ID须为英文字母开头的字母、数字或下划线，最长48位。");
   if(string.IsNullOrWhiteSpace(map.name))errors.Add("请填写地图名称。");
   if(map.id=="Arena_01"&&!map.available)errors.Add("默认关卡Arena_01必须保持开放。");
   if(!Finite(map.halfWidth)||!Finite(map.halfHeight)||map.halfWidth<4||map.halfHeight<4||map.halfWidth>100||map.halfHeight>100)errors.Add("地图宽高应在8至200之间。");
   bool Point(Vector2 p)=>Finite(p.x)&&Finite(p.y);
   bool Inside(Vector2 p,Vector2 margin)=>Point(p)&&Mathf.Abs(p.x)+margin.x<=map.halfWidth&&Mathf.Abs(p.y)+margin.y<=map.halfHeight;
   bool Covered(Vector2 p,float clearance)=>(map.blocks??Array.Empty<BlockDefinition>()).Any(b=>Mathf.Abs(p.x-b.center.x)<b.size.x*.5f+clearance&&Mathf.Abs(p.y-b.center.y)<b.size.y*.5f+clearance);
   void Rect(Vector2 center,Vector2 size,string title){if(!Point(size)||size.x<=0||size.y<=0||!Inside(center,size*.5f))errors.Add(title+"尺寸无效或超出边界。");}
   foreach(var b in map.blocks??Array.Empty<BlockDefinition>())Rect(b.center,b.size,"障碍");
   foreach(var f in map.floors??Array.Empty<FloorDefinition>())Rect(f.center,f.size,"地面");
   if(map.plateau!=null){var h=map.plateau;Rect(h.center,h.size,"高台");if(!Finite(h.stairWidth)||h.stairWidth<=.9f||h.stairs==null||h.stairs.Length==0||h.stairs.Any(y=>!Finite(y)||Mathf.Abs(y-h.center.y)+h.stairWidth*.5f>h.size.y*.5f))errors.Add("台阶宽度或位置无效。");}
   var spawns=(map.blueSpawns??Array.Empty<Vector2>()).Concat(map.redSpawns??Array.Empty<Vector2>()).ToArray();
   if(map.blueSpawns?.Length!=4||map.redSpawns?.Length!=4)errors.Add("红蓝队各需要4个有序出生点，当前2v2使用前两个。");
   foreach(var p in spawns)if(!Inside(p,Vector2.one*.45f)||Covered(p,.45f))errors.Add("出生点在边界外或与障碍重叠。");
   for(int i=0;i<spawns.Length;i++)for(int j=i+1;j<spawns.Length;j++)if(Vector2.Distance(spawns[i],spawns[j])<.9f)errors.Add("出生点之间距离不足0.9。");
   var pickups=MapContent.Pickups(map,config.pickupRespawn);
   foreach(var p in pickups){
    if(!Inside(p.position,Vector2.one*.45f)||Covered(p.position,.45f))errors.Add("拾取物在边界外或与障碍重叠。");
    if((p.weapon==WeaponKind.None)==(p.item==ItemKind.None)||!Enum.IsDefined(typeof(WeaponKind),p.weapon)||!Enum.IsDefined(typeof(ItemKind),p.item))errors.Add("拾取物必须指定一种武器或一种道具。");
    if(!Finite(p.respawnSeconds)||p.respawnSeconds<0)errors.Add("拾取物刷新秒数不能小于0；0表示不刷新。");
    if(p.weapon!=WeaponKind.None&&config.Weapon(p.weapon)==null)errors.Add("武器没有对应数值配置。");
    if(p.item!=ItemKind.None&&p.item!=ItemKind.Mystery&&(config.Item(p.item)==null||!config.Item(p.item).enabled))errors.Add("放置的道具未在配置中启用。");
   }
   if(!pickups.Any(p=>p.weapon!=WeaponKind.None))errors.Add("关卡至少需要一个武器拾取点。");
   var zones=MapContent.Revivals(map);
   foreach(var r in zones){Rect(r.center,r.size,"复活区");if(Covered(r.center,.45f))errors.Add("复活区中心与障碍重叠。");}
   return errors.Distinct().ToList();
  }
 }
}
