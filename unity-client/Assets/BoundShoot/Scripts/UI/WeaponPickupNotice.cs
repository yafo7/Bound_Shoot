namespace BoundShoot {
 // Unscaled time keeps the three-second notice independent of simulation speed.
 public sealed class WeaponPickupNotice {
  public const float Duration=3;
  public int Weapon {get;private set;}
  float until;uint revision;
  public void Clear(){Weapon=0;until=0;revision=0;}
  public void Observe(int weapon,float now,uint equipmentVersion=0){if(weapon==Weapon&&revision==equipmentVersion)return;Weapon=weapon;revision=equipmentVersion;until=weapon>0?now+Duration:0;}
  public bool Visible(float now)=>Weapon>0&&now<until;
 }
}
