Shader "Custom/URP/OutlineShell"
{
    Properties
    {
        _OutlineColor("Outline Color", Color) = (1, 1, 1, 1)
        _OutlineWidth("Outline Width", Float) = 0.02
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry-1"
        }

        Pass
        {
            Name "OutlineShell"
            Tags { "LightMode" = "UniversalForward" }

            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _OutlineColor;
            float _OutlineWidth;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                // Object-space extrusion along surface normal (follows mesh, not a world-constant offset).
                float3 n = normalize(input.normalOS);
                float3 posOS = input.positionOS.xyz + n * _OutlineWidth;
                output.positionCS = TransformObjectToHClip(posOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return half4(_OutlineColor.rgb, 1);
            }
            ENDHLSL
        }
    }
}
