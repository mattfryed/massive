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
            float4 _Motion, _Shape, _Pivot, _Flow, _FlowShape;
            float4x4 _ColonyRotation;
            fixed4 _Structure, _Motor, _Catalyst, _Nutrient;
            struct appdata { float4 vertex : POSITION; float4 center : TEXCOORD0; float4 detail : TEXCOORD1; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; };

            float3 Circulate(float3 p)
            {
                // Differential rotation: a continuous shared current with counterflowing layers,
                // rather than a rigid rotation or a separate random orbit for every particle.
                float surface = smoothstep(.12, .95, dot(p, p));
                float rate = lerp(1, .25 + .75 * sin(p.y * 3.14159265), _Flow.y);
                rate *= lerp(1, _FlowShape.x, surface);
                float s, c;
                sincos(_Flow.x * rate, s, c);
                p.xz = float2(c * p.x - s * p.z, s * p.x + c * p.z);

                // Twists depend only on their own axis coordinate: each is a smooth,
                // volume-preserving map. All three keep radius unchanged, so flow cannot
                // escape the icon or erase the chemistry's radial species arrangement.
                float eddy = .5 * _Flow.z * (sin(p.x * _Flow.w + _Flow.x * .8) - sin(p.x * _Flow.w));
                sincos(eddy, s, c);
                p.yz = float2(c * p.y - s * p.z, s * p.y + c * p.z);
                eddy = .35 * _Flow.z * (sin(p.z * _Flow.w - _Flow.x * .65) - sin(p.z * _Flow.w));
                sincos(eddy, s, c);
                p.xy = float2(c * p.x - s * p.y, s * p.x + c * p.y);
                return p;
            }

            v2f vert(appdata v)
            {
                v2f o;
                float phase = v.detail.x;
                float3 flowingCenter = v.center.xyz;
                if (_FlowShape.y > .5) flowingCenter = Circulate(flowingCenter);
                float breath = 1 + _Motion.y * sin(_Motion.x + flowingCenter.x * 2);
                float3 ripple = sin(_Motion.w + phase + float3(0, 2.0944, 4.1888)) * (_Motion.z * .57735);
                float radius = _Shape.y * (1 + v.detail.y * _Shape.z) * v.detail.z;
                float3 center = flowingCenter * (_Shape.x * breath) + ripple;
                center *= float3(1 + _Pivot.w, 1 - _Pivot.w * .5, 1 - _Pivot.w * .5);
                float3 p = mul((float3x3)_ColonyRotation, center + v.vertex.xyz * radius);
                p += _Pivot.xyz;
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
