Shader "MASSIVE/StormHUDLine"
{
    Properties
    {
        _Tint("Tint", Color) = (1, 0, 0.7, 1)
        _Glow("Glow", Range(0, 8)) = 1
        _EdgeSoftness("Edge Softness", Range(0.001, 1)) = 0.35
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        // Additive with alpha controlling intensity:
        Blend SrcAlpha One
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            half4 _Tint;
            half  _Glow;
            half  _EdgeSoftness;

            struct appdata
            {
                float4 vertex : POSITION;
                half4  color  : COLOR;     // we use alpha for per-primitive variation
                float2 uv     : TEXCOORD0; // uv.y should be in [-1..1] across thickness
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                half4  color : COLOR;
                float2 uv    : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                // thickness falloff from center (0) to edge (1)
                float d = abs(i.uv.y);
                float soft = max(1e-4, _EdgeSoftness);
                float falloff = 1.0 - smoothstep(1.0 - soft, 1.0, d);

                half4 c = i.color * _Tint;

                // alpha controls additive intensity (SrcAlpha One)
                half a = saturate(c.a * (half)falloff);
                half3 rgb = c.rgb * _Glow;

                return half4(rgb, a);
            }
            ENDCG
        }
    }
}
