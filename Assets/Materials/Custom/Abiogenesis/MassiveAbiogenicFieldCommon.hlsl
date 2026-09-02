#ifndef MASSIVE_ABIOGENIC_FIELD_COMMON_INCLUDED
#define MASSIVE_ABIOGENIC_FIELD_COMMON_INCLUDED

struct MassiveAbiogenicRaymarchResult
{
    float hit;
    float t;
    float steps01;
    float minDistance;
    float3 hitPosField;
    float3 cellPos;
    float sphereRadius;
};

float2 MassiveRotate2D(float2 p, float angle)
{
    float s = sin(angle);
    float c = cos(angle);
    return float2(c * p.x - s * p.y, s * p.x + c * p.y);
}

float MassiveSdSphere(float3 p, float radius)
{
    return length(p) - radius;
}

// frac(), unlike HLSL fmod(), gives the same useful wrapped behavior for
// negative coordinates as GLSL/TSL mod() in the source inspiration.
float MassiveCenteredRepeat(float value, float period)
{
    period = max(period, 1e-4);
    return frac(value / period) * period - period * 0.5;
}

float3 MassiveWarpAbiogenicSample(
    float3 fieldPos,
    float marchT,
    float timeSeconds,
    float swirlSpeed,
    float bendAmplitude,
    float warpStrength)
{
    float3 q = fieldPos;

    // Twist progressively with distance down the ray. This bends the repeated
    // columns into a tunnel instead of moving a flat UV pattern.
    q.xy = MassiveRotate2D(q.xy, marchT * swirlSpeed);

    q.y += (sin(marchT + timeSeconds * 0.25) * 0.5 + 1.0) * bendAmplitude;
    q.x += sin(q.z * 2.7 + timeSeconds * 0.55) * warpStrength * 0.35;
    q.y += cos(q.x * 1.7 - timeSeconds * 0.35) * warpStrength * 0.15;

    return q;
}

float3 MassiveAbiogenicCellPosition(
    float3 sampleField,
    float marchT,
    float timeSeconds,
    float cellScale,
    float layerDepth,
    float scrollSpeed,
    float swirlSpeed,
    float bendAmplitude,
    float warpStrength)
{
    float3 q = MassiveWarpAbiogenicSample(
        sampleField,
        marchT,
        timeSeconds,
        swirlSpeed,
        bendAmplitude,
        warpStrength);

    // Positive scroll moves the repeated layers toward the virtual camera,
    // matching the source sketch's q.z += time * speed convention.
    q.z += timeSeconds * scrollSpeed;

    q.xy = frac(q.xy * max(cellScale, 1e-4)) - 0.5;
    q.z = MassiveCenteredRepeat(q.z, layerDepth);

    return q;
}

float MassiveAbiogenicSphereRadius(
    float sampleZ,
    float sphereRadius,
    float sphereRadiusAmplitude,
    float sphereRadiusFrequency)
{
    float radius = sphereRadius *
        (1.0 + sphereRadiusAmplitude * sin(sampleZ * sphereRadiusFrequency));

    return max(radius, 0.01);
}

float MassiveAbiogenicFieldSdf(
    float3 sampleField,
    float marchT,
    float timeSeconds,
    float cellScale,
    float layerDepth,
    float sphereRadius,
    float sphereRadiusAmplitude,
    float sphereRadiusFrequency,
    float scrollSpeed,
    float swirlSpeed,
    float bendAmplitude,
    float warpStrength,
    out float3 cellPos,
    out float radius)
{
    cellPos = MassiveAbiogenicCellPosition(
        sampleField,
        marchT,
        timeSeconds,
        cellScale,
        layerDepth,
        scrollSpeed,
        swirlSpeed,
        bendAmplitude,
        warpStrength);

    radius = MassiveAbiogenicSphereRadius(
        sampleField.z,
        sphereRadius,
        sphereRadiusAmplitude,
        sphereRadiusFrequency);

    return MassiveSdSphere(cellPos, radius);
}

