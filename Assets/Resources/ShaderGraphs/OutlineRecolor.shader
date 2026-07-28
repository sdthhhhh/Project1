Shader "Hidden/OutlineRecolor"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "OutlineRecolor"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D_X(_SceneColorCopy);
            SAMPLER(sampler_SceneColorCopy);

            TEXTURE2D_X(_OutlineColorMask);
            SAMPLER(sampler_OutlineColorMask);

            float4 _OutlineColor; // unused for base now — base is already in _BlitTexture
            float _EdgeRecoverStrength;

            float4 SampleMask(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_OutlineColorMask, sampler_OutlineColorMask, uv);
            }

            // Expand mask slightly so silhouette pixels just outside the object still pick up color.
            float4 PickMask(float2 uv, float2 texel)
            {
                float4 c = SampleMask(uv);
                if (c.a > 0.01) return c;

                float bestA = 0;
                float4 best = 0;
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    float2 o = (i == 0) ? float2(-1, -1) :
                               (i == 1) ? float2( 1,  1) :
                               (i == 2) ? float2( 1, -1) :
                                          float2(-1,  1);
                    float4 s = SampleMask(uv + o * texel);
                    if (s.a > bestA)
                    {
                        bestA = s.a;
                        best = s;
                    }
                }
                return best;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 texel = float2(1.0 / _ScreenParams.x, 1.0 / _ScreenParams.y);

                float4 scene = SAMPLE_TEXTURE2D_X(_SceneColorCopy, sampler_SceneColorCopy, uv);
                // _BlitTexture = scene AFTER black outline pass (base layer).
                float4 blackOutlined = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float4 mask = PickMask(uv, texel * 2.0);

                // How much the black-outline pass changed this pixel (≈ outline weight).
                float edgeAmt = saturate(length(blackOutlined.rgb - scene.rgb) * _EdgeRecoverStrength);

                // 1) Base: keep black-outlined image everywhere.
                float3 result = blackOutlined.rgb;

                // 2) Then overlay color ONLY where mask exists AND there is a real outline.
                //    mask.a * edgeAmt avoids painting whole object surfaces solid white/red.
                float colorWeight = mask.a * edgeAmt;
                float3 coloredEdge = lerp(scene.rgb, mask.rgb, edgeAmt);
                result = lerp(result, coloredEdge, colorWeight);

                return float4(result, scene.a);
            }
            ENDHLSL
        }
    }
}
