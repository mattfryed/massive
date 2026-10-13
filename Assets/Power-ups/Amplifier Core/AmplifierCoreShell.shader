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
        [HideInInspector] _TimeoutWarning ("Sequence Time-out Instability", Float) = 0
        [HideInInspector] _SingularityBrightness ("Surface Brightness", Float) = 1
        [HideInInspector] _PreviewTime ("Preview Clock (-1 uses game time)", Float) = -1
        [HideInInspector] _CoreRipple ("Ejection ripple", Vector) = (0,0,0,0)
        [HideInInspector] _CoreRipple2 ("Second ejection ripple", Vector) = (0,0,0,0)
        [HideInInspector] _CoreRippleOrigin ("Ejection direction", Vector) = (1,0,0,0)
        [HideInInspector] _CoreRippleOrigin2 ("Second ejection direction", Vector) = (-1,0,0,0)
        [HideInInspector] _PossessionMarker ("Player Marker: Scale, Outline, Light Team, Flow", Vector) = (0, 0, 0, 0)
        [HideInInspector] _PossessionMarker2 ("Second Player Marker", Vector) = (0, 0, 0, 0)
        [HideInInspector] _PossessionMarkerOffsets ("Player Marker Horizontal Offsets", Vector) = (0, 0, 0, 0)
        [HideInInspector] _PossessionClearance ("Player Marker Opening Clearance", Float) = 0.16

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
        #include "AmplifierCoreRipple.cginc"

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
        float _TimeoutWarning;
        float _SingularityBrightness;
        float _PreviewTime;
        float _ShellThickness;
        float _DepthSteps;
        float4 _PossessionMarker;
        float4 _PossessionMarker2;
        float4 _PossessionMarkerOffsets;
        float _PossessionClearance;

        float3 MarkerViewAxis()
        {
            // Orthographic and perspective cameras share the same centered,
            // view-facing badge. Object rotation never rotates it off the front.
            float3 worldForward = UNITY_MATRIX_V[2].xyz;
            return normalize(mul((float3x3)unity_WorldToObject, worldForward));
        }

        float3 MarkerRightAxis()
        {
            return normalize(mul((float3x3)unity_WorldToObject, UNITY_MATRIX_V[0].xyz));
        }

        float MarkerDistance(float3 objectPosition, float offset)
        {
            float3 axis = MarkerViewAxis();
            return length(objectPosition - axis * dot(objectPosition, axis) - MarkerRightAxis() * offset);
        }

        struct appdata
        {
            float4 vertex : POSITION;
        };

        struct v2f
        {
            float4 position : SV_POSITION;
            float3 objectPosition : TEXCOORD0;
            float3 markerPosition : TEXCOORD1;
        };

        v2f BuildVertex(appdata input, float layer01)
        {
            v2f output;
            float4 layeredVertex = input.vertex;
            float rippleScale = 1.0 + CoreRippleHeight(input.vertex.xyz);
            layeredVertex.xyz *= (1.0 - saturate(_ShellThickness) * layer01) * rippleScale;
            output.position = UnityObjectToClipPos(layeredVertex);
            output.objectPosition = input.vertex.xyz;
            // Actual projected position of this slice, in outer-shell radii.
            // Normalizing in the fragment would shrink the opening on recessed layers.
            output.markerPosition = normalize(input.vertex.xyz) * (1.0 - saturate(_ShellThickness) * layer01) * rippleScale;
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
            // Keep the authored flow intact. Possession changes the shape of its
            // empty cells below; it does not radially displace or swirl the noise.
            float3 p = spherePosition * _CellScale + _PatternOffset.xyz;
            p += spherePosition * CoreRippleHeight(spherePosition) * 5.0;
            float clock = _PreviewTime >= 0 ? _PreviewTime : _Time.y;
            float flowTime = clock * _FlowSpeed;

            // Spatially different, interfering motion grows stronger as expiry approaches.
            // This deforms the authored surface field, without vibrating the physical Core.
            float instability = clamp(_TimeoutWarning, 0.0, 2.0);
            float warningTime = clock;
            float3 jitter = float3(
                sin(p.y * 2.9 + warningTime * 19.7) * sin(p.z * 1.7 - warningTime * 11.3),
                sin(p.z * 3.1 - warningTime * 23.1) * sin(p.x * 2.3 + warningTime * 13.7),
                sin(p.x * 2.7 + warningTime * 17.3) * sin(p.y * 1.9 - warningTime * 29.9));
            p += jitter * instability * 0.65;

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
            localThreshold *= 1.0 + instability * 0.17 * sin(p.x * 2.3 + p.z * 1.7 + warningTime * 21.7);
        }

        float MarkerOpening(float3 markerPosition, float4 marker, float offset)
        {
            if (marker.x <= .001) return -100;
            return (marker.x * (1.0 + _PossessionClearance) - MarkerDistance(markerPosition, offset)) * _CellScale;
        }

        float MergeEmptyCells(float a, float b, float blendWidth)
        {
            // Positive values are empty shell. A smooth union joins the marker's
            // cell to passing gaps, reshaping their shared contour into necks and
            // saddles instead of leaving a repelled circular halo.
            float h = saturate(.5 + .5 * (a - b) / blendWidth);
            return lerp(b, a, h) + blendWidth * h * (1.0 - h);
        }

        float ShellDistance(float field, float threshold, float3 markerPosition)
        {
            float a = MarkerOpening(markerPosition, _PossessionMarker, _PossessionMarkerOffsets.x);
            float b = MarkerOpening(markerPosition, _PossessionMarker2, _PossessionMarkerOffsets.y);
            float flow = max(_PossessionMarker.x > .001 ? _PossessionMarker.w : 0,
                             _PossessionMarker2.x > .001 ? _PossessionMarker2.w : 0);
            // One joint field for both players: order-independent, without a
            // pinched ribbon trapped between sequentially subtracted circles.
            float rounding = lerp(.04, 1.8, saturate(flow * .5));
            float opening = MergeEmptyCells(a, b, rounding * .65);
            return MergeEmptyCells(abs(field) - threshold, opening, rounding);
        }

        bool RenderMarker(float3 position, float4 marker, float offset, out fixed4 color)
        {
            float distance = MarkerDistance(position, offset);
            float aa = max(fwidth(distance), .001);
            float interior = 1.0 - smoothstep(marker.x * (1.0 - marker.y) - aa,
                marker.x * (1.0 - marker.y) + aa, distance);
            float value = lerp(1.0 - marker.z, marker.z, interior);
            color = fixed4(value.xxx * saturate(_SingularityBrightness), 1);
            return marker.x > .001 && distance < marker.x + aa;
        }

        fixed4 RenderOuter(v2f input) : SV_Target
        {
            fixed4 markerColor;
            if (RenderMarker(input.markerPosition, _PossessionMarker, _PossessionMarkerOffsets.x, markerColor)) return markerColor;
            if (RenderMarker(input.markerPosition, _PossessionMarker2, _PossessionMarkerOffsets.y, markerColor)) return markerColor;
            float signedShellField;
            float localThreshold;
            EvaluateShell(input.objectPosition, signedShellField, localThreshold);
            float shellDistance = ShellDistance(signedShellField, localThreshold, input.markerPosition);
            clip(-shellDistance);

            // Black follows each ribbon's local cross-section. A guaranteed
            // edge and broader underside create binary dimensional shading
            // without a global dark hemisphere.
            float signedAcrossRibbon = signedShellField / max(localThreshold, 0.0001);
            float edge01 = saturate(max(abs(signedAcrossRibbon), 1.0 + shellDistance / max(localThreshold, .0001)));

            float outlineStart = lerp(0.97, 0.82, _RimLight);
            float blackOutline = step(outlineStart, edge01);

            float roundness = saturate(_EmbossStrength / 1.2);
            float shadowStart = lerp(0.96, 0.38, _ShadeThreshold);
            shadowStart += (_WhiteBalance - 0.5) * 0.28;
            shadowStart -= roundness * 0.06;
            float blackUnderside = step(shadowStart, signedAcrossRibbon);
            float blackRegion = max(blackOutline, blackUnderside);

            fixed4 color = lerp(_WhiteColor, _BlackColor, blackRegion);
            color.rgb *= saturate(_SingularityBrightness);
            return color;
        }

        fixed4 RenderDepth(v2f input, float requiredStep) : SV_Target
        {
            // Whole passes can be disabled from the material. Zero steps
            // restores the original single-surface shell exactly.
            clip(_DepthSteps - requiredStep + 0.5);

            float signedShellField;
            float localThreshold;
            EvaluateShell(input.objectPosition, signedShellField, localThreshold);
            clip(-ShellDistance(signedShellField, localThreshold, input.markerPosition));

            // Recessed slices are the exact neutral-black interior wall.
            return fixed4(_BlackColor.rgb * saturate(_SingularityBrightness), _BlackColor.a);
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
