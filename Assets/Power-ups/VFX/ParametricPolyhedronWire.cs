using System;
using System.Collections.Generic;
using UnityEngine;
using Shapes;

[ExecuteAlways]
public class ParametricPolyhedronWire : ImmediateModeShapeDrawer
{
    public enum PolyFamily
    {
        Prism,
        Pyramid,
        Bipyramid,
        Antiprism,
        IcoSphere
    }

    [Header("Polyhedron")]
    public PolyFamily family = PolyFamily.Prism;

    [Range(3, 128)] public int sides = 6;

    [Tooltip("Overall size. For most families this is the ring radius. For IcoSphere this is sphere radius.")]
    [Min(0.001f)] public float radius = 1f;

    [Tooltip("Height along local Y. For IcoSphere this is ignored.")]
    [Min(0.001f)] public float height = 1f;

    [Tooltip("Rotates the base/top rings around Y.")]
    [Range(0f, 360f)] public float startAngleDegrees = 0f;

    [Tooltip("For Antiprism: top ring rotation offset. Default (180/sides) makes the classic antiprism.")]
    public bool antiprismUseClassicTwist = true;
    [Range(0f, 360f)] public float antiprismTwistDegrees = 0f;

    [Header("IcoSphere")]
    [Range(0, 5)] public int icoSubdivisions = 2;

    [Header("Line Style")]
    public LineGeometry lineGeometry = LineGeometry.Volumetric3D;     // thick 3D rods
    public ThicknessSpace thicknessSpace = ThicknessSpace.Meters;     // world units
    [Min(0.0001f)] public float thickness = 0.05f;
    public Color color = Color.white;

    [Header("Build Animation")]
    [Range(0f, 1f)] public float foldProgress = 1f; // 0 = flattened, 1 = full 3D
    [Range(0f, 1f)] public float drawProgress = 1f; // 0 = no edges, 1 = all edges
    public Vector3 foldAxis = Vector3.up;           // axis to "flatten" along

    [Header("Shatter")]
    [Range(0f, 1f)] public float shatterProgress = 0f;  // 0..1 separate
    [Range(0f, 1f)] public float collapseProgress = 0f; // 0..1 shrink/fade
    [Min(0f)] public float shatterDistance = 0.35f;
    [Min(0f)] public float shatterSpinDegrees = 220f;
    [Range(0f, 1f)] public float shatterRandomness = 0.35f; // 0 = purely radial, 1 = purely random



    struct Edge { public int a, b; public Edge(int A, int B) { a = A; b = B; } }

    // Cached geometry (so we don't rebuild heavy stuff every frame)
    readonly List<Vector3> _verts = new List<Vector3>(256);
    readonly List<Edge> _edges = new List<Edge>(512);
    int _lastHash;

