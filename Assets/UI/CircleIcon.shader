Shader "MASSIVE/UI/CircleIcon"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _FillColor    ("Fill Color", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Width (UV)", Range(0,0.5)) = 0.10
        _EdgeSoftness ("Edge Softness", Range(0,0.02)) = 0.002

        // ---- Standard UGUI masking plumbing (Mask component / stencil) ----
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
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

        // Important for correct UI depth behavior across canvas modes
        ZTest [unity_GUIZTestMode]

        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #pragma multi_compile __ UNITY_UI_CLIP_RECT
            #pragma multi_compile __ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color;

            fixed4 _FillColor;
            fixed4 _OutlineColor;
            float  _OutlineWidth;
            float  _EdgeSoftness;

            // RectMask2D drives this when UNITY_UI_CLIP_RECT is enabled.
            #ifdef UNITY_UI_CLIP_RECT
            float4 _ClipRect;
            #endif

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 uv            : TEXCOORD0;
                float4 worldPosition : TEXCOORD1; // (UGUI uses local/object space here)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;

                // vertex color already includes Image.color + CanvasGroup fades
                o.color = v.color * _Color;
                return o;
            }

            inline float ClipRectAlpha(float2 pos, float4 clipRect)
            {
                float2 inside = step(clipRect.xy, pos) * step(pos, clipRect.zw);
                return inside.x * inside.y;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);

                // circle in UV space (0..1)
                float2 p = i.uv - 0.5;
                float dist = length(p);
                float radius = 0.5;

                float inner = radius - saturate(_OutlineWidth);

                // Anti-alias in screen-space
                float aa = max(_EdgeSoftness, fwidth(dist));

                float outerAlpha = saturate((radius - dist) / aa);
                float innerAlpha = saturate((inner  - dist) / aa);

                // Outline outside inner radius, fill inside inner radius
                fixed4 col = lerp(_OutlineColor, _FillColor, innerAlpha);
                col.a *= outerAlpha;

                // Respect UI tint/fade and sprite alpha (sprite is usually white here)
                col *= i.color;
                col.a *= tex.a;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= ClipRectAlpha(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }
}
