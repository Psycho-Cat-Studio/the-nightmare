Shader "DissolverShader/DissolveShader" {
Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		_NormalMap ("Normal Map", 2D) = "bump" {}
		_NormalStrenght ("Normal Strength", Range(0, 1.5)) = 0.5
		_DissolveMap ("Dissolve Map", 2D) = "white" {}
		_DissolveAmount ("DissolveAmount", Range(0,1)) = 0
		_DissolveColor ("DissolveColor", Color) = (1,1,1,1)
		_DissolveEmission ("DissolveEmission", Range(0,1)) = 1
		_DissolveWidth ("DissolveWidth", Range(0,0.1)) = 0.05
		_Glossiness ("Smoothness", Range(0,1)) = 0.5
		_Metallic ("Metallic", Range(0,1)) = 0.0
	}
	SubShader {
 Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="AlphaTest" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
        TEXTURE2D(_DissolveMap); SAMPLER(sampler_DissolveMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST, _NormalMap_ST, _DissolveMap_ST;
            half4 _Color, _DissolveColor;
            half _NormalStrenght, _DissolveAmount, _DissolveEmission, _DissolveWidth, _Glossiness, _Metallic;
        CBUFFER_END
        struct Attributes {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            half4 tangentWS : TEXCOORD2;
            float2 uv : TEXCOORD3;
            half fog : TEXCOORD4;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings Vert(Attributes v) {
            Varyings o = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_TRANSFER_INSTANCE_ID(v, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
            o.positionCS = p.positionCS;
            o.positionWS = p.positionWS;
            o.normalWS = TransformObjectToWorldNormal(v.normalOS);
            o.tangentWS = half4(TransformObjectToWorldDir(v.tangentOS.xyz), v.tangentOS.w * GetOddNegativeScale());
            o.uv = v.uv;
            o.fog = ComputeFogFactor(p.positionCS.z);
            return o;
        }
        float3 _LightDirection;
        float3 _LightPosition;
        Varyings ShadowVert(Attributes v) {
            Varyings o = Vert(v);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirection = normalize(_LightPosition - o.positionWS);
            #else
                float3 lightDirection = _LightDirection;
            #endif
            o.positionCS = TransformWorldToHClip(ApplyShadowBias(o.positionWS, o.normalWS, lightDirection));
            #if UNITY_REVERSED_Z
                o.positionCS.z = min(o.positionCS.z, UNITY_NEAR_CLIP_VALUE * o.positionCS.w);
            #else
                o.positionCS.z = max(o.positionCS.z, UNITY_NEAR_CLIP_VALUE * o.positionCS.w);
            #endif
            return o;
        }
        half Dissolve(float2 uv) {
            half mask = SAMPLE_TEXTURE2D(_DissolveMap, sampler_DissolveMap, TRANSFORM_TEX(uv, _DissolveMap)).r;
            clip(mask - _DissolveAmount);
            // A white mask must also disappear completely when the animation finishes.
            clip(0.9999h - _DissolveAmount);
            return 1 - step(_DissolveAmount + _DissolveWidth, mask);
        }
        half4 Frag(Varyings i) : SV_Target {
            UNITY_SETUP_INSTANCE_ID(i);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
            half edge = Dissolve(i.uv);
            half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, TRANSFORM_TEX(i.uv, _MainTex)) * _Color;
            half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, TRANSFORM_TEX(i.uv, _NormalMap)), _NormalStrenght);
            half3 n = normalize(i.normalWS);
            half3 t = normalize(i.tangentWS.xyz);
            InputData data = (InputData)0;
            data.positionWS = i.positionWS;
            data.normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, half3x3(t, cross(n,t) * i.tangentWS.w, n)));
            data.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
            data.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
            data.fogCoord = i.fog;
            data.vertexLighting = VertexLighting(i.positionWS, data.normalWS);
            data.bakedGI = SampleSH(data.normalWS);
            data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
            data.shadowMask = half4(1,1,1,1);
            SurfaceData surface = (SurfaceData)0;
            surface.albedo = lerp(albedo.rgb, _DissolveColor.rgb, edge);
            surface.alpha = 1;
            surface.metallic = _Metallic;
            surface.smoothness = _Glossiness;
            surface.normalTS = normalTS;
            surface.occlusion = 1;
            surface.emission = edge * _DissolveColor.rgb * _DissolveEmission;
            half4 color = UniversalFragmentPBR(data, surface);
            color.rgb = MixFog(color.rgb, i.fog);
            return color;
        }
        half4 DepthFrag(Varyings i) : SV_Target { Dissolve(i.uv); return 0; }
        half4 NormalsFrag(Varyings i) : SV_Target { Dissolve(i.uv); return half4(normalize(i.normalWS), 0); }
        ENDHLSL

        Pass {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            ENDHLSL
        }
        Pass {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma vertex ShadowVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass {
            Name "DepthNormalsOnly"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment NormalsFrag
            #pragma multi_compile_instancing
            ENDHLSL
        }
}
Fallback Off
}
