Shader "MatthewGuz/OutlineDetection"
{
    Properties
    {
        _EdgeColor ("Edge Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" }
        Cull Off

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
                float4 clipPos : SV_POSITION;
                float4 screenPos : TEXCOORD0;
            };

            float4 _EdgeColor;
            sampler2D _GrabTexture;

            v2f vert(appdata v)
            {
                v2f o;
                o.clipPos = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.clipPos); 
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                
                float2 uv = i.screenPos.xy / i.screenPos.w;

                
                float2 texelSize = float2(1.0 / _ScreenParams.x, 1.0 / _ScreenParams.y);

                
                float sampleLeft = tex2D(_GrabTexture, uv - float2(texelSize.x, 0)).r;
                float sampleRight = tex2D(_GrabTexture, uv + float2(texelSize.x, 0)).r;
                float sampleUp = tex2D(_GrabTexture, uv + float2(0, texelSize.y)).r;
                float sampleDown = tex2D(_GrabTexture, uv - float2(0, texelSize.y)).r;

                
                float gradientX = abs(sampleRight - sampleLeft);
                float gradientY = abs(sampleUp - sampleDown);

                
                float edgeIntensity = sqrt(gradientX * gradientX + gradientY * gradientY);

                
                return float4(_EdgeColor.rgb * edgeIntensity, _EdgeColor.a);
            }
            ENDCG
        }
    }
}
