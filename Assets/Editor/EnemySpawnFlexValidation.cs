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
public static class EnemySpawnFlexValidation
{
    private const string Key = "EnemySpawnFlexValidation.Run", Folder = "Library/EnemySpawnFlexValidation";
    private static IEnumerator routine;
    private static int frame;
    private static float resume;
    private static double deadline;
    private static readonly List<string> results = new(), errors = new();
    private static readonly List<Object> owned = new();
    static EnemySpawnFlexValidation()
    {
        EditorApplication.playModeStateChanged += State;
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(Folder + "/run.request") || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Folder + "/run.request"); Run();
        };
    }
    [MenuItem("MASSIVE/Demonstrations/Validate Flexible Spawning")]
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
        Debug.Log("[Flexible spawn validation] " + (pass ? "PASSED" : "FAILED: " + error));
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
        Check(lab && lab.timelinePreview, "Enemy Lab is available");
        lab.mode = EnemyLab.LabMode.EncounterTimeline;
        var preview = lab.timelinePreview;
        while (!preview.isActiveAndEnabled || preview.Players.Count != 2) yield return null;
        preview.loop = preview.actorsMove = preview.actorsAttack = false;
        preview.director.timelinePaused = true; preview.preset = EnemyEncounterLab.LayoutPreset.Open;
        yield return .1f;
        var authored = AssetDatabase.LoadAssetAtPath<EnemyEncounterTimeline>(EnemyEncounterLabSetup.TimelinePath);
        string savedTimeline = EditorJsonUtility.ToJson(authored);
        var source = AssetDatabase.LoadAssetAtPath<EnemyFormation>(EnemyEncounterLabSetup.Folder + "/01 Drone Paired Rows.asset");
        var definition = source.slots[0].enemy; var telegraph = source.slots[0].telegraph;
        var root = Own(new GameObject("Flexible spawn fixture")); root.SetActive(false);
        var d = root.AddComponent<EnemyDirector>(); d.enabled = false;
        d.arenaLayout = preview.layout; d.arenaBounds = preview.layout.arena;
        d.spawnProfile = Own(Object.Instantiate(preview.director.spawnProfile));
        d.spawnProfile.maxAliveTotal = 40; d.spawnProfile.maxAliveMelee = d.spawnProfile.maxAliveRanged = 40;
        d.spawnBlockMask = LayerMask.GetMask("Obstacle"); d.borderBufferWorld = .2f; d.spawnCheckRadiusWorld = .55f;
        d.waitForScoring = false; d.ConfigureDemonstration(root.transform); d.enemyRoot = root.transform;
        var timeline = Own(ScriptableObject.CreateInstance<EnemyEncounterTimeline>());
        d.encounterTimeline = timeline; d.previewCue = -1;
        d.TimelineEnemySpawned += e => { e.HoldPosition = true; e.AttacksEnabled = false; };
        root.SetActive(true);
        // Let Start initialize its clocks before the manually stepped fixtures, not midway through the live test.
        d.enabled = true; yield return null; d.enabled = false;
        var form = Own(ScriptableObject.CreateInstance<EnemyFormation>());
        form.integrity = EnemyFormation.Integrity.Flexible; form.warningSeconds = .25f; form.blockedSlotGrace = 1.5f;
        foreach (float x in new[] { .3f, .5f, .7f })
            form.slots.Add(new EnemyFormation.Slot { enemy = definition, telegraph = telegraph,
                position = new Vector2(x, .7f), releaseDelay = form.slots.Count * .25f });
        void Use(EnemyFormation value)
        {
            d.RestartTimeline(true);
            timeline.cues.Clear(); timeline.cues.Add(new EnemyEncounterTimeline.Cue { formation = value, arrivalSeconds = .5f, allowedLateness = 2 });
            timeline.maxPressure = 60; timeline.maxAliveTotal = 40;
            d.RestartTimeline();
        }
        Vector3 Point(EnemyFormation value, int index) { d.arenaLayout.Resolve(value.slots[index], false, out var pose, out _); return pose.position; }
        var obstacle = Own(GameObject.CreatePrimitive(PrimitiveType.Cube)); obstacle.name = "Single blocked slot";
        obstacle.layer = LayerMask.NameToLayer("Obstacle"); obstacle.transform.localScale = Vector3.one * .5f;
        void Block(Vector3 position) { obstacle.SetActive(true); obstacle.transform.position = position; Physics.SyncTransforms(); }
        EnemyBase Occupant(Vector3 position)
        {
            var go = new GameObject("Existing enemy blocker"); go.transform.SetParent(root.transform); go.transform.position = position;
            var e = go.AddComponent<EnemyBase>(); e.ConfigureDemonstration(root.transform, null);
            d.RegisterAuthoredEnemy(e, definition); return e;
        }

        Use(form); Block(Point(form, 0)); Step(d, 1.2f);
        Check(d.TotalSpawned == 2 && d.TimelinePendingCount == 1, "One blocked point leaves two independent slots free to spawn");
        Check(d.CueStates[0].Progress == "2 spawned, 1 waiting, 0 skipped", "Progress distinguishes waiting members from successful arrivals");
        Step(d, 1.5f);
        Check(d.CueStates[0].state == "Partial" && d.CueStates[0].skipped == 1 && d.TimelinePendingCount == 0,
            "Only the blocked slot expires and releases its reservation");
        Check(d.CurrentPressure == 2 && d.CueStates[0].slots[0].reason.Contains("Collider"), "Expired slots release pressure and retain their reason");

        Use(form); yield return .05f; Block(Point(form, 2)); Step(d, .85f);
        Check(d.TotalSpawned == 2, "A later blocked slot cannot delay earlier clear releases");
        form.integrity = EnemyFormation.Integrity.Strict;
        Use(form); yield return .05f; Step(d, 1.2f);
        Check(d.TotalSpawned == 0, "Strict mode still requires a complete clear formation");
        form.integrity = EnemyFormation.Integrity.Flexible;

        Use(form); yield return .05f;
        timeline.cues[0].overrideSpawnPolicy = true; timeline.cues[0].integrity = EnemyFormation.Integrity.Strict;
        timeline.cues[0].maxPositionAdjustment = 0;
        Step(d, 1.2f);
        Check(d.TotalSpawned == 0, "A cue can override a Flexible formation with Strict integrity");

        Use(form); yield return .05f; Block(Point(form, 0)); Step(d, 1.2f);
        obstacle.SetActive(false); Step(d, .1f);
        Check(d.TotalSpawned == 2, "A late-clearing slot still receives its complete warning");
        Step(d, .35f);
        Check(d.TotalSpawned == 3, "A late-clearing slot arrives after its warning within its grace window");

        obstacle.SetActive(false); Use(form); yield return .05f; Step(d, .26f);
        var first = d.CueStates[0].slots[0]; var announced = first.position;
        var warning = root.GetComponentsInChildren<EnemySpawnTelegraph>().First(w => Vector3.Distance(w.transform.position, announced) < .01f);
        Block(announced); float clock = d.GameplayAge, age = warning.Age;
        d.timelinePaused = true; Step(d, .8f);
        Check(d.GameplayAge == clock && warning.Age == age, "Pause freezes warning, retry and expiry clocks");
        d.timelinePaused = false; Step(d, 1.05f);
        Check(d.TotalSpawned == 2 && first.position == announced && warning.transform.position == announced,
            "An obstacle entering an announced slot holds only that slot and never moves its telegraph");
        obstacle.SetActive(false); Step(d, .16f);
        Check(d.TotalSpawned == 3 && first.state == "Spawned" && first.position == announced, "Clearing the obstruction releases the held slot at its warned position");

        Use(form); yield return .05f; Step(d, .26f); Block(Point(form, 0)); Step(d, .34f);
        obstacle.SetActive(false); Step(d, .51f);
        Check(d.TotalSpawned == 3, "A recovered blocked slot never shifts later clear slots off their scheduled cadence");

        form.maxPositionAdjustment = 1; form.adjustmentPattern = EnemyFormation.AdjustmentPattern.Rows;
        Use(form); yield return .05f; var blocker = Occupant(Point(form, 0) + Vector3.right * .65f); Step(d, .26f);
        var adjusted = d.CueStates[0].slots.Select(s => s.position).ToArray();
        Check(d.CueStates[0].slots.All(s => s.adjusted && Vector3.Distance(s.position, s.authored) <= 1.001f), "Nearby enemy produces bounded pre-warning adjustments");
        Vector3 offset = adjusted[0] - Point(form, 0);
        Check(d.CueStates[0].slots.All(s => Vector3.Distance(s.position - s.authored, offset) < .001f), "Row correction preserves spacing by translating the row together");
        Step(d, 1.1f); Check(d.TotalSpawned == 3, "Adjusted arrivals all spawn clear of the existing enemy");
        Use(form); yield return .05f; Occupant(Point(form, 0) + Vector3.right * .65f); Step(d, .26f);
        Check(d.CueStates[0].slots.Select(s => s.position).SequenceEqual(adjusted), "Identical blockers and authored slots choose identical corrections");

        form.maxPositionAdjustment = 0; Use(form); yield return .05f;
        timeline.maxAliveTotal = 2; Step(d, 3);
        Check(d.TotalSpawned == 0 && d.TimelinePendingCount == 0 && d.CueStates[0].reason.StartsWith("Total limit 2:"), "Flexible placement does not bypass population caps");
        Use(form); timeline.maxPressure = 2; Step(d, 3);
        Check(d.TotalSpawned == 0 && d.CueStates[0].reason.StartsWith("Pressure limit 2:"), "Flexible placement does not bypass pressure budget");

        form.slots[0].releaseGroup = form.slots[1].releaseGroup = 1; form.slots[1].releaseDelay = 0;
        Use(form); yield return .05f; Block(Point(form, 0)); Step(d, 1.2f);
        Check(d.TotalSpawned == 1 && d.TimelinePendingCount == 2, "Explicit linked pair waits together while unrelated slots release");
        Step(d, 1.5f); Check(d.CueStates[0].skipped == 2 && d.CueStates[0].spawned == 1, "A blocked linked pair expires together");
        form.slots[0].releaseGroup = form.slots[1].releaseGroup = 0; form.slots[1].releaseDelay = .25f;

        obstacle.SetActive(false); Use(form); yield return .05f;
        var player = preview.Players[0]; var playerPosition = player.transform.position;
        player.ConfigureDemonstration(root.transform, 0, 1); player.transform.position = Point(form, 0);
        if (player.TryGetComponent<Rigidbody>(out var playerBody)) playerBody.position = player.transform.position;
        Physics.SyncTransforms(); Step(d, 1.2f);
        Check(d.TotalSpawned == 2 && d.CueStates[0].slots[0].reason == "Player occupies arrival", "Player occupancy holds its slot while other members spawn safely");
        player.transform.position = playerPosition; if (playerBody) playerBody.position = playerPosition;
        player.ConfigureDemonstration(preview.director.SimulationRoot, 0, 1);

        var ring = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<EnemyFormation>(EnemyEncounterLabSetup.Folder + "/01b Drone Center Ring.asset")));
        ring.warningSeconds = .25f;
        Use(ring); yield return .05f;
        Vector3 ringCenter = d.arenaLayout.World(Vector2.zero), radial = (Point(ring, 0) - ringCenter).normalized;
        Occupant(Point(ring, 0) + radial * .65f); Step(d, .26f);
        Check(d.CueStates[0].slots.All(s => Mathf.Abs(Vector3.Distance(s.position, ringCenter) - 3.8f) < .001f), "Ring correction preserves its radius");
        Check(d.CueStates[0].slots.Any(s => s.adjusted), "Ring rotates away from an obstructing enemy");
        var phalanx = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<EnemyFormation>(EnemyEncounterLabSetup.Folder + "/01c Drone Goal Phalanxes.asset")));
        phalanx.warningSeconds = .25f;
        Use(phalanx); yield return .05f; Occupant(Point(phalanx, 0) + Vector3.left * .65f); Step(d, .26f);
        var positions = d.CueStates[0].slots;
        Check(positions.Any(s => s.adjusted), "Phalanx placement reacts to its blocked footprint");
        foreach (var group in phalanx.slots.Select((s, i) => (s, i)).GroupBy(x => x.s.placementGroup))
        {
            var firstSlot = positions[group.First().i]; Vector3 shift = firstSlot.position - firstSlot.authored;
            Check(group.All(x => Vector3.Distance(positions[x.i].position - positions[x.i].authored, shift) < .001f), "Phalanx group " + group.Key + " keeps its shape");
        }
        Check(positions.All(s => d.arenaBounds.ContainsWorldPoint(s.position, s.radius + d.borderBufferWorld)), "Corrected footprints remain inside arena bounds");

        var turret = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<EnemyFormation>(EnemyEncounterLabSetup.Folder + "/05 Turret Top Bottom.asset")));
        turret.maxPositionAdjustment = 1; turret.warningSeconds = .25f;
        Use(turret); yield return .05f; d.arenaLayout.Resolve(turret.slots[0], false, out var mount, out _);
        Occupant(mount.clearance); Step(d, .3f);
        Check(d.TotalSpawned == 0 && d.CueStates[0].slots.All(s => !s.adjusted && s.position == s.authored), "Blocked wall sockets never slide off their mount");

        // A real mobile Drone enters an already announced slot. Let its ordinary physics steering run.
        var liveForm = Own(Object.Instantiate(form)); liveForm.warningSeconds = 2; liveForm.slots.RemoveRange(1, 2);
        Use(liveForm); timeline.cues[0].arrivalSeconds = 3; d.RestartTimeline(); yield return .05f; Step(d, 1.1f);
        Vector3 livePoint = Point(liveForm, 0);
        var liveObject = Object.Instantiate(definition.prefab, livePoint + Vector3.right * .2f, Quaternion.identity, root.transform);
        var mobile = liveObject.GetComponent<EnemyBase>(); mobile.ConfigureDemonstration(root.transform, null); d.RegisterAuthoredEnemy(mobile, definition);
        mobile.HoldPosition = false; mobile.AttacksEnabled = false;
        var drone = liveObject.GetComponent<DroneController>(); drone.detectionRange = .1f; drone.spawnGraceSeconds = 0;
        d.enabled = true; mobile.Pause(false);
        var bias = d.ArrivalAvoidance(mobile, Vector3.left);
        Check(bias.x > 0 && bias.magnitude <= d.arrivalAvoidanceStrength + .001f, "Announced footprint supplies bounded outward steering");
        mobile.HoldPosition = true; Check(d.ArrivalAvoidance(mobile, Vector3.left) == Vector3.zero, "HoldPosition prevents arrival steering"); mobile.HoldPosition = false;
        d.timelinePaused = true; Check(d.ArrivalAvoidance(mobile, Vector3.left) == Vector3.zero, "Paused Director supplies no arrival steering"); d.timelinePaused = false;
        yield return .9f;
        Check(Vector3.Distance(mobile.transform.position, livePoint) > .25f,
            $"Live Drone movement yields away from the warned footprint (distance {Vector3.Distance(mobile.transform.position, livePoint):0.00}, paused {mobile.IsPaused}, kinematic {mobile.GetComponent<Rigidbody>().isKinematic})");
        d.RestartTimeline(true); yield return .1f;
        Check(d.TimelinePendingCount == 0 && d.AliveCount == 0, "Restart cleans up flexible reservations and living fixture enemies");
        Check(EditorJsonUtility.ToJson(authored) == savedTimeline, "User-authored timeline remains unchanged");
        Check(errors.Count == 0, "No runtime errors or exceptions");
    }
}
#endif
