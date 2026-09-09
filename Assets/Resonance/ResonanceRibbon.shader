Shader "MASSIVE/Resonance/Ribbon"
{
    Properties
    {
        _Reveal ("Reveal", Range(0,1)) = 1
        _Ghost ("Ghost layer", Float) = 0
        _OrganicMotion ("Organic motion", Range(0,1)) = .32
        _FlowSpeed ("Flow speed", Float) = .7
        _ResonanceTime ("Animation clock", Float) = 0
        _FlowMode ("Flow mode", Float) = 0
        _Wavelength ("Radial wavelength", Float) = 2
        _PatternCenter ("Pattern center", Vector) = (0,0,0,0)
        _Filament ("Filament width opacity brightness softness", Vector) = (.22,.85,1.4,2)
        _Ribbon ("Ribbon width opacity brightness softness", Vector) = (1,.48,.85,4)
        _Diffuse ("Haze width opacity brightness softness", Vector) = (2.8,.16,.7,1.5)
        [HDR] _FilamentColor ("Filament color", Color) = (.65,.9,1,1)
        [HDR] _RibbonColor ("Ribbon color", Color) = (.65,.9,1,1)
        [HDR] _DiffuseColor ("Diffuse color", Color) = (.65,.9,1,1)
        _ArcColorInfluence ("Arc color influence", Range(0,1)) = 0
        _Plasma ("Displacement scale speed detail", Vector) = (.22,3,1.3,.35)
        _TipBoost ("Tip activity", Float) = 1
        [HideInInspector] _CurveMode ("Curve distance field", Float) = 0
        [HideInInspector] _HalfWidth ("Curve half width", Float) = .15
        [HideInInspector] _ArcIntensity ("Arc intensity", Float) = 1
        [HideInInspector] _SrcBlend ("Source blend", Float) = 5
        [HideInInspector] _DstBlend ("Destination blend", Float) = 10
        [HideInInspector] _BlendStyle ("Blend style", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            #include "ResonanceField.hlsl"
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float4 color : COLOR; float3 world : TEXCOORD0; float2 local : TEXCOORD1; };
            v2f vert(appdata v)
            {
                v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.color=v.color;
                o.world=mul(unity_ObjectToWorld,v.vertex).xyz; o.local=v.vertex.xz; return o;
            }
            float4 frag(v2f i) : SV_Target
            {
                ResonanceFieldInput field; field.color=i.color; field.world=i.world; field.local=i.local;
                return EvaluateResonanceField(field);
            }
            ENDCG
        }
    }
}
