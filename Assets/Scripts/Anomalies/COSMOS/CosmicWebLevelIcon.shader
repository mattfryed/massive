Shader "MASSIVE/Cosmos/Level Icon"
{
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" "DisableBatching"="True" "IgnoreProjector"="True" }
        Blend Off
        ZWrite On ZTest LEqual Cull Off
        Pass
        {
            Name "SOLID_PARTICLES"
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "CosmicWebMotion.hlsl"
            StructuredBuffer<Particle> _Particles;
            float4x4 _VolumeRotation;
            float4 _Blue, _Pink, _VolumeShape;
            float _MotionTime, _CloudScale, _PointSize, _Brightness, _SizeVariance;
            float _DriftStrength, _ClusterTurbulence, _EvolutionVariation;
            struct appdata { float4 vertex : POSITION; float2 id : TEXCOORD0; };
            struct v2f { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float3 emission : TEXCOORD1; };
            float Hash(float x) { return frac(sin(x * 12.9898 + 78.233) * 43758.5453); }
            v2f vert(appdata v)
            {
                uint id = (uint)v.id.x;
                Particle p = _Particles[id];
                float formation, evacuation;
                // Fixed epoch and independent motion, with no grid or depth flattening.
                float3 center = WebPosition(p, .5, _EvolutionVariation, _MotionTime, _DriftStrength,
                    _ClusterTurbulence, formation, evacuation);
                float3 volume = center.xzy;
                // Shape a filled ovoid in its own frame, then rotate the complete volume.
                // Its depth and silhouette foreshorten naturally as the ovoid turns.
                float r2 = dot(volume, volume);
                float r4 = r2 * r2;
                volume *= pow(1 + r4 * r4, -.125);
                float3 local = mul((float3x3)_VolumeRotation, volume * _VolumeShape.xyz) * _CloudScale;
                float4 view = mul(UNITY_MATRIX_MV, float4(local, 1));
                // Express the baked density/brightness distribution through solid dot size.
                // The seed is stable: dots retain their size while the web flows and turns.
                float sizeSample = clamp(sqrt(max(.04, p.initial.w / .625)) * lerp(.55, 1.45, Hash(id + 7)), .25, 1.75);
                float sizeScale = pow(sizeSample, clamp(_SizeVariance, 0, 2));
                // Match the old bright core's footprint, omitting its surrounding halo.
                float radius = _PointSize * .45 * sizeScale;
                float objectScale = length(mul((float3x3)unity_ObjectToWorld, float3(1,0,0)));
                float2 corner = v.vertex.xy;
                view.xy += corner * radius * objectScale;
                v2f o; o.position = mul(UNITY_MATRIX_P, view); o.uv = corner;
                float knot = lerp(p.filament.w, p.cluster.w, evacuation) * formation;
                float3 color = lerp(_Blue.rgb, _Pink.rgb, knot * .85);
                color = lerp(color, float3(.7,.88,1), step(.985, Hash(id + 17)) * .65);
                // Full unlit emission across each disk; no radial or depth dimming.
                o.emission = color * _Brightness;
                return o;
            }
            float4 frag(v2f i) : SV_Target
            {
                // Binary circular coverage: surviving fragments replace color and depth.
                clip(1 - dot(i.uv, i.uv));
                return float4(i.emission, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
