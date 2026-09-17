Shader "MASSIVE/Study/Attract Ferrofluid"
{
    Properties
    {
        _Density("Surface density",Range(5,14))=9
        _Relief("Rounded relief",Range(.03,.16))=.1
        _Wetness("Highlight coverage",Range(0,1))=.86
        _RimWidth("Rim width",Range(0,.4))=.20
        _RimAngle("Rim angle",Range(-180,180))=-40
        _Attraction("Attraction offset",Vector)=(0,0,0,0)
        _FluidTime("Flow time",Float)=0
        _WhiteDominant("White mounds with black valleys",Float)=0
        _LogoField("Logo signed distance",2D)="black"{}
        _LogoEnabled("Embedded lettering enabled",Float)=0
        _LogoSpacing("Letter spacing",Float)=0
        _LogoReveal("Lettering arrival",Range(0,1))=1
        _LogoFlow("Letter ridge undulation",Range(0,1))=1
        _LogoTypeFlow("White type undulation",Range(0,1))=0
        _LogoMeshGuard("Letter floor geometry margin",Float)=.025
        _LogoSettings("Logo arc, height, elevation, recess",Vector)=(2.2,.45,.12,0)
        _LogoRight("Logo right",Vector)=(1,0,0,0)
        _LogoUp("Logo up",Vector)=(0,0,1,0)
        _LogoFront("Logo front",Vector)=(0,1,0,0)
    }
    SubShader
    {
        Tags {"RenderType"="Opaque" "Queue"="Geometry"}
        Pass
        {
            Cull Back ZWrite On ZTest LEqual Blend Off
            HLSLPROGRAM
            #pragma target 5.0
            #pragma vertex tessVert
            #pragma hull hull
            #pragma domain domain
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Density,_Relief,_Wetness,_FluidTime,_RimWidth,_RimAngle,_LogoEnabled,_LogoFlow,_LogoTypeFlow,_LogoMeshGuard,_WhiteDominant,_LogoSpacing;
            float4 _Attraction;
            sampler2D _LogoField;
            float4 _LogoSettings,_LogoRight,_LogoUp,_LogoFront;
            float4 _LogoField_TexelSize;
            float _LogoReveal;
            struct appdata{float4 vertex:POSITION;};
            struct controlPoint{float4 vertex:INTERNALTESSPOS;};
            struct tessFactors{float edge[3]:SV_TessFactor;float inside:SV_InsideTessFactor;};
            // Sample-frequency interpolation lets the camera's existing MSAA
            // resolve our internal hard borders, not just triangle silhouettes.
            // Each sample remains opaque pure black or pure white.
            struct v2f{float4 position:SV_POSITION;sample float3 viewNormal:TEXCOORD0;sample float3 viewPosition:TEXCOORD1;sample float3 viewRadial:TEXCOORD2;sample float3 localRadial:TEXCOORD3;sample float3 shoulderRadial:TEXCOORD4;sample float moundHeight:TEXCOORD5;sample float floorReached:TEXCOORD6;};

            float logoRestDistance(float3 n)
            {
                if(_LogoEnabled<.5)return -1;
                float front=dot(n,_LogoFront.xyz);
                if(front<=0)return -1;
                float2 angles=float2(atan2(dot(n,_LogoRight.xyz),front),asin(clamp(dot(n,_LogoUp.xyz),-1,1)));
                float2 uv=(angles-float2(0,_LogoSettings.z))/_LogoSettings.xy+.5;
                float2 bounded=saturate(uv);
                if(abs(_LogoSpacing)>.000001)
                {
                    // Midpoints of the six gaps in MassiveLogoDistance. Translate
                    // each complete outline, including its recess, without widening
                    // the glyph or changing its vertical scale.
                    const float cuts[6]={.1962890625,.3640136719,.5021972656,.6491699219,.7077636719,.8676757813};
                    int glyph=0;
                    [unroll]for(int j=0;j<6;j++)
                        glyph+=uv.x>cuts[j]+(j-2.5)*_LogoSpacing?1:0;
                    uv.x-=(glyph-3)*_LogoSpacing;
                    float lo=glyph==0?0:cuts[glyph-1];
                    float hi=glyph==6?1:cuts[glyph];
                    bounded=float2(clamp(uv.x,lo,hi),saturate(uv.y));
                }
                float outside=length((uv-bounded)*_LogoSettings.xy);
                if(outside>.2)return -1;
                // Exact distance to the source mesh outlines, packed as 16 bits
                // in RG. Linear decoding commutes with bilinear filtering. B
                // marks the new encoding so older study textures still work.
                float3 encoded=tex2Dlod(_LogoField,float4(bounded,0,0)).rgb;
                float value=encoded.b>.99?dot(encoded.rg,float2(256.0/257.0,1.0/257.0)):encoded.r;
                float range=encoded.b>.99?256:64;
                // Extend the negative field beyond the texture padding, so the
                // outer M/E shoulders do not end in a sudden vertical wall.
                return (value-.5)*range*_LogoField_TexelSize.x*_LogoSettings.x-outside;
            }

            #include "AttractFerrofluidField.hlsl"
            float arrivalDepth()
            {
                // Sink the small initial opening before uncovering its floor.
                // The outward retreat resumes as soon as this downstroke ends.
                return smoothstep(.20,.34,saturate(_LogoReveal));
            }
            float logoDistance(float3 n)
            {
                // At both endpoints this is the original continuous liquid
                // shell or the exact approved recessed lettering, respectively.
                if(_LogoReveal<=0)return -1;
                float d=logoRestDistance(n);
                if(_LogoReveal>=1 || d<-.2)return d;
                float p=saturate(_LogoReveal);
                float opening=p<=.22?.4*smoothstep(.12,.22,p):lerp(.4,1,smoothstep(.34,1,p));
                float remaining=1-opening;
                float scale=_LogoSettings.x/2.2;
                float2 local=float2(dot(n,_LogoRight.xyz),dot(n,_LogoUp.xyz))/max(scale,.1);
                // Eroding the signed field opens each stroke from its thick
                // interior. Broad, flowing perturbations leave rounded liquid
                // scallops at the retreating edge, then disappear completely.
                float wave=noise(float3(local*32,p*.8+7.3));
                float detail=noise(float3(local*53+13.7,p*.6+21.1));
                float retreat=(.048*remaining+.17*pow(remaining,9))*scale;
                retreat*=clamp(.58+.65*wave+.18*detail,.45,1.55);
                return d-retreat;
            }
            float arrivalCollarSettle()
            {
                // The broad resting collar must not announce the whole recess
                // before any white is exposed. Open with a narrow liquid lip,
                // then relax into the approved ridge near the end of arrival.
                return smoothstep(.65,1,saturate(_LogoReveal));
            }
            float radius(float3 n,out float3 logoN,out float floorReached)
            {
                float3 moundSlope;float surface=fluidSurface(n,moundSlope);
                float3 tangentSlope=moundSlope-n*dot(n,moundSlope);
                // Transport the outer shoulder only. The inner letter boundary
                // and the recessed spherical floor keep their original mapping.
                float transport=.012*min(_LogoSettings.x/2.2,1)*(_Relief/.1)*saturate(_LogoFlow);
                float collarSettle=arrivalCollarSettle();
                if(_LogoReveal<1)transport*=lerp(.1,1,collarSettle);
                logoN=normalize(n+tangentSlope*transport);
                float bevelScale=_LogoSettings.x/2.2;
                float d=logoDistance(n);
                // Include the vertices supporting every white fragment in the
                // stable floor. Otherwise adjacent moving vertices still tug at
                // a fixed UV outline through rasterization of their triangles.
                // Clearance at the inner wall also prevents taller neighboring
                // mounds from leaning across the white face in the front view.
                float clearance=.006*bevelScale+.65*_LogoSettings.w;
                float inner=.006*bevelScale-_LogoMeshGuard-clearance;
                float outer=inner-.042*bevelScale;
                if(_LogoReveal<1)
                {
                    float partingInner=.006*bevelScale-(.003*bevelScale+.12*_LogoMeshGuard);
                    outer=lerp(partingInner-.008*bevelScale,outer,collarSettle);
                    inner=lerp(partingInner,inner,collarSettle);
                }
                float shoulder=1-smoothstep(outer,inner,d);
                float movedDistance=d+clamp(logoDistance(logoN)-d,-.014*bevelScale,.014*bevelScale)*shoulder;
                float cut=smoothstep(outer,inner,movedDistance);
                if(_LogoReveal<1)cut*=arrivalDepth();
                floorReached=cut;
                float floorRadius=.455+_Relief*.42-_LogoSettings.w;
                // Independently couple the white floor to the actual mound
                // heights. Zero preserves the stable type exactly. The same
                // cut blends its motion into the inner wall; the outer junction
                // retains its own transport and is unchanged where cut is zero.
                floorRadius=lerp(floorRadius,surface-_LogoSettings.w,saturate(_LogoTypeFlow));
                return lerp(surface,floorRadius,cut);
            }
            float radius(float3 n,out float3 logoN){float unused;return radius(n,logoN,unused);}
            float radius(float3 n){float3 unused;return radius(n,unused);}
            controlPoint tessVert(appdata input){controlPoint o;o.vertex=input.vertex;return o;}
            float edgeDetail(float3 a,float3 b)
            {
                if(_LogoEnabled<.5)return 1;
                float d=abs(logoRestDistance(normalize(a+b)));
                float band=.085*_LogoSettings.x/2.2+_LogoMeshGuard;
                // Only the lettering area receives extra geometry. A shared
                // edge uses the same midpoint from both triangles, so the
                // tessellator joins the refined collar without cracks.
                return lerp(1,4,1-smoothstep(band*.65,band,d));
            }
            tessFactors patchConstants(InputPatch<controlPoint,3> p)
            {
                tessFactors o;
                o.edge[0]=edgeDetail(p[1].vertex.xyz,p[2].vertex.xyz);
                o.edge[1]=edgeDetail(p[2].vertex.xyz,p[0].vertex.xyz);
                o.edge[2]=edgeDetail(p[0].vertex.xyz,p[1].vertex.xyz);
                o.inside=max(o.edge[0],max(o.edge[1],o.edge[2]));return o;
            }
            [domain("tri")][partitioning("fractional_odd")][outputtopology("triangle_cw")]
            [outputcontrolpoints(3)][patchconstantfunc("patchConstants")]
            controlPoint hull(InputPatch<controlPoint,3> p,uint id:SV_OutputControlPointID){return p[id];}
            v2f vert(appdata input)
            {
                v2f o;float3 n=normalize(input.vertex.xyz);
                float3 tangent=normalize(cross(abs(n.y)<.9?float3(0,1,0):float3(1,0,0),n));
                float3 bitangent=cross(n,tangent);
                const float e=.0015;
                float3 nt=normalize(n+tangent*e),nb=normalize(n+bitangent*e);
                float3 mt=normalize(n-tangent*e),mb=normalize(n-bitangent*e);
                float3 logoN;float r=radius(n,logoN,o.floorReached);float3 p=n*r;
                float3 normal=normalize(cross(nt*radius(nt)-mt*radius(mt),nb*radius(nb)-mb*radius(mb)));
                o.position=UnityObjectToClipPos(float4(p,1));
                o.viewPosition=UnityObjectToViewPos(float4(p,1));
                o.viewNormal=mul((float3x3)UNITY_MATRIX_V,UnityObjectToWorldNormal(normal));
                o.viewRadial=mul((float3x3)UNITY_MATRIX_V,UnityObjectToWorldNormal(n));
                o.localRadial=n;
                o.shoulderRadial=logoN;
                o.moundHeight=(r-.455)/max(_Relief,.001);
                return o;
            }
            [domain("tri")]
            v2f domain(tessFactors factors,const OutputPatch<controlPoint,3> p,float3 bary:SV_DomainLocation)
            {
                appdata input;input.vertex=p[0].vertex*bary.x+p[1].vertex*bary.y+p[2].vertex*bary.z;
                return vert(input);
            }
            float reflectionShape(float3 ray,float3 center,float width,float height)
            {
                center=normalize(center);float3 u=normalize(cross(float3(0,1,0),center)),v=cross(center,u);
                float2 p=float2(dot(ray,u)/width,dot(ray,v)/height);
                float2 p2=p*p;
                return step(dot(p2*p2,float2(1,1)),1)*step(.3,dot(ray,center));
            }
            float4 frag(v2f i):SV_Target
            {
                float3 n=normalize(i.viewNormal),v=normalize(lerp(-i.viewPosition,float3(0,0,1),unity_OrthoParams.w));
                float3 reflected=reflect(-v,n);
                // Coverage changes the size of the white shapes, never brightness
                // or opacity. Hard boundaries preserve a strictly two-color surface.
                float scale=lerp(.55,1.2,saturate(_Wetness));
                float key=reflectionShape(reflected,float3(-.55,.55,.7),.20*scale,.62*scale);
                float side=reflectionShape(reflected,float3(.72,-.15,.45),.11*scale,.48*scale);
                float upper=reflectionShape(reflected,float3(.1,.78,-.4),.62*scale,.13*scale);
                // A rear light catches only a narrow part of the outer silhouette.
                // The radial gate prevents interior folds from becoming rim marks;
                // the real displaced normal makes the edge follow the moving lobes.
                float angle=radians(_RimAngle);
                float3 backLight=normalize(float3(cos(angle),sin(angle),-1.7));
                float3 radial=normalize(i.viewRadial);
                float rim=step(.0001,_RimWidth)*step(dot(radial,v),_RimWidth);
                rim*=step(.12,dot(radial.xy,backLight.xy))*step(.16,dot(n,backLight));
                float white=max(max(key,max(side,upper)),rim);
                // Team one's small menu spheres expose the same moving relief
                // as white crests and black valleys. The title keeps its original
                // reflection treatment; every sample is still opaque black/white.
                if(_WhiteDominant>.5)white=step(.425-saturate(_Wetness)*.06,i.moundHeight);
                float lettering=logoDistance(normalize(i.localRadial));
                // Black shoulder and wall separate the white inlay from passing
                // reflections. There is no translucent decal or text overlay.
                float bevelScale=_LogoSettings.x/2.2;
                float shoulder=logoDistance(normalize(i.shoulderRadial));
                float clearance=.006*bevelScale+.65*_LogoSettings.w;
                float restingBorder=-.018*bevelScale-_LogoMeshGuard-clearance;
                float partingBorder=-.001*bevelScale-.12*_LogoMeshGuard;
                float border=restingBorder;
                if(_LogoReveal<1)border=lerp(partingBorder,restingBorder,arrivalCollarSettle());
                float letterWhite=step(.006*bevelScale,lettering);
                // Gate on the actual tessellated surface, interpolated at each
                // MSAA sample. A white outline alone could run across triangles
                // still descending into the recess. Keep those liquid-black
                // until their supporting surface has reached the letter floor.
                if(_LogoReveal<1)letterWhite*=step(.99999,i.floorReached);
                if(arrivalDepth()>.01 && (shoulder>border || lettering>.006*bevelScale))white=letterWhite;
                return float4(white,white,white,1);
            }
            ENDHLSL
        }
    }
}
