using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace BoundShoot.Tests {
 public class MenuExitPlayTests {
  const ushort TestPort=17894;GameClient client;NetworkManager peer;System.Net.Sockets.UdpClient pendingEndpoint;int skin,sound;string name;
  [UnitySetUp] public IEnumerator Setup(){
   name=PlayerPrefs.GetString("BoundShoot.Name","");skin=PlayerPrefs.GetInt("BoundShoot.Skin",0);sound=PlayerPrefs.GetInt("BoundShoot.Sound",1);
   if(Object.FindObjectOfType<Bootstrap>()==null)new GameObject("Menu bootstrap").AddComponent<Bootstrap>();yield return null;yield return null;
   client=GameClient.Instance;client.EnterName("menu_test");client.SelectCharacter(3);
  }
  void ClickReturn(){
   typeof(GameClient).GetField("settings",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(client,true);
   typeof(GameClient).GetMethod("Redraw",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(client,null);
   Object.FindObjectsOfType<Button>().Last(b=>b.GetComponentInChildren<Text>()?.text=="回到主菜单").onClick.Invoke();
  }
  [UnityTest] public IEnumerator TrainingMenuReturnPreservesPreferencesAndAllowsAnotherSession(){
   client.StartTraining();yield return Wait(()=>client.Screen=="Training");var p=TrainingRuntime.Instance.World.Players.Single(n=>n.TrainingRole==0);
   var ammo=Object.FindObjectsOfType<Text>().Single(t=>t.name=="Ammo value");Assert.IsTrue(ammo.text.Contains("无限"));p.Equip(WeaponKind.AK);yield return null;
   Assert.IsTrue(ammo.text.Contains("60/60"));yield return new WaitForSecondsRealtime(3.1f);Assert.IsTrue(ammo.isActiveAndEnabled);
   ClickReturn();yield return Wait(()=>client.Screen=="Menu"&&!Bootstrap.Network.IsListening);
   Assert.IsNull(TrainingRuntime.Instance);Assert.IsNull(BattleWorld.Instance);Assert.IsEmpty(NetPlayer.All);Assert.AreEqual(3,client.SelectedSkin);Assert.AreEqual("menu_test",PlayerPrefs.GetString("BoundShoot.Name"));Assert.AreEqual(sound,PlayerPrefs.GetInt("BoundShoot.Sound",1));Assert.IsNull(client.Room);Assert.IsNull(client.User);
   yield return null;Assert.IsFalse(Object.FindObjectsOfType<Button>().Any(b=>b.GetComponentInChildren<Text>()?.text=="回到主菜单"),"Settings overlay must close");
   client.StartTraining();yield return Wait(()=>client.Screen=="Training");Assert.AreEqual((int)WeaponKind.Dagger,TrainingRuntime.Instance.World.Players.Single(n=>n.TrainingRole==0).Weapon.Value);
  }
  [UnityTest] public IEnumerator HostMenuReturnDisconnectsPeerAndWorksFromWaitingRoomAndBattle(){
   foreach(bool start in new[]{false,true}){
    client.StartLocalMatch(port:TestPort);yield return Wait(()=>client.Screen=="LocalRoom");
    if(start){
     var go=new GameObject("Menu exit remote peer");var transport=go.AddComponent<UnityTransport>();peer=go.AddComponent<NetworkManager>();
     peer.NetworkConfig=new NetworkConfig{NetworkTransport=transport,EnableSceneManagement=false,ConnectionApproval=true,ForceSamePrefabs=true,TickRate=30};peer.AddNetworkPrefab(Resources.Load<GameObject>("NetPlayer"));peer.AddNetworkPrefab(Resources.Load<GameObject>("NetWorld"));
     peer.NetworkConfig.ConnectionData=Encoding.UTF8.GetBytes(JsonUtility.ToJson(new LocalIdentity{name="menu_peer",skin=2}));transport.SetConnectionData("127.0.0.1",TestPort);Assert.IsTrue(peer.StartClient());yield return Wait(()=>peer.IsConnectedClient);LocalMatchRuntime.Instance.StartRound();yield return Wait(()=>client.Screen=="LocalBattle");
    }
    ClickReturn();yield return Wait(()=>client.Screen=="Menu"&&!Bootstrap.Network.IsListening);Assert.IsNull(LocalMatchRuntime.Instance);
    if(peer!=null){yield return Wait(()=>!peer.IsConnectedClient);peer.Shutdown();yield return Wait(()=>!peer.IsListening&&!peer.ShutdownInProgress);Object.Destroy(peer.gameObject);peer=null;yield return null;}
    Assert.IsEmpty(NetPlayer.All);
   }
  }
  [UnityTest] public IEnumerator MenuReturnCancelsAnInProgressConnection(){
   pendingEndpoint=new System.Net.Sockets.UdpClient(TestPort); // Silent endpoint: connection remains pending without closed-port ICMP errors.
   client.StartLocalMatch(false,TestPort);yield return null;client.ReturnToMainMenu();client.ReturnToMainMenu();
   yield return Wait(()=>client.Screen=="Menu"&&!Bootstrap.Network.IsListening&&!Bootstrap.Network.ShutdownInProgress);Assert.IsNull(LocalMatchRuntime.Instance);pendingEndpoint.Dispose();pendingEndpoint=null;
   client.StartTraining();yield return Wait(()=>client.Screen=="Training");
  }
  [UnityTest] public IEnumerator LocalKillShowsNoticeAndResultPagesShowMvpThenOwnRow(){
   client.StartLocalMatch(port:TestPort);
   yield return Wait(()=>client.Screen=="LocalRoom");
   var runtime=LocalMatchRuntime.Instance;
   var world=runtime.World;
   runtime.StartRound();
   yield return Wait(()=>world.Phase.Value==2&&client.Screen=="LocalBattle");
   world.enabled=false;
   var own=world.Players.Single(p=>!p.IsBot.Value);
   var teammate=world.Players.First(p=>p!=own&&p.Team.Value==own.Team.Value);
   var victim=world.Players.First(p=>p.Team.Value!=own.Team.Value);
   victim.ProtectionUntil=0;
   victim.Health.Value=10;
   victim.Shield.Value=0;
   victim.Hurt(1,teammate);
   victim.Hurt(9,own);
   Assert.AreEqual(1,own.Kills.Value);
   Assert.AreEqual(1,teammate.Assists.Value);
   string blue=own.Team.Value==0?own.DisplayName.Value.ToString():victim.DisplayName.Value.ToString();
   string red=own.Team.Value==1?own.DisplayName.Value.ToString():victim.DisplayName.Value.ToString();
   yield return Wait(()=>Object.FindObjectsOfType<Text>().Any(t=>t.supportRichText&&t.text.Contains("<color=#61C8FF>"+blue+"</color>")&&t.text.Contains("<color=#FF7272>"+red+"</color>")));
   world.Winner.Value=own.Team.Value;
   world.Phase.Value=3;
   yield return Wait(()=>client.Screen=="LocalResult");
   Assert.IsTrue(Object.FindObjectsOfType<Text>().Any(t=>t.text=="MVP"));
   Object.FindObjectsOfType<Button>().Single(b=>b.GetComponentInChildren<Text>()?.text=="下一页 · 全员战绩").onClick.Invoke();
   Assert.IsTrue(Object.FindObjectsOfType<Text>().Any(t=>t.text==own.DisplayName.Value+"  ·  你"));
   Assert.IsTrue(Object.FindObjectsOfType<Text>().Any(t=>t.text=="助攻 A"));
  }
  static IEnumerator Wait(System.Func<bool> ready){float end=Time.realtimeSinceStartup+15;while(!ready()&&Time.realtimeSinceStartup<end)yield return null;Assert.IsTrue(ready(),"Menu lifecycle timed out");}
  [UnityTearDown] public IEnumerator Cleanup(){
   if(client!=null){client.ReturnToMainMenu();yield return Wait(()=>client.Screen=="Menu"&&!Bootstrap.Network.IsListening);}
   if(peer!=null){peer.Shutdown();yield return Wait(()=>!peer.IsListening&&!peer.ShutdownInProgress);Object.Destroy(peer.gameObject);yield return null;}
   pendingEndpoint?.Dispose();pendingEndpoint=null;
   PlayerPrefs.SetString("BoundShoot.Name",name);PlayerPrefs.SetInt("BoundShoot.Skin",skin);PlayerPrefs.SetInt("BoundShoot.Sound",sound);
  }
 }
}
