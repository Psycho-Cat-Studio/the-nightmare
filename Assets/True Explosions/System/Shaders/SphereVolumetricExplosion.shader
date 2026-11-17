// Original effect by Steven Craeynest; URP port preserves displacement and ramp animation.
Shader "Custom/Explosion" {
Properties
    {
        _RampTex ("Color Ramp", 2D) = "white" {}
        _DispTex ("Displacement Texture", 2D) = "gray" {}
        _Displacement ("Displacement", Range(0, 1.0)) = 0.1
        _ChannelFactor ("ChannelFactor (r,g,b)", Vector) = (1,0,0)
        _Range ("Range (min,max)", Vector) = (0,0.5,0)
        _ClipRange ("ClipRange [0,1]", float) = 0.8
    }

    SubShader {
 Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
 Cull Off
 Pass {
  HLSLPROGRAM
  #pragma vertex vert
  #pragma fragment frag
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  TEXTURE2D(_DispTex); SAMPLER(sampler_DispTex);
  TEXTURE2D(_RampTex); SAMPLER(sampler_RampTex);
  CBUFFER_START(UnityPerMaterial)
   float4 _DispTex_ST, _RampTex_ST;
   float4 _ChannelFactor, _Range;
   float _Displacement, _ClipRange;
  CBUFFER_END
  struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
  struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
  Varyings vert(Attributes v) {
   Varyings o;
   float d = dot(SAMPLE_TEXTURE2D_LOD(_DispTex, sampler_DispTex, v.uv, 0).rgb, _ChannelFactor.rgb);
   o.positionCS = TransformObjectToHClip(v.positionOS.xyz + v.normalOS * d * _Displacement);
   o.uv = TRANSFORM_TEX(v.uv, _DispTex);
   return o;
  }
  half4 frag(Varyings i):SV_Target {
   float d = dot(SAMPLE_TEXTURE2D(_DispTex, sampler_DispTex, i.uv).rgb, _ChannelFactor.rgb);
   d = lerp(_Range.x, _Range.y, d);
   clip(_ClipRange - d);
   half4 c = SAMPLE_TEXTURE2D(_RampTex, sampler_RampTex, float2(d,0.5));
   return half4(c.rgb * (1 + c.a), 1);
  }
  ENDHLSL
 }
}
Fallback Off
}
