Shader "MASSIVE/Dynamo Gameplay Field Lines"
{
    Properties
    {
        [HDR] _NearColor("Inner color", Color) = (1,.38,.64,1)
        [HDR] _FarColor("Outer color", Color) = (.65,.015,.6,1)
        _WidthPixels("Core width in pixels", Range(.5,4)) = 1.35
        _Brightness("Brightness", Float) = 1.1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend One One
        ZWrite Off
        ZTest LEqual
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Segment { float4 a; float4 b; };
            StructuredBuffer<Segment> _Segments;
            StructuredBuffer<float4> _Envelope;
            int _EnvelopeCount;
            float _EnvelopeExtent, _WorldScale, _DepthScale, _Deformation, _InnerRadius, _Radius;
            float3 _Center, _DisplayWind, _EnvelopeWind;
            float4 _NearColor, _FarColor;
            float _WidthPixels, _Brightness, _BackBrightness;

            float3 EnvelopeShape(float along)
            {
                float index = saturate(.5 + along / (2 * _EnvelopeExtent)) * (_EnvelopeCount - 1);
                uint lo = (uint)index;
                float3 value = lerp(_Envelope[lo].xyz, _Envelope[min(lo + 1, (uint)_EnvelopeCount - 1)].xyz, frac(index));
                value.xy = max(.001, value.xy);
                // Linear continuation keeps the map invertible beyond the sampled table as well.
                value.z += (along - clamp(along, -_EnvelopeExtent, _EnvelopeExtent)) * value.x;
                return value;
            }

            float3 Present(float3 fieldPosition)
            {
                // Tracing already incorporates the wind. Keep the intrinsic dipole anchored to
                // the arena so changing wind cannot rotate an otherwise calm planetary field.
                float3 local = fieldPosition * _WorldScale;
                local.y *= _DepthScale;
                float along = dot(local, _EnvelopeWind);
                float3 perpendicular = local - _EnvelopeWind * along;
                // A continuous monotonic mapping preserves recorded topology under strong storms.
                float3 shape = EnvelopeShape(along);
                return _Center + _EnvelopeWind * lerp(along, shape.z, _Deformation) +
                    perpendicular * lerp(1, shape.y, _Deformation);
            }

            float BoundaryFade(float3 world)
            {
                float3 delta = world - _Center; delta.y = 0;
                float along = dot(delta, _EnvelopeWind);
                float perpendicular = length(delta - _EnvelopeWind * along);
                float2 scale = EnvelopeShape(along).xy * max(.1, _Radius);
                float q = length(float2(along, perpendicular) / scale);
                // The original gameplay envelope remains the hazard/particle boundary.
                return 1 - smoothstep(.86, 1, q);
            }

            struct v2f
            {
                float4 pos : SV_POSITION;
                float edge : TEXCOORD0;
                float radius : TEXCOORD1;
                float visibility : TEXCOORD2;
            };
            v2f vert(uint id : SV_VertexID)
            {
                Segment s = _Segments[id / 6];
                uint v = id % 6;
                bool end = v == 2 || v == 3 || v == 5;
                float side = (v == 0 || v == 2 || v == 3) ? -1 : 1;
                float3 wa = Present(s.a.xyz), wb = Present(s.b.xyz);
                float4 a = UnityWorldToClipPos(wa), b = UnityWorldToClipPos(wb);
                float2 delta = (b.xy / max(b.w, 1e-5) - a.xy / max(a.w, 1e-5)) * _ScreenParams.xy;
                float2 normal = float2(-delta.y, delta.x) / max(length(delta), 1e-5);
                v2f o;
                o.pos = end ? b : a;
                o.pos.xy += normal * side * (_WidthPixels + 1.5) / _ScreenParams.xy * o.pos.w;
                o.edge = side;
                o.radius = length(end ? s.b.xyz : s.a.xyz);
                float3 world = end ? wb : wa;
                float front = smoothstep(-2, 2, world.y - _Center.y);
                o.visibility = s.b.w * (a.w > 0 && b.w > 0) * BoundaryFade(world) * lerp(_BackBrightness, 1, front);
                return o;
            }
            float4 frag(v2f i) : SV_Target
            {
                float core = 1 - smoothstep(.3, 1, abs(i.edge));
                float3 col = lerp(_NearColor.rgb, _FarColor.rgb, saturate((i.radius - _InnerRadius) / 16));
                return float4(col * core * _Brightness * i.visibility, 0);
            }
            ENDHLSL
        }
    }
}
