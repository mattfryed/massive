Shader "MASSIVE/Cosmos/Supernova Flash"
{
    Properties
    {
        [HDR] _Color ("Flash Color", Color) = (1, .67, .25, 1)
        _Brightness ("Emission", Float) = 4
        _Phase ("Burst Phase (-1 Before Burst)", Range(-1,1)) = 0
        _Glow ("Localized Glow", Float) = 0
        _Superluminous ("Superluminous", Float) = 0
        [HDR] _ShellColor ("Outer Shell Color", Color) = (1, .42, .08, 1)
        _ShellDetail ("Shell Detail", Range(0,1)) = .7
        _BurstSeed ("Burst Seed", Float) = 0
        _CloudExpansion ("Cloud Expansion", Range(0,1)) = 0
        _CloudOpacity ("Cloud Opacity", Range(0,1)) = 0
        _CloudDistortion ("Cloud Distortion", Range(0,1)) = .85
        _TelegraphDistortion ("Telegraph Distortion", Range(0,1)) = .85
        _FlowTime ("Internal Flow Time", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off ZTest LEqual Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            float4 _Color, _ShellColor;
            float _Brightness, _Phase, _Glow, _Superluminous, _ShellDetail, _BurstSeed;
            float _CloudExpansion, _CloudOpacity, _CloudDistortion, _TelegraphDistortion, _FlowTime;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata v)
            {
                v2f o; o.position = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o;
            }
            float Noise(float2 p)
            {
                float2 cell = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                float4 h = frac(sin(float4(dot(cell, float2(127.1,311.7)),
                    dot(cell + float2(1,0), float2(127.1,311.7)),
                    dot(cell + float2(0,1), float2(127.1,311.7)),
                    dot(cell + 1, float2(127.1,311.7)))) * 43758.5453);
                return lerp(lerp(h.x,h.y,f.x), lerp(h.z,h.w,f.x), f.y);
            }
            float Fractal(float2 p)
            {
                return .55 * Noise(p) + .3 * Noise(float2(p.x + p.y, p.y - p.x) * 1.61 + 23.7)
                    + .15 * Noise(p * 4.13 - 11.2);
            }
            float2 Warp(float2 p, float t)
            {
                return float2(Noise(p + float2(t, -t * .63)),
                    Noise(p.yx + float2(-t * .47, t * .81) + 39.2)) - .5;
            }
            float4 frag(v2f i) : SV_Target
            {
                float r2 = dot(i.uv, i.uv); clip(1 - r2);
                float phase = saturate(_Phase), r = sqrt(r2);
                float core = step(r, lerp(.2, .015, phase));
                float ringRadius = lerp(.12, .95, sqrt(phase));
                float ring = step(abs(r - ringRadius), lerp(.055, .008, phase));
                // The localized glow can rise and flicker before any burst geometry appears.
                // It has zero intensity at the quad boundary and fades continuously after peak.
                float glow = max(0, exp(-r2 * 10) - exp(-10)) * max(0, _Glow);
                float light = (1 - phase) * (1 - phase) * step(0, _Phase);
                float3 color = lerp(_Color.rgb, float3(1, 1, .95), core * .85);
                float3 emission = color * max(core, ring) * light + lerp(_Color.rgb, 1, .12) * glow;
                if (_Superluminous > .5)
                {
                    // Each event has its own orientation and smoothly advected gas field.
                    float angle = _BurstSeed * 2.39996;
                    float cs = cos(angle), sn = sin(angle);
                    float2 uv = float2(i.uv.x * cs - i.uv.y * sn, i.uv.x * sn + i.uv.y * cs);
                    float2 seed = float2(_BurstSeed * .173, _BurstSeed * .319);
                    float2 flow = float2(_FlowTime * .21, -_FlowTime * .13);
                    float edge = 1 - smoothstep(.82, 1, r);

                    // Distorted, mottled precursor gas: off-center lobes and wisps instead
                    // of an isotropic point light. Noise evolves continuously across peak.
                    float2 precursor = uv * lerp(float2(1,1), float2(.83,1.16), _TelegraphDistortion);
                    precursor += Warp(uv * 4.5 + seed, _FlowTime) * .6 * _TelegraphDistortion;
                    float gas = Fractal(precursor * 10 + seed + flow);
                    float halo = exp(-dot(precursor, precursor) * 10) *
                        lerp(1, .18 + 1.9 * smoothstep(.24,.77,gas), _TelegraphDistortion);
                    float wisps = exp(-r2 * 7) * pow(saturate(1 - abs(gas * 2 - 1)), 5) * .18 * _TelegraphDistortion;
                    emission = _Color.rgb * (halo + wisps) * max(0, _Glow) * .62;
                    float hotCore = exp(-dot(precursor, precursor) * lerp(95,650,phase));
                    emission += float3(.88,.97,1) * hotCore * pow(1 - phase,4) * step(0,_Phase) * 1.4;

                    [branch] if (_CloudOpacity > 0)
                    {
                        // An expanding volume of billows, not nested radial shells. Domain
                        // warping mixes warm/cool material throughout the cloud interior.
                        float expansion = saturate(_CloudExpansion);
                        float radius = lerp(.14,.78,pow(expansion,.72));
                        float2 q = uv / radius;
                        float2 warp = Warp(q * 2.3 + seed, _FlowTime * .7);
                        float2 billow = q * float2(.91,1.08) + warp * 1.15 * _CloudDistortion;
                        float density = Fractal(billow * 3.7 + seed + flow);
                        float detail = Fractal(billow * 11.3 - seed - flow * 1.4);
                        float envelope = 1 - smoothstep(.24,1.13,length(billow));
                        float clumps = smoothstep(.25,.78,density);
                        float filaments = pow(saturate(1 - abs(detail * 2 - 1)), 4);
                        float cloud = envelope * (.15 + .85 * clumps) *
                            lerp(1,.3 + filaments * 1.25,_ShellDetail);
                        float warm = smoothstep(.28,.7,density + warp.y * .35 + billow.x * .08);
                        float3 gasColor = lerp(_Color.rgb, _ShellColor.rgb, warm);
                        // Dilution as the volume grows, plus a separately controlled dissolve.
                        float fade = saturate(_CloudOpacity);
                        cloud *= smoothstep((1 - fade) * .48, (1 - fade) * .48 + .22, density) * fade;
                        emission += gasColor * cloud * lerp(.5,.26,expansion);
                    }
                    emission *= edge;
                }
                return float4(emission * _Brightness, 0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
