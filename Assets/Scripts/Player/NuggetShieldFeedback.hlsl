#ifndef MASSIVE_NUGGET_SHIELD_FEEDBACK
#define MASSIVE_NUGGET_SHIELD_FEEDBACK
#include "BlobDeform.hlsl"

// Render-only displacement: the underlying mass simulation remains undisturbed.
float4 _ShieldFeedback; // stun amplitude, parry envelope, time, rim padding
float4 _ShieldBlob0;    // radius, outline half, idle wobble, deformation
float4 _ShieldBlob1;    // direction.xy, teardrop k1/k2
float4 _ShieldBlob2;    // hit impulse, noise phase, dot extent, parry vibration

float ShieldRim(float2 p)
{
    float angle = atan2(p.y, p.x);
    float radius = ExpectedRadiusTeardrop(_ShieldBlob0.x, _ShieldBlob0.z, _ShieldBlob0.w,
        _ShieldBlob1.xy, _ShieldBlob1.z, _ShieldBlob1.w, _ShieldBlob2.x, _ShieldBlob2.y, angle);
    return max(.01, radius - _ShieldBlob0.y - _ShieldBlob2.z - _ShieldFeedback.w * _ShieldFeedback.y - .005);
}

float3 ApplyNuggetShieldFeedback(float3 local, uint index)
{
    if (_ShieldFeedback.x <= .00001 && _ShieldFeedback.y <= .00001) return local;
    float phase = (index + 1) * 2.39996323;
    float2 p = local.xz;
    float len = length(p);
    float2 dir = len > .0001 ? p / len : float2(cos(phase), sin(phase));
    p = lerp(p, dir * ShieldRim(dir), _ShieldFeedback.y);
    float t = _ShieldFeedback.z * 36;
    float2 vibration = float2(sin(t * 1.13 + phase), cos(t * 1.37 + phase * 1.7));
    p += vibration * (_ShieldFeedback.x + _ShieldBlob2.w * _ShieldFeedback.y);
    float rim = ShieldRim(p);
    p *= min(1, rim / max(length(p), .00001));
    return float3(p.x, local.y, p.y);
}
#endif