MassiveAbiogenicRaymarchResult MassiveRaymarchAbiogenicField(
    float3 rayOriginField,
    float3 rayDirField,
    float timeSeconds,
    int maxSteps,
    float maxDistance,
    float hitEpsilon,
    float stepScale,
    float cellScale,
    float layerDepth,
    float sphereRadius,
    float sphereRadiusAmplitude,
    float sphereRadiusFrequency,
    float scrollSpeed,
    float swirlSpeed,
    float bendAmplitude,
    float warpStrength)
{
    MassiveAbiogenicRaymarchResult result;
    result.hit = 0.0;
    result.t = 0.0;
    result.steps01 = 0.0;
    result.minDistance = 1e20;
    result.hitPosField = rayOriginField;
    result.cellPos = float3(0.0, 0.0, 1.0);
    result.sphereRadius = sphereRadius;

    float t = 0.0;
    stepScale = saturate(stepScale);

    [loop]
    for (int i = 0; i < 256; i++)
    {
        if (i >= maxSteps)
            break;

        float3 sampleField = rayOriginField + rayDirField * t;
        float3 cellPos;
        float radius;

        float d = MassiveAbiogenicFieldSdf(
            sampleField,
            t,
            timeSeconds,
            cellScale,
            layerDepth,
            sphereRadius,
            sphereRadiusAmplitude,
            sphereRadiusFrequency,
            scrollSpeed,
            swirlSpeed,
            bendAmplitude,
            warpStrength,
            cellPos,
            radius);

        result.minDistance = min(result.minDistance, abs(d));
        result.hitPosField = sampleField;
        result.cellPos = cellPos;
        result.sphereRadius = radius;
        result.t = t;
        result.steps01 = (i + 1.0) / max((float)maxSteps, 1.0);

        if (d < hitEpsilon)
        {
            result.hit = 1.0;
            return result;
        }

        // The XY repeat scales space anisotropically, so this is not a perfect
        // mathematical SDF. A small safety factor prevents skipped near beads.
        t += max(d * stepScale, hitEpsilon * 0.5);

        if (t > maxDistance)
        {
            result.t = maxDistance;
            return result;
        }
    }

    return result;
}

float3 MassiveShadeAbiogenicField(
    MassiveAbiogenicRaymarchResult rm,
    float3 rayDirField,
    float3 backgroundColor,
    float3 fieldColor,
    float timeSeconds,
    float maxDistance,
    float baseLuma,
    float glowStrength,
    float stripeFrequency,
    float stripeContrast,
    float fogDensity,
    float surfaceSoftness,
    float normalShading,
    float nearBoost)
{
    float fog = exp(-rm.t * max(fogDensity, 1e-4));

    // Depth bands retain some of the source sketch's palette-by-distance feel,
    // but remain monochrome for MASSIVE.
    float bands = 0.5 + 0.5 * sin(rm.t * stripeFrequency - timeSeconds * 0.7);
    bands = lerp(1.0, bands, saturate(stripeContrast));

    float surfaceGlow = exp(-rm.minDistance / max(surfaceSoftness, 1e-4));
    float marchGlow = 1.0 - rm.steps01;

    // Cheap local-sphere normal. It is deliberately approximate, but gives each
    // bead a solid form cue without six more SDF samples per pixel.
    float3 normalField = normalize(rm.cellPos + float3(0.0, 0.0, 1e-5));
    float3 lightDirField = normalize(float3(-0.45, 0.60, -0.70));
    float diffuse = 0.22 + 0.78 * saturate(dot(normalField, lightDirField) * 0.5 + 0.5);

    float facing = saturate(dot(normalField, -rayDirField));
    float rim = pow(1.0 - facing, 2.0);
    float form = saturate(diffuse + rim * 0.18);
    form = lerp(1.0, form, saturate(normalShading));

    float depth01 = saturate(rm.t / max(maxDistance, 1e-4));
    float nearFactor = 1.0 + max(nearBoost, 0.0) * (1.0 - depth01) * (1.0 - depth01);

    float luma = baseLuma;
    luma += rm.hit * glowStrength * fog * bands * form * nearFactor;
    luma += surfaceGlow * 0.12 * fog * marchGlow;
    luma = saturate(luma);

    return lerp(backgroundColor, fieldColor, luma);
}

#endif
