using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BoundShoot.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BoundShoot.Editor {
 public sealed class LevelEditorWindow:EditorWindow {
  LevelLayout root;GameConfig catalog;int mapIndex;Vector2 scroll;string status="",newId="Arena_02",newName="新竞技场";
  MessageType statusType;UnityEditor.Editor inspector;int brush=-1;bool placing;
  static readonly string[] Palette={"地面块","障碍柱","蓝队出生点","红队出生点","复活区","狙击枪","手枪","AK","匕首","问号盒","绳索收缩","绳索延长","护盾","冲刺装置","炸弹","弹性强化","血包"};
  [MenuItem("Bound Shoot/Level Editor 关卡编辑器")]
  public static void Open(){var w=GetWindow<LevelEditorWindow>("关卡编辑器");w.minSize=new Vector2(340,560);w.Show();}
  public static void OpenStarter(){
   CreateStarterScene();
   if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
   EditorSceneManager.OpenScene(LevelLayoutIO.SceneFolder+"/Arena_01.unity");Open();
   var w=GetWindow<LevelEditorWindow>();w.root=FindObjectOfType<LevelLayout>();Selection.activeGameObject=w.root.gameObject;w.Frame();
  }
  void OnEnable(){RefreshCatalog();root=FindObjectOfType<LevelLayout>();SelectChanged();SceneView.duringSceneGui+=SceneGUI;Selection.selectionChanged+=SelectChanged;Undo.undoRedoPerformed+=Changed;}
  public static void CreateStarterScene(){
   string path=LevelLayoutIO.SceneFolder+"/Arena_01.unity";
   if(File.Exists(path))return;
   if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
   var map=LevelLayoutIO.Read().maps.First(m=>m.id=="Arena_01");var layout=LevelLayoutIO.Create(map,"Arena_01 关卡编辑");
   Directory.CreateDirectory(LevelLayoutIO.SceneFolder);
   if(!EditorSceneManager.SaveScene(layout.gameObject.scene,path))throw new InvalidOperationException("Cannot save starter editing scene");
   AssetDatabase.Refresh();Debug.Log("BOUND_LEVEL_STARTER_READY");
  }
  void OnDisable(){SceneView.duringSceneGui-=SceneGUI;Selection.selectionChanged-=SelectChanged;Undo.undoRedoPerformed-=Changed;if(inspector!=null)DestroyImmediate(inspector);}
  void RefreshCatalog(){try{catalog=LevelLayoutIO.Read();mapIndex=Mathf.Clamp(mapIndex,0,catalog.maps.Length-1);}catch(Exception e){Message(e.Message,MessageType.Error);}}
  void SelectChanged(){if(Selection.activeGameObject!=null){var selected=Selection.activeGameObject.GetComponentInParent<LevelLayout>();if(selected!=null)root=selected;}Repaint();}
  void Changed(){SceneView.RepaintAll();Repaint();}
  void Message(string text,MessageType type=MessageType.Info){status=text;statusType=type;Repaint();}
  void OnGUI(){
   using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode)){
    scroll=EditorGUILayout.BeginScrollView(scroll);
    EditorGUILayout.LabelField("Bound Shoot 关卡编辑器",EditorStyles.boldLabel);
    if(catalog?.maps==null){if(GUILayout.Button("读取配置"))RefreshCatalog();EditorGUILayout.EndScrollView();return;}
    mapIndex=EditorGUILayout.Popup("关卡",mapIndex,catalog.maps.Select(m=>m.name+"  ["+m.id+"]").ToArray());
    EditorGUILayout.BeginHorizontal();
    if(GUILayout.Button("载入地图"))Load(catalog.maps[mapIndex]);
    if(GUILayout.Button("刷新列表"))RefreshCatalog();
    EditorGUILayout.EndHorizontal();
    newId=EditorGUILayout.TextField("新关卡ID",newId);newName=EditorGUILayout.TextField("新关卡名称",newName);
    if(GUILayout.Button("复制当前布局为新关卡"))DuplicateMap();
    EditorGUILayout.Space();
    root=(LevelLayout)EditorGUILayout.ObjectField("当前编辑对象",root,typeof(LevelLayout),true);
    if(root!=null){
     UnityEditor.Editor.CreateCachedEditor(root,null,ref inspector);inspector.OnInspectorGUI();
     EditorGUILayout.Space();EditorGUILayout.LabelField("物件面板",EditorStyles.boldLabel);
     int chosen=GUILayout.SelectionGrid(brush,Palette,2,GUILayout.Height(250));
     if(chosen!=brush){brush=chosen;placing=brush>=0;SceneView.RepaintAll();}
     placing=GUILayout.Toggle(placing,"在 Scene 左键连续放置（Esc退出）","Button");
     EditorGUILayout.BeginHorizontal();
     if(GUILayout.Button("放到场景中心"))Place(Vector2.zero);
     if(GUILayout.Button("定位关卡"))Frame();
     EditorGUILayout.EndHorizontal();
     EditorGUILayout.BeginHorizontal();
     if(GUILayout.Button("复制选中物件"))DuplicatePieces();
     if(GUILayout.Button("删除选中物件"))DeletePieces();
     EditorGUILayout.EndHorizontal();
     if(GUILayout.Button("选中物件吸附到网格"))Snap(SelectedPieces());
     EditorGUILayout.Space();EditorGUILayout.LabelField("检查与保存",EditorStyles.boldLabel);
     if(GUILayout.Button("检查关卡"))Validate();
     if(GUILayout.Button("保存到游戏配置",GUILayout.Height(30)))SaveConfig();
     if(GUILayout.Button("保存编辑场景"))SaveScene();
     if(GUILayout.Button("打开游戏入口 Boot")){
      if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene("Assets/BoundShoot/Scenes/Boot.unity");
     }
     EditorGUILayout.HelpBox("W移动、R缩放；矩形右上角手柄调整宽高。Ctrl+D复制、Delete删除、Ctrl+Z撤销。0秒刷新表示只出现一次。保存到游戏配置后才影响实际关卡。",MessageType.None);
    }else EditorGUILayout.HelpBox("选择关卡后载入。在独立编辑场景中放置物件，不改Boot场景。",MessageType.Info);
    if(!string.IsNullOrEmpty(status))EditorGUILayout.HelpBox(status,statusType);
    EditorGUILayout.EndScrollView();
   }
  }
  void Load(MapDefinition map){
   if(root!=null&&!EditorUtility.DisplayDialog("载入关卡","会替换当前编辑布局；尚未保存的布局请先保存。","载入","取消"))return;
   if(root==null&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
   if(root==null)root=LevelLayoutIO.Create(map);else LevelLayoutIO.Populate(root,map);
   Selection.activeGameObject=root.gameObject;placing=false;Frame();Message("已载入 "+map.name);
  }
  void DuplicateMap(){
   try{
    if(catalog.maps.Any(m=>m.id==newId))throw new InvalidOperationException("新关卡ID已存在。");
    var map=root!=null?LevelLayoutIO.Collect(root):JsonUtility.FromJson<MapDefinition>(JsonUtility.ToJson(catalog.maps[mapIndex]));
    map.id=newId;map.name=newName;map.available=false;
    if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
    root=LevelLayoutIO.Create(map);
    root.newMap=true;root.sourceMapId="";root.sourceMapJson="";Selection.activeGameObject=root.gameObject;placing=false;Frame();Message("新关卡默认为未开放；编辑完成后可勾选开放并保存。");
   }catch(Exception e){Message(e.Message,MessageType.Error);}
  }
  void Frame(){if(root==null)return;var scene=SceneView.lastActiveSceneView??GetWindow<SceneView>();scene.in2DMode=true;scene.orthographic=true;scene.LookAt(Vector3.zero,Quaternion.identity,Mathf.Max(root.dimensions.x,root.dimensions.y)*.58f);scene.drawGizmos=true;scene.Repaint();}
  void Place(Vector2 point){
   if(root==null||brush<0)return;
   if(root.snap)point=Snapped(point,root.grid);
   LevelPieceKind kind=brush==0?LevelPieceKind.Floor:brush==1?LevelPieceKind.Obstacle:brush<4?LevelPieceKind.Spawn:brush==4?LevelPieceKind.Revival:LevelPieceKind.Pickup;
   Vector2 size=kind==LevelPieceKind.Floor?new Vector2(4,4):kind==LevelPieceKind.Obstacle?new Vector2(1.5f,1.5f):kind==LevelPieceKind.Revival?new Vector2(4,3):Vector2.one;
   if(kind==LevelPieceKind.Spawn){
    int team=brush==2?0:1;var existing=root.GetComponentsInChildren<LevelPiece>();
    int seat=Enumerable.Range(0,4).FirstOrDefault(s=>!existing.Any(p=>p.kind==kind&&p.team==team&&p.seat==s));
    if(existing.Count(p=>p.kind==kind&&p.team==team)>=4){Message("每队已有4个出生点，请选择并移动现有出生点。",MessageType.Warning);return;}
    var spawn=LevelLayoutIO.Add(root,kind,point,size);spawn.team=team;spawn.seat=seat;LevelLayoutIO.Rename(spawn);Selection.activeGameObject=spawn.gameObject;
   }else{
    var piece=LevelLayoutIO.Add(root,kind,point,size);
    if(kind==LevelPieceKind.Pickup){if(brush<=8)piece.weapon=(WeaponKind)(brush-4);else piece.item=brush==9?ItemKind.Mystery:(ItemKind)(brush-9);LevelLayoutIO.Rename(piece);}
    Selection.activeGameObject=piece.gameObject;
   }
   EditorSceneManager.MarkSceneDirty(root.gameObject.scene);Changed();
  }
  LevelPiece[] SelectedPieces()=>Selection.gameObjects.Select(g=>g.GetComponent<LevelPiece>()).Where(p=>p!=null&&p.GetComponentInParent<LevelLayout>()==root).ToArray();
  void DuplicatePieces(){foreach(var p in SelectedPieces()){
    if(p.kind==LevelPieceKind.Spawn){Message("出生点请使用出生点面板补齐空座位。",MessageType.Warning);continue;}
    var copy=Instantiate(p.gameObject,p.transform.parent);Undo.RegisterCreatedObjectUndo(copy,"复制关卡物件");copy.transform.localPosition+=new Vector3(root.grid,root.grid,0);Selection.activeGameObject=copy;
   }Changed();}
  void DeletePieces(){foreach(var p in SelectedPieces())Undo.DestroyObjectImmediate(p.gameObject);Changed();}
  static Vector2 Snapped(Vector2 point,float grid){grid=Mathf.Max(.1f,grid);return new Vector2(Mathf.Round(point.x/grid)*grid,Mathf.Round(point.y/grid)*grid);}
  public static void Snap(IEnumerable<LevelPiece> pieces){foreach(var p in pieces){var root=p.GetComponentInParent<LevelLayout>();if(root==null)continue;Undo.RecordObject(p.transform,"关卡网格吸附");p.transform.localPosition=Snapped(p.transform.localPosition,root.grid);if(p.Rectangular){Vector2 size=Snapped(p.transform.localScale,root.grid);p.transform.localScale=new Vector3(Mathf.Max(.1f,size.x),Mathf.Max(.1f,size.y),1);}}SceneView.RepaintAll();}
  void Validate(){try{var issues=LevelLayoutIO.Validate(root);Message(issues.Count==0?"检查通过。":string.Join("\n",issues),issues.Count==0?MessageType.Info:MessageType.Error);}catch(Exception e){Message(e.Message,MessageType.Error);}}
  void SaveConfig(){try{LevelLayoutIO.Save(root);RefreshCatalog();Message("已保存到GameConfig.json。客户端与服务器更新后生效。");}catch(Exception e){Message(e.Message,MessageType.Error);}}
  void SaveScene(){
   try{
    if(!System.Text.RegularExpressions.Regex.IsMatch(root.mapId,"^[A-Za-z][A-Za-z0-9_]{0,47}$"))throw new InvalidOperationException("请先填写合法地图ID。");
    Directory.CreateDirectory(LevelLayoutIO.SceneFolder);string path=LevelLayoutIO.SceneFolder+"/"+root.mapId+".unity";
    if(File.Exists(path)&&root.gameObject.scene.path!=path&&!EditorUtility.DisplayDialog("覆盖编辑场景",path+"已存在，是否覆盖？","覆盖","取消"))return;
    if(!EditorSceneManager.SaveScene(root.gameObject.scene,path))throw new InvalidOperationException("场景保存失败。");
    AssetDatabase.Refresh();Message("已保存编辑场景；实际游戏使用“保存到游戏配置”的数据。");
   }catch(Exception e){Message(e.Message,MessageType.Error);}
  }
  void SceneGUI(SceneView scene){
   if(root==null||EditorApplication.isPlayingOrWillChangePlaymode)return;
   Event e=Event.current;int control=GUIUtility.GetControlID(FocusType.Passive);
   if(e.type==EventType.Layout)HandleUtility.AddDefaultControl(control);
   var ray=HandleUtility.GUIPointToWorldRay(e.mousePosition);if(!new Plane(Vector3.forward,Vector3.zero).Raycast(ray,out float hit))return;
   Vector2 point=root.transform.InverseTransformPoint(ray.GetPoint(hit));
   if(placing&&brush>=0){Handles.color=Color.white;Handles.DrawWireDisc(root.transform.TransformPoint(root.snap?Snapped(point,root.grid):point),Vector3.forward,.3f);scene.Repaint();}
   if(e.type==EventType.KeyDown&&e.keyCode==KeyCode.Escape){placing=false;e.Use();Repaint();}
   if(e.type==EventType.MouseDown&&e.button==0&&!e.alt&&HandleUtility.nearestControl==control){
    if(placing&&brush>=0)Place(point);
    else{
     var piece=root.GetComponentsInChildren<LevelPiece>(false).Where(p=>{
      Vector2 pos=p.transform.localPosition,size=p.Rectangular?(Vector2)p.transform.localScale:Vector2.one;
      return new Rect(pos-size*.5f,size).Contains(point);
     }).OrderBy(p=>p.kind==LevelPieceKind.Floor?1:0).ThenBy(p=>p.Rectangular?p.transform.localScale.x*p.transform.localScale.y:0).FirstOrDefault();
     Selection.activeGameObject=piece!=null?piece.gameObject:root.gameObject;
    }
    e.Use();
   }
  }
 }
}
