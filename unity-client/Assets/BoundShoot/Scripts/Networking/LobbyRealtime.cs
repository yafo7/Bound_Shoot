using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
namespace BoundShoot {
 // Receive off-thread; Unity objects and callbacks are touched only in Update.
 public class LobbyRealtime:MonoBehaviour {
  ClientWebSocket socket;CancellationTokenSource cancel;
  readonly ConcurrentQueue<string> messages=new ConcurrentQueue<string>();
  public Action<RealtimeEvent> OnEvent;
  public async void Connect(string url,string accessToken) {
   Disconnect();cancel=new CancellationTokenSource();var token=cancel.Token;
   var current=new ClientWebSocket();socket=current;
   try{
    await current.ConnectAsync(new Uri(url.Replace("https://","wss://").Replace("http://","ws://").TrimEnd('/')+"/ws"),token);
    var payload=Encoding.UTF8.GetBytes("{\"type\":\"auth\",\"accessToken\":\""+accessToken+"\"}");
    await current.SendAsync(new ArraySegment<byte>(payload),WebSocketMessageType.Text,true,token);
    var buffer=new byte[65536];var text=new StringBuilder();
    while(current.State==WebSocketState.Open&&!token.IsCancellationRequested){
     var r=await current.ReceiveAsync(new ArraySegment<byte>(buffer),token);
     if(r.MessageType==WebSocketMessageType.Close)break;
     text.Append(Encoding.UTF8.GetString(buffer,0,r.Count));
     if(text.Length>65536)break;
     if(r.EndOfMessage){messages.Enqueue(text.ToString());text.Clear();}
    }
   }catch(Exception){/* HTTP polling remains the fallback when websocket expires. */}
   finally{current.Dispose();}
  }
  void Update(){while(messages.TryDequeue(out var json)){try{OnEvent?.Invoke(JsonUtility.FromJson<RealtimeEvent>(json));}catch{}}}
  public void Disconnect(){cancel?.Cancel();cancel?.Dispose();cancel=null;socket?.Abort();socket=null;}
  void OnDestroy(){Disconnect();}
 }
}
