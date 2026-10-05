Shader "MASSIVE/Cosmos/CosmicWebParticles"
{
    SubShader
    {
        Tags { "Queue"="Transparent-100" "RenderType"="Transparent" }
        Pass
        {
            Blend One One
            ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "CosmicWebMotion.hlsl"
            StructuredBuffer<Particle> _Particles;
            float4x4 _GridToWorld;
            float4 _Oval, _WorldScale, _Blue, _Pink, _EarlyHeat;
            float4 _HeatTransition, _HeatCoverage, _HeatBlue, _HeatCyan, _HeatGold, _HeatRed;
            float _Age, _Padding, _Depth, _Brightness, _GalaxySize, _EvolutionVariation, _EdgeCondensation;
            float _MotionTime, _DriftStrength, _ClusterTurbulence;
            float _BrightnessVariance, _EdgeVignette, _LensingTransitionSmoothness;
            struct v2f
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 arenaPosition : TEXCOORD1;
                float4 light : TEXCOORD2;
            };
            float Hash(float x) { return frac(sin(x * 12.9898 + 78.233) * 43758.5453); }
            float HeatColorCoverage(float temperature, float coverage, float feather)
            {
                if (coverage <= 0) return 0;
                if (coverage >= 1) return 1;
                return smoothstep(1 - coverage - feather, 1 - coverage + feather, temperature);
            }
            float3 EarlyHeatColor(Particle p)
            {
                // Color follows the original matter parcels. Depth-independent sampling
                // keeps overlapping tracers in the same patch from washing out to white.
                float offset = _EarlyHeat.z * .017;
                float3 q = float3(p.initial.xy * float2(18, 6.5), 0)
                    + float3(offset + 13.7, offset * .71 + 37.1, 19.3);
                q += _MotionTime * float3(.006, -.004, .002);
                float mottling = .60 * WebTurbulenceNoise(q * .85)
                    + .28 * WebTurbulenceNoise(q * 2.6 + 17.3)
                    + .12 * WebTurbulenceNoise(q * 8.2 - 11.7);
                float temperature = saturate(.5 + mottling * 1.4);
                float3 color = lerp(_HeatBlue.rgb, _HeatCyan.rgb, HeatColorCoverage(temperature, _HeatCoverage.x, .14));
                color = lerp(color, _HeatGold.rgb, HeatColorCoverage(temperature, _HeatCoverage.y, .095));
                return lerp(color, _HeatRed.rgb, HeatColorCoverage(temperature, _HeatCoverage.z, .13));
            }
            v2f vert(uint vertex : SV_VertexID, uint instance : SV_InstanceID)
            {
                const float2 corners[6] = { float2(-1,-1), float2(-1,1), float2(1,1), float2(-1,-1), float2(1,1), float2(1,-1) };
                Particle p = _Particles[instance];
                float formation, evacuation;
                float3 center = WebPosition(p, _Age, _EvolutionVariation, _MotionTime, _DriftStrength,
                    _ClusterTurbulence, formation, evacuation);
                float knot = lerp(p.filament.w, p.cluster.w, evacuation) * formation;
                float haze = step(.83, Hash(instance + 1));
                float radius = _GalaxySize * lerp(.65, 1.8, Hash(instance + 7)) * lerp(1, 5.2, haze);
                radius *= lerp(1, .6, evacuation) * lerp(.8, 1.15, saturate(center.z * .5 + .5));
                float2 corner = corners[vertex];
                float2 projected = WebProject(center, _Oval, _WorldScale.xy, _Padding + .06, _EdgeCondensation, _LensingTransitionSmoothness);
                float2 arenaPosition = projected + corner * radius / _WorldScale.xy;
                // Thin visual depth slab fits between the existing ground and gameplay grid.
                float3 local = float3(arenaPosition, -_Depth + clamp(center.z, -1, 1) * .45);
                v2f o;
                o.position = mul(UNITY_MATRIX_VP, mul(_GridToWorld, float4(local, 1)));
                o.uv = corner;
                o.arenaPosition = arenaPosition * _WorldScale.xy;
                float3 color = lerp(_Blue.rgb, _Pink.rgb, knot * .85);
                color = lerp(color, float3(.7,.88,1), step(.985, Hash(instance + 17)) * .65);
                float cooling = _HeatTransition.y <= _HeatTransition.x
                    ? step(_HeatTransition.y, _Age) : smoothstep(_HeatTransition.x, _HeatTransition.y, _Age);
                float heat = saturate(_EarlyHeat.x) * (1 - cooling);
                // Color/light only; topology and motion do not depend on these controls.
                [branch] if (heat > 0) color = lerp(color, EarlyHeatColor(p), heat);
                float baseBrightness = max(0, lerp(.625, p.initial.w, _BrightnessVariance));
                float intensity = baseBrightness * lerp(.7, 1, formation) * lerp(1, .17, haze);
                intensity *= lerp(1, _EarlyHeat.y, heat);
                // Cluster crowding is additive; reduce per-particle flux late to retain pink cores.
                intensity *= lerp(1, .45, evacuation * knot);
                // Avoid additive saturation where the compact projection crowds outer tracers.
                intensity *= lerp(1, .4, smoothstep(.78, 1.3, length(center.xy)) * formation);
                o.light = float4(color * intensity * _Brightness, heat * haze);
                return o;
            }
            float4 frag(v2f i) : SV_Target
            {
                float2 ab = float2(_Oval.z, _Oval.y) * _WorldScale.xy;
                float k0 = length(i.arenaPosition / ab);
                float k1 = length(i.arenaPosition / (ab * ab));
                // Ellipse distance approximation is accurate close to the outline and uses world units.
                float inside = (1 - k0) * k0 / max(k1, 1e-5);
                if (k0 < .001) inside = min(ab.x, ab.y);
                float edge = inside - _Padding;
                clip(edge);
                float r2 = dot(i.uv, i.uv);
                clip(1 - r2);
                float glow = (exp(-r2 * 5.5) - exp(-5.5)) * .7 + exp(-r2 * 28) * .6;
                // Soften only the existing early haze within its unchanged footprint.
                // This adds a luminous soup between pinpoints without increasing overdraw.
                [branch] if (i.light.a > 0)
                {
                    float plasmaGlow = (exp(-r2 * 2.8) - exp(-2.8)) * .9;
                    glow = lerp(glow, plasmaGlow, i.light.a);
                }
                float edgeFade = saturate(edge / max(.00001, _EdgeVignette));
                return float4(i.light.rgb * glow * edgeFade, 0);
            }
            ENDHLSL
        }
    }
}
