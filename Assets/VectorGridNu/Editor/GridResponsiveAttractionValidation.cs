using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

/// <summary>Independent state and native GPU regression checks. Never touches a live scene,
/// grid instance or shared ComputeShader bindings. The comparison uses the unchanged CSMain.</summary>
public static class GridResponsiveAttractionValidation
{
    private static readonly Vector2 Arena = new Vector2(28f, 12f);
    private const string ProbePath = "Assets/VectorGridNu/Editor/GridResponsiveAttractionValidation.compute";
    private const string LegacyPath = "Assets/VectorGridNu/GridUnlitLines.compute";
    private const int Width = 225, Height = 97;

    [MenuItem("MASSIVE/Vector Grid/Validate Responsive Attraction State")]
    public static void RunMenu() { Debug.Log(Run()); }
    [MenuItem("MASSIVE/Vector Grid/Validate Responsive Attraction GPU and Baseline")]
    public static void RunGpuMenu() { Debug.Log(RunGpu()); }

    public static string Run()
    {
        var passed = new List<string>();
        foreach (int fps in new[] { 30, 60, 120 })
        {
            var field = new GridResponsiveAttraction();
            for (int i = 0; i < fps / 5; i++)
            { field.Submit(1, 0, new Vector2(.3f, -.2f), 1.32f, .4f, .2f, .2f); field.Advance(1f / fps); }
            Check(Near(Centers(field)[0].w, .4f * .95f), "95 percent strength response at .2 seconds / " + fps + " fps", passed);
            for (int i = 0; i < fps / 5; i++) field.Advance(1f / fps);
            Check(Near(Centers(field)[0].w, .4f * .95f * .05f), "95 percent release at .2 seconds / " + fps + " fps", passed);
            for (int i = 0; i < fps / 5; i++)
            { field.Submit(1, 0, new Vector2(2.3f, .8f), 2.32f, .4f, .2f, .2f); field.Advance(1f / fps); }
            Vector4 translated = Centers(field)[0];
            Check(Near(translated.x, 2.2f) && Near(translated.y, .75f) && Near(translated.z, 2.27f),
                "center and radius follow the same frame-rate-independent response / " + fps + " fps", passed);
        }
        var state = new GridResponsiveAttraction();
        state.Submit(10, 0, Vector2.one, 1.3f, .5f, 0f, .1f); state.Advance(.016f);
        Check(state.Count == 1 && Near(Centers(state)[0].w, .5f), "zero response time reaches target in current frame", passed);
        state.Submit(10, 0, new Vector2(-1, .2f), 2f, .3f, 0f, .1f);
        state.Submit(10, 0, new Vector2(2, -.4f), 1f, .7f, 0f, .1f); state.Advance(.016f);
        var latest = Centers(state)[0];
        Check(state.Count == 1 && Near(latest.x, 2) && Near(latest.y, -.4f) && Near(latest.z, 1) && Near(latest.w, .7f),
            "latest repeated owner/module submission wins without duplicates or one-frame input delay", passed);
        state.Submit(10, 1, Vector2.zero, 1, .3f, 0, .1f); state.Advance(.016f);
        Check(state.Count == 2, "separate modules retain independent owner slots", passed);
        state.Clear(); Check(state.Count == 0, "Clear removes every renderer-only source", passed);

        state.Submit(1, 0, Vector2.zero, 1, .6f, 0, .1f); state.Advance(.016f);
        var paused = Centers(state)[0];
        state.Submit(1, 0, Vector2.one * 2, 3, .9f, .1f, .1f); state.Advance(0);
        Check(Centers(state)[0] == paused && state.Count == 1, "zero delta freezes existing center, radius and strength", passed);
        state.Advance(-1); state.Advance(float.NaN); state.Advance(float.PositiveInfinity);
        Check(Centers(state).All(Finite), "invalid time deltas do not create nonfinite source data", passed);
        float previous = Centers(state)[0].w;
        bool monotonic = true;
        for (int i = 0; i < 120; i++)
        {
            state.Advance(1f / 60f);
            float current = state.Count == 0 ? 0 : Centers(state)[0].w;
            monotonic &= current >= 0 && current <= previous + .000001f; previous = current;
        }
        Check(monotonic && state.Count == 0, "missing/disabled owner releases monotonically and retires its slot", passed);
        for (int i = 0; i < GridResponsiveAttraction.Capacity + 4; i++) state.Submit(i, 0, Vector2.zero, 1, .1f, 0, .1f);
        state.Advance(.016f);
        Check(state.Count == GridResponsiveAttraction.Capacity, "source capacity is bounded", passed);
        for (int i = 0; i < 100; i++) state.Submit(0, 0, Vector2.one, 1, .3f, 0, .1f);
        state.Advance(.016f);
        Check(state.Count <= GridResponsiveAttraction.Capacity, "repeated updates do not grow the fixed pool", passed);
        state.Clear();
        state.Submit(1, 0, new Vector2(float.NaN, 0), 1, .2f, .1f, .1f);
        state.Submit(2, 0, new Vector2(0, float.PositiveInfinity), 1, .2f, .1f, .1f);
        Check(state.Count == 0, "nonfinite source centers are rejected", passed);
        state.Submit(3, 0, Vector2.zero, 0, .5f, .1f, .1f);
        state.Submit(4, 0, Vector2.zero, -1, .5f, .1f, .1f);
        state.Submit(5, 0, Vector2.zero, 1, float.NaN, .1f, .1f);
        Check(state.Count == 0, "zero/negative radius and invalid pull cannot create active influence", passed);

        Vector4[] source = { new Vector4(0, 0, 1.32f, .45f) };
        Check(Evaluate(Vector2.zero, source) == Vector3.zero, "source center has finite zero displacement", passed);
        Check(Evaluate(new Vector2(1.32f, 0), source) == new Vector3(1.32f, 0, 0)
            && Evaluate(new Vector2(1.8f, 0), source) == new Vector3(1.8f, 0, 0), "boundary and outside radius have compact zero influence", passed);
        Vector2 point = new Vector2(.45f, .2f);
        Vector3 moved = Evaluate(point, source);
        Check(moved.x > 0 && moved.x < point.x && moved.y > 0 && moved.y < point.y,
            "field pulls toward its source without crossing the center", passed);
        Vector3 mirrored = Evaluate(-point, source);
        Check((moved + mirrored).sqrMagnitude < .000000001f, "radial field is mirror symmetric", passed);
        var overlap = new[] { new Vector4(-.2f, .1f, 1.2f, .8f), new Vector4(.4f, -.2f, 1.4f, .65f), new Vector4(0, .3f, 1f, .5f) };
        Check(Vector3.Distance(Evaluate(point, overlap), Evaluate(point, overlap.Reverse().ToArray())) < .00001f,
            "overlapping sources are independent of registration order", passed);
        Vector2 metric = new Vector2(2, .5f);
        Vector2 localPoint = new Vector2(point.x / metric.x, point.y / metric.y);
        Vector3 metricPosition = GridResponsiveAttraction.EvaluateDisplacement(localPoint, localPoint,
            new Vector2(Arena.x / metric.x, Arena.y / metric.y), metric, source, 1);
        Check(Vector2.Distance(new Vector2(metricPosition.x * metric.x, metricPosition.y * metric.y), moved) < .00001f,
            "world metric preserves round world-space reach under unequal grid scale", passed);
        Vector4[] edgeSource = { new Vector4(13.8f, 0, 2f, .7f) };
        Check(Evaluate(new Vector2(14, 0), edgeSource) == new Vector3(14, 0, 0)
            && Evaluate(new Vector2(15, 0), edgeSource) == new Vector3(15, 0, 0), "pinned border and presentation overscan remain stationary", passed);
        return passed.Count + " responsive attraction state checks passed.\n" + string.Join("\n", passed);
    }

