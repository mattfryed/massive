#ifndef MASSIVE_AMPLIFIER_GOAL_SURFACE
#define MASSIVE_AMPLIFIER_GOAL_SURFACE
// These values are local to the score renderer's property block. Zero enabled
// preserves the original score surface in scenes without the workbench.
float _AmpGoalEnabled, _AmpGoalTime;
float4 _AmpGoalOriginWS, _AmpGoalPulse, _AmpGoalShape, _AmpGoalCorona;
float4 _AmpGoalStyle, _AmpGoalWaveA, _AmpGoalWaveB, _AmpGoalWaveC;
float4 _AmpGoalPoles, _AmpGoalBranch, _AmpGoalMode, _AmpGoalTrain;
float4 _AmpSeam, _AmpSeamModes;
float _AmpSeamSplit;
float4 _AmpCloud, _AmpFlare, _AmpPaletteA, _AmpPaletteB, _AmpPaletteC;
float4 _AmpCloudShimmer, _AmpFlareShimmer, _AmpWispShimmer;
float4 _AmpInterferenceShimmer;
float4 _AmpGridEffectGate;
float4 _GridSize;
float4x4 _AmpGridWorldToLocal, _AmpGridLocalToWorld;
#include "../../VectorGridNu/AmplifierGridTreatment.hlsl"
static float3 ampGridCorrectionOS;
float3 AmpGridInverse(float3 world)
{
    float3 target=mul(_AmpGridWorldToLocal,float4(world,1)).xyz;
    float3 q=target;
    // Invert the same continuous map used by grid junctions. Damped iterations
    // avoid oscillation at the steeper side of a packet.
    [unroll] for(int j=0;j<5;j++) q+=0.7*(target-AmpDisplace(q,q.xy));
    return mul(_AmpGridLocalToWorld,float4(q,1)).xyz;
}

float3 AmpGoalWarp(float3 pOS)
{
    if (_AmpGoalEnabled < 0.5) return pOS;
    pOS+=ampGridCorrectionOS;
    float3 p = mul(unity_ObjectToWorld, float4(pOS,1)).xyz;
    float2 delta = p.xz - _AmpGoalOriginWS.xz;
    float r = length(delta), angle = atan2(delta.y, delta.x);
    float2 n = delta / max(0.001,r), tangent = float2(-n.y,n.x);
    float2 displacement = 0;
    [loop] for(int packet=0;packet<(int)_AmpGoalTrain.x;packet++)
    {
    float age = _AmpGoalPulse.x-packet*_AmpGoalTrain.y;
    if(age >= 0 && age < 8 && _AmpGoalPulse.y > 0)
    {
        float width = sqrt(_AmpGoalPulse.z*_AmpGoalPulse.z + pow(age*_AmpGoalShape.z,2));
        width = max(0.1,width);
        float s = r-age*_AmpGoalPulse.w;
        float envelope = exp(-s*s/(2*width*width))*exp(-age*0.75)*smoothstep(0,0.12,age);
        float phase = s*6.283185/max(0.25,_AmpGoalShape.x)-age*_AmpGoalShape.y*6.283185+_AmpGoalShape.z*age*s*s/(width*width);
        displacement += _AmpGoalPulse.y*envelope*(n*cos(phase)+tangent*cos(phase-_AmpGoalTrain.z)*_AmpGoalShape.w);
    }
    }
    // Very small held motion is spatially varied; never a uniform radius pulse.
    float held = _AmpGoalOriginWS.w;
    displacement += n*held*(0.6*sin(angle*3+r*2-_AmpGoalTime*0.8)+0.4*sin(angle*7+r*3+_AmpGoalTime*0.47));
    // The aperture is owned by the shared grid mask; body motion cannot pull it away.
    float apertureMask=smoothstep(0,0.65,abs(delta.x));
    p.xz -= displacement*smoothstep(0,0.25,r)*apertureMask;
    return mul(unity_WorldToObject,float4(p,1)).xyz;
}

