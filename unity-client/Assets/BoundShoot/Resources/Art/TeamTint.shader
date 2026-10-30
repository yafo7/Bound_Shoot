Shader "BoundShoot/Team Tint" {
 Properties { [PerRendererData] _MainTex("Sprite",2D)="white" {} _Color("Tint",Color)=(1,1,1,1) _Team("Team",Color)=(0,0.9,1,1) }
 SubShader {
  Tags {"Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True"}
  Cull Off Lighting Off ZWrite Off Blend One OneMinusSrcAlpha
  Pass {
   CGPROGRAM
   #pragma vertex SpriteVert
   #pragma fragment TeamFrag
   #include "UnitySprites.cginc"
   fixed4 _Team;
   fixed4 TeamFrag(v2f i):SV_Target {
    fixed4 c=SampleSpriteTexture(i.texcoord)*i.color;
    float mask=saturate((c.g-c.r)*5)*saturate((c.b-c.r)*5);
    c.rgb=lerp(c.rgb,_Team.rgb*max(c.g,c.b),mask);c.rgb*=c.a;return c;
   }
   ENDCG
  }
 }
}
