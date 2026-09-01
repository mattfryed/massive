Shader "MASSIVE/HIGGS/IconStackedTopo_URP"
{
    Properties
    {
        _HiggsHeight ("Higgs Height (RFloat)", 2D) = "black" {}
        _HiggsExcite ("Higgs Excite (RFloat)", 2D) = "black" {}

        _Opacity     ("Line Opacity", Range(0,1)) = 0.9

        [Header(Slice Range)]
        _HeightMin   ("Height Min raw", Float) = -0.25
        _HeightMax   ("Height Max raw", Float) = 0.25

        [Header(Line)]
        _StrokeWidthPx ("Stroke Width px-ish", Range(0.25, 6)) = 1.25
        _AAMult        ("AA Mult", Range(0.5, 4)) = 1.25

        [Header(Color Slice Gradient)]
        _ColorMin ("Color Min", Color) = (1,1,1,1)
        _ColorMax ("Color Max", Color) = (1,1,1,1)
        _ColorStrength ("Color Strength", Range(0,1)) = 0.0
        _ColorPow ("Color Curve", Range(0.1, 4)) = 1.0

        [Header(Pierce Highlight)]
        _PierceHeight ("Pierce Height raw", Float) = 0.0
        _PierceFeather ("Pierce Feather", Range(0.001, 2)) = 0.25
        _PierceColorMin ("Pierce Color Min", Color) = (1,1,1,1)
        _PierceColorMax ("Pierce Color Max", Color) = (1,1,1,1)
        _PierceStrength ("Pierce Strength", Range(0,1)) = 0.75
        _PiercePow ("Pierce Curve", Range(0.1, 4)) = 1.0

        // IMPORTANT: these must exist as ShaderLab properties so MaterialPropertyBlock can set them.
        [HideInInspector] _Slice01 ("Slice01", Float) = 0
        [HideInInspector] _LayerAlpha ("LayerAlpha", Float) = 1
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

            TEXTURE2D(_HiggsHeight); SAMPLER(sampler_HiggsHeight);
            TEXTURE2D(_HiggsExcite); SAMPLER(sampler_HiggsExcite);

            float _Opacity;

            float _HeightMin;
            float _HeightMax;

            float _StrokeWidthPx;
            float _AAMult;

            float4 _ColorMin;
            float4 _ColorMax;
            float _ColorStrength;
            float _ColorPow;

            float _PierceHeight;
            float _PierceFeather;
            float4 _PierceColorMin;
            float4 _PierceColorMax;
            float _PierceStrength;
            float _PiercePow;

            // Now backed by real ShaderLab properties
            float _Slice01;
            float _LayerAlpha;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            float SampleH(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_HiggsHeight, sampler_HiggsHeight, uv).r;
            }

            float SampleE(float2 uv)
            {
                return saturate(SAMPLE_TEXTURE2D(_HiggsExcite, sampler_HiggsExcite, uv).r);
            }

            float IsoSliceMask(float h, float level, float strokeWidthPx, float aaMult)
            {
                float d  = abs(h - level);
                float fw = fwidth(h) * aaMult;
                float w  = max(1e-5, strokeWidthPx * fw);
                return 1.0 - smoothstep(w, w + fw, d);
            }

            half4 frag (Varyings i) : SV_Target
            {
                float2 uv = i.uv;

                float h = SampleH(uv);
                float e = SampleE(uv);

                float level = lerp(_HeightMin, _HeightMax, saturate(_Slice01));
                float mask  = IsoSliceMask(h, level, _StrokeWidthPx, _AAMult);

                float t = pow(max(saturate(_Slice01), 1e-5), _ColorPow);
                float3 gradCol = lerp(_ColorMin.rgb, _ColorMax.rgb, t);
                float3 col = lerp(float3(1,1,1), gradCol, _ColorStrength);

                float pierceGate = smoothstep(_PierceHeight, _PierceHeight + max(_PierceFeather, 1e-5), h);
                pierceGate = pow(max(pierceGate, 1e-5), _PiercePow) * e;

                float3 pierceCol = lerp(_PierceColorMin.rgb, _PierceColorMax.rgb, t);
                col = lerp(col, pierceCol, saturate(pierceGate * _PierceStrength));

                float a = mask * _Opacity * saturate(_LayerAlpha);

                return half4(col, a);
            }
            ENDHLSL
        }
    }
}
