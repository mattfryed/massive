// Assets/VectorGridNu/3DVector/FieldLines.shader
// Enhanced line shader for MASSIVE magnetosphere field lines.
// - Legacy mode (|B| mapped) OR enhanced mode (inner->outer radial + pole overlay)
// - Pseudo-3D back/front shading using Y offset (compute writes Y)
// - Soft behind-planet occlusion (fade, not hard cut)
// - Additive blending (order-independent) to eliminate Append-buffer draw-order flicker

Shader "MASSIVE/FieldLines"
{
    Properties
    {
        // Legacy
        _Alpha("Alpha", Range(0,1)) = 1
        _LineColorNear("Legacy Color Near", Color) = (1,0.75,0.7,1)
        _LineColorFar("Legacy Color Far", Color) = (1,0,0.6,1)
        _ColorMagScale("Legacy |B| Scale", Float) = 1

        // Enhanced coloring (controlled via _UseEnhancedColor float)
        _UseEnhancedColor("Use Enhanced Color", Float) = 0
        _ArcInnerColor("Arc Inner Color", Color) = (1,0.75,0.7,1)
        _ArcOuterColor("Arc Outer Color", Color) = (1,0,0.6,1)

        _PoleOverlayColor("Pole Overlay Color", Color) = (1,0.2,0.9,1)
        _PoleOverlayRadius("Pole Overlay Radius", Float) = 0.45
        _PoleOverlayStrength("Pole Overlay Strength", Float) = 1
        _PoleOverlayPower("Pole Overlay Power", Float) = 2

        _RadialColorRadiusScale("Radial Color Radius Scale", Float) = 1

        // Pseudo 3D shading (front/back illusion)
        _Pseudo3DEnabled("Pseudo 3D Enabled", Float) = 0
        _BackBrightness("Back Brightness", Range(0,1)) = 0.45
        _BackAlpha("Back Alpha", Range(0,1)) = 0.55

        // Instead of a hard cut, fade behind-planet segments (0 = off, 1 = on)
        _BehindPlanetFade("Behind Planet Fade", Range(0,1)) = 1
        _BehindPlanetFadeSoftness("Fade Softness", Range(0.01,2)) = 0.35

        // Vertex warble (shader-side, optional; can stay 0 if compute warble is used)
        _VSPathWarbleStrength("VS Path Warble Strength", Float) = 0
        _VSPathWarbleSpeed("VS Path Warble Speed", Float) = 0.7
        _VSPathWarbleFreq("VS Path Warble Freq", Float) = 2
        _VSPathWarbleBoundaryDamp("VS Warble Boundary Damp", Range(0,1)) = 0.7
        _VSPathWarblePoleDamp("VS Warble Pole Damp", Range(0,1)) = 0.7
    }

    SubShader
    {
        Tags{ "Queue"="Transparent" "RenderType"="Transparent" }

        // Additive = order independent (fixes flicker from append-buffer draw order)
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

            struct Segment { float3 a; float3 b; float mag; };
            StructuredBuffer<Segment> _Segments;

            // Legacy
            float4 _LineColorNear, _LineColorFar;
            float _Alpha, _ColorMagScale;

            // Enhanced
            float _UseEnhancedColor;
            float4 _ArcInnerColor, _ArcOuterColor;
            float4 _PoleOverlayColor;
            float _PoleOverlayRadius, _PoleOverlayStrength, _PoleOverlayPower;
            float _RadialColorRadiusScale;

            // Pseudo3D
            float _Pseudo3DEnabled;
            float _BackBrightness, _BackAlpha;
            float _BehindPlanetFade, _BehindPlanetFadeSoftness;

            // Shader-side warble
            float _VSPathWarbleStrength;
            float _VSPathWarbleSpeed;
            float _VSPathWarbleFreq;
            float _VSPathWarbleBoundaryDamp;
            float _VSPathWarblePoleDamp;

            // World-space context (set by C#)
            float3 _CenterWS;
            float _MagRadiusWS;
            float _PlanetRadiusWS;
            float3 _DipoleAxisWS;

            // In your setup C# sets identity, but keep for compatibility
            float4x4 _LocalToWorld;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float  mag  : TEXCOORD0;
                float  radT : TEXCOORD1;
                float  poleT : TEXCOORD2;
                float  backT : TEXCOORD3;
                float  alphaMul : TEXCOORD4;
            };

            float Smooth01(float x) { x = saturate(x); return x*x*(3.0-2.0*x); }

            v2f vert(uint vid : SV_VertexID)
            {
                v2f o;
                uint segIndex = vid / 2;
                uint endPoint = vid & 1u;

                Segment s = _Segments[segIndex];
                float3 p = (endPoint == 0u) ? s.a : s.b;

                float3 pW = mul(_LocalToWorld, float4(p, 1)).xyz;

                float3 c = _CenterWS;
                float magR = max(1e-4, _MagRadiusWS);

                // Midpoint metrics for stable per-segment color
                float3 mid = 0.5 * (s.a + s.b);
                float3 midW = mul(_LocalToWorld, float4(mid, 1)).xyz;

                float2 rm2 = (midW.xz - c.xz);
                float rm = length(rm2);
                o.radT = saturate(rm / (magR * max(1e-4, _RadialColorRadiusScale)));

                // Pole overlay factor (distance to either pole)
                float3 axis = normalize(float3(_DipoleAxisWS.x, 0, _DipoleAxisWS.z));
                if (dot(axis, axis) < 1e-6) axis = float3(0,0,1);

                float3 poleA = c + axis * _PlanetRadiusWS;
                float3 poleB = c - axis * _PlanetRadiusWS;

                float dA = length(midW - poleA);
                float dB = length(midW - poleB);
                float dMin = min(dA, dB);

                float poleT = saturate(1.0 - (dMin / max(1e-4, _PoleOverlayRadius)));
                poleT = pow(poleT, max(0.25, _PoleOverlayPower)) * _PoleOverlayStrength;
                o.poleT = saturate(poleT);

                // Back/front: dy < 0 means "behind"
                float dy = pW.y - c.y;
                o.backT = (_Pseudo3DEnabled > 0.5 && dy < 0.0) ? 1.0 : 0.0;

                // Optional shader-side warble (keep subtle)
                if (_VSPathWarbleStrength > 1e-6)
                {
                    float2 r2 = (pW.xz - c.xz);
                    float r = max(1e-5, length(r2));
                    float2 dir = r2 / r;
                    float2 tan = float2(-dir.y, dir.x);

                    float q = saturate(r / magR);
                    float boundaryDamp = lerp(1.0, 1.0 - _VSPathWarbleBoundaryDamp, Smooth01(saturate((q - 0.7) / 0.3)));
                    float poleDamp = lerp(1.0, 1.0 - _VSPathWarblePoleDamp, o.poleT);

                    float phase = _Time.y * _VSPathWarbleSpeed + (r * _VSPathWarbleFreq * 0.15) + (segIndex * 0.07);
                    float wob = sin(phase) * _VSPathWarbleStrength * boundaryDamp * poleDamp;
                    pW.xz += tan * wob;
                }

                // Behind-planet fade (soft)
                o.alphaMul = 1.0;
                if (_Pseudo3DEnabled > 0.5 && _BehindPlanetFade > 0.5 && o.backT > 0.5)
                {
                    float2 r2 = (pW.xz - c.xz);
                    float r = length(r2);
                    float soft = max(0.01, _BehindPlanetFadeSoftness);
                    float fade = smoothstep(_PlanetRadiusWS - soft, _PlanetRadiusWS + soft, r);
                    o.alphaMul = fade;
                }

                o.mag = s.mag;
                o.pos = UnityWorldToClipPos(pW);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float a = _Alpha * i.alphaMul;

                // Legacy: |B| drives color
                float m = saturate(i.mag * _ColorMagScale);
                float3 col = lerp(_LineColorFar.rgb, _LineColorNear.rgb, m);

                // Enhanced: radial inner->outer with pole overlay
                if (_UseEnhancedColor > 0.5)
                {
                    col = lerp(_ArcInnerColor.rgb, _ArcOuterColor.rgb, i.radT);
                    col = lerp(col, _PoleOverlayColor.rgb, saturate(i.poleT));
                    col *= lerp(0.65, 1.05, m);
                }

                // Pseudo3D dimming (still useful with additive)
                if (_Pseudo3DEnabled > 0.5 && i.backT > 0.5)
                {
                    col *= _BackBrightness;
                    a *= _BackAlpha;
                }

                // Additive blend: encode intensity in RGB; alpha channel not used for blending.
                return float4(col * a, 1.0);
            }

            ENDHLSL
        }
    }
}