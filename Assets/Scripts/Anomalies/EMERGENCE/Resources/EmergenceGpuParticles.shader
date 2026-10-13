Shader "MASSIVE/Emergence/GPU Particles"
{
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Pass
        {
            Blend Off
            ZWrite On ZTest LEqual Cull Off
            CGPROGRAM
            #pragma target 5.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "EmergenceGpuCommon.hlsl"
            StructuredBuffer<Particle> _Particles;
            float4 _Colors[5], _WasteColor, _DisruptionColor;
            float4 _Origin, _AxisX, _AxisY;
            float _WasteColorAt;
            int _ShowWaste, _ShowDisruption;
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            v2f vert(uint vertex : SV_VertexID, uint instance : SV_InstanceID)
            {
                const float2 corners[6] = { float2(-1,-1), float2(1,1), float2(1,-1), float2(-1,-1), float2(-1,1), float2(1,1) };
                Particle p = _Particles[instance];
                float2 uv = corners[vertex], pos = p.position + uv * p.radius * p.visualScale;
                float3 world = _Origin.xyz + _AxisX.xyz * pos.x + _AxisY.xyz * pos.y;
                v2f o; o.vertex = mul(UNITY_MATRIX_VP, float4(world, 1)); o.uv = uv;
                o.color = _Colors[p.species];
                if (_ShowWaste != 0 && p.species == 2) o.color = lerp(o.color, _WasteColor, saturate(p.waste / _WasteColorAt));
                if (_ShowDisruption != 0 && p.disruption > 0) o.color = _DisruptionColor;
                return o;
            }
            float4 frag(v2f i) : SV_Target { clip(1 - dot(i.uv, i.uv)); return float4(i.color.rgb, 1); }
            ENDCG
        }
    }
}
