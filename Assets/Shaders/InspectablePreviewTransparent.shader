Shader "UI/InspectablePreviewTransparent"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _MagentaSoftness ("Magenta Key Softness", Range(0.01, 0.35)) = 0.16
        _BlackThreshold ("URP Black Threshold", Range(0.001, 0.12)) = 0.025
        _BlackSoftness ("URP Black Softness", Range(0.005, 0.25)) = 0.06
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Cull Off Lighting Off ZWrite Off ZTest Always
        // The fragment program returns premultiplied colour. This prevents dark outlines where
        // an anti-aliased model edge was rendered against a black target.
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            sampler2D _MainTex;
            float _MagentaSoftness;
            float _BlackThreshold;
            float _BlackSoftness;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex=UnityObjectToClipPos(v.vertex);
                o.uv=v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 sampleColor=tex2D(_MainTex,i.uv);
                const float3 keyColor=float3(1.0,0.0,1.0);

                float distanceFromMagenta=distance(sampleColor.rgb,keyColor);
                float magentaCoverage=smoothstep(0.015,_MagentaSoftness,distanceFromMagenta);

                float brightness=max(sampleColor.r,max(sampleColor.g,sampleColor.b));
                float blackCoverage=smoothstep(_BlackThreshold,_BlackThreshold+_BlackSoftness,brightness);

                float coverage=min(sampleColor.a,min(magentaCoverage,blackCoverage));

                // Remove key-colour spill and keep the result premultiplied for clean edges.
                float3 premultiplied=max(sampleColor.rgb-keyColor*(1.0-magentaCoverage),0.0);
                premultiplied*=coverage/max(magentaCoverage,0.0001);
                return fixed4(premultiplied,coverage);
            }
            ENDCG
        }
    }
}
