Shader "MASSIVE/Dynamo Flow Blanket"
{
    Properties
    {
        _Color ("Color", Color) = (.34,.96,.86,1)
        [HideInInspector] _DstBlend ("Destination blend", Float) = 1
        [HideInInspector] _OpaqueStreaks ("Opaque tapered streaks", Float) = 0
        [HideInInspector] _CoreIntensity ("Sharp core intensity", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend One [_DstBlend]
            ZWrite Off
            ZTest LEqual
            Cull Off
            CGPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Streak { float3 a; float3 b; float brightness; };
            StructuredBuffer<Streak> _Streaks;
            StructuredBuffer<float2> _Envelope;
            float4 _Color, _ProfileRange;
            float3 _Center, _Wind, _FrontFlow;
            float _Opacity, _Width, _Front, _Feather, _Brightness, _OpaqueStreaks, _CoreIntensity;
            struct Varyings { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 world : TEXCOORD1; float brightness : TEXCOORD2; };
            Varyings vert(uint id : SV_VertexID)
            {
                Streak s = _Streaks[id / 6];
                uint corner = id % 6;
                float2 uv = corner == 0 ? float2(0,0) : corner == 1 ? float2(1,0) : corner == 2 ? float2(1,1) : corner == 3 ? float2(0,0) : corner == 4 ? float2(1,1) : float2(0,1);
                // Two triangles form a solid diamond: zero width at each tip.
                if (_OpaqueStreaks > .5)
                    uv = corner == 0 || corner == 3 ? float2(0,.5) : corner == 1 ? float2(.5,0) : corner == 5 ? float2(.5,1) : float2(1,.5);
                float3 tangent = s.b - s.a;
                tangent /= max(.00001, length(tangent));
                float3 side = normalize(cross(tangent, float3(0,1,0)) + float3(.000001,0,0));
                Varyings o;
                float width = _Width * (_OpaqueStreaks > .5 ? saturate(_Opacity) : 1);
                o.world = lerp(s.a, s.b, uv.x) + side * ((uv.y - .5) * width);
                o.pos = UnityWorldToClipPos(o.world);
                o.uv = uv; o.brightness = s.brightness;
                return o;
            }
            float4 frag(Varyings i) : SV_Target
            {
                float3 r = i.world - _Center; r.y = 0;
                float along = dot(r, _Wind);
                float across = length(r - along * _Wind);
                float u = saturate((along - _ProfileRange.x) / max(.001, _ProfileRange.y - _ProfileRange.x)) * (_ProfileRange.z - 1);
                uint ix = min((uint)u, (uint)_ProfileRange.z - 2);
                float2 ab = max(.001, lerp(_Envelope[ix], _Envelope[ix+1], u-ix));
                float q2 = along * along / (ab.x * ab.x) + across * across / (ab.y * ab.y);
                // Also clips the outgoing layout during a direction transition against the NEW boundary.
                clip(q2 - 1.002);
                float front = smoothstep(0, max(.001, _Feather), _Front - dot(i.world, _FrontFlow));
                if (_OpaqueStreaks > .5)
                {
                    clip(front - .001);
                    clip(_Opacity - .0001);
                    return float4(_Color.rgb * _Brightness * _CoreIntensity, 1);
                }
                float crossFade = pow(saturate(1 - abs(i.uv.y * 2 - 1)), 1.3);
                float shape = smoothstep(0,.2,i.uv.x) * (1 - smoothstep(.85,1,i.uv.x)) * lerp(.25,1.5,i.uv.x);
                return float4(_Color.rgb * (_Color.a * _Brightness * _CoreIntensity * _Opacity * front * crossFade * shape * i.brightness), 0);
            }
            ENDCG
        }
    }
}
