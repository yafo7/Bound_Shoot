using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Unity.Netcode;
namespace BoundShoot.Editor {
 public static class BuildTools {
  const string Scene="Assets/BoundShoot/Scenes/Boot.unity";
  [MenuItem("Bound Shoot/1. Prepare Project")]
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
   PlayerSettings.insecureHttpOption=InsecureHttpOption.DevelopmentOnly;
   var tags=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
   tags.FindProperty("layers").GetArrayElementAtIndex(8).stringValue="ArenaWall";tags.ApplyModifiedProperties();
   AssetDatabase.SaveAssets();AssetDatabase.Refresh();Debug.Log("BOUND_PROJECT_PREPARED");
  }
  [MenuItem("Bound Shoot/2. Build Windows Client + Local Server")]
  public static void Windows(){
   Prepare();PlayerSettings.insecureHttpOption=InsecureHttpOption.AlwaysAllowed; // LAN developer build; production must use HTTPS.
   string target=System.Environment.GetEnvironmentVariable("BOUND_BUILD_DIR");
   if(string.IsNullOrEmpty(target))target=Path.GetFullPath("../BoundShootBuilds/Windows");
   Directory.CreateDirectory(target);
   var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene},locationPathName=Path.Combine(target,"BoundShoot.exe"),target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
   if(report.summary.result!=BuildResult.Succeeded)throw new System.Exception("Windows build failed: "+report.summary.result);
   Debug.Log("BOUND_BUILD_SUCCEEDED "+target);
  }
  [MenuItem("Bound Shoot/3. Build Linux Dedicated Server")]
  public static void LinuxServer(){
   Prepare();PlayerSettings.insecureHttpOption=InsecureHttpOption.AlwaysAllowed;
   string target=System.Environment.GetEnvironmentVariable("BOUND_BUILD_DIR")??Path.GetFullPath("../BoundShootBuilds/LinuxServer");
   Directory.CreateDirectory(target);
   var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene},locationPathName=Path.Combine(target,"BoundShootServer.x86_64"),target=BuildTarget.StandaloneLinux64,subtarget=(int)StandaloneBuildSubtarget.Server,options=BuildOptions.None});
   if(report.summary.result!=BuildResult.Succeeded)throw new System.Exception("Linux server build failed (install Linux Dedicated Server support): "+report.summary.result);
  }
 }
}
