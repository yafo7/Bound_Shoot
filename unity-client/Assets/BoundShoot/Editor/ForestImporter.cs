using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
namespace BoundShoot.Editor {
 public static class ForestImporter {
  [MenuItem("Bound Shoot/Art/Prepare Forest Demo")]
  public static void Prepare(){
   AssetDatabase.Refresh();
   foreach(string path in Directory.GetFiles("Assets/BoundShoot/Resources/Forest","*.png")){
    var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Default;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.npotScale=TextureImporterNPOTScale.None;importer.SaveAndReimport();
   }
   const string configPath="Assets/BoundShoot/Resources/GameConfig.json";
   string original=File.ReadAllText(configPath),text=original;var config=JsonUtility.FromJson<GameConfig>(text);
   if(config.ropeSpring==45&&config.ropeDamping==6&&config.ropeMaxForce==100){
    foreach(var pair in new[]{new[]{"ropeSpring","90"},new[]{"ropeDamping","12"},new[]{"ropeMaxForce","200"}})text=Regex.Replace(text,"(\""+pair[0]+"\"\\s*:\\s*)[0-9.]+",m=>m.Groups[1].Value+pair[1]);
   }
   if(!config.maps.Any(m=>m.id==ForestMap.Id)){
    var ending=Regex.Match(text,@"\]\s*\}\s*$");if(!ending.Success)throw new System.InvalidOperationException("GameConfig maps array must be the final field");
    text=text.Substring(0,ending.Index).TrimEnd()+",\n"+JsonUtility.ToJson(ForestMap.Create(),true)+"\n]\n}\n";
   }
   if(text!=original){File.WriteAllText(configPath,text);AssetDatabase.ImportAsset(configPath);}AssetDatabase.SaveAssets();Debug.Log("Forest demo prepared: CC0 atlases, automatic-revive forest map, stronger rope baseline.");
  }
 }
}
