Shader "MASSIVE/PlayerBlobVector"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _FillColor    ("Fill Color", Color)    = (0,0,0,1)

        _Radius       ("Radius", Float)        = 0.65
        _OutlineHalf  ("Outline Half Width", Float) = 0.03

        // Wobble & stretch
        _IdleWobble   ("Idle Wobble (0..1)", Range(0,1)) = 0.0
        _DeformAmt    ("Directional Stretch Amount", Float) = 0.0
        _DeformDir    ("Directional Axis (local)", Vector) = (1,0,0,0)
        _DirFrontGain ("Dir Front Gain", Range(0,2)) = 1.2
        _DirBackGain  ("Dir Back Gain",  Range(0,2)) = 0.6
        _AreaKeep     ("Area Keep (0..1)", Range(0,1)) = 0.85   // how much to cancel average 'growth'
        _HitImpulse   ("Hit Impulse", Float)   = 0.0
        _NoisePhase   ("Noise Phase", Float)   = 0.0
        _Stretch      ("Uniform Y scale", Float) = 1.0
        _HitAngle   ("Hit Angle", Float) = 0.0
        _HitTime   ("Hit Time", Float) = 0.0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off

        HLSLINCLUDE
        #pragma target 4.5
        #include "UnityCG.cginc"
        #include "BlobDeform.hlsl"

        static const uint SEG = 128u;

        // Shared uniforms
        float4x4 _MVP;
        float4   _OutlineColor, _FillColor;
        float    _Radius, _OutlineHalf, _IdleWobble, _DeformAmt, _DirFrontGain, _DirBackGain, _AreaKeep, _HitImpulse, _NoisePhase, _Stretch, _HitAngle, _HitTime;
        float4   _DeformDir;


        // -------- helpers
        float2 normSafe(float2 v) { float l = max(length(v), 1e-6); return v / l; }

        float symIdle(float ang)
        {
            float band = sin(_NoisePhase * 2.3 + ang * 5.0) * 0.6
                       + sin(_NoisePhase * 1.3 + ang * 9.0) * 0.4;
            return _IdleWobble * 0.08 * band;
        }

        float wrapToPi(float a)
        {
            // wrap to [-pi, pi]
            const float PI = 3.14159265;
            a = fmod(a + PI, 2.0 * PI);
            if (a < 0.0) a += 2.0 * PI;
            return a - PI;
        }

        float hitRipple(float ang)
        {
            if (_HitImpulse <= 1e-5) return 0.0;

            // Angle relative to impact direction
            float da = wrapToPi(ang - _HitAngle);

            // Time since hit
            float t = _HitTime;

            // 1) Local indentation at impact (inward dent), decaying fairly quickly
            float indentWidth = 0.45;         // angular width of initial dent
            float indentFallT = 2.0;          // how fast the dent relaxes
            float indent = -exp(- (da * da) / (2.0 * indentWidth * indentWidth))
                        * exp(-t * indentFallT);

            // 2) Traveling ring: wave center moves away from impact along circumference
            float waveSpeed   = 3.5;          // radians per second along the circle
            float waveK       = 8.0;          // ripple frequency
            float waveDecay   = 1.5;          // angular decay away from ring center
            float timeDecay   = 1.0;          // overall time decay

            // Wavefront position measured in angle-space
            float travel      = da - waveSpeed * t;
            float travelEnv   = exp(-abs(travel) * waveDecay);
            float ripple      = sin(travel * waveK) * travelEnv * exp(-t * timeDecay);

            // Combine: dent + weaker traveling ring
            float combined = indent + 0.7 * ripple;

            // Scale by hit impulse and a global gain
            return _HitImpulse * combined * 0.25;
        }


        // asymmetric + elliptical stretch along DeformDir; keeps overall area ~constant
        float dirStretch(float2 dirUnit)
        {
            if (_DeformAmt <= 1e-6) return 0.0;

            float2 ax = normSafe(_DeformDir.xy);
            float c   = dot(dirUnit, ax);           // cos Δ
            float cos2 = 2.0 * c * c - 1.0;         // cos(2Δ): +1 along ±axis, -1 at sides

            // 1) Symmetric ellipse: front+back elongated, sides compressed.
            //    This already has zero mean over the circle, so area is preserved by construction.
            float sEllipse = cos2 * (_DeformAmt * 0.5);  // 0.5 is a good starting gain

            // 2) Asymmetric teardrop: front vs back bias layered on top.
            float front = max(c, 0.0);
            float back  = max(-c, 0.0);
            float sRaw  = front * _DirFrontGain - back * _DirBackGain;

            // Remove average bias from the asymmetric part only,
            // so we don't inflate the whole blob when front/back gains differ.
            float meanCorr   = 0.32 * (_DirFrontGain - _DirBackGain);
            float sZeroMean  = sRaw - _AreaKeep * meanCorr;
            float sTeardrop  = _DeformAmt * sZeroMean;

            return sEllipse + sTeardrop;
        }


        // unified radius used by DEPTH/FILL/RING (keeps fill & ring perfectly tight)
        float expectedRadius(float ang)
        {
            float2 dir = float2(cos(ang), sin(ang));
            float sSym = 1.0 + symIdle(ang) + hitRipple(ang);  // symmetric wobble
            float sDir = dirStretch(dir);                      // signed, zero-mean lurch
            return _Radius * (sSym + sDir);
        }

        struct v2f_fill { float4 pos : SV_POSITION; };
        struct v2f_ring { float4 pos : SV_POSITION; float2 lp : TEXCOORD0; };

        float2 fanPos(uint tri, uint corner, float ang, float r)
        {
            if (corner == 0u) return 0;
            float2 dir = float2(cos(ang), sin(ang));
            return float2(dir.x * r, dir.y * r * _Stretch);
        }
        ENDHLSL

        // ---------------- PASS 0: FILL (writes depth)
        Pass
        {
            Name "FILL"
            ZWrite On
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex   vert_fill
            #pragma fragment frag_fill

            v2f_fill vert_fill(uint vid : SV_VertexID)
            {
                v2f_fill o;
                uint tri    = vid / 3u;
                uint corner = vid % 3u;

                // angle for rim verts
                uint idx = (corner == 1u) ? tri : (tri + 1u);
                float t   = (idx % SEG) / (float)SEG;
                float ang = t * 6.2831853;

                // inner edge so it tucks under the outline
                float rIn = max(0, expectedRadius(ang) - _OutlineHalf);
                float2 p  = fanPos(tri, corner, ang, rIn);

                o.pos = mul(_MVP, float4(p.x, 0, p.y, 1));
                return o;
            }

            fixed4 frag_fill(v2f_fill i) : SV_Target
            {
                return _FillColor;
            }
            ENDHLSL
        }

        // ---------------- PASS 1: RING (analytic band; no depth write)
        Pass
        {
            Name "RING"
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex   vert_ring
            #pragma fragment frag_ring

            v2f_ring vert_ring(uint vid : SV_VertexID)
            {
                v2f_ring o;
                uint tri    = vid / 3u;
                uint corner = vid % 3u;

                uint idx = (corner == 1u) ? tri : (tri + 1u);
                float t   = (idx % SEG) / (float)SEG;
                float ang = t * 6.2831853;

                float r    = expectedRadius(ang);      // ring fan sits on rim center
                float2 p   = fanPos(tri, corner, ang, r);
                o.lp       = p;
                o.pos      = mul(_MVP, float4(p.x, 0, p.y, 1));
                return o;
            }

            fixed4 frag_ring(v2f_ring i) : SV_Target
            {
                // evaluate pixels in circular space (undo uniform Y stretch)
                float2 lpUn = float2(i.lp.x, i.lp.y / max(_Stretch, 1e-5));
                float ang   = atan2(lpUn.y, lpUn.x);

                float rExp  = expectedRadius(ang);
                float rNow  = length(lpUn);
                float delta = abs(rNow - rExp);

                float w = max(_OutlineHalf, 1e-4);
                float alpha = 1.0 - smoothstep(w * 0.6, w, delta);
                return _OutlineColor * alpha;
            }
            ENDHLSL
        }
    }
}
