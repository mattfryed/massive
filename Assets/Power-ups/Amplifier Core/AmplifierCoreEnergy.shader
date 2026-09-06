Shader "MASSIVE/Amplifier Core/Energy"
{
    Properties
    {
        [HDR] _WarmColor ("Warm Energy", Color) = (1.0, 0.06, 0.015, 1.0)
        [HDR] _MagentaColor ("Magenta Energy", Color) = (1.0, 0.015, 0.42, 1.0)
        [HDR] _CoolColor ("Cool Energy", Color) = (0.0, 0.72, 1.0, 1.0)
        [HDR] _AccentColor ("Accent Energy", Color) = (0.48, 1.0, 0.04, 1.0)
        [HDR] _FresnelColor ("Fresnel Energy", Color) = (0.45, 0.95, 1.0, 1.0)
        _PatternScale ("Pattern Scale", Range(1.0, 10.0)) = 4.4
        _FlowSpeed ("Flow Speed", Range(0.0, 4.0)) = 0.62
        _FresnelPower ("Fresnel Power", Range(0.5, 8.0)) = 2.6
        _Emission ("Neon Emission", Range(0.5, 5.0)) = 1.2
    }

    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        Cull Back
        Blend Off
        ZWrite On
        ZTest LEqual
        ColorMask RGBA

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            fixed4 _WarmColor;
            fixed4 _MagentaColor;
            fixed4 _CoolColor;
            fixed4 _AccentColor;
            fixed4 _FresnelColor;
            float _PatternScale;
            float _FlowSpeed;
            float _FresnelPower;
            float _Emission;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float3 objectPosition : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldPosition : TEXCOORD2;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.objectPosition = input.vertex.xyz;
                output.worldNormal = UnityObjectToWorldNormal(input.normal);
                output.worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float3 p = normalize(input.objectPosition);
                float time = _Time.y * _FlowSpeed;

                float warp =
                    sin(dot(p, float3(1.7, 2.3, -1.1)) * _PatternScale + time) +
                    sin(dot(p, float3(-2.1, 0.8, 1.9)) * (_PatternScale * 0.73) - time * 1.31) +
                    sin(dot(p, float3(0.6, -1.8, 2.7)) * (_PatternScale * 0.47) + time * 0.61);

                float warmBand = 0.5 + 0.5 * sin(
                    (p.x * 1.4 + p.y * 0.7 - p.z * 0.9) * _PatternScale + warp * 0.9 - time * 1.7);
                float coolBand = 0.5 + 0.5 * sin(
                    (p.x * -0.8 + p.y * 1.6 + p.z * 1.2) * (_PatternScale * 1.13) - warp * 0.62 + time);
                float accentBand = 0.5 + 0.5 * sin(
                    (p.x * 1.9 - p.y * 1.1 + p.z * 0.45) * (_PatternScale * 0.81) + warp * 0.71 + time * 1.27);

                fixed3 energy = lerp(_WarmColor.rgb, _MagentaColor.rgb, smoothstep(0.2, 0.82, warmBand));
                energy = lerp(energy, _CoolColor.rgb, smoothstep(0.58, 0.94, coolBand) * 0.78);
                energy = lerp(energy, _AccentColor.rgb, smoothstep(0.72, 0.97, accentBand) * 0.72);

                float3 normalWS = normalize(input.worldNormal);
                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - input.worldPosition);
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirection)), _FresnelPower);
                energy += _FresnelColor.rgb * fresnel * 0.7;

                // Preserve chroma as emission rises. Multiplying RGB directly
                // pushes every channel into display clipping and turns mixed
                // colors white. Instead, isolate hue, increase its saturation,
                // and apply a modest HDR peak so bloom reads as colored neon.
                float peak = max(max(energy.r, energy.g), max(energy.b, 0.0001));
                float3 chroma = saturate(energy / peak);
                float emission01 = saturate((_Emission - 0.5) / 4.5);
                chroma = pow(chroma, lerp(1.0, 2.45, emission01));

                float baseBrightness = saturate(peak);
                float brightness = lerp(baseBrightness, 1.0, emission01);
                float hdrPeak = lerp(1.0, 1.65, emission01);

                return fixed4(chroma * brightness * hdrPeak, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
