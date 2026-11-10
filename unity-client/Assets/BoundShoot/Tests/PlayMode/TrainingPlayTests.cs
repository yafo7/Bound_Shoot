using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace BoundShoot.Tests {
 public class TrainingPlayTests {
  [UnityTest] public IEnumerator TrainingRunsWithoutBackendAndCanRestart(){

   if(Object.FindObjectOfType<Bootstrap>()==null)new GameObject("Training Test Bootstrap").AddComponent<Bootstrap>();
   yield return null;yield return null;
   var client=Object.FindObjectOfType<GameClient>();
   Assert.IsTrue(client!=null,"Bootstrap did not create GameClient");
   client.EnterName("yafo777");Assert.AreEqual("Menu",client.Screen);Assert.IsNull(client.User);
   client.StartTraining();yield return WaitFor(()=>client.Screen=="Training");
   var runtime=TrainingRuntime.Instance;var world=runtime.World;
   Assert.IsTrue(Bootstrap.Network.IsHost);Assert.AreEqual(2,world.Phase.Value);Assert.AreEqual(0,world.Clock.Value);
   Assert.AreEqual(15,world.Supplies.Count);Assert.AreEqual(3,world.Players.Count);
   for(int i=0;i<4;i++)runtime.AddTeammate();
   Assert.AreEqual(3,runtime.Teammates);Assert.AreEqual(6,world.Players.Count);
   Assert.IsTrue(world.Players.All(p=>p.Connected.Value&&!p.IsBot.Value));
   var passive=world.Players.Where(p=>p.TrainingRole==1).ToArray();
   var initial=passive.Select(p=>p.Body.position).ToArray();
   for(int i=0;i<10;i++)yield return new WaitForFixedUpdate();
   for(int i=0;i<3;i++){Assert.Less(Vector2.Distance(initial[i],passive[i].Body.position),.1f);Assert.AreEqual(Vector2.zero,passive[i].Move);Assert.IsFalse(passive[i].Fire);}
   var human=world.Players.Single(p=>p.TrainingRole==0);
   int maxHealth=GameConfig.Current.baseHealth;
   Assert.IsTrue(world.Players.All(p=>p.Health.Value==maxHealth&&p.MaxHealth.Value==maxHealth),"All players use the configured character health cap");
   foreach(var actor in world.Players){
    foreach(int hp in new[]{maxHealth,maxHealth-10}){
     actor.Health.Value=hp;actor.Shield.Value=1;
     foreach(var weapon in new[]{WeaponKind.Sniper,WeaponKind.Pistol,WeaponKind.AK,WeaponKind.Dagger}){
      Assert.IsTrue(actor.Equip(weapon));Assert.AreEqual(hp,actor.Health.Value,"Equipping "+weapon+" must not heal or truncate health");Assert.AreEqual(maxHealth,actor.MaxHealth.Value);Assert.AreEqual(1,actor.Shield.Value);
     }
    }actor.Resurrect();
   }
   human.ProtectionUntil=0;human.Hurt(10,null);Assert.AreEqual(maxHealth-10,human.Health.Value);world.ApplyItem(human,ItemKind.Heal);Assert.AreEqual(maxHealth-5,human.Health.Value);world.ApplyItem(human,ItemKind.Shield);Assert.AreEqual(5,human.Shield.Value);world.ApplyItem(human,ItemKind.Shield);Assert.AreEqual(5,human.Shield.Value);
   human.Equip(WeaponKind.Dagger);Assert.AreEqual(maxHealth,human.MaxHealth.Value);Assert.AreEqual(maxHealth-5,human.Health.Value,"Weapon switches preserve current health");
   yield return null;yield return null;
   var popup=Object.FindObjectsOfType<RectTransform>().Single(t=>t.name=="Weapon pickup notice");Assert.IsTrue(popup.gameObject.activeSelf);Assert.IsFalse(popup.GetComponentsInChildren<UnityEngine.UI.Text>().Any(t=>t.text.Contains("生命上限")),"Weapon card must not advertise a weapon health cap");
   var hearts=Object.FindObjectsOfType<UnityEngine.UI.Image>().Where(i=>i.isActiveAndEnabled&&i.sprite!=null&&(i.sprite.name=="forest_heart"||i.sprite.name=="forest_heartEmpty")).ToArray();
   Assert.AreEqual(1,hearts.Length,"HUD uses one icon regardless of maximum HP");
   var health=Object.FindObjectsOfType<UnityEngine.UI.Text>().Single(t=>t.name=="Health value");Assert.AreEqual((maxHealth-5)+"/"+maxHealth,health.text);
   Assert.IsTrue(Object.FindObjectsOfType<UnityEngine.UI.Text>().Single(t=>t.name=="Shield value").text.Contains("5"));
   foreach(var heart in hearts){var r=heart.rectTransform;Assert.LessOrEqual(r.anchoredPosition.x+r.rect.width,1600);Assert.LessOrEqual(-r.anchoredPosition.y+r.rect.height,900);}
   yield return new WaitForSecondsRealtime(3.1f);Assert.IsFalse(popup.gameObject.activeSelf,"Weapon card must disappear after three seconds");
   human.Health.Value=maxHealth;world.ApplyItem(human,ItemKind.Heal);Assert.AreEqual(maxHealth,human.Health.Value);human.Resurrect();Assert.AreEqual(maxHealth,human.Health.Value);Assert.AreEqual(maxHealth,human.MaxHealth.Value);yield return null;yield return null;Assert.AreEqual(maxHealth+"/"+maxHealth,health.text);
   Assert.AreEqual(RigidbodyInterpolation2D.Interpolate,human.Body.interpolation,"Host movement must interpolate between physics steps");
   human.Body.position=world.MapOverride.pickups.Single(p=>p.weapon==WeaponKind.Pistol).position;
   yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
   Assert.AreEqual((int)WeaponKind.Pistol,human.Weapon.Value);
   human.Body.position=world.MapOverride.pickups.Single(p=>p.weapon==WeaponKind.AK).position;
   yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
   Assert.AreEqual((int)WeaponKind.AK,human.Weapon.Value);
   human.Body.position=world.MapOverride.pickups.Single(p=>p.item==ItemKind.Dash).position;
   yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
   Assert.AreEqual((int)ItemKind.Dash,human.ItemAt(1));
   human.SubmitInput(Vector2.zero,Vector2.right,false,true);human.SubmitInput(Vector2.zero,Vector2.right,true,false,true);human.SubmitInput(Vector2.zero,Vector2.right,false,false);
   yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
   Assert.AreEqual(0,human.ItemAt(1));Assert.Greater(human.SpeedBoostUntil,Time.time);
   var target=world.Players.First(p=>p.TrainingRole==2);target.Hurt(999,human);Assert.IsFalse(target.Alive);
   yield return new WaitForSeconds(2.2f);Assert.IsTrue(target.Alive);Assert.AreEqual(2,world.Phase.Value);
   runtime.ResetRoom();Assert.AreEqual((int)WeaponKind.Dagger,human.Weapon.Value);Assert.AreEqual(0,human.ItemAt(1));Assert.AreEqual(0,human.Kills.Value);
   Assert.IsTrue(world.Supplies.All(p=>p.active));
   for(int i=0;i<4;i++)runtime.RemoveTeammate();Assert.AreEqual(0,runtime.Teammates);
   client.LeaveTraining();yield return WaitFor(()=>TrainingRuntime.Instance==null&&!Bootstrap.Network.IsListening);
   Assert.AreEqual("Menu",client.Screen);Assert.AreEqual(0,NetPlayer.All.Count);Assert.IsNull(client.User);
   client.StartTraining();yield return WaitFor(()=>client.Screen=="Training");Assert.AreEqual(3,TrainingRuntime.Instance.World.Players.Count);
   client.LeaveTraining();yield return WaitFor(()=>TrainingRuntime.Instance==null&&!Bootstrap.Network.IsListening);

  }
  static IEnumerator WaitFor(System.Func<bool> ready){float end=Time.realtimeSinceStartup+10;while(!ready()&&Time.realtimeSinceStartup<end)yield return null;Assert.IsTrue(ready(),"Training lifecycle timed out");}
  [UnityTearDown] public IEnumerator Cleanup(){if(TrainingRuntime.Instance!=null)yield return TrainingRuntime.Instance.Stop();}
 }
}
