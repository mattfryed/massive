using UnityEngine;

[DisallowMultipleComponent]
public class DynamoStormWaveformViz : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private DynamoStormController stormController;
    [SerializeField] private bool autoFindController = true;
    [SerializeField] private Material lineMaterial;

    [Header("Sorting (optional)")]
    [SerializeField] private string sortingLayerName = "";
    [SerializeField] private int sortingOrder = 0;

    [Header("Layout (local XZ plane)")]
    [Min(0.01f)] public float width = 8f;
    [Min(0.001f)] public float maxHeight = 0.55f;
    [Min(2)] public int samples = 160;

    [Header("Intensity Mapping")]
    [Min(0f)] public float intensitySmooth = 10f;     // higher = snappier response
    [Min(0.01f)] public float amplitudeExponent = 1.0f;
    [Range(0f, 0.25f)] public float calmAmplitude01 = 0.03f; // "barely registers" when storm ~0

    [Header("Baseline")]
    public bool drawBaseline = true;
    public Color baselineColor = new Color(1f, 1f, 1f, 0.20f);
    [Min(0.0001f)] public float baselineThickness = 0.03f;
    [Range(0.001f, 1f)] public float baselineEdgeSoftness = 0.45f;

    [System.Serializable]
    public class WaveLayer
    {
        public bool enabled = true;

        [Header("Look")]
        public Color color = new Color(1f, 0f, 0.60f, 1f);
        [Min(0.0001f)] public float thickness = 0.045f;
        [Range(0.001f, 1f)] public float edgeSoftness = 0.35f;

        [Header("Glow")]
        public bool glow = true;
        public Color glowColor = new Color(1f, 0f, 0.60f, 0.25f);
        [Min(0.0001f)] public float glowThickness = 0.12f;
        [Min(0f)] public float glowMultiplier = 1.0f;
        [Range(0.001f, 1f)] public float glowEdgeSoftness = 0.70f;

        [Header("Shape")]
        [Min(0.01f)] public float noiseScale = 5.0f;
        [Min(0f)] public float noiseSpeed = 1.0f;
        [Range(0f, 2f)] public float amplitudeMul = 1.0f;

        [Header("Spikes (optional)")]
        [Range(0f, 1f)] public float spikeAmount = 0.0f;
        [Range(0.0f, 1.0f)] public float spikeThreshold = 0.85f;
        [Range(0.5f, 8f)] public float spikeSharpness = 3.0f;
    }

    [Header("Wave Layers")]
    public WaveLayer waveA = new WaveLayer();
    public WaveLayer waveB = new WaveLayer
    {
        enabled = false,
        color = new Color(1f, 0.55f, 0.15f, 1f),
        glowColor = new Color(1f, 0.55f, 0.15f, 0.25f),
        noiseScale = 2.6f,
        noiseSpeed = 0.55f,
        amplitudeMul = 1.35f,
        spikeAmount = 0.35f,
        spikeThreshold = 0.82f,
        spikeSharpness = 2.6f
    };

    // --------------------------------------------------------------------

    private float _displayIntensity01;

    private Vector2[] _ptsA;
    private Vector2[] _ptsB;
    private Vector2[] _ptsBase;

    private LineMesh _baseline;
    private LineMesh _aGlow, _aCore;
    private LineMesh _bGlow, _bCore;

    private void Awake()
    {
        EnsureSetup();
    }

    private void OnEnable()
    {
        EnsureSetup();
    }

    private void EnsureSetup()
    {
        if (autoFindController && stormController == null)
            stormController = FindObjectOfType<DynamoStormController>();

        if (lineMaterial == null)
            Debug.LogWarning($"[{name}] DynamoStormWaveformViz has no lineMaterial assigned.", this);

        EnsureLineMesh(ref _baseline, "Baseline");
        EnsureLineMesh(ref _aGlow, "WaveA_Glow");
        EnsureLineMesh(ref _aCore, "WaveA_Core");
        EnsureLineMesh(ref _bGlow, "WaveB_Glow");
        EnsureLineMesh(ref _bCore, "WaveB_Core");
    }

    private void Update()
    {
        if (autoFindController && stormController == null)
            stormController = FindObjectOfType<DynamoStormController>();

        if (lineMaterial == null)
            return;

        int n = Mathf.Max(2, samples);
        EnsurePointArrays(n);

        float target = (stormController != null) ? Mathf.Clamp01(stormController.StormStrength01) : 0f;
        _displayIntensity01 = SmoothExp(_displayIntensity01, target, intensitySmooth, Time.deltaTime);

        float amp01 = Mathf.Lerp(calmAmplitude01, 1f, Mathf.Pow(_displayIntensity01, Mathf.Max(0.01f, amplitudeExponent)));
        float halfW = width * 0.5f;
        float t = Time.time;

        // Baseline points
        for (int i = 0; i < n; i++)
        {
            float u = (n <= 1) ? 0f : (i / (float)(n - 1));
            float x = Mathf.Lerp(-halfW, halfW, u);
            _ptsBase[i] = new Vector2(x, 0f);
        }

        // Wave A
        if (waveA != null && waveA.enabled)
        {
            for (int i = 0; i < n; i++)
            {
                float u = (n <= 1) ? 0f : (i / (float)(n - 1));
                float x = Mathf.Lerp(-halfW, halfW, u);
                float z = EvalWave(waveA, u, t, amp01);
                _ptsA[i] = new Vector2(x, z);
            }
        }

        // Wave B
        if (waveB != null && waveB.enabled)
        {
            for (int i = 0; i < n; i++)
            {
                float u = (n <= 1) ? 0f : (i / (float)(n - 1));
                float x = Mathf.Lerp(-halfW, halfW, u);
                // Often looks better if the 2nd layer comes in more at high intensity:
                float ampB = Mathf.Pow(_displayIntensity01, 1.35f);
                float z = EvalWave(waveB, u, t, Mathf.Lerp(calmAmplitude01, 1f, ampB));
                _ptsB[i] = new Vector2(x, z);
            }
        }

        // Baseline draw
        SetVisible(_baseline, drawBaseline);
        if (drawBaseline)
        {
            BuildPolylineMesh(_baseline.mesh, _ptsBase, baselineThickness, new Color32(255, 255, 255, 255));
            SetLineMpb(_baseline, baselineColor, 1f, baselineEdgeSoftness);
        }

        // Wave A draw
        bool aOn = (waveA != null && waveA.enabled);
        SetVisible(_aCore, aOn);
        SetVisible(_aGlow, aOn && waveA.glow);

        if (aOn)
        {
            BuildPolylineMesh(_aCore.mesh, _ptsA, waveA.thickness, new Color32(255, 255, 255, 255));
            SetLineMpb(_aCore, waveA.color, 1f, waveA.edgeSoftness);

            if (waveA.glow)
            {
                BuildPolylineMesh(_aGlow.mesh, _ptsA, waveA.glowThickness, new Color32(255, 255, 255, 255));
                SetLineMpb(_aGlow, waveA.glowColor, waveA.glowMultiplier, waveA.glowEdgeSoftness);
            }
        }

        // Wave B draw
        bool bOn = (waveB != null && waveB.enabled);
        SetVisible(_bCore, bOn);
        SetVisible(_bGlow, bOn && waveB.glow);

        if (bOn)
        {
            BuildPolylineMesh(_bCore.mesh, _ptsB, waveB.thickness, new Color32(255, 255, 255, 255));
            SetLineMpb(_bCore, waveB.color, 1f, waveB.edgeSoftness);

            if (waveB.glow)
            {
                BuildPolylineMesh(_bGlow.mesh, _ptsB, waveB.glowThickness, new Color32(255, 255, 255, 255));
                SetLineMpb(_bGlow, waveB.glowColor, waveB.glowMultiplier, waveB.glowEdgeSoftness);
            }
        }
    }

    // --------------------------------------------------------------------
    // Wave shaping
    // --------------------------------------------------------------------

    private float EvalWave(WaveLayer w, float u, float t, float amp01)
    {
        float a = maxHeight * amp01 * Mathf.Max(0f, w.amplitudeMul);

        // Signed Perlin blend (two octaves)
        float n1 = SignedPerlin(u * w.noiseScale + 11.73f, t * w.noiseSpeed + 3.17f);
        float n2 = SignedPerlin(u * (w.noiseScale * 2.1f) + 61.9f, t * (w.noiseSpeed * 1.65f) + 9.1f);
        float n = (n1 * 0.65f + n2 * 0.35f);

        float z = n * a;

        // Optional “spike” shaping (adds rare tall peaks at high intensity)
        if (w.spikeAmount > 1e-4f)
        {
            float an = Mathf.Abs(n);
            float th = Mathf.Clamp01(w.spikeThreshold);
            if (an > th)
            {
                float s = (an - th) / Mathf.Max(1e-4f, (1f - th));
                s = Mathf.Pow(s, Mathf.Max(0.5f, w.spikeSharpness));
                z += Mathf.Sign(n) * s * a * w.spikeAmount;
            }
        }

        return z;
    }

    private static float SignedPerlin(float x, float y)
    {
        return Mathf.PerlinNoise(x, y) * 2f - 1f;
    }

    // --------------------------------------------------------------------
    // Mesh + rendering helpers
    // --------------------------------------------------------------------

    private void EnsurePointArrays(int n)
    {
        if (_ptsBase == null || _ptsBase.Length != n) _ptsBase = new Vector2[n];
        if (_ptsA == null || _ptsA.Length != n) _ptsA = new Vector2[n];
        if (_ptsB == null || _ptsB.Length != n) _ptsB = new Vector2[n];
    }

    private class LineMesh
    {
        public MeshFilter mf;
        public MeshRenderer mr;
        public Mesh mesh;
        public MaterialPropertyBlock mpb = new MaterialPropertyBlock();
    }

    private void EnsureLineMesh(ref LineMesh lm, string childName)
    {
        if (lm != null && lm.mf != null && lm.mr != null && lm.mesh != null)
        {
            if (lineMaterial != null) lm.mr.sharedMaterial = lineMaterial;
            ApplySorting(lm.mr);
            return;
        }

        Transform child = transform.Find(childName);
        if (child == null)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            child = go.transform;
        }

        if (lm == null) lm = new LineMesh();

        lm.mf = child.GetComponent<MeshFilter>();
        if (!lm.mf) lm.mf = child.gameObject.AddComponent<MeshFilter>();

        lm.mr = child.GetComponent<MeshRenderer>();
        if (!lm.mr) lm.mr = child.gameObject.AddComponent<MeshRenderer>();

        if (lineMaterial != null) lm.mr.sharedMaterial = lineMaterial;
        ApplySorting(lm.mr);

        if (lm.mf.sharedMesh == null)
        {
            lm.mesh = new Mesh { name = $"{childName}_Mesh" };
            lm.mesh.MarkDynamic();
            lm.mf.sharedMesh = lm.mesh;
        }
        else
        {
            lm.mesh = lm.mf.sharedMesh;
        }
    }

    private void ApplySorting(Renderer r)
    {
        if (!r) return;
        if (!string.IsNullOrEmpty(sortingLayerName)) r.sortingLayerName = sortingLayerName;
        r.sortingOrder = sortingOrder;
    }

    private void SetVisible(LineMesh lm, bool visible)
    {
        if (lm == null || lm.mr == null) return;
        lm.mr.enabled = visible;
    }

    private void SetLineMpb(LineMesh lm, Color tint, float glowMul, float edgeSoftness)
    {
        if (lm == null || lm.mr == null) return;

        lm.mpb.Clear();
        lm.mpb.SetColor("_Tint", tint);
        lm.mpb.SetFloat("_Glow", glowMul);
        lm.mpb.SetFloat("_EdgeSoftness", Mathf.Clamp(edgeSoftness, 0.001f, 1f));
        lm.mr.SetPropertyBlock(lm.mpb);
    }

    // points are Vector2(x,z) in local space; mesh is built in local XZ plane with y=0.
