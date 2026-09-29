#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Massive.Demonstrations;
using Massive.EditorTools;
using Massive.Player;
using Massive.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class OriginalAttackStageValidation
{
    const string Key = "MASSIVE.OriginalStageValidation.";
    const string Output = "Library/OriginalStagePrefabValidation";
    static readonly Stack<IEnumerator> routines = new();
    static readonly List<string> results = new();
    static int frame;
    static double deadline;
    static MeleeVisualProfile profile;
    struct Particle { public Vector3 p; public Vector2 v; public float u, life, maxLife, size; public uint state; }
    static OriginalAttackStageValidation()
    {
        EditorApplication.update += BeginWhenReady;
        EditorApplication.playModeStateChanged += StateChanged;
    }
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new Exception("Use an isolated batch editor.");
        SessionState.SetBool(Key + "Start", true);
    }
    static void BeginWhenReady()
    {
        if (!SessionState.GetBool(Key + "Start", false) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        SessionState.SetBool(Key + "Start", false);
        try
        {
            OriginalAttackStagePrefabSetup.Install();
            Directory.CreateDirectory(Output);
            profile = AssetDatabase.LoadAssetAtPath<MeleeVisualProfile>(OriginalAttackStagePrefabSetup.ProfilePath);
            var all = new[] { profile.originalThrustPrefab, profile.originalSweepPrefab, profile.originalRepulsorPrefab };
            if (all.Any(p => !p) || all.Select(p => p.GetInstanceID()).Distinct().Count() != 3) throw new Exception("Three distinct prefab assignments are required.");
            for (int i = 0; i < 3; i++)
            {
                string path = AssetDatabase.GetAssetPath(all[i]);
                SessionState.SetString(Key + "Path" + i, path);
                SessionState.SetString(Key + "Original" + i, File.ReadAllText(path));
                if ((int)all[i].stage != i) throw new Exception("Incorrect stage assignment.");
                if (all[i].GetComponentsInChildren<Collider>(true).Length > 0) throw new Exception("Visual prefabs must not introduce hitboxes.");
            }
            // The same explicit preview path used in Prefab Mode must draw actual GPU particles.
            var preview = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(all[0]));
            try
            {
                var effect = preview.GetComponent<OriginalAttackStageVfx>();
                var tuning = SharedSettingsRuntime.Load<PlayerTuningProfile>();
                var stage = tuning.attackProfile.GetStage(0);
                effect.Begin();
                for (int i = 1; i <= 12; i++) effect.Sample(stage, i / 20f, 0, Vector3.forward, .016f);
                var gpu = effect.Emitters.Single();
                if (!Alive(gpu)) throw new Exception("Prefab Mode preview did not produce GPU particles.");
                effect.StopImmediately();
            }
            finally { PrefabUtility.UnloadPrefabContents(preview); }
            SessionState.SetBool(Key + "Pending", true);
            SessionState.SetBool(Key + "Passed", false);
            EditorSceneManager.OpenScene(PlayerActionTestArenaSetup.ScenePath);
            EditorApplication.isPlaying = true;
        }
        catch (Exception e) { Debug.LogException(e); File.WriteAllText(Output + "/report.txt", "FAILED " + e); EditorApplication.Exit(1); }
    }
    static void StateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key + "Pending", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            results.Clear(); results.Add("PASS Three distinct prefabs with valid stage assignments and no gameplay colliders");
            results.Add("PASS Prefab Mode preview simulates the actual GPU particles");
            profile = SharedSettingsRuntime.Load<MeleeVisualProfile>();
            deadline = EditorApplication.timeSinceStartup + 180; frame = 0;
            routines.Clear(); routines.Push(Checks()); EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick;
            bool passed = SessionState.GetBool(Key + "Passed", false);
            try
            {
                if (passed)
                {
                    string path = SessionState.GetString(Key + "Path0", "");
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    var saved = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    Check(Mathf.Approximately(saved.GetComponentInChildren<AttackTrailGPU>().baseWidth, .91f) && saved.transform.Find("Added during Play Mode"), "Prefab particle values and added children survive Play Mode exit and asset reimport");
                }
            }
            catch (Exception e) { passed = false; results.Add(e.ToString()); }
            finally
            {
                for (int i = 0; i < 3; i++)
                    File.WriteAllText(SessionState.GetString(Key + "Path" + i, ""), SessionState.GetString(Key + "Original" + i, ""));
                AssetDatabase.Refresh();
                SessionState.SetBool(Key + "Pending", false);
            }
            File.WriteAllText(Output + "/report.txt", (passed ? "PASSED" : "FAILED") + "\n" + string.Join("\n", results));
            Debug.Log("ORIGINAL STAGE PREFAB VALIDATION " + (passed ? "PASSED" : "FAILED"));
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || routines.Count == 0 || Time.frameCount < frame) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Validation timed out.");
            while (routines.Count > 0)
            {
                var current = routines.Peek();
                if (!current.MoveNext()) { routines.Pop(); continue; }
                if (current.Current is IEnumerator nested) { routines.Push(nested); continue; }
                frame = Time.frameCount + 1; return;
            }
            Finish(true);
        }
        catch (Exception e) { results.Add(e.ToString()); Finish(false); }
    }
    static void Finish(bool passed)
    {
        routines.Clear(); EditorApplication.update -= Tick;
        File.WriteAllText(Output + "/report.txt", (passed ? "PASSED" : "FAILED") + "\n" + string.Join("\n", results));
        SessionState.SetBool(Key + "Passed", passed); EditorApplication.isPlaying = false;
    }
    static void Check(bool condition, string label)
    { if (!condition) throw new Exception(label); results.Add("PASS " + label); }
    static IEnumerator Until(Func<bool> predicate, string label)
    {
        float end = Time.time + 35;
        while (!predicate() && Time.time < end) yield return null;
        if (!predicate()) throw new Exception("Timed out: " + label);
    }
    static ComputeBuffer Buffer(AttackTrailGPU trail) => (ComputeBuffer)typeof(AttackTrailGPU).GetField("_particles", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(trail);
    static bool Alive(AttackTrailGPU trail)
    {
        var buffer = Buffer(trail); if (buffer == null) return false;
        var p = new Particle[buffer.count]; buffer.GetData(p);
        return p.Any(x => x.state != 0 && x.life < x.maxLife);
    }
    static OriginalAttackStageVfx Effect(PlayerControllerScript actor, int stage) => actor.GetComponentsInChildren<OriginalAttackStageVfx>(true).FirstOrDefault(e => (int)e.stage == stage);
    static IEnumerator Checks()
    {
        PlayerDemoGallery gallery = null;
        yield return Until(() => (gallery = Object.FindFirstObjectByType<PlayerDemoGallery>()) && gallery.IsPlaying, "gallery startup");
        var solo = gallery.Demos[2].Primary;
        var stages = new bool[3];
        Quaternion thrustRotation = Quaternion.identity;
        bool sampledThrust = false, sampledTail = false;
        float tailTurn = 0;
        yield return Until(() =>
        {
            var thrust = Effect(solo, 0);
            var sweep = Effect(solo, 1);
            if (thrust && thrust.IsEmitting)
            { thrustRotation = thrust.transform.rotation; sampledThrust = true; }
            else if (sampledThrust && thrust && thrust.IsRunning && sweep && sweep.IsEmitting)
            { sampledTail = true; tailTurn = Mathf.Max(tailTurn, Quaternion.Angle(thrustRotation, thrust.transform.rotation)); }
            for (int i = 0; i < 3; i++)
            {
                var effect = Effect(solo, i);
                if (effect && effect.IsRunning && (i == 2 || effect.Emitters.Any(Alive))) stages[i] = true;
            }
            return stages.All(v => v);
        }, "three real stage prefabs");
        Check(stages.All(v => v), "Solo combo runs all three assigned prefabs, including real GPU emission for Thrust and Sweep");
        Check(sampledTail && tailTurn < .1f, "Thrust retains its final rotation while Sweep arcs through the next stage");
        Check(solo.GetComponent<AttackTrailGPU>().MeleeVisualSuppressed, "Player-root legacy trail is suppressed while stage prefabs supply melee particles");
        Check(Effect(solo, 0).Emitters[0].IsStagePrefabEmitter && !Effect(solo, 0).Emitters[0].UseSharedSettings, "GPU values belong to each stage prefab");
        Check(!Effect(solo, 2).GetComponentInChildren<ParticleSystem>(true).gameObject.activeSelf, "Optional Repulsor particles are disabled by default, preserving the original look");
        Capture("gallery-original-prefabs");
        float sweepWidth = profile.originalSweepPrefab.GetComponentInChildren<AttackTrailGPU>().baseWidth;
        var authored = profile.originalThrustPrefab.GetComponentInChildren<AttackTrailGPU>();
        float thrustWidth = authored.baseWidth;
        Undo.IncrementCurrentGroup();
        using (var data = new SerializedObject(authored))
        {
            data.FindProperty("baseWidth").floatValue = .91f;
            data.ApplyModifiedProperties();
        }
        EditorUtility.SetDirty(authored); PrefabUtility.SavePrefabAsset(authored.transform.root.gameObject);
        OriginalAttackStageVfx.InvalidateContent();
        string thrustPath = AssetDatabase.GetAssetPath(profile.originalThrustPrefab);
        var contents = PrefabUtility.LoadPrefabContents(thrustPath);
        try
        {
            var layer = new GameObject("Added during Play Mode"); layer.transform.SetParent(contents.transform, false);
            var ps = layer.AddComponent<ParticleSystem>();
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
            var main = ps.main; main.playOnAwake = false; main.startLifetime = 1f; main.startSpeed = .3f; main.startSize = .08f;
            var emission = ps.emission; emission.rateOverTime = 600;
            PrefabUtility.SaveAsPrefabAsset(contents, thrustPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
        AssetDatabase.ImportAsset(thrustPath, ImportAssetOptions.ForceUpdate);
        yield return Until(() =>
        {
            var effect = Effect(solo, 0); if (!effect || !effect.IsEmitting) return false;
            var ps = effect.transform.Find("Added during Play Mode")?.GetComponent<ParticleSystem>();
            return ps && ps.particleCount > 0 && Mathf.Approximately(effect.Emitters[0].Effective_baseWidth, .91f);
        }, "live saved prefab update and new particle layer");
        Check(Mathf.Approximately(profile.originalSweepPrefab.GetComponentInChildren<AttackTrailGPU>().baseWidth, sweepWidth), "Thrust prefab edits do not change Sweep tuning");
        Check(true, "Saving a prefab during Play Mode refreshes its GPU parameters and plays newly added Particle System children");
        Capture("gallery-added-particles");
        Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
        Check(Mathf.Approximately(profile.originalThrustPrefab.GetComponentInChildren<AttackTrailGPU>().baseWidth, thrustWidth), "Undo restores the prefab's previous particle width");
        Undo.PerformRedo();
        var redone = profile.originalThrustPrefab.GetComponentInChildren<AttackTrailGPU>();
        PrefabUtility.SavePrefabAsset(redone.transform.root.gameObject);
        Check(Mathf.Approximately(redone.baseWidth, .91f), "Redo restores and saves the edited prefab particle width");
        string repulsorPath = AssetDatabase.GetAssetPath(profile.originalRepulsorPrefab);
        contents = PrefabUtility.LoadPrefabContents(repulsorPath);
        try
        {
            contents.GetComponentInChildren<ParticleSystem>(true).gameObject.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(contents, repulsorPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
        AssetDatabase.ImportAsset(repulsorPath, ImportAssetOptions.ForceUpdate);
        yield return Until(() =>
        {
            var effect = Effect(solo, 2);
            var ps = effect ? effect.GetComponentInChildren<ParticleSystem>() : null;
            return ps && ps.particleCount > 0;
        }, "enabled Repulsor particle layer");
        Check(Effect(solo, 2).GetComponentInChildren<ParticleSystemRenderer>().sharedMaterial.shader.isSupported, "Repulsor's optional Particle System plays with a supported material when enabled");
        Capture("gallery-repulsor-layer");
        yield return Until(() => gallery.Demos.All(d => d.SuccessfulLoops >= 2 || d.LastFailure != null), "seven gameplay demos");
        Check(gallery.Demos.All(d => d.SuccessfulLoops >= 2 && d.LastFailure == null), "All seven demos continue to pass with stage-prefab visuals");
        using (var data = new SerializedObject(profile)) { data.FindProperty("visualStyle").intValue = 2; SharedSettingsEditing.Apply(data); }
        yield return null; yield return null;
        Check(Object.FindObjectsByType<OriginalAttackStageVfx>(FindObjectsSortMode.None).Length == 0, "Off style stops all stage-prefab effects and tails");
        using (var data = new SerializedObject(profile)) { data.FindProperty("visualStyle").intValue = 0; SharedSettingsEditing.Apply(data); }
        yield return Until(() => Effect(solo, 0) && Effect(solo, 0).IsEmitting, "Original Particles restored");
        solo.attackController.CancelAttack(); yield return null;
        Check(solo.GetComponentsInChildren<OriginalAttackStageVfx>(true).All(e => !e.IsRunning), "Cancellation clears the player's stage effects");
        var lab = Object.FindFirstObjectByType<PlayerActionTestArena>();
        lab.SetMode(PlayerActionTestArena.TestMode.Manual); yield return null; yield return null;
        Check(Object.FindObjectsByType<OriginalAttackStageVfx>(FindObjectsSortMode.None).Length == 0, "Stopping the gallery cleans up its stage effects");
    }
    static void Capture(string name)
    {
        var camera = Camera.main; if (!camera) return;
        foreach (var text in Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None)) text.ForceMeshUpdate();
        var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
        var target = RenderTexture.GetTemporary(1600, 900, 24); var texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); texture.Apply();
            File.WriteAllBytes(Output + "/" + name + ".png", texture.EncodeToPNG());
        }
        finally { camera.targetTexture = oldTarget; RenderTexture.active = oldActive; RenderTexture.ReleaseTemporary(target); Object.Destroy(texture); }
    }
}
#endif