    void OnValidate()
    {
        // Force rebuild when inspector changes
        _lastHash = 0;
    }

public override void DrawShapes(Camera cam)
{
    if (sides < 3) sides = 3;

    RebuildIfNeeded();

    using (Draw.Command(cam))
    {
        Draw.Matrix = transform.localToWorldMatrix;

        Draw.LineGeometry = lineGeometry;
        Draw.ThicknessSpace = thicknessSpace;

        float fp = Mathf.Clamp01(foldProgress);
        float dp = Mathf.Clamp01(drawProgress);

        // NEW: shatter params (assumes you added these fields)
        float sh = Mathf.Clamp01(shatterProgress);   // 0..1 separate
        float co = Mathf.Clamp01(collapseProgress);  // 0..1 collapse/fade
        float fade = 1f - co;

        int edgeCount = _edges.Count;
        if (edgeCount == 0 || dp <= 0f || fade <= 0f)
            return;

        // Fade thickness + alpha during collapse
        Draw.Thickness = Mathf.Max(0.0001f, thickness * fade);

        Color c = color;
        c.a *= fade;
        Draw.Color = c;

        Vector3 axis = (foldAxis.sqrMagnitude < 1e-6f) ? Vector3.up : foldAxis.normalized;

        Vector3 Fold(Vector3 v)
        {
            if (fp >= 0.9999f) return v;
            float d = Vector3.Dot(v, axis);
            Vector3 flat = v - axis * d;               // remove component along axis (flatten)
            return Vector3.LerpUnclamped(flat, v, fp); // fold back into 3D
        }

        // --- deterministic hashing (no per-frame Random) ---

        float HashSigned01(int i, int seed)
        {
            unchecked
            {
                uint h = (uint)(i * 374761393) ^ (uint)seed;
                h ^= h >> 13; h *= 1274126177u; h ^= h >> 16;
                float u = (h & 0x00FFFFFF) / 16777215f; // 0..1
                return u * 2f - 1f; // -1..1
            }
        }

        Vector3 HashDir(int i, int seed)
        {
            Vector3 v = new Vector3(
                HashSigned01(i * 3 + 0, seed),
                HashSigned01(i * 3 + 1, seed),
                HashSigned01(i * 3 + 2, seed)
            );

            if (v.sqrMagnitude < 0.0001f) v = Vector3.right;
            return v.normalized;
        }

        Vector3 ExplodeDir(int edgeIndex, Vector3 mid)
        {
            // Bias outward in XZ so it reads clearly in MASSIVE’s top-down view.
            Vector3 radial = new Vector3(mid.x, 0f, mid.z);
            if (radial.sqrMagnitude < 0.0001f) radial = Vector3.right;
            radial.Normalize();

            // Random 3D direction adds “shard chaos”
            Vector3 rnd = HashDir(edgeIndex + 101, GetInstanceID());

            Vector3 dir = Vector3.Lerp(radial, rnd, Mathf.Clamp01(shatterRandomness));
            if (dir.sqrMagnitude < 0.0001f) dir = radial;
            return dir.normalized;
        }

        void ApplyShatter(int edgeIndex, ref Vector3 a, ref Vector3 b)
        {
            if (sh <= 0f && co <= 0f) return;

            Vector3 mid = (a + b) * 0.5f;

            // explode
            Vector3 dir = ExplodeDir(edgeIndex, mid);
            Vector3 off = dir * (shatterDistance * sh);

            // spin
            Vector3 spinAxis = HashDir(edgeIndex + 1337, GetInstanceID());
            Quaternion q = Quaternion.AngleAxis(shatterSpinDegrees * sh, spinAxis);

            Vector3 ra = a - mid;
            Vector3 rb = b - mid;

            Vector3 mid2 = mid + off;

            // separate + rotate around the (exploded) midpoint
            a = mid2 + q * ra;
            b = mid2 + q * rb;

            // collapse: shrink the segment into its midpoint
            if (co > 0f)
            {
                a = Vector3.Lerp(a, mid2, co);
                b = Vector3.Lerp(b, mid2, co);
            }
        }

        // --- draw logic (preserves your drawProgress behavior) ---

        if (dp >= 0.9999f)
        {
            for (int i = 0; i < edgeCount; i++)
            {
                var e = _edges[i];

                Vector3 a = Fold(_verts[e.a]);
                Vector3 b = Fold(_verts[e.b]);

                ApplyShatter(i, ref a, ref b);

                Draw.Line(a, b);
            }
        }
        else
        {
            float edgeF = dp * edgeCount;
            int fullEdges = Mathf.Clamp(Mathf.FloorToInt(edgeF), 0, edgeCount);
            float frac = Mathf.Clamp01(edgeF - fullEdges);

            for (int i = 0; i < fullEdges; i++)
            {
                var e = _edges[i];

                Vector3 a = Fold(_verts[e.a]);
                Vector3 b = Fold(_verts[e.b]);

                ApplyShatter(i, ref a, ref b);

                Draw.Line(a, b);
            }

            // partial edge for “draw in” feel
            if (fullEdges < edgeCount && frac > 0f)
            {
                var e = _edges[fullEdges];

                Vector3 a = Fold(_verts[e.a]);
                Vector3 b = Fold(_verts[e.b]);

                ApplyShatter(fullEdges, ref a, ref b);

                Draw.Line(a, Vector3.Lerp(a, b, frac));
            }
        }
    }
}


    void RebuildIfNeeded()
    {
        int hash = ComputeHash();
        if (hash == _lastHash) return;
        _lastHash = hash;

        _verts.Clear();
        _edges.Clear();

        switch (family)
        {
            case PolyFamily.Prism:     BuildPrism(); break;
            case PolyFamily.Pyramid:   BuildPyramid(); break;
            case PolyFamily.Bipyramid: BuildBipyramid(); break;
            case PolyFamily.Antiprism: BuildAntiprism(); break;
            case PolyFamily.IcoSphere: BuildIcoSphere(); break;
        }
    }

