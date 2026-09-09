Shader "MASSIVE/AmplifierTreatment"
{
    SubShader
    {
        Tags { "Queue"="Transparent+40" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct input { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct output { float4 vertex:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            output vert(input v) { output o; o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color;o.uv=v.uv;return o; }
            fixed4 frag(output i):SV_Target { float r=length(i.uv); i.color.a*=1-smoothstep(0.25,1,r);return i.color; }
            ENDCG
        }
    }
}
