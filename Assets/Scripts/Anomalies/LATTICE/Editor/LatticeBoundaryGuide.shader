Shader "Hidden/MASSIVE/Lattice Editor Boundary"
{
    SubShader
    {
        Tags { "Queue"="Transparent+1" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off ZTest LEqual Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float2 _LatticeGuideSize;
            struct v2f { float4 position : SV_POSITION; float2 local : TEXCOORD0; };
            v2f vert(float4 vertex : POSITION)
            { v2f o; o.position = UnityObjectToClipPos(vertex); o.local = vertex.xy; return o; }
            float4 frag(v2f i) : SV_Target
            { clip(_LatticeGuideSize * .5 - abs(i.local)); return float4(.8,.9,1,.23); }
            ENDHLSL
        }
    }
}
