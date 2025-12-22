Shader "MASSIVE/Goopy Shell (Built-in, Liquid Membranes v2)"
{
    Properties
    {
        _Color          ("Goop Color", Color) = (1,1,1,1)

        // Coverage / Cutout
        _Cutoff         ("Cutoff (Coverage)", Range(0,1)) = 0.50
        _EdgeAAStrength ("Edge AA Strength", Range(0.25,4)) = 1.5
        _MinAAPixels    ("Min AA (pixels)", Range(0,3)) = 1.0
        [Toggle] _Invert ("Invert (swap holes/goop)", Float) = 0

        // Membranes: Large layer
        _CellScale      ("Cell Scale (Large)", Range(0.5,30)) = 5.5
        _MembraneWidth  ("Membrane Width (Large)", Range(0.001,0.35)) = 0.13
        _MembraneSoft   ("Membrane Softness (Large)", Range(0.0001,0.2)) = 0.035

        // Membranes: Small layer
        _CellScale2     ("Cell Scale (Small)", Range(0.5,80)) = 14.0
        _MembraneWidth2 ("Membrane Width (Small)", Range(0.001,0.35)) = 0.08
        _MembraneSoft2  ("Membrane Softness (Small)", Range(0.0001,0.2)) = 0.03
        _SmallWeight    ("Small Layer Weight", Range(0,1)) = 0.45

        _Jitter         ("Cell Jitter", Range(0,1)) = 1.0

        // Flow / liquid motion
        _FlowScale      ("Flow Scale", Range(0.2,20)) = 1.6
        _FlowStrength   ("Flow Strength", Range(0,3)) = 1.25
        _FlowSpeed      ("Flow Speed XYZ", Vector) = (0.12, 0.00, 0.08, 0)
        _FlowIters      ("Flow Iterations", Range(0,3)) = 2

        // Shell handling
        _ShellOffset    ("Shell Inflate (object units)", Range(-0.05,0.05)) = 0.003
        [Enum(Off,0,Front,1,Back,2)] _Cull ("Cull", Float) = 0
        [Toggle] _WorldSpace ("World Space Mapping", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Cull [_Cull]
        ZWrite On
        Blend Off

        // Key for smooth cutout edges with MSAA:
        AlphaToMask On

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;

            float _Cutoff, _EdgeAAStrength, _MinAAPixels, _Invert;

            float _CellScale, _MembraneWidth, _MembraneSoft;
            float _CellScale2, _MembraneWidth2, _MembraneSoft2, _SmallWeight;
            float _Jitter;

            float _FlowScale, _FlowStrength;
            float4 _FlowSpeed;
            float _FlowIters;

            float _ShellOffset;
            float _WorldSpace;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float3 posWS : TEXCOORD0;
                float3 nWS   : TEXCOORD1;
            };

            float3 hash33(float3 p)
            {
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

                return lerp(nxy0, nxy1, u.z); // ~ -1..1
            }

            float3 flowVec(float3 p)
            {
                float nx = gradNoise3(p + float3(17.1, 3.7, 9.2));
                float ny = gradNoise3(p + float3(5.9, 21.3, 11.4));
                float nz = gradNoise3(p + float3(13.6, 7.1, 19.8));
                return float3(nx, ny, nz);
            }

            // Worley F1/F2
            float2 worleyF1F2(float3 p, float jitter)
            {
                float3 ip = floor(p);
                float3 fp = frac(p);

                float min1 = 1e9;
                float min2 = 1e9;

                [unroll] for (int z = -1; z <= 1; z++)
                [unroll] for (int y = -1; y <= 1; y++)
                [unroll] for (int x = -1; x <= 1; x++)
                {
                    float3 cell = ip + float3(x,y,z);
                    float3 rnd = hash33(cell);
                    float3 feature = float3(x,y,z) + lerp(0.5, rnd, jitter);

                    float3 d = feature - fp;
                    float dsq = dot(d,d);

                    if (dsq < min1) { min2 = min1; min1 = dsq; }
                    else if (dsq < min2) { min2 = dsq; }
                }

                return float2(sqrt(min1), sqrt(min2));
            }

            float membraneMask(float3 p, float scale, float jitter, float width, float soft, float aaBoost)
            {
                float2 f12 = worleyF1F2(p * scale, jitter);
                float edgeMetric = f12.y - f12.x; // borders small
                float aa = max(1e-5, fwidth(edgeMetric) * aaBoost);
                return 1.0 - smoothstep(width - soft - aa, width + soft + aa, edgeMetric);
            }

            v2f vert(appdata v)
            {
                v2f o;
                float3 posOS = v.vertex.xyz + v.normal * _ShellOffset;
                float4 posWS4 = mul(unity_ObjectToWorld, float4(posOS, 1));
                o.posWS = posWS4.xyz;
                o.nWS = UnityObjectToWorldNormal(v.normal);
                o.pos = UnityObjectToClipPos(float4(posOS, 1));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y;

                // Use direction as sphere domain (seamless)
                float3 p = (_WorldSpace > 0.5)
                    ? normalize(i.posWS)
                    : normalize(mul(unity_WorldToObject, float4(i.posWS,1)).xyz);

                // Flow warp
                float3 pw = p * _FlowScale + (_FlowSpeed.xyz * t);
                int iters = (int)_FlowIters;
                [unroll] for (int k = 0; k < 3; k++)
                {
                    if (k >= iters) break;
                    pw += flowVec(pw) * _FlowStrength;
                }

                // Layered membranes (large + small)
                float aaBoost = _EdgeAAStrength;

                float m1 = membraneMask(pw, _CellScale,  _Jitter, _MembraneWidth,  _MembraneSoft,  aaBoost);
                float m2 = membraneMask(pw, _CellScale2, _Jitter, _MembraneWidth2, _MembraneSoft2, aaBoost);

                float field = saturate( max(m1, m2 * _SmallWeight) );

                if (_Invert > 0.5) field = 1.0 - field;

                // Convert to coverage alpha (AlphaToMask will MSAA-smooth this)
                float minAA = (_MinAAPixels / max(1.0, _ScreenParams.y)); // ~pixels -> normalized-ish
                float aa = max(fwidth(field) * _EdgeAAStrength, minAA);
                float coverage = smoothstep(_Cutoff - aa, _Cutoff + aa, field);

                // Optional tiny discard for performance (keeps most edges for coverage AA)
                clip(coverage - 0.01);

                return fixed4(_Color.rgb, coverage);
            }
            ENDCG
        }
    }
}