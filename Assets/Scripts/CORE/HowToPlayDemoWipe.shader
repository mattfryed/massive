Shader "MASSIVE/UI/How To Demo Wipe"
{
    Properties
    {
        [PerRendererData] _MainTex ("Outgoing frame", 2D) = "black" {}
        _Progress ("Reveal progress", Range(0, 1)) = 0
        _Direction ("Direction", Float) = 1
        _CaptureRect ("Screen region", Vector) = (0, 0, 1, 1)
        _Ripple ("Ripple amount", Float) = .012
        _Softness ("Edge softness", Float) = .002
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _CaptureRect;
            float _Progress, _Direction, _Ripple, _Softness;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            v2f vert(appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 local = (i.uv - _CaptureRect.xy) / _CaptureRect.zw;
                float across = _Direction > 0 ? local.x : 1 - local.x;
                float ripple = (sin(local.y * 12.56637 + _Progress * 1.1) +
                    .35 * sin(local.y * 21.99115 - _Progress * .8)) * _Ripple * sin(_Progress * 3.14159);
                float padding = _Ripple * 1.35 + _Softness * 2;
                float edge = lerp(-padding, 1 + padding, _Progress) + ripple;
                float coverage = smoothstep(edge - _Softness, edge + _Softness, across);
                float2 captureUV = i.uv;
                // ScreenCapture follows the graphics API's render-target origin.
                #if UNITY_UV_STARTS_AT_TOP
                    captureUV.y = 1 - captureUV.y;
                #endif
                fixed4 color = tex2D(_MainTex, captureUV) * i.color;
                color.a = coverage * i.color.a;
                return color;
            }
            ENDCG
        }
    }
}
