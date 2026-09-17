#ifndef MASSIVE_REPULSOR_GRID_PULSE_INCLUDED
#define MASSIVE_REPULSOR_GRID_PULSE_INCLUDED
int _RepulsorPulseCount;
float4 _RepulsorPulseOrigins[16]; // grid-local center, normalized age, world travel radius
float4 _RepulsorPulseShapes[16]; // world amplitude/width, curl, world starting radius
float4 _RepulsorGridMetric; // world units along the grid's local X/Y axes

// The same map is applied to the existing horizontal and vertical lines at
// their shared flat coordinates, so intersections remain connected.
float3 RepulsorDisplace(float3 position, float2 flat)
{
    float2 metric = max(_RepulsorGridMetric.xy, float2(0.0001, 0.0001));
    [loop] for (int i = 0; i < min(_RepulsorPulseCount, 16); i++)
    {
        float4 origin = _RepulsorPulseOrigins[i];
        float4 shape = _RepulsorPulseShapes[i];
        float age = saturate(origin.z);
        float2 delta = (flat - origin.xy) * metric;
        float radius = length(delta);
        float width = max(0.0001, min(shape.y, origin.w * 0.45));
        float front = lerp(shape.w, origin.w, age);
        float q = (radius - front) / width;
        if (abs(q) >= 1.0 || radius < 0.0001) continue;

        // A compact crest/trough travels a short distance and fades fully;
        // no simulation impulse or additional line survives the event.
        float packet = (1.0 - q * q);
        packet *= packet;
        float life = sin(age * 3.14159265);
        float outer = 1.0 - smoothstep(origin.w - width, origin.w, radius);
        float center = smoothstep(0.0, width * 0.5, radius);
        float2 toEdge = (_GridSize * 0.5 - abs(flat)) * metric;
        float edge = smoothstep(0.0, width, min(toEdge.x, toEdge.y));
        float2 normal = delta / radius;
        float2 tangent = float2(-normal.y, normal.x);
        float phase = q * 3.14159265;
        float2 displacement = (normal * cos(phase) + tangent * sin(phase) * shape.z)
            * (shape.x * packet * life * outer * center * edge);
        position.xy += displacement / metric;
    }
    return position;
}
#endif
