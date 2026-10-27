using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using UnityEngine;
namespace BoundShoot {
 public class LobbyRealtime:MonoBehaviour {
  struct Frame {public int generation;public string json;}
  ClientWebSocket socket;CancellationTokenSource cancel;int generation;
  readonly ConcurrentQueue<Frame> messages=new ConcurrentQueue<Frame>();
  public Action<RealtimeEvent> OnEvent;
  public bool Connected=>socket!=null&&socket.State==WebSocketState.Open;
  public bool Connecting {get;private set;}
  public async void Connect(string url,string accessToken) {
   Disconnect();cancel=new CancellationTokenSource();var token=cancel.Token;
   int attempt=generation;var current=new ClientWebSocket();socket=current;Connecting=true;
   try{
    using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(token)){
     timeout.CancelAfter(TimeSpan.FromSeconds(10));
     await current.ConnectAsync(new Uri(url.Replace("https://","wss://").Replace("http://","ws://").TrimEnd('/')+"/ws"),timeout.Token);
    }
    var payload=Encoding.UTF8.GetBytes("{\"type\":\"auth\",\"accessToken\":\""+accessToken+"\"}");
    await current.SendAsync(new ArraySegment<byte>(payload),WebSocketMessageType.Text,true,token);
    var buffer=new byte[8192];var text=new StringBuilder();
    while(current.State==WebSocketState.Open&&!token.IsCancellationRequested){
     var r=await current.ReceiveAsync(new ArraySegment<byte>(buffer),token);
     if(r.MessageType==WebSocketMessageType.Close)break;
     if(r.MessageType!=WebSocketMessageType.Text)throw new InvalidOperationException("Unexpected message type");
     text.Append(Encoding.UTF8.GetString(buffer,0,r.Count));
     if(text.Length>65536)throw new InvalidOperationException("Message exceeds limit");
     if(r.EndOfMessage){messages.Enqueue(new Frame{generation=attempt,json=text.ToString()});text.Clear();}
    }
   }catch(Exception){/* HTTP polling remains the fallback when websocket expires. */}
   finally{current.Dispose();if(attempt==generation){socket=null;Connecting=false;}}
  }
  void Update(){
   if(Connected)Connecting=false;
   while(messages.TryDequeue(out var frame)){
    if(frame.generation!=generation)continue;
    try{OnEvent?.Invoke(JsonUtility.FromJson<RealtimeEvent>(frame.json));}catch(Exception e){Debug.LogWarning("Room event rejected: "+e.GetType().Name);}
   }
  }
  public void Disconnect(){generation++;Connecting=false;cancel?.Cancel();socket?.Abort();socket=null;cancel?.Dispose();cancel=null;}
  void OnDestroy(){Disconnect();}
 }
}
