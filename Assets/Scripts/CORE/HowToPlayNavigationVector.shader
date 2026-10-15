Shader "MASSIVE/How To Play Navigation Vector"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 shape : TEXCOORD1;
                float4 color : COLOR;
            };
            struct v2f
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 shape : TEXCOORD1;
                float4 color : COLOR;
            };
            v2f vert(appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.shape = v.shape;
                o.color = v.color;
                return o;
            }
            float segmentDistance(float2 p, float2 a, float2 b)
            {
                float2 delta = b - a;
                return length(p - a - delta * saturate(dot(p - a, delta) / max(dot(delta, delta), 1e-8)));
            }
            float4 frag(v2f i) : SV_Target
            {
                float2 tip = float2(i.shape.x, 0);
                float2 back = float2(i.shape.x - i.shape.y, i.shape.z);
                float distance = min(segmentDistance(i.uv, float2(0, 0), tip),
                    min(segmentDistance(i.uv, tip, back), segmentDistance(i.uv, tip, back * float2(1, -1))));
                float aa = max(fwidth(distance), 1e-5);
                float coverage = 1 - smoothstep(i.shape.w - aa * .5, i.shape.w + aa * .5, distance);
                return float4(i.color.rgb, i.color.a * coverage);
            }
            ENDHLSL
        }
    }
}
