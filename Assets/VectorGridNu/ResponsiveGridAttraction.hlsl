#ifndef MASSIVE_RESPONSIVE_GRID_ATTRACTION_INCLUDED
#define MASSIVE_RESPONSIVE_GRID_ATTRACTION_INCLUDED

// Presentation only. _GridSize comes from GridCurveSampling.hlsl; positions are
// grid-local XY while radius is in world units. Each grid supplies its own block.
int _ResponsiveAttractorCount;
float4 _ResponsiveAttractorCenters[16]; // local center XY, world radius, current pull
float4 _ResponsiveGridMetric; // world units per grid-local X/Y unit

float ResponsiveAttractionEdge(float t)
{
    t = saturate(t);
    return t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
}

float3 ResponsiveAttractionDisplace(float3 position, float2 flat)
{
    float2 metric = max(_ResponsiveGridMetric.xy, float2(.0001, .0001));
    float2 toEdge = (_GridSize * .5 - abs(flat)) * metric;
    if (_ArenaOval.w > .5)
        toEdge.y = (_ArenaOval.y * sqrt(max(0, 1 - flat.x * flat.x / (_ArenaOval.z * _ArenaOval.z))) - abs(flat.y)) * metric.y;
    float edgeDistance = min(toEdge.x, toEdge.y);
    if (edgeDistance <= 0.0) return position;
    float2 sum = 0.0;
    float totalPull = 0.0;
    [loop] for (int i = 0; i < min(_ResponsiveAttractorCount, 16); i++)
    {
        float4 source = _ResponsiveAttractorCenters[i];
        if (!(source.z > 0.0) || !(source.w > 0.0)) continue;
        float2 toward = (source.xy - flat) * metric;
        float radius = max(.0001, source.z);
        float tSquared = dot(toward, toward) / (radius * radius);
        if (tSquared >= 1.0) continue;

        // Compact bell with zero outer slope. The unnormalized center vector
        // naturally goes to zero at its center instead of flipping a unit force.
        float bell = 1.0 - tSquared;
        bell = bell * bell * bell;
        float edgeWidth = max(.0001, min(radius * .25, 1.0));
        float weight = source.w * bell * ResponsiveAttractionEdge(edgeDistance / edgeWidth);
        sum += toward * weight;
        totalPull += weight;
    }

    // Smooth and symmetric overlap cap. Contraction sum/(.85+sum)*.85 stays
    // below .85 without a clamp shoulder. No spring/velocity feedback or mass input.
    float scale = .85 / (.85 + totalPull);
    position.xy += sum * scale / metric;
    return position;
}
#endif
