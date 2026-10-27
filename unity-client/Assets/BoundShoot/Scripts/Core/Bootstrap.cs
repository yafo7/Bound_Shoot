using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
namespace BoundShoot {
 public class Bootstrap:MonoBehaviour {
  public static NetworkManager Network;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Launch(){
   if(FindObjectOfType<Bootstrap>()!=null)return;
   new GameObject("Bound Shoot").AddComponent<Bootstrap>();
  }
  void Awake(){
   DontDestroyOnLoad(gameObject);Application.runInBackground=true;Application.targetFrameRate=60;
   Time.fixedDeltaTime=1f/GameConfig.Current.physicsHz;Physics2D.gravity=Vector2.zero;
   var net=new GameObject("Network");DontDestroyOnLoad(net);
   var transport=net.AddComponent<UnityTransport>();Network=net.AddComponent<NetworkManager>();
   Network.NetworkConfig=new NetworkConfig{NetworkTransport=transport,EnableSceneManagement=false,ConnectionApproval=true,
    TickRate=(uint)GameConfig.Current.physicsHz,ClientConnectionBufferTimeout=20,ForceSamePrefabs=true};
   Network.AddNetworkPrefab(Resources.Load<GameObject>("NetPlayer"));
   Network.AddNetworkPrefab(Resources.Load<GameObject>("NetWorld"));
   if(!Application.isBatchMode&&Camera.main==null){
    var cameraObject=new GameObject("Main Camera");cameraObject.tag="MainCamera";var camera=cameraObject.AddComponent<Camera>();
    camera.orthographic=true;camera.orthographicSize=13.5f;camera.transform.position=new Vector3(0,0,-20);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.055f,.08f,.12f);
   }
   bool server=Array.IndexOf(Environment.GetCommandLineArgs(),"-matchFile")>=0;
   if(server)gameObject.AddComponent<ServerRuntime>();else gameObject.AddComponent<GameClient>();
  }
 }
}
