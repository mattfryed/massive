using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Bounded GPU checks with isolated buffers. Never advances or modifies the live grid.</summary>
public static class GridCurveSamplingValidation
{
    const int Width = 9, Height = 7;
    static readonly Vector2 Size = new Vector2(8f, 6f);

    [MenuItem("MASSIVE/Vector Grid/Validate Curve Smoothing")]
    public static void RunMenu() { Debug.Log(Run()); }

    public static string Run()
    {
        var passed = new List<string>();
        var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(
            "Assets/VectorGridNu/Editor/GridCurveSamplingValidation.compute");
        Require(shader != null && SystemInfo.supportsComputeShaders, "GPU probe available", passed);
        Require(ShaderUtil.GetComputeShaderMessages(shader).Length == 0, "GPU probe compiles", passed);
        var gridShader = Shader.Find("MASSIVE/GridUnlitLines");
        Require(gridShader != null && gridShader.isSupported && ShaderUtil.GetShaderMessages(gridShader).Length == 0,
            "grid line and border shader compile", passed);

        var queries = new List<Vector2>();
        for (int y = 0; y <= 48; y++)
            for (int x = 0; x <= 64; x++) queries.Add(new Vector2(x / 64f, y / 48f));
        var uv = queries.ToArray();
        var flat = Nodes((x, y) => Vector3.zero);
        foreach (float tension in new[] { 0f, .1f, 1f })
        {
            var result = Sample(shader, flat, uv, tension);
            Require(result.Select((p, i) => Vector3.Distance(p, Flat(uv[i]))).Max() < .00001f,
                "straight rest lattice at tension " + tension, passed);
        }

        var random = new System.Random(8719);
        var displaced = Nodes((x, y) => new Vector3((float)random.NextDouble() * 3 - 1.5f,
            (float)random.NextDouble() * 3 - 1.5f, (float)random.NextDouble() - .5f));
        var smooth = Sample(shader, displaced, uv);
        Require(smooth.All(Finite), "strong alternating deformation remains finite", passed);
        var knots = Enumerable.Range(0, Width * Height).Select(i =>
            new Vector2((i % Width) / (float)(Width - 1), (i / Width) / (float)(Height - 1))).ToArray();
        var atKnots = Sample(shader, displaced, knots);
        Require(atKnots.Select((p, i) => Vector3.Distance(p, displaced[i])).Max() < .00001f,
            "interpolates simulation nodes exactly", passed);

        float excursion = 0;
        for (int i = 0; i < uv.Length; i++)
        {
            int x = Mathf.Min(Width - 2, Mathf.FloorToInt(uv[i].x * (Width - 1)));
            int y = Mathf.Min(Height - 2, Mathf.FloorToInt(uv[i].y * (Height - 1)));
            Vector3 lo = Vector3.positiveInfinity, hi = Vector3.negativeInfinity;
            for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++)
            {
                Vector3 d = displaced[x + dx + (y + dy) * Width] - Flat(new Vector2(
                    (x + dx) / (float)(Width - 1), (y + dy) / (float)(Height - 1)));
                lo = Vector3.Min(lo, d); hi = Vector3.Max(hi, d);
            }
            Vector3 value = smooth[i] - Flat(uv[i]);
            for (int c = 0; c < 3; c++) excursion = Mathf.Max(excursion, lo[c] - value[c], value[c] - hi[c]);
        }
        Require(excursion < .00002f, "protected displacement never exceeds cell-corner ranges", passed);

        var legacy = Sample(shader, displaced, uv, .1f, true, false);
        Require(legacy.Select((p, i) => Vector3.Distance(p, Bilinear(displaced, uv[i]))).Max() < .00001f,
            "Linear matches the previous bilinear renderer", passed);
        Require(legacy.Select((p, i) => Vector3.Distance(p, smooth[i])).Max() > .02f,
            "Smooth changes intermediate curve positions", passed);

        var unrestricted = Sample(shader, displaced, uv, 0f, false);
        Require(unrestricted.All(Finite) && unrestricted.Select((p, i) => Vector3.Distance(p, smooth[i])).Max() > .02f,
            "overshoot toggle changes curve shape without invalid values", passed);

        // Derivatives on either side of each knot, including fractional positions
        // on the other axis (as used by Y-fitted cells and presentation zooms).
        var continuityUV = new List<Vector2>();
        const float e = .0001f;
        for (int axis = 0; axis < 2; axis++)
            for (int k = 1; k < (axis == 0 ? Width : Height) - 1; k++)
                foreach (float other in new[] { .19f, .37f, .61f })
                {
                    Vector2 p = axis == 0 ? new Vector2(k / (float)(Width - 1), other) :
                        new Vector2(other, k / (float)(Height - 1));
                    Vector2 h = axis == 0 ? new Vector2(e, 0) : new Vector2(0, e);
                    continuityUV.Add(p - h); continuityUV.Add(p); continuityUV.Add(p + h);
                }
        var continuity = Sample(shader, displaced, continuityUV.ToArray());
        float maxSlopeJump = 0;
        for (int i = 0; i < continuity.Length; i += 3)
        {
            Vector3 a = (continuity[i + 1] - continuity[i]) / e;
            Vector3 b = (continuity[i + 2] - continuity[i + 1]) / e;
            maxSlopeJump = Mathf.Max(maxSlopeJump, (a - b).magnitude / Mathf.Max(1, a.magnitude, b.magnitude));
        }
        Require(maxSlopeJump < .04f, "continuous knot tangents (finite-difference error " + maxSlopeJump.ToString("F4") + ")", passed);

