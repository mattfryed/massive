Shader "MASSIVE/Emergence/Level Icon"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            ZWrite On
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Motion, _Shape;
            float4x4 _ColonyRotation;
            fixed4 _Structure, _Motor, _Catalyst, _Nutrient;
            struct appdata { float4 vertex : POSITION; float4 center : TEXCOORD0; float4 detail : TEXCOORD1; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; };
            v2f vert(appdata v)
            {
                v2f o;
                float phase = v.detail.x;
                float breath = 1 + _Motion.y * sin(_Motion.x + v.center.x * 2);
                float3 ripple = sin(_Motion.w + phase + float3(0, 2.0944, 4.1888)) * (_Motion.z * .57735);
                float radius = _Shape.y * (1 + v.detail.y * _Shape.z) * v.detail.z;
                float3 center = v.center.xyz * (_Shape.x * breath) + ripple;
                float3 p = mul((float3x3)_ColonyRotation, center + v.vertex.xyz * radius);
                o.vertex = UnityObjectToClipPos(float4(p, 1));
                o.color = v.center.w < .5 ? _Structure : v.center.w < 1.5 ? _Motor : v.center.w < 2.5 ? _Catalyst : _Nutrient;
                o.color.a = 1;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target { return fixed4(i.color.rgb, 1); }
            ENDCG
        }
    }
}
