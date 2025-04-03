Shader "MatthewGuz/Blurred Filter"
{
    Properties
    {
        _BlurStrength ("Blur Strength", Range(0, 0.05)) = 0.02
        _TintColor ("Tint Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" }
        ZTest Always
        Cull Back

        GrabPass { }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float4 screenUV : TEXCOORD0;
            };

            float _BlurStrength;
            float4 _TintColor;
            sampler2D _GrabTexture;

            v2f vert (appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.screenUV = ComputeGrabScreenPos(output.position);
                return output;
            }

            half4 frag (v2f input) : SV_Target
            {
                float2 uv = input.screenUV.xy / input.screenUV.w;

                
                half4 col = tex2D(_GrabTexture, uv) * 2.0;

                col += tex2D(_GrabTexture, uv + float2(_BlurStrength, _BlurStrength));
                col += tex2D(_GrabTexture, uv + float2(-_BlurStrength, _BlurStrength));
                col += tex2D(_GrabTexture, uv + float2(_BlurStrength, -_BlurStrength));
                col += tex2D(_GrabTexture, uv + float2(-_BlurStrength, -_BlurStrength));

                col += tex2D(_GrabTexture, uv + float2(_BlurStrength * 0.5, _BlurStrength));
                col += tex2D(_GrabTexture, uv + float2(-_BlurStrength * 0.5, _BlurStrength));
                col += tex2D(_GrabTexture, uv + float2(_BlurStrength * 0.5, -_BlurStrength));
                col += tex2D(_GrabTexture, uv + float2(-_BlurStrength * 0.5, -_BlurStrength));

                col += tex2D(_GrabTexture, uv + float2(_BlurStrength, 0));
                col += tex2D(_GrabTexture, uv + float2(-_BlurStrength, 0));
                col += tex2D(_GrabTexture, uv + float2(0, _BlurStrength));
                col += tex2D(_GrabTexture, uv + float2(0, -_BlurStrength));

                
                col /= 14.0;

                
                col *= _TintColor;

                return col;
            }
            ENDCG
        }
    }
}
