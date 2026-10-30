using System.Linq;
using UnityEngine;
namespace BoundShoot {
 [DefaultExecutionOrder(200),RequireComponent(typeof(Camera))] public sealed class MeadowCamera:MonoBehaviour {
  public static MeadowCamera Instance;public float Shake;Camera cameraView;
  void Awake(){Instance=this;cameraView=GetComponent<Camera>();}
  public void Impulse(float strength){} // The local character remains exactly centred.
  void LateUpdate(){
   var world=BattleWorld.Instance;
   var player=NetPlayer.All.FirstOrDefault(p=>p.IsOwner&&p.IsClient&&!p.IsBot.Value&&p.TrainingRole==0);
   if(world==null||player==null)return;var map=world.MapOverride??GameConfig.Current.Map(world.MapKey.Value.ToString());if(map==null)return;
   cameraView.orthographicSize=8.5f;
   transform.position=Anchor(player)+Vector3.back*20;
  }
  public static Vector3 Anchor(NetPlayer player)=>player.transform.position+Vector3.up*.25f;
  void OnGUI(){
   if(Application.isBatchMode||BattleWorld.Instance==null)return;
   var me=NetPlayer.All.FirstOrDefault(p=>p.IsOwner&&!p.IsBot.Value&&p.TrainingRole==0);if(me==null)return;
   foreach(var p in NetPlayer.All.Where(p=>p!=me&&p.Team.Value==me.Team.Value)){
    var point=cameraView.WorldToViewportPoint(p.transform.position);if(point.x>.06f&&point.x<.94f&&point.y>.15f&&point.y<.88f)continue;
    float x=Mathf.Clamp(point.x,.06f,.94f)*Screen.width,y=(1-Mathf.Clamp(point.y,.15f,.88f))*Screen.height;
    GUI.color=p.Alive?(me.Team.Value==0?Draw2D.Blue:Draw2D.Red):Color.gray;GUI.Label(new Rect(x-45,y-10,120,30),"◆ "+p.DisplayName.Value.ToString());
   }GUI.color=Color.white;
  }
 }
}
