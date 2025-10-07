// Assets/VectorGridNu/3DVector/FieldLines.shader
Shader "MASSIVE/FieldLines"
{
    Properties{
        _Alpha("Alpha", Range(0,1)) = 1
        _LineColorNear("Color Near", Color) = (0.6,0.9,1,1)
        _LineColorFar("Color Far", Color) = (0.1,0.4,1,1)
        _ColorMagScale("Color |B| Scale", Float) = 1
    }
    SubShader
    {
        Tags{ "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct Segment { float3 a; float3 b; float mag; };
            StructuredBuffer<Segment> _Segments;

            float4 _LineColorNear, _LineColorFar;
            float _Alpha, _ColorMagScale;
            float4x4 _LocalToWorld;

            struct v2f {
                float4 pos : SV_POSITION;
                float t    : TEXCOORD0; // 0 or 1 endpoint
                float mag  : TEXCOORD1; // |B| avg for segment
            };

            v2f vert(uint vid : SV_VertexID)
            {
                v2f o;
                uint segIndex = vid / 2;
                uint endPoint = vid & 1u;

                Segment s = _Segments[segIndex];
                float3 p = (endPoint == 0u) ? s.a : s.b;
                float4 w = mul(_LocalToWorld, float4(p,1));
                o.pos = UnityObjectToClipPos(mul(unity_WorldToObject, w));
                o.t = (endPoint == 0u) ? 0.0 : 1.0;
                o.mag = s.mag;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                // Map |B|->color via simple lerp (tweak scale to taste)
                float m = saturate(i.mag * _ColorMagScale);
                float3 col = lerp(_LineColorFar.rgb, _LineColorNear.rgb, m);
                return float4(col, _Alpha);
            }
            ENDHLSL
        }
    }
}
