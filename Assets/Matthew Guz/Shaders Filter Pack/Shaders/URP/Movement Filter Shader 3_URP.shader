Shader "MatthewGuz/Movement Filter Shader 3_URP"
{
    Properties
    {
        _Frequency("Frequency", Range( 0 , 200)) = 1
        _Speed("Speed", Range( 0 , 30)) = 2
        [HDR]_Intensity("Intensity", Range( 0 , 50)) = 3.898163
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
            
            uniform float _Frequency;
            uniform float _Speed;
            uniform float _Intensity;

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
                float mulTime18 = _Time.y * _Speed;
                float2 offset = float2(
                    cos((screenUV.y * _Frequency) + mulTime18) * 0.002,
                    sin((screenUV.x * _Frequency) + mulTime18) * 0.005
                );

                // Apply distortion effect
                float2 distortedUV = screenUV + offset;

                // Sample the texture with the distorted UVs
                float4 screenColor = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, distortedUV);

                // Return the modified color with intensity
                return screenColor * _Intensity;
            }

            ENDHLSL
        }
    }

    Fallback "Diffuse"
    CustomEditor "ASEMaterialInspector"
}
