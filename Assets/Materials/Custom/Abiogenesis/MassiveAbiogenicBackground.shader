Shader "MASSIVE/AbiogenicBackground"
{
    Properties
    {
        _BackgroundColor         ("Background Color", Color)      = (0.01,0.01,0.01,1)
        _FieldColor              ("Field Color", Color)           = (1,1,1,1)

        [Header(Virtual Perspective)]
        _VirtualCameraDistance   ("Virtual Camera Distance", Float) = 3.0
        _ViewScale               ("Perspective Spread", Range(0.01,0.5)) = 0.125
        _VanishingPoint          ("Vanishing Point XY", Vector)    = (0,0,0,0)
        _VirtualCameraOffset     ("Virtual Camera Offset XY", Vector) = (0,0,0,0)
        _CameraDriftAmplitude    ("Camera Drift Amplitude", Range(0,0.25)) = 0.0
        _CameraDriftSpeed        ("Camera Drift Speed", Float)     = 0.20

        [Header(Shading)]
        _BaseLuma                ("Base Luma", Range(0,1))        = 0.02
        _GlowStrength            ("Glow Strength", Float)         = 0.90
        _StripeFrequency         ("Stripe Frequency", Float)      = 6.0
        _StripeContrast          ("Stripe Contrast", Range(0,1))  = 0.16
        _FogDensity              ("Fog Density", Float)           = 0.10
        _SurfaceSoftness         ("Surface Softness", Float)      = 0.020
        _NormalShading           ("Sphere Form Shading", Range(0,1)) = 0.75
        _NearBoost               ("Near Bead Boost", Range(0,2))  = 0.65

        [Header(Raymarch)]
        _MaxSteps                ("Max Steps", Float)             = 80
        _HitEpsilon              ("Hit Epsilon", Float)           = 0.001
        _MaxDistance             ("Max Distance", Float)          = 14.0
        _RayStartOffset          ("Ray Start Offset", Float)      = 0.0
        _StepScale               ("Step Safety", Range(0.25,1))   = 0.78

        [Header(Field)]
        _CellScale               ("XY Cell Scale", Float)         = 3.0
        _LayerDepth              ("Layer Depth", Float)           = 0.5
        _SphereRadius            ("Sphere Radius", Float)         = 0.10
        _SphereRadiusAmplitude   ("Radius Amplitude", Range(0,1)) = 0.50
        _SphereRadiusFrequency   ("Radius Frequency", Float)      = 1.0

        [Header(Motion and Warp)]
        _ScrollSpeed             ("Depth Travel Speed", Float)    = 0.15
        _SwirlSpeed              ("Swirl Speed", Float)           = 0.20
        _BendAmplitude           ("Bend Amplitude", Float)        = 0.10
        _WarpStrength            ("Warp Strength", Float)         = 0.14
        _PhaseOffset             ("Phase Offset", Float)          = 0.0
        _EdgeFade                ("Screen Edge Fade", Range(0,0.5)) = 0.03

        // Kept for compatibility with the existing arena controller. The v2
        // projection is intentionally screen/virtual-camera based instead.
        [HideInInspector] _FieldOriginWS    ("Field Origin WS", Vector) = (0,0,0,0)
        [HideInInspector] _FieldAxisX_WS    ("Field Axis X WS", Vector) = (1,0,0,0)
        [HideInInspector] _FieldAxisY_WS    ("Field Axis Y WS", Vector) = (0,0,1,0)
        [HideInInspector] _FieldAxisZ_WS    ("Field Axis Z WS", Vector) = (0,-1,0,0)
        [HideInInspector] _ArenaHalfExtents ("Arena Half Extents", Vector) = (8,4,0,0)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry-10" }
        Cull Off
        ZWrite On
        ZTest LEqual

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "UnityCG.cginc"
            #include "MassiveAbiogenicFieldCommon.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 posCS     : SV_POSITION;
                float4 screenPos : TEXCOORD0;
            };

            float4 _BackgroundColor;
            float4 _FieldColor;

            float _VirtualCameraDistance;
            float _ViewScale;
            float4 _VanishingPoint;
            float4 _VirtualCameraOffset;
            float _CameraDriftAmplitude;
            float _CameraDriftSpeed;

            float _BaseLuma;
            float _GlowStrength;
            float _StripeFrequency;
            float _StripeContrast;
            float _FogDensity;
            float _SurfaceSoftness;
            float _NormalShading;
            float _NearBoost;

            float _MaxSteps;
            float _HitEpsilon;
            float _MaxDistance;
            float _RayStartOffset;
            float _StepScale;

            float _CellScale;
            float _LayerDepth;
            float _SphereRadius;
            float _SphereRadiusAmplitude;
            float _SphereRadiusFrequency;

            float _ScrollSpeed;
            float _SwirlSpeed;
            float _BendAmplitude;
            float _WarpStrength;
            float _PhaseOffset;
            float _EdgeFade;

            v2f vert(appdata v)
            {
                v2f o;
                o.posCS = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.posCS);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float timeSeconds = _Time.y + _PhaseOffset;

                // This is the important v2 change: build a synthetic perspective
                // ray from normalized screen coordinates. It remains perspective
                // even when MASSIVE's gameplay camera is orthographic/top-down.
                float2 screenUV = i.screenPos.xy / max(i.screenPos.w, 1e-5);
                float2 uv = screenUV * 2.0 - 1.0;
                uv.x *= _ScreenParams.x / max(_ScreenParams.y, 1.0);

                float2 drift = float2(
                    sin(timeSeconds * _CameraDriftSpeed),
                    cos(timeSeconds * _CameraDriftSpeed * 0.83)) * _CameraDriftAmplitude;

                float3 rayOriginField = float3(
                    _VirtualCameraOffset.xy + drift,
                    -max(_VirtualCameraDistance, 0.01));

                float2 projected = (uv - _VanishingPoint.xy) * _ViewScale;
                float3 rayDirField = normalize(float3(projected, 1.0));
                rayOriginField += rayDirField * _RayStartOffset;

                MassiveAbiogenicRaymarchResult rm = MassiveRaymarchAbiogenicField(
                    rayOriginField,
                    rayDirField,
                    timeSeconds,
                    (int)round(_MaxSteps),
                    _MaxDistance,
                    _HitEpsilon,
                    _StepScale,
                    _CellScale,
                    _LayerDepth,
                    _SphereRadius,
                    _SphereRadiusAmplitude,
                    _SphereRadiusFrequency,
                    _ScrollSpeed,
                    _SwirlSpeed,
                    _BendAmplitude,
                    _WarpStrength);

                float3 color = MassiveShadeAbiogenicField(
                    rm,
                    rayDirField,
                    _BackgroundColor.rgb,
                    _FieldColor.rgb,
                    timeSeconds,
                    _MaxDistance,
                    _BaseLuma,
                    _GlowStrength,
                    _StripeFrequency,
                    _StripeContrast,
                    _FogDensity,
                    _SurfaceSoftness,
                    _NormalShading,
                    _NearBoost);

                float2 centered = abs(screenUV * 2.0 - 1.0);
                float edge = max(centered.x, centered.y);
                float edgeFade = 1.0 - saturate(
                    (edge - (1.0 - _EdgeFade)) / max(_EdgeFade, 1e-4));
                color = lerp(_BackgroundColor.rgb, color, edgeFade);

                return float4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
