#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Massive.Cosmos;
using Massive.Levels;
using Massive.Multiplier;
using Massive.Scoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class CosmosLayoutValidation
{
    const string Key = "COSMOS.Validation", Output = "Library/CosmosLayout";
    static IEnumerator routine;
    static readonly List<string> results = new(), errors = new();
    static int frame;
    static double deadline;
    static CosmosLayoutValidation() { EditorApplication.playModeStateChanged += State; }
    [MenuItem("MASSIVE/COSMOS/Validate layout in Play Mode %#&F11")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != CosmosLayoutSetup.ScenePath || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Open saved COSMOS in Edit Mode.");
        Directory.CreateDirectory(Output); File.WriteAllText(Output + "/report.txt", "STARTING\n");
        SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
    }
    static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            SessionState.SetBool(Key + ".Background", Application.runInBackground); Application.runInBackground = true;
            results.Clear(); errors.Clear(); routine = Checks(); frame = -1;
            deadline = EditorApplication.timeSinceStartup + 100;
            Application.logMessageReceived += Log; EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        { Application.runInBackground = SessionState.GetBool(Key + ".Background", true); SessionState.SetBool(Key, false); }
    }
    static void Log(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount || routine == null) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Validation timed out");
            frame = Time.frameCount;
            if (!routine.MoveNext()) Finish(null);
        }
        catch (Exception e) { Finish(e); }
    }
    static void Finish(Exception error)
    {
        routine = null; EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        bool passed = error == null && errors.Count == 0;
        File.WriteAllText(Output + "/report.txt", (passed ? "PASSED" : "FAILED") + "\n" + string.Join("\n", results) + "\n" + error + "\n" + string.Join("\n", errors));
        EditorApplication.isPlaying = false;
        Debug.Log("COSMOS validation " + (passed ? "PASSED" : "FAILED: " + error));
    }
    static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        results.Add("PASS " + label); File.WriteAllText(Output + "/report.txt", "RUNNING\n" + string.Join("\n", results));
    }
    static IEnumerator Checks()
    {
        yield return null;
        var layout = CosmosLayoutSetup.All<CosmosArenaLayout>().Single();
        var bounds = layout.GetComponent<ArenaBoundsFromVectorGrid>();
        var grid = bounds.Grid;
        var context = CosmosLayoutSetup.All<LevelSceneContext>().Single();
        Check(context.level.levelTitle == "COSMOS" && context.stageTitleText.text == "COSMOS", "COSMOS identity and HUD");
        Check(layout.Walls && layout.Walls.childCount == 194, "Curved solid walls rebuilt after scene load");
        Check(grid.GetComponents<MonoBehaviour>().Where(c => c && c.GetType().Name == "ArenaBoundaryCollidersFromVectorGrid").All(c => !c.enabled), "Rectangular wall builder disabled only in COSMOS");
        Check(!grid.boundary.borderOverlay && Mathf.Abs(bounds.OutlineHalfHeightLocal(0) - 7) < .001f &&
            Mathf.Abs(bounds.OvalHalfWidthLocal - 15.6f) < .001f, "14-high centre and 31.2-wide true ellipse");
        Check(Mathf.Abs(Camera.main.orthographicSize - 9) < .001f, "Original camera framing retained");
        Check(File.ReadAllText(Output + "/layout-preservation.txt").StartsWith("PASS"), "Apply preserved all non-grid/goal transforms and cameras");
        float previousSlope = 0;
        bool convex = true;
        for (int i = 1; i <= 96; i++)
        {
            float x = 12f * i / 96;
            float slope = bounds.OutlineHalfHeightLocal(x) - bounds.OutlineHalfHeightLocal(x - 12f / 96);
            convex &= slope <= previousSlope + .00001f; previousSlope = slope;
        }
        Check(convex, "Oval shoulders stay convex without pinched goal joins");
        Vector3 World(float x, float y) => grid.transform.TransformPoint(new Vector3(x, y, 0));
        Check(!bounds.ContainsWorldPoint(World(11, 6.8f)), "Former rectangular corners are outside");
        Check(!bounds.ContainsWorldPoint(World(13, 0)), "Goal caps remain outside playable bounds");
        ValidateCurvedGrid(grid, bounds);
        foreach (var goal in CosmosLayoutSetup.All<CosmosGoalShape>())
        {
            bool ellipse = true;
            for (int i = 0; i <= 96; i++)
            {
                float angle = Mathf.Lerp(-Mathf.PI * .5f, Mathf.PI * .5f, i / 96f);
                Vector3 p = grid.transform.InverseTransformPoint(goal.MapDiscPoint(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle))));
                ellipse &= Mathf.Abs(p.x * p.x / (15.6f * 15.6f) + p.y * p.y / 49f - 1) < .0001f;
            }
            Check(ellipse, "Goal and promotion cap follow the same exact ellipse: " + goal.side);
        }
        foreach (float x in new[] { -15.6f, 15.6f })
        {
            Vector3 viewport = Camera.main.WorldToViewportPoint(new Vector3(x, 26.93f, 0));
            Check(viewport.x > .01f && viewport.x < .99f, "Whole goal cap is visible at " + x);
        }
        for (int i = 0; i < 30; i++)
        {
            float angle = i * Mathf.PI * 2 / 30;
            Vector3 p = World(Mathf.Cos(angle) * 20, Mathf.Sin(angle) * 9);
            Vector3 clamped = bounds.ClampWorldPointInside(p, .5f);
            Check(bounds.ContainsWorldPoint(clamped, .499f), "Padded boundary clamp " + i);
        }
        foreach (var t in CosmosLayoutSetup.All<PlayerControllerScript>().Select(p => p.MatchSpawnAnchor).Distinct())
            Check(bounds.ContainsWorldPoint(t.position, .5f), "Safe spawn/respawn " + t.name);
        Physics.SyncTransforms();
        int rayHits = 0;
        foreach (Transform wall in layout.Walls)
        {
            Vector3 origin = wall.position - wall.up * .8f;
            if (Physics.Raycast(origin, wall.up, out RaycastHit hit, 1.2f, ~0, QueryTriggerInteraction.Ignore) && hit.transform.IsChildOf(layout.Walls)) rayHits++;
        }
        Check(rayHits == 194, "Physics raycasts hit all 194 wall segments");
        float readyEnd = Time.time + 18;
        while (context.match.Phase != MatchRuntimePhase.Regulation && Time.time < readyEnd) yield return null;
        Check(context.match.Phase == MatchRuntimePhase.Regulation && context.roster.IsRosterReady, "Direct launch reaches regulation with roster ready");
        ScreenCapture.CaptureScreenshot(Output + "/cosmos-gameplay.png"); yield return null; yield return null;

        // Real rigidbody contact, including the shoulders and both goal separators.
        var probe = new GameObject("COSMOS boundary physics probe");
        var sphere = probe.AddComponent<SphereCollider>(); sphere.radius = .25f;
        var body = probe.AddComponent<Rigidbody>(); body.useGravity = false; body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
        foreach (Vector2 local in new[] { new Vector2(-9, 5), new Vector2(9, 5), new Vector2(-9, -5), new Vector2(9, -5), new Vector2(-12, 0), new Vector2(12, 0) })
        {
            Vector3 edge = World(local.x, local.y == 0 ? 0 : Mathf.Sign(local.y) * bounds.OutlineHalfHeightLocal(local.x)); edge.y = .2f;
            Vector3 start = bounds.ClampWorldPointInside(edge, .6f); start.y = .2f;
            body.position = start; body.linearVelocity = (edge - start).normalized * 16;
            Physics.SyncTransforms(); float end = Time.time + .35f;
            while (Time.time < end) yield return null;
            Check(bounds.ContainsWorldPoint(body.position, .22f), "Moving rigidbody contained at " + local);
            body.linearVelocity = Vector3.zero;
        }
        Object.Destroy(probe);
        var scores = CosmosLayoutSetup.All<ScoreSphereScript>();
        var visuals = CosmosLayoutSetup.All<ScoreVoidMetaballsVisual>().Where(v => v.transform.parent.name.Contains("goal")).ToArray();
        Vector3[] triggerSizes = scores.Select(s => s.GetComponent<BoxCollider>().bounds.size).ToArray();
        Check(visuals.Length == 2 && visuals.Select(v => v.TeamID).Distinct().Count() == 2, "Both goal visuals retain separate team ownership");
        var visiblePromotions = new HashSet<string>();
        foreach (long score in new[] { 0L, 999L, 1000L, 999999L, 1000000L, 999999999L, 1000000000L, 999999999999999L })
        {
            foreach (var s in scores) s.PreviewScorePresentation(score, true);
            foreach (var v in visuals) v.PreviewScorePresentation(score, true);
            float end = Time.time + 1.1f;
            while (Time.time < end)
            {
                foreach (var ring in CosmosLayoutSetup.All<CosmosGoalShape>())
                    if (ring.PromotionVisible)
                        visiblePromotions.Add(ring.transform.parent.name);
                yield return null;
            }
            for (int i = 0; i < scores.Length; i++)
                Check((scores[i].GetComponent<BoxCollider>().bounds.size - triggerSizes[i]).sqrMagnitude < .0001f,
                    "Score " + score + " leaves team " + scores[i].teamID + " capture geometry fixed");
        }
        Check(visiblePromotions.Count == 2, "Elliptical promotion rings render for both teams");
        ScreenCapture.CaptureScreenshot(Output + "/cosmos-high-score.png"); yield return null; yield return null;
        foreach (var s in scores) { Check(Mathf.Abs(s.MaxSize - 11 * bounds.OvalGoalHalfHeightLocal / 6) < .01f, "Legacy score maximum scaled for team " + s.teamID); s.EndScorePresentationPreview(); }
        foreach (var v in visuals)
        {
            Check(v.EllipticalContainer && Mathf.Abs(v.transform.lossyScale.x - bounds.OvalGoalDepthLocal * 2) < .001f &&
                Mathf.Abs(v.transform.lossyScale.z - bounds.OvalGoalHalfHeightLocal * 2) < .001f,
                "Score renderer fits elliptical goal depth and height " + v.TeamID);
            var block = new MaterialPropertyBlock(); v.GetComponent<MeshRenderer>().GetPropertyBlock(block);
            Check(block.GetVector("_CosmosGoalProfile").w > .5f, "Score fill uses elliptical cap mapping " + v.TeamID);
            v.EndScorePresentationPreview();
        }
        var service = MatchScoreService.Instance;
        Check(service.GetTeamScore(1) == 0 && service.GetTeamScore(2) == 0, "Score previews do not alter authoritative totals");
        foreach (var goal in CosmosLayoutSetup.All<AmplifierGoalCapture>())
        {
            int before = service.GetTeamAmplifierTierIndex(goal.TeamID);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Power-ups/Amplifier Core/Amplifier Core.prefab");
            Vector3 position = goal.CapturePoint.position; position.x -= Mathf.Sign(position.x) * .45f; position.y = .27f;
            var core = Object.Instantiate(prefab, position, Quaternion.identity).GetComponent<AmplifierCoreGameplay>();
            core.CompleteSpawnImmediately(); core.Body.position = position;
            float end = Time.time + 3;
            while (service.GetTeamAmplifierTierIndex(goal.TeamID) == before && Time.time < end) yield return null;
            Check(service.GetTeamAmplifierTierIndex(goal.TeamID) == before + 1, "Physical Core capture advances team " + goal.TeamID + " Amplifier");
            if (core) Object.Destroy(core.gameObject);
        }
        float finish = Time.time + 2;
        while (Time.time < finish) yield return null;
        Check(errors.Count == 0, "No runtime errors or exceptions");
    }

    static void ValidateCurvedGrid(VectorGridGPU grid, ArenaBoundsFromVectorGrid bounds)
    {
        var buffer = (ComputeBuffer)typeof(VectorGridGPU).GetField("_origBuf",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(grid);
        var rest = new Vector3[grid.GridX * grid.GridY]; buffer.GetData(rest);
        int middle = (grid.GridY / 2) * grid.GridX;
        float centerSpacing = rest[middle + grid.GridX + grid.GridX / 2].y - rest[middle + grid.GridX / 2].y;
        float edgeSpacing = rest[middle + grid.GridX].y - rest[middle].y;
        Check(edgeSpacing < centerSpacing * .7f, "Simulation rows condense at the sides");
        Check(Mathf.Abs(rest[(grid.GridY - 1) * grid.GridX].y - bounds.OvalGoalHalfHeightLocal) < .001f,
            "Pinned simulation boundary meets the goal separator");
        var sampler = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/VectorGridNu/Editor/GridCurveSamplingValidation.compute");
        var queries = new[] { new Vector2(0, .75f), new Vector2(.25f, .75f), new Vector2(.5f, .75f), new Vector2(.75f, .75f), new Vector2(1, .75f) };
        using (var inputs = new ComputeBuffer(queries.Length, 8))
        using (var outputs = new ComputeBuffer(queries.Length, 12))
        {
            try
            {
                int kernel = sampler.FindKernel("ValidateSamples"); inputs.SetData(queries);
                sampler.SetBuffer(kernel, "_Pos", buffer); sampler.SetBuffer(kernel, "_Queries", inputs); sampler.SetBuffer(kernel, "_Results", outputs);
                sampler.SetInt("_QueryCount", queries.Length); sampler.SetInt("_SimGridX", grid.GridX); sampler.SetInt("_SimGridY", grid.GridY);
                sampler.SetVector("_GridSize", grid.size); sampler.SetVector("_ArenaOval", bounds.OutlineShaderParameters);
                sampler.SetInt("_CurveInterpolation", 1); sampler.SetFloat("_CurveTension", grid.curveTension); sampler.SetInt("_CurveOvershootProtection", 1);
                sampler.Dispatch(kernel, 1, 1, 1);
                var actual = new Vector3[queries.Length]; outputs.GetData(actual);
                Check(actual[2].y > actual[1].y && actual[1].y > actual[0].y && Mathf.Abs(actual[0].y - actual[4].y) < .001f,
                    "Production GPU sampler bows rows symmetrically");
                Check(actual.Select((p, i) => Vector3.Distance(p, bounds.GridRestPosition(queries[i]))).Max() < .001f,
                    "Rendered grid and simulation agree on curved rest positions");
                sampler.SetVector("_ArenaOval", Vector4.zero);
                var flat = new Vector3[rest.Length];
                for (int y = 0; y < grid.GridY; y++) for (int x = 0; x < grid.GridX; x++)
                    flat[x + y * grid.GridX] = new Vector3(((float)x / (grid.GridX - 1) - .5f) * grid.size.x,
                        ((float)y / (grid.GridY - 1) - .5f) * grid.size.y, 0);
                using (var flatBuffer = new ComputeBuffer(flat.Length, 12))
                {
                    flatBuffer.SetData(flat); sampler.SetBuffer(kernel, "_Pos", flatBuffer); sampler.Dispatch(kernel, 1, 1, 1); outputs.GetData(actual);
                    Check(actual.All(p => Mathf.Abs(p.y - 3.5f) < .001f), "Rectangular scenes retain flat rows when ellipse is disabled");
                }
            }
            finally { sampler.SetVector("_ArenaOval", Vector4.zero); }
        }
    }
}
#endif
