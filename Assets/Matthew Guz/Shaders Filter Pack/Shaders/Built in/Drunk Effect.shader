Shader "MatthewGuz/Drunk Effect"
{
    Properties
    {
        _Intensity ("Glitch Intensity", Range(0, 1)) = 0.5
        _Speed ("Glitch Speed", Range(0, 10)) = 1.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" }
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
                float4 screenUV : TEXCOORD0;
            };

            sampler2D _GrabTexture;
            float _Intensity;
            float _Speed;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);

                // Correctly calculate screen-space UV coordinates
                o.screenUV = ComputeScreenPos(o.pos);

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Normalize UV coordinates
                float2 uv = i.screenUV.xy / i.screenUV.w;

                // Apply drunk effect distortion based on time
                float2 offset = _Intensity * float2(
                    sin(_Time.y * _Speed + uv.y * 50.0),
                    cos(_Time.y * _Speed + uv.x * 50.0)
                ) * 0.01;

                // Generate R, G, and B channels with offsets
                float2 uvR = uv + offset;
                float2 uvG = uv - offset;
                float2 uvB = uv;

                fixed4 colorR = tex2D(_GrabTexture, uvR);
                fixed4 colorG = tex2D(_GrabTexture, uvG);
                fixed4 colorB = tex2D(_GrabTexture, uvB);

                return fixed4(colorR.r, colorG.g, colorB.b, 1.0);
            }
            ENDCG
        }
    }
}
