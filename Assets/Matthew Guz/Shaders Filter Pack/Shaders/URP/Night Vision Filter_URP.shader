Shader "MatthewGuz/Night Vision Filter_URP"
{
    Properties
    {
        [HideInInspector] __dirty( "", Int ) = 1
    }

    SubShader
    {
        Tags{ "RenderType" = "Opaque"  "Queue" = "Geometry+0" }
        Cull Back
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            TEXTURE2D_X(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            inline float3 RGBToHSV(float3 c)
            {
                float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                float4 p = lerp( float4( c.bg, K.wz ), float4( c.gb, K.xy ), step( c.b, c.g ) );
                float4 q = lerp( float4( p.xyw, c.r ), float4( c.r, p.yzx ), step( p.x, c.r ) );
                float d = q.x - min( q.w, q.y );
                float e = 1.0e-10;
                return float3( abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            }

            inline float4 ComputeGrabScreenPos(float4 pos)
            {
                #if UNITY_UV_STARTS_AT_TOP
                    float scale = -1.0;
                #else
                    float scale = 1.0;
                #endif
                float4 o = pos;
                o.y = pos.w * 0.5f;
                o.y = (pos.y - o.y) * _ProjectionParams.x * scale + o.y;
                return o;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS);
                OUT.uv = IN.uv;
                OUT.screenPos = ComputeScreenPos(OUT.positionHCS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;

                // Sample the screen color
                float4 screenColor = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV);

                // Convert to HSV and apply the night vision filter
                float3 hsv = RGBToHSV(screenColor.rgb);
                float4 nightVisionColor = (hsv.z < 0.3 ? 
                    (hsv.z < 0.12 ? 
                        (hsv.z < 0.09 ? 
                            (hsv.z < 0.06 ? 
                                (hsv.z < 0.03 ? float4(0.0196, 0.1981, 0.0657, 0) : float4(0.0786, 0.3585, 0.0490, 0)) 
                                : float4(0.1734, 0.5189, 0.1640, 0)) 
                            : float4(0.3798, 0.6321, 0.2117, 0)) 
                        : float4(0.5521, 0.8774, 0.3352, 0)) 
                    : float4(0.6384, 0.8208, 0.5149, 0));

                return nightVisionColor;
            }

            ENDHLSL
        }
    }

    Fallback "Diffuse"
    CustomEditor "ASEMaterialInspector"
}
