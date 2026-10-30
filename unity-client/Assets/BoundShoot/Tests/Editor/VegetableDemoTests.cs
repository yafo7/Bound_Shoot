using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using BoundShoot.Authoring;
using BoundShoot.Editor;
namespace BoundShoot.Tests {
 public class VegetableDemoTests {
  [Test] public void EightCharactersHaveEveryMotionAndPortrait(){
   for(int i=0;i<8;i++){Assert.IsNotNull(VegetableArt.Get("portrait"+i));for(int frame=0;frame<36;frame++)Assert.IsNotNull(VegetableArt.Get("veg"+i+"_f"+frame.ToString("00")));}
   Assert.AreEqual(344,Resources.LoadAll<Sprite>("Vegetables/Slices").Length);
  }
  [Test] public void FieldMapHasFourSeatsAndNoInvalidPlacements(){
   var map=FieldMap.Create();Assert.AreEqual(4,map.blueSpawns.Length);Assert.AreEqual(4,map.redSpawns.Length);Assert.AreEqual(80,map.halfWidth*2);Assert.AreEqual(64,map.halfHeight*2);
   Assert.IsEmpty(LevelMapValidation.Validate(map,GameConfig.Current));Assert.AreEqual("meadow",map.theme);
  }
  [Test] public void EditorRoundTripPreservesFieldThemeAndObstacleTypes(){
   var go=new GameObject("Field authoring");try{var root=go.AddComponent<LevelLayout>();var map=FieldMap.Create();LevelLayoutIO.Populate(root,map);var saved=LevelLayoutIO.Collect(root);Assert.AreEqual("meadow",saved.theme);CollectionAssert.AreEqual(map.blocks.Select(b=>b.visual),saved.blocks.Select(b=>b.visual));}finally{Object.DestroyImmediate(go);}
  }
  [Test] public void MotionSheetsHaveRealAlphaAndPointSampling(){
   for(int i=0;i<8;i++){var path="Assets/BoundShoot/Resources/Vegetables/veg"+i+".png";var imp=(TextureImporter)AssetImporter.GetAtPath(path);Assert.AreEqual(FilterMode.Point,imp.filterMode);Assert.IsFalse(imp.mipmapEnabled);Assert.AreEqual(0,AssetDatabase.LoadAssetAtPath<Texture2D>(path).GetPixel(0,0).a,.01f);}
  }
 }
}
