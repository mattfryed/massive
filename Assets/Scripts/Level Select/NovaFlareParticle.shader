Shader "MASSIVE/NOVA/Opaque Flare Particle"
{
    Properties
    {
        _MainTex ("Particle Shape", 2D) = "white" {}
        _Cutoff ("Shape Cutoff", Range(0.01,0.99)) = 0.5
        _Color ("Particle Color (RGB; Always Opaque)", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" "IgnoreProjector"="True" }
        Blend Off
        ZWrite On
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            half4 _Color;
            half _Cutoff;

            struct Attributes
            {
                float4 position : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 position : SV_POSITION;
                half3 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position = UnityObjectToClipPos(input.position);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color.rgb * _Color.rgb;
                UNITY_TRANSFER_FOG(output, output.position);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 shape = tex2D(_MainTex, input.uv);
                // Support both alpha masks and white-on-black circle textures.
                // Shape coverage is binary; particle/material alpha never fades it.
                clip(min(shape.a, max(shape.r, max(shape.g, shape.b))) - _Cutoff);
                half4 color = half4(input.color, 1);
                UNITY_APPLY_FOG(input.fogCoord, color);
                color.a = 1;
                return color;
            }
            ENDCG
        }
    }
    Fallback Off
}
