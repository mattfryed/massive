using System;
using UnityEngine;



[ExecuteAlways]
public class ArenaBoundsFromVectorGrid : MonoBehaviour
{
    [SerializeField] private VectorGridGPU grid;

    [Header("Playable inset (optional)")]
    [Tooltip("Positive shrinks the playable rect inward (world units). Useful if you want the inner edge of the thick border to be the 'true' playable area.")]
    [Min(0f)] public float insetWorld = 0f;

// Broadcast (not shown in inspector)
public event Action<BoundsSnapshot> OnBoundsChanged;


    [Serializable]
    public struct BoundsSnapshot
    {
        public Vector3 centerWS;
        public Vector3 axisX_WS;   // grid local +X in world
        public Vector3 axisY_WS;   // grid local +Y in world (often maps to world +Z in top-down)
        public Vector3 normalWS;   // grid local +Z in world (often maps to world +Y in top-down)
        public Vector2 halfSizeLocal;      // before inset
        public Vector2 halfSizeLocalInset; // after inset
    }

    public BoundsSnapshot Current { get; private set; }

    void Reset()
    {
        if (!grid) grid = GetComponent<VectorGridGPU>();
    }

    void OnEnable() => RecomputeAndNotify(force: true);
    void Update()   => RecomputeAndNotify(force: false);
    void OnValidate() => RecomputeAndNotify(force: true);

    public Vector2 GetHalfSizeLocalInset()
    {
        if (!grid) return Vector2.zero;

        Vector2 half = grid.size * 0.5f;

        // Convert world inset to local inset per-axis (handles scaled grid object)
        Vector3 ls = transform.lossyScale;
        float sx = Mathf.Max(1e-6f, Mathf.Abs(ls.x));
        float sy = Mathf.Max(1e-6f, Mathf.Abs(ls.y));
        Vector2 insetLocal = new Vector2(insetWorld / sx, insetWorld / sy);

        return new Vector2(
            Mathf.Max(0f, half.x - insetLocal.x),
            Mathf.Max(0f, half.y - insetLocal.y)
        );
    }

    public bool ContainsWorldPoint(Vector3 worldPos, float extraPaddingWorld = 0f)
    {
        if (!grid) return false;

        // Convert to grid-local space (accounts for grid transform + scale)
        Vector3 pL = transform.InverseTransformPoint(worldPos);

        // Convert extra padding from world to local (per axis)
        Vector3 ls = transform.lossyScale;
        float sx = Mathf.Max(1e-6f, Mathf.Abs(ls.x));
        float sy = Mathf.Max(1e-6f, Mathf.Abs(ls.y));
        float padLX = extraPaddingWorld / sx;
        float padLY = extraPaddingWorld / sy;

        Vector2 halfInset = GetHalfSizeLocalInset();
        return (Mathf.Abs(pL.x) <= (halfInset.x - padLX)) && (Mathf.Abs(pL.y) <= (halfInset.y - padLY));
    }

    public Vector3 ClampWorldPointInside(Vector3 worldPos, float extraPaddingWorld = 0f)
    {
        if (!grid) return worldPos;

        Vector3 pL = transform.InverseTransformPoint(worldPos);

        Vector3 ls = transform.lossyScale;
        float sx = Mathf.Max(1e-6f, Mathf.Abs(ls.x));
        float sy = Mathf.Max(1e-6f, Mathf.Abs(ls.y));
        float padLX = extraPaddingWorld / sx;
        float padLY = extraPaddingWorld / sy;

        Vector2 halfInset = GetHalfSizeLocalInset();
        float hx = Mathf.Max(0f, halfInset.x - padLX);
        float hy = Mathf.Max(0f, halfInset.y - padLY);

        pL.x = Mathf.Clamp(pL.x, -hx, hx);
        pL.y = Mathf.Clamp(pL.y, -hy, hy);

        return transform.TransformPoint(pL);
    }

    void RecomputeAndNotify(bool force)
    {
        if (!grid) { Current = default; return; }

        var next = new BoundsSnapshot
        {
            centerWS = transform.TransformPoint(Vector3.zero),
            axisX_WS = transform.TransformVector(Vector3.right).normalized,
            axisY_WS = transform.TransformVector(Vector3.up).normalized,
            normalWS = transform.TransformVector(Vector3.forward).normalized,
            halfSizeLocal = grid.size * 0.5f,
            halfSizeLocalInset = GetHalfSizeLocalInset()
        };

        if (force || HasMeaningfulDiff(Current, next))
        {
            Current = next;
            OnBoundsChanged?.Invoke(Current);
        }
    }

    static bool HasMeaningfulDiff(BoundsSnapshot a, BoundsSnapshot b)
    {
        const float eps = 1e-4f;
        if ((a.centerWS - b.centerWS).sqrMagnitude > eps) return true;
        if ((a.axisX_WS - b.axisX_WS).sqrMagnitude > eps) return true;
        if ((a.axisY_WS - b.axisY_WS).sqrMagnitude > eps) return true;
        if ((a.normalWS - b.normalWS).sqrMagnitude > eps) return true;
        if ((a.halfSizeLocal - b.halfSizeLocal).sqrMagnitude > eps) return true;
        if ((a.halfSizeLocalInset - b.halfSizeLocalInset).sqrMagnitude > eps) return true;
        return false;
    }
}
