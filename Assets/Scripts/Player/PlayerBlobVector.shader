Shader "MASSIVE/PlayerBlobVector"
{
    Properties
    {
        _OutlineColor    ("Outline Color", Color)             = (1,1,1,1)
        _FillColor       ("Fill Color", Color)                = (0,0,0,1)

        _Radius          ("Radius", Float)                    = 0.65
        _OutlineHalf     ("Outline Half Width", Float)        = 0.03

        // Symmetric wobble
        _IdleWobble      ("Idle Wobble (0..1)", Range(0,1))   = 0.0

        // Forward axis (local)
        _DeformDir       ("Directional Axis (local)", Vector) = (1,0,0,0)

        // Hit ripple
        _HitImpulse      ("Hit Impulse", Float)               = 0.0
        _HitAngle        ("Hit Angle",   Float)               = 0.0
        _HitTime         ("Hit Time",    Float)               = 0.0
        _NoisePhase      ("Noise Phase", Float)               = 0.0

        // Global vertical squash
        _Stretch         ("Uniform Y scale", Float)           = 1.0

        // Contact flatten
        _ContactStrength ("Contact Strength", Float)          = 0.0
        _ContactAngle    ("Contact Angle",   Float)           = 0.0

        // Teardrop / lunge shape
        _DeformLerp      ("Teardrop Strength (0..1)", Range(0,1)) = 0.0
        _TeardropK1      ("Teardrop Front/Back Gain", Float)      = 0.8
        _TeardropK2      ("(unused)", Float)                      = 0.0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off

        HLSLINCLUDE
        #pragma target 4.5
        #include "UnityCG.cginc"

        static const uint SEG = 128u;

        float4x4 _MVP;
        float4   _OutlineColor, _FillColor;

        float    _Radius;
        float    _OutlineHalf;
        float    _IdleWobble;

        float2   _DeformDir;

        float    _HitImpulse;
        float    _HitAngle;
        float    _HitTime;
        float    _NoisePhase;
        float    _Stretch;

        float    _ContactStrength;
        float    _ContactAngle;

        float    _DeformLerp;
        float    _TeardropK1;
        float    _TeardropK2; // currently unused

        float2 NormSafe(float2 v)
        {
            float len2 = dot(v, v);
            if (len2 < 1e-6) return float2(1.0, 0.0);
            return v * rsqrt(len2);
        }

        // Simple single-lobe cosine teardrop, monotonic front→tail.
        // cosTheta = dot(dir, forward) in [-1,1]
        float TeardropRadius(
            float baseR,
            float cosTheta,
            float strength,
            float kFront
        )
        {
            float s = saturate(strength);
            if (s <= 1e-5) return baseR; // perfect circle when idle

            // Base cosine amplitude
            float d = s * kFront;

            // Clamp so tail radius never goes below minFrac * baseR
            float minFrac = 0.40;          // tail at least 40% of base
            float dMax    = 1.0 - minFrac; // max allowed amplitude
            d = min(d, dMax);

            // Raw cosine shape: largest at front (cos=1), smallest at tail (cos=-1)
            float rRaw = 1.0 + d * cosTheta;   // in [1-d, 1+d]

            // Exact area of r(θ) = R (1 + d cosθ) is:
            // A = π R^2 (1 + 0.5 d^2)  ⇒ scale radius by 1/sqrt(1 + 0.5 d^2)
            float areaFactor = 1.0 + 0.5 * d * d;
            float scale      = rsqrt(areaFactor);

            float r = baseR * rRaw * scale;

            // Final safety clamp (should already satisfy this)
            r = max(r, baseR * minFrac);

            return r;
        }

        // Idle wobble (symmetric)
        float symIdle(float ang)
        {
            float band = sin(_NoisePhase * 2.3 + ang * 5.0) * 0.6
                       + sin(_NoisePhase * 1.3 + ang * 9.0) * 0.4;
            return _IdleWobble * 0.08 * band;
        }

        float wrapToPi(float a)
        {
            const float PI = 3.14159265;
            a = fmod(a + PI, 2.0 * PI);
            if (a < 0.0) a += 2.0 * PI;
            return a - PI;
        }

        float hitRipple(float ang)
        {
            if (_HitImpulse <= 1e-5) return 0.0;

            float t  = _HitTime;
            float da = wrapToPi(ang - _HitAngle);

            float indentWidth = 0.45;
            float indentFallT = 2.0;
            float indent = -exp(-(da * da) / (2.0 * indentWidth * indentWidth))
                           * exp(-t * indentFallT);

            float waveSpeed = 3.0;
            float waveK     = 8.0;
            float waveDecay = 2.0;
            float timeDecay = 1.0;

            float center1 = _HitAngle + waveSpeed * t;
            float center2 = _HitAngle - waveSpeed * t;
            float d1 = wrapToPi(ang - center1);
            float d2 = wrapToPi(ang - center2);

            float env1 = exp(-abs(d1) * waveDecay);
            float env2 = exp(-abs(d2) * waveDecay);
            float w1   = cos(d1 * waveK) * env1;
            float w2   = cos(d2 * waveK) * env2;
            float waves = (w1 + w2) * exp(-t * timeDecay);

            float distFromImpact = abs(da);
            const float PI = 3.14159265;
            float fullSpan = PI;
            float midNorm  = saturate(distFromImpact / fullSpan);
            float hump     = 4.0 * midNorm * (1.0 - midNorm);
            float wavesShaped = waves * hump;

            float combined = indent + 0.7 * wavesShaped;
            return _HitImpulse * combined * 0.25;
        }

        float contactFlatten(float ang)
        {
            if (_ContactStrength <= 1e-5) return 0.0;

            float da = wrapToPi(ang - _ContactAngle);
            const float PI = 3.14159265;
            da = fmod(da + PI, 2.0 * PI);
            if (da < 0.0) da += 2.0 * PI;
            da -= PI;

            float width   = 0.7;
            float norm    = abs(da) / width;
            float flatAmt = saturate(1.0 - norm);

            return -_ContactStrength * flatAmt * 0.4;
        }

        struct v2f_fill
        {
            float4 pos : SV_POSITION;
        };

        struct v2f_ring
        {
            float4 pos : SV_POSITION;
            float2 lp  : TEXCOORD0;
        };

        // Area-preserving fan geometry with vertical squash
        float2 fanPos(uint tri, uint corner, float ang, float r)
        {
            // center vertex
            if (corner == 0u) return 0;

            float2 dir = float2(cos(ang), sin(ang));

            float stretchSafe = max(_Stretch, 1e-4);
            float rScaled     = r * rsqrt(stretchSafe); // 1/sqrt(stretch)

            return float2(dir.x * rScaled, dir.y * rScaled * stretchSafe);
        }

        float expectedRadius(float ang)
        {
            float2 dir = float2(cos(ang), sin(ang));

            float2 fwd = NormSafe(_DeformDir.xy);
            float cosTheta = clamp(dot(dir, fwd), -1.0, 1.0);

            float rBase = TeardropRadius(
                _Radius,
                cosTheta,
                _DeformLerp,
                _TeardropK1
            );

            float sSym = 1.0
                + symIdle(ang)
                + hitRipple(ang)
                + contactFlatten(ang);

            float r = rBase * sSym;
            return max(r, 0.02);
        }

        ENDHLSL

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

                uint idx = (corner == 1u) ? tri : (tri + 1u);
                float t   = (idx % SEG) / (float)SEG;
                float ang = t * 6.2831853;

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

                float r    = expectedRadius(ang);
                float2 p   = fanPos(tri, corner, ang, r);
                o.lp       = p;
                o.pos      = mul(_MVP, float4(p.x, 0, p.y, 1));
                return o;
            }

            fixed4 frag_ring(v2f_ring i) : SV_Target
            {
                float stretchSafe = max(_Stretch, 1e-5);
                float2 lpUn = float2(i.lp.x, i.lp.y / stretchSafe);
                float ang   = atan2(lpUn.y, lpUn.x);

                float rExpIso  = expectedRadius(ang) * rsqrt(stretchSafe);
                float rNowIso  = length(lpUn);
                float delta    = abs(rNowIso - rExpIso);

                float w = max(_OutlineHalf, 1e-4);
                float alpha = 1.0 - smoothstep(w * 0.6, w, delta);
                return _OutlineColor * alpha;
            }
            ENDHLSL
        }
    }
}