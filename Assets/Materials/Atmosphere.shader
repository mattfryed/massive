Shader "MASSIVE/Goopy Dissolve Shell (Built-in, FerroFluid Procedural)"
{
    Properties
    {
        // Look
        _Color          ("Goop Color", Color) = (1,1,1,1)
        _EdgeColor      ("Hole Edge Color", Color) = (1,1,1,1)
        _EdgeIntensity  ("Edge Intensity", Range(0,5)) = 1.0
        _EdgeWidth      ("Edge Width", Range(0.001,0.25)) = 0.05

        // Holes (alpha cutout)
        _Cutoff         ("Hole Cutoff", Range(0,1)) = 0.48
        _Feather        ("Hole Feather (AA)", Range(0.0001,0.1)) = 0.015
        [Toggle] _InvertHoles ("Invert Holes", Float) = 0

        // Main noise domain + motion
        _Scale          ("Noise Scale", Range(0.05,50)) = 6.0
        _Scroll         ("Scroll XYZ", Vector) = (0.18, 0.0, 0.09, 0)

        // Detail
        _DetailScale    ("Detail Scale", Range(0.05,200)) = 24.0
        _DetailWeight   ("Detail Weight", Range(0,1)) = 0.35
        _DetailScroll   ("Detail Scroll XYZ", Vector) = (-0.09, 0.0, 0.13, 0)

        // Domain warp (THIS is where “fluid” comes from)
        _WarpScale      ("Warp Scale", Range(0.05,50)) = 1.8
        _WarpStrength   ("Warp Strength", Range(0,3)) = 0.95
        _WarpScroll     ("Warp Scroll XYZ", Vector) = (0.10, 0.0, -0.08, 0)
        _WarpIters      ("Warp Iterations", Range(0,3)) = 2

        // FBM knobs
        _Octaves        ("FBM Octaves", Range(1,8)) = 5
        _Lacunarity     ("FBM Lacunarity", Range(1.2,3.5)) = 2.0
        _Gain           ("FBM Gain", Range(0.2,0.9)) = 0.5

        // “Ferrofluid” shaping
        _RidgedAmount   ("Ridged Amount", Range(0,1)) = 0.75
        _RidgedPower    ("Ridged Power", Range(0.5,6)) = 2.2

        // Optional swirl (adds rotational “flow”)
        _SwirlStrength  ("Swirl Strength", Range(0,2)) = 0.45
        _SwirlScale     ("Swirl Scale", Range(0.05,20)) = 1.2

        // Rim read
        _RimIntensity   ("Rim Intensity", Range(0,3)) = 0.35
        _RimPower       ("Rim Power", Range(0.5,12)) = 4.0

        // Shell handling
        _ShellOffset    ("Shell Inflate (object units)", Range(-0.05,0.05)) = 0.003
        [Enum(Off,0,Front,1,Back,2)] _Cull ("Cull", Float) = 0

        // Mapping
        [Toggle] _WorldSpace ("World Space Mapping", Float) = 0
        _TriSharpness   ("Triplanar Sharpness", Range(1,16)) = 2.2
    }

    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Cull [_Cull]
        ZWrite On
        Blend Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color, _EdgeColor;
            float _EdgeIntensity, _EdgeWidth;

            float _Cutoff, _Feather, _InvertHoles;

            float _Scale;
            float4 _Scroll;

            float _DetailScale, _DetailWeight;
            float4 _DetailScroll;

            float _WarpScale, _WarpStrength;
            float4 _WarpScroll;
            float _WarpIters;

            float _Octaves, _Lacunarity, _Gain;

            float _RidgedAmount, _RidgedPower;

            float _SwirlStrength, _SwirlScale;

            float _RimIntensity, _RimPower;

            float _ShellOffset;

            float _WorldSpace, _TriSharpness;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float3 posWS    : TEXCOORD0;
                float3 nWS      : TEXCOORD1;
                float4 screenPos: TEXCOORD2;
                float3 viewDirWS: TEXCOORD3;
            };

            // ----------------- Triplanar weights -----------------
            float3 TriWeights(float3 n, float sharpness)
            {
                float3 a = abs(n);
                a = pow(a, sharpness);
                return a / max(1e-5, (a.x + a.y + a.z));
            }

            // ----------------- Gradient Perlin-ish 3D noise -----------------
            float3 hash33(float3 p)
            {
                // stable hash -> 0..1
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.xxy + p.yzz) * p.zyx);
            }

            float fade(float t) { return t * t * t * (t * (t * 6 - 15) + 10); }

            float gradNoise3(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);

                float3 u = float3(fade(f.x), fade(f.y), fade(f.z));

                // gradients at corners (not normalized – good enough + cheaper)
                float3 g000 = hash33(i + float3(0,0,0)) * 2 - 1;
                float3 g100 = hash33(i + float3(1,0,0)) * 2 - 1;
                float3 g010 = hash33(i + float3(0,1,0)) * 2 - 1;
                float3 g110 = hash33(i + float3(1,1,0)) * 2 - 1;
                float3 g001 = hash33(i + float3(0,0,1)) * 2 - 1;
                float3 g101 = hash33(i + float3(1,0,1)) * 2 - 1;
                float3 g011 = hash33(i + float3(0,1,1)) * 2 - 1;
                float3 g111 = hash33(i + float3(1,1,1)) * 2 - 1;

                float n000 = dot(g000, f - float3(0,0,0));
                float n100 = dot(g100, f - float3(1,0,0));
                float n010 = dot(g010, f - float3(0,1,0));
                float n110 = dot(g110, f - float3(1,1,0));
                float n001 = dot(g001, f - float3(0,0,1));
                float n101 = dot(g101, f - float3(1,0,1));
                float n011 = dot(g011, f - float3(0,1,1));
                float n111 = dot(g111, f - float3(1,1,1));

                float nx00 = lerp(n000, n100, u.x);
                float nx10 = lerp(n010, n110, u.x);
                float nx01 = lerp(n001, n101, u.x);
                float nx11 = lerp(n011, n111, u.x);

                float nxy0 = lerp(nx00, nx10, u.y);
                float nxy1 = lerp(nx01, nx11, u.y);

                float nxyz = lerp(nxy0, nxy1, u.z);

                // map approx -1..1 -> 0..1
                return nxyz * 0.5 + 0.5;
            }

            // rotate domain each octave to kill axis-aligned artifacts
            float3 rotateDomain(float3 p)
            {
                const float3x3 M = float3x3(
                     0.00,  0.80,  0.60,
                    -0.80,  0.36, -0.48,
                    -0.60, -0.48,  0.64
                );
                return mul(M, p);
            }

            float fbm3(float3 p, float octaves, float lacunarity, float gain)
            {
                float amp = 0.5;
                float freq = 1.0;
                float sum = 0.0;

                [unroll] for (int o = 0; o < 8; o++)
                {
                    if (o >= (int)octaves) break;
                    sum += amp * gradNoise3(p * freq);
                    p = rotateDomain(p);
                    freq *= lacunarity;
                    amp *= gain;
                }
                return sum; // ~0..1
            }

            // Ridged shaping for “ferrofluid” lumps
            float ridged(float x, float power)
            {
                // x 0..1 -> ridges 0..1
                float r = 1.0 - abs(x * 2.0 - 1.0);
                r = pow(saturate(r), power);
                return r;
            }

            float triFbm(float3 p, float3 n, float scale, float oct, float lac, float gain)
            {
                float3 w = TriWeights(n, _TriSharpness);

                // true 3D domains per axis (no “fake 2.5D” slices)
                float sx = fbm3(float3(p.z, p.y, p.x) * scale, oct, lac, gain);
                float sy = fbm3(float3(p.x, p.z, p.y) * scale, oct, lac, gain);
                float sz = fbm3(float3(p.x, p.y, p.z) * scale, oct, lac, gain);

                return sx * w.x + sy * w.y + sz * w.z;
            }

            float2 rot2(float2 v, float a)
            {
                float s = sin(a), c = cos(a);
                return float2(c*v.x - s*v.y, s*v.x + c*v.y);
            }

            float3 warpVector(float3 p, float3 n, float t)
            {
                // 3 decorrelated fields -> vector warp
                float wx = triFbm(p + float3(17.1,  3.7,  9.2) + _WarpScroll.xyz * t, n, _WarpScale, 3, 2.0, 0.5);
                float wy = triFbm(p + float3( 5.9, 21.3, 11.4) + _WarpScroll.xyz * t, n, _WarpScale, 3, 2.0, 0.5);
                float wz = triFbm(p + float3(13.6,  7.1, 19.8) + _WarpScroll.xyz * t, n, _WarpScale, 3, 2.0, 0.5);
                return (float3(wx, wy, wz) * 2.0 - 1.0);
            }

            v2f vert(appdata v)
            {
                v2f o;

                float3 posOS = v.vertex.xyz + v.normal * _ShellOffset;
                float4 posWS4 = mul(unity_ObjectToWorld, float4(posOS, 1));
                o.posWS = posWS4.xyz;

                o.pos = UnityObjectToClipPos(float4(posOS, 1));
                o.nWS = UnityObjectToWorldNormal(v.normal);

                o.screenPos = ComputeScreenPos(o.pos);
                o.viewDirWS = normalize(_WorldSpaceCameraPos.xyz - o.posWS);

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.nWS);
                float t = _Time.y;

                float3 p = (_WorldSpace > 0.5) ? i.posWS : mul(unity_WorldToObject, float4(i.posWS,1)).xyz;

                // optional swirl around Y (helps “flow”)
                if (_SwirlStrength > 0.0001)
                {
                    float a = (triFbm(p + _Scroll.xyz * t, n, _SwirlScale, 3, 2.0, 0.5) - 0.5) * 2.0;
                    p.xz = rot2(p.xz, a * _SwirlStrength);
                }

                // domain warp iterations (biggest contributor to fluid look)
                float3 pw = p;
                int iters = (int)_WarpIters;
                for (int k = 0; k < 3; k++)
                {
                    if (k >= iters) break;
                    float3 wv = warpVector(pw, n, t);
                    pw += wv * _WarpStrength;
                }

                // main + detail fields
                float mainN   = triFbm(pw + _Scroll.xyz * t, n, _Scale, _Octaves, _Lacunarity, _Gain);
                float detailN = triFbm(pw + _DetailScroll.xyz * t, n, _DetailScale, max(1, _Octaves - 2), _Lacunarity, _Gain);

                float field = lerp(mainN, saturate(mainN + (detailN - 0.5) * 2.0), _DetailWeight);

                // ferrofluid-ish shaping (ridged blend)
                float r = ridged(field, _RidgedPower);
                field = lerp(field, r, _RidgedAmount);

                if (_InvertHoles > 0.5) field = 1.0 - field;

                // Cutout with a tiny AA feather (keeps hard B/W without jaggies)
                float aaw = max(_Feather, fwidth(field) * 0.75);
                float alpha = smoothstep(_Cutoff - aaw, _Cutoff + aaw, field);
                clip(alpha - 0.5);

                // Edge band around holes
                float e0 = smoothstep(_Cutoff - aaw, _Cutoff + aaw, field);
                float e1 = smoothstep((_Cutoff + _EdgeWidth) - aaw, (_Cutoff + _EdgeWidth) + aaw, field);
                float edge = saturate(e0 - e1);

                // Rim
                float rim = pow(1.0 - saturate(dot(n, normalize(i.viewDirWS))), _RimPower) * _RimIntensity;

                float3 col = _Color.rgb;
                col += _EdgeColor.rgb * edge * _EdgeIntensity;
                col += rim;

                return fixed4(saturate(col), 1);
            }
            ENDCG
        }
    }
}