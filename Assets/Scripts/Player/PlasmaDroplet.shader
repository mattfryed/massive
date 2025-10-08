Shader "MASSIVE/PlasmaDroplet"
{
    Properties
    {
        _ColorMain ("Main Color", Color) = (1,1,1,1)
        _UseOutline ("Use Outline", Float) = 0
        _OutlinePx ("Outline Pixels", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_INSTANCING_BUFFER_END(Props)

            float4 _ColorMain;
            float _UseOutline;
            float _OutlinePx;

            struct appdata
            {
                float4 vertex : POSITION;    // unit quad [-0.5..0.5]
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;   // centered coords for circle SDF
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            v2f vert (appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                v2f o;
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                // Instance transform (Graphics.DrawMeshInstanced supplies unity_ObjectToWorld)
                float3 objCenterWS = mul(unity_ObjectToWorld, float4(0,0,0,1)).xyz;

                // Approx uniform scale from instance matrix X column
                float3 xcol = mul(unity_ObjectToWorld, float4(1,0,0,0)).xyz;
                float scale = length(xcol);

                // Camera-facing billboard basis
                float3 toCam = normalize(_WorldSpaceCameraPos - objCenterWS);
                float3 tmpUp = float3(0,1,0);
                // avoid degeneracy when camera is near vertical
                if (abs(dot(tmpUp, toCam)) > 0.95) tmpUp = float3(0,0,1);
                float3 right = normalize(cross(tmpUp, toCam));
                float3 up    = normalize(cross(toCam, right));

                // Place quad in world space
                float2 q = v.vertex.xy; // -0.5..0.5
                float3 worldPos = objCenterWS + (right * q.x + up * q.y) * scale;

                o.pos = UnityWorldToClipPos(float4(worldPos,1));
                o.uv  = q; // keep centered coords for SDF in frag
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                // SDF circle of radius 0.5 in quad space
                float2 p = i.uv;
                float dist = length(p) - 0.5;

                // Hard, crisp silhouette
                if (dist > 0.0) discard;

                // Base fill (unlit, high contrast)
                float4 col = _ColorMain;
                float alpha = 1.0;

                // Optional thin white outline for black droplets (Team 2)
                if (_UseOutline > 0.5)
                {
                    // Screen-space outline via SDF + fwidth in FRAGMENT (valid)
                    float w = fwidth(dist);
                    // Edge region near the rim
                    float edgeMask = 1.0 - smoothstep(0.0, w, dist + w*0.5);
                    // Thin control: convert desired pixel width to SDF thickness constant
                    // (Empirical factor; tweak to taste)
                    float outline = smoothstep(-_OutlinePx * 0.0015 - w, -_OutlinePx * 0.0015 + w, dist);
                    float4 outlineCol = float4(1,1,1,1) * edgeMask;
                    // Keep fill solid; add outline near the edge
                    col = lerp(col, outlineCol, outline);
                }

                return float4(col.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