        var mirrored = Nodes((x, y) => {
            Vector3 original = displaced[Width - 1 - x + y * Width] - Flat(new Vector2(
                (Width - 1 - x) / (float)(Width - 1), y / (float)(Height - 1)));
            return new Vector3(-original.x, original.y, original.z);
        });
        var mirroredResult = Sample(shader, mirrored, uv.Select(p => new Vector2(1 - p.x, p.y)).ToArray());
        Require(mirroredResult.Select((p, i) => Vector3.Distance(p, new Vector3(-smooth[i].x, smooth[i].y, smooth[i].z))).Max() < .00002f,
            "opposing sides remain mirror symmetric", passed);

        // Transpose rectangular grids too: catches hidden X-first interpolation bias.
        var transposed = new Vector3[displaced.Length];
        for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++)
        { var p = displaced[x + y * Width]; transposed[y + x * Height] = new Vector3(p.y, p.x, p.z); }
        var transposeResult = Sample(shader, transposed, uv.Select(p => new Vector2(p.y, p.x)).ToArray(),
            .1f, true, true, Height, Width, new Vector2(Size.y, Size.x));
        Require(transposeResult.Select((p, i) => Vector3.Distance(p, new Vector3(smooth[i].y, smooth[i].x, smooth[i].z))).Max() < .00002f,
            "axis-independent shared deformation field", passed);

        var pinned = Nodes((x, y) => x == 0 || y == 0 || x == Width - 1 || y == Height - 1 ? Vector3.zero :
            new Vector3(Mathf.Sin(x * 1.2f), Mathf.Cos(y * 1.4f), .2f));
        var outside = new List<Vector2>();
        for (int i = 0; i <= 60; i++)
        {
            float p = i / 60f;
            outside.Add(new Vector2(0, p)); outside.Add(new Vector2(1, p));
            outside.Add(new Vector2(p, 0)); outside.Add(new Vector2(p, 1));
            outside.Add(new Vector2(-.5f, p)); outside.Add(new Vector2(1.5f, p));
            outside.Add(new Vector2(p, -.5f)); outside.Add(new Vector2(p, 1.5f));
        }
        var edges = Sample(shader, pinned, outside.ToArray());
        Require(edges.Select((p, i) => Vector3.Distance(p, Flat(outside[i]))).Max() < .00001f,
            "pinned border and presentation overscan stay flat", passed);
        var offset = new Vector3(.13f, -.2f, .05f);
        var shifted = Sample(shader, Nodes((x, y) => offset), outside.ToArray());
        Require(shifted.Select((p, i) => Vector3.Distance(p, Flat(outside[i]) + offset)).Max() < .00001f,
            "unpinned overscan carries edge displacement", passed);
        return "Vector grid curve smoothing: " + passed.Count + " checks passed.\n" + string.Join("\n", passed);
    }

    static Vector3 Flat(Vector2 uv) => new Vector3((uv.x - .5f) * Size.x, (uv.y - .5f) * Size.y, 0);
    static bool Finite(Vector3 p) => !float.IsNaN(p.x + p.y + p.z) && !float.IsInfinity(p.x + p.y + p.z);
    static Vector3[] Nodes(Func<int, int, Vector3> displacement)
    {
        var result = new Vector3[Width * Height];
        for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++)
            result[x + y * Width] = Flat(new Vector2(x / (float)(Width - 1), y / (float)(Height - 1))) + displacement(x, y);
        return result;
    }
    static Vector3 Bilinear(Vector3[] p, Vector2 uv)
    {
        float x = uv.x * (Width - 1), y = uv.y * (Height - 1);
        int x0 = (int)x, y0 = (int)y, x1 = Mathf.Min(x0 + 1, Width - 1), y1 = Mathf.Min(y0 + 1, Height - 1);
        return Vector3.Lerp(Vector3.Lerp(p[x0 + y0 * Width], p[x1 + y0 * Width], x - x0),
            Vector3.Lerp(p[x0 + y1 * Width], p[x1 + y1 * Width], x - x0), y - y0);
    }
    static void Require(bool condition, string label, List<string> passed)
    {
        if (!condition) throw new InvalidOperationException("Grid smoothing check failed: " + label);
        passed.Add(label);
    }
    static Vector3[] Sample(ComputeShader shader, Vector3[] nodes, Vector2[] queries, float tension = .1f,
        bool protect = true, bool smooth = true, int width = Width, int height = Height, Vector2? size = null)
    {
        using (var positions = new ComputeBuffer(nodes.Length, 12))
        using (var inputs = new ComputeBuffer(queries.Length, 8))
        using (var outputs = new ComputeBuffer(queries.Length, 12))
        {
            int kernel = shader.FindKernel("ValidateSamples");
            positions.SetData(nodes); inputs.SetData(queries);
            shader.SetBuffer(kernel, "_Pos", positions); shader.SetBuffer(kernel, "_Queries", inputs);
            shader.SetBuffer(kernel, "_Results", outputs);
            shader.SetInt("_QueryCount", queries.Length);
            shader.SetInt("_SimGridX", width); shader.SetInt("_SimGridY", height);
            shader.SetVector("_GridSize", size ?? Size);
            shader.SetInt("_CurveInterpolation", smooth ? 1 : 0);
            shader.SetFloat("_CurveTension", tension);
            shader.SetInt("_CurveOvershootProtection", protect ? 1 : 0);
            shader.Dispatch(kernel, Mathf.CeilToInt(queries.Length / 64f), 1, 1);
            var result = new Vector3[queries.Length]; outputs.GetData(result);
            return result;
        }
    }
}
