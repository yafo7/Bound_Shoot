using System.Collections.Generic;
using UnityEngine;
namespace BoundShoot {
 public static class VegetableArt {
  static readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
  static bool loaded;
  static readonly string[] names={"红根","番茄","西兰花","茄子","草莓","洋葱","胡萝卜","蓝帽菇"};
  public static string Name(int skin)=>names[Mathf.Clamp(skin,0,7)];
  public static Sprite Get(string key){if(!loaded){foreach(var s in Resources.LoadAll<Sprite>("Vegetables/Slices"))sprites[s.name]=s;loaded=true;}return sprites.TryGetValue(key,out var result)?result:null;}
  public static void Reload(){loaded=false;sprites.Clear();}
 }
}
