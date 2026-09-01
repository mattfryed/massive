Shader "MASSIVE/PlayerBlob"
{
    Properties
    {
        _FillColor        ("Fill Color", Color) = (0,0,0,1)
        _OutlineColor     ("Outline Color", Color) = (1,1,1,1)
        _OutlineHalfWidth ("Outline Half Width", Float) = 0.03
        _Taper            ("Back Taper", Float) = 0.18
        _Stretch          ("Motion Stretch Scale", Float) = 1.0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        ZWrite Off
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        // ---------- PASS 1: FILL ----------
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            // set from script
            float2 _Center;     // local center (usually 0,0)
            float  _Radius;
            float2 _DeformDir;  // normalized (XZ projected)
            float  _DeformAmt;  // 0..small
            float  _HitImpulse; // decays after impact
            float  _NoisePhase; // subtle shimmer

            // inspector
            float4 _FillColor;
            float  _Taper;
            float  _Stretch;

            struct app { float4 pos:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 clip:SV_POSITION; float2 lp:TEXCOORD0; };

            v2f vert(app v)
            {
                v2f o;
                o.clip = UnityObjectToClipPos(v.pos);
                o.lp   = v.pos.xy;
                return o;
            }

            float sdDeformedCircle(float2 p, float2 dir, float R, float deform, float hit)
            {
                dir = normalize(dir + 1e-5);
                float2 ortho = float2(-dir.y, dir.x);

                // motion stretch (squash along heading)
                float sx = 1.0 - deform * _Stretch;
                float sy = 1.0 + deform * _Stretch;
                float2 q = float2(dot(p, dir) * sx, dot(p, ortho) * sy);

                // asymmetric back taper (trailing side thinner)
                float s = dot(p, dir);                                // signed along heading
                float taper = 1.0 + _Taper * saturate(-s / (R+1e-5)); // only behind
                float rr = R * (1.0 + 0.04 * hit) * taper;

                return length(q) - rr;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.lp - _Center;
                float deform = _DeformAmt + 0.015 * sin(_NoisePhase + p.x*11.3 + p.y*7.9);
                float d = sdDeformedCircle(p, _DeformDir, _Radius, deform, _HitImpulse);
                if (d > 0) discard;
                return _FillColor;
            }
            ENDHLSL
        }

        // ---------- PASS 2: OUTLINE ----------
        Pass
        {
            // rim should always paint over nuggets/fill
            ZWrite Off
            Cull Off
            ZTest  Always
            Blend  SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            // set from script
            float2 _Center; float _Radius; float2 _DeformDir; float _DeformAmt; float _HitImpulse; float _NoisePhase;

            // inspector
            float  _OutlineHalfWidth;
            float4 _OutlineColor;
            float  _Taper;
            float  _Stretch;

            struct app { float4 pos:POSITION; };
            struct v2f { float4 clip:SV_POSITION; float2 lp:TEXCOORD0; };

            v2f vert(app v){ v2f o; o.clip = UnityObjectToClipPos(v.pos); o.lp = v.pos.xy; return o; }

            float sdDeformedCircle(float2 p, float2 dir, float R, float deform, float hit)
            {
                dir = normalize(dir + 1e-5);
                float2 ortho = float2(-dir.y, dir.x);

                float sx = 1.0 - deform * _Stretch;
                float sy = 1.0 + deform * _Stretch;
                float2 q = float2(dot(p, dir) * sx, dot(p, ortho) * sy);

                float s = dot(p, dir);
                float taper = 1.0 + _Taper * saturate(-s / (R+1e-5));
                float rr = R * (1.0 + 0.04 * hit) * taper;

                return length(q) - rr;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.lp - _Center;
                float deform = _DeformAmt + 0.015 * sin(_NoisePhase + p.x*11.3 + p.y*7.9);
                float d = sdDeformedCircle(p, _DeformDir, _Radius, deform, _HitImpulse);

                if (abs(d) > _OutlineHalfWidth) discard;   // crisp band
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}