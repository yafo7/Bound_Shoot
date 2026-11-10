using UnityEngine;
namespace BoundShoot {
 public sealed class GardenFx:MonoBehaviour {
  sealed class Effect {public SpriteRenderer image;public int first,count;public float start,duration;public Vector2 size;}
  static GardenFx instance;readonly Effect[] pool=new Effect[64];AudioSource audioSource;readonly AudioClip[] sounds=new AudioClip[10];int cursor;
  static GardenFx Current {get{if(Application.isBatchMode&&!Draw2D.RenderInBatch)return null;if(instance==null){var go=new GameObject("Garden feedback");DontDestroyOnLoad(go);instance=go.AddComponent<GardenFx>();instance.Setup();}return instance;}}
  void Setup(){
   for(int i=0;i<pool.Length;i++){var r=CyberArt.Place(transform,"fx0",Vector2.zero,Vector2.one,30);r.enabled=false;pool[i]=new Effect{image=r};}
   audioSource=gameObject.AddComponent<AudioSource>();audioSource.spatialBlend=0;
   for(int k=0;k<sounds.Length;k++){
    int samples=k==7?19845:k==8?13230:k==4?11025:6615;var data=new float[samples];uint noise=1234;
    for(int i=0;i<samples;i++){float t=i/44100f,e=Mathf.Exp(-t*(k==4?18:42));noise=noise*1664525+1013904223;float n=(noise%65536)/32768f-1;
     float freq=k==5?650:k==6?950:k==4?70:180+k*120;
     if(k>=7){
      float attack=Mathf.Min(1,t*250),tail=Mathf.Exp(-t*(k==7?9:14));
      float phase=2*Mathf.PI*(k==7?155*t-120*t*t:k==8?520*t+880*t*t:860*t+1200*t*t);
      data[i]=attack*tail*(.5f*Mathf.Sin(phase)+.2f*Mathf.Sin(phase*2)+(k==7?.3f*n*Mathf.Exp(-t*25):0));
     }else data[i]=(Mathf.Sin(t*freq*Mathf.PI*2)*.3f+n*(k==5||k==6?.05f:.45f))*e;
    }
    sounds[k]=AudioClip.Create("BoundShoot feedback "+k,samples,1,44100,false);sounds[k].SetData(data,0);
   }
  }
  public static void Emit(Vector2 position,int first,int count,float duration,Vector2 size,float angle=0){
   var fx=Current;if(fx==null)return;var e=fx.pool[fx.cursor++%fx.pool.Length];e.first=first;e.count=count;e.start=Time.time;e.duration=duration;e.size=size;
   e.image.transform.position=position;e.image.transform.rotation=Quaternion.Euler(0,0,angle);e.image.color=Color.white;e.image.enabled=true;CyberArt.Set(e.image,"fx"+first,size,true);
  }
  public static void Tone(int kind,float volume,Vector2 position){
   float gain=PlayerPrefs.GetFloat("BoundShoot.EffectsVolume",PlayerPrefs.GetInt("BoundShoot.Sound",1));
   var fx=Current;
   if(fx==null||gain<=0)return;
   float distance=Camera.main==null?0:Vector2.Distance(Camera.main.transform.position,position);
   if(distance<=28)fx.audioSource.PlayOneShot(fx.sounds[Mathf.Clamp(kind,0,fx.sounds.Length-1)],volume*gain*Mathf.Clamp01(1-distance/32));
  }
  void Update(){foreach(var e in pool){if(!e.image.enabled)continue;float t=(Time.time-e.start)/e.duration;if(t>=1){e.image.enabled=false;continue;}int frame=e.first+Mathf.Min(e.count-1,Mathf.FloorToInt(t*e.count));CyberArt.Set(e.image,"fx"+frame,e.size,true);e.image.color=new Color(1,1,1,Mathf.Min(1,(1-t)*3));}}
  void OnDestroy(){if(instance==this)instance=null;foreach(var clip in sounds)if(clip!=null)Destroy(clip);}
 }
}
