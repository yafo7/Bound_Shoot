using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace BoundShoot.Editor {
 public static class VegetablePreview {
  [MenuItem("Bound Shoot/Art/Open Vegetable Preview")]
  public static void Create(){
   Draw2D.RenderInBatch=true;VegetableArt.Reload();
   var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);new GameObject("Vegetable preview").AddComponent<ArtPreviewMarker>();
   var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.orthographic=true;camera.orthographicSize=10;camera.transform.position=new Vector3(0,0,-20);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.23f,.32f,.15f);
   var arena=new GameObject("Harvest Meadow").AddComponent<ArenaView>();arena.Build(FieldMap.Create());
   Vector2[] poses={new Vector2(-9,3),new Vector2(-10,-2),new Vector2(9,3),new Vector2(10,-2),new Vector2(-4,-7),new Vector2(-1,-7),new Vector2(2,-7),new Vector2(5,-7)};
   for(int i=0;i<8;i++){
    var actor=new GameObject("Vegetable "+i);actor.transform.position=poses[i];var view=actor.AddComponent<CyberPlayerView>();view.Build();view.ShowCharacter(i,i%4<2?0:1,i%4+1,i%4<2?0:180,true,false);
   }
   for(int t=0;t<2;t++){var rope=Draw2D.Line(arena.transform,t==0?Draw2D.Blue:Draw2D.Red,.09f);rope.positionCount=3;rope.SetPosition(0,poses[t*2]);rope.SetPosition(1,new Vector2(t==0?-12:12,0));rope.SetPosition(2,poses[t*2+1]);}
   for(int i=1;i<=8;i++)CyberArt.Place(arena.transform,"item"+i,new Vector2(-8+i*2,7),Vector2.one*.8f,6);
   Directory.CreateDirectory("Assets/BoundShoot/Scenes");EditorSceneManager.SaveScene(scene,"Assets/BoundShoot/Scenes/VegetablePreview.unity");
   string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/VegetableDemo"));Directory.CreateDirectory(folder);
   Capture(camera,Path.Combine(folder,"meadow.png"));camera.orthographicSize=36;Capture(camera,Path.Combine(folder,"map-overview.png"));camera.orthographicSize=10;
   Draw2D.RenderInBatch=false;AssetDatabase.SaveAssets();Debug.Log("Vegetable preview camera screenshots saved: "+folder);
  }
  static void Capture(Camera camera,string output){
   var render=new RenderTexture(1600,900,24);var old=RenderTexture.active;camera.targetTexture=render;camera.Render();RenderTexture.active=render;
   var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(output,image.EncodeToPNG());
   camera.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(render);UnityEngine.Object.DestroyImmediate(image);
  }
 }
}
