Shader "MatthewGuz/Outline Detection_URP"
{
    Properties
    {
        _EdgeColor ("Edge Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Overlay" }
        Pass
        {
            Name "OutlineDetection"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
            };

            TEXTURE2D_X(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            float4 _EdgeColor;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS);
                OUT.screenPos = ComputeScreenPos(OUT.positionHCS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;

                // Sample the screen texture
                float sampleLeft = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV - float2(1.0 / _ScreenParams.x, 0));
                float sampleRight = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV + float2(1.0 / _ScreenParams.x, 0));
                float sampleUp = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV + float2(0, 1.0 / _ScreenParams.y));
                float sampleDown = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV - float2(0, 1.0 / _ScreenParams.y));

                // Calculate horizontal and vertical gradients
                float gradientX = abs(sampleRight - sampleLeft);
                float gradientY = abs(sampleUp - sampleDown);

                // Edge intensity
                float edgeIntensity = sqrt(gradientX * gradientX + gradientY * gradientY);

                // Apply the edge color
                return float4(_EdgeColor.rgb * edgeIntensity, _EdgeColor.a);
            }

            ENDHLSL
        }
    }

    Fallback "Diffuse"
    CustomEditor "ASEMaterialInspector"
}
