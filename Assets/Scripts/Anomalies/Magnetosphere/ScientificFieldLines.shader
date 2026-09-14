Shader "MASSIVE/Scientific Field Lines"
{
    Properties
    {
        [HDR] _NearColor("Inner color", Color) = (1,.65,.82,1)
        [HDR] _FarColor("Outer color", Color) = (.95,.02,.5,1)
        _WidthPixels("Core width in pixels", Range(.5,4)) = 1.1
        _Brightness("Brightness", Float) = 1.5
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
            float4x4 _GsmToWorld;
            float4 _NearColor, _FarColor;
            float _WidthPixels, _Brightness, _Flow, _AnimationTime, _Step;
            uint _Steps;
            struct v2f { float4 pos:SV_POSITION; float edge:TEXCOORD0; float radius:TEXCOORD1;
                float valid:TEXCOORD2; float path:TEXCOORD3; float speed:TEXCOORD4; };
            v2f vert(uint id:SV_VertexID)
            {
                Segment s = _Segments[id / 6];
                uint v = id % 6;
                bool end = v == 2 || v == 3 || v == 5;
                float side = (v == 0 || v == 2 || v == 3) ? -1 : 1;
                float4 a = UnityWorldToClipPos(mul(_GsmToWorld, float4(s.a.xyz,1)).xyz);
                float4 b = UnityWorldToClipPos(mul(_GsmToWorld, float4(s.b.xyz,1)).xyz);
                float2 delta = (b.xy / max(b.w,1e-5) - a.xy / max(a.w,1e-5)) * _ScreenParams.xy;
                float2 normal = float2(-delta.y,delta.x) / max(length(delta),1e-5);
                v2f o;
                o.pos = end ? b : a;
                // Camera-facing strips give a stable pixel core with an antialiased fringe.
                o.pos.xy += normal * side * (_WidthPixels + 1.5) / _ScreenParams.xy * o.pos.w;
                o.edge = side; o.radius = length(end ? s.b.xyz : s.a.xyz);
                o.valid = s.b.w * (a.w > 0 && b.w > 0);
                uint segment = id / 6;
                float direction = ((segment / _Steps) % 2) == 0 ? 1 : -1;
                o.path = direction * ((segment % _Steps) + (end ? 1 : 0)) * _Step;
                o.speed = s.a.w;
                return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float core = 1 - smoothstep(.3, 1, abs(i.edge));
                float3 col = lerp(_NearColor.rgb,_FarColor.rgb,saturate((i.radius-3)/18));
                float pulse = _Flow > .5 ? .25 + .75 * pow(.5 + .5 * cos(i.path * 1.5 - _AnimationTime * 3), 4) : 1;
                return float4(col * core * _Brightness * i.valid * pulse, 0);
            }
            ENDHLSL
        }
    }
}
