Shader "MASSIVE/UI/CircleIcon"
{
    Properties
    {
        _FillColor    ("Fill Color", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Width", Range(0,0.5)) = 0.1
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

            float4 _FillColor;
            float4 _OutlineColor;
            float  _OutlineWidth;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // UV from 0..1 → -1..1
                float2 p = i.uv * 2.0 - 1.0;
                float  r = length(p);  // distance from center

                // Hard circle edge
                if (r > 1.0) discard;

                float innerRadius = 1.0 - _OutlineWidth;

                // Hard fill/outline selection
                float4 col = (r <= innerRadius) ? _FillColor : _OutlineColor;

                // Hard alpha (no diffusion)
                if (r > 1.0) discard;
                col.a = 1.0;

                return col;

            }
            ENDCG
        }
    }
}
