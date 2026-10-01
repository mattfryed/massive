#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Massive.Demonstrations;
using Massive.Enemies;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class EnemyEncounterValidation
{
    private const string Key = "EnemyEncounterValidation.Run", Folder = "Library/EnemyEncounterValidation";
    private static IEnumerator routine;
    private static int frame;
    private static float resume;
    private static double deadline;
    private static readonly List<string> results = new(), errors = new();
    private static readonly List<Object> owned = new();
    static EnemyEncounterValidation()
    {
        EditorApplication.playModeStateChanged += State;
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(Folder + "/run.request") || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Folder + "/run.request"); Run();
        };
    }
    [MenuItem("MASSIVE/Demonstrations/Validate Encounter Timeline")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        Directory.CreateDirectory(Folder); File.WriteAllText(Folder + "/report.txt", "RUNNING\n");
        SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
    }
    private static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            frame = 0; resume = 0; results.Clear(); errors.Clear(); owned.Clear(); deadline = EditorApplication.timeSinceStartup + 240;
            SessionState.SetBool(Key + "Background", Application.runInBackground); Application.runInBackground = true;
            Application.logMessageReceived += Log; routine = Checks(); EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        { Application.runInBackground = SessionState.GetBool(Key + "Background", true); SessionState.SetBool(Key, false); }
    }
    private static void Log(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || routine == null || Time.frameCount <= frame || Time.time < resume) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Validation timed out");
            if (routine.MoveNext()) { frame = Time.frameCount; resume = Time.time + (routine.Current is float f ? f : 0); }
            else Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log; routine = null;
        bool pass = error == null && errors.Count == 0;
        File.WriteAllText(Folder + "/report.txt", (pass ? "PASSED" : "FAILED") + "\n" + string.Join("\n", results) + "\n" + error + "\n" + string.Join("\n", errors));
        Debug.Log("[Encounter validation] " + (pass ? "PASSED" : "FAILED: " + error));
        foreach (var obj in owned) if (obj) Object.Destroy(obj);
        Time.timeScale = 1; EditorApplication.isPlaying = false;
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        results.Add("PASS " + message); File.WriteAllText(Folder + "/report.txt", "RUNNING\n" + string.Join("\n", results));
    }
    private static T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    private static void Step(EnemyDirector director, float seconds)
    {
        var method = typeof(EnemyDirector).GetMethod("TickTimeline", BindingFlags.Instance | BindingFlags.NonPublic);
        for (float time = 0; time < seconds - .001f; time += .05f) method.Invoke(director, new object[] { Mathf.Min(.05f, seconds - time) });
    }
    private static IEnumerator Checks()
    {
        var lab = Object.FindFirstObjectByType<EnemyLab>();
        Check(lab && lab.timelinePreview, "Scene contains the new preview and original columns");
        lab.mode = EnemyLab.LabMode.EncounterTimeline;
        var preview = lab.timelinePreview;
        while (!preview.isActiveAndEnabled || preview.Players.Count != 2) yield return null;
        preview.director.timelinePaused = true; preview.actorsMove = preview.actorsAttack = false;
        preview.loop = false; yield return .1f;
        var root = Own(new GameObject("Encounter validation fixture")); root.SetActive(false);
        var d = root.AddComponent<EnemyDirector>(); d.enabled = false;
        d.arenaBounds = preview.layout.arena; d.arenaLayout = preview.layout; d.enemyRoot = root.transform;
        d.spawnProfile = Own(Object.Instantiate(preview.director.spawnProfile)); d.spawnBlockMask = LayerMask.GetMask("Obstacle");
        d.borderBufferWorld = .2f; d.spawnCheckRadiusWorld = .55f; d.ConfigureDemonstration(root.transform);
        var timeline = Own(Object.Instantiate(preview.director.encounterTimeline)); d.encounterTimeline = timeline; d.previewCue = 0;
        root.SetActive(true); d.RestartTimeline();
        var selected = d.ChooseFormation(0, out var mirror);
        Check(d.ChooseFormation(0, out var mirrorAgain) == selected && mirror == mirrorAgain, "Seed repeats the same variant and mirror");
        var slot = selected.slots[0];
        Check(d.arenaLayout.Resolve(slot, false, out var pose, out _) && d.arenaLayout.Resolve(slot, true, out var reflected, out _)
            && Mathf.Abs(pose.position.x + reflected.position.x) < .01f, "Mirror produces opposite arena coordinates");
        var centralSlot = new EnemyFormation.Slot { enemy = slot.enemy, telegraph = slot.telegraph, position = new Vector2(.5f, .5f) };
        preview.centerObstacle.SetActive(true); Physics.SyncTransforms();
        d.arenaLayout.Resolve(centralSlot, false, out var centerPose, out _);
        Check(!d.arenaLayout.Clear(centerPose, .55f, .2f, out var exclusionReason) && exclusionReason.StartsWith("Exclusion"), "Central exclusion rejects a formation footprint");
        preview.centerObstacle.SetActive(false);
        d.spawnProfile.maxAliveTotal = 5; Step(d, 7);
        Check(d.TotalSpawned == 0 && d.TimelinePendingCount == 0 && d.CueStates[0].state == "Skipped", "A six-member formation cannot partially reserve a five-enemy cap; it expires");
        d.spawnProfile.maxAliveTotal = 40; timeline.maxPressure = 5; d.RestartTimeline(); Step(d, 7);
        Check(d.TotalSpawned == 0 && d.CueStates[0].reason == "Pressure budget", "Pressure limits account for the entire formation");
        timeline.maxPressure = 60; d.RestartTimeline(); Step(d, 1.1f);
        Check(d.TimelinePendingCount == 6 && d.ActiveTelegraphCount >= 6 && d.TotalSpawned == 0, "Paired rows reserve all six positions before arrival");
        var warning = d.GetComponentInChildren<EnemySpawnTelegraph>(); var announced = warning.transform.position;
        float age = d.GameplayAge, warningAge = warning.Age; d.timelinePaused = true; Step(d, 2);
        Check(Mathf.Approximately(d.GameplayAge, age) && Mathf.Approximately(warning.Age, warningAge), "Pause holds timeline and warning clocks");
        d.timelinePaused = false;
        timeline.maxPressure = 6;
        var canSpawn = typeof(EnemyDirector).GetMethod("CanSpawnEnemy", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(!(bool)canSpawn.Invoke(d, new object[] { slot.enemy, 0, null }), "Carrier child launch gate respects pressure already reserved by warnings");
        timeline.maxPressure = 60;
        var obstacle = Own(GameObject.CreatePrimitive(PrimitiveType.Cube)); obstacle.name = "Blocked announced slot";
        obstacle.layer = LayerMask.NameToLayer("Obstacle"); obstacle.transform.position = pose.position; obstacle.transform.localScale = Vector3.one;
        Physics.SyncTransforms(); Step(d, 2.1f);
        Check(d.TotalSpawned == 0 && d.TimelinePendingCount == 6 && warning.transform.position == announced, "A blocked announced slot holds both rows without relocation");
        Step(d, 3.1f);
        Check(d.TotalSpawned == 0 && d.TimelinePendingCount == 0 && d.CueStates[0].state == "Expired", "A blocked formation expires without releasing a backlog");
        obstacle.SetActive(false); d.RestartTimeline(); Step(d, 3.1f); yield return .1f;
        Check(d.TotalSpawned == 6 && d.CueStates[0].spawned == 6, "Unblocked paired rows arrive together");
        var entered = root.GetComponentsInChildren<EnemyFormationEntry>();
        Check(entered.Length == 6, "Every row member has a coordinated entrance");
        yield return 1.8f;
        Check(entered.All(e => !e.enabled && !e.GetComponent<EnemyBase>().HoldPosition), "Coordinated entrance hands movement back to AI");
        d.RestartTimeline(true); yield return .1f;
        Check(d.AliveCount == 0 && d.TimelinePendingCount == 0, "Restart releases population, pending positions and socket claims");
        var fast = Own(ScriptableObject.CreateInstance<EnemyEncounterTimeline>());
        var shortWarning = Own(Object.Instantiate(selected)); shortWarning.warningSeconds = .1f;
        fast.cues.Add(new EnemyEncounterTimeline.Cue { label = "Short warning", arrivalSeconds = .15f, formation = shortWarning, allowedLateness = 2 });
        d.encounterTimeline = fast; d.previewCue = -1; d.RestartTimeline();
        var tickMethod = typeof(EnemyDirector).GetMethod("TickTimeline", BindingFlags.Instance | BindingFlags.NonPublic);
        tickMethod.Invoke(d, new object[] { .3f });
        Check(d.TimelinePendingCount == 6 && d.TotalSpawned == 0, "A frame longer than the warning lead cannot spawn an unannounced formation");
        tickMethod.Invoke(d, new object[] { .05f });
        Check(d.TotalSpawned == 0, "Short warning remains visible for its full duration");
        tickMethod.Invoke(d, new object[] { .06f });
        Check(d.TotalSpawned == 6, "Short-warning arrival releases after its full lead");
        d.RestartTimeline(true); Object.Destroy(root); yield return .1f;

        var actual = preview.director;
        preview.preset = EnemyEncounterLab.LayoutPreset.Open; actual.previewCue = 4;
        actual.timelinePaused = false; preview.RestartPreview();
        while (actual.GameplayAge < 6.2f) yield return null;
        Check(actual.CueStates[0].state == "Complete" && actual.CueStates[0].spawned == 2,
            "Top/bottom turret mounts are valid: " + actual.CueStates[0].state + " " + actual.CueStates[0].reason);
        preview.preset = EnemyEncounterLab.LayoutPreset.SideWallMounts; actual.previewCue = 4;
        actual.timelinePaused = false; preview.RestartPreview();
        while (actual.GameplayAge < 4) yield return null;
        Check(actual.CueStates[0].state == "Complete" && actual.CueStates[0].spawned == 2, "Side-wall layout selects the approved corner-turret fallback");
        var turrets = actual.enemyRoot.GetComponentsInChildren<ParticleBeamTurretController>();
        Check(turrets.Length == 2 && turrets.All(t => t.mount == ParticleBeamTurretController.WallMount.Authored && Mathf.Abs(Mathf.Abs(t.transform.position.x) - 14) < .05f), "Turrets retain their side-wall mounting poses after Start");
        Check(turrets.All(t => Vector3.Dot(t.transform.forward, -Mathf.Sign(t.transform.position.x) * Vector3.right) > .99f), "Side turrets face inward");
        preview.preset = EnemyEncounterLab.LayoutPreset.BlockedCenter; actual.previewCue = -1;
        preview.actorsMove = preview.actorsAttack = true; preview.RestartPreview();
        var seen = new HashSet<string>();
        actual.TimelineEnemySpawned += enemy => seen.Add(enemy.Definition.id);
        Time.timeScale = 2;
        while (actual.GameplayAge < 80)
        {
            foreach (var enemy in actual.enemyRoot.GetComponentsInChildren<EnemyBase>())
            {
                CheckScope(enemy, preview);
                if (enemy.TryGetComponent<CarrierController>(out var carrier) && carrier.TotalLaunched > 0) seen.Add("Carrier children");
            }
            yield return .4f;
        }
        File.WriteAllText(Folder + "/timeline.txt", string.Join("\n", actual.CueStates.Select(c => c.label + ": " + c.state + ", spawned " + c.spawned + ", " + c.reason)));
        Check(seen.Count >= 7, "All six enemy types and Carrier-launched children ran in the shared-arena timeline: " + string.Join(", ", seen));
        Check(actual.CurrentPressure <= actual.encounterTimeline.maxPressure && actual.AliveCount <= actual.spawnProfile.maxAliveTotal, "Runtime pressure and population stay within limits");
        Check(actual.CueStates.All(c => c.finished), "Every sample cue resolves; none remain queued");
        File.WriteAllText(Folder + "/timeline.txt", string.Join("\n", actual.CueStates.Select(c => c.label + ": " + c.state + ", spawned " + c.spawned + ", " + c.reason)));
        preview.loop = true;
        while (preview.CompletedLoops < 1) yield return null;
        Check(actual.GameplayAge < 3 && preview.Players.Count == 2, "Preview loops from the beginning with fresh actors");
        Time.timeScale = 1;
        lab.mode = EnemyLab.LabMode.Columns; yield return 4f;
        Check(!preview.gameObject.activeSelf && lab.columns.All(c => c.Enemy && c.Player && c.Enemy.SharesSimulationWith(c.Player)), "The original six-column showcase still starts and retains targeting isolation");
        Check(lab.columns.All(c => lab.columns.Where(other => other != c).All(other => !c.Enemy.SharesSimulationWith(other.Player))), "Columns reject every other column's player");
        Check(errors.Count == 0, "No Unity errors or exceptions during validation");
    }
    private static void CheckScope(EnemyBase enemy, EnemyEncounterLab preview)
    {
        if (!enemy || enemy.IsDead) return;
        if (preview.Players.Any(p => !enemy.SharesSimulationWith(p))) throw new Exception("Preview enemy rejected a player in its shared simulation");
        foreach (var real in PlayerControllerScript.ActivePlayers)
            if (real && !real.IsPseudoPlayer && enemy.SharesSimulationWith(real)) throw new Exception("Preview enemy can target the live roster");
    }
}
#endif
