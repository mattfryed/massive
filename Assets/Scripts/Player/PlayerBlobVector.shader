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
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off

        HLSLINCLUDE
        #pragma target 4.5
        #include "UnityCG.cginc"

        static const uint SEG = 128u;

        // Shared uniforms
        float4x4 _MVP;
        float4   _OutlineColor, _FillColor;
        float    _Radius, _OutlineHalf, _IdleWobble, _DeformAmt, _DirFrontGain, _DirBackGain, _AreaKeep, _HitImpulse, _NoisePhase, _Stretch;
        float4   _DeformDir;

        // -------- helpers
        float2 normSafe(float2 v) { float l = max(length(v), 1e-6); return v / l; }

        float symIdle(float ang)
        {
            float band = sin(_NoisePhase * 2.3 + ang * 5.0) * 0.6
                       + sin(_NoisePhase * 1.3 + ang * 9.0) * 0.4;
            return _IdleWobble * 0.08 * band;
        }

        float hitRipple(float ang)
        {
            return _HitImpulse * (sin(_NoisePhase * 20.0 + ang * 8.0)) * 0.10;
        }

        // asymmetric teardrop along DeformDir; signed front(+) / back(-)
        float dirStretch(float2 dirUnit)
        {
            if (_DeformAmt <= 1e-6) return 0.0;

            float2 ax = normSafe(_DeformDir.xy);
            float front = max(dot(dirUnit,  ax), 0.0); // 0..1
            float back  = max(dot(dirUnit, -ax), 0.0); // 0..1

            // raw signed stretch
            float sRaw = front * _DirFrontGain - back * _DirBackGain;

            // remove the average bias so the blob doesn't "grow" when gains differ
            // (the mean of max(dot,0) around the circle ~ 0.318; 0.32 is a good practical constant)
            float meanCorr = 0.32 * (_DirFrontGain - _DirBackGain);
            float sZeroMean = sRaw - _AreaKeep * meanCorr;

            return _DeformAmt * sZeroMean;
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
