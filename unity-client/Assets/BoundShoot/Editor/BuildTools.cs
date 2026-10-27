using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Netcode;
namespace BoundShoot.Editor {
 public static class BuildTools {
  const string Scene="Assets/BoundShoot/Scenes/Boot.unity";
  [MenuItem("Bound Shoot/Prepare Project")]
  public static void Prepare(){
   Directory.CreateDirectory("Assets/BoundShoot/Resources");Directory.CreateDirectory("Assets/BoundShoot/Scenes");
   if(!File.Exists("Assets/BoundShoot/Resources/NetPlayer.prefab")){
    var p=new GameObject("NetPlayer",typeof(NetworkObject),typeof(Rigidbody2D),typeof(CircleCollider2D),typeof(NetPlayer));
    p.GetComponent<Rigidbody2D>().gravityScale=0;p.GetComponent<Rigidbody2D>().freezeRotation=true;
    p.GetComponent<CircleCollider2D>().radius=.42f;
    PrefabUtility.SaveAsPrefabAsset(p,"Assets/BoundShoot/Resources/NetPlayer.prefab");Object.DestroyImmediate(p);
   }
   if(!File.Exists("Assets/BoundShoot/Resources/NetWorld.prefab")){
    var p=new GameObject("NetWorld",typeof(NetworkObject),typeof(BattleWorld));PrefabUtility.SaveAsPrefabAsset(p,"Assets/BoundShoot/Resources/NetWorld.prefab");Object.DestroyImmediate(p);
   }
   if(!File.Exists(Scene)){var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorSceneManager.SaveScene(scene,Scene);}
   EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Scene,true)};
   PlayerSettings.companyName="Bound Shoot Team";PlayerSettings.productName="派对射击 - Bound Shoot";
   PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=800;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
   PlayerSettings.runInBackground=true;PlayerSettings.resizableWindow=true;
   PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone,ScriptingImplementation.Mono2x);
   PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Standalone,ApiCompatibilityLevel.NET_Standard);
   // The authenticated internal API uses loopback HTTP, including in a release server.
   PlayerSettings.insecureHttpOption=InsecureHttpOption.AlwaysAllowed;
   var tags=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
   tags.FindProperty("layers").GetArrayElementAtIndex(8).stringValue="ArenaWall";tags.ApplyModifiedProperties();
   AssetDatabase.SaveAssets();AssetDatabase.Refresh();Debug.Log("BOUND_PROJECT_PREPARED");
  }
  [MenuItem("Bound Shoot/Network Settings")]
  public static void SelectNetworkSettings(){Selection.activeObject=Resources.Load<NetworkSettings>("NetworkSettings");EditorGUIUtility.PingObject(Selection.activeObject);}
 }
}
