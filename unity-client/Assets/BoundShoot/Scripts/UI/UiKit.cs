using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace BoundShoot {
 public class UiKit:MonoBehaviour {
  public RectTransform Root;public Font Font;
  public static readonly Color Ink=new Color(.055f,.08f,.12f),Panel=new Color(.10f,.14f,.20f),Soft=new Color(.58f,.67f,.75f),White=new Color(.93f,.95f,.96f);
  public void Initialize(){
   Font=UnityEngine.Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","Microsoft YaHei UI","Noto Sans CJK SC","Arial"},24);
   var canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;
   var scaler=gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
   gameObject.AddComponent<GraphicRaycaster>();Root=GetComponent<RectTransform>();
   if(FindObjectOfType<EventSystem>()==null)new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
  }
  public void Clear(){foreach(Transform child in transform)Destroy(child.gameObject);}
  public void Backdrop(Color color){
   var go=new GameObject("Background",typeof(RectTransform),typeof(Image));var r=go.GetComponent<RectTransform>();r.SetParent(Root,false);
   r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;go.GetComponent<Image>().color=color;
  }
  RectTransform Rect(string name,Transform parent,float x,float y,float w,float h){
   var go=new GameObject(name,typeof(RectTransform));var r=go.GetComponent<RectTransform>();r.SetParent(parent,false);
   r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;
  }
  public RectTransform Box(float x,float y,float w,float h,Color color,Transform parent=null){
   var r=Rect("Panel",parent??Root,x,y,w,h);r.gameObject.AddComponent<Image>().color=color;return r;
  }
  public Text Text(string text,float x,float y,float w,float h,int size=24,Color? color=null,Transform parent=null,TextAnchor align=TextAnchor.MiddleLeft){
   var r=Rect("Text",parent??Root,x,y,w,h);var t=r.gameObject.AddComponent<Text>();t.font=Font;t.text=text;t.fontSize=size;t.color=color??White;t.alignment=align;t.raycastTarget=false;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;return t;
  }
  public Button Button(string title,float x,float y,float w,float h,Action action,Color? color=null,Transform parent=null){
   var r=Box(x,y,w,h,color??Draw2D.Gold,parent);var b=r.gameObject.AddComponent<Button>();
   var c=b.colors;c.highlightedColor=new Color(1,1,1,.88f);c.pressedColor=new Color(.75f,.8f,.85f);b.colors=c;b.onClick.AddListener(()=>action());
   Text(title,12,0,w-24,h,23,Ink,r,TextAnchor.MiddleCenter);return b;
  }
  public InputField Field(string hint,float x,float y,float w,bool password=false,string value=""){
   var r=Box(x,y,w,58,new Color(.15f,.2f,.27f));
   var input=r.gameObject.AddComponent<InputField>();input.textComponent=Text("",16,0,w-32,58,23,White,r);
   input.placeholder=Text(hint,16,0,w-32,58,23,Soft,r);input.contentType=password?InputField.ContentType.Password:InputField.ContentType.Standard;
   input.text=value;input.characterLimit=password?72:128;return input;
  }
 }
}
