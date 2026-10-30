using System;
using System.IO;
using System.Linq;
using BoundShoot.Authoring;
using BoundShoot.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace BoundShoot.Tests {
 public class LevelEditorTests {
  string folder,file;LevelLayout root;
  [SetUp] public void Setup(){folder=Path.Combine(Path.GetTempPath(),"bound-level-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);file=Path.Combine(folder,"GameConfig.json");File.Copy(LevelLayoutIO.ConfigPath,file);}
  [TearDown] public void Cleanup(){if(root!=null)Object.DestroyImmediate(root.gameObject);if(Directory.Exists(folder))Directory.Delete(folder,true);}
  GameConfig Config()=>LevelLayoutIO.Read(file);
  void Load(){root=new GameObject("Level authoring test").AddComponent<LevelLayout>();root.gameObject.tag="EditorOnly";LevelLayoutIO.Populate(root,Config().maps.First(m=>m.id=="Arena_01"));}
  [Test] public void LegacyMapsKeepTheirPickupsAndZonesThroughConfigSerialization(){
   var original=Config().maps[0];var copy=JsonUtility.FromJson<MapDefinition>(JsonUtility.ToJson(original));
   var pickups=MapContent.Pickups(copy,17);
   Assert.AreEqual(original.supplies.Length,pickups.Length);Assert.AreEqual(WeaponKind.Sniper,pickups[0].weapon);
   Assert.AreEqual(ItemKind.Mystery,pickups[4].item);Assert.IsTrue(pickups.All(p=>p.randomizeOnRespawn&&p.respawnSeconds==17));
   Assert.AreEqual(original.revivals.Length,MapContent.Revivals(copy).Length);Assert.AreEqual(new Vector2(4,3),MapContent.Revivals(copy)[0].size);
  }
  [Test] public void EditedSceneRoundTripsTypedDropsGroundObstaclesAndZoneSizes(){
   Load();var health=LevelLayoutIO.Add(root,LevelPieceKind.Pickup,new Vector2(1,1),Vector2.one);health.item=ItemKind.Heal;health.respawnSeconds=3.5f;
   var floor=LevelLayoutIO.Add(root,LevelPieceKind.Floor,new Vector2(2,0),new Vector2(3,2));floor.color=Color.yellow;
   var wall=root.GetComponentsInChildren<LevelPiece>().First(p=>p.kind==LevelPieceKind.Obstacle);wall.transform.localPosition=new Vector2(-4,-3);wall.transform.localScale=new Vector3(1,2,1);
   var zone=root.GetComponentsInChildren<LevelPiece>().First(p=>p.kind==LevelPieceKind.Revival);zone.transform.localScale=new Vector3(3,2,1);
   Assert.IsEmpty(LevelLayoutIO.Validate(root));
   LevelLayoutIO.Save(root,file);var saved=Config().maps[0];
   Assert.AreEqual(1,saved.layoutVersion);Assert.AreEqual(14,saved.pickups.Length);
   var drop=saved.pickups.Single(p=>p.item==ItemKind.Heal);Assert.AreEqual(new Vector2(1,1),drop.position);Assert.AreEqual(3.5f,drop.respawnSeconds);Assert.IsFalse(drop.randomizeOnRespawn);
   Assert.AreEqual(Color.yellow,saved.floors.Single().color);Assert.AreEqual(new Vector2(3,2),saved.revivalZones[0].size);
   Assert.IsTrue(saved.blocks.Any(b=>b.center==new Vector2(-4,-3)&&b.size==new Vector2(1,2)));
   Assert.AreEqual(saved.pickups.Length,MapContent.Pickups(saved).Length);
   Assert.AreEqual(saved.supplies.Length,saved.pickups.Length);Assert.AreEqual(saved.revivals[0],saved.revivalZones[0].center);
   // Saving twice must not report a false concurrent-edit conflict.
   Assert.DoesNotThrow(()=>LevelLayoutIO.Save(root,file));
  }
  [Test] public void SavingUsesLatestWeaponTuningAndKeepsOtherMaps(){
   Load();var latest=Config();latest.weapons[1].damage=123;
   var other=JsonUtility.FromJson<MapDefinition>(JsonUtility.ToJson(latest.maps[0]));other.id="Arena_02";other.name="同伴新地图";other.available=false;
   latest.maps=latest.maps.Concat(new[]{other}).ToArray();File.WriteAllText(file,JsonUtility.ToJson(latest));
   root.mapName="编辑器修改的名称";LevelLayoutIO.Save(root,file);
   var saved=Config();Assert.AreEqual(123,saved.weapons[1].damage);Assert.AreEqual("同伴新地图",saved.maps.Single(m=>m.id=="Arena_02").name);Assert.AreEqual(root.mapName,saved.maps[0].name);
  }
  [Test] public void ConcurrentSameMapEditIsRejectedWithoutChangingTheFile(){
   Load();var latest=Config();latest.maps[0].name="同伴已更新";File.WriteAllText(file,JsonUtility.ToJson(latest));string before=File.ReadAllText(file);
   root.mapName="过期的编辑器";Assert.Throws<InvalidOperationException>(()=>LevelLayoutIO.Save(root,file));Assert.AreEqual(before,File.ReadAllText(file));
  }
  [Test] public void NewMapCannotOverwriteExistingAndDefaultMapCannotBeDisabled(){
   Load();int originalCount=Config().maps.Length;root.newMap=true;Assert.Throws<InvalidOperationException>(()=>LevelLayoutIO.Save(root,file));
   root.mapId="Arena_02";root.mapName="复制关卡";root.available=false;LevelLayoutIO.Save(root,file);Assert.AreEqual(originalCount+1,Config().maps.Length);
   root.mapId="Arena_01";root.available=false;Assert.IsTrue(LevelLayoutIO.Validate(root).Any(s=>s.Contains("必须保持开放")));
  }
  [Test] public void InvalidPlacementsAndSeatDuplicatesBlockExport(){
   Load();var p=LevelLayoutIO.Add(root,LevelPieceKind.Pickup,new Vector2(-3.8f,-2.8f),Vector2.one);p.weapon=WeaponKind.AK;
   var extra=LevelLayoutIO.Add(root,LevelPieceKind.Spawn,new Vector2(-14,-5),Vector2.one);extra.team=0;extra.seat=0;
   var floor=LevelLayoutIO.Add(root,LevelPieceKind.Floor,new Vector2(16,0),new Vector2(4,4));
   var errors=LevelLayoutIO.Validate(root);Assert.IsTrue(errors.Any(s=>s.Contains("重叠")));Assert.IsTrue(errors.Any(s=>s.Contains("只能有一个")));Assert.IsTrue(errors.Any(s=>s.Contains("地面")));
   string before=File.ReadAllText(file);Assert.Throws<InvalidOperationException>(()=>LevelLayoutIO.Save(root,file));Assert.AreEqual(before,File.ReadAllText(file));
  }
  [Test] public void NativeUndoRestoresMovementAndGridSnapChangesOnlySelectedPiece(){
   Load();var piece=root.GetComponentsInChildren<LevelPiece>().First(p=>p.kind==LevelPieceKind.Obstacle);var original=piece.transform.localPosition;
   Undo.IncrementCurrentGroup();Undo.RecordObject(piece.transform,"Move test");piece.transform.localPosition=new Vector3(1.13f,2.36f,0);Undo.FlushUndoRecordObjects();
   Undo.PerformUndo();Assert.AreEqual(original,piece.transform.localPosition);Undo.PerformRedo();Assert.AreEqual(1.13f,piece.transform.localPosition.x,.001f);
   root.grid=.5f;LevelEditorWindow.Snap(new[]{piece});Assert.AreEqual(new Vector3(1,2.5f,0),piece.transform.localPosition);
  }
  [Test] public void FixedPickupsRespawnAsPlacedAndZeroSecondsMeansSingleUse(){
   var state=MapContent.CreatePickup(new PickupDefinition{position=Vector2.one,item=ItemKind.Bomb,respawnSeconds=3,randomizeOnRespawn=false});
   MapContent.ScheduleRespawn(state,10);Assert.IsFalse(MapContent.TryRespawn(state,12,()=>ItemKind.Heal));Assert.IsTrue(MapContent.TryRespawn(state,13,()=>ItemKind.Heal));
   Assert.AreEqual((int)ItemKind.Bomb,state.item);Assert.AreEqual(0,state.weapon);
   state.respawnSeconds=0;MapContent.ScheduleRespawn(state,20);Assert.IsFalse(MapContent.TryRespawn(state,float.MaxValue,()=>ItemKind.Heal));Assert.IsFalse(state.active);
  }
  [Test] public void RuntimeUsesEditedRevivalSizesAndFloorNeverCreatesWallCollision(){
   Load();var zone=root.GetComponentsInChildren<LevelPiece>().First(p=>p.kind==LevelPieceKind.Revival);zone.transform.localScale=new Vector3(2,2,1);
   LevelLayoutIO.Add(root,LevelPieceKind.Floor,Vector2.zero,new Vector2(10,8));var map=LevelLayoutIO.Collect(root);
   Assert.IsTrue(MapContent.InRevival(map,(Vector2)zone.transform.localPosition+new Vector2(.9f,0)));
   Assert.IsFalse(MapContent.InRevival(map,(Vector2)zone.transform.localPosition+new Vector2(1.1f,0)));
   var go=new GameObject("Runtime map test");try{var arena=go.AddComponent<ArenaView>();arena.Build(map);Assert.AreEqual(map.blocks.Length+4,go.GetComponentsInChildren<BoxCollider2D>().Length);}finally{Object.DestroyImmediate(go);}
  }
 }
}
