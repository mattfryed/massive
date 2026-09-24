using UnityEngine;

/// <summary>Per-grid, bounded visual attraction state. This never applies Rigidbody
/// forces or writes to the spring simulation; the renderer composes its field afterward.</summary>
public sealed class GridResponsiveAttraction
{
    public const int Capacity = 16;
    public const float MaximumCombinedPull = .85f;
    private const float LogTwenty = 2.995732274f;
    private const float ReleaseThreshold = .00001f;
    private static readonly int CountId = Shader.PropertyToID("_ResponsiveAttractorCount");
    private static readonly int CentersId = Shader.PropertyToID("_ResponsiveAttractorCenters");
    private static readonly int MetricId = Shader.PropertyToID("_ResponsiveGridMetric");

    private struct Source
    {
        public bool occupied, seen;
        public int owner, module;
        public Vector2 center, targetCenter;
        public float radius, targetRadius, pull, targetPull, response, release;
    }

    private readonly Source[] sources = new Source[Capacity];
    private readonly Vector4[] packedCenters = new Vector4[Capacity];
    public int Count { get; private set; }

    /// <summary>Submit each active owner/module once per frame, before Advance.
    /// New sources start at their requested position with zero visible strength.</summary>
    public void Submit(int ownerId, int moduleIndex, Vector2 gridLocalCenter,
        float worldRadius, float pull, float responseSeconds, float releaseSeconds)
    {
        // A corrupt position must not teleport an existing source to the origin.
        // Treat it as an absent submission, letting the existing field release safely.
        if (!Finite(gridLocalCenter.x) || !Finite(gridLocalCenter.y)) return;
        int index = -1, empty = -1;
        for (int i = 0; i < Capacity; i++)
        {
            if (!sources[i].occupied) { if (empty < 0) empty = i; }
            else if (sources[i].owner == ownerId && sources[i].module == moduleIndex) { index = i; break; }
        }
        float safeRadius = Nonnegative(worldRadius, 1000000f);
        float safePull = safeRadius > 0f ? Nonnegative(pull, 1000f) : 0f;
        if (index < 0)
        {
            if (empty < 0 || safePull <= 0f) return;
            index = empty;
            sources[index] = new Source { occupied = true, owner = ownerId, module = moduleIndex,
                center = SafeCenter(gridLocalCenter), radius = Mathf.Max(.0001f, safeRadius) };
            Count++;
        }
        ref Source source = ref sources[index];
        source.seen = true;
        source.targetCenter = SafeCenter(gridLocalCenter);
        source.targetRadius = Mathf.Max(.0001f, safeRadius);
        source.targetPull = safePull;
        source.response = Nonnegative(responseSeconds, 1000000f);
        source.release = Nonnegative(releaseSeconds, 1000000f);
    }

    /// <summary>First-order, non-overshooting response: 95% of a change in the
    /// authored number of seconds. Zero/invalid delta freezes animation; submissions
    /// are consumed once even while paused so stale activity cannot leak into resume.</summary>
    public void Advance(float dt)
    {
        bool advance = Finite(dt) && dt > 0f;
        for (int i = 0; i < Capacity; i++)
        {
            ref Source source = ref sources[i];
            if (!source.occupied) continue;
            if (advance)
            {
                if (source.seen)
                {
                    float alpha = ResponseAlpha(dt, source.response);
                    source.center = Vector2.LerpUnclamped(source.center, source.targetCenter, alpha);
                    source.radius = Mathf.LerpUnclamped(source.radius, source.targetRadius, alpha);
                    source.pull = Mathf.LerpUnclamped(source.pull, source.targetPull, alpha);
                }
                else source.pull *= 1f - ResponseAlpha(dt, source.release);

                if ((!source.seen || source.targetPull <= 0f) && source.pull <= ReleaseThreshold)
                {
                    source = default;
                    Count--;
                    continue;
                }
            }
            source.seen = false;
        }
    }

    public void Clear()
    {
        System.Array.Clear(sources, 0, sources.Length);
        System.Array.Clear(packedCenters, 0, packedCenters.Length);
        Count = 0;
    }

