Shader "MASSIVE/NuggetsMesh"
{
    Properties
    {
        _Color("Dot Color", Color) = (1,1,1,1)
        _DebugBypassMask("Debug: Bypass SDF (0/1)", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+5" "RenderType"="Transparent" }
        ZWrite Off
        ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Color;
            float  _DebugBypassMask;

            // world-space SDF params (XZ plane)
            float3 _CenterWS;   // xyz, use xz
            float  _Radius;
            float2 _DeformDirWS; // x,z dir
            float  _DeformAmt;
            float  _HitImpulse;
            float  _NoisePhase;

            struct appdata {
                float4 vertex : POSITION; // XZ plane vertices
                float2 uv     : TEXCOORD0; // 0..1
            };
            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
                float3 ws  : TEXCOORD1;
            };

            v2f vert(appdata v) {
                v2f o;
                float4 w = mul(unity_ObjectToWorld, v.vertex);
                o.ws = w.xyz;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float sdDeformedCircleXZ(float2 p, float2 dir, float R, float deform, float hit)
            {
                float2 ortho = float2(-dir.y, dir.x);
                float2 q = float2(dot(p, dir) * (1.0 - deform),
                                  dot(p, ortho) * (1.0 + deform));
                float rr = R * (1.0 + 0.04 * hit);
                return length(q) - rr;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // circular dot from quad UV (no gradient)
                float2 uv = i.uv * 2 - 1;                 // [-1,1]
                if (dot(uv,uv) > 1.0) discard;

                if (_DebugBypassMask < 0.5) {
                    float2 p = i.ws.xz - _CenterWS.xz;
                    float deform = _DeformAmt + 0.015 * sin(_NoisePhase + p.x*11.3 + p.y*7.9);
                    float d = sdDeformedCircleXZ(p, normalize(_DeformDirWS + 1e-5), _Radius, deform, _HitImpulse);
                    if (d > 0) discard;
                }

                return _Color;
            }
            ENDHLSL
        }
    }
}