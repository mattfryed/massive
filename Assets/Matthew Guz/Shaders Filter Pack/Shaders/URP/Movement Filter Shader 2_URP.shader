Shader "MatthewGuz/Movement Filter Shader 2_URP"
{
    Properties
    {
        _Frequency("Frequency", Range(0, 200)) = 1
        _Speed("Speed", Range(0, 30)) = 2
        [HDR]_Intensity("Intensity", Range(0, 50)) = 3.898163
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            Name "UniversalForward"
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
            
            float _Frequency;
            float _Speed;
            float _Intensity;
            
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
                float timeOffset = _Time.y * _Speed;
                float2 offset = float2(0, 0.005) * sin((screenUV.x * _Frequency) + timeOffset);
                
                float4 screenColor = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV + offset);
                return screenColor * _Intensity;
            }
            
            ENDHLSL
        }
    }
}