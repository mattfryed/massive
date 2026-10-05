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
            float4 _Oval, _WorldScale, _Blue, _Pink;
            float _Age, _Padding, _Depth, _Brightness, _GalaxySize, _EvolutionVariation, _EdgeCondensation;
            struct v2f
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 arenaPosition : TEXCOORD1;
                float4 light : TEXCOORD2;
            };
            float Hash(float x) { return frac(sin(x * 12.9898 + 78.233) * 43758.5453); }
            v2f vert(uint vertex : SV_VertexID, uint instance : SV_InstanceID)
            {
                const float2 corners[6] = { float2(-1,-1), float2(-1,1), float2(1,1), float2(-1,-1), float2(1,1), float2(1,-1) };
                Particle p = _Particles[instance];
                float formation, evacuation;
                float3 center = WebPosition(p, _Age, _EvolutionVariation, formation, evacuation);
                float knot = lerp(p.filament.w, p.cluster.w, evacuation) * formation;
                float haze = step(.83, Hash(instance + 1));
                float radius = _GalaxySize * lerp(.65, 1.8, Hash(instance + 7)) * lerp(1, 5.2, haze);
                radius *= lerp(1, .6, evacuation) * lerp(.8, 1.15, saturate(center.z * .5 + .5));
                float2 corner = corners[vertex];
                float2 projected = WebProject(center, _Oval, _WorldScale.xy, _Padding + .06, _EdgeCondensation);
                float2 arenaPosition = projected + corner * radius / _WorldScale.xy;
                // Thin visual depth slab fits between the existing ground and gameplay grid.
                float3 local = float3(arenaPosition, -_Depth + clamp(center.z, -1, 1) * .45);
                v2f o;
                o.position = mul(UNITY_MATRIX_VP, mul(_GridToWorld, float4(local, 1)));
                o.uv = corner;
                o.arenaPosition = arenaPosition * _WorldScale.xy;
                float3 color = lerp(_Blue.rgb, _Pink.rgb, knot * .85);
                color = lerp(color, float3(.7,.88,1), step(.985, Hash(instance + 17)) * .65);
                float intensity = p.initial.w * lerp(.7, 1, formation) * lerp(1, .17, haze);
                // Cluster crowding is additive; reduce per-particle flux late to retain pink cores.
                intensity *= lerp(1, .45, evacuation * knot);
                // Avoid additive saturation where the compact projection crowds outer tracers.
                intensity *= lerp(1, .4, smoothstep(.78, 1.3, length(center.xy)) * formation);
                o.light = float4(color * intensity * _Brightness, haze);
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
                float edge = min(inside - _Padding, _Oval.x * _WorldScale.x - abs(i.arenaPosition.x) - _Padding);
                clip(edge);
                float r2 = dot(i.uv, i.uv);
                clip(1 - r2);
                float glow = (exp(-r2 * 5.5) - exp(-5.5)) * .7 + exp(-r2 * 28) * .6;
                return float4(i.light.rgb * glow * saturate(edge / .05), 0);
            }
            ENDHLSL
        }
    }
}
