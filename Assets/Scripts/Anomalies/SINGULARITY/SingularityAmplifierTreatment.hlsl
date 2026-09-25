#ifndef MASSIVE_SINGULARITY_AMPLIFIER_INCLUDED
#define MASSIVE_SINGULARITY_AMPLIFIER_INCLUDED
// Existing AmplifierGoalTreatments owns all settings, capture ages and held levels.
// This projection adapts its packet/discharge field to a periodic curved surface.
float4 _AmpOrigins[2], _AmpStates[2];
float4 _AmpPacket, _AmpPhase, _AmpTiming, _AmpField;
float _AmpHeldTint;

float SingularityAmpDistance(float value)
{
    float length = max(.0001, _SurfaceShape.y);
    return (frac(value / length + .5) - .5) * length;
}
float2 SingularityAmpPair(float y, float age, float amplitude, float side)
{
    if(age < 0 || age > 8 || amplitude == 0) return 0;
    float w = max(.1, _AmpPacket.y), width = sqrt(w*w + pow(_AmpPacket.w*age, 2));
    float gain = sqrt(w/width) * smoothstep(0, .12, age) * exp(-age*.65);
    float2 offset = 0;
    [unroll] for(int d=-1; d<=1; d+=2)
    {
        float s = SingularityAmpDistance(y*d - _AmpPacket.z*age);
        float wrapFade = 1-smoothstep(.35, .5, abs(s)/max(.0001, _SurfaceShape.y));
        float envelope = exp(-s*s/(2*width*width)) * gain * wrapFade;
        float phase = s*6.283185/max(.25, _AmpPacket.x) - age*_AmpPhase.x*6.283185
            + _AmpPacket.w*age*s*s/(width*width);
        offset += amplitude*envelope*float2(cos(phase)*side, cos(phase-_AmpPhase.z)*_AmpPhase.y*d);
    }
    return offset;
}
float2 SingularityAmpDischarge(float2 delta, float age, float amplitude)
{
    float loopLength = max(.0001, _SurfaceShape.y);
    float width = max(.2, _AmpField.y), reach = _AmpField.x*age;
    float support = max(0, reach)+4*width;
    int first = max(-24, (int)ceil((-support-delta.y)/loopLength));
    int last = min(24, (int)floor((support-delta.y)/loopLength));
    float2 result = 0;
    // Periodic images retain both approaching wavefronts at the rear antipode.
    // Compact support makes the finite sum continuous, not a clipped repeat.
    [loop] for(int image=first; image<=last; image++)
    {
        float2 d = float2(delta.x, delta.y+image*loopLength);
        float r = length(d), travel = r-reach;
        float sigma = abs(travel)/width;
        if(sigma>=4) continue;
        float window = 1-smoothstep(3,4,sigma);
        float env = exp(-travel*travel/(2*width*width))*exp(-age*.65)*smoothstep(0,.15,age)*window;
        float phase = travel*6.283185/max(.3,_AmpField.z)-age*2;
        float2 n = d/max(.001,r), t = float2(-n.y,n.x);
        result += amplitude*env*(n*cos(phase)+t*sin(phase)*_AmpField.w);
    }
    return result;
}
float2 SingularityAmplifierOffset(float2 logical)
{
    if(_AmpTiming.w < .5) return 0;
    float metric = max(.0001, SurfaceFrame(logical.y).r);
    float2 result = 0;
    [unroll] for(int team=0; team<2; team++)
    {
        float4 origin = _AmpOrigins[team], state = _AmpStates[team];
        float side = team==0 ? 1 : -1;
        float y = SingularityAmpDistance(logical.y-origin.y);
        float distance = (logical.x-origin.x)*metric;
        float2 offset = 0;
        [unroll] for(int i=0; i<5; i++)
            if(i<(int)_AmpTiming.x) offset += SingularityAmpPair(y, origin.z-i*_AmpTiming.y, state.x, side);
        float modes = max(1, floor(_SurfaceShape.y/max(.25, _AmpPacket.x)+.5));
        offset.x += state.y*origin.w*sin(y*6.283185*modes/max(.0001, _SurfaceShape.y)-_AmpPhase.w*_AmpPhase.x*6.283185);
        float period = max(.5, _AmpTiming.z);
        [unroll] for(int j=0; j<3; j++)
            offset += SingularityAmpPair(y, fmod(_AmpPhase.w, period)+j*period, state.z*origin.w, side);
        result += float2(offset.x/metric, offset.y)*exp(-distance*distance/(2*.65*.65));
        if(state.w>0 && origin.z>=0 && origin.z<8)
        {
            float2 shift = SingularityAmpDischarge(float2(distance,y), origin.z, state.w);
            result += float2(shift.x/metric, shift.y);
        }
    }
    return result;
}
float4 SingularityAmplifierColor(float2 logical, float4 baseColor)
{
    if(_AmpTiming.w < .5) return baseColor;
    float metric = max(.0001, SurfaceFrame(logical.y).r);
    [unroll] for(int team=0; team<2; team++)
    {
        float4 origin = _AmpOrigins[team];
        float y = SingularityAmpDistance(logical.y-origin.y);
        float edge = 1-smoothstep(.005,.03,abs(logical.x-origin.x)*metric);
        float strength = _AmpHeldTint*origin.w;
        if(origin.z>=0 && origin.z<8)
        {
            float width = max(.1,_AmpPacket.y)+_AmpPacket.w*origin.z;
            float s = SingularityAmpDistance(abs(y)-_AmpPacket.z*origin.z);
            strength += _AmpStates[team].x*3*exp(-s*s/(2*width*width))*exp(-origin.z*.65);
        }
        float hue = .55+.13*sin(y*6.283185*3/max(.0001,_SurfaceShape.y)-_AmpPhase.w*.4);
        float3 rgb = saturate(abs(frac(hue+float3(0,.666667,.333333))*6-3)-1);
        baseColor.rgb = lerp(baseColor.rgb,lerp(1,rgb,.72),saturate(strength)*edge);
    }
    return baseColor;
}
#endif
