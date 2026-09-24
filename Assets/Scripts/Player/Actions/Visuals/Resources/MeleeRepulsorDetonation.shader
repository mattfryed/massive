Shader "MASSIVE/MeleeRepulsorDetonation"
{
    SubShader
    {
        Tags { "Queue"="Transparent+22" "RenderType"="Transparent" "IgnoreProjector"="True" "DisableBatching"="True" }
        Cull Front ZWrite Off ZTest LEqual
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _PulseOrigin, _PulseBounds, _PulseShape, _PulseMotion, _PulseLayers, _PulseLight, _PulsePhase, _PulseBurst, _PulseRange;
            float4 _DetonationLayers, _DetonationArt;
            struct v2f { float4 pos:SV_POSITION; float3 world:TEXCOORD0; };
            struct output { float4 color:SV_Target; float depth:SV_Depth; };
            v2f vert(float4 v:POSITION) { v2f o; o.pos=UnityObjectToClipPos(v); o.world=mul(unity_ObjectToWorld,v).xyz; return o; }
            float hash(float3 p) { p=frac(p*.1031); p+=dot(p,p.yzx+33.33); return frac((p.x+p.y)*p.z); }
            float noise(float3 p)
            {
                float3 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(lerp(hash(i),hash(i+float3(1,0,0)),f.x),lerp(hash(i+float3(0,1,0)),hash(i+float3(1,1,0)),f.x),f.y),
                    lerp(lerp(hash(i+float3(0,0,1)),hash(i+float3(1,0,1)),f.x),lerp(hash(i+float3(0,1,1)),hash(i+1),f.x),f.y),f.z);
            }
            float fbm(float3 p) { return noise(p)*.57+noise(p*2.07+7.1)*.28+noise(p*4.19-3.7)*.15; }
            float gaussian(float v) { return exp(-v*v); }

            // Extinction and emission are independent: radiance must not turn
            // transparent hot gas into an opaque silver surface.
            float2 medium(float3 world)
            {
                float size=max(.001,_PulseShape.w);
                float3 p=world/size;
                float rho=length(p.xz), outline=_PulseRange.x/size;
                float radius=_PulseShape.x/size, age=_PulseBurst.w;
                float fade=_PulsePhase.z, flow=_PulseMotion.x, seed=_PulseMotion.y;
                float3 seedOffset=seed*float3(2.37,4.17,1.13);
                float3 dir=normalize(p+float3(.0001,0,0));
                float2 planar=p.xz/max(.0001,rho);
                float ext=0, em=0;
                float shellDepth=max(.1,_DetonationArt.y);
                float insideBody=smoothstep(outline*.80,outline*1.02,rho);
                if(age<0)
                {
                    float charge=_PulseRange.z;
                    float chargeRadius=outline*lerp(2.5,1.03,pow(charge,1.5));
                    float3 q=p/max(.1,chargeRadius)*3.5+seedOffset;
                    float n=fbm(q+dir*charge*3);
                    float ellipsoid=length(float3(p.x,p.y*1.7,p.z));
                    float shell=gaussian((ellipsoid-chargeRadius-(n-.5)*.18)/(.025+outline*.055));
                    float channels=pow(saturate(1-abs(sin((n+noise(q*3))*16))),10);
                    float veil=gaussian((ellipsoid-chargeRadius)/(.09+outline*.15))*pow(n,3);
                    em=(shell*(.2+channels*3)+veil*.7)*pow(charge,1.4)*_DetonationLayers.x*_DetonationArt.z;
                    ext=veil*.15*_DetonationLayers.x;
                    return float2(ext,em)*insideBody/size;
                }

                float progress=_PulsePhase.y;
                float activeDecay=1-smoothstep(.0,.34,fade);
                float frontWidth=.010+.005*radius;
                float3 angular=float3(planar.x*6,p.y*2,planar.y*6)+seedOffset;
                float rough=noise(angular*2+float3(flow*2,0,flow));
                float frontOffset=(rough-.5)*.038*_PulseMotion.z*2.5;
                float crest=gaussian((rho-radius-frontOffset)/frontWidth)*gaussian(p.y/(.08+radius*.035));
                float crestSector=.13+smoothstep(.38,.67,noise(angular*.48+flow));
                em+=crest*(1.2+rough*3)*crestSector*activeDecay*_PulseLayers.y*_PulseLight.y;

                // Independent source discharge and short planar seismic radiance.
                float flash=exp(-age*55)*_PulseBurst.x;
                float spherical=length(p);
                float source=exp(-pow(spherical/max(.025,outline*.85+age*2),3));
                float sourceGrain=fbm(p*9+seedOffset-dir*flow*13);
                em+=source*(1+sourceGrain*3)*flash*25;
                float2 axis=float2(.94,.342), other=float2(-axis.y,axis.x);
                float along=dot(p.xz,axis), across=dot(p.xz,other);
                float lens=gaussian(across/(.018+.08*age))*gaussian(p.y/.11)*exp(-abs(along)/max(.15,outline*2.3));
                float lensHaze=gaussian(across/.13)*gaussian(p.y/.15)*exp(-abs(along)/max(.1,outline*1.5));
                em+=(lens*14+lensHaze*.6)*exp(-age*24)*_DetonationLayers.z;

                // A curved ellipsoidal blast shell follows the fast planar front.
                float blastRadius=lerp(outline*1.06,radius,.81);
                float blastHeight=max(.15,blastRadius*shellDepth);
                float3 ell=float3(p.x,p.y*blastRadius/blastHeight,p.z);
                float er=length(ell);
                float3 local=ell/max(.08,blastRadius);
                float3 q=local*3.2+seedOffset;
                q-=normalize(ell+float3(.001,0,0))*flow*1.7;
                float3 warp=float3(noise(q+19),noise(q.yzx+7),noise(q.zxy-13))-.5;
                float broad=noise(q+warp*1.6);
                float fine=noise(q*2.2+warp*2.2);
                float grain=noise(q*5.3+warp*3.5);
                float turbulenceField=broad*.6+fine*.28+grain*.12;
                float turbulent=(broad-.5)*(.10+blastRadius*.17)*(_PulseMotion.z*2.5+.25);
                float distance=er-blastRadius-turbulent;
                float thickness=.06+blastRadius*.09;
                float shell=gaussian(distance/thickness);
                float holes=smoothstep(.35+fade*.35,.56+fade*.35,turbulenceField);
                float erosion=lerp(1,holes,_PulseBurst.z);
                float cloud=shell*erosion;
                float hot=smoothstep(.53,.72,fine)*smoothstep(.45,.64,broad);
                float veins=hot*cloud*smoothstep(.22,.72,grain);
                float fineDischarge=pow(saturate(1-abs((fine-.5)*6+(grain-.5)*.7)),10)*cloud*smoothstep(.53,.69,broad);
                float illumination=.035+pow(saturate((turbulenceField-.22)*1.6),3)*1.4;
                float late=1-smoothstep(.18,.88,fade);
                float shellOn=_DetonationLayers.y;
                ext+=cloud*_DetonationLayers.w*(1.6+fine)*late*shellOn;
                em+=cloud*illumination*late*shellOn*(1.0-progress*.65);
                em+=(veins*2.0+fineDischarge*.55)*_PulseLayers.z*_PulseLight.z*late*shellOn;
                // Coarse radial jets carry the acceleration; the separate fine
                // discharge mesh supplies the high-frequency detail.
                float jetNoise=noise(float3(planar*8+seed, rho*.45-flow*4));
                float jets=pow(saturate((jetNoise-.47)*2.6),4);
                float jetEnvelope=smoothstep(outline*.5,outline*1.3,rho)*(1-smoothstep(radius*.72,radius,rho));
                em+=jets*jetEnvelope*gaussian(p.y/(.10+rho*.1))*.6*_PulseLayers.z*late;

                // Radially stretched vapor thins into disconnected residues.
                float back=radius-rho;
                float wake=smoothstep(0,.12,back)*(1-smoothstep(radius*.35,radius*.85,back));
                float vapor=pow(saturate((turbulenceField-.30)*1.5),2)*wake*gaussian(p.y/max(.1,blastHeight*.7));
                vapor*=smoothstep(.05,.4,progress)*(1-smoothstep(.1,1,fade));
                ext+=vapor*.22*_DetonationLayers.w*_PulseLayers.w;
                em+=vapor*.24*_PulseLayers.w*_PulseLight.w;
                float support=1-smoothstep(radius+.15,radius+.4,rho);
                em*=lerp(1,insideBody,smoothstep(.05,.10,age));
                ext*=insideBody;
                return float2(ext,em)*support/size;
            }

            output frag(v2f i)
            {
                float opacity=saturate(_PulsePhase.x); clip(opacity-.001);
                float3 forward=normalize(-UNITY_MATRIX_V[2].xyz);
                float3 direction=normalize(i.world-_WorldSpaceCameraPos), origin=_WorldSpaceCameraPos;
                if(unity_OrthoParams.w>.5) { direction=forward; origin=i.world+direction*dot(_WorldSpaceCameraPos-i.world,direction); }
                float3 relative=origin-_PulseOrigin.xyz, bounds=max(_PulseBounds.xyz,.0001);
                float3 safe=(step(0,direction)*2-1)*max(abs(direction),.000001);
                float3 aa=(-bounds-relative)/safe, bb=(bounds-relative)/safe;
                float3 nearT=min(aa,bb), farT=max(aa,bb);
                float entry=max(0,max(nearT.x,max(nearT.y,nearT.z)));
                entry=max(entry,_ProjectionParams.y/max(.0001,dot(direction,forward)));
                entry=max(entry,_ProjectionParams.y/max(.0001,dot(direction,forward)));
                float exit=min(farT.x,min(farT.y,farT.z)); clip(exit-entry-.00001);
                const int Steps=64;
                float stepM=(exit-entry)/Steps;
                float trans=1, radiance=0, first=-1;
                [loop] for(int s=0;s<Steps;s++)
                {
                    float d=entry+(s+.5)*stepM;
                    float2 v=medium(relative+direction*d);
                    float absorb=1-exp(-max(0,v.x)*stepM);
                    radiance+=trans*max(0,v.y)*stepM;
                    trans*=1-absorb;
                    if(first<0 && (1-trans+radiance)*opacity>.003) first=d;
                    if(trans<.01) break;
                }
                float emission=1-exp(-radiance*max(0,_DetonationArt.x)*1.8);
                float alpha=(1-trans)*opacity;
                emission*=opacity;
                clip(max(alpha,emission)-.002);
                output o;
                o.color=float4(emission.xxx,alpha);
                float4 projected=mul(UNITY_MATRIX_VP,float4(origin+direction*max(entry,first),1));
                o.depth=projected.z/projected.w;
                #if defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
                    o.depth=o.depth*.5+.5;
                #endif
                return o;
            }
            ENDCG
        }
    }
    Fallback Off
}