    public static string RunGpu()
    {
        var passed = new List<string>();
        Check(SystemInfo.supportsComputeShaders, "compute support available", passed);
        var probe = AssetDatabase.LoadAssetAtPath<ComputeShader>(ProbePath);
        var legacy = AssetDatabase.LoadAssetAtPath<ComputeShader>(LegacyPath);
        Check(probe != null && legacy != null, "native validation and unchanged simulation shaders found", passed);
        Check(ShaderUtil.GetComputeShaderMessages(probe).Length == 0 && ShaderUtil.GetComputeShaderMessages(legacy).Length == 0,
            "validation HLSL and original simulation compile", passed);
        var random = new System.Random(91019);
        var points = new Vector3[515];
        for (int i = 0; i < points.Length - 3; i++) points[i] = new Vector3((float)random.NextDouble() * 6 - 3,
            (float)random.NextDouble() * 4 - 2, (float)random.NextDouble() * .2f);
        points[512] = Vector3.zero; points[513] = new Vector3(14, 0, 0); points[514] = new Vector3(15, 0, 0);
        var centers = new[] { new Vector4(-.2f, .1f, 1.32f, .45f), new Vector4(.6f, -.4f, 2f, .8f), new Vector4(13.8f, 0, 2, .7f) };
        foreach (Vector2 metric in new[] { Vector2.one, new Vector2(2, .5f) })
        using (var gpu = new ResponsiveGpu(probe, points, Arena, metric))
        {
            gpu.Step(centers, centers.Length);
            var result = gpu.ReadAll();
            float error = 0;
            for (int i = 0; i < points.Length; i++) error = Mathf.Max(error, Vector3.Distance(result[i],
                GridResponsiveAttraction.EvaluateDisplacement(points[i], points[i], Arena, metric, centers, centers.Length)));
            Check(result.All(Finite) && error < .00004f, "real HLSL/CPU parity at metric " + metric + " (max error " + F(error) + ")", passed);
            Check(result[513] == points[513] && result[514] == points[514], "native GPU preserves exact border and overscan at metric " + metric, passed);
            gpu.Step(centers.Reverse().ToArray(), centers.Length);
            Check(gpu.ReadAll().Select((p, i) => Vector3.Distance(p, result[i])).Max() < .00001f,
                "native overlap order independence at metric " + metric, passed);
            gpu.Step(centers, 0);
            Check(gpu.ReadAll().Select((p, i) => Vector3.Distance(p, points[i])).Max() < .000001f,
                "zero active sources leave existing grid geometry unchanged", passed);
            gpu.Step(new[] { new Vector4(0, 0, 1.32f, .45f) }, 1);
            var compact = gpu.ReadAll();
            Check(compact[512] == points[512] && compact.Select((p, i) => new { p, i })
                .Where(v => Vector2.Scale((Vector2)points[v.i], metric).magnitude >= 1.32f)
                .All(v => Vector3.Distance(v.p, points[v.i]) < .000001f),
                "native single source is finite at center and exactly compact outside its world radius", passed);
        }
        string comparison = CompareNativePaths(probe, legacy, passed);
        return passed.Count + " responsive attraction GPU/baseline checks passed.\n" + string.Join("\n", passed) + "\n\n" + comparison;
    }

