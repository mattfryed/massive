Shader "MASSIVE/StormStreak"
{
    Properties
    {
        _Intensity ("Storm Intensity", Range(0,1)) = 0
        _Color ("Color", Color) = (1,1,1,1)

        _Width ("Base Width", Range(0.02, 1.0)) = 0.22
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.5)) = 0.08

        _TailStretch ("Tail Stretch", Range(0.5, 6.0)) = 1.6
        _TailStretchStormMul ("Tail Stretch Storm Mul", Range(0, 6.0)) = 1.4

        _HeadBoost ("Head Boost", Range(0, 4.0)) = 1.2
        _TailFadePow ("Tail Fade Pow", Range(0.25, 6.0)) = 1.8

        _FlowRippleAmp ("Flow Ripple Amp", Range(0, 0.25)) = 0.05
        _FlowRippleFreq ("Flow Ripple Freq", Range(0, 20)) = 6.0
        _FlowRippleSpeed ("Flow Ripple Speed", Range(0, 10)) = 2.0

        _StormBrightMul ("Storm Bright Mul", Range(0, 3.0)) = 1.2
        _StormWidthMul ("Storm Width Mul", Range(0.1, 2.0)) = 0.85
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
        }

        Blend One One
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            // IMPORTANT for ParticleSystemRenderer:
            #pragma multi_compile_particles
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            float _Intensity;
            fixed4 _Color;

            float _Width;
            float _EdgeSoftness;

            float _TailStretch;
            float _TailStretchStormMul;

            float _HeadBoost;
            float _TailFadePow;

            float _FlowRippleAmp;
            float _FlowRippleFreq;
            float _FlowRippleSpeed;

            float _StormBrightMul;
            float _StormWidthMul;

            // Your Custom Vertex Streams show:
            // UV: TEXCOORD0.xy
            // Velocity.xy: TEXCOORD0.zw
            // Velocity.z: TEXCOORD1.x  (the “(x)” in the inspector)
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                fixed4 color  : COLOR;
                float4 uv0    : TEXCOORD0;   // xy=uv, zw=vel.xy
                float4 uv1    : TEXCOORD1;   // x = vel.z (at least)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
                fixed4 col : TEXCOORD1;
                float2 dirVS : TEXCOORD2;
                float  speed : TEXCOORD3;
                UNITY_FOG_COORDS(4)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            inline float Smooth01(float x) { x = saturate(x); return x*x*(3.0-x*2.0); }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv0.xy;
                o.col = v.color * _Color;

                float3 velW = float3(v.uv0.z, v.uv0.w, v.uv1.x);
                float sp = length(velW);
                o.speed = sp;

                // Direction in view-plane
                float3 velVS = mul((float3x3)UNITY_MATRIX_V, velW);
                float2 d = velVS.xy;

                // Fallback if velocity stream is missing/zero
                if (dot(d, d) < 1e-8) d = float2(1, 0);
                o.dirVS = normalize(d);

                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            // Capsule SDF in normalized space, along X axis from -1..1, radius = 1
            inline float CapsuleSdf(float2 q)
            {
                float2 a = float2(-1, 0);
                float2 b = float2( 1, 0);
                float2 pa = q - a;
                float2 ba = b - a;
                float h = saturate(dot(pa, ba) / dot(ba, ba));
                float2 c = a + ba * h;
                return length(q - c) - 1.0;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.uv * 2.0 - 1.0;

                float2 dir = i.dirVS;
                float2 perp = float2(-dir.y, dir.x);

                float along = dot(p, dir);
                float side  = dot(p, perp);

                float stormS = saturate(_Intensity);
                float stretch = _TailStretch * (1.0 + _TailStretchStormMul * stormS);
                float width   = _Width * lerp(1.0, _StormWidthMul, stormS);

                if (_FlowRippleAmp > 1e-6)
                {
                    float phase = along * _FlowRippleFreq + _Time.y * _FlowRippleSpeed;
                    side += sin(phase) * _FlowRippleAmp * (0.3 + 0.7 * stormS);
                }

                float2 q = float2(along / max(1e-4, stretch), side / max(1e-4, width));
                float d = CapsuleSdf(q);

                float aa = max(1e-4, _EdgeSoftness);
                float shape = 1.0 - smoothstep(0.0, aa, d);
                shape = saturate(shape);

                float tHead = saturate((along + 1.0) * 0.5);
                float tailFade = pow(tHead, max(0.25, _TailFadePow));
                float headBoost = lerp(1.0, (1.0 + _HeadBoost), tHead);

                float brightStorm = lerp(1.0, _StormBrightMul, stormS);

                float intensity = shape * tailFade * headBoost * i.col.a * brightStorm;

                float3 col = i.col.rgb * intensity;

                // Additive blending
                fixed4 outC = fixed4(col, 1.0);
                UNITY_APPLY_FOG(i.fogCoord, outC);
                return outC;
            }
            ENDCG
        }
    }
}