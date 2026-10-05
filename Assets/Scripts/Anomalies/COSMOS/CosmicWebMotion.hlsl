#ifndef MASSIVE_COSMIC_WEB_MOTION
#define MASSIVE_COSMIC_WEB_MOTION
struct Particle { float4 initial; float4 filament; float4 cluster; float4 flow; float4 bend; };
float WebPhase(float t, float bias, float variation)
{
    return smoothstep(0, 1, pow(saturate(t), exp2(bias * variation)));
}
float3 WebPosition(Particle p, float age, float variation, out float formation, out float evacuation)
{
    formation = WebPhase(age * 2, p.flow.w, variation);
    evacuation = WebPhase(age * 2 - 1, p.bend.w, variation);
    float3 infall = lerp(p.initial.xyz, p.filament.xyz, formation) + p.flow.xyz * sin(3.14159265 * formation) * variation;
    return lerp(infall, p.cluster.xyz, evacuation) + p.bend.xyz * (4 * evacuation * (1 - evacuation));
}
float2 WebProject(float3 p, float4 oval, float2 worldScale, float padding, float condensation)
{
    float r = length(p.xy);
    float2 ab = float2(oval.z, oval.y) * worldScale;
    // Smooth compact projection: progressively smaller radial spacing toward the limb.
    float power = lerp(16, 6, saturate(condensation));
    float lens = r / pow(1 + pow(r, power), 1 / power);
    float2 direction = p.xy / max(.00001, r);
    float2 boundary = direction * ab;
    float2 normal = direction / ab;
    normal /= max(.00001, length(normal));
    float2 result = (boundary - normal * padding) * lens;
    // Fold the far sides into a compressed band before the team goal separators.
    float cutoff = max(.01, oval.x * worldScale.x - padding);
    float start = max(0, cutoff - lerp(1.4, 4.5, condensation));
    if (abs(result.x) > start)
    {
        float span = max(.01, ab.x - padding - start), room = max(.01, cutoff - start);
        float t = saturate((abs(result.x) - start) / span);
        result.x = sign(result.x) * (start + room * (1 - pow(1 - t, span / room)));
    }
    return result / worldScale;
}
#endif
