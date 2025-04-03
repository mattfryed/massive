Shader "MatthewGuz/Pixel Filter"
{
    Properties
    {
        _PixelSize ("Pixel Size", Range(1, 1024)) = 8
    }
    SubShader
    {
        Tags { "Queue"="Overlay" }
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
                float4 pos : POSITION;
                float4 grabUV : TEXCOORD0;
            };

            sampler2D _GrabTexture;
            float _PixelSize;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.grabUV = ComputeScreenPos(o.pos); // Calculate screen space position
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Normalize screen UV coordinates
                float2 uv = i.grabUV.xy / i.grabUV.w;

                // Flip Y-axis to correct the inversion
                uv.y = uv.y;

                // Apply pixelation effect
                float2 pixelatedUV = floor(uv * _PixelSize) / _PixelSize;

                // Sample the grabbed texture
                fixed4 color = tex2D(_GrabTexture, pixelatedUV);

                return color;
            }
            ENDCG
        }
    }
}
