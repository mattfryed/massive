Shader "MASSIVE/UI/CircleIcon"
{
    Properties
    {
        // ✅ UGUI expects this name:
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        _FillColor    ("Fill Color", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Width", Range(0,0.5)) = 0.1
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex; // ✅ declare it (even if you don’t “need” it)

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
                float2 p = i.uv * 2.0 - 1.0;
                float  r = length(p);

                if (r > 1.0) discard;

                float innerRadius = 1.0 - _OutlineWidth;
                fixed4 col = (r <= innerRadius) ? _FillColor : _OutlineColor;

                // ✅ Multiply by UI texture (usually white unless an Image/Sprite supplies one)
                fixed4 tex = tex2D(_MainTex, i.uv);
                col *= tex;

                return col;
            }
            ENDCG
        }
    }
}
