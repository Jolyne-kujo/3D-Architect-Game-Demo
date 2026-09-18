Shader "CoastalTemple/Ocean"
{
 Properties
 {
  _Shallow("Shallow",Color)=(.09,.53,.48,1)
  _Deep("Deep",Color)=(.022,.16,.23,1)
  _NearBounds("Simulated rectangle XZ",Vector)=(-144,-88,144,256)
  _WaveSettings("Height speed wavelength seam fade",Vector)=(1,1,1,12)
  _RippleNormal("Fine ripple normal",2D)="gray"{}
 }
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent-20" "RenderType"="Transparent"}
  Pass
  {
   Tags{"LightMode"="UniversalForward"}
   Blend One Zero
   ZWrite Off
   Cull Off
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
   CBUFFER_START(UnityPerMaterial)
    half4 _Shallow,_Deep;
    float4 _NearBounds;
    float4 _WaveSettings,_Wave0,_Wave1,_Wave2,_Wave3,_WavePhases;
    float _WaveTime;
   CBUFFER_END
   TEXTURE2D(_RippleNormal);SAMPLER(sampler_RippleNormal);
   struct A{float4 p:POSITION;float3 n:NORMAL;float4 water:TEXCOORD1;};
   struct V{float4 p:SV_POSITION;float3 world:TEXCOORD0;float3 n:TEXCOORD1;float4 screen:TEXCOORD2;float4 water:TEXCOORD3;float eye:TEXCOORD4;half crest:TEXCOORD5;};
   void AddWave(float4 wave,float phase,float2 p,inout float h,inout float2 slope)
   {
    float k=wave.w/max(.25,_WaveSettings.z);
    float theta=k*dot(wave.xy,p)-sqrt(9.81*k)*_WaveTime*_WaveSettings.y+phase;
    float s,c;sincos(theta,s,c);h+=wave.z*s;slope+=wave.z*k*c*wave.xy;
   }
   V Vert(A a)
   {
    V o;o.world=TransformObjectToWorld(a.p.xyz);
    float height=0;float2 slope=0;float2 p=o.world.xz;
    AddWave(_Wave0,_WavePhases.x,p,height,slope);AddWave(_Wave1,_WavePhases.y,p,height,slope);
    AddWave(_Wave2,_WavePhases.z,p,height,slope);AddWave(_Wave3,_WavePhases.w,p,height,slope);
    float edge=min(min(p.x-_NearBounds.x,_NearBounds.z-p.x),min(p.y-_NearBounds.y,_NearBounds.w-p.y));
    float envelope=smoothstep(.1,2,a.water.x)*smoothstep(0,max(.01,_WaveSettings.w),edge)*_WaveSettings.x;
    height*=envelope;slope*=envelope;o.world.y+=height;
    o.p=TransformWorldToHClip(o.world);o.n=normalize(TransformObjectToWorldNormal(a.n)+float3(-slope.x,0,-slope.y));
    o.screen=ComputeScreenPos(o.p);o.water=a.water;o.eye=-TransformWorldToView(o.world).z;o.crest=height;return o;
   }
   half4 Frag(V i):SV_Target
   {
    clip(i.water.x-.012);
    float2 uv=i.screen.xy/i.screen.w;
    float2 p=i.world.xz;float t=_Time.y;
    float2 rippleA=SAMPLE_TEXTURE2D(_RippleNormal,sampler_RippleNormal,p*.067+float2(t*.012,t*.019)).rg*2-1;
    float2 rippleB=SAMPLE_TEXTURE2D(_RippleNormal,sampler_RippleNormal,float2(p.x*.8+p.y*.6,p.y*.8-p.x*.6)*.091+float2(-t*.021,t*.013)).rg*2-1;
    float2 waves=(rippleA+rippleB)*.12*lerp(1,.2,saturate(distance(_WorldSpaceCameraPos,i.world)/450));
    float3 n=normalize(i.n+float3(waves.x,0,waves.y));
    float3 v=SafeNormalize(_WorldSpaceCameraPos-i.world);if(v.y<0)n=-n;
    float thickness=max(0,LinearEyeDepth(SampleSceneDepth(uv),_ZBufferParams)-i.eye);
    float2 refrUV=saturate(uv+TransformWorldToViewDir(n).xy*.013*saturate(thickness));
    if(LinearEyeDepth(SampleSceneDepth(refrUV),_ZBufferParams)<i.eye)refrUV=uv;
    half3 behind=SampleSceneColor(refrUV);
    half3 tint=lerp(_Shallow.rgb,_Deep.rgb,saturate(i.water.x/3));
    half3 transmission=exp(-min(thickness,35)*half3(.42,.13,.105));
    transmission*=1-smoothstep(4,8,thickness);
    half3 color=behind*transmission+tint*(1-transmission)*.72;
    float fresnel=pow(1-saturate(dot(n,v)),4);
    half3 sky=lerp(half3(.22,.42,.57),half3(.45,.64,.76),saturate(n.y));
    color=lerp(color,sky,.05+fresnel*.50);
    Light sun=GetMainLight();
    color+=pow(saturate(dot(n,SafeNormalize(sun.direction+v))),260)*sun.color*.55;
    float shore=saturate(1-thickness/.75);
    float lace=smoothstep(-.2,.4,rippleA.x+rippleB.y);
    color=lerp(color,half3(.81,.89,.85),shore*lace*.4);
    color=lerp(color,half3(.68,.82,.81),smoothstep(.34,.58,i.crest)*lace*.2);
    float seam=min(min(abs(p.x-_NearBounds.x),abs(p.x-_NearBounds.z)),min(abs(p.y-_NearBounds.y),abs(p.y-_NearBounds.w)));
    color=lerp(color,half3(.56,.7,.73),saturate(1-seam/.32)*.12);
    float fog=saturate((distance(_WorldSpaceCameraPos,i.world)-260)/1240);
    return half4(lerp(color,unity_FogColor.rgb,fog),1);
   }
   ENDHLSL
  }
 }
}