private static Vector2 Perp(Vector2 v) => new Vector2(-v.y, v.x);

private static Vector2 SafeNorm(Vector2 v, Vector2 fallback)
{
    float m2 = v.sqrMagnitude;
    if (m2 < 1e-10f) return fallback;
    return v / Mathf.Sqrt(m2);
}

// points are Vector2(x,z) in local space; mesh is built in local XZ plane with y=0.
private static void BuildPolylineMesh(
    Mesh mesh,
    Vector2[] pts,
    float thickness,
    Color32 vtxColor,
    float miterLimit = 2.0f // 1 = bevel-ish, 2..4 = typical
)
{
    if (mesh == null) return;
    int n = (pts == null) ? 0 : pts.Length;

    if (n < 2 || thickness <= 0f)
    {
        mesh.Clear();
        return;
    }

    // Two verts per point (left/right)
    int vCount = n * 2;
    int tCount = (n - 1) * 6;

    var verts = new Vector3[vCount];
    var uvs   = new Vector2[vCount];
    var cols  = new Color32[vCount];
    var tris  = new int[tCount];

    float half = thickness * 0.5f;
    float maxMiter = Mathf.Max(1f, miterLimit) * half;

    // Precompute segment directions + normals
    int segs = n - 1;
    var segDir = new Vector2[segs];
    var segNrm = new Vector2[segs];

    // Also compute cumulative distance for uv.x (not required by your shader, but useful)
    var cumLen = new float[n];
    cumLen[0] = 0f;
    float totalLen = 0f;

    for (int i = 0; i < segs; i++)
    {
        Vector2 d = pts[i + 1] - pts[i];
        float len = d.magnitude;
        if (len < 1e-6f)
        {
            d = Vector2.right;
            len = 0f;
        }
        else
        {
            d /= len;
        }

        segDir[i] = d;
        segNrm[i] = new Vector2(-d.y, d.x); // unit normal

        totalLen += Vector2.Distance(pts[i], pts[i + 1]);
        cumLen[i + 1] = totalLen;
    }

    float invTotal = (totalLen > 1e-6f) ? (1f / totalLen) : 0f;

    // Build left/right vertices at each point
    for (int i = 0; i < n; i++)
    {
        Vector2 p = pts[i];

        Vector2 miter = Vector2.up;
        float miterLen = half;

        if (i == 0)
        {
            // Start cap uses first segment normal
            miter = segNrm[0];
            miterLen = half;
        }
        else if (i == n - 1)
        {
            // End cap uses last segment normal
            miter = segNrm[segs - 1];
            miterLen = half;
        }
        else
        {
            // Miter join
            Vector2 n0 = segNrm[i - 1];
            Vector2 n1 = segNrm[i];

            Vector2 sum = n0 + n1;

            if (sum.sqrMagnitude < 1e-6f)
            {
                // 180-degree-ish turn, fall back to next normal
                miter = n1;
                miterLen = half;
            }
            else
            {
                miter = sum.normalized;

                // scale factor so offset meets the segment edges
                float denom = Vector2.Dot(miter, n1);

                // Safety against extremely small denom
                if (Mathf.Abs(denom) < 1e-4f)
                    denom = (denom >= 0f) ? 1e-4f : -1e-4f;

                miterLen = half / denom;

                // Clamp extreme miters to avoid long spikes
                if (Mathf.Abs(miterLen) > maxMiter)
                    miterLen = Mathf.Sign(miterLen) * maxMiter;
            }
        }

        Vector2 off = miter * miterLen;

        int vi = i * 2;

        // left/right in XZ plane (Vector2 is x,z)
        verts[vi + 0] = new Vector3(p.x - off.x, 0f, p.y - off.y);
        verts[vi + 1] = new Vector3(p.x + off.x, 0f, p.y + off.y);

        float u = (invTotal > 0f) ? (cumLen[i] * invTotal) : ((n <= 1) ? 0f : (float)i / (n - 1));
        uvs[vi + 0] = new Vector2(u, -1f);
        uvs[vi + 1] = new Vector2(u,  1f);

        cols[vi + 0] = vtxColor;
        cols[vi + 1] = vtxColor;
    }

    // Triangles between consecutive point-pairs
    int ti = 0;
    for (int i = 0; i < n - 1; i++)
    {
        int i0 = i * 2;
        int i1 = (i + 1) * 2;

        tris[ti + 0] = i0 + 0;
        tris[ti + 1] = i0 + 1;
        tris[ti + 2] = i1 + 1;

        tris[ti + 3] = i0 + 0;
        tris[ti + 4] = i1 + 1;
        tris[ti + 5] = i1 + 0;

        ti += 6;
    }

    mesh.Clear();
    mesh.vertices = verts;
    mesh.uv = uvs;
    mesh.colors32 = cols;
    mesh.triangles = tris;
    mesh.RecalculateBounds();
}



    private static float SmoothExp(float current, float target, float speed, float dt)
    {
        if (speed <= 0f) return target;
        float t = 1f - Mathf.Exp(-speed * dt);
        return Mathf.Lerp(current, target, t);
    }
}