    public void WriteProperties(MaterialPropertyBlock block, Vector2 worldMetric)
    {
        if (block == null) return;
        int count = 0;
        for (int i = 0; i < Capacity; i++)
        {
            if (!sources[i].occupied) continue;
            Source source = sources[i];
            packedCenters[count++] = new Vector4(source.center.x, source.center.y, source.radius, source.pull);
        }
        for (int i = count; i < Capacity; i++) packedCenters[i] = Vector4.zero;
        Vector2 metric = SafeMetric(worldMetric);
        block.SetInt(CountId, count);
        block.SetVectorArray(CentersId, packedCenters);
        block.SetVector(MetricId, new Vector4(metric.x, metric.y, 0f, 0f));
    }

    /// <summary>CPU reference for ResponsiveGridAttraction.hlsl. World radius and
    /// local centers are converted with the same grid-axis metric as the shader.</summary>
    public static Vector3 EvaluateDisplacement(Vector3 position, Vector2 flat, Vector2 gridSize,
        Vector2 worldMetric, Vector4[] centers, int count)
    {
        if (centers == null || !Finite(flat.x) || !Finite(flat.y)
            || !Finite(gridSize.x) || !Finite(gridSize.y)) return position;
        Vector2 metric = SafeMetric(worldMetric);
        Vector2 toEdge = Vector2.Scale(gridSize * .5f - new Vector2(Mathf.Abs(flat.x), Mathf.Abs(flat.y)), metric);
        float edgeDistance = Mathf.Min(toEdge.x, toEdge.y);
        if (edgeDistance <= 0f) return position;
        Vector2 sum = Vector2.zero;
        float totalPull = 0f;
        int boundedCount = Mathf.Clamp(count, 0, Mathf.Min(Capacity, centers.Length));
        for (int i = 0; i < boundedCount; i++)
        {
            Vector4 source = centers[i];
            if (!Finite(source.x) || !Finite(source.y) || !Finite(source.z) || !Finite(source.w)
                || source.z <= 0f || source.w <= 0f) continue;
            Vector2 toward = Vector2.Scale(new Vector2(source.x, source.y) - flat, metric);
            float radius = Mathf.Max(.0001f, source.z);
            float tSquared = toward.sqrMagnitude / (radius * radius);
            if (tSquared >= 1f) continue;
            float bell = 1f - tSquared; bell = bell * bell * bell;
            float edgeWidth = Mathf.Max(.0001f, Mathf.Min(radius * .25f, 1f));
            float weight = source.w * bell * SmootherStep(edgeDistance / edgeWidth);
            sum += toward * weight;
            totalPull += weight;
        }
        // Smooth rational normalization, not a hard clamp. The combined contraction
        // is strictly below .85 and has no threshold kink as fields overlap. A single
        // source is attenuated too: raw Pull is a strength dial, not an exact percentage.
        float scale = MaximumCombinedPull / (MaximumCombinedPull + totalPull);
        position.x += sum.x * scale / metric.x;
        position.y += sum.y * scale / metric.y;
        return position;
    }

    private static float ResponseAlpha(float dt, float seconds)
    { return seconds <= 0f ? 1f : 1f - Mathf.Exp(-LogTwenty * dt / seconds); }
    private static float SmootherStep(float t)
    { t = Mathf.Clamp01(t); return t * t * t * (t * (t * 6f - 15f) + 10f); }
    private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    private static float Nonnegative(float value, float maximum)
    { return Finite(value) ? Mathf.Clamp(value, 0f, maximum) : 0f; }
    private static Vector2 SafeCenter(Vector2 value)
    { return new Vector2(Mathf.Clamp(value.x, -1000000f, 1000000f), Mathf.Clamp(value.y, -1000000f, 1000000f)); }
    private static Vector2 SafeMetric(Vector2 value)
    {
        return new Vector2(Finite(value.x) ? Mathf.Clamp(Mathf.Abs(value.x), .0001f, 10000f) : 1f,
            Finite(value.y) ? Mathf.Clamp(Mathf.Abs(value.y), .0001f, 10000f) : 1f);
    }
}
