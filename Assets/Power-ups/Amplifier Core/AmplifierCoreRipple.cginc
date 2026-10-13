#ifndef MASSIVE_CORE_RIPPLE_INCLUDED
#define MASSIVE_CORE_RIPPLE_INCLUDED
float4 _CoreRipple, _CoreRipple2;
float4 _CoreRippleOrigin, _CoreRippleOrigin2;

float CoreRippleWave(float3 direction, float4 pulse, float3 origin)
{
    if (pulse.y <= 0.00001) return 0;
    float angle = acos(clamp(dot(direction, normalize(origin)), -0.99999, 0.99999));
    float offset = angle - pulse.x * UNITY_PI;
    float envelope = exp(-offset * offset / max(0.01, pulse.z * pulse.z));
    float decay = sin(pulse.x * UNITY_PI) * (1.0 - pulse.x) * 1.6;
    return pulse.y * envelope * cos(offset * pulse.w * 2.0) * decay;
}

float CoreRippleHeight(float3 position)
{
    float3 direction = normalize(position);
    return CoreRippleWave(direction, _CoreRipple, _CoreRippleOrigin.xyz)
         + CoreRippleWave(direction, _CoreRipple2, _CoreRippleOrigin2.xyz);
}
#endif
