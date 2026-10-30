using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace BoundShoot.Editor {
 [InitializeOnLoad] public static class ForestReview {
  static int stage;static double at,deadline;const string Key="BoundShoot.ForestReview";
  static ForestReview(){bool active=System.Environment.GetCommandLineArgs().Any(a=>a=="BoundShoot.Editor.ForestReview.Start");EditorPrefs.SetBool(Key,active);if(active)Draw2D.RenderInBatch=true;EditorApplication.update+=Tick;}
  public static void Start(){EditorPrefs.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/BoundShoot/Scenes/Boot.unity");EditorApplication.isPlaying=true;}
  static void Tick(){try{Step();}catch(System.Exception error){Debug.LogException(error);Finish(2);}}
  static void Step(){
   if(!EditorPrefs.GetBool(Key,false)||!EditorApplication.isPlaying)return;
   if(deadline==0)deadline=EditorApplication.timeSinceStartup+120;
   if(EditorApplication.timeSinceStartup>deadline){Debug.LogError("Forest review timed out at "+stage);Finish(2);return;}
   if(EditorApplication.timeSinceStartup<at||GameClient.Instance==null)return;var c=GameClient.Instance;
   if(stage==0){Capture("01-title");c.OpenIdentity();stage++;at=EditorApplication.timeSinceStartup+.4;}
   else if(stage==1){Capture("02-identity");c.EnterName("yafo777");stage++;at=EditorApplication.timeSinceStartup+.4;}
   else if(stage==2){Capture("03-modes");c.SelectMode("Training");c.SetRoomSize(3);stage++;at=EditorApplication.timeSinceStartup+.4;}
   else if(stage==3){Capture("04-training-room");c.StartTraining();stage++;}
   else if(stage==4&&c.Screen=="Training"){
    var p=TrainingRuntime.Instance.World.Players.Single(n=>n.TrainingRole==0);p.Equip(WeaponKind.AK);p.Health.Value=1;p.Shield.Value=1;stage++;at=EditorApplication.timeSinceStartup+1;
   }
   else if(stage==5){Capture("05-training");var p=TrainingRuntime.Instance.World.Players.Single(n=>n.TrainingRole==0);p.Equip(WeaponKind.Dagger);p.Health.Value=12;stage=51;at=EditorApplication.timeSinceStartup+.3;}
   else if(stage==51){Capture("12-fifteen-hearts-weapon-stats");stage=52;at=EditorApplication.timeSinceStartup+3.2;}
   else if(stage==52){Capture("13-weapon-card-hidden");c.LeaveTraining();stage=6;}
   else if(stage==6&&TrainingRuntime.Instance==null&&!Bootstrap.Network.IsListening){c.SelectMode("AI");stage++;}
   else if(stage==7&&c.Screen=="LocalRoom"){c.SetRoomSize(4);stage++;at=EditorApplication.timeSinceStartup+.6;}
   else if(stage==8){Capture("06-room-4v4");c.SetRoomSize(3);stage++;at=EditorApplication.timeSinceStartup+.5;}
   else if(stage==9){Capture("07-room-3v3");LocalMatchRuntime.Instance.StartRound();stage++;}
   else if(stage==10&&LocalMatchRuntime.Instance.World.Phase.Value==2){
    var w=LocalMatchRuntime.Instance.World;
    foreach(var p in w.Players){p.IsBot.Value=false;Vector2 pos=new Vector2(p.Team.Value==0?-20:-11,-2+p.Seat.Value*2);p.Body.position=pos;p.transform.position=pos;p.ResetInput();p.Equip(WeaponKind.AK);w.ResetRopesForTeleport(p);}
    stage++;at=EditorApplication.timeSinceStartup+.5;
   }
   else if(stage==11){Capture("08-stairs-battle");var w=LocalMatchRuntime.Instance.World;foreach(var p in w.Players.Where(n=>n.Team.Value==1)){p.ProtectionUntil=0;p.Hurt(999,null);}stage++;at=EditorApplication.timeSinceStartup+.5;}
   else if(stage==12&&c.Screen=="LocalResult"){Capture("09-result");CaptureMap();Catalog();Finish(0);}
  }
  static void Catalog(){
   var canvas=Object.FindObjectOfType<Canvas>();canvas.enabled=true;var ui=canvas.GetComponent<UiKit>();ui.Clear();ui.Backdrop(Color.black);
   var t=Resources.Load<Texture2D>("Forest/tileset_village_abandoned");var s=Sprite.Create(t,new Rect(0,0,t.width,t.height),Vector2.one*.5f);ui.Picture(s,0,0,1600,900);
   for(int row=0;row<12;row++)for(int col=0;col<20;col++)ui.Text(col+","+row,col*75+50,row*75,70,20,14,Color.white);
   Capture("11-source-catalog");
  }
  static void CaptureMap(){var camera=Camera.main;var canvas=Object.FindObjectOfType<Canvas>();canvas.enabled=false;var follow=camera.GetComponent<MeadowCamera>();follow.enabled=false;camera.transform.position=Vector3.back*20;camera.orthographicSize=29;Capture("10-map-overview");canvas.enabled=true;}
  static void Capture(string name){
   var camera=Camera.main;var canvas=Object.FindObjectOfType<Canvas>();var mode=canvas.renderMode;var rt=new RenderTexture(1600,900,24);camera.targetTexture=rt;
   canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=5;Canvas.ForceUpdateCanvases();camera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;
   var png=new Texture2D(1600,900,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,1600,900),0,0);png.Apply();string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/ForestDemo"));Directory.CreateDirectory(folder);File.WriteAllBytes(Path.Combine(folder,name+".png"),png.EncodeToPNG());
   camera.targetTexture=null;canvas.renderMode=mode;RenderTexture.active=previous;Object.Destroy(rt);Object.Destroy(png);Debug.Log("Forest review captured "+name);
  }
  static void Finish(int code){EditorPrefs.SetBool(Key,false);EditorApplication.isPlaying=false;EditorApplication.delayCall+=()=>EditorApplication.Exit(code);}
 }
}
