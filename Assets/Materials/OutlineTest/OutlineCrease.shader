Shader "Custom/URP/OutlineCrease"
{
    Properties
    {
        _OutlineColor("Outline Color", Color) = (1, 1, 1, 1)
        _OutlinePixelWidth("Outline Width (Pixels)", Float) = 1.5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry+1"
        }

        Pass
        {
            Name "OutlineCrease"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On
            ZTest LEqual
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _OutlineColor;
            float _OutlinePixelWidth;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT; // xyz = edge dir OS, w = side ±1
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                float3 posOS = input.positionOS.xyz + normalize(input.normalOS) * 1e-4;
                float3 positionWS = TransformObjectToWorld(posOS);
                float4 positionCS = TransformWorldToHClip(positionWS);

                float3 edgeWS = TransformObjectToWorldDir(input.tangentOS.xyz, true);
                float3 edgeCS = TransformWorldToHClipDir(edgeWS, true);

                float2 edgePx = float2(edgeCS.x * _ScreenParams.x, edgeCS.y * _ScreenParams.y);
                float edgeLen = length(edgePx);
                float2 perp = edgeLen > 1e-5
                    ? float2(-(edgePx / edgeLen).y, (edgePx / edgeLen).x)
                    : float2(1, 0);

                float side = input.tangentOS.w;
                float halfPx = max(_OutlinePixelWidth, 0.0) * 0.5;
                positionCS.x += perp.x * side * halfPx * (2.0 / _ScreenParams.x) * positionCS.w;
                positionCS.y += perp.y * side * halfPx * (2.0 / _ScreenParams.y) * positionCS.w;

                output.positionCS = positionCS;
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
