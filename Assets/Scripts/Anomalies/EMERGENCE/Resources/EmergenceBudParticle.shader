Shader "MASSIVE/Emergence/Opaque Reward Bud"
{
    Properties
    {
        _MainTex("Particle shape", 2D) = "white" {}
        _Color("Color", Color) = (1,1,1,1)
        _Spectrum("Prismatic", Float) = 0
        _CycleSeconds("Cycle seconds", Float) = 4
        _HueOffset("Starting hue", Float) = 0
        _Saturation("Saturation", Float) = .95
        _Brightness("Brightness", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest+10" "RenderType"="TransparentCutout" }
        Pass
        {
            Blend Off ZWrite On Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _Color;
            float _Spectrum, _CycleSeconds, _HueOffset, _Saturation, _Brightness;
            struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float3 color:COLOR; };
            Varying vert(Input v) { Varying o; o.position=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color.rgb*_Color.rgb;return o; }
            fixed4 frag(Varying i):SV_Target
            {
                fixed4 shape=tex2D(_MainTex,i.uv);clip(shape.a-.5);
                float hue=frac(_Time.y/max(.01,_CycleSeconds)+_HueOffset);
                float3 spectrum=saturate(abs(frac(hue+float3(0,2.0/3.0,1.0/3.0))*6-3)-1);
                float3 tint=lerp(float3(1,1,1),lerp(float3(1,1,1),spectrum,_Saturation)*_Brightness,_Spectrum);
                return fixed4(shape.rgb*i.color*tint,1);
            }
            ENDCG
        }
    }
}
