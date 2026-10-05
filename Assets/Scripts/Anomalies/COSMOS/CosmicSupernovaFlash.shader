Shader "MASSIVE/Cosmos/Supernova Flash"
{
    Properties
    {
        [HDR] _Color ("Flash Color", Color) = (1, .67, .25, 1)
        _Brightness ("Emission", Float) = 4
        _Phase ("Burst Phase (-1 Before Burst)", Range(-1,1)) = 0
        _Glow ("Localized Glow", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off ZTest LEqual Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color;
            float _Brightness, _Phase, _Glow;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata v)
            {
                v2f o; o.position = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o;
            }
            float4 frag(v2f i) : SV_Target
            {
                float r2 = dot(i.uv, i.uv); clip(1 - r2);
                float phase = saturate(_Phase), r = sqrt(r2);
                float core = step(r, lerp(.2, .015, phase));
                float ringRadius = lerp(.12, .95, sqrt(phase));
                float ring = step(abs(r - ringRadius), lerp(.055, .008, phase));
                // The localized glow can rise and flicker before any burst geometry appears.
                // It has zero intensity at the quad boundary and fades continuously after peak.
                float glow = max(0, exp(-r2 * 10) - exp(-10)) * max(0, _Glow);
                float light = (1 - phase) * (1 - phase) * step(0, _Phase);
                float3 color = lerp(_Color.rgb, float3(1, 1, .95), core * .85);
                float3 emission = color * max(core, ring) * light + lerp(_Color.rgb, float3(1, .9, .75), .25) * glow;
                return float4(emission * _Brightness, 0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
