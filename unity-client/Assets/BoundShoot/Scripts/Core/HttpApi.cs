using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
namespace BoundShoot {
 public class HttpApi:MonoBehaviour {
  public string BaseUrl="http://127.0.0.1:3000",AccessToken="",RefreshToken="";
  public string Secret="";
  bool refreshing;int credentialVersion;
  public event Action SessionExpired;
  public IEnumerator Request<T>(string method,string path,object body,Action<T> success,Action<string> failure,bool retry=true) {
   string tokenUsed=AccessToken;
   using(var req=new UnityWebRequest(BaseUrl.TrimEnd('/')+path,method)){
    if(method!="GET"){req.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(body==null?"{}":JsonUtility.ToJson(body)));}
    req.downloadHandler=new DownloadHandlerBuffer();req.SetRequestHeader("Content-Type","application/json");
    if(!string.IsNullOrEmpty(AccessToken))req.SetRequestHeader("Authorization","Bearer "+AccessToken);
    if(!string.IsNullOrEmpty(Secret))req.SetRequestHeader("X-Match-Secret",Secret);
    req.timeout=12;yield return req.SendWebRequest();
    if(req.responseCode==401&&retry&&!string.IsNullOrEmpty(RefreshToken)&&!path.StartsWith("/api/auth/")){
     while(refreshing)yield return null;
     if(AccessToken==tokenUsed&&!string.IsNullOrEmpty(RefreshToken)){
      refreshing=true;int version=credentialVersion;AuthData renewed=null;
      try{
       yield return Request<AuthData>("POST","/api/auth/refresh",new RefreshBody{refreshToken=RefreshToken},v=>renewed=v,e=>{},false);
       if(version==credentialVersion){
        if(renewed!=null)SetTokens(renewed);else {Forget();SessionExpired?.Invoke();}
       }
      }finally{refreshing=false;}
     }
     if(!string.IsNullOrEmpty(AccessToken)&&AccessToken!=tokenUsed){yield return Request(method,path,body,success,failure,false);yield break;}
     failure?.Invoke("请重新登录");yield break;
    }
    Envelope<T> envelope=null;
    if(req.responseCode==401&&!string.IsNullOrEmpty(tokenUsed)&&!path.StartsWith("/api/auth/")){Forget();SessionExpired?.Invoke();}
    try{envelope=JsonUtility.FromJson<Envelope<T>>(req.downloadHandler.text);}catch{}
    if(req.result==UnityWebRequest.Result.Success&&envelope!=null&&envelope.ok)success?.Invoke(envelope.data);
    else failure?.Invoke(envelope?.error?.message??"连接失败，请重试");
   }
  }
  public bool Remember;
  public string CredentialKey=>"BoundRefresh:"+BaseUrl.TrimEnd('/');
  public void SetTokens(AuthData data){
   credentialVersion++;
   AccessToken=data.accessToken;RefreshToken=data.refreshToken;
   if(Remember)PlayerPrefs.SetString(CredentialKey,RefreshToken);else PlayerPrefs.DeleteKey(CredentialKey);
   PlayerPrefs.Save();
  }
  public void Forget(){credentialVersion++;AccessToken="";RefreshToken="";PlayerPrefs.DeleteKey(CredentialKey);PlayerPrefs.Save();}
 }
}
