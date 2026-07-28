Shader "Hidden/OutlinesMultiColor"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "OutlinesMultiColor"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D_X(_SceneViewSpaceNormals);
            SAMPLER(sampler_SceneViewSpaceNormals);

            TEXTURE2D_X(_OutlineColorMask);
            SAMPLER(sampler_OutlineColorMask);

            float4 _OutlineColor;
            float _OutlineScale;
            float _DepthThreshold;
            float _RobertsCrossMultiplier;
            float _NormalThreshold;
            float _SteepAngleThreshold;
            float _SteepAngleMultiplier;

            float SampleRawDepth(float2 uv)
            {
                return SampleSceneDepth(uv);
            }

            float3 SampleViewNormal(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_SceneViewSpaceNormals, sampler_SceneViewSpaceNormals, uv).xyz;
            }

            float4 SampleColorMask(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_OutlineColorMask, sampler_OutlineColorMask, uv);
            }

            float RobertsCrossDepth(float2 uv, float2 texel)
            {
                float d0 = SampleRawDepth(uv + float2(-1, -1) * texel * _OutlineScale);
                float d1 = SampleRawDepth(uv + float2( 1,  1) * texel * _OutlineScale);
                float d2 = SampleRawDepth(uv + float2( 1, -1) * texel * _OutlineScale);
                float d3 = SampleRawDepth(uv + float2(-1,  1) * texel * _OutlineScale);
                float2 diff = float2(d0 - d1, d2 - d3);
                return sqrt(dot(diff, diff)) * _RobertsCrossMultiplier;
            }

            float RobertsCrossNormal(float2 uv, float2 texel)
            {
                float3 n0 = SampleViewNormal(uv + float2(-1, -1) * texel * _OutlineScale);
                float3 n1 = SampleViewNormal(uv + float2( 1,  1) * texel * _OutlineScale);
                float3 n2 = SampleViewNormal(uv + float2( 1, -1) * texel * _OutlineScale);
                float3 n3 = SampleViewNormal(uv + float2(-1,  1) * texel * _OutlineScale);
                float3 d0 = n0 - n1;
                float3 d1 = n2 - n3;
                return sqrt(dot(d0, d0) + dot(d1, d1));
            }

            float4 PickOutlineColor(float2 uv, float2 texel)
            {
                float4 c = SampleColorMask(uv);
                if (c.a > 0.01) return c;

                // Prefer a neighbour that sits on an outlined object.
                float4 best = c;
                float bestA = c.a;
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    float2 o = (i == 0) ? float2(-1, -1) :
                               (i == 1) ? float2( 1,  1) :
                               (i == 2) ? float2( 1, -1) :
                                          float2(-1,  1);
                    float4 s = SampleColorMask(uv + o * texel * _OutlineScale);
                    if (s.a > bestA)
                    {
                        bestA = s.a;
                        best = s;
                    }
                }

                if (bestA > 0.01) return best;
                return float4(_OutlineColor.rgb, 1);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float4 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float2 texel = float2(1.0 / _ScreenParams.x, 1.0 / _ScreenParams.y);

                float depthEdge = RobertsCrossDepth(uv, texel);
                float normalEdge = RobertsCrossNormal(uv, texel);

                // Steep-angle depth soften (same knobs as the original feature).
                float3 normal = SampleViewNormal(uv);
                float NdotV = saturate(normal.z);
                float steep = 1.0 - smoothstep(_SteepAngleThreshold, 1.0, NdotV);
                depthEdge *= 1.0 + steep * _SteepAngleMultiplier;

                float edgeDepth = step(_DepthThreshold, depthEdge);
                float edgeNormal = step(_NormalThreshold, normalEdge);
                float edge = saturate(max(edgeDepth, edgeNormal));

                float4 outlineCol = PickOutlineColor(uv, texel);
                return lerp(sceneColor, float4(outlineCol.rgb, sceneColor.a), edge);
            }
            ENDHLSL
        }
    }
}
