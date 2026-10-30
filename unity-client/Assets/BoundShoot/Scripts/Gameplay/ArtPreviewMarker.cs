using UnityEngine;
namespace BoundShoot {
 [ExecuteAlways] public sealed class ArtPreviewMarker:MonoBehaviour {
  MaterialPropertyBlock tint;
  void LateUpdate(){
   if(tint==null)tint=new MaterialPropertyBlock();
   foreach(var player in FindObjectsOfType<CyberPlayerView>())foreach(var renderer in player.GetComponentsInChildren<SpriteRenderer>()){
    if(renderer.sharedMaterial==null||renderer.sharedMaterial.shader.name!="BoundShoot/Team Tint")continue;
    renderer.GetPropertyBlock(tint);tint.SetColor("_Team",player.transform.position.x<0?Draw2D.Blue:Draw2D.Red);renderer.SetPropertyBlock(tint);
   }
  }
 }
}
