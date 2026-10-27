using System;
using UnityEngine;
namespace BoundShoot {
 [CreateAssetMenu(menuName="Bound Shoot/Network Settings")]
 public sealed class NetworkSettings:ScriptableObject {
  public const string PublicBackendUrl="http://134.175.153.218:3000";
  [SerializeField] string backendUrl=PublicBackendUrl;
  [SerializeField,Min(5)] int connectTimeoutSeconds=20;
  [SerializeField,Range(1000,30000)] int transportDisconnectMs=5000;
  [SerializeField,Range(0,500)] int interpolationMs=100;
  [SerializeField,Range(0,250)] int extrapolationMs=100;
  public string BackendUrl {
   get {
    var value=(backendUrl??"").Trim().TrimEnd('/');
    if(!Uri.TryCreate(value,UriKind.Absolute,out var uri)||(uri.Scheme!="http"&&uri.Scheme!="https"))
     throw new InvalidOperationException("Set a valid Backend URL in Resources/NetworkSettings.");
    return value;
   }
  }
  public int ConnectTimeoutSeconds=>Mathf.Max(5,connectTimeoutSeconds);
  public int TransportDisconnectMs=>Mathf.Clamp(transportDisconnectMs,1000,30000);
  public double InterpolationSeconds=>Mathf.Clamp(interpolationMs,0,500)/1000.0;
  public double ExtrapolationSeconds=>Mathf.Clamp(extrapolationMs,0,250)/1000.0;
  static NetworkSettings current;
  public static NetworkSettings Current {
   get {
    if(current==null)current=Resources.Load<NetworkSettings>("NetworkSettings");
    if(current==null)throw new InvalidOperationException("Missing Resources/NetworkSettings.asset.");
    return current;
   }
  }
 }
}
