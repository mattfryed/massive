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
        _FormationCore("Collected liquid",Float)=0
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
            float4 _Attraction,_FluidDrops[8];
            #include "AttractFerrofluidField.hlsl"
            struct appdata{float4 vertex:POSITION;};
            struct v2f{float4 position:SV_POSITION;sample float3 localPosition:TEXCOORD0;};
            struct output{float4 color:SV_Target;float depth:SV_Depth;};
            v2f vert(appdata v)
            {
                // A fixed envelope, not a growing render mesh. The implicit
                // surface inside it can split and merge into separate droplets.
                v2f o;o.localPosition=normalize(v.vertex.xyz)*.79;
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
                        radius=fluidSurface(p/max(lengthP,.0001),unused);
                    distance=lengthP-radius*_FormationCore;
                }
                [unroll]for(int j=0;j<8;j++)
                {
                    float4 drop=_FluidDrops[j];
                    if(drop.w>.0001)distance=unionLiquid(distance,length(p-drop.xyz)-drop.w,.055+.045*_FormationCore);
                }
                return distance;
            }
            float fixture(float3 ray,float3 center,float width,float height)
            {
                center=normalize(center);float3 u=normalize(cross(float3(0,1,0),center)),v=cross(center,u);
                float2 p=float2(dot(ray,u)/width,dot(ray,v)/height),p2=p*p;
                return step(dot(p2*p2,float2(1,1)),1)*step(.3,dot(ray,center));
            }
            output frag(v2f i)
            {
                float3 cameraLocal=mul(unity_WorldToObject,float4(_WorldSpaceCameraPos,1)).xyz;
                float3 forward=mul((float3x3)unity_WorldToObject,mul((float3x3)UNITY_MATRIX_I_V,float3(0,0,-1)));
                float3 direction=normalize(lerp(i.localPosition-cameraLocal,forward,unity_OrthoParams.w));
                float3 p=i.localPosition;float travel=0,distance=1;
                [loop]for(int stepIndex=0;stepIndex<88;stepIndex++)
                {
                    distance=field(p);
                    if(distance<.0007 || travel>1.65)break;
                    float stepLength=max(distance*.58,.0004);travel+=stepLength;p+=direction*stepLength;
                }
                clip(.0007-distance);clip(1.65-travel);
                const float e=.0015;
                float3 normal=normalize(float3(field(p+float3(e,0,0))-field(p-float3(e,0,0)),field(p+float3(0,e,0))-field(p-float3(0,e,0)),field(p+float3(0,0,e))-field(p-float3(0,0,e))));
                float3 n=normalize(mul((float3x3)UNITY_MATRIX_V,UnityObjectToWorldNormal(normal)));
                float3 radial=normalize(mul((float3x3)UNITY_MATRIX_V,UnityObjectToWorldNormal(normalize(p))));
                float3 viewPosition=UnityObjectToViewPos(float4(p,1));
                float3 v=normalize(lerp(-viewPosition,float3(0,0,1),unity_OrthoParams.w)),reflected=reflect(-v,n);
                float scale=lerp(.55,1.2,saturate(_Wetness));
                float white=max(fixture(reflected,float3(-.55,.55,.7),.20*scale,.62*scale),max(fixture(reflected,float3(.72,-.15,.45),.11*scale,.48*scale),fixture(reflected,float3(.1,.78,-.4),.62*scale,.13*scale)));
                float angle=radians(_RimAngle);float3 backLight=normalize(float3(cos(angle),sin(angle),-1.7));
                float rim=step(.0001,_RimWidth)*step(dot(radial,v),_RimWidth)*step(.12,dot(radial.xy,backLight.xy))*step(.16,dot(n,backLight));
                white=max(white,rim);
                if(_WhiteDominant>.5)
                {
                    float3 unused;float height=(fluidSurface(normalize(p),unused)-.455)/max(_Relief,.001);
                    // White droplets acquire black valleys as the body joins.
                    white=step(.43*smoothstep(.35,.85,_FormationCore),height);
                }
                output o;o.color=float4(white,white,white,1);
                float4 clipPosition=UnityObjectToClipPos(float4(p,1));o.depth=clipPosition.z/clipPosition.w;
                return o;
            }
            ENDHLSL
        }
    }
}
