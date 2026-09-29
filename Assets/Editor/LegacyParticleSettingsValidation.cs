#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Massive.Demonstrations;
using Massive.EditorTools;
using Massive.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Isolated Play Mode validation of persistent legacy GPU particle editing.</summary>
[InitializeOnLoad]
public static class LegacyParticleSettingsValidation
{
    const string Key = "MASSIVE.LegacyParticlesValidation.";
    const string AssetPath = "Assets/Scripts/Settings/Resources/MeleeVisualProfile.asset";
    const string Output = "Library/LegacyParticleSettingsValidation/report.txt";
    static readonly Stack<IEnumerator> routines = new();
    static int frame, passed;
    static double deadline;
    static readonly List<string> results = new();
    static MeleeVisualProfile profile;
    struct Particle { public Vector3 position; public Vector2 velocity; public float u, life, maxLife, size; public uint state; }

    static LegacyParticleSettingsValidation()
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
            profile = AssetDatabase.LoadAssetAtPath<MeleeVisualProfile>(AssetPath);
            SessionState.SetString(Key + "Original", EditorJsonUtility.ToJson(profile));
            SessionState.SetBool(Key + "Pending", true);
            EditorSceneManager.OpenScene(PlayerActionTestArenaSetup.ScenePath);
            EditorApplication.isPlaying = true;
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void StateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key + "Pending", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            passed = 0; results.Clear(); frame = 0; deadline = EditorApplication.timeSinceStartup + 120;
            profile = SharedSettingsRuntime.Load<MeleeVisualProfile>();
            routines.Clear(); routines.Push(Checks()); EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick;
            bool success = SessionState.GetBool(Key + "Passed", false);
            try
            {
                if (success)
                {
                    SharedSettingsRuntime.Reload();
                    AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
                    var persisted = SharedSettingsRuntime.Load<MeleeVisualProfile>().legacyParticles;
                    if (persisted.particleCount != 257 || Mathf.Abs(persisted.baseWidth - .71f) > .0001f)
                        throw new Exception("Particle edits did not survive Play Mode exit and asset reload.");
                    File.AppendAllText(Output, "\nPASS Saved particle edits survived Play Mode exit, Resources cache reset and asset reimport.\n");
                }
            }
            catch (Exception e) { success = false; File.AppendAllText(Output, "\nFAILED persistence: " + e); }
            finally
            {
                var restored = AssetDatabase.LoadAssetAtPath<MeleeVisualProfile>(AssetPath);
                EditorJsonUtility.FromJsonOverwrite(SessionState.GetString(Key + "Original", ""), restored);
                EditorUtility.SetDirty(restored); AssetDatabase.SaveAssetIfDirty(restored);
                SessionState.SetBool(Key + "Pending", false);
            }
            Debug.Log("LEGACY PARTICLE SETTINGS VALIDATION " + (success ? "PASSED" : "FAILED"));
            EditorApplication.Exit(success ? 0 : 1);
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
                frame = Time.frameCount + 1;
                return;
            }
            Finish(true, passed + " Play Mode checks passed.");
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
    static void Finish(bool success, string report)
    {
        routines.Clear(); EditorApplication.update -= Tick;
        Directory.CreateDirectory(Path.GetDirectoryName(Output));
        File.WriteAllText(Output, (success ? "PASSED: " : "FAILED: ") + report + "\n" + string.Join("\n", results));
        SessionState.SetBool(Key + "Passed", success); EditorApplication.isPlaying = false;
    }
    static void Check(bool condition, string label)
    { if (!condition) throw new Exception(label); passed++; results.Add("PASS " + label); }
    static IEnumerator Until(Func<bool> predicate)
    {
        float end = Time.time + 20;
        while (!predicate() && Time.time < end) yield return null;
        if (!predicate()) throw new Exception("Timed out waiting for live gallery / GPU particles.");
    }
    static ComputeBuffer Buffer(AttackTrailGPU trail) => (ComputeBuffer)typeof(AttackTrailGPU)
        .GetField("_particles", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(trail);
    static void Edit(Action<SerializedObject> change)
    {
        using var data = new SerializedObject(profile);
        data.Update(); change(data); SharedSettingsEditing.Apply(data);
    }
    static IEnumerator Checks()
    {
        PlayerDemoGallery gallery = null;
        yield return Until(() => (gallery = Object.FindFirstObjectByType<PlayerDemoGallery>()) && gallery.IsPlaying);
        // Exercise the preserved fallback path; stage-prefab ownership has its own validator.
        Edit(d =>
        {
            d.FindProperty("originalThrustPrefab").objectReferenceValue = null;
            d.FindProperty("originalSweepPrefab").objectReferenceValue = null;
            d.FindProperty("originalRepulsorPrefab").objectReferenceValue = null;
        });
        yield return null; yield return null;
        var trails = Object.FindObjectsByType<AttackTrailGPU>(FindObjectsSortMode.None)
            .Where(t => t.GetComponent<PlayerControllerScript>() is { IsPseudoPlayer: true }).ToArray();
        Check(trails.Length == 13, $"All 13 gallery actors have the shared GPU trail consumer (found {trails.Length})");
        Check(trails.All(t => t.UseSharedSettings && t.SharedSettingsAsset == profile && t.SharedSettingsGroup == "legacyParticles"), "Every actor resolves the persistent melee profile");
        var locals = trails.Select(t => EditorJsonUtility.ToJson(t)).ToArray();
        Edit(d =>
        {
            d.FindProperty("visualStyle").intValue = 0;
            var p = d.FindProperty("legacyParticles");
            p.FindPropertyRelative("particleCount").intValue = 256;
            p.FindPropertyRelative("trailLength").floatValue = 1.37f;
            p.FindPropertyRelative("baseWidth").floatValue = .33f;
            p.FindPropertyRelative("tipWidth").floatValue = .07f;
            p.FindPropertyRelative("forwardSpeed").floatValue = .65f;
            p.FindPropertyRelative("trailDrag").floatValue = 2.1f;
            p.FindPropertyRelative("minLifetime").floatValue = 2f;
            p.FindPropertyRelative("maxLifetime").floatValue = 2f;
            p.FindPropertyRelative("sizeStart").floatValue = .12f;
            p.FindPropertyRelative("sizeEnd").floatValue = .12f;
            p.FindPropertyRelative("emissionRate").floatValue = 0;
        });
        yield return null; yield return null;
        Check(trails.All(t => Buffer(t).count == 256), "Particle count edits resize every running GPU buffer");
        Check(trails.All(t => Mathf.Approximately(t.Effective_trailLength, 1.37f) && Mathf.Approximately(t.Effective_baseWidth, .33f) &&
            Mathf.Approximately(t.Effective_tipWidth, .07f) && Mathf.Approximately(t.Effective_forwardSpeed, .65f) &&
            Mathf.Approximately(t.Effective_trailDrag, 2.1f) && t.Effective_minLifetime == 2 && t.Effective_maxLifetime == 2 &&
            Mathf.Approximately(t.Effective_sizeStart, .12f) && Mathf.Approximately(t.Effective_sizeEnd, .12f) && t.Effective_emissionRate == 0), "All eleven settings propagate to every actor");
        Check(trails.Select((t, i) => EditorJsonUtility.ToJson(t) == locals[i]).All(v => v), "Editing shared values preserves all local fields and scene bindings");
        var particles = new Particle[256];
        Buffer(trails[0]).GetData(particles);
        Check(particles.All(p => p.state == 0), "Zero shared emission reaches the GPU simulation");
        var local = trails[0]; int oldCount = local.particleCount;
        local.UseSharedSettings = false; local.particleCount = 17;
        yield return null; yield return null;
        Check(Buffer(local).count == 17 && local.Effective_trailLength == local.trailLength, "Per-instance opt-out restores authored values and local capacity");
        local.particleCount = oldCount; local.UseSharedSettings = true;
        Edit(d => d.FindProperty("sharedEnabled").boolValue = false);
        yield return null; yield return null;
        Check(trails.All(t => Buffer(t).count == t.particleCount && t.Effective_baseWidth == t.baseWidth), "Profile-wide off restores local values without overwriting them");
        Edit(d => d.FindProperty("sharedEnabled").boolValue = true);
        yield return null; yield return null;
        Check(trails.All(t => Buffer(t).count == 256), "Re-enabling shared tuning restores shared capacity");
        var unchanged = Buffer(local);
        Undo.IncrementCurrentGroup();
        Edit(d => d.FindProperty("legacyParticles.baseWidth").floatValue = .71f);
        Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
        Check(Mathf.Approximately(profile.legacyParticles.baseWidth, .33f), "Shared particle edit supports Undo");
        Undo.PerformRedo(); SharedSettingsEditing.Save(profile);
        Check(Mathf.Approximately(profile.legacyParticles.baseWidth, .71f), "Shared particle edit supports Redo and saving");
        yield return null;
        Check(ReferenceEquals(unchanged, Buffer(local)), "Shape-only changes reuse the GPU buffer");
        Edit(d => { d.FindProperty("legacyParticles.particleCount").intValue = 257; d.FindProperty("legacyParticles.emissionRate").floatValue = 15000; });
        yield return null; yield return null;
        var solo = gallery.Demos[2].Primary.GetComponent<AttackTrailGPU>();
        particles = new Particle[257];
        yield return Until(() => { Buffer(solo).GetData(particles); return particles.Any(p => p.state != 0 && p.life < p.maxLife); });
        float size = .12f * Massive.Player.PlayerScaleAdjuster.SizeOf(solo);
        Check(particles.Where(p => p.state != 0 && p.life < p.maxLife).All(p => Mathf.Approximately(p.maxLife, 2f) && Mathf.Abs(p.size - size) < .0001f), "Shared lifetime and size reach actual GPU particles");
        Check(Buffer(solo).count == 257, "Non-thread-multiple capacity resizes and dispatches safely");
        var parent = new GameObject("Future spawn validation"); parent.SetActive(false); parent.transform.position = Vector3.forward * 40;
        var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerActor.prefab"), parent.transform);
        go.GetComponent<PlayerControllerScript>().ConfigureDemonstration(parent.transform, 0, 1);
        parent.SetActive(true); yield return null; yield return null;
        Check(Buffer(go.GetComponent<AttackTrailGPU>()).count == 257 && Mathf.Approximately(go.GetComponent<AttackTrailGPU>().Effective_baseWidth, .71f), "Newly spawned actors inherit saved tuning");
        Object.Destroy(parent);
        yield return Until(() => gallery.Demos.All(d => d.SuccessfulLoops >= 1 || d.LastFailure != null));
        Check(gallery.Demos.All(d => d.SuccessfulLoops >= 1 && d.LastFailure == null), "All seven gameplay demos continue during live particle edits");
        Check(File.ReadAllText(AssetPath).Contains("particleCount: 257"), "The shared editor save path writes the asset during Play Mode");
    }
}
#endif
