using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace BoundShoot.Tests {
 public class InventoryPlayTests {
  TrainingRuntime runtime;NetPlayer player;BattleWorld World=>runtime.World;
  [UnitySetUp] public IEnumerator Setup(){
   if(Object.FindObjectOfType<Bootstrap>()==null)new GameObject("Inventory bootstrap").AddComponent<Bootstrap>();yield return null;yield return null;
   var client=GameClient.Instance;client.EnterName("inventory_test");client.StartTraining();yield return Wait(()=>client.Screen=="Training");runtime=TrainingRuntime.Instance;
   runtime.enabled=false;World.enabled=false;foreach(var p in World.Players){p.enabled=false;p.Body.simulated=false;}player=World.Players.Single(p=>p.TrainingRole==0);
  }
  void Cycle()=>player.SubmitInput(Vector2.zero,Vector2.right,false,true);
  void Press(bool held=true)=>player.SubmitInput(Vector2.zero,Vector2.right,held,false,true);
  void Release()=>player.SubmitInput(Vector2.zero,Vector2.right,false,false);
  [UnityTest] public IEnumerator AllSevenItemsAreStoredAndRepeatedItemsOccupySeparateSlots(){
   foreach(ItemKind kind in new[]{ItemKind.Heal,ItemKind.Shield,ItemKind.Dash,ItemKind.Bomb,ItemKind.Shorten,ItemKind.Extend,ItemKind.Elastic}){
    player.ClearInventory();player.Health.Value=70;player.Shield.Value=0;
    Assert.IsTrue(World.TryCollectItem(player,kind));Assert.AreEqual((int)kind,player.ItemAt(1));Assert.AreEqual(70,player.Health.Value);Assert.AreEqual(0,player.Shield.Value);Assert.AreEqual(0,player.SelectedSlot.Value);
   }
   player.ClearInventory();for(int i=0;i<3;i++)Assert.IsTrue(World.TryCollectItem(player,ItemKind.Heal));Assert.AreEqual(3,player.InventoryCount);Assert.IsFalse(World.TryCollectItem(player,ItemKind.Bomb));
   for(int i=1;i<=3;i++)Assert.AreEqual((int)ItemKind.Heal,player.ItemAt(i));
   player.ClearInventory();Assert.IsTrue(World.TryCollectItem(player,ItemKind.Mystery));Assert.That(player.ItemAt(1),Is.InRange(1,7));yield return null;
  }
  [UnityTest] public IEnumerator CyclingSkipsEmptySlotsAndOneClickUsesOnlySelectedItemWithoutFiring(){
   player.Equip(WeaponKind.AK);int ammo=player.Ammo.Value;player.Health.Value=70;
   World.TryCollectItem(player,ItemKind.Heal);World.TryCollectItem(player,ItemKind.Shield);World.TryCollectItem(player,ItemKind.Heal);
   foreach(int expected in new[]{1,2,3,0,1}){Cycle();Assert.AreEqual(expected,player.SelectedSlot.Value);}
   Assert.AreEqual(70,player.Health.Value,"E must not use the selected item");Assert.IsFalse(World.TryAttack(player),"Selected items must block weapon attacks");
   int healed=Mathf.Min(player.MaxHealth.Value,70+GameConfig.Current.healAmount);
   Press();Assert.AreEqual(healed,player.Health.Value);Assert.AreEqual(2,player.InventoryCount);Assert.AreEqual(0,player.SelectedSlot.Value);Assert.AreEqual(0,player.ItemAt(1));Assert.AreEqual(ammo,player.Ammo.Value);Assert.IsFalse(player.Fire);
   player.NextShot=0;player.SubmitInput(Vector2.zero,Vector2.right,true,false);Assert.IsFalse(player.Fire,"Holding after item use must not automatically shoot");Assert.AreEqual(ammo,player.Ammo.Value);
   player.SubmitInput(Vector2.zero,Vector2.right,false,false,false,false); // Pointer over UI, mouse still held.
   player.SubmitInput(Vector2.zero,Vector2.right,true,false,false,false);Assert.IsFalse(player.Fire);
   Release();Cycle();Assert.AreEqual(2,player.SelectedSlot.Value);Press(false);Assert.AreEqual(Mathf.Min(GameConfig.Current.shieldCap,GameConfig.Current.shieldAmount),player.Shield.Value);Assert.AreEqual(1,player.InventoryCount);Assert.AreEqual(0,player.SelectedSlot.Value);
   Release();player.NextShot=0;Press();Assert.AreEqual(ammo-1,player.Ammo.Value);Release();yield return null;
  }
  [UnityTest] public IEnumerator FullInventoryLeavesGroundPickupUntouchedAndFreedSlotCanCollectIt(){
   for(int i=0;i<3;i++)World.TryCollectItem(player,ItemKind.Shield);
   var drop=World.Supplies.Single(p=>p.item==(int)ItemKind.Heal);player.Body.position=drop.position;player.transform.position=drop.position;
   var tick=typeof(BattleWorld).GetMethod("TickPickups",BindingFlags.Instance|BindingFlags.NonPublic);tick.Invoke(World,null);
   Assert.IsTrue(drop.active);Assert.AreEqual(0,drop.respawnAt);Assert.AreEqual(3,player.InventoryCount);
   Cycle();Press(false);Assert.AreEqual(2,player.InventoryCount);tick.Invoke(World,null);Assert.IsFalse(drop.active);Assert.Greater(drop.respawnAt,Time.time);Assert.AreEqual((int)ItemKind.Heal,player.ItemAt(1));Assert.AreEqual(3,player.InventoryCount);yield return null;
  }
  [UnityTest] public IEnumerator InventoryHudShowsFourSlotsAndDeathReviveResetClearContents(){
   World.TryCollectItem(player,ItemKind.Bomb);World.TryCollectItem(player,ItemKind.Dash);Cycle();yield return null;
   var slots=Object.FindObjectsOfType<RectTransform>().Where(t=>t.name.StartsWith("Inventory slot ")).OrderBy(t=>t.name).ToArray();Assert.AreEqual(4,slots.Length);Assert.AreEqual((Color)new Color32(250,205,91,255),slots[1].GetComponent<Image>().color);
   Assert.IsTrue(slots[1].GetComponentsInChildren<Text>().Any(t=>t.text=="炸弹"));Assert.IsTrue(slots[3].GetComponentsInChildren<Text>().Any(t=>t.text=="空"));
   player.ProtectionUntil=0;player.Hurt(9999,null);Assert.AreEqual(0,player.InventoryCount);Assert.AreEqual(0,player.SelectedSlot.Value);player.Resurrect();Assert.AreEqual(0,player.InventoryCount);Assert.AreEqual((int)WeaponKind.Dagger,player.Weapon.Value);
   World.TryCollectItem(player,ItemKind.Bomb);Cycle();World.ResetTraining();Assert.AreEqual(0,player.InventoryCount);Assert.AreEqual(0,player.SelectedSlot.Value);
  }
  [UnityTest] public IEnumerator EachItemEffectOccursOnlyOnActivationAndReturnsToWeapon(){
   foreach(ItemKind kind in new[]{ItemKind.Shorten,ItemKind.Extend,ItemKind.Elastic,ItemKind.Dash,ItemKind.Bomb}){
    Assert.IsTrue(World.TryCollectItem(player,kind));Cycle();Assert.IsTrue(World.TryUseSelected(player));Assert.AreEqual(0,player.SelectedSlot.Value);Assert.AreEqual(0,player.InventoryCount);
    if(kind==ItemKind.Dash){
     Assert.IsTrue(player.SpeedBoosted);
     Assert.AreEqual(GameConfig.Current.speedBoostSeconds,player.SpeedBoostUntil-Time.time,.01f);
    }
    if(kind==ItemKind.Bomb)Assert.AreEqual(1,((System.Collections.Generic.List<BombState>)typeof(BattleWorld).GetField("bombs",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(World)).Count);
    if(kind==ItemKind.Elastic)Assert.Greater((float)typeof(BattleWorld).GetField("elasticUntil",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(World),Time.time);
    if(kind==ItemKind.Shorten||kind==ItemKind.Extend){var values=(float[])typeof(BattleWorld).GetField(kind==ItemKind.Shorten?"shortenUntil":"extendUntil",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(World);Assert.Greater(values[kind==ItemKind.Shorten?1-player.Team.Value:player.Team.Value],Time.time);}
   }yield return null;
  }
  static IEnumerator Wait(System.Func<bool> ready){float end=Time.realtimeSinceStartup+10;while(!ready()&&Time.realtimeSinceStartup<end)yield return null;Assert.IsTrue(ready());}
  [UnityTearDown] public IEnumerator Cleanup(){if(GameClient.Instance!=null){GameClient.Instance.ReturnToMainMenu();yield return Wait(()=>GameClient.Instance.Screen=="Menu"&&!Bootstrap.Network.IsListening);}}
 }
}
