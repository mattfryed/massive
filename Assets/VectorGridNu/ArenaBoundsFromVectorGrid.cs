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
    }

    public VectorGridGPU Grid => grid;
    public float InsetWorld => insetWorld;
    public bool IsValid => grid != null;
    public BoundsSnapshot Current { get; private set; }

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
            halfSizeLocalInset = GetHalfSizeLocalInset()
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
