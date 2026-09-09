#ifndef MASSIVE_AMPLIFIER_GRID_INCLUDED
#define MASSIVE_AMPLIFIER_GRID_INCLUDED
float4 _AmpOrigins[2], _AmpStates[2];
float4 _AmpPacket, _AmpPhase, _AmpTiming, _AmpField;
float _AmpHeldTint;

float2 AmpPair(float y, float age, float amplitude, float side)
{
    if(age < 0 || age > 8 || amplitude == 0) return 0;
    float w=max(0.1,_AmpPacket.y), width=sqrt(w*w+pow(_AmpPacket.w*age,2));
    float gain=sqrt(w/width)*smoothstep(0,0.12,age)*exp(-age*0.65);
    float2 offset=0;
    [unroll] for(int d=-1;d<=1;d+=2)
    {
        float s=y*d-_AmpPacket.z*age;
        float envelope=exp(-s*s/(2*width*width))*gain;
        float phase=s*6.283185/max(0.25,_AmpPacket.x)-age*_AmpPhase.x*6.283185+_AmpPacket.w*age*s*s/(width*width);
        offset+=amplitude*envelope*float2(cos(phase)*side,cos(phase-_AmpPhase.z)*_AmpPhase.y*d);
    }
    return offset;
}
// A single spatial map is evaluated at shared UVs by ALL intersecting lines.
// Topology is retained even when high curl settings create visual folds.
float3 AmpDisplace(float3 p, float2 flat)
{
    if(_AmpTiming.w<0.5) return p;
    [unroll] for(int team=0;team<2;team++)
    {
        float4 origin=_AmpOrigins[team], state=_AmpStates[team];
        float side=team==0?1:-1;
        float y=flat.y-origin.y, distance=flat.x-origin.x;
        float2 offset=0;
        [unroll] for(int i=0;i<5;i++)
            if(i<(int)_AmpTiming.x) offset+=AmpPair(y,origin.z-i*_AmpTiming.y,state.x,side);
        offset.x+=state.y*origin.w*sin(y*6.283185/max(0.25,_AmpPacket.x)-_AmpPhase.w*_AmpPhase.x*6.283185);
        float period=max(0.5,_AmpTiming.z);
        [unroll] for(int j=0;j<3;j++)
            offset+=AmpPair(y,fmod(_AmpPhase.w,period)+j*period,state.z*origin.w,side);
        float edge=smoothstep(0,0.5,_GridSize.y*0.5-abs(flat.y));
        p.xy+=offset*exp(-distance*distance/(2*0.65*0.65))*edge;
        if(state.w>0 && origin.z>=0 && origin.z<8)
        {
            float2 delta=flat-origin.xy;
            float r=length(delta), travel=r-_AmpField.x*origin.z;
            float width=max(0.2,_AmpField.y);
            float env=exp(-travel*travel/(2*width*width))*exp(-origin.z*0.65)*smoothstep(0,0.15,origin.z);
            float phase=travel*6.283185/max(0.3,_AmpField.z)-origin.z*2;
            float2 n=delta/max(0.001,r), t=float2(-n.y,n.x);
            // Tangential + radial quadrature produces the curling discharge.
            p.xy+=state.w*env*(n*cos(phase)+t*sin(phase)*_AmpField.w)*edge;
        }
    }
    return p;
}
float4 AmpColor(float2 flat, float4 baseColor)
{
    if(_AmpTiming.w<0.5) return baseColor;
    [unroll] for(int team=0;team<2;team++)
    {
        float4 origin=_AmpOrigins[team];
        float edge=1-smoothstep(0.005,0.03,abs(flat.x-origin.x));
        float age=origin.z;
        float strength=_AmpHeldTint*origin.w;
        if(age>=0 && age<8)
        {
            float width=max(0.1,_AmpPacket.y)+_AmpPacket.w*age;
            float s=abs(flat.y-origin.y)-_AmpPacket.z*age;
            strength+=_AmpStates[team].x*3*exp(-s*s/(2*width*width))*exp(-age*0.65);
        }
        float hue=0.55+0.13*sin(flat.y*0.8-_AmpPhase.w*0.4);
        float3 rgb=saturate(abs(frac(hue+float3(0,0.666667,0.333333))*6-3)-1);
        baseColor.rgb=lerp(baseColor.rgb,lerp(1,rgb,0.72),saturate(strength)*edge);
    }
    return baseColor;
}
#endif
