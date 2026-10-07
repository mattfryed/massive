Shader "MASSIVE/Amplifier Prismatic Particle"
{
    Properties
    {
        _MainTex ("Particle Shape", 2D) = "white" {}
        _CycleSeconds ("Rainbow Cycle (seconds)", Range(0.25, 12)) = 4
        _HueOffset ("Starting Hue", Range(0, 1)) = 0
        _Saturation ("Saturation", Range(0, 1)) = 0.95
        _Brightness ("Brightness", Range(0, 2)) = 1
        _Opacity ("Opacity", Range(0, 1)) = 0.85
        [HideInInspector] _PreviewTime ("Preview Time", Float) = -1
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        // Alpha compositing retains saturated color where the three clouds cross.
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _CycleSeconds, _HueOffset, _Saturation, _Brightness, _Opacity, _PreviewTime;
            struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct Varying { float4 position : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            Varying vert(Input v)
            {
                Varying o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }
            fixed4 frag(Varying i) : SV_Target
            {
                float clock = _PreviewTime >= 0 ? _PreviewTime : _Time.y;
                float hue = frac(clock / max(0.01, _CycleSeconds) + _HueOffset);
                float3 spectrum = saturate(abs(frac(hue + float3(0, 2.0/3.0, 1.0/3.0)) * 6 - 3) - 1);
                float3 color = lerp(float3(1,1,1), spectrum, _Saturation) * _Brightness;
                fixed4 shape = tex2D(_MainTex, i.uv);
                return fixed4(color * shape.rgb * i.color.rgb, shape.a * i.color.a * _Opacity);
            }
            ENDCG
        }
    }
    Fallback Off
}
