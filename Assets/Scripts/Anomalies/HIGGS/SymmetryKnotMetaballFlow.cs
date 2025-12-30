using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(250)]
[DisallowMultipleComponent]
public class SymmetryKnotMetaballFlow : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform sourceSlicesParent;
    [SerializeField] private string endRootName = "EndKnotRoot";

    [SerializeField] private MetaballSDFInstance sourceSDF; // direct target
    [SerializeField] private MetaballSDFInstance endSDF;    // direct target

    [Header("Ball Counts")]
    [Range(1, 32)] [SerializeField] private int sourceBallCount = 14;
    [Range(1, 32)] [SerializeField] private int endBallCount = 12;

    [Header("Motion")]
    [SerializeField] private float cyclesPerSecond = 1.35f;

    [Header("Sizing (Object Space)")]
    [Tooltip("Ball radius in OBJECT SPACE of the SDF cube (shader expects -0.5..0.5 unit cube).")]
    [SerializeField] private float baseRadiusOS = 0.12f;

    [SerializeField] private float endRadiusMul = 1.15f;

    [Header("Envelope")]
    [SerializeField] private float taperPow = 1.6f;
    [Range(0f, 0.45f)] [SerializeField] private float wrapFeather = 0.18f;

    [Header("Flow In/Out")]
    [Tooltip("Seconds to fade in metaball flow after the knot locks.")]
    [SerializeField] private float flowInSeconds = 0.25f;

    [Tooltip("Seconds to fade out metaball flow after the knot unlocks.")]
    [SerializeField] private float flowOutSeconds = 0.22f;

    [Header("Auto-fit SDF Volumes")]
    [SerializeField] private bool autoFitVolumes = true;
    [SerializeField] private float fitPaddingWorld = 0.75f;
    [SerializeField] private float fitMinSizeWorld = 1.0f;

    [Header("Debug")]
    [SerializeField] private bool logCountsEverySecond = false;

    private readonly List<Transform> _srcSlices = new();
    private readonly List<Transform> _endSlices = new();
    private Transform _endRoot;

    private bool _active;
    private float _flowMul; // 0..1
    private float _logTimer;

    private void Awake()
    {
        // Auto-find SDF instances if not assigned
        if (!sourceSDF) sourceSDF = GetComponentInChildren<MetaballSDFInstance>(true);
        // endSDF might not be found by the above if there are two; user can assign explicitly.
    }

    private void Update()
    {
        if (!sourceSDF || !endSDF)
            return;

        _endRoot = FindChildRecursive(transform, endRootName);

        // Collect slices
        _srcSlices.Clear();
        if (sourceSlicesParent != null)
            CollectDirectSlices(sourceSlicesParent, _srcSlices);
        if (_srcSlices.Count == 0)
            CollectSlicesRecursive(transform, _srcSlices);

        _endSlices.Clear();
        if (_endRoot != null)
            CollectDirectSlices(_endRoot, _endSlices);

        bool wantsActive = (_endRoot != null) && (_srcSlices.Count >= 2) && (_endSlices.Count >= 2);
        _active = wantsActive;

        // Tween flowMul
        float dt = Time.deltaTime;
        if (_active)
        {
            float k = (flowInSeconds <= 0.001f) ? 1f : dt / flowInSeconds;
            _flowMul = Mathf.Clamp01(_flowMul + k);
        }
        else
        {
            float k = (flowOutSeconds <= 0.001f) ? 1f : dt / flowOutSeconds;
            _flowMul = Mathf.Clamp01(_flowMul - k);
        }

        // Auto-fit volumes (keeps positions inside SDF cube bounds)
        if (autoFitVolumes)
        {
            FitVolumeToSlices(sourceSDF.transform, _srcSlices, fitPaddingWorld, fitMinSizeWorld);
            FitVolumeToSlices(endSDF.transform, _endSlices, fitPaddingWorld, fitMinSizeWorld);
        }

        // Write balls directly
        UpdateSourceCapDirect();
        UpdateEndCapDirect();

        if (logCountsEverySecond)
        {
            _logTimer += dt;
            if (_logTimer >= 1f)
            {
                _logTimer = 0f;
                Debug.Log($"[SymmetryKnotMetaballFlow] srcSlices={_srcSlices.Count} endSlices={_endSlices.Count} flowMul={_flowMul:0.00} " +
                          $"srcBallCount={sourceSDF.Count} endBallCount={endSDF.Count}", this);
            }
        }
    }

    private void UpdateSourceCapDirect()
    {
        sourceSDF.Clear();

        if (_flowMul <= 0.0001f || _srcSlices.Count < 2)
        {
            sourceSDF.Apply();
            return;
        }

        float time = Time.time;
        int n = Mathf.Clamp(sourceBallCount, 1, 32);

        for (int i = 0; i < n; i++)
        {
            float phase = i / (float)n;
            float s = Frac(time * cyclesPerSecond + phase); // 0..1 base->tip

            Vector3 posWS = SamplePolyline(_srcSlices, s);
            Vector3 posOS = sourceSDF.transform.InverseTransformPoint(posWS);

            // big -> 0 as it approaches tip
            float taper = Mathf.Pow(1f - s, taperPow);
            float wrap = WrapFade(s, wrapFeather);

            float r = baseRadiusOS * taper * wrap * _flowMul;

            if (r > 0f)
                sourceSDF.AddBall(posOS, r);
        }

        sourceSDF.Apply();
    }

    private void UpdateEndCapDirect()
    {
        endSDF.Clear();

        if (_flowMul <= 0.0001f || _endSlices.Count < 2)
        {
            endSDF.Apply();
            return;
        }

        float time = Time.time;
        int n = Mathf.Clamp(endBallCount, 1, 32);

        for (int i = 0; i < n; i++)
        {
            float phase = i / (float)n;
            float s = Frac(time * cyclesPerSecond + phase); // 0..1

            // end list is mouth->meet; we want meet->mouth
            float t = 1f - s;

            Vector3 posWS = SamplePolyline(_endSlices, t);
            Vector3 posOS = endSDF.transform.InverseTransformPoint(posWS);

            // reverse: 0 -> big as it approaches mouth
            float grow = Mathf.Pow(s, taperPow);
            float wrap = WrapFade(s, wrapFeather);

            float r = baseRadiusOS * endRadiusMul * grow * wrap * _flowMul;

            if (r > 0f)
                endSDF.AddBall(posOS, r);
        }

        endSDF.Apply();
    }

    // ------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------

    private static void FitVolumeToSlices(Transform volume, List<Transform> slices, float padWorld, float minSizeWorld)
    {
        if (volume == null || slices == null || slices.Count == 0) return;

        Vector3 min = slices[0].position;
        Vector3 max = slices[0].position;

        for (int i = 1; i < slices.Count; i++)
        {
            Vector3 p = slices[i].position;
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }

        Vector3 center = (min + max) * 0.5f;
        Vector3 size = (max - min);

        size += Vector3.one * padWorld;
        size.x = Mathf.Max(size.x, minSizeWorld);
        size.y = Mathf.Max(size.y, minSizeWorld);
        size.z = Mathf.Max(size.z, minSizeWorld);

        volume.position = center;
        volume.rotation = Quaternion.identity;
        volume.localScale = size;
    }

    private static void CollectDirectSlices(Transform parent, List<Transform> outList)
    {
        outList.Clear();
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (c.name.StartsWith("Slice_", StringComparison.Ordinal))
                outList.Add(c);
        }
        outList.Sort((a, b) => ParseSliceIndex(a.name).CompareTo(ParseSliceIndex(b.name)));
    }

    private static void CollectSlicesRecursive(Transform root, List<Transform> outList)
    {
        outList.Clear();
        var all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t != null && t.name.StartsWith("Slice_", StringComparison.Ordinal))
                outList.Add(t);
        }
        outList.Sort((a, b) => ParseSliceIndex(a.name).CompareTo(ParseSliceIndex(b.name)));
    }

    private static int ParseSliceIndex(string name)
    {
        int u = name.LastIndexOf('_');
        if (u < 0 || u + 1 >= name.Length) return 0;
        if (int.TryParse(name.Substring(u + 1), out int v)) return v;
        return 0;
    }

    private static Vector3 SamplePolyline(List<Transform> pts, float t01)
    {
        if (pts.Count == 0) return Vector3.zero;
        if (pts.Count == 1) return pts[0].position;

        t01 = Mathf.Clamp01(t01);

        float total = 0f;
        for (int i = 0; i < pts.Count - 1; i++)
            total += Vector3.Distance(pts[i].position, pts[i + 1].position);

        if (total <= 1e-5f) return pts[0].position;

        float target = total * t01;
        float acc = 0f;

        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector3 a = pts[i].position;
            Vector3 b = pts[i + 1].position;
            float seg = Vector3.Distance(a, b);

            if (acc + seg >= target)
            {
                float u = (target - acc) / Mathf.Max(seg, 1e-5f);
                return Vector3.LerpUnclamped(a, b, u);
            }

            acc += seg;
        }

        return pts[pts.Count - 1].position;
    }

    private static float WrapFade(float s, float feather)
    {
        if (feather <= 0f) return 1f;
        float a = Mathf.SmoothStep(0f, feather, s);
        float b = Mathf.SmoothStep(0f, feather, 1f - s);
        return a * b;
    }

    private static float Frac(float x) => x - Mathf.Floor(x);

    private static Transform FindChildRecursive(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;

        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindChildRecursive(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }
}
 