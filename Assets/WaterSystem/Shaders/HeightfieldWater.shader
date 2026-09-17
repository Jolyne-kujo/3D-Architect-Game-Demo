Shader "Courtyard/Heightfield Water"
{
 Properties { _Shallow("Shallow water",Color)=(.14,.54,.56,1) _Deep("Deep water",Color)=(.025,.20,.24,1) _ShowFlow("Flow overlay",Float)=0 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent" }
  Pass
  {
   Name "Water"
   Tags { "LightMode"="UniversalForward" }
   Blend One Zero
   ZWrite Off
   Cull Off
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
   CBUFFER_START(UnityPerMaterial)
    half4 _Shallow, _Deep;
    float _ShowFlow;
   CBUFFER_END
   struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;float4 water:TEXCOORD1;};
   struct V {float4 pos:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float4 screen:TEXCOORD2;float4 water:TEXCOORD3;float eye:TEXCOORD4;};
   V Vert(A a){V o;o.world=TransformObjectToWorld(a.positionOS.xyz);o.pos=TransformWorldToHClip(o.world);o.screen=ComputeScreenPos(o.pos);o.normal=TransformObjectToWorldNormal(a.normalOS);o.water=a.water;o.eye=-TransformWorldToView(o.world).z;return o;}
   half4 Frag(V i):SV_Target
   {
    clip(i.water.x-.008);
    float2 uv=i.screen.xy/i.screen.w;
    float t=_Time.y;
    float2 p=i.world.xz;
    // Small normal ripples decorate the simulated geometry; they never drive physics.
    float2 drift=i.water.yz;
    float2 ripple=float2(sin(p.x*5.7+p.y*3.8+t*1.25),cos(p.x*3.2-p.y*6.1+t*1.55))*.026;
    float3 n=normalize(i.normal+float3(ripple.x,0,ripple.y));
    float3 view=SafeNormalize(_WorldSpaceCameraPos-i.world);
    if(view.y<0)n=-n;
    float raw=SampleSceneDepth(uv);
    float thickness=max(0,LinearEyeDepth(raw,_ZBufferParams)-i.eye);
    float2 offset=TransformWorldToViewDir(n).xy*.013*saturate(thickness);
    float2 refrUV=saturate(uv+offset);
    // Avoid refracting foreground silhouettes across the waterline.
    if(LinearEyeDepth(SampleSceneDepth(refrUV),_ZBufferParams)<i.eye)refrUV=uv;
    half3 behind=SampleSceneColor(refrUV);
    // Column depth provides a stable minimum optical tint at foreground depth discontinuities.
    float opticalDepth=max(thickness,i.water.x*.65);
    half3 tint=lerp(_Shallow.rgb,_Deep.rgb,saturate(opticalDepth*.14));
    half3 transmission=exp(-opticalDepth*half3(.65,.18,.115));
    half3 color=behind*transmission+tint*(1-transmission)*.3;
    Light light=GetMainLight(TransformWorldToShadowCoord(i.world));
    float fresnel=pow(1-saturate(dot(n,view)),5);
    half3 sky=lerp(half3(.48,.67,.75),half3(.73,.83,.85),saturate(n.y));
    color=lerp(color,sky,.10+fresnel*.43);
    float spec=pow(saturate(dot(n,SafeNormalize(light.direction+view))),160)*.55;
    color+=spec*light.color*light.shadowAttenuation;
    float foam=saturate(1-i.water.x*10)*.28;
    float speed=length(drift);
    float streak=pow(saturate(sin(dot(p,normalize(drift+float2(.001,0)))*18-t*max(.5,speed*12))),18);
    color+=half3(.63,.84,.82)*streak*saturate(speed*.5)*.07;
    color=lerp(color,half3(.83,.91,.87),foam);
    float stripe=step(.82,frac(dot(p,normalize(drift+float2(.001,0)))*2-t*speed));
    color=lerp(color,lerp(half3(.035,.23,.8),half3(1,.28,.045),saturate(speed*.6)),_ShowFlow*.72);
    color+=_ShowFlow*stripe*saturate(speed)*.24;
    return half4(color,1);
   }
   ENDHLSL
  }
 }
}
