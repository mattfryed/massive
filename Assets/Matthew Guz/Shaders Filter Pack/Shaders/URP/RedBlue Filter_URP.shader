Shader "MatthewGuz/RedBlue Filter_URP"
{
    Properties
    {
        _ColorLow  ("Darkest Color", Color) = (0, 0.047, 0.368, 1)
        _ColorMidLow ("Mid-Low Color", Color) = (0, 0.087, 0.650, 1)
        _ColorMidHigh ("Mid-High Color", Color) = (0.801, 0.092, 0, 1)
        _ColorHigh ("Brightest Color", Color) = (1, 0.429, 0.429, 1)
    }
    
    SubShader
    {
        Tags { "RenderType" = "Opaque"  "Queue" = "Geometry+0" }
        Pass
        {
            Name "RedBlueFilter"
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
            
            float4 _ColorLow, _ColorMidLow, _ColorMidHigh, _ColorHigh;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS);
                OUT.screenPos = ComputeScreenPos(OUT.positionHCS);
                return OUT;
            }

            // Function to convert RGB to HSV
            float3 RGBToHSV(float3 c)
            {
                float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
                float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
                float d = q.x - min(q.w, q.y);
                float e = 1.0e-10;
                return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Normalize screen UV coordinates
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;

                // Sample the texture from the grabbed screen texture
                half4 screenColor = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV);

                // Convert RGB to HSV
                float3 hsv = RGBToHSV(screenColor.rgb);

                // Select color based on HSV brightness value
                half4 color;
                if (hsv.z < 0.3)
                {
                    if (hsv.z < 0.08)
                    {
                        if (hsv.z < 0.05)
                            color = _ColorLow;
                        else
                            color = _ColorMidLow;
                    }
                    else
                        color = _ColorMidHigh;
                }
                else
                    color = _ColorHigh;

                return color;
            }

            ENDHLSL
        }
    }
    Fallback "Diffuse"
}
