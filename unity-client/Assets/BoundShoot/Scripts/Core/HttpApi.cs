using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
namespace BoundShoot {
 public class HttpApi:MonoBehaviour {
  public string BaseUrl="http://127.0.0.1:3000",AccessToken="",RefreshToken="";
  public string Secret="";
  public IEnumerator Request<T>(string method,string path,object body,Action<T> success,Action<string> failure,bool retry=true) {
   using(var req=new UnityWebRequest(BaseUrl.TrimEnd('/')+path,method)){
    if(method!="GET"){req.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(body==null?"{}":JsonUtility.ToJson(body)));}
    req.downloadHandler=new DownloadHandlerBuffer();req.SetRequestHeader("Content-Type","application/json");
    if(!string.IsNullOrEmpty(AccessToken))req.SetRequestHeader("Authorization","Bearer "+AccessToken);
    if(!string.IsNullOrEmpty(Secret))req.SetRequestHeader("X-Match-Secret",Secret);
    req.timeout=12;yield return req.SendWebRequest();
    if(req.responseCode==401&&retry&&!string.IsNullOrEmpty(RefreshToken)&&!path.StartsWith("/api/auth/")){
     AuthData renewed=null;string err=null;
     yield return Request<AuthData>("POST","/api/auth/refresh",new RefreshBody{refreshToken=RefreshToken},v=>renewed=v,e=>err=e,false);
     if(renewed!=null){SetTokens(renewed);yield return Request(method,path,body,success,failure,false);yield break;}
     AccessToken="";RefreshToken="";PlayerPrefs.DeleteKey(CredentialKey);failure?.Invoke(err??"登录已过期，请重新登录");yield break;
    }
    Envelope<T> envelope=null;
    try{envelope=JsonUtility.FromJson<Envelope<T>>(req.downloadHandler.text);}catch{}
    if(req.result==UnityWebRequest.Result.Success&&envelope!=null&&envelope.ok)success?.Invoke(envelope.data);
    else failure?.Invoke(envelope?.error?.message??"无法连接服务器，请检查地址和后端状态");
   }
  }
  public bool Remember;
  public string CredentialKey=>"BoundRefresh:"+BaseUrl.TrimEnd('/');
  public void SetTokens(AuthData data){
   AccessToken=data.accessToken;RefreshToken=data.refreshToken;
   if(Remember)PlayerPrefs.SetString(CredentialKey,RefreshToken);
  }
  public void Forget(){AccessToken="";RefreshToken="";PlayerPrefs.DeleteKey(CredentialKey);PlayerPrefs.Save();}
 }
}
