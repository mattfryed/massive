// Paired, face-local radial attraction. Keep in sync with EvaluateBlackHoleOffset.
// Dedicated uniforms avoid consuming actor slots or obeying Show Player Attraction.
float2 BlackHoleOffset(float2 logical)
{
    float radius = _BlackHoleGridField.z, pull = _BlackHoleGridField.w;
    if (radius <= 0 || pull <= 0) return 0;
    float height = _BlackHoleGridShape.x, rearStart = _BlackHoleGridShape.y;
    float rearScale = max(.0001, _BlackHoleGridShape.z);
    float s = frac(logical.y / max(.0001, _SurfaceShape.y)) * _SurfaceShape.y;
    bool rear = s >= rearStart && s <= rearStart + height * rearScale;
    if (s > height && (!rear || _BlackHoleGridShape.w < .5)) return 0;
    float2 facePoint = float2(logical.x, rear ? height * .5 - (s - rearStart) / rearScale : s - height * .5);
    float edge = min(_SurfaceShape.x * .5 - abs(facePoint.x), height * .5 - abs(facePoint.y));
    if (edge <= 0) return 0;
    float2 toward = _BlackHoleGridField.xy - facePoint;
    float t = length(toward) / radius;
    if (t >= 1) return 0;
    float feather = clamp(_BlackHoleGridProfile.x, .05, 1);
    float weight = pull * (1 - ResonanceSmooth((t - (1 - feather)) / feather));
    float2 offset = toward * (.85 * weight / (.85 + weight))
        * ResonanceSmooth(edge / max(.05, min(radius * .25, 1)));
    return float2(offset.x, rear ? -offset.y * rearScale : offset.y);
}
