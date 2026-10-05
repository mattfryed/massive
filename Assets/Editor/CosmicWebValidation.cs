#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Massive.Cosmos;
using Massive.Scoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class CosmicWebValidation
{
    const string Key = "COSMOS.WebValidation";
    static IEnumerator routine;
    static readonly List<string> checks = new(), errors = new();
    static double deadline;
    static int frame;
    static CosmicWebValidation() { EditorApplication.playModeStateChanged += State; }

    [MenuItem("MASSIVE/COSMOS/Cosmic web/Validate evolution in Play Mode %#&F12")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != CosmosLayoutSetup.ScenePath || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Open saved COSMOS in Edit Mode.");
        Directory.CreateDirectory(CosmicWebSetup.Output);
        File.WriteAllText(CosmicWebSetup.Output + "/validation.txt", "STARTING\n");
        SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
    }
    static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            checks.Clear(); errors.Clear(); deadline = EditorApplication.timeSinceStartup + 90; frame = -1;
            routine = Checks(); Application.logMessageReceived += Log; EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode) SessionState.SetBool(Key, false);
    }
    static void Log(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || routine == null || frame == Time.frameCount) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Web validation timed out");
            frame = Time.frameCount;
            if (!routine.MoveNext()) Finish(null);
        }
        catch (Exception e) { Finish(e); }
    }
    static void Finish(Exception failure)
    {
        routine = null; EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        bool passed = failure == null && errors.Count == 0;
        File.WriteAllText(CosmicWebSetup.Output + "/validation.txt", (passed ? "PASSED" : "FAILED") + "\n" + string.Join("\n", checks) + "\n" + failure + "\n" + string.Join("\n", errors));
        EditorApplication.isPlaying = false;
        Debug.Log("COSMOS cosmic web validation " + (passed ? "PASSED" : "FAILED: " + failure));
    }
    static void Check(bool value, string label)
    { if (!value) throw new Exception(label); checks.Add("PASS " + label); }
    static IEnumerator Checks()
    {
        yield return null;
        var web = CosmosLayoutSetup.All<CosmicWebBackground>().Single();
        var arena = web.GetComponent<ArenaBoundsFromVectorGrid>();
        var match = web.match;
        Check(web.RenderedParticleCount == web.particleCount && web.FilamentCount > 50, "GPU particles and three-dimensional filament topology initialized");
        Check(!ShaderUtil.ShaderHasError(web.particleShader), "Cosmic web shader compiles on the active graphics device");
        Check(Mathf.Abs(web.padding - .25f) < .0001f, "0.25 world-unit inset configured");
        Check(File.ReadAllText(CosmicWebSetup.Output + "/preservation.txt").StartsWith("PASS"), "Installation preserves chosen oval, camera and all existing transforms");
        Check(web.Age == 0, "Preparing/countdown begins in the homogeneous era");
        int generation = web.Generation;
        var randomBefore = UnityEngine.Random.state;
        var data = CosmicWebTopology.Build(web.particleCount, web.seed, out var statistics, web.organicStrength);
        Check(UnityEngine.Random.state.Equals(randomBefore), "Procedural seed does not alter gameplay random state");
        var repeat = CosmicWebTopology.Build(12, web.seed, out _, web.organicStrength);
        Check(data.Take(12).SequenceEqual(repeat), "Particle paths are deterministic for the same seed");
        Check(statistics.boundaryEnds > 0 && statistics.boundaryEnds == statistics.reconnectedEnds, $"All {statistics.boundaryEnds} outer filament ends have return connections");
        Check(statistics.meanBend > .15f, $"Filament paths have spatial curvature (mean midpoint bend {statistics.meanBend:F3} model units)");
        CheckMotion(data, web, arena);
        float startEmpty = EmptyFraction(data, arena, web, 0), midEmpty = EmptyFraction(data, arena, web, .5f), endEmpty = EmptyFraction(data, arena, web, 1);
        Check(startEmpty < .08f, $"Initial distribution fills the oval evenly (empty cells {startEmpty:P1})");
        Check(midEmpty > startEmpty + .15f, $"Midpoint develops connected filaments and voids (empty cells {midEmpty:P1})");
        Check(endEmpty > midEmpty + .12f && endEmpty > .65f, $"Late voids dominate while particles clump (empty cells {endEmpty:P1})");
        while (match.Phase != MatchRuntimePhase.Regulation) yield return null;
        Check(web.Age < .05f, "Web starts with regulation, after roster readiness and countdown");
        Capture("early");
        SetRemaining(match, match.RegulationDurationSeconds * .75f);
        yield return null; yield return null; Capture("forming");
        SetRemaining(match, match.RegulationDurationSeconds * .5f);
        yield return null; yield return null;
        Check(Mathf.Abs(web.Age - .5f) < .015f, "Midpoint follows the authoritative regulation clock");
        Check(match.BeginBonusRound(true), "Paused-clock bonus phase can be entered");
        float pausedAge = web.Age;
        for (int i = 0; i < 12; i++) yield return null;
        Check(web.Age == pausedAge, "Paused regulation does not advance cosmic evolution");
        Capture("middle");
        SetRemaining(match, match.RegulationDurationSeconds * .25f);
        yield return null; yield return null; Capture("evacuating");
        SetRemaining(match, 0);
        yield return null; yield return null;
        Check(web.Age == 1, "End of regulation reaches the void-dominated era");
        Capture("late");
        Check(web.Generation == generation, "Animation reuses its GPU buffer without rebuilding topology");
        web.enabled = false;
        Check(web.RenderedParticleCount == 0, "Disabling releases GPU resources");
        web.enabled = true; yield return null; yield return null;
        Check(web.RenderedParticleCount == web.particleCount, "Re-enabling safely reconstructs the web");
        Check(errors.Count == 0, "No runtime errors or exceptions");
    }
    static void SetRemaining(GameManagerScript match, float value) =>
        typeof(GameManagerScript).GetField("_regulationRemainingSeconds", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(match, value);

    static float EmptyFraction(CosmicWebTopology.Particle[] data, ArenaBoundsFromVectorGrid arena, CosmicWebBackground web, float age)
    {
        const int nx = 120, ny = 44;
        var cells = new bool[nx * ny];
        foreach (var particle in data)
        {
            Vector3 raw = CosmicWebTopology.Position(particle, age, web.evolutionVariation);
            Vector2 projected = CosmicWebTopology.Project(raw, arena.OutlineShaderParameters, WorldScale(web), web.padding + .06f, web.edgeCondensation);
            Vector3 p = new Vector3(projected.x / arena.OvalHalfWidthLocal, projected.y / (arena.Grid.size.y * .5f), 0);
            int x = Mathf.FloorToInt((p.x + 1) * .5f * nx), y = Mathf.FloorToInt((p.y + 1) * .5f * ny);
            if (x >= 0 && x < nx && y >= 0 && y < ny) cells[x + y * nx] = true;
        }
        int total = 0, empty = 0;
        for (int y = 0; y < ny; y++)
            for (int x = 0; x < nx; x++)
            {
                Vector3 local = new Vector3(((x + .5f) / nx * 2 - 1) * arena.OvalHalfWidthLocal,
                    ((y + .5f) / ny * 2 - 1) * arena.Grid.size.y * .5f, 0);
                if (!arena.ContainsWorldPoint(arena.transform.TransformPoint(local), web.padding)) continue;
                total++; if (!cells[x + y * nx]) empty++;
            }
        return (float)empty / Mathf.Max(1, total);
    }
    static Vector2 WorldScale(CosmicWebBackground web) => new Vector2(Mathf.Abs(web.transform.lossyScale.x), Mathf.Abs(web.transform.lossyScale.y));
    static void CheckMotion(CosmicWebTopology.Particle[] data, CosmicWebBackground web, ArenaBoundsFromVectorGrid arena)
    {
        const int count = 512;
        var probe = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Editor/CosmicWebProbe.compute");
        Check(probe, "Production trajectory/projection shader probe loads");
        var sample = data.Take(count).ToArray();
        using var input = new ComputeBuffer(count, 80);
        using var output = new ComputeBuffer(count, 16);
        input.SetData(sample);
        int kernel = probe.FindKernel("Evaluate");
        probe.SetBuffer(kernel, "_Particles", input); probe.SetBuffer(kernel, "_Projected", output);
        probe.SetInt("_Count", count); probe.SetFloat("_Variation", web.evolutionVariation);
        probe.SetFloat("_Padding", web.padding); probe.SetFloat("_Condensation", web.edgeCondensation);
        probe.SetVector("_Oval", arena.OutlineShaderParameters);
        var scale = WorldScale(web); probe.SetVector("_Scale", new Vector4(scale.x, scale.y, 0, 0));
        var actual = new Vector4[count]; float maxError = 0, minPhase = 1, maxPhase = 0;
        bool inside = true, continuous = true;
        foreach (float age in new[] { 0f, .15f, .25f, .4f, .5f, .6f, .75f, .9f, 1f })
        {
            probe.SetFloat("_Age", age); probe.Dispatch(kernel, count / 64, 1, 1); output.GetData(actual);
            for (int i = 0; i < count; i++)
            {
                Vector3 raw = CosmicWebTopology.Position(sample[i], age, web.evolutionVariation);
                Vector2 expected = CosmicWebTopology.Project(raw, arena.OutlineShaderParameters, scale, web.padding + .06f, web.edgeCondensation);
                maxError = Mathf.Max(maxError, Vector2.Distance(expected, actual[i]));
                inside &= !float.IsNaN(actual[i].x) && arena.ContainsWorldPoint(web.transform.TransformPoint(new Vector3(actual[i].x, actual[i].y, 0)), web.padding - .002f);
                if (age == .25f) { minPhase = Mathf.Min(minPhase, actual[i].z); maxPhase = Mathf.Max(maxPhase, actual[i].z); }
                continuous &= Vector3.Distance(CosmicWebTopology.Position(sample[i], age, web.evolutionVariation),
                    CosmicWebTopology.Position(sample[i], Mathf.Min(1, age + .0001f), web.evolutionVariation)) < .02f;
            }
        }
        Check(maxError < .001f, $"GPU trajectory/projection agrees with CPU reference (max error {maxError:F6})");
        Check(inside, "Projected particles stay inside the padded oval and outside goals throughout evolution");
        Check(continuous, "No trajectory jumps at epoch transitions");
        Check(maxPhase - minPhase > .3f, $"Regions collapse at different rates (quarter-match phase range {minPhase:F2}–{maxPhase:F2})");
        Check(sample.All(p => Vector3.Distance(CosmicWebTopology.Position(p, 0), p.initial) < .00001f &&
            Vector3.Distance(CosmicWebTopology.Position(p, .5f), p.filament) < .00001f &&
            Vector3.Distance(CosmicWebTopology.Position(p, 1), p.cluster) < .00001f), "Early, midpoint and late anchors remain exact");
    }
    static void Capture(string stage)
    {
        var camera = Camera.main;
        var previous = camera.targetTexture; var previousActive = RenderTexture.active;
        var rt = RenderTexture.GetTemporary(1920, 1080, 24, RenderTextureFormat.ARGB32);
        var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
            File.WriteAllBytes(CosmicWebSetup.Output + "/" + stage + ".png", image.EncodeToPNG());
        }
        finally
        { camera.targetTexture = previous; RenderTexture.active = previousActive; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(image); }
    }
}
#endif
