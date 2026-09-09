Shader "MASSIVE/MetaballSDF-ScoreVoid"
{
    Properties
    {
        _LitColor("Lit Color", Color) = (1,1,1,1)
        _UnlitColor("Unlit Color", Color) = (0,0,0,1)
        _OutlineColor("Outline Color", Color) = (0,0,0,1)

        _ShadeThreshold("Shade Threshold", Range(-1,1)) = 0.25
        _OutlineThreshold("Outline Threshold", Range(0,1)) = 0.35

        _SmoothK("Smooth Union K", Range(0.0, 0.5)) = 0.12
        _MaxSteps("Max Steps", Range(8, 128)) = 64
        _SurfaceEps("Surface Epsilon", Range(0.0005, 0.02)) = 0.005
        _MaxDistance("Max Distance", Range(0.5, 10)) = 3.0
        [Toggle] _EdgeSmoothing("Smooth Silhouette (MSAA)", Float) = 1

        [HideInInspector] _ContainerClipEnabled("Container Clip Enabled", Float) = 0
        [HideInInspector] _ContainerCenterRadius("Container Center Radius", Vector) = (0,0,0.5,1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "Mass"
            Cull Back ZWrite On ZTest LEqual
            AlphaToMask On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Amplifier Core/AmplifierMassSurface.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "SurfaceCorona"
            Cull Back ZWrite Off ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #define AMP_CORONA_PASS
            #include "Amplifier Core/AmplifierMassSurface.hlsl"
            ENDHLSL
        }
    }
}
