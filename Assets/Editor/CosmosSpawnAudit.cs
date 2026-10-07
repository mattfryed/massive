#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Massive.Enemies;
using Massive.Scoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Records spawn rejection evidence in the real level without editing authored assets.</summary>
[InitializeOnLoad]
public static class CosmosSpawnAudit
{
    const string Folder = "Library/CosmosSpawnAudit", Key = "CosmosSpawnAudit";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static IEnumerator routine;
    static readonly List<string> evidence = new(), errors = new();
    static readonly HashSet<string> seen = new();
    static double deadline;
    static int frame;
    static float originalScale;
    static bool originalBackground;
    static string prefix;
    static CosmosSpawnAudit()
    {
        EditorApplication.playModeStateChanged += State;
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(Folder + "/run.request") || EditorApplication.isPlayingOrWillChangePlaymode) return;
            string label = File.ReadAllText(Folder + "/run.request").Trim(); File.Delete(Folder + "/run.request");
            Run(label == "fixed" ? "fixed" : "baseline");
        };
    }
    [MenuItem("MASSIVE/COSMOS/Audit full encounter spawning")]
    public static void RunMenu() => Run("audit");
    static void Run(string label)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != "Assets/Scenes/S-9_COSMOS.unity")
            throw new InvalidOperationException("Open COSMOS in Edit Mode.");
        Directory.CreateDirectory(Folder); SessionState.SetString(Key + ".Label", label);
        SessionState.SetString(Key + ".SceneHash", Hash128.Compute(File.ReadAllText(SceneManager.GetActiveScene().path)).ToString());
        SessionState.SetBool(Key, true); File.WriteAllText(Folder + "/" + label + ".txt", "STARTING\n");
        EditorApplication.isPlaying = true;
    }
    static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            prefix = Folder + "/" + SessionState.GetString(Key + ".Label", "audit");
            evidence.Clear(); errors.Clear(); seen.Clear(); frame = -1;
            originalScale = Time.timeScale; originalBackground = Application.runInBackground;
            Application.runInBackground = true; deadline = EditorApplication.timeSinceStartup + 260;
            Application.logMessageReceived += Log; routine = Audit(); EditorApplication.update += Tick;
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
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Audit timed out");
            frame = Time.frameCount;
            if (!routine.MoveNext()) Finish(null);
        }
        catch (Exception e) { Finish(e); }
    }
    static void Note(string text)
    { evidence.Add(text); File.WriteAllText(prefix + ".txt", "RUNNING\n" + string.Join("\n", evidence)); }
    static void Finish(Exception error)
    {
        routine = null; EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        Time.timeScale = originalScale; Application.runInBackground = originalBackground;
        bool unchanged = Hash128.Compute(File.ReadAllText(SceneManager.GetActiveScene().path)).ToString() == SessionState.GetString(Key + ".SceneHash", "");
        File.WriteAllText(prefix + ".txt", (error == null ? "COMPLETE" : "FAILED") + "\n" + string.Join("\n", evidence)
            + "\nSaved scene unchanged: " + unchanged + "\n" + error + "\nConsole errors: " + errors.Count + "\n" + string.Join("\n", errors));
        Debug.Log("[COSMOS spawn audit] " + (error == null ? "COMPLETE" : "FAILED: " + error));
        EditorApplication.isPlaying = false;
    }
    static bool Clear(EnemyDirector director, EnemyFormation.Slot slot, Vector3 point, out string reason)
    {
        var type = typeof(EnemyDirector).GetNestedType("FormationMember", BindingFlags.NonPublic);
        var member = Activator.CreateInstance(type);
        type.GetField("slot").SetValue(member, slot);
        type.GetField("pose").SetValue(member, new EnemyArenaLayout.Pose { position = point, clearance = point, rotation = Quaternion.identity });
        object[] args = { member, null, null };
        bool clear = (bool)typeof(EnemyDirector).GetMethod("MemberClear", Private).Invoke(director, args);
        reason = args[2] as string; return clear;
    }
    static string Path(Transform t) => t.parent ? Path(t.parent) + "/" + t.name : t.name;
    static void Capture(EnemyDirector director)
    {
        foreach (var cue in director.CueStates)
        {
            var entries = new[] { $"cue {cue.index} {cue.label}: {cue.state}; {cue.Progress}; {cue.reason}" }
                .Concat(cue.slots.Where(s => !string.IsNullOrEmpty(s.reason)).Select(s => $"cue {cue.index} slot {s.index}: {s.state}; {s.reason}"));
            foreach (string entry in entries)
            {
                if (!seen.Add(entry)) continue;
                Note($"t={director.GameplayAge:F2} {entry}");
                int marker = entry.IndexOf("Collider: ", StringComparison.Ordinal);
                if (marker < 0) continue;
                string name = entry.Substring(marker + 10);
                foreach (var c in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Where(c => c.name == name))
                    Note($"  BLOCKER {Path(c.transform)} layer={LayerMask.LayerToName(c.gameObject.layer)} trigger={c.isTrigger} pos={c.bounds.center} components={string.Join(",", c.GetComponentsInParent<MonoBehaviour>().Where(b => b).Select(b => b.GetType().Name))}");
            }
        }
    }
    static IEnumerator Audit()
    {
        yield return null;
        var director = Object.FindObjectsByType<EnemyDirector>(FindObjectsSortMode.None).Single(d => d.encounterTimeline && d.arenaLayout && d.arenaLayout.arena.ovalOutline);
        var timeline = director.encounterTimeline;
        string originalTimeline = EditorJsonUtility.ToJson(timeline);
        director.timelinePaused = true;
        Note($"CONFIG {director.name}; mask={director.spawnBlockMask.value}; placementRegion={(director.placementRegion ? director.placementRegion.name : "none")}; overlap={director.arenaLayout.allowResonanceAndAmplifierOverlap}; seed={director.encounterSeed}; cues={timeline.cues.Count}");
        var template = timeline.cues.First(c => c.formation && c.formation.slots.Count > 0).formation.slots[0];
        Vector3 point = director.arenaLayout.World(new Vector2(0, .35f));
        if (!Clear(director, template, point, out var emptyReason)) throw new Exception("Probe point blocked: " + emptyReason);
        string[] paths = {
            "Assets/Power-ups/PU_Decoherence.prefab", "Assets/Power-ups/PU_MassNode.prefab",
            "Assets/Power-ups/PU_ParticleAccelerator.prefab", "Assets/Power-ups/PU_TimeDilation.prefab",
            "Assets/Prefabs/Matter nugget.prefab", "Assets/Scripts/Anomalies/ORBITAL/Orbital Mass Nugget.prefab",
            "Assets/Scripts/Anomalies/ORBITAL/Mass Nugglet.prefab"
        };
        foreach (string path in paths)
        {
            var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), point, Quaternion.identity);
            go.SetActive(true);
            if (go.TryGetComponent<MatterNuggetScript>(out var nugget)) nugget.Eject(point, Vector3.zero, 10);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = true;
            Physics.SyncTransforms();
            bool clear = Clear(director, template, point, out string reason);
            Note($"PROBE {go.name}: {(clear ? "ALLOWED" : "BLOCKED")} {reason}; colliders=" + string.Join(",", go.GetComponentsInChildren<Collider>().Select(c => $"{c.GetType().Name} layer={c.gameObject.layer} trigger={c.isTrigger} overlapAllowed={director.arenaLayout.AllowsSpawnOverlap(c)}")));
            Object.DestroyImmediate(go);
        }
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position = point; wall.layer = LayerMask.NameToLayer("Obstacle"); Physics.SyncTransforms();
        Note("CONTROL ordinary obstacle blocked: " + !Clear(director, template, point, out _));
        wall.layer = LayerMask.NameToLayer("NoSpawnZone"); Physics.SyncTransforms();
        Note("CONTROL no-spawn zone blocked: " + !Clear(director, template, point, out _)); Object.DestroyImmediate(wall);
        foreach (var player in PlayerControllerScript.ActivePlayers)
            Note("CONTROL player clearance blocked: " + !Clear(director, template, player.transform.position, out _));
        director.timelinePaused = false; Time.timeScale = 2;
        var score = Object.FindFirstObjectByType<MatchScoreService>();
        Note("FULL RUN: normal scene players, AI, power-up spawner and population policy; 2x time; no automatic kills, health refill or placement overrides.");
        bool started = false;
        while (director.GameplayAge < timeline.duration + 2)
        {
            Capture(director);
            if (director.GameplayAge > 1) started = true;
            if (started && score && !score.IsScoringOpen) { Note("Match closed at timeline age " + director.GameplayAge); break; }
            if (director.CueStates.Count > 0 && director.CueStates.All(c => c.finished) && director.GameplayAge > timeline.duration - 1) break;
            yield return null;
        }
        Capture(director);
        File.WriteAllText(prefix + "-summary.txt", string.Join("\n", director.CueStates.Select(c =>
            $"{c.arrival:F2}s {c.label}: {c.state}, {c.spawned}/{(c.formation ? c.formation.slots.Count : 0)} spawned; {c.reason}\n" +
            string.Join("\n", c.slots.Where(s => s.state != "Spawned").Select(s => $"  slot {s.index}: {s.state} {s.reason}")))));
        Note("Authored timeline unchanged: " + (originalTimeline == EditorJsonUtility.ToJson(timeline)));
    }
}
#endif
