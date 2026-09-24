Shader "MASSIVE/Level Select/Live Particle Icon"
{
    Properties { _MainTex ("Live Image", 2D) = "black" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        // Source particles are additive. Their accumulated RGB already includes
        // their original coverage; do not multiply it by capture alpha again.
        Blend One One
        ColorMask RGB
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            half4 frag(v2f i) : SV_Target { return half4(tex2D(_MainTex, i.uv).rgb, 1); }
            ENDCG
        }
    }
}
