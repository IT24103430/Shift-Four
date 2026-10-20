Shader "ShiftFour/World Builder Signage"
{
    Properties
    {
        _BaseMap("Glyph atlas", 2D) = "white" {}
        _BaseColor("Letter colour", Color) = (0.82, 0.85, 0.81, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half coverage = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a;
                clip(coverage - .4h);
                return _BaseColor;
            }
            ENDHLSL
        }
    }
}
