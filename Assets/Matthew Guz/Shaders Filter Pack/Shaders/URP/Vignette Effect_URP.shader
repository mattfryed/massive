Shader "MatthewGuz/Vignette Effect_URP" 
{
    Properties
    {
        _VignetteRadius ("Vignette Radius", Range(0.01, 1)) = 0.1
        _VignetteSoftness ("Vignette Softness", Range(0.01, 0.5)) = 0.1
        _WallColor ("Wall Color", Color) = (0.5, 0.5, 0.5, 1)
        _ObjectScreenPos ("Object Screen Position", Vector) = (0.5, 0.5, 0, 0)
        _Offset ("Effect Offset", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalRenderPipeline" "Queue"="Geometry" "RenderType"="Opaque" }
        LOD 100
        Pass
        {
            Name "WallVignettePass"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            HLSLPROGRAM
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
                float2 screenUV : TEXCOORD0;
            };
            
            float _VignetteRadius;
            float _VignetteSoftness;
            half4 _WallColor;
            float4 _ObjectScreenPos;
            float4 _Offset; 
            
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS);
                float2 screenPos = OUT.positionHCS.xy / OUT.positionHCS.w;
                screenPos.y = 1 - screenPos.y; // Corrige la inversión de la imagen
                OUT.screenUV = screenPos * 0.5 + 0.5;
                return OUT;
            }
            
            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.screenUV;
                float2 center = _ObjectScreenPos.xy + _Offset.xy; 
                float dist = distance(uv, center);
                float vignette = smoothstep(_VignetteRadius, _VignetteRadius + _VignetteSoftness, dist);
                return lerp(half4(_WallColor.rgb, 0.0), _WallColor, vignette);
            }
            ENDHLSL
        }
    }
}
