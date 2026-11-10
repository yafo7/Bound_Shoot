using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace BoundShoot {
 public enum SupplyTier {None,Common,Medium,High}
 [Serializable] public class SupplyEntry {public WeaponKind weapon;public ItemKind item;public float weight;}
 [Serializable] public class SupplyPoolDefinition {public SupplyTier tier;public float respawnSeconds;public SupplyEntry[] entries;}
 public static class SupplyPools {
  public static string Label(SupplyTier tier)=>new[]{"固定物资","普通（白）","中等（蓝）","高级（红）"}[Mathf.Clamp((int)tier,0,3)];
  public static Color ColorOf(SupplyTier tier)=>tier==SupplyTier.Medium?new Color(.2f,.55f,1):tier==SupplyTier.High?new Color(1,.2f,.22f):Color.white;
  public static SupplyPoolDefinition Find(GameConfig config,SupplyTier tier)=>config.supplyPools?.FirstOrDefault(p=>p!=null&&p.tier==tier);
  public static bool Available(SupplyEntry e,GameConfig c)=>e!=null&&e.weight>0&&!float.IsNaN(e.weight)&&!float.IsInfinity(e.weight)&&
   ((e.weapon!=WeaponKind.None&&e.weapon!=WeaponKind.Dagger&&e.item==ItemKind.None&&c.Weapon(e.weapon)!=null)||(e.weapon==WeaponKind.None&&e.item!=ItemKind.None&&e.item!=ItemKind.Mystery&&c.Item(e.item)?.enabled==true));
  public static SupplyEntry Select(SupplyPoolDefinition pool,GameConfig config,float roll){
   var entries=(pool?.entries??Array.Empty<SupplyEntry>()).Where(e=>Available(e,config)).ToArray();
   if(entries.Length==0)throw new InvalidOperationException("物资概率池为空："+pool?.tier);
   double total=entries.Sum(e=>(double)e.weight),value=Mathf.Clamp01(roll)*total;
   foreach(var entry in entries){value-=entry.weight;if(value<0)return entry;}return entries[entries.Length-1];
  }
  public static void Roll(PickupState state,GameConfig config){var entry=Select(Find(config,(SupplyTier)state.supplyTier),config,UnityEngine.Random.value);state.weapon=(int)entry.weapon;state.item=(int)entry.item;}
  public static List<string> Validate(SupplyPoolDefinition[] pools,GameConfig config){
   var errors=new List<string>();
   if(pools==null||pools.Length!=3||pools.Any(p=>p==null)||!pools.Select(p=>p.tier).OrderBy(t=>t).SequenceEqual(new[]{SupplyTier.Common,SupplyTier.Medium,SupplyTier.High})){
    errors.Add("需要普通、中等、高级各一个物资概率池。");return errors;
   }
   foreach(var p in pools){string name=Label(p.tier);
    if(float.IsNaN(p.respawnSeconds)||float.IsInfinity(p.respawnSeconds)||p.respawnSeconds<0||p.respawnSeconds>3600)errors.Add(name+"刷新间隔须为0–3600秒。");
    if(p.entries==null||p.entries.Length!=config.weapons.Length+config.items.Count(i=>i.kind!=ItemKind.None&&i.kind!=ItemKind.Mystery)||p.entries.Any(e=>e==null)){errors.Add(name+"需要每种武器与实际道具各一个概率条目。");continue;}
    var keys=new HashSet<string>();
    foreach(var e in p.entries){
     bool valid=(e.weapon!=WeaponKind.None&&e.item==ItemKind.None&&config.Weapon(e.weapon)!=null)||(e.weapon==WeaponKind.None&&e.item!=ItemKind.None&&e.item!=ItemKind.Mystery&&config.Item(e.item)!=null);
     if(!valid||!keys.Add((int)e.weapon+":"+(int)e.item))errors.Add(name+"含重复或无效物资。");
     if(float.IsNaN(e.weight)||float.IsInfinity(e.weight)||e.weight<0||e.weight>100000)errors.Add(name+"权重须为0–100000的有限数值。");
     if(e.weight>0&&e.item!=ItemKind.None&&config.Item(e.item)?.enabled!=true)errors.Add(name+"不能给已禁用的道具设置正权重。");
    }
    if(!p.entries.Any(e=>Available(e,config)))errors.Add(name+"至少需要一种可生成的物资。");
   }return errors;
  }
 }
}
