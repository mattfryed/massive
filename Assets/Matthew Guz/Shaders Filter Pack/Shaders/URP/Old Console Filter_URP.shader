Shader "MatthewGuz/Old Console Filter_URP"
{
    Properties
    {
        [HideInInspector] __dirty( "", Int ) = 1
        _Intensity("Intensity", Range(0, 10)) = 1.0 
        _Color1("Darkest Color", Color) = (0.0237, 0.3868, 0.1174, 1)
        _Color2("Darker Color", Color) = (0.0414, 0.4528, 0, 1)
        _Color3("Medium Color", Color) = (0.4505, 0.7264, 0.0377, 1)
        _Color4("Lighter Color", Color) = (0.5593, 0.9057, 0, 1)
        _Color5("Lightest Color", Color) = (0.1710, 0.4865, 0.0029, 1)
    }

    SubShader
    {
        Tags{ "RenderType" = "Opaque"  "Queue" = "Geometry+0" }
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
            float _Intensity;
            float4 _Color1, _Color2, _Color3, _Color4, _Color5;

            inline float3 RGBToHSV(float3 c)
            {
                float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
                float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
                float d = q.x - min(q.w, q.y);
                float e = 1.0e-10;
                return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
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
                float4 screenColor = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV);
                float3 hsv = RGBToHSV(screenColor.rgb);

                float4 consoleColor = (hsv.z < 0.3 ? 
                    (hsv.z < 0.08 ? 
                        (hsv.z < 0.05 ? 
                            (hsv.z < 0.02 ? _Color1 : _Color2) 
                            : _Color3) 
                        : _Color4) 
                    : _Color5);
                
                return consoleColor * _Intensity;
            }
            ENDHLSL
        }
    }
    Fallback "Diffuse"
}