    private static string CompareNativePaths(ComputeShader probe, ComputeShader legacyShader, List<string> passed)
    {
        const float dt = 1f / 60f, response = .06f, release = .2f;
        const float responsiveRadius = 3.25f * .66f, responsivePull = .8f;
        const int holdFrames = 240, releaseFrames = 120;
        var original = Nodes();
        int probeIndex = (Height / 2) * Width + Width / 2 + 5; // x=.625, y=0, safely inside the current radius.
        var state = new GridResponsiveAttraction();
        using (var legacy = new LegacyGpu(legacyShader, original))
        using (var responsive = new ResponsiveGpu(probe, original, Arena, Vector2.one))
        {
            var oldStep = new float[holdFrames]; var newStep = new float[holdFrames];
            for (int i = 0; i < holdFrames; i++)
            {
                legacy.Step(Vector2.zero, true, dt);
                state.Submit(1, 0, Vector2.zero, responsiveRadius, responsivePull, response, release); state.Advance(dt);
                responsive.Step(Centers(state), state.Count);
                oldStep[i] = original[probeIndex].x - legacy.Read(probeIndex).x;
                newStep[i] = original[probeIndex].x - responsive.Read(probeIndex).x;
            }
            float oldRest = oldStep[holdFrames - 1], newRest = newStep[holdFrames - 1];
            Check(oldRest > .01f && newRest > .01f, "both real GPU paths produce measurable held attraction", passed);
            float old95 = FirstReached(oldStep, oldRest * .95f, dt), new95 = FirstReached(newStep, newRest * .95f, dt);
            Check(new95 <= .12f && new95 < old95, "responsive native field reaches its held displacement sooner than current simulation", passed);
            float oldResidual = 0, newResidual = 0, oldPrevious = oldRest, newPrevious = newRest;
            int oldCrossings = 0, newCrossings = 0; bool newMonotonic = true;
            for (int i = 0; i < releaseFrames; i++)
            {
                legacy.Step(Vector2.zero, false, dt); state.Advance(dt); responsive.Step(Centers(state), state.Count);
                float oldValue = original[probeIndex].x - legacy.Read(probeIndex).x;
                float newValue = original[probeIndex].x - responsive.Read(probeIndex).x;
                if (Mathf.Abs(oldValue) > .00001f && Mathf.Sign(oldValue) != Mathf.Sign(oldPrevious)) oldCrossings++;
                if (Mathf.Abs(newValue) > .00001f && Mathf.Sign(newValue) != Mathf.Sign(newPrevious)) newCrossings++;
                newMonotonic &= newValue >= -.00001f && newValue <= newPrevious + .00001f;
                if (i == 14) { oldResidual = Mathf.Abs(oldValue / oldRest); newResidual = Mathf.Abs(newValue / newRest); }
                oldPrevious = oldValue; newPrevious = newValue;
            }
            Check(newMonotonic && newCrossings == 0 && state.Count == 0, "native release has no oscillatory tail and retires the source", passed);
            Check(newResidual < .05f && newResidual < oldResidual, "native residual at .25 seconds is below five percent and smaller than current simulation", passed);

            legacy.Reset(); state.Clear();
            Vector2 source = Vector2.zero;
            for (int i = 0; i < 60; i++)
            {
                source = new Vector2(-3f + 6f * ((i + 1) * dt), 0);
                legacy.Step(source, true, dt); state.Submit(1, 0, source, responsiveRadius, responsivePull, response, release); state.Advance(dt);
                responsive.Step(Centers(state), state.Count);
            }
            float oldMovingLag = source.x - DeformationCentroid(legacy.ReadAll(), original).x;
            float newMovingLag = source.x - DeformationCentroid(responsive.ReadAll(), original).x;
            for (int i = 0; i < 30; i++)
            {
                source = new Vector2(3f - 6f * ((i + 1) * dt), 0);
                legacy.Step(source, true, dt); state.Submit(1, 0, source, responsiveRadius, responsivePull, response, release); state.Advance(dt);
                responsive.Step(Centers(state), state.Count);
            }
            float oldReverseLag = DeformationCentroid(legacy.ReadAll(), original).x - source.x;
            float newReverseLag = DeformationCentroid(responsive.ReadAll(), original).x - source.x;
            Check(Mathf.Abs(newMovingLag) < Mathf.Abs(oldMovingLag), "native moving-source field tracks closer at 6 world units per second", passed);
            Check(Mathf.Abs(newReverseLag) < Mathf.Abs(oldReverseLag), "native field tracks a reversal closer without a carried spring wake", passed);
            return "Measured isolated native GPU comparison (60 Hz, 225x97 simulation, 28x12 arena).\n" +
                "Legacy: spring12/damping2.2, profile6/radius1.32, global+module inner=.463, weightCap=.8, crowd=.6; source lies in grid plane.\n" +
                "Responsive actual new P1 tuning: radius3.25*.66=" + F(responsiveRadius) + ", pull.8, response.06s, release.2s. Amplitudes normalized to each path's own held displacement.\n" +
                "Held displacement: legacy " + F(oldRest) + ", responsive " + F(newRest) + "; first95%: " + F(old95) + "s / " + F(new95) + "s.\n" +
                "Residual at .25s after release: " + F(oldResidual * 100) + "% / " + F(newResidual * 100) + "%; release zero crossings: " + oldCrossings + " / " + newCrossings + ".\n" +
                "Deformation-centroid lag, 6u/s traverse: " + F(oldMovingLag) + " / " + F(newMovingLag) + " world units; .5s after reversal: " + F(oldReverseLag) + " / " + F(newReverseLag) + ".";
        }
    }

