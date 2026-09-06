Shader "MASSIVE/Amplifier Core/Aurora Particle"
{
    Properties
    {
        _Softness ("Cloud Softness", Range(0.1, 1.0)) = 0.62
        _Distortion ("Organic Distortion", Range(0.0, 1.0)) = 0.48
        _Brightness ("Additive Brightness", Range(0.1, 4.0)) = 1.35
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        Blend SrcAlpha One
        ColorMask RGB
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            float _Softness;
            float _Distortion;
            float _Brightness;

            struct AppData
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Interpolators
            {
                float4 position : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Interpolators Vert(AppData input)
            {
                Interpolators output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            fixed4 Frag(Interpolators input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                float radius = length(p);
                float angle = atan2(p.y, p.x);

                float organic =
                    sin(angle * 3.0 + radius * 11.0 + _Time.y * 1.7) * 0.055 +
                    sin(p.x * 9.0 - p.y * 7.0 - _Time.y * 1.15) * 0.035 +
                    sin((p.x + p.y) * 13.0 + _Time.y * 0.8) * 0.02;
                float field = radius + organic * _Distortion;

                float outer = saturate((1.0 - field) / max(0.001, _Softness));
                outer = outer * outer * (3.0 - 2.0 * outer);
                float cloudyInterior = lerp(
                    0.58,
                    1.0,
                    0.5 + 0.5 * sin(angle * 2.0 - radius * 8.0 + _Time.y));
                float alpha = outer * cloudyInterior * input.color.a;
                clip(alpha - 0.002);

                fixed3 color = input.color.rgb * _Brightness;
                return fixed4(color, alpha);
            }
            ENDCG
        }
    }
}
