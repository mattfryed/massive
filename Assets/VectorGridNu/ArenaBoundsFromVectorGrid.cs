using System;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class ArenaBoundsFromVectorGrid : MonoBehaviour
{
    [SerializeField] private VectorGridGPU grid;

    [Header("Playable Inset (Optional)")]
    [Tooltip("Positive values shrink the queryable playable rectangle inward in world units. " +
             "The physical wall builder may still use the full VectorGridGPU.Size.")]
    [Min(0f)]
    public float insetWorld;

    [Header("Outline")]
    [Tooltip("Elliptical top/bottom edges, with goals completing the ellipse outside the field. Off preserves the standard rectangle.")]
    public bool ovalOutline;
    [Range(.3f, .95f)] public float ovalEndHeightRatio = .7f;
    public const int OvalSegments = 96;

    private struct OvalClearanceSegment
    {
        public Vector2 start, direction;
    }

    private sealed class OvalClearanceCache
    {
        public VectorGridGPU grid;
        public Vector2 halfSize;
        public float ratio, scaleX, scaleY, minimumY, interiorGuard;
        public readonly OvalClearanceSegment[] segments = new OvalClearanceSegment[OvalSegments];
    }

    [NonSerialized] private OvalClearanceCache ovalClearanceCache;

    // C# event: intentionally not serialized or shown in the Inspector.
    public event Action<BoundsSnapshot> OnBoundsChanged;

    [Serializable]
    public struct BoundsSnapshot
    {
        public Vector3 centerWS;
        public Vector3 axisX_WS;
        public Vector3 axisY_WS;
        public Vector3 normalWS;
        public Vector2 halfSizeLocal;
        public Vector2 halfSizeLocalInset;
        public bool ovalOutline;
        public float ovalEndHeightRatio;
    }

    public VectorGridGPU Grid => grid;
    public float InsetWorld => insetWorld;
    public bool IsValid => grid != null;
    public BoundsSnapshot Current { get; private set; }

    public float OutlineHalfHeightLocal(float x)
    {
        if (!grid) return 0;
        Vector2 half = grid.size * .5f;
        if (!ovalOutline) return half.y;
        float u = x / OvalHalfWidthLocal;
        return half.y * Mathf.Sqrt(Mathf.Max(0, 1 - u * u));
    }

    public float OvalGoalHalfHeightLocal => grid ? grid.size.y * .5f * Mathf.Clamp(ovalEndHeightRatio, .3f, .95f) : 0;
    public float OvalHalfWidthLocal => grid ? grid.size.x * .5f /
        Mathf.Sqrt(1 - Mathf.Pow(Mathf.Clamp(ovalEndHeightRatio, .3f, .95f), 2)) : 0;
    public float OvalGoalDepthLocal => grid ? OvalHalfWidthLocal - grid.size.x * .5f : 0;

    public Vector3 GridRestPosition(Vector2 uv)
    {
        if (!grid) return Vector3.zero;
        float x = (uv.x - .5f) * grid.size.x;
        return new Vector3(x, (uv.y - .5f) * 2 * OutlineHalfHeightLocal(x), 0);
    }

    public Vector4 OutlineShaderParameters => grid && ovalOutline
        ? new Vector4(grid.size.x * .5f, grid.size.y * .5f, OvalHalfWidthLocal, 1f)
        : Vector4.zero;

    bool ContainsOvalLocal(Vector3 p, float clearance)
    {
        Vector2 half = grid.size * .5f;
        Vector3 scale = GridTransform.lossyScale;
        float sx = Mathf.Abs(scale.x), sy = Mathf.Abs(scale.y);
        if (Mathf.Abs(p.x) > half.x || Mathf.Abs(p.y) > OutlineHalfHeightLocal(p.x) ||
            (half.x - Mathf.Abs(p.x)) * sx < clearance) return false;
        if (clearance <= 0) return true;
        // Match the tessellated physical wall, measuring clearance in world units.
        Vector2 point = new Vector2(p.x * sx, Mathf.Abs(p.y) * sy);
        OvalClearanceCache cache = GetOvalClearanceCache(half, sx, sy);
        // Every curved segment lies above this band. Stay away from equality so
        // rounding at the boundary still uses the original closest-point test.
        if (point.y + clearance + cache.interiorGuard < cache.minimumY) return true;
        for (int i = 0; i < cache.segments.Length; i++)
        {
            OvalClearanceSegment segment = cache.segments[i];
            // Retain the original inline arithmetic at clearance equality;
            // precomputed scalar intermediates can change its float rounding.
            float t = Mathf.Clamp01(Vector2.Dot(point - segment.start, segment.direction) / Mathf.Max(.000001f, segment.direction.sqrMagnitude));
            if ((point - (segment.start + segment.direction * t)).sqrMagnitude < clearance * clearance) return false;
        }
        return true;
    }

    private OvalClearanceCache GetOvalClearanceCache(Vector2 half, float sx, float sy)
    {
        float ratio = Mathf.Clamp(ovalEndHeightRatio, .3f, .95f);
        OvalClearanceCache cache = ovalClearanceCache;
        // Query-time, exact component checks also catch live edits smaller than
        // the bounds-notification epsilon and scale changes with zero inset.
        if (cache != null && cache.grid == grid &&
            cache.halfSize.x == half.x && cache.halfSize.y == half.y &&
            cache.ratio == ratio && cache.scaleX == sx && cache.scaleY == sy)
            return cache;

        if (cache == null) cache = ovalClearanceCache = new OvalClearanceCache();
        cache.grid = grid;
        cache.halfSize = half;
        cache.ratio = ratio;
        cache.scaleX = sx;
        cache.scaleY = sy;
        Vector2 a = new Vector2(-half.x * sx, OutlineHalfHeightLocal(-half.x) * sy);
        cache.minimumY = a.y;
        for (int i = 1; i <= OvalSegments; i++)
        {
            float x = Mathf.Lerp(-half.x, half.x, (float)i / OvalSegments);
            Vector2 b = new Vector2(x * sx, OutlineHalfHeightLocal(x) * sy);
            Vector2 ab = b - a;
            cache.segments[i - 1] = new OvalClearanceSegment
            {
                start = a, direction = ab
            };
            cache.minimumY = Mathf.Min(cache.minimumY, b.y);
            a = b;
        }
        cache.interiorGuard = .00001f * Mathf.Max(1f, Mathf.Max(Mathf.Abs(half.x * sx), Mathf.Abs(half.y * sy)));
        return cache;
    }

    private Transform GridTransform =>
        grid != null ? grid.transform : transform;

    private void Reset()
    {
        AutoAssignGrid();
    }

    private void OnEnable()
    {
        AutoAssignGrid();
        RecomputeAndNotify(force: true);
    }

    private void OnValidate()
    {
        insetWorld = Mathf.Max(0f, insetWorld);
        AutoAssignGrid();
        RecomputeAndNotify(force: true);
    }

    private void Update()
    {
        RecomputeAndNotify(force: false);
    }

    public void RefreshNow(bool forceNotification = true)
    {
        AutoAssignGrid();
        RecomputeAndNotify(forceNotification);
    }

    public Vector2 GetHalfSizeLocalInset()
    {
        if (grid == null)
            return Vector2.zero;

        Vector2 half = grid.size * 0.5f;
        Vector2 insetLocal = WorldDistanceToGridLocalAxes(insetWorld);

        return new Vector2(
            Mathf.Max(0f, half.x - insetLocal.x),
            Mathf.Max(0f, half.y - insetLocal.y));
    }

    public bool ContainsWorldPoint(
        Vector3 worldPosition,
        float extraPaddingWorld = 0f)
    {
        if (grid == null)
            return false;

        Transform gridTransform = GridTransform;
        Vector3 localPosition =
            gridTransform.InverseTransformPoint(worldPosition);

        if (ovalOutline)
            return ContainsOvalLocal(localPosition, insetWorld + Mathf.Max(0, extraPaddingWorld));

        Vector2 paddingLocal = WorldDistanceToGridLocalAxes(
            Mathf.Max(0f, extraPaddingWorld));

        Vector2 half = GetHalfSizeLocalInset();
        float halfX = Mathf.Max(0f, half.x - paddingLocal.x);
        float halfY = Mathf.Max(0f, half.y - paddingLocal.y);

        return Mathf.Abs(localPosition.x) <= halfX &&
               Mathf.Abs(localPosition.y) <= halfY;
    }

    public Vector3 ClampWorldPointInside(
        Vector3 worldPosition,
        float extraPaddingWorld = 0f)
    {
        if (grid == null)
            return worldPosition;

        Transform gridTransform = GridTransform;
        Vector3 localPosition =
            gridTransform.InverseTransformPoint(worldPosition);

        if (ovalOutline)
        {
            float clearance = insetWorld + Mathf.Max(0, extraPaddingWorld);
            if (ContainsOvalLocal(localPosition, clearance)) return worldPosition;
            // The convex outline is star-shaped about the centre. Bisect only invalid queries.
            float low = 0, high = 1;
            for (int i = 0; i < 24; i++)
            {
                float mid = (low + high) * .5f;
                if (ContainsOvalLocal(new Vector3(localPosition.x * mid, localPosition.y * mid, localPosition.z), clearance)) low = mid;
                else high = mid;
            }
            localPosition.x *= low; localPosition.y *= low;
            return gridTransform.TransformPoint(localPosition);
        }

        Vector2 paddingLocal = WorldDistanceToGridLocalAxes(
            Mathf.Max(0f, extraPaddingWorld));

        Vector2 half = GetHalfSizeLocalInset();
        float halfX = Mathf.Max(0f, half.x - paddingLocal.x);
        float halfY = Mathf.Max(0f, half.y - paddingLocal.y);

        localPosition.x = Mathf.Clamp(localPosition.x, -halfX, halfX);
        localPosition.y = Mathf.Clamp(localPosition.y, -halfY, halfY);

        return gridTransform.TransformPoint(localPosition);
    }

    private Vector2 WorldDistanceToGridLocalAxes(float worldDistance)
    {
        Vector3 scale = GridTransform.lossyScale;
        float scaleX = Mathf.Max(0.000001f, Mathf.Abs(scale.x));
        float scaleY = Mathf.Max(0.000001f, Mathf.Abs(scale.y));

        return new Vector2(
            worldDistance / scaleX,
            worldDistance / scaleY);
    }

    private void RecomputeAndNotify(bool force)
    {
        if (grid == null)
        {
            BoundsSnapshot empty = default;

            if (force || HasMeaningfulDifference(Current, empty))
            {
                Current = empty;
                OnBoundsChanged?.Invoke(Current);
            }

            return;
        }

        Transform gridTransform = GridTransform;

        BoundsSnapshot next = new BoundsSnapshot
        {
            centerWS = gridTransform.TransformPoint(Vector3.zero),
            axisX_WS = gridTransform.TransformDirection(Vector3.right).normalized,
            axisY_WS = gridTransform.TransformDirection(Vector3.up).normalized,
            normalWS = gridTransform.TransformDirection(Vector3.forward).normalized,
            halfSizeLocal = grid.size * 0.5f,
            halfSizeLocalInset = GetHalfSizeLocalInset(),
            ovalOutline = ovalOutline,
            ovalEndHeightRatio = ovalEndHeightRatio
        };

        if (!force && !HasMeaningfulDifference(Current, next))
            return;

        Current = next;
        OnBoundsChanged?.Invoke(Current);
    }

    private void AutoAssignGrid()
    {
        if (grid == null)
            grid = GetComponent<VectorGridGPU>();
    }

    private static bool HasMeaningfulDifference(
        BoundsSnapshot a,
        BoundsSnapshot b)
    {
        const float epsilon = 0.0001f;
        const float epsilonSquared = epsilon * epsilon;

        if (a.ovalOutline != b.ovalOutline || !Mathf.Approximately(a.ovalEndHeightRatio, b.ovalEndHeightRatio))
            return true;

        if ((a.centerWS - b.centerWS).sqrMagnitude > epsilonSquared)
            return true;

        if ((a.axisX_WS - b.axisX_WS).sqrMagnitude > epsilonSquared)
            return true;

        if ((a.axisY_WS - b.axisY_WS).sqrMagnitude > epsilonSquared)
            return true;

        if ((a.normalWS - b.normalWS).sqrMagnitude > epsilonSquared)
            return true;

        if ((a.halfSizeLocal - b.halfSizeLocal).sqrMagnitude > epsilonSquared)
            return true;

        if ((a.halfSizeLocalInset - b.halfSizeLocalInset).sqrMagnitude >
            epsilonSquared)
            return true;

        return false;
    }
}
