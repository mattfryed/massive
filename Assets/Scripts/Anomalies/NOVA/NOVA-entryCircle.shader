Shader "MASSIVE/NOVA-entryCircle"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Softness ("Softness", Range(0,2)) = 0.25
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

            float4 _Color;
            float _Softness;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.uv * 2.0 - 1.0;
                float r = length(p);

                // Soft edge to avoid shimmer
                float a = smoothstep(1.0, 1.0 - 0.01 * _Softness, r);

                fixed4 col = _Color;
                col.a *= a;
                return col;
            }
            ENDCG
        }
    }
}
