Shader "MatthewGuz/DrunkEffect_URP"
{
    Properties
    {
        _Intensity ("Glitch Intensity", Range(0, 1)) = 0.5
        _Speed ("Glitch Speed", Range(0, 10)) = 1.0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalRenderPipeline" "Queue"="Transparent" }
        
        Pass
        {
            Name "DrunkEffectPass"
            Tags { "LightMode"="UniversalForward" }
            
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
            float _Speed;
            
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
                
                // Apply glitch distortion
                float2 offset = _Intensity * float2(
                    sin(_Time.y * _Speed + screenUV.y * 50.0),
                    cos(_Time.y * _Speed + screenUV.x * 50.0)
                ) * 0.01;
                
                float2 uvR = screenUV + offset;
                float2 uvG = screenUV - offset;
                float2 uvB = screenUV;
                
                float4 colorR = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uvR);
                float4 colorG = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uvG);
                float4 colorB = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uvB);
                
                return float4(colorR.r, colorG.g, colorB.b, 1.0);
            }
            
            ENDHLSL
        }
    }
}
