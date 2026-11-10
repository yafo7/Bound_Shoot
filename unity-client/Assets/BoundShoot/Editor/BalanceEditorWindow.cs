using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BoundShoot.Editor {
 [Serializable] public sealed class BalanceSettings {
  public float throwDistance;
  public float throwFlightSeconds;
  public float smokeRadius;
  public float smokeSeconds;
  public float fireRadius;
  public float fireSeconds;
  public int fireDamagePerSecond;
  public int fireFriendlyDamagePerSecond;
  public float rocketFlightSeconds,rocketBlastRadius;
  public bool friendlyFire,overrideTeamWipeSeconds;
  public int baseHealth,roundSeconds,healAmount,shieldAmount,shieldCap,bombDamage;
  public float reviveSeconds,protectionSeconds,bombFuse,bombRadius;
  public float shortenScale,extendScale,elasticScale;
  public float shortenSeconds,extendSeconds,elasticSeconds;
  public float downedDrag,speedBoostMultiplier,speedBoostSeconds,speedBoostPullMultiplier;
  public WeaponDefinition[] weapons;
 }
 public static class BalanceConfigIO {
  public const string Path="Assets/BoundShoot/Resources/GameConfig.json";
  public static BalanceSettings Capture(GameConfig config){
   return new BalanceSettings{rocketFlightSeconds=config.rocketFlightSeconds,rocketBlastRadius=config.rocketBlastRadius,throwDistance=config.throwDistance,throwFlightSeconds=config.throwFlightSeconds,smokeRadius=config.smokeRadius,smokeSeconds=config.smokeSeconds,fireRadius=config.fireRadius,fireSeconds=config.fireSeconds,fireDamagePerSecond=config.fireDamagePerSecond,fireFriendlyDamagePerSecond=config.fireFriendlyDamagePerSecond,friendlyFire=config.friendlyFire,baseHealth=config.baseHealth,roundSeconds=config.roundSeconds,reviveSeconds=config.reviveSeconds,
    overrideTeamWipeSeconds=config.overrideTeamWipeSeconds,
    protectionSeconds=config.protectionSeconds,healAmount=config.healAmount,shieldAmount=config.shieldAmount,shieldCap=config.shieldCap,
    bombDamage=config.bombDamage,bombFuse=config.bombFuse,bombRadius=config.bombRadius,
    shortenScale=config.shortenScale,extendScale=config.extendScale,elasticScale=config.elasticScale,
    shortenSeconds=config.Item(ItemKind.Shorten).duration,extendSeconds=config.Item(ItemKind.Extend).duration,
    elasticSeconds=config.Item(ItemKind.Elastic).duration,downedDrag=config.downedDrag,
    speedBoostMultiplier=config.speedBoostMultiplier,speedBoostSeconds=config.speedBoostSeconds,speedBoostPullMultiplier=config.speedBoostPullMultiplier,
    weapons=config.weapons.Select(w=>JsonUtility.FromJson<WeaponDefinition>(JsonUtility.ToJson(w))).ToArray()};
  }
  public static string Fingerprint(BalanceSettings values)=>JsonUtility.ToJson(values);
  public static BalanceSettings Read(string path=Path)=>Capture(JsonUtility.FromJson<GameConfig>(File.ReadAllText(path)));
  public static List<string> Validate(BalanceSettings v){
   var errors=new List<string>();
   void Integer(int n,int min,int max,string label){if(n<min||n>max)errors.Add(label+"须在"+min+"–"+max+"之间。");}
   void Number(float n,float min,float max,string label){if(float.IsNaN(n)||float.IsInfinity(n)||n<min||n>max)errors.Add(label+"须为"+min+"–"+max+"之间的有限数值。");}
   Number(v.throwDistance,0.1f,100f,"投掷距离");
   Number(v.throwFlightSeconds,0.1f,10f,"投掷飞行秒数");
   Number(v.smokeRadius,0.1f,30f,"烟雾半径");
   Number(v.smokeSeconds,0.1f,60f,"烟雾持续秒数");
   Number(v.fireRadius,0.1f,30f,"燃烧半径");
   Number(v.fireSeconds,0.1f,60f,"燃烧持续秒数");
   Integer(v.fireDamagePerSecond,0,9999,"燃烧敌方每秒伤害");
   Integer(v.fireFriendlyDamagePerSecond,0,9999,"燃烧友方及自身每秒伤害");
   Number(v.rocketFlightSeconds,.1f,10,"火箭飞行时间");Number(v.rocketBlastRadius,.1f,30,"火箭爆炸半径");
   Integer(v.baseHealth,1,9999,"出生生命");Integer(v.roundSeconds,1,3600,"回合秒数");
   Number(v.reviveSeconds,.1f,300,"团灭复活等待");Number(v.protectionSeconds,0,60,"复活保护");
   Integer(v.healAmount,0,9999,"血包恢复");Integer(v.shieldAmount,0,9999,"每次获得护盾");Integer(v.shieldCap,0,9999,"护盾上限");
   Integer(v.bombDamage,0,9999,"炸弹伤害");Number(v.bombFuse,.1f,60,"炸弹引爆秒数");Number(v.bombRadius,.1f,100,"炸弹半径");
   Number(v.shortenScale,.01f,1,"缩短绳长度倍率");
   Number(v.extendScale,1,10,"延长绳长度倍率");
   Number(v.elasticScale,.01f,100,"弹簧弹力与最大拉力倍率");
   Number(v.shortenSeconds,0,300,"缩短绳持续时间");
   Number(v.extendSeconds,0,300,"延长绳持续时间");
   Number(v.elasticSeconds,0,300,"弹簧持续时间");
   Number(v.downedDrag,0,20,"倒地移动阻力");
   Number(v.speedBoostMultiplier,1,5,"提速移动速度倍率");
   Number(v.speedBoostSeconds,.1f,60,"提速持续时间");
   Number(v.speedBoostPullMultiplier,1,5,"提速绳索拉力倍率");
   if(v.weapons==null||v.weapons.Length!=6||v.weapons.Any(w=>w==null)||!v.weapons.Select(w=>w.kind).OrderBy(k=>k).SequenceEqual(new[]{WeaponKind.Sniper,WeaponKind.Pistol,WeaponKind.AK,WeaponKind.Dagger,WeaponKind.RocketLauncher,WeaponKind.Greatsword})){
    errors.Add("需要且只能有狙击枪、手枪、AK、匕首、火箭炮和大宝剑各一条配置。");return errors;
   }
   foreach(var w in v.weapons){string label=w.name+"：";
    Integer(w.damage,1,9999,label+"伤害");if(!BattleRules.IsMelee(w.kind))Integer(w.ammoCapacity,1,9999,label+"初始弹药");
    Number(w.cooldown,.01f,60,label+"攻击间隔");Number(w.range,.1f,1000,label+"射程");
    Number(w.bulletSpeed,BattleRules.IsMelee(w.kind)?0:.1f,1000,label+"弹速");Number(w.knockbackImpulse,0,100,label+"击退冲量");
   }return errors;
  }
  public static void Save(string path,BalanceSettings values,string original){
   if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("请先停止Play，再保存数值。");
   var errors=Validate(values);if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
   var latest=JsonUtility.FromJson<GameConfig>(File.ReadAllText(path));
   if(Fingerprint(Capture(latest))!=original)throw new InvalidOperationException("数值已被其他修改更新，请重新载入后再编辑，避免覆盖。");
   // Only managed balance fields are replaced; latest map, rope and item pool edits survive.
   latest.throwDistance=values.throwDistance;
   latest.throwFlightSeconds=values.throwFlightSeconds;
   latest.smokeRadius=values.smokeRadius;
   latest.smokeSeconds=values.smokeSeconds;
   latest.fireRadius=values.fireRadius;
   latest.fireSeconds=values.fireSeconds;
   latest.fireDamagePerSecond=values.fireDamagePerSecond;
   latest.fireFriendlyDamagePerSecond=values.fireFriendlyDamagePerSecond;
   latest.rocketFlightSeconds=values.rocketFlightSeconds;latest.rocketBlastRadius=values.rocketBlastRadius;
   latest.friendlyFire=values.friendlyFire;latest.baseHealth=values.baseHealth;latest.roundSeconds=values.roundSeconds;latest.reviveSeconds=values.reviveSeconds;latest.protectionSeconds=values.protectionSeconds;
   latest.overrideTeamWipeSeconds=values.overrideTeamWipeSeconds;
   latest.healAmount=values.healAmount;latest.shieldAmount=values.shieldAmount;latest.shieldCap=values.shieldCap;
   latest.bombDamage=values.bombDamage;latest.bombFuse=values.bombFuse;latest.bombRadius=values.bombRadius;latest.weapons=values.weapons;
   latest.shortenScale=values.shortenScale;
   latest.extendScale=values.extendScale;
   latest.elasticScale=values.elasticScale;
   latest.Item(ItemKind.Shorten).duration=values.shortenSeconds;
   latest.Item(ItemKind.Extend).duration=values.extendSeconds;
   latest.Item(ItemKind.Elastic).duration=values.elasticSeconds;
   latest.downedDrag=values.downedDrag;
   latest.speedBoostMultiplier=values.speedBoostMultiplier;
   latest.speedBoostSeconds=values.speedBoostSeconds;
   latest.speedBoostPullMultiplier=values.speedBoostPullMultiplier;
   string full=System.IO.Path.GetFullPath(path),temp=full+".balance-tmp";
   try{File.WriteAllText(temp,JsonUtility.ToJson(latest,true)+"\n",new UTF8Encoding(false));File.Replace(temp,full,null);}
   finally{if(File.Exists(temp))File.Delete(temp);}
  }
 }
 public sealed class BalanceEditorWindow:EditorWindow {
  [SerializeField] BalanceSettings draft;[SerializeField] string original;Vector2 scroll;string message;bool failed;
  [MenuItem("Bound Shoot/数值编辑器")]
  public static void Open(){var w=GetWindow<BalanceEditorWindow>("数值编辑器");w.minSize=new Vector2(470,480);w.Show();}
  void OnEnable(){if(draft==null)Reload();saveChangesMessage="数值编辑器有未保存修改。保存后需重新Play生效。";}
  void Reload(){try{draft=BalanceConfigIO.Read();original=BalanceConfigIO.Fingerprint(draft);message="已载入当前配置";failed=false;hasUnsavedChanges=false;}catch(Exception e){message=e.Message;failed=true;}}
  public override void SaveChanges(){
   try{BalanceConfigIO.Save(BalanceConfigIO.Path,draft,original);original=BalanceConfigIO.Fingerprint(draft);AssetDatabase.Refresh();message="已保存，下次Play生效";failed=false;hasUnsavedChanges=false;base.SaveChanges();}
   catch(Exception e){message=e.Message;failed=true;}
  }
  public override void DiscardChanges(){Reload();base.DiscardChanges();}
  void OnGUI(){
   float previousLabelWidth=EditorGUIUtility.labelWidth;EditorGUIUtility.labelWidth=220;
   try{DrawFields();}finally{EditorGUIUtility.labelWidth=previousLabelWidth;}
  }
  void DrawFields(){
   EditorGUILayout.LabelField("战斗数值",EditorStyles.boldLabel);
   EditorGUILayout.HelpBox("真人、AI与训练共用。停止Play后编辑并保存，重新Play生效。生命上限固定，换枪不改变血量；击退是物理冲量，不是固定距离。",MessageType.Info);
   if(draft==null){EditorGUILayout.HelpBox(message??"无法读取配置",MessageType.Error);if(GUILayout.Button("重新载入"))Reload();return;}
   using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode)){
    scroll=EditorGUILayout.BeginScrollView(scroll);
    EditorGUILayout.LabelField("基础规则",EditorStyles.boldLabel);
    draft.friendlyFire=EditorGUILayout.Toggle("开启友伤",draft.friendlyFire);
    draft.baseHealth=EditorGUILayout.IntField("角色生命上限（HP）",draft.baseHealth);
    EditorGUILayout.LabelField("救助距离与重生点","在关卡编辑器设置");
    draft.overrideTeamWipeSeconds=EditorGUILayout.Toggle("统一团灭复活等待（覆盖地图设置）",draft.overrideTeamWipeSeconds);
    using(new EditorGUI.DisabledScope(!draft.overrideTeamWipeSeconds))
     draft.reviveSeconds=EditorGUILayout.FloatField("团灭复活等待（秒）",draft.reviveSeconds);
    draft.protectionSeconds=EditorGUILayout.FloatField("复活保护（秒）",draft.protectionSeconds);
    draft.downedDrag=EditorGUILayout.FloatField("倒地移动阻力",draft.downedDrag);
    foreach(var w in draft.weapons){EditorGUILayout.Space();EditorGUILayout.LabelField(w.name,EditorStyles.boldLabel);
     w.damage=EditorGUILayout.IntField("单次伤害（HP）",w.damage);
     if(!BattleRules.IsMelee(w.kind))w.ammoCapacity=EditorGUILayout.IntField("初始弹药（发）",w.ammoCapacity);
     else EditorGUILayout.LabelField("弹药","无限（近战）");
     w.cooldown=EditorGUILayout.FloatField("攻击间隔（秒）",w.cooldown);w.range=EditorGUILayout.FloatField("射程（世界单位）",w.range);
     if(w.kind==WeaponKind.RocketLauncher){
      draft.rocketFlightSeconds=EditorGUILayout.FloatField("飞行时间（秒）",draft.rocketFlightSeconds);
      draft.rocketBlastRadius=EditorGUILayout.FloatField("爆炸半径",draft.rocketBlastRadius);
      continue;
     }
     using(new EditorGUI.DisabledScope(BattleRules.IsMelee(w.kind)))w.bulletSpeed=EditorGUILayout.FloatField("弹速（单位／秒）",w.bulletSpeed);
     w.knockbackImpulse=EditorGUILayout.FloatField("击退冲量（0为关闭）",w.knockbackImpulse);
    }
    EditorGUILayout.Space();EditorGUILayout.LabelField("绳索道具与提速",EditorStyles.boldLabel);
    draft.shortenScale=EditorGUILayout.FloatField("缩短绳 · 对方绳长倍率",draft.shortenScale);
    draft.shortenSeconds=EditorGUILayout.FloatField("缩短绳 · 持续时间（秒）",draft.shortenSeconds);
    draft.extendScale=EditorGUILayout.FloatField("延长绳 · 己方绳长倍率",draft.extendScale);
    draft.extendSeconds=EditorGUILayout.FloatField("延长绳 · 持续时间（秒）",draft.extendSeconds);
    draft.elasticScale=EditorGUILayout.FloatField("弹簧 · 弹力与最大拉力倍率",draft.elasticScale);
    draft.elasticSeconds=EditorGUILayout.FloatField("弹簧 · 持续时间（秒）",draft.elasticSeconds);
    draft.speedBoostMultiplier=EditorGUILayout.FloatField("提速 · 移动速度倍率",draft.speedBoostMultiplier);
    draft.speedBoostSeconds=EditorGUILayout.FloatField("提速 · 持续时间（秒）",draft.speedBoostSeconds);
    draft.speedBoostPullMultiplier=EditorGUILayout.FloatField("提速 · 绳索拉力倍率",draft.speedBoostPullMultiplier);
    EditorGUILayout.Space();EditorGUILayout.LabelField("血包、护盾与炸弹",EditorStyles.boldLabel);
    draft.healAmount=EditorGUILayout.IntField("血包每次恢复（HP）",draft.healAmount);
    draft.shieldAmount=EditorGUILayout.IntField("每次获得护盾",draft.shieldAmount);draft.shieldCap=EditorGUILayout.IntField("护盾上限",draft.shieldCap);
    draft.bombDamage=EditorGUILayout.IntField("炸弹伤害（HP）",draft.bombDamage);draft.bombFuse=EditorGUILayout.FloatField("炸弹引爆等待（秒）",draft.bombFuse);draft.bombRadius=EditorGUILayout.FloatField("炸弹半径（世界单位）",draft.bombRadius);
    EditorGUILayout.Space();EditorGUILayout.LabelField("烟雾弹与燃烧弹",EditorStyles.boldLabel);
    draft.throwDistance=EditorGUILayout.FloatField("投掷距离",draft.throwDistance);
    draft.throwFlightSeconds=EditorGUILayout.FloatField("投掷飞行秒数",draft.throwFlightSeconds);
    draft.smokeRadius=EditorGUILayout.FloatField("烟雾半径",draft.smokeRadius);
    draft.smokeSeconds=EditorGUILayout.FloatField("烟雾持续秒数",draft.smokeSeconds);
    draft.fireRadius=EditorGUILayout.FloatField("燃烧半径",draft.fireRadius);
    draft.fireSeconds=EditorGUILayout.FloatField("燃烧持续秒数",draft.fireSeconds);
    draft.fireDamagePerSecond=EditorGUILayout.IntField("燃烧敌方每秒伤害",draft.fireDamagePerSecond);
    draft.fireFriendlyDamagePerSecond=EditorGUILayout.IntField("燃烧友方及自身每秒伤害",draft.fireFriendlyDamagePerSecond);
    EditorGUILayout.EndScrollView();hasUnsavedChanges=BalanceConfigIO.Fingerprint(draft)!=original;
    var errors=BalanceConfigIO.Validate(draft);if(errors.Count>0)EditorGUILayout.HelpBox(string.Join("\n",errors),MessageType.Error);
    EditorGUILayout.BeginHorizontal();using(new EditorGUI.DisabledScope(errors.Count>0||!hasUnsavedChanges)){if(GUILayout.Button("保存数值",GUILayout.Height(32)))SaveChanges();}
    if(GUILayout.Button("重新载入",GUILayout.Height(32))&&(!hasUnsavedChanges||EditorUtility.DisplayDialog("重新载入","放弃尚未保存的数值修改？","重新载入","取消")))Reload();EditorGUILayout.EndHorizontal();
   }
   if(!string.IsNullOrEmpty(message))EditorGUILayout.HelpBox(message,failed?MessageType.Error:MessageType.None);
  }
 }
}
