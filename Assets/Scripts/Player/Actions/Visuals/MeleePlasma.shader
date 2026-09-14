Shader "MASSIVE/MeleePlasma"
{
    Properties
    {
        _FlowTex ("Flow / Caustic Texture", 2D) = "gray" {}
        _WarpTex ("Flow Distortion Texture", 2D) = "gray" {}
        _EffectTime ("Effect Time", Float) = 0
        _Opacity ("Effect Opacity", Range(0,1)) = 1
        _DarkTeam ("Dark Team", Float) = 0
        _BodyOpacity ("Body Opacity", Range(0,1)) = 1
        _RimIntensity ("Rim Intensity", Range(0,1)) = 0.95
        _FilamentIntensity ("Filament Contrast", Range(0,1)) = 0.8
        _HaloIntensity ("Halo Intensity", Range(0,1)) = 0.08
        _WarpAmplitude ("Flow Warble", Range(0,1)) = 0.3
        _NoiseScale ("Flow Scale", Float) = 3
        _FlowSpeed ("Flow Speed", Float) = 1
        _RimWidth ("Rim Width", Range(0.005,0.25)) = 0.045
        _ClosedLoop ("Closed Loop", Float) = 0
        _BodyEnabled ("Body Enabled", Float) = 1
        _RimEnabled ("Rim Enabled", Float) = 1
        _FilamentsEnabled ("Filaments Enabled", Float) = 1
        _HaloEnabled ("Halo Enabled", Float) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent+20" "RenderType"="Transparent" "IgnoreProjector"="True" }
        ZWrite Off
        ZTest LEqual
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "MeleePlasma"
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _FlowTex;
            sampler2D _WarpTex;
            float _EffectTime, _Opacity, _DarkTeam;
            float _BodyOpacity, _RimIntensity, _FilamentIntensity, _HaloIntensity;
            float _WarpAmplitude, _NoiseScale, _FlowSpeed, _RimWidth, _ClosedLoop;
            float _BodyEnabled, _RimEnabled, _FilamentsEnabled, _HaloEnabled;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // Composite internally in premultiplied form, then return straight alpha.
            // Black must occlude the field; additive blending would lose that layer.
            void Composite(inout float luminance, inout float alpha, float gray, float coverage)
            {
                coverage = saturate(coverage);
                luminance = gray * coverage + luminance * (1.0 - coverage);
                alpha = coverage + alpha * (1.0 - coverage);
            }

            float2 FlowDomain(float2 uv)
            {
                float scale = max(0.1, _NoiseScale);
                float2 straight = float2(uv.x * scale, uv.y * 0.7 + 0.5);
                // A circular domain is continuous in value and slope at the ring join,
                // even for non-integral flow scales and differently drifting textures.
                float angle = uv.x * 6.28318530718;
                float2 closed = float2(cos(angle), sin(angle)) * (scale / 6.28318530718);
                closed += uv.y * float2(0.42, 0.57);
                return lerp(straight, closed, saturate(_ClosedLoop));
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _EffectTime * _FlowSpeed;
                float2 domain = FlowDomain(i.uv);
                float2 warpUV = domain * float2(0.63, 0.91) + float2(-t * 0.17, t * 0.071);
                float warpA = tex2D(_WarpTex, warpUV).r * 2.0 - 1.0;
                float warpB = tex2D(_WarpTex, warpUV.yx * 1.37 + float2(0.31, -t * 0.11)).r * 2.0 - 1.0;
                float warble = saturate(_WarpAmplitude);
                float2 flowUV = domain + float2(-t * 0.48, t * 0.035);
                flowUV += float2(warpA, warpB) * (warble * 0.42);
                float flow = tex2D(_FlowTex, flowUV).r;
                float detail = tex2D(_FlowTex, flowUV * float2(1.71, 1.13) + float2(t * 0.16, 0.37)).r;

                // Noise animates within a connected ribbon. Its amplitude cannot cut
                // holes in the outer shape or expand beyond the geometry's halo margin.
                float edgeOffset = warpA * warble * 0.065;
                float distanceFromCenter = abs(i.uv.y - edgeOffset);
                float bodyEdge = 0.69 + warpB * warble * 0.035;
                float aa = max(fwidth(distanceFromCenter), 0.001);
                float bodyMask = 1.0 - smoothstep(bodyEdge - aa, bodyEdge + aa, distanceFromCenter);
                float rimWidth = clamp(_RimWidth, 0.005, 0.25);
                float rimInner = 1.0 - smoothstep(bodyEdge - rimWidth - aa, bodyEdge - rimWidth + aa, distanceFromCenter);
                float rimMask = saturate(bodyMask - rimInner);
                float centerMask = 1.0 - smoothstep(bodyEdge - rimWidth - 0.045, bodyEdge, distanceFromCenter);

                float haloDistance = max(0.0, distanceFromCenter - bodyEdge);
                float haloMask = exp2(-haloDistance * haloDistance * 130.0);
                haloMask *= 1.0 - smoothstep(0.91, 0.995, abs(i.uv.y));
                haloMask *= 1.0 - bodyMask;

                float veins = smoothstep(0.34, 0.69, flow * 0.77 + detail * 0.23);
                float fineVeins = smoothstep(0.58, 0.88, detail) * (1.0 - veins) * 0.32;
                float filamentMask = saturate(veins + fineVeins) * centerMask;
                float darkTeam = saturate(_DarkTeam);
                float bodyGray = lerp(0.94, 0.008, darkTeam);
                float filamentGray = lerp(0.018, 0.96, darkTeam);

                float luminance = 0;
                float alpha = 0;
                Composite(luminance, alpha, 0.8, haloMask * saturate(_HaloIntensity) * saturate(_HaloEnabled));
                Composite(luminance, alpha, bodyGray, bodyMask * saturate(_BodyOpacity) * saturate(_BodyEnabled));
                Composite(luminance, alpha, filamentGray, filamentMask * saturate(_FilamentIntensity) * saturate(_FilamentsEnabled));
                Composite(luminance, alpha, 1.0, rimMask * saturate(_RimIntensity) * saturate(_RimEnabled));

                float gray = saturate(luminance / max(alpha, 0.00001));
                return fixed4(gray, gray, gray, alpha * saturate(_Opacity));
            }
            ENDCG
        }
    }
    FallBack Off
}
