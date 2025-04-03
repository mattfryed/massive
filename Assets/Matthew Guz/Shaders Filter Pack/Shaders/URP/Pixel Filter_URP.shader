Shader "MatthewGuz/Pixel Filter"
{
    Properties
    {
        _PixelSize ("Pixel Size", Range(1, 1024)) = 8
    }

    SubShader
    {
        Tags { "Queue"="Overlay" }
        
        Pass
        {
            Name "PixelFilter"
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
            float _PixelSize;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS);
                OUT.screenPos = ComputeScreenPos(OUT.positionHCS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Normalize screen UV coordinates
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;

                // No flipping needed for this case
                // Apply pixelation effect by flooring the UV coordinates
                float2 pixelatedUV = floor(screenUV * _PixelSize) / _PixelSize;

                // Sample the texture from the grabbed screen texture
                half4 color = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, pixelatedUV);

                return color;
            }

            ENDHLSL
        }
    }

    Fallback "Diffuse"
    CustomEditor "ASEMaterialInspector"
}
