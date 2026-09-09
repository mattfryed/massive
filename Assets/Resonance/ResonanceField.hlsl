#ifndef MASSIVE_RESONANCE_FIELD_INCLUDED
#define MASSIVE_RESONANCE_FIELD_INCLUDED
// Shared field equations. Option A evaluates pixels; Option B samples the same field with particles.
            float _Reveal, _Ghost, _OrganicMotion, _FlowSpeed, _ResonanceTime;
            float _FlowMode, _Wavelength, _BlendStyle, _ArcColorInfluence;
            float _CurveMode, _HalfWidth, _ArcIntensity, _TipBoost;
            int _CurveCount;
            float4 _CurvePoints[128]; // local XZ, taper envelope, cumulative distance
            float4 _PatternCenter, _Filament, _Ribbon, _Diffuse, _Plasma;
            float4 _FilamentColor, _RibbonColor, _DiffuseColor;
            int _ContactCoreCount, _ContactPlayerCount;
            float4 _ContactCore[4]; // arc distance, age (-1 = approach), strength, signed tangential bias
            float4 _ContactPlayer[4]; // arc distance, envelope, travel along tangent / normal
            float4 _ContactPath, _CoreContactShape, _CoreContactTiming, _PlayerContactShape;
            float _ContactNoiseTime;

