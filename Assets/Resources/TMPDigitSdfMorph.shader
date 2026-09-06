// Resources placement keeps the runtime-created morph material available in player builds.
Shader "MASSIVE/UI/TMP Digit SDF Morph"
{
    Properties
    {
        [PerRendererData] _MainTex ("TMP Font Atlas", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FromUVRect ("Outgoing Atlas Rect", Vector) = (0,0,1,1)
        _ToUVRect ("Incoming Atlas Rect", Vector) = (0,0,1,1)
        _FromBounds ("Outgoing Cell Bounds", Vector) = (0,0,1,1)
        _ToBounds ("Incoming Cell Bounds", Vector) = (0,0,1,1)
        _Morph ("Morph", Range(0,1)) = 0
        _EdgeSoftness ("Edge Softness", Range(0.25,3)) = 1
        _ContourBias ("Contour Bias", Range(-0.2,0.2)) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
        [HideInInspector] _ClipRect ("Clip Rect", Vector) = (-32767,-32767,32767,32767)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="False"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "DigitMorph"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile __ UNITY_UI_CLIP_RECT
            #pragma multi_compile __ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _FromUVRect;
            float4 _ToUVRect;
            float4 _FromBounds;
            float4 _ToBounds;
            float _Morph;
            float _EdgeSoftness;
            float _ContourBias;
            float4 _ClipRect;

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            v2f vert(appdata_t input)
            {
                v2f output;
                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }

            float SampleGlyph(float2 panelUv, float4 bounds, float4 atlasRect)
            {
                float2 size = max(bounds.zw - bounds.xy, float2(0.00001, 0.00001));
                float2 glyphUv = (panelUv - bounds.xy) / size;
                float inside =
                    step(0.0, glyphUv.x) * step(glyphUv.x, 1.0) *
                    step(0.0, glyphUv.y) * step(glyphUv.y, 1.0);
                float2 atlasUv = lerp(atlasRect.xy, atlasRect.zw, saturate(glyphUv));
                return tex2D(_MainTex, atlasUv).a * inside;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float outgoing = SampleGlyph(input.uv, _FromBounds, _FromUVRect);
                float incoming = SampleGlyph(input.uv, _ToBounds, _ToUVRect);
                float distanceField = lerp(outgoing, incoming, saturate(_Morph));
                distanceField += _ContourBias;

                float antialias = max(fwidth(distanceField) * _EdgeSoftness, 0.0025);
                float alpha = smoothstep(0.5 - antialias, 0.5 + antialias, distanceField);
                fixed4 color = input.color;
                color.a *= alpha;

                #ifdef UNITY_UI_CLIP_RECT
                    color.a *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                    clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }

    Fallback "UI/Default"
}
