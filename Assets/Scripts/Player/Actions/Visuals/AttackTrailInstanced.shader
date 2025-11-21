Shader "MASSIVE/AttackTrailInstanced"
{
    Properties
    {
        _Team1Color   ("Team 1 Color", Color) = (1,1,1,1)
        _Team2Color   ("Team 2 Core",  Color) = (0,0,0,1)
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlineHalf  ("Outline Half Width", Float) = 0.04
    }

    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" }
        ZWrite Off
        ZTest LEqual
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        HLSLINCLUDE
        #pragma target 4.5
        #include "UnityCG.cginc"

        struct AttackParticle
        {
            float3 posWS;
            float2 velXZ;
            float  u0;
            float  life;
            float  maxLife;
            float  size;
            uint   state;
        };

        StructuredBuffer<AttackParticle> _Particles;

        float4 _Team1Color;
        float4 _Team2Color;
        float4 _OutlineColor;
        float  _OutlineHalf;
        float  _IsTeam2; // 0 or 1, set from C#

        // Camera basis
        float3 _CamRightWS;
        float3 _CamUpWS;

        struct appdata
        {
            uint vertexID   : SV_VertexID;
            uint instanceID : SV_InstanceID;
        };

        struct v2f
        {
            float4 pos : SV_POSITION;
            float2 uv  : TEXCOORD0;
        };

        v2f vert(appdata v)
        {
            v2f o;

            AttackParticle p = _Particles[v.instanceID];

            if (p.size <= 0.0001 || p.state == 0u)
            {
                // Place offscreen if dead
                o.pos = float4(0, 0, 0, 0);
                o.uv  = float2(0, 0);
                return o;
            }

            // Quad corners in [-1,1]^2
            float2 corners[4] = {
                float2(-1, -1),
                float2(-1,  1),
                float2( 1,  1),
                float2( 1, -1)
            };

            float2 quad = corners[v.vertexID & 3];

            float3 center = p.posWS;
            float  radius = p.size;

            float3 right = normalize(_CamRightWS);
            float3 up    = normalize(_CamUpWS);

            float3 worldPos = center + (right * quad.x + up * quad.y) * radius;

            o.pos = UnityWorldToClipPos(worldPos);
            o.uv  = quad; // we only need radial distance in fragment
            return o;
        }

        fixed4 frag(v2f i) : SV_Target
        {
            // Circular mask in quad space [-1,1]^2
            float2 uv = i.uv;
            float r   = length(uv);

            if (r > 1.0) discard;

            // Hard edge circle with optional outline
            float outlineHalf = _OutlineHalf;
            float outlineInner = 1.0 - outlineHalf;
            float outlineOuter = 1.0;

            // Core mask
            float coreMask = step(r, outlineInner);

            // Outline mask (ring)
            float ringMask = step(outlineInner, r) * step(r, outlineOuter);

            float4 coreColor = (_IsTeam2 > 0.5) ? _Team2Color : _Team1Color;
            float4 col = coreColor * coreMask;

            if (_IsTeam2 > 0.5)
            {
                // Team 2: black core, white outline
                col += _OutlineColor * ringMask;
            }
            else
            {
                // Team 1: white core only (no outline)
                // If you want outline for Team 1, you can add _OutlineColor * ringMask here.
            }

            // Crisp disc, no soft fade
            col.a = 1.0;
            return col;
        }
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="Always" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            ENDHLSL
        }
    }
}
