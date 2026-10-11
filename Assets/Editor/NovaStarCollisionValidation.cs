#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Massive.Scoring;
using Object = UnityEngine.Object;

/// <summary>Checks NOVA's solid body independently of its entry and attraction triggers.</summary>
[InitializeOnLoad]
public static class NovaStarCollisionValidation
{
    public const string Folder = "Library/NovaStarCollisionValidation";
    public const string PrefabPath = "Assets/Prefabs/Levels/NOVA_Feature.prefab";
    const string Key = "MASSIVE.NovaStarCollisionValidation";
    static readonly List<string> checks = new(), errors = new();
    static IEnumerator routine;
    static int frame;
    static double deadline;
    static bool background;
    static NovaStarCollisionValidation()
    {
        EditorApplication.playModeStateChanged += State;
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists(Folder + "/inspect.request")) { File.Delete(Folder + "/inspect.request"); Inspect(); }
            if (!File.Exists(Folder + "/install.request")) return;
            File.Delete(Folder + "/install.request");
            try { Install(); Run(); }
            catch (Exception e) { File.WriteAllText(Folder + "/install.txt", e.ToString()); Debug.LogException(e); }
        };
    }
    [MenuItem("MASSIVE/Levels/Align NOVA Star Collision With Growth")]
    public static void Install()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != NovaLevelSetup.NovaPath)
            throw new InvalidOperationException("Open NOVA in Edit Mode first.");
        Directory.CreateDirectory(Folder);
        if (!File.Exists(Folder + "/NOVA_Feature-before.prefab")) File.Copy(PrefabPath, Folder + "/NOVA_Feature-before.prefab");
        var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var star = prefab.GetComponentInChildren<NovaStarController>(true);
            var transforms = star.GetComponentsInChildren<Transform>(true).ToDictionary(t => t, t => t.localToWorldMatrix);
            var solids = star.GetComponentsInChildren<Collider>(true).Where(c => c.enabled && !c.isTrigger && c.transform != star.transform).ToArray();
            foreach (var body in solids.Select(c => c.transform).Distinct())
                if (!body.IsChildOf(star.rumble.visualRoot)) body.SetParent(star.rumble.visualRoot, true);
            foreach (var item in transforms)
                for (int i = 0; i < 16; i++)
                    if (Mathf.Abs(item.Key.localToWorldMatrix[i] - item.Value[i]) > .0001f) throw new InvalidOperationException("Authored pose changed: " + item.Key.name);
            if (solids.Length != 3 || !solids.All(c => c.transform.IsChildOf(star.rumble.visualRoot)) ||
                star.GetComponents<Collider>().Any(c => c.transform.IsChildOf(star.rumble.visualRoot)))
                throw new InvalidOperationException("Unexpected star collider layout.");
            if (!PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath)) throw new IOException("Could not save NOVA feature prefab.");
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        File.WriteAllText(Folder + "/install.txt", "Three existing solid colliders now inherit Star Visuals growth. Authored world poses, collider settings and root triggers preserved.");
    }
    [MenuItem("MASSIVE/Levels/Validate NOVA Star Collision Growth")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != NovaLevelSetup.NovaPath)
            throw new InvalidOperationException("Open NOVA in Edit Mode first.");
        Directory.CreateDirectory(Folder);
        SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".refreshHeld", true);
        AssetDatabase.DisallowAutoRefresh(); EditorApplication.isPlaying = true;
    }
    static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            checks.Clear(); errors.Clear(); frame = -1; deadline = EditorApplication.timeSinceStartup + 70;
            background = Application.runInBackground; Application.runInBackground = true;
            Application.logMessageReceived += Log; routine = Nested(Checks()); EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false);
            if (SessionState.GetBool(Key + ".refreshHeld", false))
            { SessionState.SetBool(Key + ".refreshHeld", false); AssetDatabase.AllowAutoRefresh(); }
        }
    }
    static void Log(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
    static IEnumerator Nested(IEnumerator source)
    {
        while (source.MoveNext())
            if (source.Current is IEnumerator nested)
            { var runner = Nested(nested); while (runner.MoveNext()) yield return runner.Current; }
            else yield return source.Current;
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || routine == null || frame == Time.frameCount) return;
        try
        {
            frame = Time.frameCount;
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("NOVA star collision validation timed out");
            if (!routine.MoveNext()) Finish(null);
        }
        catch (Exception e) { Finish(e); }
    }
    static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        checks.Add("PASS " + label); File.WriteAllText(Folder + "/report.txt", "RUNNING\n" + string.Join("\n", checks));
    }
    static void Finish(Exception error)
    {
        routine = null; EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        Application.runInBackground = background; Time.timeScale = 1;
        bool passed = error == null && errors.Count == 0;
        File.WriteAllText(Folder + "/report.txt", (passed ? "PASSED" : "FAILED") + " (" + checks.Count + " checks)\n" + string.Join("\n", checks) +
            "\n" + error + "\nConsole errors: " + errors.Count + "\n" + string.Join("\n", errors));
        Debug.Log("NOVA STAR COLLISION " + (passed ? "PASSED" : "FAILED")); EditorApplication.isPlaying = false;
    }
    static float Scale(Transform t) => Mathf.Max(Mathf.Abs(t.lossyScale.x), Mathf.Abs(t.lossyScale.y), Mathf.Abs(t.lossyScale.z));
    static bool BodyMatches(NovaStarController star, out float radius)
    {
        Physics.SyncTransforms();
        var visual = star.rumble.visualRoot.GetComponentsInChildren<MeshRenderer>().Single(r => r.enabled);
        var mesh = visual.GetComponent<MeshFilter>().sharedMesh;
        var center = visual.transform.TransformPoint(mesh.bounds.center);
        float visibleRadius = mesh.bounds.extents.x * Scale(visual.transform);
        var solids = star.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger).ToArray();
        var outer = solids.OfType<SphereCollider>().OrderByDescending(c => c.radius * Scale(c.transform)).First();
        radius = outer.radius * Scale(outer.transform);
        if (Mathf.Abs(radius - visibleRadius) > .002f || Vector3.Distance(outer.transform.TransformPoint(outer.center), center) > .002f) return false;
        // Check the physical surface, including the old invisible blocking area.
        foreach (var direction in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
        {
            var outside = center + direction * (visibleRadius + .05f);
            var inside = center + direction * (visibleRadius - .05f);
            if (!outer.Raycast(new Ray(center + direction * (visibleRadius + 1), -direction), out var hit, 2) || Mathf.Abs(hit.distance - 1) > .002f) return false;
            if ((outer.ClosestPoint(inside) - inside).sqrMagnitude > .00001f || solids.Any(c => (c.ClosestPoint(outside) - outside).sqrMagnitude < .00001f)) return false;
        }
        return true;
    }
    static IEnumerator Until(Func<bool> condition, float seconds, string label)
    {
        float end = Time.unscaledTime + seconds;
        while (!condition() && Time.unscaledTime < end) yield return null;
        Check(condition(), label);
    }
    static IEnumerator Checks()
    {
        var scene = SceneManager.GetActiveScene(); var star = Object.FindFirstObjectByType<NovaStarController>(); var match = star.match;
        var hashes = new[] { scene.path, PrefabPath }.ToDictionary(p => p, p => Hash128.Compute(File.ReadAllText(p)));
        var triggers = star.GetComponents<Collider>().Where(c => c.isTrigger).Cast<SphereCollider>().ToDictionary(c => c, c => c.radius);
        var rootScale = star.transform.localScale;
        var settings = new SerializedObject(match); settings.FindProperty("allPlayersInactiveHoldSeconds").floatValue = 600; settings.ApplyModifiedPropertiesWithoutUndo();
        yield return Until(() => match.Phase == MatchRuntimePhase.Regulation, 20, "Real NOVA startup reaches regulation");
        Check(star.CurrentStage == 1 && BodyMatches(star, out _), "Stage 1 solid surface matches the small visible core");
        float previousRadius = 0;
        for (int stage = 1; stage <= 4; stage++)
        {
            if (stage > 1)
            {
                typeof(GameManagerScript).GetField("_regulationRemainingSeconds", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(match, match.RegulationDurationSeconds * (1f - (stage - 1) / 4f) - .01f);
                yield return Until(() => star.CurrentStage == stage, 4, "Shared match clock advances to stage " + stage);
                float finish = Time.unscaledTime + star.transitionSeconds + .15f;
                while (Time.unscaledTime < finish)
                { if (!BodyMatches(star, out _)) throw new InvalidOperationException("Collision detached during growth to stage " + stage); yield return null; }
            }
            Check(BodyMatches(star, out float radius) && radius > previousRadius, "Stage " + stage + " physics radius matches rendering and grows (" + radius.ToString("F3") + " units)");
            previousRadius = radius;
            Check(star.transform.localScale == rootScale && triggers.All(p => p.Key.radius == p.Value), "Stage " + stage + " preserves entry and attraction trigger sizes");
            if (stage == 1 || stage == 4) { ScreenCapture.CaptureScreenshot(Folder + "/stage-" + stage + ".png"); yield return null; }
        }
        Check(star.BurstsReleased == 3, "All three mini-nova transitions still release matter");
        // Exercise the final entry pulse, which uses realtime even when world simulation pauses.
        typeof(GameManagerScript).GetField("_regulationRemainingSeconds", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(match, .01f);
        yield return Until(() => star.IsEntryWindowOpen, 3, "Final supernova entry opens normally");
        Time.timeScale = 0;
        float end = Time.unscaledTime + .8f;
        while (Time.unscaledTime < end)
        { if (!BodyMatches(star, out _)) throw new InvalidOperationException("Collision detached during final-entry pulse"); yield return null; }
        Check(star.CurrentStage == 5 && triggers.All(p => p.Key.radius == p.Value), "Final-entry pulse keeps body aligned while trigger sizes remain stable");
        Time.timeScale = 1;
        star.gameObject.SetActive(false); star.gameObject.SetActive(true); star.rumble.SetStage(1);
        Check(BodyMatches(star, out float resetRadius) && Mathf.Abs(resetRadius - .735f) < .002f, "Disable/re-enable restores correct stage 1 collision without compounded scale");
        Check(hashes.All(p => Hash128.Compute(File.ReadAllText(p.Key)) == p.Value), "Play Mode leaves the saved scene and prefab unchanged");
        Check(errors.Count == 0, "No runtime errors, exceptions or assertions");
    }
    static void Inspect()
    {
        Directory.CreateDirectory(Folder);
        var scene = SceneManager.GetActiveScene();
        var star = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<NovaStarController>(true)).Single();
        var report = new StringBuilder("Active scene: " + scene.path + " | dirty: " + scene.isDirty + "\n");
        Describe(star, report);
        var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            star = prefab.GetComponentInChildren<NovaStarController>(true);
            report.AppendLine("\nPREFAB BASELINE"); Describe(star, report);
            for (int stage = 1; stage <= 4; stage++)
            {
                star.rumble.SetStage(stage); Physics.SyncTransforms();
                report.AppendLine("\nStage " + stage); Describe(star, report);
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        File.WriteAllText(Folder + "/before.txt", report.ToString());
        Debug.Log("NOVA collider baseline captured.");
    }
    static void Describe(NovaStarController star, StringBuilder report)
    {
        report.AppendLine("Star scale " + star.transform.lossyScale + " / visual " + star.rumble.visualRoot.lossyScale);
        foreach (var t in star.GetComponentsInChildren<Transform>(true))
        {
            var colliders = t.GetComponents<Collider>(); var renderer = t.GetComponent<MeshRenderer>();
            if (colliders.Length == 0 && !renderer) continue;
            report.AppendLine(AnimationUtility.CalculateTransformPath(t, star.transform) + " active=" + t.gameObject.activeInHierarchy +
                " localScale=" + t.localScale + " worldScale=" + t.lossyScale + " position=" + t.position +
                " components=" + string.Join(",", t.GetComponents<Component>().Where(c => c).Select(c => c.GetType().Name)));
            foreach (var c in colliders) report.AppendLine("  " + c.GetType().Name + " enabled=" + c.enabled + " trigger=" + c.isTrigger + " bounds=" + c.bounds);
            if (renderer) report.AppendLine("  Mesh enabled=" + renderer.enabled + " bounds=" + renderer.bounds + " mesh=" + t.GetComponent<MeshFilter>()?.sharedMesh?.name);
        }
    }
}
#endif
