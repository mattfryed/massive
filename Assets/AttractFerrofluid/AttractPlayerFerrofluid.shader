Shader "MASSIVE/Study/Attract Player Ferrofluid"
{
    Properties
    {
        _Density("Surface density",Float)=9
        _Relief("Rounded relief",Float)=.1
        _Wetness("Highlight coverage",Float)=.86
        _RimWidth("Rim width",Float)=.2
        _RimAngle("Rim angle",Float)=-40
        _FluidTime("Loop time",Float)=0
        _WhiteDominant("White mounds",Float)=0
        _FormationCore("Reservoir size",Float)=1
    }
    SubShader
    {
        Tags {"RenderType"="Opaque" "Queue"="Geometry"}
        Pass
        {
            Cull Back ZWrite On Blend Off
            HLSLPROGRAM
            #pragma target 5.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Density,_Relief,_Wetness,_FluidTime,_RimWidth,_RimAngle,_WhiteDominant,_FormationCore;
            #include "AttractPlayerSurface.hlsl"
            struct appdata{float4 vertex:POSITION;};
            // Preserve per-sample evaluation of every hard black/white border.
            struct v2f{float4 position:SV_POSITION;sample float3 viewNormal:TEXCOORD0;sample float3 viewPosition:TEXCOORD1;sample float3 viewRadial:TEXCOORD2;sample float moundHeight:TEXCOORD3;};
            v2f vert(appdata input)
            {
                v2f o;float3 n=normalize(input.vertex.xyz),derivative;
                float r=formingSurfaceGradient(n,derivative);
                float3 normal=normalize(n*r-(derivative-n*dot(n,derivative)));
                float3 p=n*r*_FormationCore;
                o.position=UnityObjectToClipPos(float4(p,1));
                o.viewPosition=UnityObjectToViewPos(float4(p,1));
                o.viewNormal=mul((float3x3)UNITY_MATRIX_V,UnityObjectToWorldNormal(normal));
                o.viewRadial=mul((float3x3)UNITY_MATRIX_V,UnityObjectToWorldNormal(n));
                o.moundHeight=(r-.455)/max(_Relief,.001);
                return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float white=playerWhite(i.viewNormal,i.viewRadial,i.viewPosition,i.moundHeight);
                return float4(white,white,white,1);
            }
            ENDHLSL
        }
    }
}
