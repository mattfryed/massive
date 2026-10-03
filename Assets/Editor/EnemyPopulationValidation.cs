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
public static class EnemyPopulationValidation
{
    private const string Key = "EnemyPopulationValidation.Run", Folder = "Library/EnemyPopulationValidation";
    private static IEnumerator routine;
    private static int frame;
    private static float resume;
    private static double deadline;
    private static readonly List<string> results = new(), errors = new();
    private static readonly List<Object> owned = new();
    static EnemyPopulationValidation()
    {
        EditorApplication.playModeStateChanged += State;
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(Folder + "/run.request") || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Folder + "/run.request"); Run();
        };
    }
    [MenuItem("MASSIVE/Demonstrations/Validate Timeline Population")]
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
        Debug.Log("[Timeline population validation] " + (pass ? "PASSED" : "FAILED: " + error));
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
        Check(lab && lab.timelinePreview, "Enemy Lab available");
        lab.mode = EnemyLab.LabMode.EncounterTimeline;
        var preview = lab.timelinePreview;
        while (!preview.isActiveAndEnabled || preview.Players.Count != 2) yield return null;
        preview.loop = preview.actorsMove = preview.actorsAttack = false;
        preview.director.timelinePaused = true; preview.ignorePopulationLimits = false;
        yield return .1f;
        var source = preview.director.encounterTimeline;
        string original = EditorJsonUtility.ToJson(source);
        var drone = Own(Object.Instantiate(source.cues[0].formation.slots[0].enemy)); drone.name = "ED_Drone";
        drone.maxAliveOverride = 1;
        var root = Own(new GameObject("Population fixture")); root.SetActive(false);
        var d = root.AddComponent<EnemyDirector>(); d.enabled = false;
        d.spawnProfile = Own(Object.Instantiate(preview.director.spawnProfile));
        d.spawnProfile.maxAliveTotal = d.spawnProfile.maxAliveMelee = d.spawnProfile.maxAliveRanged = 1;
        d.spawnProfile.rules.Clear(); d.spawnProfile.batches.Clear();
        d.arenaLayout = preview.layout; d.arenaBounds = preview.layout.arena;
        d.spawnBlockMask = LayerMask.GetMask("Obstacle"); d.borderBufferWorld = .2f; d.spawnCheckRadiusWorld = .55f;
        d.waitForScoring = false; d.ConfigureDemonstration(root.transform); d.enemyRoot = root.transform;
        var timeline = Own(ScriptableObject.CreateInstance<EnemyEncounterTimeline>());
        timeline.maxAliveTotal = 0; timeline.maxPressure = 0;
        d.encounterTimeline = timeline; root.SetActive(true);
        d.enabled = true; yield return null; d.enabled = false;
        var canSpawn = typeof(EnemyDirector).GetMethod("CanSpawnEnemy", BindingFlags.Instance | BindingFlags.NonPublic);
        bool Gate(object consuming = null) => (bool)canSpawn.Invoke(d, new object[] { drone, 1, consuming });
        EnemyBase Existing(EnemyDefinition definition)
        {
            var go = new GameObject("Existing enemy"); go.transform.SetParent(root.transform); go.transform.position = Vector3.one * 100;
            var enemy = go.AddComponent<EnemyBase>(); enemy.ConfigureDemonstration(root.transform, null);
            d.RegisterAuthoredEnemy(enemy, definition); return enemy;
        }
        Existing(drone); Existing(drone);
        Check(Gate(), "Timeline ignores lower legacy total, category, rule and definition caps");
        Check(!d.IgnoreTimelineBudgetsForPreview, "A demonstration scope alone cannot bypass budgets");
        timeline.maxAliveTotal = 2;
        Check(!Gate(), "Timeline total limit gates an additional Drone");
        var budget = d.CapturePopulationBudget(); budget.Add(timeline, drone, EnemyPopulationBudget.Kind.Requested);
        Check(!budget.Allows(timeline, out var reason) && reason == "Total limit 2: 2 alive + 0 reserved + 1 requested", "Total blocker explains live and requested counts");
        timeline.maxAliveTotal = 0;
        timeline.populationLimits.Add(new EnemyEncounterTimeline.PopulationLimit { enemy = drone, maxAlive = 2 });
        Check(!Gate() && !budget.Allows(timeline, out reason) && reason == "Drone limit 2: 2 alive + 0 reserved + 1 requested", "Per-enemy blocker names the enemy and exact counts");
        timeline.populationLimits[0].maxAlive = 0; timeline.maxPressure = 2;
        Check(!Gate() && !budget.Allows(timeline, out reason) && reason.StartsWith("Pressure limit 2:"), "Pressure is an independent optional gate");
        timeline.maxPressure = 0;
        Check(Gate(), "Zero limits disable budget gates without falling back to legacy caps");
        d.encounterTimeline = null;
        Check(!Gate(), "Legacy total limit remains active without a timeline");
        d.spawnProfile.maxAliveTotal = 0;
        Check(!Gate(), "Legacy per-enemy limit remains active without a timeline");
        drone.maxAliveOverride = 0;
        Check(!(bool)canSpawn.Invoke(d, new object[] { drone, 0, null }), "Legacy category limit remains active without a timeline");
        d.spawnProfile.maxAliveMelee = 0;
        Check((bool)canSpawn.Invoke(d, new object[] { drone, 0, null }), "Legacy uncapped spawning still works");
        drone.maxAliveOverride = 1; d.encounterTimeline = timeline; d.RestartTimeline(true); yield return .05f;

        var form = Own(ScriptableObject.CreateInstance<EnemyFormation>());
        form.integrity = EnemyFormation.Integrity.Flexible; form.warningSeconds = .25f;
        foreach (float x in new[] { .4f, .6f }) form.slots.Add(new EnemyFormation.Slot { enemy = drone,
            telegraph = source.cues[0].formation.slots[0].telegraph, position = new Vector2(x, .85f) });
        timeline.cues.Add(new EnemyEncounterTimeline.Cue { formation = form, arrivalSeconds = 1, allowedLateness = 2 });
        timeline.maxAliveTotal = 2; d.RestartTimeline(); Step(d, .8f);
        Check(d.TimelinePendingCount == 2 && d.CurrentPressure == 2, "Telegraphs reserve total and pressure before enemies exist");
        Check(!Gate(), "Carrier launch cannot consume capacity held by reservations");
        timeline.maxAliveTotal = 3; timeline.maxPressure = 3;
        d.enabled = true;
        bool launched = d.TryLaunchEnemy(drone, d.arenaLayout.World(Vector2.zero), Quaternion.identity, out var child);
        Check(launched && child && child.Definition == drone && d.AliveCount == 1 && d.TimelinePendingCount == 2,
            "Carrier launch API uses timeline authority despite the lower profile cap");
        child.HoldPosition = true; child.AttacksEnabled = false;
        Check(d.CurrentPressure == 3 && !Gate(), "Launched Carrier child is counted once alongside reserved arrivals");
        budget = d.CapturePopulationBudget(); budget.Add(timeline, drone, EnemyPopulationBudget.Kind.Requested);
        Check(!budget.Allows(timeline, out reason) && reason == "Total limit 3: 1 alive + 2 reserved + 1 requested", "Mixed live/reserved/requested diagnostics stay distinct");
        d.enabled = false; d.RestartTimeline(true); yield return .05f;
        Check(d.CapturePopulationBudget().Total.Total == 0 && d.CurrentPressure == 0, "Restart frees all reserved and living capacity");
        timeline.cues.Clear(); d.RestartTimeline();

        // A legacy reservation being consumed contributes once, even if the timeline was assigned afterwards.
        var reservationType = typeof(EnemyDirector).GetNestedType("ReservedSpawn", BindingFlags.NonPublic);
        var reservation = Activator.CreateInstance(reservationType, true);
        reservationType.GetField("enemy").SetValue(reservation, drone);
        var reservations = (IList)typeof(EnemyDirector).GetField("_reservedSpawns", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(d);
        reservations.Add(reservation); timeline.maxAliveTotal = 1; timeline.maxPressure = 1;
        Check(!Gate() && Gate(reservation), "Consuming a legacy reservation does not count it twice");
        Check(d.CurrentPressure == 1, "Pressure display includes outstanding legacy reservations");
        reservations.Clear();

        timeline.maxAliveTotal = 0; timeline.maxPressure = 0;
        for (int i = 0; i < 7; i++) Existing(drone);
        var carrierDef = source.pressureCosts.Select(c => c.enemy).First(e => e && e.prefab.GetComponent<CarrierController>());
        d.enabled = true; d.timelinePaused = true;
        var carrierObject = Object.Instantiate(carrierDef.prefab, d.arenaLayout.World(new Vector2(0, .3f)), Quaternion.identity, root.transform);
        var carrierEnemy = carrierObject.GetComponent<EnemyBase>(); d.RegisterAuthoredEnemy(carrierEnemy, carrierDef);
        carrierEnemy.ConfigureDemonstration(root.transform, null); carrierEnemy.AttacksEnabled = false; carrierEnemy.HoldPosition = true;
        var carrier = carrierObject.GetComponent<CarrierController>(); carrier.droneDefinition = drone; carrier.maxActiveDrones = 6;
        yield return .1f;
        d.timelinePaused = false; carrierEnemy.AttacksEnabled = true; Physics.SyncTransforms();
        var launch = typeof(CarrierController).GetMethod("TryLaunch", BindingFlags.Instance | BindingFlags.NonPublic);
        bool carrierReleased = (bool)launch.Invoke(carrier, new object[] { 0 });
        Check(carrierReleased && carrier.TotalLaunched == 1 && d.CapturePopulationBudget().For(drone).alive == 8,
            "Actual Carrier bypasses its local and definition caps in timeline mode");
        carrierEnemy.AttacksEnabled = false; d.enabled = false; d.RestartTimeline(true); yield return .05f;

        // Exercise the real Lab owner: unrestricted budgets must still honor geometry, warnings and pause.
        var live = preview.director;
        var unlimited = Own(ScriptableObject.CreateInstance<EnemyEncounterTimeline>());
        unlimited.maxAliveTotal = 1; unlimited.maxPressure = .5f;
        unlimited.populationLimits.Add(new EnemyEncounterTimeline.PopulationLimit { enemy = drone, maxAlive = 1 });
        unlimited.cues.Add(new EnemyEncounterTimeline.Cue { formation = form, arrivalSeconds = 1, allowedLateness = 2 });
        live.enabled = false; live.encounterTimeline = unlimited; live.previewCue = -1; preview.RestartPreview();
        live.timelinePaused = false; preview.ignorePopulationLimits = true;
        Check(live.IgnoreTimelineBudgetsForPreview, "Only an active Lab with its own session enables unlimited preview");
        var obstacle = Own(GameObject.CreatePrimitive(PrimitiveType.Cube)); obstacle.name = "Population validation obstruction";
        obstacle.layer = LayerMask.NameToLayer("Obstacle"); obstacle.transform.localScale = Vector3.one;
        preview.layout.Resolve(form.slots[0], false, out var blocked, out _); obstacle.transform.position = blocked.position;
        Physics.SyncTransforms(); Step(live, .8f);
        Check(live.TimelinePendingCount == 2 && live.TotalSpawned == 0, "Unlimited preview keeps full warning lead while bypassing all budgets");
        Step(live, .6f);
        Check(live.TotalSpawned == 1 && live.TimelinePendingCount == 1 && live.CueStates[0].slots[0].state == "Blocked",
            "Unlimited preview preserves collider checks and independent blocked-slot waiting");
        live.timelinePaused = true; float before = live.GameplayAge; Step(live, 1);
        Check(live.GameplayAge == before, "Unlimited preview preserves pause");
        Check(!(bool)canSpawn.Invoke(live, new object[] { drone, 0, null }), "Pause also gates Carrier launches during unlimited preview");
        live.timelinePaused = false; preview.ignorePopulationLimits = false;
        Check(!live.IgnoreTimelineBudgetsForPreview && !(bool)canSpawn.Invoke(live, new object[] { drone, 0, null }), "Disabling unlimited preview restores saved limits immediately");
        Check(live.AliveCount == 1, "Restoring limits does not destroy existing enemies");
        preview.ignorePopulationLimits = true; live.ConfigureDemonstration(root.transform);
        Check(!live.IgnoreTimelineBudgetsForPreview, "Lab toggle cannot bypass budgets in an unrelated simulation scope");
        preview.ignorePopulationLimits = false; obstacle.SetActive(false);
        Check(EditorJsonUtility.ToJson(source) == original, "Validation preserves all authored timeline settings and encounters");
    }
}
#endif
