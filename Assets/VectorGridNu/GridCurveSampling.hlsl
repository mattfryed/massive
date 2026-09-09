#ifndef MASSIVE_GRID_CURVE_SAMPLING_INCLUDED
#define MASSIVE_GRID_CURVE_SAMPLING_INCLUDED

// Shared by grid lines, border strips and the GPU validation probe. Smooth
// only displacement: tension must never bunch up the undeformed lattice.
StructuredBuffer<float3> _Pos;
int _SimGridX, _SimGridY;
float2 _GridSize;
int _CurveInterpolation; // 0 = legacy bilinear, 1 = smooth Hermite
float _CurveTension;
int _CurveOvershootProtection;

float3 FlatPositionFromUV(float2 uv)
{
    return float3((uv - 0.5) * _GridSize, 0.0);
}

float3 GridNodeDisplacement(int2 node)
{
    int2 last = int2(_SimGridX - 1, _SimGridY - 1);
    node = clamp(node, int2(0, 0), last);
    return _Pos[node.x + node.y * _SimGridX] -
        FlatPositionFromUV((float2)node / max(float2(1, 1), (float2)last));
}

float GridLimitedSlope(float left, float right)
{
    if (left * right <= 0.0) return 0.0;
    float a = abs(left), b = abs(right);
    float sum = max(1e-12, a + b);
    float balance = 4.0 * (a / sum) * (b / sum);
    // Harmonic mean with a smooth gate near extrema. Shared node slopes,
    // not a clamp of the final position (which would reintroduce corners).
    return sign(left) * (2.0 * (a / sum) * b) * balance;
}

float3 GridCurveSlope(float3 left, float3 right)
{
    float3 slope = 0.5 * (left + right);
    if (_CurveOvershootProtection != 0)
        slope = float3(GridLimitedSlope(left.x, right.x),
                       GridLimitedSlope(left.y, right.y),
                       GridLimitedSlope(left.z, right.z));
    return slope * (1.0 - saturate(_CurveTension));
}

float3 GridCubic(float3 p0, float3 p1, float3 p2, float3 p3, float t)
{
    float3 m1 = GridCurveSlope(p1 - p0, p2 - p1);
    float3 m2 = GridCurveSlope(p2 - p1, p3 - p2);
    float t2 = t * t, t3 = t2 * t;
    return (2.0 * t3 - 3.0 * t2 + 1.0) * p1 +
           (t3 - 2.0 * t2 + t) * m1 +
           (-2.0 * t3 + 3.0 * t2) * p2 + (t3 - t2) * m2;
}

float3 SampleSimPosClamped(float2 uv)
{
    uv = saturate(uv);
    float2 cell = uv * float2(_SimGridX - 1, _SimGridY - 1);
    int2 baseNode = (int2)floor(cell);
    float2 t = cell - baseNode;

    if (_CurveInterpolation == 0)
    {
        int2 next = min(baseNode + 1, int2(_SimGridX - 1, _SimGridY - 1));
        float3 p00 = _Pos[baseNode.x + baseNode.y * _SimGridX];
        float3 p10 = _Pos[next.x + baseNode.y * _SimGridX];
        float3 p01 = _Pos[baseNode.x + next.y * _SimGridX];
        float3 p11 = _Pos[next.x + next.y * _SimGridX];
        return lerp(lerp(p00, p10, t.x), lerp(p01, p11, t.x), t.y);
    }

    float3 d[16];
    [unroll] for (int y = 0; y < 4; y++)
        [unroll] for (int x = 0; x < 4; x++)
            d[x + y * 4] = GridNodeDisplacement(baseNode + int2(x - 1, y - 1));

    float3 rows[4], columns[4];
    [unroll] for (int i = 0; i < 4; i++)
    {
        rows[i] = GridCubic(d[i*4], d[i*4+1], d[i*4+2], d[i*4+3], t.x);
        columns[i] = GridCubic(d[i], d[i+4], d[i+8], d[i+12], t.y);
    }
    // Limiting is nonlinear. Average both evaluation orders so the field
    // has no preferred X/Y orientation. Both orders stay within the four
    // cell-corner displacement ranges when protection is enabled.
    float3 xy = GridCubic(rows[0], rows[1], rows[2], rows[3], t.y);
    float3 yx = GridCubic(columns[0], columns[1], columns[2], columns[3], t.x);
    return FlatPositionFromUV(uv) + 0.5 * (xy + yx);
}

float3 SampleSimPosExtended(float2 uv)
{
    // Match the existing overscan: carry the nearest edge displacement onto
    // the flat extension. Pinned edges remain flat, including during zooms.
    float2 edgeUV = saturate(uv);
    return FlatPositionFromUV(uv) +
        (SampleSimPosClamped(edgeUV) - FlatPositionFromUV(edgeUV));
}

#endif
