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
            Draw.Thickness = thickness;
            Draw.Color = color;

            for (int i = 0; i < _edges.Count; i++)
            {
                var e = _edges[i];
                Draw.Line(_verts[e.a], _verts[e.b]);
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
