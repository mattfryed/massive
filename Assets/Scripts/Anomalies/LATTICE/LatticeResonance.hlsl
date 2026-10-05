#ifndef MASSIVE_LATTICE_RESONANCE_INCLUDED
#define MASSIVE_LATTICE_RESONANCE_INCLUDED

int _LatticeResonanceCount;
float4 _LatticeResonanceSamples[128]; // local XY, world radius, converted strength
float4 _LatticeResonanceProfiles[128]; // outer feather, inner softening
float4 _LatticeResonanceMetric; // world units per local X/Y unit
float4 _LatticeResonanceResponse; // maximum world displacement, boundary feather

float LatticeResonanceSmooth(float value)
{
    value = saturate(value);
    return value * value * value * (value * (value * 6 - 15) + 10);
}

// SINGULARITY's analytical Resonance field, evaluated continuously along each
// strand rather than interpolating a force integrated at simulation nodes.
float2 LatticeResonanceOffset(float2 flat)
{
    if (_LatticeResonanceCount <= 0) return 0;
    float2 metric = max(_LatticeResonanceMetric.xy, float2(.0001, .0001));
    float2 toEdge = (_GridSize * .5 - abs(flat)) * metric;
    float edgeDistance = min(toEdge.x, toEdge.y);
    if (edgeDistance <= 0) return 0;
    float2 offset = 0;
    [loop] for (int i = 0; i < min(_LatticeResonanceCount, 128); i++)
    {
        float4 sample = _LatticeResonanceSamples[i];
        float2 toward = (sample.xy - flat) * metric;
        float distanceSquared = dot(toward, toward);
        if (sample.z <= 0 || sample.w <= 0 || distanceSquared >= sample.z * sample.z) continue;
        float distance = sqrt(distanceSquared), t = distance / max(.000001, sample.z);
        float feather = saturate(_LatticeResonanceProfiles[i].x), soften = saturate(_LatticeResonanceProfiles[i].y);
        float outer = feather <= 0 ? 1 : 1 - LatticeResonanceSmooth((t - (1 - feather)) / max(.000001, feather));
        float inner = soften <= 0 ? 1 : LatticeResonanceSmooth(t / max(.000001, soften));
        offset += toward / max(.000001, distance) * (sample.w * outer * inner);
    }
    float maximum = max(.01, _LatticeResonanceResponse.x);
    offset *= maximum / (maximum + length(offset));
    return offset * LatticeResonanceSmooth(edgeDistance / max(.05, _LatticeResonanceResponse.y)) / metric;
}
#endif
