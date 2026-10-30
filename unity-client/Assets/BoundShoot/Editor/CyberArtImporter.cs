using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace BoundShoot.Editor {
 public static class CyberArtImporter {
  const string Root="Assets/BoundShoot/Resources/Art/";
  [MenuItem("Bound Shoot/Art/Import Cyberpunk Sprites")]
  public static void Import(){
   AssetDatabase.Refresh();
   Slice("actors",4,2,new[]{"actor0","actor1","actor2","actor3","weapon1","weapon2","weapon3","weapon4"});
   Slice("items",4,2,new[]{"item1","item2","item3","item4","item5","item6","item7","item8"});
   Slice("environment",2,2,new[]{"floor","cover","barrier","revival"});
   Debug.Log("Cyberpunk art imported: 20 sprites. No game build created.");
   AssetDatabase.ImportAsset("Assets/BoundShoot/Scripts/Gameplay/NetPlayer.cs",ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
   var player=Resources.Load<GameObject>("NetPlayer");
   if(player==null||player.GetComponent<NetPlayer>()==null)throw new InvalidOperationException("NetPlayer prefab script must resolve before preview.");
  }
  static void Slice(string atlas,int columns,int rows,string[] names){
   string path=Root+atlas+".png";var importer=(TextureImporter)AssetImporter.GetAtPath(path);
   importer.textureType=TextureImporterType.Default;importer.isReadable=true;
   importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;importer.spritePixelsPerUnit=100;importer.npotScale=TextureImporterNPOTScale.None;importer.SaveAndReimport();
   var t=AssetDatabase.LoadAssetAtPath<Texture2D>(path);var pixels=t.GetPixels32();var sheets=new List<SpriteMetaData>();
   for(int i=0;i<names.Length;i++){
    float left=(i%columns)/(float)columns,right=(i%columns+1)/(float)columns;
    if(atlas=="actors"&&i>=4){float[] edges={0,.315f,.48f,.76f,1};left=edges[i-4];right=edges[i-3];}
    if(atlas=="environment"&&i>=2){left=i==2?0:.6f;right=i==2?.6f:1;}
    int minX=Mathf.RoundToInt(left*t.width),maxX=Mathf.RoundToInt(right*t.width)-1;
    int minY=(rows-1-i/columns)*t.height/rows,maxY=(rows-i/columns)*t.height/rows-1;
    int x0=maxX,y0=maxY,x1=minX,y1=minY;
    for(int y=minY;y<=maxY;y++)for(int x=minX;x<=maxX;x++)if(pixels[y*t.width+x].a>160){x0=Mathf.Min(x0,x);x1=Mathf.Max(x1,x);y0=Mathf.Min(y0,y);y1=Mathf.Max(y1,y);}
    if(x1<x0)throw new InvalidOperationException("Empty sprite "+names[i]);
    sheets.Add(new SpriteMetaData{name=names[i],rect=new Rect(x0,y0,x1-x0+1,y1-y0+1),alignment=(int)SpriteAlignment.Center,pivot=Vector2.one*.5f});
   }
   Directory.CreateDirectory(Root+"Slices");AssetDatabase.Refresh();
   foreach(var slice in sheets){
    var sprite=Sprite.Create(t,slice.rect,Vector2.one*.5f,100,0,SpriteMeshType.FullRect);sprite.name=slice.name;
    string output=Root+"Slices/"+slice.name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Sprite>(output);
    if(existing==null)AssetDatabase.CreateAsset(sprite,output);else{EditorUtility.CopySerialized(sprite,existing);EditorUtility.SetDirty(existing);UnityEngine.Object.DestroyImmediate(sprite);}
   }
   AssetDatabase.SaveAssets();
  }
  [MenuItem("Bound Shoot/Art/Open Art Preview 美术预览")]
  public static void OpenPreview(){
   if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
   string path="Assets/BoundShoot/Scenes/ArtPreview.unity";
   if(File.Exists(path))EditorSceneManager.OpenScene(path);else PreparePreview();
  }
  public static void PreparePreview(){
   Import();Draw2D.RenderInBatch=true;
   var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
   new GameObject("Art preview").AddComponent<ArtPreviewMarker>();
   var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.orthographic=true;camera.orthographicSize=12;camera.transform.position=new Vector3(0,0,-20);camera.backgroundColor=new Color(.025f,.04f,.08f);camera.clearFlags=CameraClearFlags.SolidColor;
   var map=TrainingMap.Create();map.halfWidth=17;map.halfHeight=10;
   map.blocks=new[]{new BlockDefinition{center=new Vector2(-4,4),size=new Vector2(3,2)},new BlockDefinition{center=new Vector2(2,1),size=new Vector2(2,3)},new BlockDefinition{center=new Vector2(-4,-4),size=new Vector2(5,2)},new BlockDefinition{center=new Vector2(9,0),size=new Vector2(2,2)}};
   map.revivalZones=new[]{new RevivalDefinition{center=new Vector2(12,6),size=new Vector2(3,2)}};
   var arena=new GameObject("Arena").AddComponent<ArenaView>();arena.Build(map);
   Vector2[] positions={new Vector2(-10,4),new Vector2(-10,-3),new Vector2(11,3),new Vector2(11,-4)};
   for(int i=0;i<4;i++){
    var p=new GameObject("Player "+(i+1));p.transform.position=positions[i];var v=p.AddComponent<CyberPlayerView>();v.Build();v.Show(i/2,i==0?1:i==1?3:i==2?2:3,i<2?0:180,true,i==2);
   }
   for(int team=0;team<2;team++){var line=Draw2D.Line(arena.transform,team==0?Draw2D.Blue:Draw2D.Red,.09f);line.positionCount=3;line.SetPosition(0,positions[team*2]);line.SetPosition(1,new Vector2(team==0?-13:14,0));line.SetPosition(2,positions[team*2+1]);}
   for(int i=1;i<=8;i++)CyberArt.Place(arena.transform,"item"+i,new Vector2(-7+(i-1)*2,-7),Vector2.one*.85f,6);
   for(int i=1;i<=4;i++)CyberArt.Place(arena.transform,"weapon"+i,new Vector2(-8+i*3.2f,8),new Vector2(1.7f,.65f),6);
   string directory="Assets/BoundShoot/Scenes";Directory.CreateDirectory(directory);EditorSceneManager.SaveScene(scene,directory+"/ArtPreview.unity");
   string screenshot=Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/ArtPreview.png"));Directory.CreateDirectory(Path.GetDirectoryName(screenshot));
   var rt=new RenderTexture(1600,900,24);camera.targetTexture=rt;camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;
   var shot=new Texture2D(1600,900,TextureFormat.RGB24,false);shot.ReadPixels(new Rect(0,0,1600,900),0,0);shot.Apply();
   File.WriteAllBytes(screenshot,shot.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(shot);
   Draw2D.RenderInBatch=false;AssetDatabase.SaveAssets();Debug.Log("ArtPreview saved and camera render exported.");
  }
 }
}