    int ComputeHash()
    {
        // coarse float hashing is fine for editor iteration
        int h = 17;
        h = h * 31 + (int)family;
        h = h * 31 + sides;
        h = h * 31 + Mathf.RoundToInt(radius * 10000f);
        h = h * 31 + Mathf.RoundToInt(height * 10000f);
        h = h * 31 + Mathf.RoundToInt(startAngleDegrees * 1000f);
        h = h * 31 + (antiprismUseClassicTwist ? 1 : 0);
        h = h * 31 + Mathf.RoundToInt(antiprismTwistDegrees * 1000f);
        h = h * 31 + icoSubdivisions;
        return h;
    }

    int AddV(Vector3 v) { _verts.Add(v); return _verts.Count - 1; }
    void AddE(int a, int b) { _edges.Add(new Edge(a, b)); }

    void BuildPrism()
    {
        float halfH = height * 0.5f;
        float a0 = startAngleDegrees * Mathf.Deg2Rad;
        float step = Mathf.PI * 2f / sides;

        int[] bot = new int[sides];
        int[] top = new int[sides];

        for (int i = 0; i < sides; i++)
        {
            float a = a0 + i * step;
            float x = Mathf.Cos(a) * radius;
            float z = Mathf.Sin(a) * radius;

            bot[i] = AddV(new Vector3(x, -halfH, z));
            top[i] = AddV(new Vector3(x, +halfH, z));
        }

        for (int i = 0; i < sides; i++)
        {
            int ni = (i + 1) % sides;

            AddE(bot[i], bot[ni]); // bottom ring
            AddE(top[i], top[ni]); // top ring
            AddE(bot[i], top[i]);  // vertical
        }
    }

    void BuildPyramid()
    {
        float halfH = height * 0.5f;
        float a0 = startAngleDegrees * Mathf.Deg2Rad;
        float step = Mathf.PI * 2f / sides;

        int apex = AddV(new Vector3(0f, +halfH, 0f));

        int[] baseRing = new int[sides];
        for (int i = 0; i < sides; i++)
        {
            float a = a0 + i * step;
            float x = Mathf.Cos(a) * radius;
            float z = Mathf.Sin(a) * radius;
            baseRing[i] = AddV(new Vector3(x, -halfH, z));
        }

        for (int i = 0; i < sides; i++)
        {
            int ni = (i + 1) % sides;
            AddE(baseRing[i], baseRing[ni]); // base
            AddE(baseRing[i], apex);         // sides
        }
    }

    void BuildBipyramid()
    {
        float halfH = height * 0.5f;
        float a0 = startAngleDegrees * Mathf.Deg2Rad;
        float step = Mathf.PI * 2f / sides;

        int topApex = AddV(new Vector3(0f, +halfH, 0f));
        int botApex = AddV(new Vector3(0f, -halfH, 0f));

        int[] ring = new int[sides];
        for (int i = 0; i < sides; i++)
        {
            float a = a0 + i * step;
            float x = Mathf.Cos(a) * radius;
            float z = Mathf.Sin(a) * radius;
            ring[i] = AddV(new Vector3(x, 0f, z));
        }

        for (int i = 0; i < sides; i++)
        {
            int ni = (i + 1) % sides;
            AddE(ring[i], ring[ni]);   // equator ring
            AddE(ring[i], topApex);    // to top
            AddE(ring[i], botApex);    // to bottom
        }
    }

    void BuildAntiprism()
    {
        float halfH = height * 0.5f;
        float a0 = startAngleDegrees * Mathf.Deg2Rad;
        float step = Mathf.PI * 2f / sides;

        float twistDeg = antiprismUseClassicTwist ? (180f / sides) : antiprismTwistDegrees;
        float twist = twistDeg * Mathf.Deg2Rad;

        int[] bot = new int[sides];
        int[] top = new int[sides];

        for (int i = 0; i < sides; i++)
        {
            float aB = a0 + i * step;
            float aT = a0 + twist + i * step;

            bot[i] = AddV(new Vector3(Mathf.Cos(aB) * radius, -halfH, Mathf.Sin(aB) * radius));
            top[i] = AddV(new Vector3(Mathf.Cos(aT) * radius, +halfH, Mathf.Sin(aT) * radius));
        }

        for (int i = 0; i < sides; i++)
        {
            int ni = (i + 1) % sides;
            int pi = (i - 1 + sides) % sides;

            // rings
            AddE(bot[i], bot[ni]);
            AddE(top[i], top[ni]);

            // cross braces (alternating triangles)
            AddE(bot[i], top[i]);
            AddE(bot[i], top[pi]);
        }
    }

