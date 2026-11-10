using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using BoundShoot.Editor;
namespace BoundShoot.Tests {
 public class BalanceEditorTests {
  string folder,file;
  [SetUp] public void Setup(){folder=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"bound-balance-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);file=System.IO.Path.Combine(folder,"config.json");File.Copy(BalanceConfigIO.Path,file);}
  [TearDown] public void Cleanup(){Directory.Delete(folder,true);}
  GameConfig Read()=>JsonUtility.FromJson<GameConfig>(File.ReadAllText(file));
  [Test] public void SavePreservesConcurrentMapRopeAndItemPoolChanges(){
   var values=BalanceConfigIO.Read(file);string original=BalanceConfigIO.Fingerprint(values);
   var changed=Read();changed.maps[0].name="同伴新关卡名";changed.ropeLength=7;changed.items[0].weight=9;File.WriteAllText(file,JsonUtility.ToJson(changed));
   values.friendlyFire=true;values.baseHealth=100;values.weapons[0].damage=12;values.weapons[0].ammoCapacity=22;values.weapons[3].knockbackImpulse=3;values.healAmount=10;
   values.shortenScale=.55f;values.shortenSeconds=6;
   values.extendScale=1.8f;values.extendSeconds=8;
   values.elasticScale=4;values.elasticSeconds=12;
   values.downedDrag=1.2f;values.speedBoostMultiplier=1.6f;values.speedBoostSeconds=5;values.speedBoostPullMultiplier=1.5f;
   values.overrideTeamWipeSeconds=true;values.reviveSeconds=8.5f;
   BalanceConfigIO.Save(file,values,original);var saved=Read();Assert.IsTrue(saved.friendlyFire);Assert.AreEqual(100,saved.baseHealth);Assert.AreEqual(12,saved.weapons[0].damage);Assert.AreEqual(22,saved.weapons[0].ammoCapacity);Assert.AreEqual(3,saved.weapons[3].knockbackImpulse);
   Assert.AreEqual(10,saved.healAmount);Assert.AreEqual("同伴新关卡名",saved.maps[0].name);Assert.AreEqual(7,saved.ropeLength);Assert.AreEqual(9,saved.items[0].weight);
   Assert.AreEqual(.55f,saved.shortenScale);Assert.AreEqual(6,saved.Item(ItemKind.Shorten).duration);
   Assert.AreEqual(1.8f,saved.extendScale);Assert.AreEqual(8,saved.Item(ItemKind.Extend).duration);
   Assert.AreEqual(4,saved.elasticScale);Assert.AreEqual(12,saved.Item(ItemKind.Elastic).duration);
   Assert.AreEqual(1.2f,saved.downedDrag);Assert.AreEqual(1.6f,saved.speedBoostMultiplier);
   Assert.AreEqual(5,saved.speedBoostSeconds);Assert.AreEqual(1.5f,saved.speedBoostPullMultiplier);
   Assert.IsTrue(saved.overrideTeamWipeSeconds);Assert.AreEqual(8.5f,saved.reviveSeconds);
   Assert.AreEqual(changed.maps[0].objectives.wipeSeconds,saved.maps[0].objectives.wipeSeconds);
  }
  [Test] public void ConflictingBalanceEditsNeverOverwriteTheFile(){
   var values=BalanceConfigIO.Read(file);string original=BalanceConfigIO.Fingerprint(values);
   var latest=Read();latest.weapons[0].cooldown=2;File.WriteAllText(file,JsonUtility.ToJson(latest));string before=File.ReadAllText(file);
   values.baseHealth=200;Assert.Throws<InvalidOperationException>(()=>BalanceConfigIO.Save(file,values,original));Assert.AreEqual(before,File.ReadAllText(file));
  }
  [Test] public void InvalidNumbersAndWeaponDuplicatesCannotBeSaved(){
   var values=BalanceConfigIO.Read(file);string original=BalanceConfigIO.Fingerprint(values),before=File.ReadAllText(file);
   values.baseHealth=0;values.weapons[0].ammoCapacity=0;values.weapons[0].cooldown=float.NaN;values.weapons[1].knockbackImpulse=-1;values.healAmount=-1;
   Assert.GreaterOrEqual(BalanceConfigIO.Validate(values).Count,5);Assert.Throws<InvalidOperationException>(()=>BalanceConfigIO.Save(file,values,original));Assert.AreEqual(before,File.ReadAllText(file));
   values=BalanceConfigIO.Read(file);values.weapons[1].kind=WeaponKind.Sniper;Assert.IsNotEmpty(BalanceConfigIO.Validate(values));
  }
 }
}
