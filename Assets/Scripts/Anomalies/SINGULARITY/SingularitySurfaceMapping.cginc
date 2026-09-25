#ifndef MASSIVE_SINGULARITY_SURFACE_MAPPING
#define MASSIVE_SINGULARITY_SURFACE_MAPPING
// Per-draw opt-in. Unmodified levels/materials take the original fast path.
float _SingularityEnabled;
float _SingularityBackBrightness;
sampler2D _SingularityLookup;
float4 _SingularityLookup_TexelSize;
// Front height, loop length, physics-chart plane Y, camera-side surface lift.
float4 _SingularityShape;
float4x4 _SingularityWorldToLocal, _SingularityLocalToWorld;
float4x4 _SingularitySourceToWorld, _SingularityViewProjection;
float4 _SingularityPortalCenter; // chart-local XYZ, enabled
float4 _SingularityPortalShape;  // chart XZ direction, uniform scale, axial stretch

float4 SingularitySample(float distance)
{
    float u = frac(distance / max(.0001, _SingularityShape.y)) + .5 * _SingularityLookup_TexelSize.x;
    return tex2Dlod(_SingularityLookup, float4(u, .5, 0, 0));
}
float3 SingularityMapWorld(float3 world, out float brightness)
{
    brightness = 1;
    if (_SingularityEnabled < .5) return world;
    float3 chart = mul(_SingularityWorldToLocal, float4(world, 1)).xyz;
    if (_SingularityPortalCenter.w > .5)
    {
        float2 offset = chart.xz - _SingularityPortalCenter.xz;
        offset.y = (frac(offset.y / max(.0001, _SingularityShape.y) + .5) - .5) * _SingularityShape.y;
        float2 direction = _SingularityPortalShape.xy;
        float2 along = direction * dot(offset, direction);
        float stretch = max(1, _SingularityPortalShape.w);
        offset = (along * stretch + (offset - along) * rsqrt(stretch)) * _SingularityPortalShape.z;
        chart.xz = _SingularityPortalCenter.xz + offset;
    }
    float s = chart.z + _SingularityShape.x * .5;
    float4 frame = SingularitySample(s);
    // RGB-only cue sampled at each vertex, so extended geometry and trails
    // crossing a bend fade locally instead of popping with their owner's center.
    brightness = lerp(1, saturate(_SingularityBackBrightness), saturate(frame.a));
    float4 before = SingularitySample(s - .01);
    float4 after = SingularitySample(s + .01);
    float2 tangent = after.gb - before.gb;
    float length2 = max(.00000001, dot(tangent, tangent));
    float3 normal = float3(0, tangent.y, -tangent.x) * rsqrt(length2);
    float3 foldedPosition = float3(chart.x * frame.r, frame.g, frame.b);
    foldedPosition += normal * (chart.y - _SingularityShape.z);
    // Nuggets must be visible on the camera-facing side of either face, not
    // hidden underneath their own opaque fill when the surface normal flips.
    foldedPosition.y += _SingularityShape.w;
    return mul(_SingularityLocalToWorld, float4(foldedPosition, 1)).xyz;
}
float3 SingularityMapWorld(float3 world)
{
    float brightness;
    return SingularityMapWorld(world, brightness);
}
float4 SingularityClipLocal(float3 local, float4x4 originalMVP, out float brightness)
{
    brightness = 1;
    if (_SingularityEnabled < .5) return mul(originalMVP, float4(local, 1));
    float3 world = mul(_SingularitySourceToWorld, float4(local, 1)).xyz;
    return mul(_SingularityViewProjection, float4(SingularityMapWorld(world, brightness), 1));
}
float4 SingularityClipLocal(float3 local, float4x4 originalMVP)
{
    float brightness;
    return SingularityClipLocal(local, originalMVP, brightness);
}
#endif
