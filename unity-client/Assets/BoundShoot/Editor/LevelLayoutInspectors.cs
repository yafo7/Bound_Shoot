using System.Linq;
using BoundShoot.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BoundShoot.Editor {
 [CustomEditor(typeof(LevelLayout))]
 public sealed class LevelLayoutInspector:UnityEditor.Editor {
  public override void OnInspectorGUI(){
   serializedObject.Update();
   EditorGUILayout.PropertyField(serializedObject.FindProperty("mapId"),new GUIContent("地图ID"));
   EditorGUILayout.PropertyField(serializedObject.FindProperty("mapName"),new GUIContent("显示名称"));
   EditorGUILayout.PropertyField(serializedObject.FindProperty("available"),new GUIContent("在游戏中开放"));
   EditorGUILayout.PropertyField(serializedObject.FindProperty("dimensions"),new GUIContent("地图宽高"));
   EditorGUILayout.PropertyField(serializedObject.FindProperty("groundColor"),new GUIContent("基础地面颜色"));
   EditorGUILayout.PropertyField(serializedObject.FindProperty("theme"),new GUIContent("主题（forest森林/meadow草地）"));
   if(serializedObject.FindProperty("theme").stringValue=="forest")EditorGUILayout.PropertyField(serializedObject.FindProperty("plateau"),new GUIContent("高台外观与台阶"),true);
   EditorGUILayout.PropertyField(serializedObject.FindProperty("snap"),new GUIContent("网格吸附"));
   EditorGUILayout.PropertyField(serializedObject.FindProperty("grid"),new GUIContent("网格大小"));
   serializedObject.ApplyModifiedProperties();
   if(GUILayout.Button("打开关卡编辑器"))LevelEditorWindow.Open();
  }
 }
 [CustomEditor(typeof(LevelPiece))]
 public sealed class LevelPieceInspector:UnityEditor.Editor {
  public override void OnInspectorGUI(){
   var piece=(LevelPiece)target;serializedObject.Update();
   EditorGUILayout.LabelField(LevelLayoutIO.Label(piece),EditorStyles.boldLabel);
   var t=piece.transform;EditorGUI.BeginChangeCheck();
   var position=EditorGUILayout.Vector2Field("位置 XY",t.localPosition);
   Vector2 size=t.localScale;
   if(piece.Rectangular)size=EditorGUILayout.Vector2Field("宽高",size);
   if(EditorGUI.EndChangeCheck()){
    Undo.RecordObject(t,"修改关卡位置与尺寸");t.localPosition=position;
    t.localScale=piece.Rectangular?new Vector3(size.x,size.y,1):Vector3.one;EditorUtility.SetDirty(t);
   }
   switch(piece.kind){
    case LevelPieceKind.Obstacle:Popup("visual","外观",new[]{"石块","土堆","木箱","栅栏","树干与树冠","崖壁（forest）"});break;
    case LevelPieceKind.Floor:EditorGUILayout.PropertyField(serializedObject.FindProperty("color"),new GUIContent("地面颜色"));break;
    case LevelPieceKind.Spawn:
     Popup("team","队伍",new[]{"蓝队","红队"});Popup("seat","队内位置",new[]{"1","2","3（3v3/4v4）","4（4v4）"});break;
    case LevelPieceKind.Pickup:
     bool weapon=piece.weapon!=WeaponKind.None;
     int category=EditorGUILayout.Popup("拾取物分类",weapon?0:1,new[]{"武器","道具"});
     if(category==0){serializedObject.FindProperty("item").intValue=0;var p=serializedObject.FindProperty("weapon");p.intValue=EditorGUILayout.Popup("武器",Mathf.Clamp(p.intValue-1,0,3),new[]{"狙击枪","手枪","AK","匕首"})+1;}
     else{serializedObject.FindProperty("weapon").intValue=0;var p=serializedObject.FindProperty("item");p.intValue=EditorGUILayout.Popup("道具",Mathf.Clamp(p.intValue-1,0,7),new[]{"绳索收缩","绳索延长","护盾","冲刺装置","炸弹","弹性强化","血包","问号盒"})+1;}
     EditorGUILayout.PropertyField(serializedObject.FindProperty("respawnSeconds"),new GUIContent("刷新秒数（0不刷新）"));
     EditorGUILayout.PropertyField(serializedObject.FindProperty("randomizeOnRespawn"),new GUIContent("刷新时随机替换内容"));break;
   }
   if(serializedObject.ApplyModifiedProperties()){LevelLayoutIO.Rename(piece);SceneView.RepaintAll();}
   EditorGUILayout.Space();
   EditorGUILayout.BeginHorizontal();
   if(GUILayout.Button("吸附到网格"))LevelEditorWindow.Snap(new[]{piece});
   if(GUILayout.Button("恢复XY平面")){Undo.RecordObject(t,"恢复XY平面");t.localRotation=Quaternion.identity;t.localPosition=new Vector3(t.localPosition.x,t.localPosition.y,0);t.localScale=new Vector3(t.localScale.x,t.localScale.y,1);}
   EditorGUILayout.EndHorizontal();
  }
  void Popup(string field,string label,string[] options){var p=serializedObject.FindProperty(field);p.intValue=EditorGUILayout.Popup(label,Mathf.Clamp(p.intValue,0,options.Length-1),options);}
  void OnSceneGUI(){
   var piece=(LevelPiece)target;if(!piece.Rectangular)return;
   var t=piece.transform;var root=piece.GetComponentInParent<LevelLayout>();
   if(root==null)return;
   using(new Handles.DrawingScope(root.transform.localToWorldMatrix)){
    Vector3 center=t.localPosition;Vector3 size=t.localScale;
    EditorGUI.BeginChangeCheck();
    Vector3 corner=Handles.Slider2D(center+new Vector3(size.x,size.y,0)*.5f,Vector3.forward,Vector3.right,Vector3.up,
     HandleUtility.GetHandleSize(t.position)*.07f,Handles.RectangleHandleCap,root.snap?root.grid:0);
    if(EditorGUI.EndChangeCheck()){
     Undo.RecordObject(t,"调整关卡物件宽高");Vector2 delta=(corner-center)*2;
     t.localScale=new Vector3(Mathf.Max(.1f,delta.x),Mathf.Max(.1f,delta.y),1);EditorUtility.SetDirty(t);
    }
   }
  }
 }
 public static class LevelLayoutGizmos {
  public static Color ColorOf(LevelPiece p){
   switch(p.kind){
    case LevelPieceKind.Floor:return p.color;
    case LevelPieceKind.Obstacle:return new Color(.48f,.55f,.6f);
    case LevelPieceKind.Spawn:return p.team==0?Draw2D.Blue:Draw2D.Red;
    case LevelPieceKind.Revival:return new Color(.2f,.8f,.55f,.6f);
    default:return p.weapon!=WeaponKind.None?Draw2D.Gold:new Color(.75f,.5f,1);
   }
  }
  public static Vector3[] Corners(Vector2 p,Vector2 size){Vector2 h=size*.5f;return new[]{(Vector3)(p+new Vector2(-h.x,-h.y)),(Vector3)(p+new Vector2(h.x,-h.y)),(Vector3)(p+h),(Vector3)(p+new Vector2(-h.x,h.y))};}
  [DrawGizmo(GizmoType.NonSelected|GizmoType.Selected)]
  static void Draw(LevelLayout root,GizmoType type){
   if(root==null||Application.isPlaying)return;
   if(float.IsNaN(root.dimensions.x)||float.IsNaN(root.dimensions.y)||float.IsInfinity(root.dimensions.x)||float.IsInfinity(root.dimensions.y)||root.dimensions.x<=0||root.dimensions.y<=0||root.dimensions.x>200||root.dimensions.y>200)return;
   var previousZ=Handles.zTest;
   try{
   using(new Handles.DrawingScope(root.transform.localToWorldMatrix)){
    Handles.zTest=UnityEngine.Rendering.CompareFunction.Always;
    Handles.DrawSolidRectangleWithOutline(Corners(Vector2.zero,root.dimensions),root.groundColor,Color.white);
    float grid=Mathf.Max(.25f,root.grid);Handles.color=new Color(1,1,1,.08f);
    for(float x=-root.dimensions.x*.5f;x<=root.dimensions.x*.5f;x+=grid)Handles.DrawLine(new Vector3(x,-root.dimensions.y*.5f),new Vector3(x,root.dimensions.y*.5f));
    for(float y=-root.dimensions.y*.5f;y<=root.dimensions.y*.5f;y+=grid)Handles.DrawLine(new Vector3(-root.dimensions.x*.5f,y),new Vector3(root.dimensions.x*.5f,y));
    var style=new GUIStyle(EditorStyles.whiteMiniLabel){alignment=TextAnchor.MiddleCenter,fontSize=11};
    Handles.color=Color.white;
    var parts=root.GetComponentsInChildren<LevelPiece>(false).OrderBy(p=>p.kind==LevelPieceKind.Floor?0:p.kind==LevelPieceKind.Revival?1:2);
    foreach(var p in parts){
     Vector2 pos=p.transform.localPosition;Color color=ColorOf(p);bool selected=Selection.Contains(p.gameObject);
     Vector2 size=p.Rectangular?(Vector2)p.transform.localScale:Vector2.one*.85f;
     Handles.DrawSolidRectangleWithOutline(Corners(pos,size),color,selected?Color.white:new Color(0,0,0,.5f));
     if(p.kind!=LevelPieceKind.Floor)Handles.Label(pos,LevelLayoutIO.Label(p),style);
    }
    for(int team=0;team<2;team++){
     var spawns=root.GetComponentsInChildren<LevelPiece>().Where(p=>p.kind==LevelPieceKind.Spawn&&p.team==team&&p.seat<2).OrderBy(p=>p.seat).ToArray();
     if(spawns.Length==2){Handles.color=team==0?Draw2D.Blue:Draw2D.Red;Handles.DrawDottedLine(spawns[0].transform.localPosition,spawns[1].transform.localPosition,5);}
    }
   }
   }finally{Handles.zTest=previousZ;}
  }
 }
}
