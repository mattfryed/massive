Shader "MASSIVE/AttackTrailInstanced"
{
    Properties
    {
        _Team1Color   ("Team 1 Color", Color) = (1,1,1,1)
        _Team2Color   ("Team 2 Core",  Color) = (0,0,0,1)
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
        float  _IsTeam2;

        float3 _CamRightWS;
        float3 _CamUpWS;

        struct appdata
        {
            float3 vertex    : POSITION;     // quad verts in [-0.5,0.5]
            uint   instanceID: SV_InstanceID;
        };

        struct v2f
        {
            float4 pos   : SV_POSITION;
            float2 local : TEXCOORD0; // local quad coords in [-1,1]
        };

        v2f vert(appdata v)
        {
            v2f o;

            AttackParticle p = _Particles[v.instanceID];

            if (p.size <= 0.0001 || p.state == 0u)
            {
                o.pos   = float4(0,0,0,0);
                o.local = float2(0,0);
                return o;
            }

            // built-in quad verts are [-0.5,0.5], scale to [-1,1]
            float2 quad = v.vertex.xy * 2.0;

            float3 center = p.posWS;
            float  radius = p.size;

            float3 right = normalize(_CamRightWS);
            float3 up    = normalize(_CamUpWS);

            float3 worldPos = center + (right * quad.x + up * quad.y) * radius;

            o.pos   = UnityWorldToClipPos(worldPos);
            o.local = quad;
            return o;
        }

        fixed4 frag(v2f i) : SV_Target
        {
            float2 q = i.local;
            float r  = length(q);

            // perfect disc
            if (r > 1.0) discard;

            float4 coreColor = (_IsTeam2 > 0.5) ? _Team2Color : _Team1Color;
            coreColor.a = 1.0;
            return coreColor;
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
