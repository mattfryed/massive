Shader "MASSIVE/NuggetsDots"
{
    Properties { _Color("Dot Color", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        ZWrite Off Cull Off Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Color;

            struct app { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };

            v2f vert(app v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv=v.uv; return o; }

            fixed4 frag(v2f i):SV_Target
            {
                // hard round disc (no gradient)
                float2 uv = i.uv*2-1;
                if (dot(uv,uv) > 1.0) discard;
                return _Color;
            }
            ENDHLSL
        }
    }
}