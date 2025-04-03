Shader "MatthewGuz/Sepia Filter_URP"
{
    Properties
    {
        _Color1 ("Darkest Color", Color) = (0.339, 0.212, 0.056, 1)
        _Color2 ("Darker Color", Color) = (0.584, 0.363, 0.085, 1)
        _Color3 ("Medium Dark", Color) = (0.604, 0.415, 0.168, 1)
        _Color4 ("Medium Light", Color) = (0.689, 0.510, 0.270, 1)
        _Color5 ("Lighter Color", Color) = (0.896, 0.672, 0.393, 1)
        _Color6 ("Lightest Color", Color) = (0.915, 0.778, 0.600, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+0" }
        Pass
        {
            Name "SepiaFilter"
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
            
            half4 _Color1, _Color2, _Color3, _Color4, _Color5, _Color6;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS);
                OUT.screenPos = ComputeScreenPos(OUT.positionHCS);
                return OUT;
            }

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
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                half4 screenColor = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV);
                float3 hsv = RGBToHSV(screenColor.rgb);

                half4 color;
                if (hsv.z < 0.3)
                {
                    if (hsv.z < 0.12)
                    {
                        if (hsv.z < 0.09)
                        {
                            if (hsv.z < 0.06)
                                color = (hsv.z < 0.03) ? _Color1 : _Color2;
                            else
                                color = _Color3;
                        }
                        else
                            color = _Color4;
                    }
                    else
                        color = _Color5;
                }
                else
                    color = _Color6;

                return color;
            }
            ENDHLSL
        }
    }
    Fallback "Diffuse"
}