    private static Vector4[] Centers(GridResponsiveAttraction state)
    { var block = new MaterialPropertyBlock(); state.WriteProperties(block, Vector2.one); return block.GetVectorArray("_ResponsiveAttractorCenters"); }
    private static Vector3 Evaluate(Vector2 point, Vector4[] sources)
    { return GridResponsiveAttraction.EvaluateDisplacement(point, point, Arena, Vector2.one, sources, sources.Length); }
    private static Vector3[] Nodes()
    {
        var values = new Vector3[Width * Height];
        for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++)
            values[x + y * Width] = new Vector3((x / (float)(Width - 1) - .5f) * Arena.x, (y / (float)(Height - 1) - .5f) * Arena.y, 0);
        return values;
    }
    private static Vector2 DeformationCentroid(Vector3[] actual, Vector3[] original)
    {
        Vector2 sum = Vector2.zero; float weight = 0;
        for (int i = 0; i < actual.Length; i++)
        { float w = Vector2.Distance(actual[i], original[i]); sum += (Vector2)original[i] * w; weight += w; }
        if (weight < .00001f) throw new InvalidOperationException("GPU displacement field is empty.");
        return sum / weight;
    }
    private static float FirstReached(float[] values, float threshold, float dt)
    { for (int i = 0; i < values.Length; i++) if (values[i] >= threshold) return (i + 1) * dt; return float.PositiveInfinity; }
    private static bool Finite(Vector3 v) { return !float.IsNaN(v.x + v.y + v.z) && !float.IsInfinity(v.x + v.y + v.z); }
    private static bool Finite(Vector4 v) { return !float.IsNaN(v.x + v.y + v.z + v.w) && !float.IsInfinity(v.x + v.y + v.z + v.w); }
    private static bool Near(float a, float b) { return Mathf.Abs(a - b) < .00002f; }
    private static string F(float value) { return value.ToString("0.0000", CultureInfo.InvariantCulture); }
    private static void Check(bool value, string label, List<string> passed)
    { if (!value) throw new InvalidOperationException("Responsive attraction check failed: " + label); passed.Add(label); }

    [StructLayout(LayoutKind.Sequential)]
    private struct Query { public Vector3 position; public Vector2 flat; }
    private sealed class ResponsiveGpu : IDisposable
    {
        private readonly ComputeShader shader;
        private readonly ComputeBuffer queries, output;
        private readonly int kernel, count;
        private readonly Vector4[] centers = new Vector4[GridResponsiveAttraction.Capacity];
        private readonly Vector3[] single = new Vector3[1];
        public ResponsiveGpu(ComputeShader source, Vector3[] points, Vector2 size, Vector2 metric)
        {
            shader = UnityEngine.Object.Instantiate(source); count = points.Length; kernel = shader.FindKernel("ValidateAttraction");
            queries = new ComputeBuffer(count, Marshal.SizeOf<Query>()); output = new ComputeBuffer(count, 12);
            queries.SetData(points.Select(p => new Query { position = p, flat = p }).ToArray());
            shader.SetBuffer(kernel, "_Queries", queries); shader.SetBuffer(kernel, "_Results", output);
            shader.SetInt("_QueryCount", count); shader.SetVector("_GridSize", size); shader.SetVector("_ResponsiveGridMetric", metric);
        }
        public void Step(Vector4[] source, int length)
        {
            Array.Clear(centers, 0, centers.Length);
            if (source != null) Array.Copy(source, centers, Mathf.Min(source.Length, centers.Length));
            shader.SetInt("_ResponsiveAttractorCount", length); shader.SetVectorArray("_ResponsiveAttractorCenters", centers);
            shader.Dispatch(kernel, Mathf.CeilToInt(count / 64f), 1, 1);
        }
        public Vector3 Read(int index) { output.GetData(single, 0, index, 1); return single[0]; }
        public Vector3[] ReadAll() { var data = new Vector3[count]; output.GetData(data); return data; }
        public void Dispose() { queries.Dispose(); output.Dispose(); UnityEngine.Object.DestroyImmediate(shader); }
    }
    private sealed class LegacyGpu : IDisposable
    {
        private readonly ComputeShader shader;
        private readonly ComputeBuffer rest, positions, velocities, forces;
        private readonly Vector3[] original, single = new Vector3[1];
        private readonly VectorGridGPU.Force[] force = new VectorGridGPU.Force[1];
        private readonly int kernel;
        public LegacyGpu(ComputeShader source, Vector3[] points)
        {
            shader = UnityEngine.Object.Instantiate(source); original = points; kernel = shader.FindKernel("CSMain");
            rest = new ComputeBuffer(points.Length, 12); positions = new ComputeBuffer(points.Length, 12); velocities = new ComputeBuffer(points.Length, 12);
            forces = new ComputeBuffer(1, Marshal.SizeOf<VectorGridGPU.Force>());
            rest.SetData(points); Reset();
            shader.SetBuffer(kernel, "_OrigPos", rest); shader.SetBuffer(kernel, "_Pos", positions);
            shader.SetBuffer(kernel, "_Vel", velocities); shader.SetBuffer(kernel, "_Forces", forces);
            shader.SetInt("_Count", points.Length); shader.SetInt("_GridX", Width); shader.SetInt("_GridY", Height);
            shader.SetInt("_PinEdges", 1); shader.SetFloat("_Kspring", 12); shader.SetFloat("_Damping", 2.2f);
            shader.SetInt("_FalloffMode", 1); shader.SetFloat("_FalloffExp", 1); shader.SetFloat("_InnerFrac", .2f);
            shader.SetFloat("_Sharpness", 2); shader.SetFloat("_MaxSpeed", 12); shader.SetFloat("_WeightCap", .8f); shader.SetFloat("_CrowdStiffness", .6f);
            shader.SetInt("_FeatherCells", 0); shader.SetFloat("_FeatherSharp", .25f); shader.SetInt("_FeatherUseExp", 1);
            shader.SetFloat("_FeatherExpK", 6); shader.SetFloat("_EdgeSpringMul", 2); shader.SetFloat("_EdgeDampMul", 2.695f);
            shader.SetFloat("_EdgeForceMin", 0); shader.SetInt("_ProjectAtEdge", 1); shader.SetFloat("_EdgeAllowance", 0); shader.SetFloat("_Restitution", .6f);
        }
        public void Reset() { positions.SetData(original); velocities.SetData(new Vector3[original.Length]); }
        public void Step(Vector2 center, bool active, float dt)
        {
            force[0] = VectorGridGPU.MakeRadial(center, 1.32f, 6f, .263f); forces.SetData(force);
            shader.SetInt("_ForceCount", active ? 1 : 0); shader.SetFloat("_DeltaTime", dt);
            shader.Dispatch(kernel, Mathf.CeilToInt(original.Length / 256f), 1, 1);
        }
        public Vector3 Read(int index) { positions.GetData(single, 0, index, 1); return single[0]; }
        public Vector3[] ReadAll() { var data = new Vector3[original.Length]; positions.GetData(data); return data; }
        public void Dispose()
        { rest.Dispose(); positions.Dispose(); velocities.Dispose(); forces.Dispose(); UnityEngine.Object.DestroyImmediate(shader); }
    }
}
