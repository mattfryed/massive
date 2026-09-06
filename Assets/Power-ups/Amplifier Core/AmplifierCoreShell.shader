Shader "MASSIVE/Amplifier Core/Neutral Shell"
{
    Properties
    {
        _BlackColor ("Neutral Black", Color) = (0, 0, 0, 1)
        _WhiteColor ("Neutral White", Color) = (1, 1, 1, 1)
        _CellScale ("Organic Ribbon Scale", Range(1.5, 7.0)) = 3.15
        _BandWidth ("Shell Coverage", Range(0.1, 1.3)) = 0.93
        _CellIrregularity ("Cell Irregularity", Range(0.0, 1.0)) = 0.86
        _WhiteBalance ("White Balance", Range(0.0, 1.0)) = 0.5
        _PatternOffset ("Pattern Offset", Vector) = (0.17, -0.31, 0.43, 0)
        _FlowSpeed ("Conjoin / Disperse Speed", Range(0.0, 2.0)) = 0.34
        _FlowAmount ("Conjoin / Disperse Amount", Range(0.0, 1.0)) = 0.72
        _EmbossStrength ("Goop Roundness", Range(0.0, 1.2)) = 0.58
        _ShadeThreshold ("Black Ribbon Shading", Range(0.0, 1.0)) = 0.60
        _RimLight ("Black Edge Definition", Range(0.0, 1.0)) = 0.32

        [Header(Layered Thickness)]
        _ShellThickness ("Shell Thickness", Range(0.0, 0.12)) = 0.075
        [IntRange] _DepthSteps ("Depth Steps", Range(0, 6)) = 6
    }

    SubShader
    {
        Tags
        {
            "Queue"="AlphaTest"
            "RenderType"="TransparentCutout"
            "IgnoreProjector"="True"
        }

        Cull Off
        Blend Off
        ZWrite On
        ZTest LEqual
        ColorMask RGBA
        AlphaToMask On

        CGINCLUDE
        #pragma target 3.0
        #include "UnityCG.cginc"

        fixed4 _BlackColor;
        fixed4 _WhiteColor;
        float _CellScale;
        float _BandWidth;
        float _CellIrregularity;
        float _WhiteBalance;
        float4 _PatternOffset;
        float _FlowSpeed;
        float _FlowAmount;
        float _EmbossStrength;
        float _ShadeThreshold;
        float _RimLight;
        float _ShellThickness;
        float _DepthSteps;

        struct appdata
        {
            float4 vertex : POSITION;
        };

        struct v2f
        {
            float4 position : SV_POSITION;
            float3 objectPosition : TEXCOORD0;
        };

        v2f BuildVertex(appdata input, float layer01)
        {
            v2f output;
            float4 layeredVertex = input.vertex;
            layeredVertex.xyz *= 1.0 - saturate(_ShellThickness) * layer01;
            output.position = UnityObjectToClipPos(layeredVertex);
            output.objectPosition = input.vertex.xyz;
            return output;
        }

        v2f VertOuter(appdata input)  { return BuildVertex(input, 0.0); }
        v2f VertDepth1(appdata input) { return BuildVertex(input, 1.0 / 6.0); }
        v2f VertDepth2(appdata input) { return BuildVertex(input, 2.0 / 6.0); }
        v2f VertDepth3(appdata input) { return BuildVertex(input, 3.0 / 6.0); }
        v2f VertDepth4(appdata input) { return BuildVertex(input, 4.0 / 6.0); }
        v2f VertDepth5(appdata input) { return BuildVertex(input, 5.0 / 6.0); }
        v2f VertDepth6(appdata input) { return BuildVertex(input, 1.0); }

        void EvaluateShell(
            float3 objectPosition,
            out float signedShellField,
            out float localThreshold)
        {
            float3 spherePosition = normalize(objectPosition);
            float3 p = spherePosition * _CellScale + _PatternOffset.xyz;
            float flowTime = _Time.y * _FlowSpeed;

            float warpStrength = lerp(0.12, 0.32, _CellIrregularity);
            float3 warp = float3(
                sin(p.y * 1.35 + p.z * 0.73 + flowTime * 0.91),
                sin(p.z * 1.22 + p.x * 0.81 - flowTime * 1.13),
                sin(p.x * 1.48 + p.y * 0.69 + flowTime * 0.77));
            p += warp * warpStrength;

            signedShellField =
                sin(p.x) * cos(p.y) +
                sin(p.y) * cos(p.z) +
                sin(p.z) * cos(p.x);

            float flowPulse = sin(p.x * 1.27 - p.y * 0.83 + flowTime * 1.41);
            flowPulse += sin(p.z * 1.11 + p.y * 0.67 - flowTime * 0.96) * 0.5;
            localThreshold = _BandWidth * (1.0 + flowPulse * 0.18 * _FlowAmount);
        }

        fixed4 RenderOuter(v2f input) : SV_Target
        {
            float signedShellField;
            float localThreshold;
            EvaluateShell(input.objectPosition, signedShellField, localThreshold);
            clip(localThreshold - abs(signedShellField));

            // Black follows each ribbon's local cross-section. A guaranteed
            // edge and broader underside create binary dimensional shading
            // without a global dark hemisphere.
            float signedAcrossRibbon = signedShellField / max(localThreshold, 0.0001);
            float edge01 = saturate(abs(signedAcrossRibbon));

            float outlineStart = lerp(0.97, 0.82, _RimLight);
            float blackOutline = step(outlineStart, edge01);

            float roundness = saturate(_EmbossStrength / 1.2);
            float shadowStart = lerp(0.96, 0.38, _ShadeThreshold);
            shadowStart += (_WhiteBalance - 0.5) * 0.28;
            shadowStart -= roundness * 0.06;
            float blackUnderside = step(shadowStart, signedAcrossRibbon);
            float blackRegion = max(blackOutline, blackUnderside);

            return lerp(_WhiteColor, _BlackColor, blackRegion);
        }

        fixed4 RenderDepth(v2f input, float requiredStep) : SV_Target
        {
            // Whole passes can be disabled from the material. Zero steps
            // restores the original single-surface shell exactly.
            clip(_DepthSteps - requiredStep + 0.5);

            float signedShellField;
            float localThreshold;
            EvaluateShell(input.objectPosition, signedShellField, localThreshold);
            clip(localThreshold - abs(signedShellField));

            // Recessed slices are the exact neutral-black interior wall.
            return _BlackColor;
        }

        fixed4 FragDepth1(v2f input) : SV_Target { return RenderDepth(input, 1.0); }
        fixed4 FragDepth2(v2f input) : SV_Target { return RenderDepth(input, 2.0); }
        fixed4 FragDepth3(v2f input) : SV_Target { return RenderDepth(input, 3.0); }
        fixed4 FragDepth4(v2f input) : SV_Target { return RenderDepth(input, 4.0); }
        fixed4 FragDepth5(v2f input) : SV_Target { return RenderDepth(input, 5.0); }
        fixed4 FragDepth6(v2f input) : SV_Target { return RenderDepth(input, 6.0); }
        ENDCG

        Pass
        {
            Name "OuterShell"
            CGPROGRAM
            #pragma vertex VertOuter
            #pragma fragment RenderOuter
            ENDCG
        }

        Pass
        {
            Name "Depth1"
            CGPROGRAM
            #pragma vertex VertDepth1
            #pragma fragment FragDepth1
            ENDCG
        }

        Pass
        {
            Name "Depth2"
            CGPROGRAM
            #pragma vertex VertDepth2
            #pragma fragment FragDepth2
            ENDCG
        }

        Pass
        {
            Name "Depth3"
            CGPROGRAM
            #pragma vertex VertDepth3
            #pragma fragment FragDepth3
            ENDCG
        }

        Pass
        {
            Name "Depth4"
            CGPROGRAM
            #pragma vertex VertDepth4
            #pragma fragment FragDepth4
            ENDCG
        }

        Pass
        {
            Name "Depth5"
            CGPROGRAM
            #pragma vertex VertDepth5
            #pragma fragment FragDepth5
            ENDCG
        }

        Pass
        {
            Name "Depth6"
            CGPROGRAM
            #pragma vertex VertDepth6
            #pragma fragment FragDepth6
            ENDCG
        }
    }

    Fallback Off
}
