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
            // Shared planar Surface masks supply a rectangle with optional half-ellipse
            // goal caps. Existing COSMOS materials only supply _ArenaOval and keep
            // their original full-ellipse cutout exactly.
            float4 _ArenaRect;
            float2 _ArenaMaskPadding;
            float _ArenaMaskExcludeGoalCaps;
            struct v2f { float4 position : SV_POSITION; float2 grid : TEXCOORD0; };
            v2f vert(float4 position : POSITION)
            {
                v2f o; o.position = UnityObjectToClipPos(position);
                o.grid = mul(_CosmosWorldToGrid, mul(unity_ObjectToWorld, position)).xy;
                return o;
            }
            float4 frag(v2f i) : SV_Target
            {
                if (_ArenaOval.w > .5)
                {
                    float2 q = i.grid / (float2(_ArenaOval.z, _ArenaOval.y) + _ArenaMaskPadding);
                    float outside = dot(q,q) - 1.0001;
                    if (_ArenaMaskExcludeGoalCaps > .5)
                        outside = max(outside, abs(i.grid.x) - (_ArenaOval.x + _ArenaMaskPadding.x));
                    clip(outside);
                }
                else if (_ArenaRect.w > .5)
                {
                    float2 p = abs(i.grid);
                    float2 halfSize = _ArenaRect.xy + _ArenaMaskPadding;
                    if (_ArenaRect.z <= 0)
                        clip(max(p.x - halfSize.x, p.y - halfSize.y));
                    else
                    {
                        float2 q = float2(max(0, p.x - _ArenaRect.x) / (_ArenaRect.z + _ArenaMaskPadding.x), p.y / halfSize.y);
                        clip(dot(q,q) - 1);
                    }
                }
                return float4(0,0,0,1);
            }
            ENDHLSL
        }
    }
}
