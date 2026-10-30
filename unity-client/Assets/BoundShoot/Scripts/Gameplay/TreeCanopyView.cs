using UnityEngine;
namespace BoundShoot {
 public sealed class TreeCanopyView:MonoBehaviour {
  SpriteRenderer image;Vector3 scale;
  void Start(){image=GetComponent<SpriteRenderer>();scale=transform.localScale;}
  void LateUpdate(){if(image==null)return;float alpha=1;foreach(var p in NetPlayer.All)if((p.transform.position-transform.position).sqrMagnitude<7){alpha=.28f;break;}image.color=new Color(1,1,1,alpha);transform.localScale=scale*(1+Mathf.Sin(Time.time*1.5f+transform.position.x)*.008f);}
 }
}
