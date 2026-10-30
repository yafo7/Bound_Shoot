#if UNITY_EDITOR
using UnityEngine;
namespace BoundShoot.Authoring {
 public enum LevelPieceKind {Floor,Obstacle,Spawn,Pickup,Revival}
 [AddComponentMenu("Bound Shoot/Level Piece")]
 public sealed class LevelPiece:MonoBehaviour {
  public LevelPieceKind kind;
  public int team,seat;public WeaponKind weapon;public ItemKind item;
  [Range(0,4)] public int visual;
  public float respawnSeconds=10;public bool randomizeOnRespawn;
  public Color color=new Color(.24f,.3f,.34f,1);
  public bool Rectangular=>kind==LevelPieceKind.Floor||kind==LevelPieceKind.Obstacle||kind==LevelPieceKind.Revival;
 }
}
#endif
