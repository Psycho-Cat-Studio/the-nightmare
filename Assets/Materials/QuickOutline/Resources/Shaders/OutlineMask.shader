//
//  OutlineMask.shader
//  QuickOutline
//
//  Created by Chris Nolet on 2/21/18.
//  Copyright © 2018 Chris Nolet. All rights reserved.
//

Shader "Custom/Outline Mask" {
  Properties {
    [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 0
  }

  SubShader {
    Tags {
      "Queue" = "Transparent+100"
      "RenderType" = "Transparent"
      "RenderPipeline" = "UniversalPipeline"
    }

    Pass {
      Name "Mask"
      Cull Off
      ZTest [_ZTest]
      ZWrite Off
      ColorMask 0

      Stencil {
        Ref 1
        Pass Replace
      }
      HLSLPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      float4 vert(float4 p : POSITION) : SV_POSITION { return TransformObjectToHClip(p.xyz); }
      half4 frag() : SV_Target { return 0; }
      ENDHLSL
    }
  }
}
