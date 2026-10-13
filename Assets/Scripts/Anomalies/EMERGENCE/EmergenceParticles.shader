Shader "MASSIVE/Emergence/Particles"
{
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Pass
        {
            Blend Off
            ZWrite On ZTest LEqual Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            v2f vert(appdata v) { v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv * 2 - 1; o.color = v.color; return o; }
            float4 frag(v2f i) : SV_Target
            {
                float r2 = dot(i.uv, i.uv); clip(1 - r2);
                // Solid, unlit discs, matching the COSMOS opaque-particle approach.
                return float4(i.color.rgb, 1);
            }
            ENDCG
        }
    }
}
