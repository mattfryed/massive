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
public static class CarrierLaneAvoidanceValidation
{
    private const string Key = "CarrierLaneAvoidanceValidation", Folder = "Library/CarrierLaneAvoidanceValidation";
    private static IEnumerator routine;
    private static int frame;
    private static float resume;
    private static double deadline;
    private static readonly List<string> results = new(), errors = new();
    private static readonly List<Object> owned = new();
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static CarrierLaneAvoidanceValidation()
    {
        EditorApplication.playModeStateChanged += State;
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(Folder + "/run.request") || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Folder + "/run.request"); Run();
        };
    }
    [MenuItem("MASSIVE/Enemies/Validate Carrier Turret Avoidance")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        if (!Object.FindFirstObjectByType<EnemyLab>()) throw new InvalidOperationException("Open Player Actions with Enemy Lab.");
        Directory.CreateDirectory(Folder); File.WriteAllText(Folder + "/report.txt", "RUNNING\n");
        SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
    }
    private static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            frame = 0; resume = 0; results.Clear(); errors.Clear(); owned.Clear(); deadline = EditorApplication.timeSinceStartup + 150;
            SessionState.SetBool(Key + ".Background", Application.runInBackground); Application.runInBackground = true;
            Application.logMessageReceived += Log; routine = Checks(); EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        { Application.runInBackground = SessionState.GetBool(Key + ".Background", true); SessionState.SetBool(Key, false); }
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
        bool passed = error == null && errors.Count == 0;
        File.WriteAllText(Folder + "/report.txt", (passed ? "PASSED" : "FAILED") + "\n" + string.Join("\n", results) + "\n" + error + "\n" + string.Join("\n", errors));
        Debug.Log("[Carrier lane avoidance] " + (passed ? "PASSED" : "FAILED: " + error));
        foreach (var obj in owned) if (obj) Object.Destroy(obj);
        EditorApplication.isPlaying = false;
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        results.Add("PASS " + message); File.WriteAllText(Folder + "/report.txt", "RUNNING\n" + string.Join("\n", results));
    }
    private static T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    private static void Field(object obj, string field, object value) => obj.GetType().GetField(field, Private).SetValue(obj, value);
    private static void Property(object obj, string field, object value) => obj.GetType().GetProperty(field).SetValue(obj, value);
    private static void Place(Component obj, Vector3 p)
    { obj.transform.position = p; if (obj.TryGetComponent<Rigidbody>(out var rb)) rb.position = p; Physics.SyncTransforms(); }
    private static IEnumerator Checks()
    {
        var lab = Object.FindFirstObjectByType<EnemyLab>(); lab.mode = EnemyLab.LabMode.EncounterTimeline;
        var preview = lab.timelinePreview;
        while (!preview.isActiveAndEnabled || preview.Players.Count != 2) yield return null;
        preview.loop = false; preview.actorsMove = preview.actorsAttack = false;
        preview.preset = EnemyEncounterLab.LayoutPreset.Open;
        var timeline = Own(ScriptableObject.CreateInstance<EnemyEncounterTimeline>()); timeline.duration = 120; timeline.maxPressure = 60;
        var formation = Own(ScriptableObject.CreateInstance<EnemyFormation>()); formation.warningSeconds = .2f;
        formation.slots.Add(new EnemyFormation.Slot { enemy = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(ParticleBeamTurretSetup.DefinitionPath),
            telegraph = AssetDatabase.LoadAssetAtPath<GameObject>(ParticleBeamTurretSetup.WarningPath).GetComponent<EnemySpawnTelegraph>(), socket = "TopLeft" });
        formation.slots.Add(new EnemyFormation.Slot { enemy = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(CarrierPrototypeSetup.DefinitionPath),
            telegraph = AssetDatabase.LoadAssetAtPath<GameObject>(CarrierPrototypeSetup.Folder + "/Carrier Spawn Telegraph.prefab").GetComponent<EnemySpawnTelegraph>(),
            position = new Vector2(.25f, .7f) });
        timeline.cues.Add(new EnemyEncounterTimeline.Cue { label = "Carrier blocks turret", arrivalSeconds = .5f, formation = formation, allowedLateness = 2 });
        var director = preview.director; director.previewCue = -1; director.encounterTimeline = timeline; preview.RestartPreview();
        var player = preview.Players[0]; preview.Players[1].gameObject.SetActive(false);
        Place(player, new Vector3(-7, 0, -2));
        CarrierController carrier = null; ParticleBeamTurretController turret = null;
        director.TimelineEnemySpawned += enemy =>
        {
            if (enemy.TryGetComponent<CarrierController>(out carrier)) { carrier.avoidTurretLanes = false; Property(carrier, "RemainingSeconds", 100f); }
            if (enemy.TryGetComponent<ParticleBeamTurretController>(out var t)) { turret = t; turret.chargeSeconds = 100; }
        };
        while (director.GameplayAge < 3) yield return null;
        carrier = director.enemyRoot.GetComponentInChildren<CarrierController>(); turret = director.enemyRoot.GetComponentInChildren<ParticleBeamTurretController>();
        Check(carrier && turret && carrier.IsSpawnReady && turret.IsReady, "Real Carrier and turret prefabs spawn in the Enemy Lab fixture");
        var enemy = carrier.GetComponent<EnemyBase>(); var turretEnemy = turret.GetComponent<EnemyBase>();
        var body = carrier.GetComponent<Rigidbody>();
        Check(!turret.Target && turret.TryGetObstructedLane(enemy, out _, out _), "Carrier blockage is detected before the turret can acquire a player");
        carrier.avoidTurretLanes = true;
        Vector3 initial = body.position, previous = initial; float peakStep = 0f;
        float moveDeadline = Time.time + 5f;
        while (Time.time < moveDeadline && Vector3.Distance(body.position, initial) < .02f) yield return null;
        Check(carrier.IsClearingTurretLane && Vector3.Distance(body.position, initial) > .01f,
            "Carrier starts a gentle sidestep using its existing Rigidbody; moving=" + carrier.IsClearingTurretLane +
            ", from=" + initial + ", now=" + body.position + ", destination=" + carrier.TurretAvoidanceDestination + ", constraints=" + body.constraints + ", kinematic=" + body.isKinematic);
        Property(carrier, "RemainingSeconds", .1f);
        enemy.Pause(true); var pausedAt = body.position; yield return .5f;
        Check(Vector3.Distance(pausedAt, body.position) < .001f, "Pause freezes avoidance movement");
        enemy.Pause(false);
        float until = Time.time + 12;
        while (Time.time < until && (!turret.Target || carrier.IsClearingTurretLane))
        {
            peakStep = Mathf.Max(peakStep, Vector3.Distance(body.position, previous)); previous = body.position;
            yield return null;
        }
        Check(turret.Target == player && !carrier.IsClearingTurretLane, "Carrier clears the sightline and the turret acquires its player");
        Check(Vector3.Distance(initial, body.position) < 3f, "Clearance is a short reposition, not roaming");
        Check(carrier.TotalLaunched > 0, "Carrier keeps launching regular Drones while repositioning");
        enemy.AttacksEnabled = false;
        foreach (var drone in director.enemyRoot.GetComponentsInChildren<DroneController>()) Object.Destroy(drone.gameObject);
        var settled = body.position; yield return 1f;
        Check(Vector3.Distance(settled, body.position) < .03f, "Carrier settles without returning to its blocking spawn point");
        turret.chargeSeconds = .1f; yield return .5f;
        Check(turret.TotalBlasts > 0, "Cleared turret resumes its real beam attack");
        turret.chargeSeconds = 100; Property(turret, "Phase", ParticleBeamTurretController.AttackPhase.Seeking); Property(turret, "PhaseAge", 0f);

        void Reset(float x = -7)
        {
            Place(player, new Vector3(x, 0, -2)); Place(turret, new Vector3(x, 0, 6));
            turret.firingPivot.localRotation = Quaternion.identity; Property(turret, "Target", null);
            Place(carrier, new Vector3(x, 0, 2.4f));
            typeof(CarrierController).GetMethod("ResetLaneAvoidance", Private).Invoke(carrier, null);
        }
        GameObject Box(string name, Vector3 position, Vector3 scale)
        {
            var box = Own(GameObject.CreatePrimitive(PrimitiveType.Cube)); box.name = name;
            box.transform.position = position; box.transform.localScale = scale; Physics.SyncTransforms(); return box;
        }
        var left = Box("Left escape blocked", new Vector3(-9, 0, 2.4f), new Vector3(.5f, 2, 4));
        Reset(); yield return .5f;
        Check(carrier.IsClearingTurretLane && carrier.TurretAvoidanceDestination.x > -7, "Blocked left escape selects the clear right side");
        var right = Box("Right escape blocked", new Vector3(-5, 0, 2.4f), new Vector3(.5f, 2, 4));
        Reset(); yield return 1f;
        Check(!carrier.IsClearingTurretLane && Vector3.Distance(body.position, new Vector3(-7, 0, 2.4f)) < .01f, "Two blocked exits hold position without crossing an obstacle");
        left.SetActive(false); right.SetActive(false);
        Reset(-12.4f); yield return .5f;
        Check(carrier.IsClearingTurretLane && carrier.TurretAvoidanceDestination.x > -12.4f &&
            director.arenaBounds.ContainsWorldPoint(carrier.TurretAvoidanceDestination, 1f), "Near a boundary the Carrier chooses the inward exit");
        Reset(); enemy.HoldPosition = true; yield return .5f;
        Check(!carrier.IsClearingTurretLane && Vector3.Distance(body.position, new Vector3(-7, 0, 2.4f)) < .01f, "Explicit HoldPosition suppresses avoidance");
        enemy.HoldPosition = false;
        Reset(); Field(player, "_matchSpawning", true); yield return .5f;
        Check(!carrier.IsClearingTurretLane, "Spawning players do not request turret clearance");
        Field(player, "_matchSpawning", false);
        var otherScope = Own(new GameObject("Other simulation")); enemy.ConfigureDemonstration(otherScope.transform, null);
        Reset(); yield return .5f;
        Check(!carrier.IsClearingTurretLane, "Carrier ignores a turret in another simulation scope");
        enemy.ConfigureDemonstration(director.SimulationRoot, null);
        var wall = Box("Wall before Carrier", new Vector3(-7, 0, 4.5f), new Vector3(3f, 2f, .1f));
        Reset(); yield return .5f;
        Check(!carrier.IsClearingTurretLane, "A different first obstruction does not make the Carrier drift unnecessarily");
        wall.SetActive(false); Reset(); turret.enabled = false; yield return .5f;
        Check(!carrier.IsClearingTurretLane, "Disabled turrets do not request clearance");
        Check(errors.Count == 0, "No runtime errors during Carrier lane checks");
    }
}
#endif
