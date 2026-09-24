Shader "MASSIVE/Orbital/Impact"
{
    Properties
    {
        _CoreFraction ("Bright line fraction", Range(.05,1)) = .28
        _GlowStrength ("Soft glow brightness", Range(0,1)) = .18
    }
    SubShader
    {
        Tags { "Queue"="Transparent+30" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest LEqual
        CGINCLUDE
        #include "UnityCG.cginc"
        struct appdata { float4 vertex:POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
        struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
        float _CoreFraction, _GlowStrength;
        v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.color=v.color; o.uv=v.uv; return o; }
        ENDCG
        // Glow contributes light through RGB brightness, never through reduced opacity.
        Pass
        {
            Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment halo
            fixed4 halo(v2f i):SV_Target
            {
                float brightness = pow(saturate(1 - abs(i.uv.y * 2 - 1)), 2) * _GlowStrength;
                return fixed4(i.color.rgb * brightness, 1);
            }
            ENDCG
        }
        // Solid core. Lifetime animation changes geometry; alpha stays exactly one.
        Pass
        {
            Blend Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment core
            fixed4 core(v2f i):SV_Target
            {
                clip(_CoreFraction - abs(i.uv.y * 2 - 1));
                return fixed4(i.color.rgb, 1);
            }
            ENDCG
        }
    }
}
