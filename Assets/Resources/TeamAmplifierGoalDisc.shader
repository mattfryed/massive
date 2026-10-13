Shader "MASSIVE/Team Amplifier Goal Disc"
{
    Properties { _Color("Circle Color", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "Queue"="Transparent-9" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "TeamAmplifierGoalClip.cginc"
            fixed4 _Color;
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; };
            v2f vert(appdata_base v)
            {
                v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.texcoord.xy*2-1;
                o.world=mul(unity_ObjectToWorld,v.vertex).xyz; return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float d=1-length(i.uv);
                return fixed4(_Color.rgb,_Color.a*saturate(d/max(fwidth(d),.00001)+.5)*TeamGoalCoverage(i.world));
            }
            ENDCG
        }
    }
}