#ifdef RESONANCE_PARTICLES
float _ParticleLayer;
#endif
struct ResonanceFieldInput { float4 color; float3 world; float2 local;
#ifdef RESONANCE_PARTICLES
float distance; float envelope; float along;
#endif
};
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p)
            {
                float2 b=floor(p), f=frac(p); f=f*f*f*(f*(f*6-15)+10);
                return lerp(lerp(hash(b),hash(b+float2(1,0)),f.x),lerp(hash(b+float2(0,1)),hash(b+1),f.x),f.y);
            }
            float fbm(float2 p)
            {
                return noise(p)*.57 + noise(p*2.03+17.1)*.28 + noise(p*4.11-7.3)*.15;
            }
            void curveDistance(float2 p, out float distance, out float envelope, out float along)
            {
                float best=1e20; distance=0; envelope=0; along=0;
                [loop] for (int j=0; j<_CurveCount-1; j++)
                {
                    float4 a=_CurvePoints[j], b=_CurvePoints[j+1];
                    float2 d=b.xy-a.xy;
                    float u=saturate(dot(p-a.xy,d)/max(1e-10,dot(d,d)));
                    float2 delta=p-lerp(a.xy,b.xy,u);
                    float dsq=dot(delta,delta);
                    if (dsq>=best) continue;
                    best=dsq;
                    float side=dot(delta,float2(-d.y,d.x))>=0 ? 1 : -1;
                    distance=sqrt(dsq)*side;
                    envelope=lerp(a.z,b.z,u); along=lerp(a.w,b.w,u);
                }
            }
            float band(float y, float4 layer)
            {
                float x=abs(y)/max(.01,layer.x);
                float aa=min(.3,max(.002,fwidth(x)));
                return exp(-pow(x*1.6,max(.5,layer.w))) * (1-smoothstep(.8-aa,1+aa,x)) * layer.y;
            }
            float4 composite(float3 rgb,float alpha)
            {
                if (_BlendStyle>1.5 && _BlendStyle<3.5) rgb*=alpha;
                if (_BlendStyle>3.5) rgb=lerp(float3(1,1,1),rgb,alpha);
                return float4(rgb,alpha);
            }
            float contactDelta(float along, float center)
            {
                float d=along-center;
                if (_ContactPath.y>.5)
                {
                    float length=max(.001,_ContactPath.x);
                    d-=floor(d/length+.5)*length;
                }
                return d;
            }
            float bell(float d,float width) { d/=max(.02,width); return exp(-3*d*d); }
            float recover(float t) { t=saturate(t); return 1-t*t*t*(t*(t*6-15)+10); }
            void contacts(float along, out float compression, out float heat, out float pulse,
                out float wake, out float wakeOffset, out float wakeNoise)
            {
                compression=0; heat=0; pulse=0; wake=0; wakeOffset=0; wakeNoise=0;
                [loop] for (int k=0;k<_ContactCoreCount;k++)
                {
                    float4 c=_ContactCore[k];
                    float local=bell(contactDelta(along,c.x),_CoreContactShape.x)*c.z;
                    if (c.y<0) { compression+=local*_CoreContactShape.y; continue; }
                    float life=c.y/max(.05,_CoreContactTiming.z);
                    float fade=recover(life);
                    compression+=local*_CoreContactShape.y*exp(-life*7)*fade;
                    heat+=local*_CoreContactShape.z*exp(-life*12)*fade;
                    float travel=c.y*_CoreContactTiming.x;
                    float width=_CoreContactTiming.y*(1+life);
                    float forward=bell(contactDelta(along,c.x+travel*(1+c.w*.2)),width)*(1+c.w*.6);
                    float backward=bell(contactDelta(along,c.x-travel*(1-c.w*.2)),width)*(1-c.w*.6);
                    pulse+=(forward+backward)*c.z*_CoreContactShape.w*fade;
                }
                [loop] for (int pIndex=0;pIndex<_ContactPlayerCount;pIndex++)
                {
                    float4 p=_ContactPlayer[pIndex];
                    float shift=p.z*_PlayerContactShape.z*_HalfWidth*.45;
                    float local=bell(contactDelta(along,p.x+shift),_PlayerContactShape.x)*p.y;
                    wake+=local;
                    wakeOffset+=local*p.w*_PlayerContactShape.z;
                    wakeNoise+=local*(fbm(float2(along*7-_ContactNoiseTime*2.5,_ContactNoiseTime*1.7))*2-1)*_PlayerContactShape.w;
                }
                compression=saturate(compression); heat=min(4,heat); pulse=min(3,pulse);
                wake=saturate(wake); wakeOffset=clamp(wakeOffset,-3,3); wakeNoise=clamp(wakeNoise,-1,1);
            }
            float4 EvaluateResonanceField(ResonanceFieldInput i)
            {
                if (_CurveMode<.5) return composite(i.color.rgb,i.color.a);
                float distance,envelope,along;
#ifdef RESONANCE_PARTICLES
                distance=i.distance; envelope=i.envelope; along=i.along;
#else
                curveDistance(i.local,distance,envelope,along);
#endif
                clip(envelope-.00001);
                float halfWidth=max(.00001,_HalfWidth*envelope);
                float extent=_Ghost>.5 ? 1 : max(_Filament.x,max(_Ribbon.x,_Diffuse.x));
                float extra=_Ghost>.5 ? 0 : 2.5+clamp(_PlayerContactShape.z,0,3);
                clip(halfWidth*(extent+_Plasma.x*(1+_TipBoost)+extra+.2)-abs(distance));
                float y=distance/halfWidth;
                float clock=_ResonanceTime*_FlowSpeed*(_FlowMode<1.5 ? 1 : 0);
                float2 radial=i.world.xz-_PatternCenter.xz;
                float radius=length(radial);
                float x=(_FlowMode<.5 ? radius : along)/max(.01,_Wavelength);
                float wave=sin((x-clock)*6.2831853)*.5+.5;
                float n=fbm(float2(x*3-clock*3,clock*.2));
                float plasmaTime=(_FlowMode<1.5 ? _ResonanceTime*_Plasma.z : 0);
                float2 domain=float2(along*_Plasma.y,plasmaTime);
                float coarse=fbm(domain+float2(fbm(domain*.43)*1.7,0))*2-1;
                float detail=noise(domain*float2(3.7,1.6)+31)*2-1;
                float tip=1+_TipBoost*(1-envelope);
                float warp=lerp(coarse,detail,_Plasma.w*.55)*_Plasma.x*tip*_OrganicMotion;
                float2 flow=radial/max(.001,radius)*clock*.25;
                float fine=fbm(i.world.xz*3.1-flow+coarse*.2);
                float grain=lerp(1,.7+fine*.4,_OrganicMotion);
                float compression=0,heat=0,pulse=0,wake=0,wakeOffset=0,wakeNoise=0;
                if (_Ghost<.5) contacts(along,compression,heat,pulse,wake,wakeOffset,wakeNoise);
                float4 filament=_Filament, ribbon=_Ribbon, haze=_Diffuse;
                filament.x*=1-.45*compression+.35*wake;
                ribbon.x*=1-.22*compression+.18*wake;
                haze.x*=1-.28*compression+.2*wake;
                float f=band(y-warp-wakeNoise*.3,filament)*_FilamentColor.a*(1-wake*_PlayerContactShape.y);
                float b=band(y-warp*.45-wakeOffset*.2-wakeNoise*.4,ribbon)*_RibbonColor.a*lerp(1,.75+.25*wave,_OrganicMotion);
                float h=band(y-warp*.2-wakeOffset-wakeNoise,haze)*_DiffuseColor.a*lerp(.75,.35+.65*fine,_OrganicMotion)*(1+.8*wake+.6*compression);
                float hot=heat*band(y-warp,float4(max(.4,_Ribbon.x),1,1,2));
                float traveling=pulse*band(y-warp*.5,float4(max(.4,_Ribbon.x)*1.2,1,1,2));
#ifdef RESONANCE_PARTICLES
                // The same field, separated into three particle populations.
                if (_ParticleLayer<.5) { b=0; h=0; hot*=.5; traveling*=.5; }
                else if (_ParticleLayer<1.5) { f=0; h=0; hot*=.5; traveling*=.5; }
                else { f=0; b=0; hot=0; traveling=0; }
#endif
                float sum=f+b+h+hot+traveling;
                float3 rgb=(f*_Filament.z*_FilamentColor.rgb*(1+compression)+b*_Ribbon.z*_RibbonColor.rgb*(1+.5*compression)
                    +h*_Diffuse.z*_DiffuseColor.rgb+hot*float3(2,2,2)+traveling*lerp(_FilamentColor.rgb,float3(1,1,1),.45)*1.6)/max(.001,sum);
                rgb*=_ArcIntensity*lerp(float3(1,1,1),i.color.rgb,_ArcColorInfluence);
                float alpha=i.color.a*envelope*_Reveal*saturate(sum)*grain;
                if (_Ghost>.5)
                {
                    alpha=i.color.a*envelope*_Reveal*band(y,float4(1,1,1,2))*(.7+.3*n);
                    rgb=i.color.rgb*_ArcIntensity;
                }
                return composite(rgb,alpha);
            }

#endif

