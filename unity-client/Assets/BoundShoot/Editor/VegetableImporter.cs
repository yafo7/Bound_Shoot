using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace BoundShoot.Editor {
 public static class VegetableImporter {
  const string Root="Assets/BoundShoot/Resources/Vegetables/";
  [MenuItem("Bound Shoot/Art/Prepare Vegetable Demo")]
  public static void Prepare(){
   AssetDatabase.Refresh();Directory.CreateDirectory(Root+"Slices");AssetDatabase.Refresh();
   Slice("identities",4,2,Enumerable.Range(0,8).Select(i=>"portrait"+i).ToArray());
   for(int i=0;i<8;i++)Slice("veg"+i,6,6,Enumerable.Range(0,36).Select(f=>"veg"+i+"_f"+f.ToString("00")).ToArray(),true);
   Slice("equipment",4,3,new[]{"weapon1","weapon2","weapon3","weapon4","item1","item2","item3","item4","item5","item6","item7","item8"});
   Slice("terrain",4,2,new[]{"stone","mound","crate","fence","canopy","trunk","grass","revival"});
   Slice("ground",2,2,new[]{"floor","dirt","floorDark","dirtPale"});
   Slice("effects",6,4,Enumerable.Range(0,24).Select(i=>"fx"+i).ToArray());
   // Publish the map definition locally without changing teammate weapon tuning.
   var configPath="Assets/BoundShoot/Resources/GameConfig.json";var config=JsonUtility.FromJson<GameConfig>(File.ReadAllText(configPath));
   if(!config.maps.Any(m=>m.id==FieldMap.Id)){config.maps=config.maps.Concat(new[]{FieldMap.Create()}).ToArray();File.WriteAllText(configPath,JsonUtility.ToJson(config,true));}
   AssetDatabase.ImportAsset(configPath,ImportAssetOptions.ForceUpdate);VegetableArt.Reload();AssetDatabase.SaveAssets();
   Debug.Log("Vegetable demo: 344 sprite assets imported; field map registered. No build or remote update.");
  }
  static void Slice(string atlas,int columns,int rows,string[] names,bool motion=false){
   var path=Root+atlas+".png";var imp=(TextureImporter)AssetImporter.GetAtPath(path);
   imp.textureType=TextureImporterType.Default;imp.isReadable=true;imp.filterMode=FilterMode.Point;imp.mipmapEnabled=false;
   imp.alphaIsTransparency=true;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.maxTextureSize=2048;imp.npotScale=TextureImporterNPOTScale.None;imp.SaveAndReimport();
   var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(path);var pixels=tex.GetPixels32();
   for(int i=0;i<names.Length;i++){
    int x0=i%columns*tex.width/columns,x1=(i%columns+1)*tex.width/columns;
    int y0=(rows-1-i/columns)*tex.height/rows,y1=(rows-i/columns)*tex.height/rows;
    if(motion){x1=x0+tex.width/columns;y1=y0+tex.height/rows;}
    if(atlas=="equipment"&&i<4){float[] edges={0,.29f,.49f,.765f,1};x0=Mathf.RoundToInt(edges[i]*tex.width);x1=Mathf.RoundToInt(edges[i+1]*tex.width);}
    if(atlas=="ground"){
     // The generator grouped the 2x2 samples in the centre of a wide canvas.
     x0=Mathf.RoundToInt((i%2==0?.27f:.51f)*tex.width);x1=Mathf.RoundToInt((i%2==0?.48f:.72f)*tex.width);
    }
    if(!motion){
     int left=x1,right=x0,bottom=y1,top=y0;
     for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++)if(pixels[y*tex.width+x].a>180){left=Mathf.Min(left,x);right=Mathf.Max(right,x);bottom=Mathf.Min(bottom,y);top=Mathf.Max(top,y);}
     if(right<left)throw new InvalidOperationException("Empty art cell "+names[i]);
     x0=left;x1=right+1;y0=bottom;y1=top+1;
    }
    if(atlas=="ground"){int insetX=(x1-x0)/10,insetY=(y1-y0)/10;x0+=insetX;x1-=insetX;y0+=insetY;y1-=insetY;}
    var pivot=Vector2.one*.5f;
    if(motion&&i<24){
     // AI sheets place each pose at a slightly different location in its cell.
     // Keep the full, identical canvas but register opaque feet and body centre.
     int left=x1,right=x0,bottom=y1;
     for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++)if(pixels[y*tex.width+x].a>180){left=Mathf.Min(left,x);right=Mathf.Max(right,x);bottom=Mathf.Min(bottom,y);}
     if(right<left)throw new InvalidOperationException("Empty motion cell "+names[i]);
     pivot=new Vector2(((left+right+1)*.5f-x0)/(x1-x0),(bottom-y0)/(float)(y1-y0));
    }
    var rect=new Rect(x0,y0,x1-x0,y1-y0);var sprite=Sprite.Create(tex,rect,pivot,100,0,SpriteMeshType.FullRect);sprite.name=names[i];
    var target=Root+"Slices/"+names[i]+".asset";var old=AssetDatabase.LoadAssetAtPath<Sprite>(target);
    if(old==null)AssetDatabase.CreateAsset(sprite,target);else{EditorUtility.CopySerialized(sprite,old);EditorUtility.SetDirty(old);UnityEngine.Object.DestroyImmediate(sprite);}
   }
  }
  [MenuItem("Bound Shoot/Play Vegetable Demo")]
  public static void OpenGame(){if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;EditorSceneManager.OpenScene("Assets/BoundShoot/Scenes/Boot.unity");EditorApplication.isPlaying=true;}
 }
}
