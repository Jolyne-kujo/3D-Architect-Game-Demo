Shader "Coastal Temple/Portal Clipped Lit"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1,1,1,1)
        _Metallic("Metallic", Range(0,1)) = 0
        _Smoothness("Smoothness", Range(0,1)) = .5
        _MetallicGlossMap("Metallic Map", 2D) = "white" {}
        _SpecGlossMap("Specular Map", 2D) = "white" {}
        _SpecColor("Specular Color", Color) = (.2,.2,.2,1)
        _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Float) = 1
        _OcclusionMap("Occlusion", 2D) = "white" {}
        _OcclusionStrength("Occlusion Strength", Range(0,1)) = 1
        _EmissionMap("Emission", 2D) = "white" {}
        _EmissionColor("Emission Color", Color) = (0,0,0,0)
        _Cutoff("Alpha Cutoff", Range(0,1)) = .5
        _Cull("Cull", Float) = 2
        [HideInInspector] _Surface("Surface", Float) = 0
        [HideInInspector] _PortalClipPlane("Clip Plane", Vector) = (0,0,1,0)
        [HideInInspector] _PortalClipEnabled("Clipping", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
        float4 _PortalClipPlane;
        float _PortalClipEnabled;
        void SlicePortal(float3 positionWS)
        {
            clip(lerp(1.0, dot(float4(positionWS, 1), _PortalClipPlane), _PortalClipEnabled));
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull [_Cull] ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex LitPassVertex
            #pragma fragment PortalLitFragment
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma shader_feature_local_fragment _SPECULAR_SETUP
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #define REQUIRES_WORLD_SPACE_POS_INTERPOLATOR
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            half4 PortalLitFragment(Varyings input) : SV_Target
            {
                SlicePortal(input.positionWS);
                half4 color;
                LitPassFragment(input, color);
                return color;
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            Cull [_Cull] ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex SliceShadowVertex
            #pragma fragment SliceShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;
            struct Input { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Output { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float2 uv:TEXCOORD1; };
            Output SliceShadowVertex(Input input)
            {
                Output output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 lightDirection = _LightDirection;
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                lightDirection = normalize(_LightPosition - output.positionWS);
                #endif
                output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(output.positionWS, normalWS, lightDirection)));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }
            half4 SliceShadowFragment(Output input) : SV_Target
            {
                SlicePortal(input.positionWS);
                #if defined(_ALPHATEST_ON)
                Alpha(SampleAlbedoAlpha(input.uv,TEXTURE2D_ARGS(_BaseMap,sampler_BaseMap)).a,_BaseColor,_Cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            Cull [_Cull] ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex SliceDepthVertex
            #pragma fragment SliceDepthFragment
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            struct Input { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Output { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float2 uv:TEXCOORD1; };
            Output SliceDepthVertex(Input input)
            {
                Output output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }
            half SliceDepthFragment(Output input) : SV_Target
            {
                SlicePortal(input.positionWS);
                #if defined(_ALPHATEST_ON)
                Alpha(SampleAlbedoAlpha(input.uv,TEXTURE2D_ARGS(_BaseMap,sampler_BaseMap)).a,_BaseColor,_Cutoff);
                #endif
                return input.positionCS.z;
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            Cull [_Cull] ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex SliceNormalsVertex
            #pragma fragment SliceNormalsFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            struct Input { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Output { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; };
            Output SliceNormalsVertex(Input input)
            {
                Output output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half4 SliceNormalsFragment(Output input) : SV_Target
            {
                SlicePortal(input.positionWS);
                float3 normal = normalize(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                float2 packed = saturate(PackNormalOctQuadEncode(normal) * .5 + .5);
                return half4(PackFloat2To888(packed), 0);
                #else
                return half4(normal, 0);
                #endif
            }
            ENDHLSL
        }
    }
}
