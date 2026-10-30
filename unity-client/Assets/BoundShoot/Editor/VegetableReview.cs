using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace BoundShoot.Editor {
 // Editor-only deterministic UI/scene render review; never part of the game build.
 [InitializeOnLoad] public static class VegetableReview {
  static int stage;static double at,deadline;
  static VegetableReview(){bool review=System.Environment.GetCommandLineArgs().Any(a=>a=="BoundShoot.Editor.VegetableReview.Start");EditorPrefs.SetBool("BoundShoot.Review",review);if(review)Draw2D.RenderInBatch=true;EditorApplication.update+=Tick;}
  public static void Start(){
   EditorPrefs.SetBool("BoundShoot.Review",true);EditorSceneManager.OpenScene("Assets/BoundShoot/Scenes/Boot.unity");EditorApplication.isPlaying=true;
  }
  static void Tick(){try{Step();}catch(System.Exception error){EditorPrefs.SetBool("BoundShoot.Review",false);Debug.LogException(error);Finish(2);}}
  static void Step(){
   if(!EditorPrefs.GetBool("BoundShoot.Review",false)||!EditorApplication.isPlaying)return;
   if(deadline==0)deadline=EditorApplication.timeSinceStartup+90;
   if(EditorApplication.timeSinceStartup>deadline){Finish(2);return;}
   if(EditorApplication.timeSinceStartup<at||GameClient.Instance==null)return;
   var client=GameClient.Instance;
   if(stage==0){client.EnterName("yafo777");client.SelectCharacter(6);stage=1;at=EditorApplication.timeSinceStartup+.5;}
   else if(stage==1){Capture("01-character-menu");client.StartTraining();stage=2;}
   else if(stage==2&&client.Screen=="Training"){
    var room=TrainingRuntime.Instance;room.AddTeammate();room.AddTeammate();room.AddTeammate();
    var player=room.World.Players.Single(p=>p.TrainingRole==0);player.Equip(WeaponKind.AK);
    stage=3;at=EditorApplication.timeSinceStartup+1;
   }
   else if(stage==3){Capture("02-training");client.LeaveTraining();stage=4;}
   else if(stage==4&&client.Screen=="Menu"&&TrainingRuntime.Instance==null&&!Bootstrap.Network.IsListening){client.LocalTeamSize=4;client.StartLocalMatch();stage=5;}
   else if(stage==5&&client.Screen=="LocalRoom"){stage=51;at=EditorApplication.timeSinceStartup+.5;}
   else if(stage==51){Capture("03-local-room");LocalMatchRuntime.Instance.StartRound();stage=6;}
   else if(stage==6&&LocalMatchRuntime.Instance.World.Phase.Value==2){
    var world=LocalMatchRuntime.Instance.World;
    foreach(var p in world.Players){p.Body.position=new Vector2(p.Team.Value==0?-10:10,-3+p.Seat.Value*2);p.transform.position=p.Body.position;p.Equip((WeaponKind)(p.Seat.Value%3+1));world.ResetRopesForTeleport(p);}
    stage=7;at=EditorApplication.timeSinceStartup+1;
   }
   else if(stage==7){Capture("04-local-battle");foreach(var p in LocalMatchRuntime.Instance.World.Players.Where(p=>p.Team.Value==1)){p.ProtectionUntil=0;p.Hurt(999,null);}stage=8;at=EditorApplication.timeSinceStartup+.5;}
   else if(stage==8&&client.Screen=="LocalResult"){Capture("05-result");Finish(0);}
  }
  static void Capture(string name){
   var camera=Camera.main;var canvas=Object.FindObjectOfType<Canvas>();var originalMode=canvas.renderMode;
   var rt=new RenderTexture(1600,900,24);camera.targetTexture=rt;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=5;Canvas.ForceUpdateCanvases();
   camera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();
   string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/VegetableDemo"));Directory.CreateDirectory(folder);File.WriteAllBytes(Path.Combine(folder,name+".png"),image.EncodeToPNG());
   camera.targetTexture=null;canvas.renderMode=originalMode;RenderTexture.active=previous;Object.Destroy(rt);Object.Destroy(image);Debug.Log("Demo review captured "+name);
  }
  static void Finish(int code){EditorPrefs.SetBool("BoundShoot.Review",false);EditorApplication.isPlaying=false;EditorApplication.delayCall+=()=>EditorApplication.Exit(code);}
 }
}
