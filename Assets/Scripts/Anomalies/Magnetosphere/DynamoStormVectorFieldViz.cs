using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class DynamoStormVectorFieldViz : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private DynamoStormController stormController;
    [SerializeField] private bool autoFindController = true;
    [SerializeField] private Material lineMaterial;

    [Header("Sorting (optional)")]
    [SerializeField] private string sortingLayerName = "";
    [SerializeField] private int sortingOrder = 0;

    [Header("Box Layout (local XZ plane)")]
    public Vector2 boxSize = new Vector2(8f, 4f);      // (widthX, heightZ)
    public Vector2 padding = new Vector2(0.35f, 0.35f); // (padX, padZ)
    [Min(1)] public int columns = 22;
    [Min(1)] public int rows = 12;

    [Header("Arrow Shape")]
    [Min(0.0001f)] public float thickness = 0.03f;
    [Min(0.0001f)] public float maxArrowLength = 0.45f;
    [Min(0f)] public float calmDotLengthMul = 1.15f; // dot length = thickness * mul
    [Min(0.0001f)] public float headLength = 0.14f;
    [Range(5f, 80f)] public float headAngleDeg = 28f;

    [Header("Easing")]
    [Min(0f)] public float intensitySmooth = 12f;
    [Min(0f)] public float directionSmooth = 14f;

    [Header("Jitter")]
    [Min(0f)] public float jitterAngleDeg = 10f; // per-arrow deviation (degrees)
    [Min(0f)] public float jitterSpeed = 1.4f;
    [Min(0.01f)] public float jitterSpatialScale = 0.85f;
    [Range(0f, 1f)] public float lengthJitter01 = 0.10f;
    [Range(0f, 1f)] public float alphaJitter01 = 0.15f;

    [Header("Style")]
    public Color coreColor = new Color(1f, 1f, 1f, 1f);
    [Range(0f, 1f)] public float calmAlpha = 0.15f;
    [Range(0f, 1f)] public float stormAlpha = 1.0f;
    [Range(0.001f, 1f)] public float edgeSoftness = 0.35f;

    [Header("Glow")]
    public bool glow = true;
    public Color glowColor = new Color(1f, 1f, 1f, 0.25f);
    [Min(0.0001f)] public float glowThickness = 0.09f;
    [Min(0f)] public float glowMultiplier = 1.0f;
    [Range(0.001f, 1f)] public float glowEdgeSoftness = 0.70f;

    // --------------------------------------------------------------------

    private float _displayIntensity01;
    private Vector2 _displayDirXZ = Vector2.right;

    private LineMesh _core;
    private LineMesh _glow;

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
            Debug.LogWarning($"[{name}] DynamoStormVectorFieldViz has no lineMaterial assigned.", this);

        EnsureLineMesh(ref _glow, "Glow");
        EnsureLineMesh(ref _core, "Core");
    }

    private void Update()
    {
        if (autoFindController && stormController == null)
            stormController = FindObjectOfType<DynamoStormController>();

        if (lineMaterial == null)
            return;

        float targetIntensity = (stormController != null) ? Mathf.Clamp01(stormController.StormStrength01) : 0f;
        _displayIntensity01 = SmoothExp(_displayIntensity01, targetIntensity, intensitySmooth, Time.deltaTime);

        // Direction (world -> local), projected into local XZ plane.
        Vector2 targetDir = _displayDirXZ;
        if (stormController != null)
        {
            Vector3 wdir = stormController.GetStormFlowDirWS();
            Vector3 ldir = transform.InverseTransformDirection(wdir);
            targetDir = new Vector2(ldir.x, ldir.z);
        }

        if (targetDir.sqrMagnitude > 1e-6f)
        {
            targetDir.Normalize();
            _displayDirXZ = SmoothDir(_displayDirXZ, targetDir, directionSmooth, Time.deltaTime);
        }

        // Build core + glow
        SetVisible(_core, true);
        BuildVectorFieldMesh(_core.mesh, thickness, calmAlpha, stormAlpha, edgeSoftness, coreColor);

        if (glow)
        {
            SetVisible(_glow, true);
            BuildVectorFieldMesh(_glow.mesh, glowThickness, calmAlpha, stormAlpha, glowEdgeSoftness, glowColor);
            SetLineMpb(_glow, glowColor, glowMultiplier, glowEdgeSoftness);
        }
        else
        {
            SetVisible(_glow, false);
        }

        SetLineMpb(_core, coreColor, 1f, edgeSoftness);
    }

    // --------------------------------------------------------------------
    // Mesh build
    // --------------------------------------------------------------------

    private readonly List<Vector3> _verts = new List<Vector3>(8192);
    private readonly List<Vector2> _uvs = new List<Vector2>(8192);
    private readonly List<Color32> _cols = new List<Color32>(8192);
    private readonly List<int> _tris = new List<int>(12288);

    private void BuildVectorFieldMesh(Mesh mesh, float segThickness, float calmA, float stormA, float edgeSoft, Color tint)
    {
        if (mesh == null)
            return;

        _verts.Clear();
        _uvs.Clear();
        _cols.Clear();
        _tris.Clear();

        int cx = Mathf.Max(1, columns);
        int rz = Mathf.Max(1, rows);

        float w = Mathf.Max(0.001f, boxSize.x);
        float h = Mathf.Max(0.001f, boxSize.y);

        float padX = Mathf.Clamp(padding.x, 0f, w * 0.49f);
        float padZ = Mathf.Clamp(padding.y, 0f, h * 0.49f);

        float x0 = -w * 0.5f + padX;
        float x1 =  w * 0.5f - padX;
        float z0 = -h * 0.5f + padZ;
        float z1 =  h * 0.5f - padZ;

        float dotLen = Mathf.Max(1e-4f, segThickness * Mathf.Max(0.1f, calmDotLengthMul));
        float baseLen = Mathf.Lerp(dotLen, Mathf.Max(dotLen, maxArrowLength), _displayIntensity01);

        float aBase = Mathf.Lerp(calmA, stormA, _displayIntensity01);

        float t = Time.time;

        for (int j = 0; j < rz; j++)
        {
            float vz = (rz == 1) ? 0.5f : (j / (float)(rz - 1));
            float czp = Mathf.Lerp(z0, z1, vz);

            for (int i = 0; i < cx; i++)
            {
                float ux = (cx == 1) ? 0.5f : (i / (float)(cx - 1));
                float cxp = Mathf.Lerp(x0, x1, ux);

                int idx = i + j * cx;
                float seed = idx * 0.371f;

                // Direction jitter (angle)
                float nAng = SignedPerlin(seed * jitterSpatialScale + 1.23f, t * jitterSpeed + 0.77f);
                float angRad = nAng * jitterAngleDeg * Mathf.Deg2Rad * _displayIntensity01;

                Vector2 dir = Rotate(_displayDirXZ, angRad);
                if (dir.sqrMagnitude < 1e-6f) dir = Vector2.right;
                dir.Normalize();

                Vector2 perp = new Vector2(-dir.y, dir.x);

                // Length + alpha jitter
                float nLen = SignedPerlin(seed * jitterSpatialScale + 19.7f, t * (jitterSpeed * 0.73f) + 4.1f);
                float lenMul = 1f + nLen * lengthJitter01 * _displayIntensity01;
                float L = Mathf.Max(dotLen, baseLen * lenMul);

                float aJ = 1f + nLen * alphaJitter01 * _displayIntensity01;
                float a = Mathf.Clamp01(aBase * aJ);

                // Collapse into “dot” state (still a tiny segment)
                bool drawHead = (_displayIntensity01 > 0.05f) && (L > headLength * 2.1f);

                Vector2 center = new Vector2(cxp, czp);
                Vector2 tail = center - dir * (L * 0.5f);
                Vector2 head = center + dir * (L * 0.5f);

                // Segment color (vertex alpha drives intensity)
                byte aByte = (byte)Mathf.RoundToInt(a * 255f);
                var vcol = new Color32(255, 255, 255, aByte);

                AddQuadSegment(tail, head, segThickness, vcol);

                if (drawHead)
                {
                    float hl = Mathf.Min(headLength, L * 0.45f);
                    float hw = hl * Mathf.Tan(headAngleDeg * Mathf.Deg2Rad);

                    Vector2 leftEnd  = head - dir * hl + perp * hw;
                    Vector2 rightEnd = head - dir * hl - perp * hw;

                    AddQuadSegment(head, leftEnd,  segThickness, vcol);
                    AddQuadSegment(head, rightEnd, segThickness, vcol);
                }
            }
        }

        mesh.Clear();
        mesh.SetVertices(_verts);
        mesh.SetUVs(0, _uvs);
        mesh.SetColors(_cols);
        mesh.SetTriangles(_tris, 0);
        mesh.RecalculateBounds();
    }

    private void AddQuadSegment(Vector2 p0, Vector2 p1, float thickness, Color32 col)
    {
        Vector2 d = p1 - p0;
        float len = d.magnitude;
        if (len < 1e-6f) return;
        d /= len;

        float half = thickness * 0.5f;
        Vector2 nrm = new Vector2(-d.y, d.x) * half;

        Vector2 a = p0 - nrm;
        Vector2 b = p0 + nrm;
        Vector2 c = p1 + nrm;
        Vector2 d2 = p1 - nrm;

        int baseIndex = _verts.Count;

        _verts.Add(new Vector3(a.x, 0f, a.y));
        _verts.Add(new Vector3(b.x, 0f, b.y));
        _verts.Add(new Vector3(c.x, 0f, c.y));
        _verts.Add(new Vector3(d2.x, 0f, d2.y));

        _uvs.Add(new Vector2(0f, -1f));
        _uvs.Add(new Vector2(0f,  1f));
        _uvs.Add(new Vector2(1f,  1f));
        _uvs.Add(new Vector2(1f, -1f));

        _cols.Add(col);
        _cols.Add(col);
        _cols.Add(col);
        _cols.Add(col);

        _tris.Add(baseIndex + 0);
        _tris.Add(baseIndex + 1);
        _tris.Add(baseIndex + 2);
        _tris.Add(baseIndex + 0);
        _tris.Add(baseIndex + 2);
        _tris.Add(baseIndex + 3);
    }

    // --------------------------------------------------------------------
    // Rendering helpers
    // --------------------------------------------------------------------

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

    // --------------------------------------------------------------------
    // Math helpers
    // --------------------------------------------------------------------

    private static float SignedPerlin(float x, float y) => Mathf.PerlinNoise(x, y) * 2f - 1f;

    private static Vector2 Rotate(Vector2 v, float radians)
    {
        float c = Mathf.Cos(radians);
        float s = Mathf.Sin(radians);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    private static float SmoothExp(float current, float target, float speed, float dt)
    {
        if (speed <= 0f) return target;
        float t = 1f - Mathf.Exp(-speed * dt);
        return Mathf.Lerp(current, target, t);
    }

    private static Vector2 SmoothDir(Vector2 current, Vector2 target, float speed, float dt)
    {
        if (current.sqrMagnitude < 1e-6f) current = target;
        float t = (speed <= 0f) ? 1f : (1f - Mathf.Exp(-speed * dt));
        Vector2 v = Vector2.Lerp(current, target, t);
        if (v.sqrMagnitude < 1e-6f) return target;
        return v.normalized;
    }
}
