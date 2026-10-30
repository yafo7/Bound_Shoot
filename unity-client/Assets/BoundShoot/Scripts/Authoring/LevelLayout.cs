#if UNITY_EDITOR
using UnityEngine;
namespace BoundShoot.Authoring {
 [AddComponentMenu("Bound Shoot/Level Layout")]
 public sealed class LevelLayout:MonoBehaviour {
  public string mapId="Arena_01",mapName="竞技场";public bool available=true;
  public string theme="";
  public Vector2 dimensions=new Vector2(32,24);
  public Color groundColor=MapContent.DefaultGround;
  public bool snap=true;public float grid=.5f;
  [HideInInspector] public string sourceMapId,sourceMapJson;
  [HideInInspector] public bool newMap;
 }
}
#endif
