Shader "Custom/URP/OutlineShell"
{
    Properties
    {
        _OutlineColor("Outline Color", Color) = (1, 1, 1, 1)
        _OutlineWidth("Outline Width (Object)", Float) = 0.02
        _OutlinePixelWidth("Outline Width (Pixels)", Float) = 2.0
        _ScreenSpaceOutline("Screen Space Outline", Float) = 1.0
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
            float _OutlinePixelWidth;
            float _ScreenSpaceOutline;

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

                if (_ScreenSpaceOutline > 0.5)
                {
                    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                    float4 positionCS = TransformWorldToHClip(positionWS);

                    float3 normalCS = TransformWorldToHClipDir(normalWS, true);
                    float2 n = normalCS.xy;
                    float nLen = length(n);
                    if (nLen > 1e-5)
                    {
                        n /= nLen;
                        float px = max(_OutlinePixelWidth, 0.0);
                        positionCS.x += n.x * px * (2.0 / _ScreenParams.x) * positionCS.w;
                        positionCS.y += n.y * px * (2.0 / _ScreenParams.y) * positionCS.w;
                    }

                    output.positionCS = positionCS;
                    return output;
                }

                float3 nn = normalize(input.normalOS);
                float3 posOS = input.positionOS.xyz + nn * _OutlineWidth;
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
