Shader "MASSIVE/HIGGS/UnderlayTopo_URP"
{
    Properties
    {
        _HiggsHeight ("Higgs Height (RFloat)", 2D) = "black" {}
        _HiggsExcite ("Higgs Excite (RFloat)", 2D) = "black" {}

        _Opacity     ("Line Opacity", Range(0,1)) = 0.85

        _HeightRemapOffset ("Height Remap Offset", Float) = 0.0
        _HeightRemapScale  ("Height Remap Scale", Float) = 1.0

        _ContourFreq ("Contour Frequency", Float) = 18
        _StrokeWidthPx ("Contour Stroke Width (px-ish)", Range(0.25, 6)) = 1.25
        _AAMult      ("AA Mult", Range(0.5, 4)) = 1.25

        [Header(Amp Hue Blend)]
        _AmpColorMin ("Amp Color Min", Color) = (1,1,1,1)
        _AmpColorMax ("Amp Color Max", Color) = (1,1,1,1)
        _AmpColorStrength ("Amp Color Strength", Range(0,1)) = 1.0
        _AmpColorPow ("Amp Color Curve", Range(0.1, 4)) = 1.0

        [Header(Excitation Pierce Hue)]
        _PierceHeight ("Pierce Height (raw height threshold)", Float) = 0.0
        _PierceFeather ("Pierce Feather", Range(0.001, 2)) = 0.25
        _PierceColorMin ("Pierce Color Min", Color) = (1,1,1,1)
        _PierceColorMax ("Pierce Color Max", Color) = (1,1,1,1)
        _PierceColorStrength ("Pierce Color Strength", Range(0,1)) = 1.0
        _PierceColorPow ("Pierce Color Curve", Range(0.1, 4)) = 1.0

        [Header(Excitation Debug Ring)]
        _ShowExcite ("Show Excitation Debug", Range(0,1)) = 1
        _ExciteLevel ("Excite Ring Level", Range(0,1)) = 0.55
        _ExciteStrokeWidthPx ("Excite Ring Width (px-ish)", Range(0.25, 8)) = 2.0
        _ExciteOpacity ("Excite Ring Opacity", Range(0,1)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_HiggsHeight);  SAMPLER(sampler_HiggsHeight);
            TEXTURE2D(_HiggsExcite);  SAMPLER(sampler_HiggsExcite);

            float _Opacity;

            float _HeightRemapOffset;
            float _HeightRemapScale;

            float _ContourFreq;
            float _StrokeWidthPx;
            float _AAMult;

            float4 _AmpColorMin;
            float4 _AmpColorMax;
            float _AmpColorStrength;
            float _AmpColorPow;

            float _PierceHeight;
            float _PierceFeather;
            float4 _PierceColorMin;
            float4 _PierceColorMax;
            float _PierceColorStrength;
            float _PierceColorPow;

            float _ShowExcite;
            float _ExciteLevel;
            float _ExciteStrokeWidthPx;
            float _ExciteOpacity;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            float SampleHRaw(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_HiggsHeight, sampler_HiggsHeight, uv).r;
            }

            float SampleE(float2 uv)
            {
                return saturate(SAMPLE_TEXTURE2D(_HiggsExcite, sampler_HiggsExcite, uv).r);
            }

            // Constant pixel-ish iso-lines centered at frac(x)=0.5 (avoids integer-plateau fills)
            float IsoLinesMid(float x, float strokeWidthPx, float aaMult)
            {
                float f = frac(x);
                float distToLine = abs(f - 0.5);      // 0 at mid-band
                float fw = fwidth(x) * aaMult;
                float w  = max(1e-5, strokeWidthPx * fw);
                return 1.0 - smoothstep(w, w + fw, distToLine);
            }

            float IsoRing(float v, float level, float strokeWidthPx, float aaMult)
            {
                float d = abs(v - level);
                float fw = fwidth(v) * aaMult;
                float w  = max(1e-5, strokeWidthPx * fw);
                return 1.0 - smoothstep(w, w + fw, d);
            }

            half4 frag (Varyings i) : SV_Target
            {
                float2 uv = i.uv;

                float hRaw = SampleHRaw(uv);
                float e    = SampleE(uv);

                // Remap used for coloring (not clamped for math, but we clamp to 0..1 for blend)
                float hRemap = hRaw * _HeightRemapScale + _HeightRemapOffset;
                float ampT = saturate(hRemap);
                ampT = pow(max(ampT, 1e-5), _AmpColorPow);

                float3 ampCol = lerp(_AmpColorMin.rgb, _AmpColorMax.rgb, ampT);
                float3 baseCol = lerp(float3(1,1,1), ampCol, _AmpColorStrength);

                // Contours
                float x = hRaw * _ContourFreq;
                float contourMask = IsoLinesMid(x, _StrokeWidthPx, _AAMult);

                // Debug ring (based on excite mask)
                float ringMask = 0.0;
                if (_ShowExcite > 0.5)
                {
                    ringMask = IsoRing(e, _ExciteLevel, _ExciteStrokeWidthPx, _AAMult) * _ExciteOpacity;
                }

                // Pierce-only color: only where (a) excitation exists and (b) height exceeds threshold
                float pierceT = saturate((hRaw - _PierceHeight) / max(_PierceFeather, 1e-5));
                pierceT = pow(max(pierceT, 1e-5), _PierceColorPow);

                float pierceGate = pierceT * e; // IMPORTANT: limited to excitation regions
                float3 pierceCol = lerp(_PierceColorMin.rgb, _PierceColorMax.rgb, pierceT);

                float3 col = baseCol;
                col = lerp(col, pierceCol, saturate(pierceGate * _PierceColorStrength));

                // Only lines get alpha (no sheet fill)
                float mask = saturate(max(contourMask, ringMask));

                return half4(col, mask * _Opacity);
            }
            ENDHLSL
        }
    }
}