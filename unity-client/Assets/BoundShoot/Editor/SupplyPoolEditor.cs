using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
namespace BoundShoot.Editor {
 public static class SupplyPoolConfigIO {
  public static string Fingerprint(SupplyPoolDefinition[] pools)=>JsonUtility.ToJson(new Data{pools=pools});
  [Serializable] class Data {public SupplyPoolDefinition[] pools;}
  public static void Save(string path,SupplyPoolDefinition[] pools,string original){
   if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("请先停止Play。");
   var latest=LevelLayoutIO.Read(path);var errors=SupplyPools.Validate(pools,latest);
   if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
   if(Fingerprint(latest.supplyPools)!=original)throw new InvalidOperationException("物资概率池已被其他修改更新，请重新载入后编辑。");
   latest.supplyPools=pools;string full=Path.GetFullPath(path),temp=full+".supply-tmp";
   try{File.WriteAllText(temp,JsonUtility.ToJson(latest,true)+"\n",new UTF8Encoding(false));File.Replace(temp,full,null);}
   finally{if(File.Exists(temp))File.Delete(temp);}
  }
 }
 [Serializable] public sealed class SupplyPoolEditor {
  [SerializeField] SupplyPoolDefinition[] pools;[SerializeField] string original;
  [SerializeField] bool expanded;[SerializeField] bool[] show=new[]{true,false,false};
  string message;bool failed;
  public bool Dirty=>pools!=null&&SupplyPoolConfigIO.Fingerprint(pools)!=original;
  public void Reload(){try{pools=LevelLayoutIO.Read().supplyPools;original=SupplyPoolConfigIO.Fingerprint(pools);message="已载入物资概率池";failed=false;}catch(Exception e){message=e.Message;failed=true;}}
  public bool Save(){try{SupplyPoolConfigIO.Save(LevelLayoutIO.ConfigPath,pools,original);original=SupplyPoolConfigIO.Fingerprint(pools);AssetDatabase.Refresh();message="已保存物资概率池，下次Play生效";failed=false;return true;}catch(Exception e){message=e.Message;failed=true;return false;}}
  public void Draw(){
   expanded=EditorGUILayout.Foldout(expanded,"三级随机物资：概率与默认刷新",true);if(!expanded)return;
   if(pools==null)Reload();
   EditorGUILayout.HelpBox("同类型物资点共用概率。权重自动换算百分比；0表示不生成。拾取后计时刷新，未拾取不替换。概率池与地图布局分别保存。",MessageType.Info);
   if(pools!=null){
    float oldWidth=EditorGUIUtility.labelWidth;EditorGUIUtility.labelWidth=115;
    try{for(int i=0;i<pools.Length&&i<3;i++){
     var pool=pools[i];if(pool==null)continue;
     show[i]=EditorGUILayout.Foldout(show[i],SupplyPools.Label(pool.tier),true);if(!show[i])continue;
     pool.respawnSeconds=EditorGUILayout.FloatField("默认刷新秒数",pool.respawnSeconds);
     double total=pool.entries?.Where(e=>e!=null&&e.weight>0&&!float.IsNaN(e.weight)&&!float.IsInfinity(e.weight)).Sum(e=>(double)e.weight)??0;
     foreach(var e in pool.entries??Array.Empty<SupplyEntry>()){if(e==null)continue;
      EditorGUILayout.BeginHorizontal();string label=e.weapon!=WeaponKind.None?LevelLayoutIO.WeaponName(e.weapon):LevelLayoutIO.ItemName(e.item);
      e.weight=EditorGUILayout.FloatField(label+" 权重",e.weight);GUILayout.Label(total>0?(Math.Max(0,e.weight)/total*100).ToString("0.##")+"%":"—",GUILayout.Width(55));EditorGUILayout.EndHorizontal();
     }
    }}finally{EditorGUIUtility.labelWidth=oldWidth;}
    var errors=SupplyPools.Validate(pools,LevelLayoutIO.Read());if(errors.Count>0)EditorGUILayout.HelpBox(string.Join("\n",errors),MessageType.Error);
    EditorGUILayout.BeginHorizontal();using(new EditorGUI.DisabledScope(errors.Count>0||!Dirty)){if(GUILayout.Button("保存概率池"))Save();}
    if(GUILayout.Button("重新载入概率池")&&(!Dirty||EditorUtility.DisplayDialog("重新载入","放弃未保存的概率修改？","载入","取消")))Reload();EditorGUILayout.EndHorizontal();
   }
   if(!string.IsNullOrEmpty(message))EditorGUILayout.HelpBox(message,failed?MessageType.Error:MessageType.None);
  }
 }
}
