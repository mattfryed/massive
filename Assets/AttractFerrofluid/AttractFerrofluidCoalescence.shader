Shader "MASSIVE/Study/Attract Ferrofluid Coalescence"
{
    Properties
    {
        _Density("Surface density",Float)=9
        _Relief("Rounded relief",Float)=.1
        _Wetness("Highlight coverage",Float)=.86
        _RimWidth("Rim width",Float)=.2
        _RimAngle("Rim angle",Float)=-40
        _Attraction("Attraction offset",Vector)=(0,0,0,0)
        _FluidTime("Flow time",Float)=0
        _WhiteDominant("White mounds",Float)=0
        _FormationCore("Reservoir size",Float)=.24
        _FormationBounds("Conservative field radius",Float)=.8
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
            float _Density,_Relief,_Wetness,_FluidTime,_RimWidth,_RimAngle,_WhiteDominant,_FormationCore,_FormationBounds;
            float4 _FluidDrops[8];
            #include "AttractPlayerSurface.hlsl"
            struct appdata{float4 vertex:POSITION;};
            struct v2f{float4 position:SV_POSITION;sample float3 localPosition:TEXCOORD0;};
            struct output{float4 color:SV_Target;float depth:SV_Depth;};
            v2f vert(appdata v)
            {
                // A 12-triangle cube encloses the implicit surface. CPU bounds
                // track the core, attached drops and smooth-union expansion.
                v2f o;o.localPosition=v.vertex.xyz*(2*_FormationBounds);
                o.position=UnityObjectToClipPos(float4(o.localPosition,1));return o;
            }
            float unionLiquid(float a,float b,float k)
            {
                float h=max(k-abs(a-b),0)/k;
                return min(a,b)-h*h*k*.25;
            }
            float field(float3 p)
            {
                float distance=2;
                float lengthP=length(p);
                if(_FormationCore>.0001)
                {
                    float3 unused;float radius=.49;
                    // Only rays near the body need the fine surface kernels.
                    if(abs(lengthP-.49*_FormationCore)<.12)
                        radius=formingSurface(p/max(lengthP,.0001));
                    distance=lengthP-radius*_FormationCore;
                }
                [unroll]for(int j=0;j<8;j++)
                {
                    float4 drop=_FluidDrops[j];
                    if(drop.w>.0001)distance=unionLiquid(distance,length(p-drop.xyz)-drop.w,.055+.045*_FormationCore);
                }
                return distance;
            }
            float3 fieldNormal(float3 p)
            {
                float lengthP=max(length(p),.0001);float3 radial=p/lengthP,derivative=0;
                float radius=.49;
                if(abs(lengthP-.49*_FormationCore)<.12)radius=formingSurfaceGradient(radial,derivative);
                float distance=lengthP-radius*_FormationCore;
                float3 gradient=radial-(_FormationCore/lengthP)*(derivative-radial*dot(radial,derivative));
                float k=.055+.045*_FormationCore;
                [unroll]for(int j=0;j<8;j++)
                {
                    float4 drop=_FluidDrops[j];
                    if(drop.w>.0001)
                    {
                        float3 delta=p-drop.xyz;float dropLength=max(length(delta),.0001);
                        float d=dropLength-drop.w;
                        gradient=lerp(gradient,delta/dropLength,saturate(.5+.5*(distance-d)/k));
                        distance=unionLiquid(distance,d,k);
                    }
                }
                return normalize(gradient);
            }
            output frag(v2f i)
            {
                float3 cameraLocal=mul(unity_WorldToObject,float4(_WorldSpaceCameraPos,1)).xyz;
                float3 forward=mul((float3x3)unity_WorldToObject,mul((float3x3)UNITY_MATRIX_I_V,float3(0,0,-1)));
                float3 direction=normalize(lerp(i.localPosition-cameraLocal,forward,unity_OrthoParams.w));
                // Skip empty proxy corners and start at the bounding sphere.
                float b=dot(i.localPosition,direction);
                float discriminant=b*b-dot(i.localPosition,i.localPosition)+_FormationBounds*_FormationBounds;
                clip(discriminant);
                float span=sqrt(max(0,discriminant));
                float entry=max(0,-b-span),exit=-b+span;
                float3 p=i.localPosition+direction*entry;float travel=0,distance=1;
                float maxTravel=exit-entry;
                clip(maxTravel);
                [loop]for(int stepIndex=0;stepIndex<88;stepIndex++)
                {
                    distance=field(p);
                    if(distance<.0007 || travel>maxTravel)break;
                    float stepLength=max(distance*.58,.0004);travel+=stepLength;p+=direction*stepLength;
                }
                clip(.0007-distance);clip(maxTravel-travel);
                float3 normal=fieldNormal(p);
                float3 n=normalize(mul((float3x3)UNITY_MATRIX_V,UnityObjectToWorldNormal(normal)));
                float3 radial=normalize(mul((float3x3)UNITY_MATRIX_V,UnityObjectToWorldNormal(normalize(p))));
                float3 viewPosition=UnityObjectToViewPos(float4(p,1));
                float height=0;
                if(_WhiteDominant>.5)height=(formingSurface(normalize(p))-.455)/max(_Relief,.001);
                float white=playerWhite(n,radial,viewPosition,height);
                output o;o.color=float4(white,white,white,1);
                float4 clipPosition=UnityObjectToClipPos(float4(p,1));o.depth=clipPosition.z/clipPosition.w;
                return o;
            }
            ENDHLSL
        }
    }
}
