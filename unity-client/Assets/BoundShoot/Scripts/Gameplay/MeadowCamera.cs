using System.Linq;
using UnityEngine;
namespace BoundShoot {
 [RequireComponent(typeof(Camera))] public sealed class MeadowCamera:MonoBehaviour {
  public static MeadowCamera Instance;public float Shake;Camera cameraView;
  void Awake(){Instance=this;cameraView=GetComponent<Camera>();}
  public void Impulse(float strength){Shake=Mathf.Max(Shake,strength);}
  void LateUpdate(){
   var world=BattleWorld.Instance;
   var player=NetPlayer.All.FirstOrDefault(p=>p.IsOwner&&p.IsClient&&!p.IsBot.Value&&p.TrainingRole==0);
   if(world==null||player==null)return;var map=world.MapOverride??GameConfig.Current.Map(world.MapKey.Value.ToString());if(map==null)return;
   cameraView.orthographicSize=8.5f;
   float halfX=cameraView.orthographicSize*cameraView.aspect;
   Vector3 target=player.transform.position;
   if(Application.isFocused){Vector3 pointer=cameraView.ScreenToWorldPoint(Input.mousePosition);target+=(Vector3)Vector2.ClampMagnitude((pointer-player.transform.position)*.08f,1.2f);}
   target.x=map.halfWidth<=halfX?0:Mathf.Clamp(target.x,-map.halfWidth+halfX,map.halfWidth-halfX);
   target.y=map.halfHeight<=8.5f?0:Mathf.Clamp(target.y,-map.halfHeight+8.5f,map.halfHeight-8.5f);target.z=-20;
   var position=Vector3.Lerp(transform.position,target,1-Mathf.Exp(-9*Time.deltaTime));
   Shake=Mathf.MoveTowards(Shake,0,Time.deltaTime*2);position+=(Vector3)(UnityEngine.Random.insideUnitCircle*Shake*(PlayerPrefs.GetInt("BoundShoot.Shake",1)==1?1:0));
   transform.position=new Vector3(Mathf.Round(position.x*48)/48,Mathf.Round(position.y*48)/48,-20);
  }
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