    // ---------- IcoSphere ----------
    void BuildIcoSphere()
    {
        // base icosahedron
        var v = new List<Vector3>(12);
        var t = new List<int>(20 * 3);

        float phi = (1f + Mathf.Sqrt(5f)) * 0.5f;

        v.Add(N(new Vector3(-1,  phi, 0)));
        v.Add(N(new Vector3( 1,  phi, 0)));
        v.Add(N(new Vector3(-1, -phi, 0)));
        v.Add(N(new Vector3( 1, -phi, 0)));

        v.Add(N(new Vector3(0, -1,  phi)));
        v.Add(N(new Vector3(0,  1,  phi)));
        v.Add(N(new Vector3(0, -1, -phi)));
        v.Add(N(new Vector3(0,  1, -phi)));

        v.Add(N(new Vector3( phi, 0, -1)));
        v.Add(N(new Vector3( phi, 0,  1)));
        v.Add(N(new Vector3(-phi, 0, -1)));
        v.Add(N(new Vector3(-phi, 0,  1)));

        // 20 triangles
        int[] tris =
        {
            0,11,5,  0,5,1,   0,1,7,   0,7,10,  0,10,11,
            1,5,9,   5,11,4,  11,10,2, 10,7,6,  7,1,8,
            3,9,4,   3,4,2,   3,2,6,   3,6,8,   3,8,9,
            4,9,5,   2,4,11,  6,2,10,  8,6,7,   9,8,1
        };
        t.AddRange(tris);

        for (int s = 0; s < icoSubdivisions; s++)
        {
            var midCache = new Dictionary<ulong, int>(t.Count);
            var t2 = new List<int>(t.Count * 4);

            for (int i = 0; i < t.Count; i += 3)
            {
                int a = t[i];
                int b = t[i + 1];
                int c = t[i + 2];

                int ab = GetMid(a, b, v, midCache);
                int bc = GetMid(b, c, v, midCache);
                int ca = GetMid(c, a, v, midCache);

                // 4 new tris
                t2.Add(a);  t2.Add(ab); t2.Add(ca);
                t2.Add(b);  t2.Add(bc); t2.Add(ab);
                t2.Add(c);  t2.Add(ca); t2.Add(bc);
                t2.Add(ab); t2.Add(bc); t2.Add(ca);
            }

            t = t2;
        }

        // copy verts scaled to radius into our cached buffers
        for (int i = 0; i < v.Count; i++)
            AddV(v[i] * radius);

        // unique edges from triangles
        var edgeSet = new HashSet<ulong>(t.Count);
        for (int i = 0; i < t.Count; i += 3)
        {
            AddEdgeUnique(t[i], t[i + 1], edgeSet);
            AddEdgeUnique(t[i + 1], t[i + 2], edgeSet);
            AddEdgeUnique(t[i + 2], t[i], edgeSet);
        }
    }

    static Vector3 N(Vector3 p) => p.normalized;

    static int GetMid(int a, int b, List<Vector3> verts, Dictionary<ulong, int> cache)
    {
        int min = a < b ? a : b;
        int max = a < b ? b : a;
        ulong key = ((ulong)(uint)min << 32) | (uint)max;

        if (cache.TryGetValue(key, out int idx))
            return idx;

        Vector3 mid = (verts[a] + verts[b]) * 0.5f;
        idx = verts.Count;
        verts.Add(mid.normalized);
        cache[key] = idx;
        return idx;
    }

    void AddEdgeUnique(int a, int b, HashSet<ulong> set)
    {
        int min = a < b ? a : b;
        int max = a < b ? b : a;
        ulong key = ((ulong)(uint)min << 32) | (uint)max;
        if (set.Add(key))
            AddE(min, max);
    }
}
