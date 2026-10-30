using System.Collections;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;
namespace BoundShoot.Tests {
 public class CombatPresentationPlayTests {
  TrainingRuntime runtime;LocalMatchRuntime local;NetworkManager peer;GameObject extraCover;bool previousRender;
  [UnityTest] public IEnumerator NearbyShootersKeepTheirOwnRecoilAndBlockedMuzzlesNeverLaunch(){
   previousRender=Draw2D.RenderInBatch;Draw2D.RenderInBatch=true;
   if(Object.FindObjectOfType<Bootstrap>()==null)new GameObject("Combat rendering bootstrap").AddComponent<Bootstrap>();
   if(Camera.main==null){var camera=new GameObject("Combat test camera");camera.tag="MainCamera";camera.AddComponent<Camera>().orthographic=true;camera.transform.position=Vector3.back*20;}
   if(Object.FindObjectOfType<AudioListener>()==null)Camera.main.gameObject.AddComponent<AudioListener>();
   yield return null;yield return null;
   runtime=new GameObject("Combat presentation room").AddComponent<TrainingRuntime>();yield return runtime.Begin("shooter_test",0);
   var world=runtime.World;var first=world.Players.Single(p=>p.TrainingRole==0);var second=world.AddTrainingTeammate();
   first.IsBot.Value=true; // Disable physical keyboard polling; no AI exists in training.
   Teleport(first,new Vector2(-7,-6));Teleport(second,new Vector2(-6.6f,-6));
   first.Equip(WeaponKind.AK);second.Equip(WeaponKind.Pistol);first.Aim=second.Aim=Vector2.right;
   var firstView=first.GetComponent<CyberPlayerView>();var secondView=second.GetComponent<CyberPlayerView>();Assert.IsNotNull(firstView);Assert.IsNotNull(secondView);
   for(int i=0;i<50;i++){
    first.NextShot=0;Assert.IsTrue(world.TryAttack(first));Assert.AreEqual(i+1,firstView.KickCount);Assert.AreEqual(i,secondView.KickCount);
    second.NextShot=0;Assert.IsTrue(world.TryAttack(second));Assert.AreEqual(i+1,firstView.KickCount);Assert.AreEqual(i+1,secondView.KickCount);
   }
   Assert.AreEqual((int)WeaponKind.AK,firstView.LastShotWeapon);Assert.AreEqual((int)WeaponKind.Pistol,secondView.LastShotWeapon);
   Assert.AreEqual(100,world.VisibleProjectileCount);
   Assert.IsFalse(world.TryAttack(first),"Cooldown must still be authoritative");
   world.ResetTraining();Assert.AreEqual(0,world.VisibleProjectileCount,"Round reset must clear every visual projectile");
   first.Equip(WeaponKind.AK);first.NextShot=0;Teleport(first,new Vector2(-3.4f,0));first.Aim=Vector2.right;
   Physics2D.SyncTransforms();Assert.IsTrue(world.TryAttack(first));Assert.AreEqual(0,world.VisibleProjectileCount,"A gun crossing the wall must not spawn on its far side");
   // Boundary walls exist as colliders, not as entries in the rope visibility map.
   extraCover=new GameObject("Collider-only boundary regression");extraCover.layer=8;extraCover.transform.position=new Vector2(-5.5f,-6);extraCover.AddComponent<BoxCollider2D>().size=new Vector2(.2f,2);
   Teleport(first,new Vector2(-7,-6));first.NextShot=0;Physics2D.SyncTransforms();
   Assert.IsTrue(world.TryAttack(first));Assert.AreEqual(0,world.VisibleProjectileCount,"Collider-only cover must also stop a long muzzle");Object.DestroyImmediate(extraCover);extraCover=null;
   var target=world.Players.First(p=>p.TrainingRole==2);target.ProtectionUntil=0;target.Health.Value=3;
   Teleport(first,new Vector2(-7,-6));Teleport(target,new Vector2(-6.1f,-6));Physics2D.SyncTransforms();first.NextShot=0;
   Assert.IsTrue(world.TryAttack(first));Assert.AreEqual(1,target.Health.Value,"A close enemy under the long barrel must still be hit");Assert.AreEqual(0,world.VisibleProjectileCount);
   first.Equip(WeaponKind.Dagger);first.NextShot=0;target.Health.Value=3;int kicks=firstView.KickCount;
   Assert.IsTrue(world.TryAttack(first));Assert.AreEqual(0,target.Health.Value);Assert.AreEqual(kicks,firstView.KickCount,"A melee attack must not play gun recoil");Assert.AreEqual(0,world.VisibleProjectileCount);
   Teleport(first,new Vector2(-7,-6));Teleport(second,new Vector2(-4,-6));world.ResetRopesForTeleport(first);world.ResetRopesForTeleport(second);
   yield return null;yield return null;
   Assert.IsTrue(world.GetComponentsInChildren<LineRenderer>().Where(l=>l.transform.parent==world.transform&&l.positionCount>=2).Any(l=>Vector2.Distance(l.GetPosition(0),first.transform.position)<.001f),"Host rope endpoints must use the interpolated display position");
   yield return runtime.Stop();runtime=null;
  }
  [UnityTest] public IEnumerator RemoteObserverReceivesExactShooterAndClearsProjectilesAtResult(){
   previousRender=Draw2D.RenderInBatch;Draw2D.RenderInBatch=true;
   if(Object.FindObjectOfType<Bootstrap>()==null)new GameObject("Remote combat bootstrap").AddComponent<Bootstrap>();
   if(Camera.main==null){var camera=new GameObject("Remote test camera");camera.tag="MainCamera";camera.AddComponent<Camera>().orthographic=true;camera.transform.position=Vector3.back*20;}
   if(Object.FindObjectOfType<AudioListener>()==null)Camera.main.gameObject.AddComponent<AudioListener>();yield return null;yield return null;
   local=new GameObject("Remote combat host").AddComponent<LocalMatchRuntime>();yield return local.Begin("host",0,2,true,"127.0.0.1",17887);var authority=local.World;
   var peerObject=new GameObject("Visible remote observer");var transport=peerObject.AddComponent<UnityTransport>();peer=peerObject.AddComponent<NetworkManager>();
   peer.NetworkConfig=new NetworkConfig{NetworkTransport=transport,EnableSceneManagement=false,ConnectionApproval=true,ForceSamePrefabs=true,TickRate=30};
   peer.AddNetworkPrefab(Resources.Load<GameObject>("NetPlayer"));peer.AddNetworkPrefab(Resources.Load<GameObject>("NetWorld"));
   peer.NetworkConfig.ConnectionData=Encoding.UTF8.GetBytes(JsonUtility.ToJson(new LocalIdentity{name="observer",skin=7}));transport.SetConnectionData("127.0.0.1",17887);Assert.IsTrue(peer.StartClient());
   yield return WaitFor(()=>peer.IsConnectedClient&&peer.SpawnManager.SpawnedObjects.Values.Any(n=>n.GetComponent<BattleWorld>()!=null));
   foreach(var player in authority.Players)player.IsBot.Value=false;
   local.StartRound();yield return WaitFor(()=>authority.Phase.Value==2);
   var first=authority.Players.Single(p=>p.Team.Value==0&&p.Seat.Value==0);var second=authority.Players.Single(p=>p.Team.Value==0&&p.Seat.Value==1);
   Teleport(first,new Vector2(-31,-4));Teleport(second,new Vector2(-30.6f,-4));second.Equip(WeaponKind.AK);second.NextShot=0;second.Aim=Vector2.right;Physics2D.SyncTransforms();
   yield return WaitFor(()=>peer.SpawnManager.SpawnedObjects.ContainsKey(second.NetworkObjectId));
   var remoteFirst=peer.SpawnManager.SpawnedObjects[first.NetworkObjectId].GetComponent<CyberPlayerView>();var remoteSecond=peer.SpawnManager.SpawnedObjects[second.NetworkObjectId].GetComponent<CyberPlayerView>();
   var remoteWorld=peer.SpawnManager.SpawnedObjects.Values.Single(n=>n.GetComponent<BattleWorld>()!=null).GetComponent<BattleWorld>();
   Assert.IsTrue(authority.TryAttack(second));yield return WaitFor(()=>remoteSecond.KickCount==1);
   Assert.AreEqual(0,remoteFirst.KickCount);Assert.AreEqual((int)WeaponKind.AK,remoteSecond.LastShotWeapon);Assert.Greater(remoteWorld.VisibleProjectileCount,0);
   foreach(var player in authority.Players.Where(p=>p.Team.Value==1)){player.ProtectionUntil=0;player.Hurt(999,null);}
   yield return WaitFor(()=>remoteWorld.Phase.Value==3);Assert.AreEqual(0,remoteWorld.VisibleProjectileCount);
   local.Restart();yield return WaitFor(()=>remoteWorld.RoundId.Value==authority.RoundId.Value);Assert.AreEqual(0,remoteWorld.VisibleProjectileCount);
  }
  static IEnumerator WaitFor(System.Func<bool> ready){float until=Time.realtimeSinceStartup+15;while(!ready()&&Time.realtimeSinceStartup<until)yield return null;Assert.IsTrue(ready(),"Combat network presentation timed out");}
  static void Teleport(NetPlayer player,Vector2 position){player.Body.position=position;player.transform.position=position;player.Body.velocity=Vector2.zero;}
  [UnityTearDown] public IEnumerator Cleanup(){
   if(extraCover!=null)Object.DestroyImmediate(extraCover);
   if(peer!=null){if(Bootstrap.Network.IsServer&&peer.IsConnectedClient){Bootstrap.Network.DisconnectClient(peer.LocalClientId);yield return WaitFor(()=>!peer.IsConnectedClient);}peer.Shutdown();yield return WaitFor(()=>!peer.IsListening&&!peer.ShutdownInProgress);Object.Destroy(peer.gameObject);}
   if(local!=null)yield return local.Stop();if(runtime!=null)yield return runtime.Stop();Draw2D.RenderInBatch=previousRender;var fx=Object.FindObjectOfType<GardenFx>();if(fx!=null)Object.Destroy(fx.gameObject);
  }
 }
}
