using NUnit.Framework;
using UnityEngine;
using UnityEditor;
namespace BoundShoot.Tests {
 public class CyberArtTests {
  [Test] public void ImportedArtHasTwentyNamedSpritesAndTransparentAtlases(){
   Assert.AreEqual(20,Resources.LoadAll<Sprite>("Art/Slices").Length);
   foreach(var name in new[]{"actor0","actor1","actor2","actor3","weapon1","weapon2","weapon3","weapon4","item1","item2","item3","item4","item5","item6","item7","item8","floor","cover","barrier","revival"}){
    var s=CyberArt.Get(name);Assert.IsNotNull(s,name);Assert.Greater(s.rect.width,10);Assert.Greater(s.rect.height,10);
   }
   foreach(var atlas in new[]{"actors","items","environment"}){
    var path="Assets/BoundShoot/Resources/Art/"+atlas+".png";var importer=(TextureImporter)AssetImporter.GetAtPath(path);
    Assert.AreEqual(FilterMode.Point,importer.filterMode);Assert.IsFalse(importer.mipmapEnabled);
    var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);Assert.Less(texture.GetPixel(0,0).a,.01f);
   }
  }
 }
}
