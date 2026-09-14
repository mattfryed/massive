Shader "MASSIVE/UI/Multiplier Maximum Glow"
{
    Properties
    {
        _Tint ("Color", Color) = (1,1,1,1)
        _BarSize ("Bar width, height, glow reach", Vector) = (2.55,0.25,0.12,0)
        _GlowStrength ("Opacity, pulse", Vector) = (0.32,1,0,0)
        _SweepPhase ("Traveling highlight", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            float4 _Tint, _BarSize, _GlowStrength;
            float _SweepPhase;
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float spread = max(0.001, _BarSize.z);
                float2 p = (i.uv - 0.5) * (_BarSize.xy + 2.0 * spread);
                float2 outside = max(abs(p) - _BarSize.xy * 0.5, 0.0);
                float distance = length(outside);
                float falloff = exp(-3.0 * pow(distance / spread, 2.0));
                falloff *= 1.0 - smoothstep(spread * 0.75, spread, distance);
                float sweepDistance = (p.x - (_SweepPhase - 0.5) * _BarSize.x) / max(0.001, _BarSize.y * 0.5);
                float sweep = exp(-2.0 * sweepDistance * sweepDistance) * sin(_SweepPhase * UNITY_PI);
                float alpha = falloff * _GlowStrength.x * (_GlowStrength.y + sweep * 1.8);
                return fixed4(_Tint.rgb, saturate(alpha * _Tint.a));
            }
            ENDCG
        }
    }
}
