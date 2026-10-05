Shader "MASSIVE/Cosmos/ArenaMask"
{
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        Pass
        {
            Cull Off ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4x4 _CosmosWorldToGrid;
            float4 _ArenaOval;
            struct v2f { float4 position : SV_POSITION; float2 grid : TEXCOORD0; };
            v2f vert(float4 position : POSITION)
            {
                v2f o; o.position = UnityObjectToClipPos(position);
                o.grid = mul(_CosmosWorldToGrid, mul(unity_ObjectToWorld, position)).xy;
                return o;
            }
            float4 frag(v2f i) : SV_Target
            {
                float2 q = i.grid / float2(_ArenaOval.z, _ArenaOval.y);
                // Retain the authored mask planes and their depth, but open the new ellipse.
                clip(dot(q,q) - 1.0001);
                return float4(0,0,0,1);
            }
            ENDHLSL
        }
    }
}
