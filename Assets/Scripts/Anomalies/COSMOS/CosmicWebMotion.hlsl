#ifndef MASSIVE_COSMIC_WEB_MOTION
#define MASSIVE_COSMIC_WEB_MOTION
struct Particle { float4 initial; float4 filament; float4 cluster; float4 flow; float4 bend; float4 merger; float4 attractor; float4 vortex; };
float WebPhase(float t, float bias, float variation)
{
    return smoothstep(0, 1, pow(saturate(t), exp2(bias * variation)));
}
float3 WebFlowField(float3 p, float seconds)
{
    float3 q = p * float3(18, 6.5, 4);
    float t = seconds * .045;
    return .7 * float3(
        sin(q.y * .18 + q.z * .21 + t * .73) + .45 * cos(q.y * .43 - q.z * .29 - t * 1.13),
        sin(q.z * .24 + q.x * .13 - t * .61) + .45 * sin(q.x * .35 + q.z * .19 + t * .93),
        .5 * sin(q.x * .14 + q.y * .19 + t * .51)) / float3(18, 6.5, 4);
}
float WebTurbulenceHash(int3 cell)
{
    // Same integer lattice hash as CosmicWebTopology.Noise; no frame-wise reseeding.
    uint3 v = (uint3)cell * uint3(374761393u, 668265263u, 1442695041u);
    uint h = v.x + v.y + v.z;
    h = (h ^ (h >> 13)) * 1274126177u;
    return ((h ^ (h >> 16)) & 0xffffffu) / 8388607.5 - 1;
}
float WebTurbulenceNoise(float3 p)
{
    int3 cell = (int3)floor(p);
    float3 f = frac(p);
    float3 u = f * f * f * (f * (f * 6 - 15) + 10);
    return lerp(
        lerp(lerp(WebTurbulenceHash(cell), WebTurbulenceHash(cell + int3(1,0,0)), u.x),
             lerp(WebTurbulenceHash(cell + int3(0,1,0)), WebTurbulenceHash(cell + int3(1,1,0)), u.x), u.y),
        lerp(lerp(WebTurbulenceHash(cell + int3(0,0,1)), WebTurbulenceHash(cell + int3(1,0,1)), u.x),
             lerp(WebTurbulenceHash(cell + int3(0,1,1)), WebTurbulenceHash(cell + int3(1,1,1)), u.x), u.y), u.z);
}
float3 WebClusterTurbulence(Particle p, float3 position, float formation, float evacuation,
    float mergerBlend, float seconds, float strength)
{
    if (p.attractor.w <= 0) return position; // Preserve the residual filament population.
    const float3 modelSize = float3(18, 6.5, 4);
    float3 center = p.attractor.xyz + p.merger.xyz * (mergerBlend * evacuation);
    float3 local = (position - center) * modelSize;
    float proximity = 1 - smoothstep(.2, 1, length(local) / p.attractor.w);
    float influence = clamp(strength, 0, 2) * formation * formation * proximity;
    if (influence <= 0) return position;
    float encounter = 4 * mergerBlend * (1 - mergerBlend);
    // The stored axis now orients a local flow frame, not a shared circular orbit.
    float3 axis = p.vortex.xyz;
    float3 tangent = normalize(cross(axis, abs(axis.y) < .9 ? float3(0,1,0) : float3(1,0,0)));
    float3 bitangent = cross(axis, tangent);
    float3 q = float3(dot(local, tangent), dot(local, bitangent), dot(local, axis)) / p.attractor.w;
    float3 seed = axis * 17.3 + p.attractor.xyz * float3(11.7, 19.3, 23.1);
    float clock = seconds * p.vortex.w * .55;
    float grain = lerp(.34, .11, evacuation);
    float amplitude = influence * (.18 + .10 * encounter);
    // Coupled shears at different scales stretch and fold streams. Each pass samples
    // the displaced coordinates of the previous pass, breaking spherical symmetry.
    // Smooth lattice noise avoids regular sine loops and discontinuous random kicks.
    float a = WebTurbulenceNoise(float3(q.y / grain + clock * .27, q.z / grain - clock * .19, clock * .71) + seed);
    q.x += a * amplitude;
    float b = WebTurbulenceNoise(float3(q.z / grain * 1.6 - clock * .31, q.x / grain * 1.6 + clock * .23, -clock * .83) + seed.yzx + 17.3);
    q.y += b * amplitude * .81;
    float c = WebTurbulenceNoise(float3(q.x / grain * 2.1 + clock * .17, q.y / grain * 2.1 + clock * .29, clock * 1.13) + seed.zxy - 11.7);
    q.z += c * amplitude * .63;
    float attraction = min(.28, influence * (.09 + .05 * encounter + .025 * (a + b)));
    float3 stirred = (tangent * q.x + bitangent * q.y + axis * q.z) * p.attractor.w;
    return center + stirred * (1 - attraction) / modelSize;
}
float3 WebPosition(Particle p, float age, float variation, float motionTime, float driftStrength,
    float turbulenceStrength, out float formation, out float evacuation)
{
    formation = WebPhase(age / clamp(.5 + p.flow.w * variation * .035, .40, .62), p.flow.w, variation);
    // Overlapping local collapse windows have no common midpoint stop or final arrival.
    float start = clamp(.46 + p.bend.w * variation * .10, .30, .66);
    float t = max(0, age - start) / .6;
    evacuation = 1 - exp(-4 * t * t);
    float3 infall = lerp(p.initial.xyz, p.filament.xyz, formation) + p.flow.xyz * sin(3.14159265 * formation) * variation;
    float mergerTime = max(0, age - p.merger.w) / .4;
    float mergerBlend = 1 - exp(-3 * mergerTime * mergerTime);
    float3 target = p.cluster.xyz + p.merger.xyz * mergerBlend;
    float3 position = lerp(infall, target, evacuation) + p.bend.xyz * (4 * evacuation * (1 - evacuation));
    // Uniform toggle bypasses all local turbulence math; it does not stop the shared flow.
    [branch] if (turbulenceStrength > 0)
        position = WebClusterTurbulence(p, position, formation, evacuation, mergerBlend, motionTime, turbulenceStrength);
    return position + (WebFlowField(position, motionTime) - WebFlowField(position, 60)) * driftStrength;
}
// Existing baseline probes remain available independently of the optional visual effect.
float3 WebPosition(Particle p, float age, float variation, float motionTime, float driftStrength, out float formation, out float evacuation)
{
    return WebPosition(p, age, variation, motionTime, driftStrength, 0, formation, evacuation);
}
float2 WebProject(float3 p, float4 oval, float2 worldScale, float padding, float condensation, float transitionSmoothness)
{
    float r = length(p.xy);
    float2 ab = float2(oval.z, oval.y) * worldScale;
    // Smooth compact projection: progressively smaller radial spacing toward the limb.
    float power = lerp(16, 6, saturate(condensation));
    float lens = r / pow(1 + pow(r, power), 1 / power);
    // Shape the radial shoulder independently of the compact lens exponent. Higher
    // smoothness spreads the compression inward; lower values tighten it at the rim.
    // This map fixes 0 and 1, has unit slope at the centre, and a positive derivative
    // throughout [0,1], so shells cannot fold over one another or leave the oval.
    float shoulder = .9 * (1 - 2 * saturate(transitionSmoothness));
    lens += shoulder * lens * lens * lens * (1 - lens);
    float2 direction = p.xy / max(.00001, r);
    float2 boundary = direction * ab;
    float2 normal = direction / ab;
    normal /= max(.00001, length(normal));
    float2 result = (boundary - normal * padding) * lens;
    // The backdrop spans the complete ellipse, including the caps behind both goals.
    // oval.x is the playable separator and must not affect this visual projection.
    return result / worldScale;
}
float2 WebProject(float3 p, float4 oval, float2 worldScale, float padding, float condensation)
{
    return WebProject(p, oval, worldScale, padding, condensation, .5);
}
#endif
