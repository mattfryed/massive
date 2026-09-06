Shader "MASSIVE/Amplifier Core/Aurora Volume"
{
    Properties
    {
        _ColorA ("Spectral Color A", Color) = (1.0, 0.08, 0.72, 1.0)
        _ColorB ("Spectral Color B", Color) = (0.02, 0.78, 1.0, 1.0)
        _ColorC ("Spectral Color C", Color) = (0.55, 1.0, 0.12, 1.0)
        _Density ("Cloud Density", Range(0.1, 4.0)) = 1.45
        _NoiseScale ("Noise Scale", Range(0.5, 10.0)) = 3.8
        _NoiseThreshold ("Noise Threshold", Range(0.0, 1.0)) = 0.42
        _WarpStrength ("Organic Warp", Range(0.0, 2.0)) = 0.72
        _FlowSpeed ("Internal Flow", Range(0.0, 3.0)) = 0.68
        _Brightness ("Energy Brightness", Range(0.1, 6.0)) = 1.65
        _Opacity ("Lifecycle Opacity", Range(0.0, 1.0)) = 1.0
        _BlastProgress ("Blast Progress", Range(0.0, 1.0)) = 0.0
        _Seed ("Variation Seed", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent+5"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        Blend One OneMinusSrcAlpha
        Cull Back
        Lighting Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            fixed4 _ColorA;
            fixed4 _ColorB;
            fixed4 _ColorC;
            float _Density;
            float _NoiseScale;
            float _NoiseThreshold;
            float _WarpStrength;
            float _FlowSpeed;
            float _Brightness;
            float _Opacity;
            float _BlastProgress;
            float _Seed;

            struct AppData
            {
                float4 vertex : POSITION;
            };

            struct Interpolators
            {
                float4 position : SV_POSITION;
                float3 objectPosition : TEXCOORD0;
                float3 worldPosition : TEXCOORD1;
            };

            Interpolators Vert(AppData input)
            {
                Interpolators output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.objectPosition = input.vertex.xyz;
                output.worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
                return output;
            }

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float ValueNoise(float3 p)
            {
                float3 cell = floor(p);
                float3 local = frac(p);
                local = local * local * (3.0 - 2.0 * local);

                float n000 = Hash31(cell + float3(0, 0, 0));
                float n100 = Hash31(cell + float3(1, 0, 0));
                float n010 = Hash31(cell + float3(0, 1, 0));
                float n110 = Hash31(cell + float3(1, 1, 0));
                float n001 = Hash31(cell + float3(0, 0, 1));
                float n101 = Hash31(cell + float3(1, 0, 1));
                float n011 = Hash31(cell + float3(0, 1, 1));
                float n111 = Hash31(cell + float3(1, 1, 1));

                float n00 = lerp(n000, n100, local.x);
                float n10 = lerp(n010, n110, local.x);
                float n01 = lerp(n001, n101, local.x);
                float n11 = lerp(n011, n111, local.x);
                float n0 = lerp(n00, n10, local.y);
                float n1 = lerp(n01, n11, local.y);
                return lerp(n0, n1, local.z);
            }

            float Fbm(float3 p)
            {
                float value = 0.0;
                float weight = 0.58;
                value += ValueNoise(p) * weight;
                p = p * 2.03 + float3(7.1, 3.7, 5.4);
                weight *= 0.52;
                value += ValueNoise(p) * weight;
                p = p * 2.11 + float3(2.4, 8.3, 1.9);
                weight *= 0.52;
                value += ValueNoise(p) * weight;
                return value / 0.96;
            }

            float3 SpectralColor(float phase, float detail)
            {
                float cycle = frac(phase);
                float3 first = lerp(_ColorA.rgb, _ColorB.rgb, smoothstep(0.0, 0.5, cycle));
                float3 second = lerp(_ColorB.rgb, _ColorC.rgb, smoothstep(0.5, 1.0, cycle));
                float3 authored = lerp(first, second, step(0.5, cycle));
                float3 prism = 0.5 + 0.5 * cos(
                    6.2831853 * (cycle + float3(0.0, 0.34, 0.67)));
                return lerp(authored, prism, 0.28 + detail * 0.18);
            }

            fixed4 Frag(Interpolators input) : SV_Target
            {
                float3 rayDirectionWS = normalize(input.worldPosition - _WorldSpaceCameraPos);
                float3 rayDirectionOS = normalize(
                    mul((float3x3)unity_WorldToObject, rayDirectionWS));
                float3 samplePosition = input.objectPosition + rayDirectionOS * 0.012;
                const float stepLength = 0.058;

                float3 accumulatedColor = 0.0;
                float accumulatedAlpha = 0.0;
                float flowTime = _Time.y * _FlowSpeed;
                float3 seedOffset = float3(_Seed * 1.17, _Seed * 2.31, _Seed * 0.73);

                [unroll]
                for (int stepIndex = 0; stepIndex < 18; stepIndex++)
                {
                    float radius01 = length(samplePosition) * 2.0;
                    if (radius01 > 1.02)
                    {
                        samplePosition += rayDirectionOS * stepLength;
                        continue;
                    }

                    float edgeFade = saturate(1.0 - radius01 * radius01);
                    float3 flow = float3(
                        flowTime * 0.23,
                        -flowTime * 0.17,
                        flowTime * 0.29);
                    float broadNoise = Fbm(
                        samplePosition * (_NoiseScale * 0.58) + flow + seedOffset);
                    float3 warpedPosition = samplePosition;
                    warpedPosition += (broadNoise - 0.5) * _WarpStrength;
                    warpedPosition += normalize(samplePosition + 0.001) *
                                      (_BlastProgress * 0.22 * broadNoise);

                    float detailNoise = Fbm(
                        warpedPosition * _NoiseScale -
                        flow * 1.37 +
                        seedOffset.yzx);
                    float filament = smoothstep(0.56, 0.86, detailNoise);
                    float cloud = smoothstep(
                        _NoiseThreshold,
                        min(0.98, _NoiseThreshold + 0.34),
                        detailNoise);
                    float density = saturate(cloud * 0.72 + filament * 0.55);
                    density *= pow(edgeFade, 0.72);
                    density *= _Density * stepLength * _Opacity;

                    float phase =
                        detailNoise * 0.72 +
                        broadNoise * 0.41 +
                        dot(samplePosition, float3(0.37, 0.21, 0.43)) +
                        _Seed * 0.11 +
                        flowTime * 0.025;
                    float3 sampleColor = SpectralColor(phase, filament);
                    sampleColor *= _Brightness * (0.62 + filament * 0.72);

                    float contribution = density * (1.0 - accumulatedAlpha);
                    accumulatedColor += sampleColor * contribution;
                    accumulatedAlpha += contribution * 0.86;
                    samplePosition += rayDirectionOS * stepLength;
                }

                accumulatedAlpha = saturate(accumulatedAlpha);
                clip(accumulatedAlpha - 0.003);
                return fixed4(accumulatedColor, accumulatedAlpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
