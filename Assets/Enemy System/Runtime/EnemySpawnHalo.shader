Shader "MASSIVE/EnemySpawnHalo"
{
    Properties
    {
        _Color ("Glow Color", Color) = (1, .19, .025, 1)
        _Opacity ("Opacity", Range(0,2)) = 0
        _Defocus ("Defocus", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent-20" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest LEqual
        Blend One One
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color;
            float _Opacity, _Defocus;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata_base v)
            {
                v2f o;
                float3 center = mul(unity_ObjectToWorld, float4(0,0,0,1)).xyz;
                float radius = max(length(unity_ObjectToWorld._m00_m10_m20), length(unity_ObjectToWorld._m02_m12_m22));
                float3 world = center + (UNITY_MATRIX_V[0].xyz * v.vertex.x + UNITY_MATRIX_V[1].xyz * v.vertex.y) * radius;
                o.pos = mul(UNITY_MATRIX_VP, float4(world,1)); o.uv = v.vertex.xy;
                return o;
            }
            float4 frag(v2f i) : SV_Target
            {
                float r = length(i.uv);
                float soft = exp(-r*r*lerp(4.5, 2.2, _Defocus)) * (1-smoothstep(.65,1,r));
                return float4(_Color.rgb * _Color.a * _Opacity * soft,0);
            }
            ENDCG
        }
    }
}
