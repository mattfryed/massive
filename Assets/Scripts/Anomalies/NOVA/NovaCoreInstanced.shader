Shader "MASSIVE/NovaCoreInstanced"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _ParticleSize ("Particle Size", Float) = 0.1
        _Softness ("Softness", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "UnityCG.cginc"

            StructuredBuffer<float2> _Positions;

            float4 _Color;
            float  _ParticleSize;
            float  _Softness;

            struct appdata
            {
                float3 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

v2f vert (appdata v, uint instanceID : SV_InstanceID)
{
    v2f o;

    // 2D sim pos: x = world X, y = world Z
    float2 p = _Positions[instanceID];

    // Center of this particle in local XZ plane
    float3 center = float3(p.x, 0, p.y);

    // Build the quad in XZ as well (v.vertex.x = X, v.vertex.y = Z)
    float3 offset = float3(v.vertex.x, 0, v.vertex.y) * _ParticleSize;

    float3 localPos = center + offset;
    float4 worldPos = mul(unity_ObjectToWorld, float4(localPos, 1.0));

    o.pos = UnityWorldToClipPos(worldPos.xyz);
    o.uv  = v.uv;
    return o;
}


            fixed4 frag (v2f i) : SV_Target
            {
                // Simple circular falloff
                float2 uv = i.uv * 2 - 1;
                float d = length(uv);
                float alpha = saturate(1 - d * _Softness);
                return fixed4(_Color.rgb, _Color.a * alpha);
            }
            ENDCG
        }
    }
}