float AmpHash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
float AmpNoise(float2 p)
{
    float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
    return lerp(lerp(AmpHash(i),AmpHash(i+float2(1,0)),f.x),lerp(AmpHash(i+float2(0,1)),AmpHash(i+1),f.x),f.y);
}
float AmpPerimeterWave(float4 wave,float a) { return wave.x*sin(a*wave.y+_AmpGoalTime*wave.z*6.283185); }
float4 AmpSurfaceCorona(float3 pOS, float signedDistanceWS)
{
    if(_AmpGoalEnabled<0.5 || _AmpGoalCorona.y<=0) return 0;
    float3 pWS=mul(unity_ObjectToWorld,float4(pOS,1)).xyz;
    float2 delta=pWS.xz-_AmpGoalOriginWS.xz;
    float angle=atan2(delta.y,delta.x);
    float activity=saturate((AmpPerimeterWave(_AmpGoalWaveA,angle)+AmpPerimeterWave(_AmpGoalWaveB,angle)+AmpPerimeterWave(_AmpGoalWaveC,angle)+0.45)*1.2);
    activity=lerp(0.7,activity,_AmpGoalCorona.w);
    float hue=0.55+0.12*sin(angle*2.1+_AmpGoalTime*0.13);
    if(_AmpGoalPoles.x>0)
    {
        float pole=0;
        [loop] for(int i=0;i<(int)_AmpGoalPoles.x;i++)
        {
            float phase=_AmpGoalPoles.z+i*_AmpGoalPoles.y;
            float d=atan2(sin(angle-phase),cos(angle-phase));
            float active=exp(-d*d/max(0.005,_AmpGoalPoles.w*_AmpGoalPoles.w));
            active*=lerp(1,0.55+0.45*sin(_AmpGoalTime*1.5+i*3.141593),_AmpGoalMode.y);
            if(active>pole) { pole=active; hue=(i&1)==0?0.51:0.88; }
        }
        activity*=0.08+pole;
    }
    float extent=max(0.005,_AmpGoalCorona.x);
    float d=signedDistanceWS;
    float u=d/extent;
    float slowNoise=AmpNoise(float2(angle*17+_AmpGoalTime*0.09,u*1.8));
    float branchNoise=AmpNoise(float2(angle*5-_AmpGoalTime*0.035,_AmpGoalTime/max(0.2,_AmpGoalBranch.z)));
    float flare=smoothstep(lerp(0.75,0.5,saturate(_AmpGoalBranch.y)),0.94,branchNoise)*activity;
    float reach=extent*(0.25+activity)+_AmpGoalBranch.x*flare;
    float outer=exp(-pow(max(0,d)/max(0.003,reach),max(0.5,_AmpGoalStyle.y)));
    // Interior support is deliberately hairline; it cannot paint a fixed ring on the mass.
    float inner=exp(-pow(min(0,d)/max(0.002,extent*0.16),2));
    float grainScale=1/max(0.001,_AmpGoalStyle.x);
    float2 grainPosition=float2(angle*max(0.2,length(delta)),d)*grainScale;
    float grain=AmpNoise(grainPosition+float2(_AmpGoalTime*0.08,0));
    float filtered=lerp(grain,0.5,saturate(max(fwidth(grainPosition.x),fwidth(grainPosition.y))-0.5));
    // Superposed curved ridges, broken locally. No isolated round particle sprites.
    float bend=angle*53+u*7+sin(angle*19-u*3+_AmpGoalTime*0.32)*2.5;
    float filament=pow(saturate(1-abs(sin(bend))),14);
    filament+=0.55*pow(saturate(1-abs(sin(angle*89-u*11+slowNoise*7-_AmpGoalTime*0.19))),19);
    filament+=_AmpGoalBranch.w*flare*pow(saturate(1-abs(sin(angle*41+u*u*2-_AmpGoalTime*0.3))),16);
    float surfaceGrain=lerp(0.25+filtered*0.6,0.2+filament*0.8+filtered*0.22,_AmpGoalStyle.w);
    surfaceGrain=lerp(surfaceGrain,filtered*0.85,_AmpGoalMode.x);
    float edge=exp(-pow(u/0.17,2));
    float grainWorld=max(_AmpGoalStyle.x,0.7*length(fwidth(pWS.xz)));
    float micro=AmpNoise(float2(angle*max(0.2,length(delta)),d)/max(0.001,grainWorld));
    float cloudSupport=exp(-pow(max(0,d)/max(0.005,_AmpCloud.y*(0.3+activity)),1.6))*inner;
    float grainCoverage=smoothstep(min(0.999,1-_AmpCloud.z),1,micro)*step(0.001,_AmpCloud.z);
    // The cloud's base grain is part of shimmer too. At zero contrast (including
    // Disabled), retain smooth density/falloff without either noise texture.
    float smoothCoverage=0.5*saturate(_AmpCloud.z);
    float cloudCoverage=max(0,smoothCoverage+(grainCoverage-smoothCoverage)*_AmpCloudShimmer.y);
    float cloud=cloudSupport*cloudCoverage*_AmpCloud.x*(0.2+activity);
    float2 cloudUV=float2(angle*21,u*1.6)/max(0.2,_AmpCloudShimmer.x);
    float cloudTime=_AmpGoalTime*_AmpCloudShimmer.w;
    cloudUV.x+=sin(cloudUV.y*0.7+cloudTime*0.22)*_AmpCloudShimmer.z;
    float cloudPattern=AmpNoise(cloudUV-float2(0,cloudTime*0.17));
    cloud*=max(0,1+(cloudPattern-0.5)*_AmpCloudShimmer.y);
    float flareSupport=exp(-pow(max(0,d)/max(0.005,_AmpFlare.y*(0.25+activity)+_AmpGoalBranch.x*flare),2))*inner;
    float flareScale=max(0.2,_AmpFlareShimmer.x);
    float flareBend=(angle*53+u*7)/flareScale+sin((angle*19-u*3)/flareScale+_AmpGoalTime*0.32*_AmpFlareShimmer.w)*2.5*_AmpFlareShimmer.z;
    float flareThreads=1-smoothstep(0.08,0.28+min(0.3,fwidth(flareBend)),abs(sin(flareBend)));
    flareThreads=max(0,0.35+(flareThreads-0.35)*_AmpFlareShimmer.y);
    float flareRoot=exp(-pow(max(0,d)/0.025,2))*inner;
    float flareLayer=(flareSupport*flareThreads*1.5+flareRoot*0.9)*activity*_AmpFlare.x;
    float branchLayer=surfaceGrain*outer*inner*activity*flare*_AmpGoalMode.w;
    float wispScale=max(0.2,_AmpWispShimmer.x),wispTime=_AmpGoalTime*_AmpWispShimmer.w;
    float smoke=AmpNoise(float2(angle*21/wispScale+sin(u*0.7/wispScale+wispTime*0.22)*_AmpWispShimmer.z,u*1.6/wispScale-wispTime*0.17));
    float atmosphere=exp(-pow(max(0,d)/max(0.01,_AmpFlare.w*(0.3+activity)),2))*inner;
    atmosphere*=max(0,0.3+(smoothstep(0.45,0.85,smoke)-0.3)*_AmpWispShimmer.y)*activity*_AmpFlare.z*1.2;
    float alpha=saturate((cloud+flareLayer+branchLayer+atmosphere)*_AmpGoalCorona.y*_AmpGoalCorona.z);
    // Chromatic offset varies locally with phase, not evenly stacked contour bands.
    hue+=_AmpGoalStyle.z*(0.07*sin(bend*0.21)+0.05*u);
    float palette=frac(angle/6.283185+_AmpGoalTime*0.025+u*0.09*_AmpGoalStyle.z)*3;
    float3 rgb=palette<1?lerp(_AmpPaletteA.rgb,_AmpPaletteB.rgb,palette):palette<2?lerp(_AmpPaletteB.rgb,_AmpPaletteC.rgb,palette-1):lerp(_AmpPaletteC.rgb,_AmpPaletteA.rgb,palette-2);
    return float4(rgb,alpha);
}
// Chladni-inspired angular nodes restricted to the actual silhouette. This is
// an authored edge mode, not a rigid-plate eigenmode solver or a mass-fill pass.
float4 AmpSurfaceSeam(float3 pOS,float distanceWS)
{
    if(_AmpGoalEnabled<0.5 || _AmpSeam.x<0.5 || _AmpSeam.y<=0) return 0;
    float3 pWS=mul(unity_ObjectToWorld,float4(pOS,1)).xyz;
    float2 delta=pWS.xz-_AmpGoalOriginWS.xz;
    float a=atan2(delta.y,delta.x), time=_AmpGoalTime;
    float phase=time*_AmpSeamModes.w;
    float modeA=cos(a*_AmpSeamModes.x+phase);
    float modeB=cos(a*(_AmpSeamModes.x+1)-phase*0.73);
    float mixGain=_AmpSeam.x>1.5?_AmpSeamModes.y*cos(time*_AmpSeamModes.z*6.283185):0;
    float node=(modeA+mixGain*modeB)/(1+abs(mixGain));
    float cluster=exp(-node*node/0.10);
    float width=max(0.005,_AmpSeam.z);
    float reach=width+_AmpSeam.w*cluster;
    // Hard finite support prevents any colored film across the body or field.
    float support=(1-smoothstep(reach*0.6,reach,max(0,distanceWS)))*exp(-pow(min(0,distanceWS)/(width*0.12),2));
    float u=distanceWS/width;
    // Fine-pattern drift is independent of the broader moving mode envelope.
    float shimmerTime=time*_AmpInterferenceShimmer.w;
    float shimmerPhase=shimmerTime*_AmpSeamModes.w;
    float shimmerScale=max(0.2,_AmpInterferenceShimmer.x);
    float shimmerMode=cos(a*(_AmpSeamModes.x+1)-shimmerPhase*0.73);
    float shimmerMix=_AmpSeam.x>1.5?_AmpSeamModes.y*cos(shimmerTime*_AmpSeamModes.z*6.283185):0;
    float bend=(u*9+a*37)/shimmerScale+(sin((a*13+u*2)/shimmerScale-shimmerPhase*2)*2.6+shimmerMode*shimmerMix*3)*_AmpInterferenceShimmer.z;
    float aa=max(0.055,min(0.7,fwidth(bend)));
    float3 split=float3(-1,0,1)*_AmpSeamSplit*1.8;
    float3 threads=1-smoothstep(0.03,0.03+aa,abs(sin(bend+split)));
    threads=saturate(0.35+(threads-0.35)*_AmpInterferenceShimmer.y);
    float breaks=0.25+0.75*AmpNoise(float2(a*71/shimmerScale+shimmerPhase,u*3/shimmerScale-shimmerPhase));
    breaks=saturate(0.625+(breaks-0.625)*_AmpInterferenceShimmer.y);
    // Spectral strands only: never add a white core, additive white flash or fill.
    float3 spectral=threads*float3(0.95,0.45,1);
    float peak=max(spectral.r,max(spectral.g,spectral.b));
    float alpha=saturate(peak*support*(0.10+0.90*cluster)*breaks*_AmpSeam.y);
    return float4(spectral/max(peak,0.001),alpha);
}
#endif
