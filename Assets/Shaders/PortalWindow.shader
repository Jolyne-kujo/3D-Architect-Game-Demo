Shader "Coastal Temple/Portal Window"
{
    Properties
    {
        _PortalTexture("Destination View", 2D) = "black" {}
        _FallbackColor("Recursion Limit / Unpaired", Color) = (.025,.055,.07,1)
        [HideInInspector] _PortalValid("View Available", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+10" }
        Pass
        {
            Name "PortalWindow"
            Tags { "LightMode"="UniversalForward" }
            Cull Off ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_PortalTexture); SAMPLER(sampler_PortalTexture);
            CBUFFER_START(UnityPerMaterial)
                half4 _FallbackColor;
                float _PortalValid;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 screenPosition : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPosition = ComputeScreenPos(output.positionCS);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.screenPosition.xy / input.screenPosition.w;
                half4 destination = SAMPLE_TEXTURE2D(_PortalTexture, sampler_PortalTexture, uv);
                return half4(lerp(_FallbackColor.rgb, destination.rgb, saturate(_PortalValid)), 1);
            }
            ENDHLSL
        }
    }
}
