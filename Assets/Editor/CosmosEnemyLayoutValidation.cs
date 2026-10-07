#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Massive.Enemies;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Uses the scene's real encounter assets. All fixture changes are Play Mode only.</summary>
[InitializeOnLoad]
public static class CosmosEnemyLayoutValidation
{
    const string Key = "COSMOS.EnemyLayoutValidation", Folder = "Library/CosmosEnemyLayoutValidation";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<string> checks = new(), errors = new();
    static IEnumerator routine;
    static int frame;
    static double deadline;
    static float timeScale;
    static bool background;
    static CosmosEnemyLayoutValidation()
    {
        EditorApplication.playModeStateChanged += State;
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(Folder + "/run.request") || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Folder + "/run.request"); Run();
        };
    }
    [MenuItem("MASSIVE/COSMOS/Validate oval enemy placements")]
    public static void Run()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != CosmosLayoutSetup.ScenePath || scene.isDirty)
            throw new InvalidOperationException("Open saved COSMOS in Edit Mode.");
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Folder + "/report.txt", "STARTING\n");
        File.WriteAllText(Folder + "/cues.txt", "");
        SessionState.SetString(Key + ".Scene", Hash128.Compute(File.ReadAllText(scene.path)).ToString());
        SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
    }
    static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            checks.Clear(); errors.Clear(); frame = -1;
            deadline = EditorApplication.timeSinceStartup + 180;
            timeScale = Time.timeScale; background = Application.runInBackground;
            Application.runInBackground = true;
            Application.logMessageReceived += Log;
            routine = Checks(); EditorApplication.update += Tick;
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
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Validation timed out.");
            frame = Time.frameCount;
            if (!routine.MoveNext()) Finish(null);
        }
        catch (Exception e) { Finish(e); }
    }
    static void Finish(Exception failure)
    {
        routine = null; EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        Time.timeScale = timeScale; Application.runInBackground = background;
        bool passed = failure == null && errors.Count == 0;
        File.WriteAllText(Folder + "/report.txt", (passed ? "PASSED" : "FAILED") + "\n" + string.Join("\n", checks) + "\n" + failure + "\n" + string.Join("\n", errors));
        Debug.Log("COSMOS enemy layout validation " + (passed ? "PASSED" : "FAILED: " + failure));
        EditorApplication.isPlaying = false;
    }
    static void Check(bool value, string label)
    {
        if (!value) throw new Exception(label);
        checks.Add("PASS " + label); File.WriteAllText(Folder + "/report.txt", "RUNNING\n" + string.Join("\n", checks));
    }
    static IEnumerator Checks()
    {
        yield return null;
        var director = Object.FindObjectsByType<EnemyDirector>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Single(d => d.encounterTimeline && d.arenaLayout && d.arenaLayout.arena.ovalOutline);
        var layout = director.arenaLayout; var arena = layout.arena;
        director.enabled = false;
        var timeline = director.encounterTimeline;
        var forms = timeline.cues.SelectMany(c => new[] { c.formation, c.fallback }.Concat(c.variants)).Where(f => f).Distinct().ToArray();
        var files = forms.Select(AssetDatabase.GetAssetPath).Append(AssetDatabase.GetAssetPath(timeline)).ToArray();
        var hashes = files.ToDictionary(p => p, p => Hash128.Compute(File.ReadAllText(p)));
        Check(arena.IsValid && arena.ovalOutline, "COSMOS Director uses live oval arena bounds");
        Check(layout.ovalSpawnInset >= 0f, "Oval Spawn Inset is available on the scene layout: " + layout.ovalSpawnInset);
        GeometryChecks(layout, forms[0].slots[0]);
        int poses = 0;
        foreach (var form in forms) foreach (bool mirror in new[] { false, true })
        {
            var resolved = new List<(EnemyArenaLayout.Pose pose, float radius)>();
            foreach (var slot in form.slots)
            {
                if (!layout.Resolve(slot, mirror, out var pose, out string reason))
                {
                    arena.ovalOutline = false;
                    bool baselineResolved;
                    string baselineReason;
                    try { baselineResolved = layout.Resolve(slot, mirror, out _, out baselineReason); }
                    finally { arena.ovalOutline = true; }
                    if (baselineResolved || reason != baselineReason || !reason.StartsWith("Unavailable socket:"))
                        throw new Exception(form.name + ": " + reason);
                    checks.Add("EXISTING " + form.name + ": " + reason); continue;
                }
                float radius = Mathf.Max(director.spawnCheckRadiusWorld, slot.enemy.GetSpawnRadiusWorld());
                if (!layout.Clear(pose, radius, director.borderBufferWorld, out reason))
                    throw new Exception(form.name + " slot " + resolved.Count + ": " + reason + " at " + pose.position);
                if (resolved.Any(p => Vector3.Distance(p.pose.clearance, pose.clearance) < p.radius + radius - .0001f))
                    throw new Exception(form.name + " overlaps itself at slot " + resolved.Count);
                resolved.Add((pose, radius)); poses++;
            }
        }
        Check(true, $"All {poses} original and mirrored formation slots fit their enemy footprints and retain internal spacing");

        // Isolate each cue's placement from unrelated match actors, keeping real wall
        // colliders, original formation assets, budgets, masks, warning and spawn code.
        var scope = new GameObject("Oval spawn validation scope");
        director.ConfigureDemonstration(scope.transform);
        var step = typeof(EnemyDirector).GetMethod("TickTimeline", Private);
        var warningsField = typeof(EnemyDirector).GetField("_telegraphs", Private);
        var releases = new List<(EnemyDefinition definition, Vector3 position, float age)>();
        void Spawned(EnemyBase enemy)
        {
            var statuses = director.CueStates.SelectMany(c => c.slots).ToArray();
            if (!statuses.Any(s => s.announced && Vector3.Distance(s.position, enemy.transform.position) < .001f &&
                Quaternion.Angle(s.rotation, enemy.transform.rotation) < .01f)) throw new Exception("Spawn did not match an announced pose");
            releases.Add((enemy.Definition, enemy.transform.position, director.GameplayAge));
            enemy.Pause(true);
        }
        director.TimelineEnemySpawned += Spawned;
        bool captured = false, sawTurret = false;
        for (int cue = 0; cue < timeline.cues.Count; cue++)
        {
            director.previewCue = cue; director.timelinePaused = false; director.RestartTimeline(true);
            yield return null; Physics.SyncTransforms(); releases.Clear();
            var locked = new Dictionary<EnemyDirector.SlotStatus, (Vector3 position, Quaternion rotation)>();
            var state = director.CueStates.Single();
            bool missingMount = state.formation.slots.Any(s => !string.IsNullOrEmpty(s.socket) && !layout.sockets.Any(m => m.enabled && m.id == s.socket));
            while (!state.finished && director.GameplayAge < 30f)
            {
                step.Invoke(director, new object[] { .05f });
                foreach (var status in state.slots.Where(s => s.announced))
                {
                    if (locked.TryGetValue(status, out var prior))
                    {
                        if (Vector3.Distance(status.position, prior.position) > .0001f || Quaternion.Angle(status.rotation, prior.rotation) > .01f)
                            throw new Exception("An announced oval pose moved");
                    }
                    else locked.Add(status, (status.position, status.rotation));
                }
                foreach (var warning in (List<EnemySpawnTelegraph>)warningsField.GetValue(director))
                    if (warning && !state.slots.Any(s => Vector3.Distance(s.position, warning.transform.position) < .001f))
                        throw new Exception("Telegraph did not use a resolved oval pose");
                if (!captured && cue == 0 && director.GameplayAge >= 3.8f)
                { yield return null; Capture("rows.png"); captured = true; }
                // Allow Start/physics/render to run, while keeping deterministic arrival times.
                if (Mathf.RoundToInt(director.GameplayAge * 20) % 5 == 0) yield return null;
            }
            File.AppendAllText(Folder + "/cues.txt", state.label + ": " + state.state + " " + state.Progress + " " + state.reason + "\n" +
                string.Join("\n", state.slots.Where(s => s.state != "Spawned").Select(s => s.index + ": " + s.state + " " + s.reason)) + "\n");
            if (missingMount && state.formation.slots.Any(s => !string.IsNullOrEmpty(s.socket) && !layout.sockets.Any(m => m.enabled && m.id == s.socket)))
            {
                Check(state.state == "Skipped" && releases.Count == 0 && state.reason.StartsWith("Unavailable socket:"),
                    state.label + ": existing missing-mount behavior is preserved");
                continue;
            }
            Check(state.state == "Complete" && releases.Count == state.formation.slots.Count,
                state.label + $": all {state.formation.slots.Count} members spawn at their locked warnings");
            var expected = state.formation.slots.GroupBy(s => s.releaseDelay).OrderBy(g => g.Key).ToArray();
            var actual = releases.GroupBy(r => r.age).OrderBy(g => g.Key).ToArray();
            File.AppendAllText(Folder + "/cues.txt", "Releases: " + string.Join(", ", actual.Select(g => $"{g.Key:0.000}s x{g.Count()}")) + "\n");
            // The Director carries frame drift forward to preserve each interval. Compare
            // adjacent gaps, allowing a tick at warning and another at release, rather
            // than cumulative drift (strict staggered slots can encounter both).
            Check(expected.Length == actual.Length && expected.Select((g, i) => g.Count() == actual[i].Count() &&
                (i == 0 || (actual[i].Key - actual[i - 1].Key >= g.Key - expected[i - 1].Key - .001f &&
                    actual[i].Key - actual[i - 1].Key <= g.Key - expected[i - 1].Key + .101f))).All(v => v),
                state.label + ": original release grouping and stagger are preserved");
            if (state.formation.slots.Any(s => !string.IsNullOrEmpty(s.socket)))
            {
                yield return null;
                foreach (var turret in director.enemyRoot.GetComponentsInChildren<ParticleBeamTurretController>())
                    if (!locked.Values.Any(p => Vector3.Distance(p.position, turret.transform.position) < .001f &&
                        Quaternion.Angle(p.rotation, turret.transform.rotation) < .01f)) throw new Exception("Turret Start changed its curved mount");
                Capture("mounts.png"); sawTurret = true;
            }
        }
        Check(captured && sawTurret, "Live curved rows and wall mounts captured for visual review");
        director.RestartTimeline(true); director.TimelineEnemySpawned -= Spawned;
        Object.Destroy(scope); yield return null;
        Check(hashes.All(h => Hash128.Compute(File.ReadAllText(h.Key)) == h.Value), "Timeline and every referenced formation asset remain byte-for-byte unchanged");
        Check(Hash128.Compute(File.ReadAllText(SceneManager.GetActiveScene().path)).ToString() == SessionState.GetString(Key + ".Scene", ""),
            "Saved COSMOS scene, camera, goals, grid and HUD remain unchanged");
    }
    static void GeometryChecks(EnemyArenaLayout layout, EnemyFormation.Slot source)
    {
        var arena = layout.arena; var grid = arena.Grid; var half = arena.Current.halfSizeLocal;
        float inset = layout.ovalSpawnInset;
        var slot = new EnemyFormation.Slot { enemy = source.enemy, telegraph = source.telegraph, region = "Arena" };
        Vector3 center = layout.World(Vector2.zero);
        bool inside = true, symmetric = true, height = true;
        for (int y = 0; y <= 20; y++) for (int x = 0; x <= 20; x++)
        {
            slot.position = new Vector2(x / 20f, y / 20f);
            layout.Resolve(slot, false, out var a, out _); layout.Resolve(slot, true, out var b, out _);
            inside &= arena.ContainsWorldPoint(a.position, Mathf.Max(0f, inset - .0001f));
            height &= Mathf.Abs(a.position.y - layout.gameplayHeight) < .00001f;
            symmetric &= Mathf.Abs(Vector3.Dot(a.position + b.position - 2 * center, arena.Current.axisX_WS)) < .0001f;
        }
        Check(inside && height && symmetric, "441 sampled positions respect the inset, horizontal mirroring and gameplay height");
        Check(Mathf.Abs(layout.World(new Vector2(.9f, .8f)).z - center.z) < Mathf.Abs(layout.World(new Vector2(0, .8f)).z - center.z),
            "Rows curve inward toward the goal separators");
        layout.ovalSpawnInset = 0;
        slot.position = new Vector2(.95f, .95f); layout.Resolve(slot, false, out var outer, out _);
        layout.ovalSpawnInset = 1;
        layout.Resolve(slot, false, out var inner, out _);
        layout.ovalSpawnInset = inset;
        Check((inner.position - center).sqrMagnitude < (outer.position - center).sqrMagnitude && arena.ContainsWorldPoint(inner.position, .9999f),
            "Inspector inset moves formations inward in world units");
        foreach (var socket in layout.sockets.Where(s => s.enabled))
        {
            slot.socket = socket.id; layout.Resolve(slot, false, out var pose, out _);
            Vector3 tangent = layout.World(socket.position + Vector2.right * .001f) - layout.World(socket.position - Vector2.right * .001f);
            Vector3 inward = pose.rotation * Vector3.forward;
            Check(Vector3.Distance(pose.position, layout.World(socket.position)) < .00001f &&
                Mathf.Abs(Vector3.Dot(tangent.normalized, inward)) < .001f && Vector3.Dot(inward, center - pose.position) > 0,
                socket.id + " remains on the curve and aims along the inward normal");
        }
        slot.socket = null;
        arena.ovalOutline = false;
        try
        {
            bool rectangular = true;
            float sign = Vector3.Dot(arena.Current.axisY_WS, Vector3.forward) >= 0 ? 1 : -1;
            foreach (var p in new[] { Vector2.zero, new Vector2(-.8f, .7f), Vector2.one, -Vector2.one })
            {
                var expected = grid.transform.TransformPoint(new Vector3(p.x * half.x, p.y * half.y * sign)); expected.y = layout.gameplayHeight;
                rectangular &= Vector3.Distance(layout.World(p), expected) < .00001f;
                slot.position = (p + Vector2.one) * .5f; layout.Resolve(slot, false, out var pose, out _);
                rectangular &= Vector3.Distance(pose.position, expected) < .00001f;
            }
            Check(rectangular, "Rectangular arenas retain their original positions exactly and ignore oval inset");
        }
        finally { arena.ovalOutline = true; }
        Vector2 originalSize = grid.size;
        Vector3 originalScale = grid.transform.localScale;
        try
        {
            grid.size = originalSize * new Vector2(1.1f, 1.2f); grid.transform.localScale = new Vector3(1.2f, .9f, 1f); arena.RefreshNow(false);
            slot.position = new Vector2(.9f, .9f); layout.Resolve(slot, false, out var pose, out _);
            Check(arena.ContainsWorldPoint(pose.position, Mathf.Max(0, inset - .0001f)), "Mapping follows live width/height and nonuniform scale changes");
        }
        finally { grid.size = originalSize; grid.transform.localScale = originalScale; arena.RefreshNow(false); }
    }
    static void Capture(string name)
    {
        var camera = Camera.main; if (!camera) return;
        var target = RenderTexture.GetTemporary(1600, 900, 24);
        var prior = camera.targetTexture; var active = RenderTexture.active;
        var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply(); File.WriteAllBytes(Folder + "/" + name, image.EncodeToPNG());
        }
        finally { camera.targetTexture = prior; RenderTexture.active = active; RenderTexture.ReleaseTemporary(target); Object.Destroy(image); }
    }
}
#endif
