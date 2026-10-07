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

/// <summary>Deterministic Play Mode fixtures using COSMOS geometry and real encounter prefabs.</summary>
[InitializeOnLoad]
public static partial class EnemyFlexiblePlacementValidation
{
    const string Folder = "Library/EnemyFlexiblePlacement", Key = "EnemyFlexiblePlacement.Validation";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<string> checks = new(), errors = new();
    static readonly List<GameObject> blockers = new();
    static readonly Dictionary<EnemyDirector.SlotStatus, Vector3> warned = new();
    static EnemyDirector director;
    static EnemyArenaLayout layout;
    static EnemyEncounterTimeline fixture;
    static Collider[] originalExclusions;
    static IEnumerator routine;
    static int frame;
    static double deadline;
    static bool background;
    static readonly MethodInfo StepMethod = typeof(EnemyDirector).GetMethod("TickTimeline", Private);
    static EnemyFlexiblePlacementValidation()
    {
        EditorApplication.playModeStateChanged += State;
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(Folder + "/test.request") || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Folder + "/test.request"); Run();
        };
    }
    [MenuItem("MASSIVE/Encounters/Validate flexible COSMOS placement")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != "Assets/Scenes/S-9_COSMOS.unity")
            throw new InvalidOperationException("Open COSMOS in Edit Mode.");
        Directory.CreateDirectory(Folder); File.WriteAllText(Folder + "/validation.txt", "STARTING\n");
        SessionState.SetBool(Key, true); EnemyEncounterPreviewMode.Request(true); EditorApplication.isPlaying = true;
    }
    static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            checks.Clear(); errors.Clear(); blockers.Clear(); warned.Clear(); frame = -1;
            background = Application.runInBackground; Application.runInBackground = true;
            deadline = EditorApplication.timeSinceStartup + 150;
            Application.logMessageReceived += Log; routine = RunNested(Checks()); EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode) SessionState.SetBool(Key, false);
    }
    static void Log(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
    static IEnumerator RunNested(IEnumerator source)
    {
        while (source.MoveNext())
        {
            if (source.Current is IEnumerator nested)
            {
                var runner = RunNested(nested);
                while (runner.MoveNext()) yield return runner.Current;
            }
            else yield return source.Current;
        }
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || routine == null || frame == Time.frameCount) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timed out");
            frame = Time.frameCount; if (!routine.MoveNext()) Finish(null);
        }
        catch (Exception e) { Finish(e); }
    }
    static void Finish(Exception failure)
    {
        routine = null; EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        Application.runInBackground = background;
        bool passed = failure == null && errors.Count == 0;
        File.WriteAllText(Folder + "/validation.txt", (passed ? "PASSED" : "FAILED") + "\n" + string.Join("\n", checks) + "\n" + failure + "\nConsole errors: " + errors.Count + "\n" + string.Join("\n", errors));
        Debug.Log("[Flexible encounter validation] " + (passed ? "PASSED" : "FAILED: " + failure));
        EditorApplication.isPlaying = false;
    }
    static void Check(bool value, string text)
    {
        if (!value) throw new Exception(text + " at " + director.GameplayAge + "\n" + string.Join("\n", director.CueStates.Select(c => c.label + ": " + c.state + " " + c.Progress + " " + c.reason + " " + string.Join("; ", c.slots.Select(s => s.reason)))));
        checks.Add("PASS " + text); File.WriteAllText(Folder + "/validation.txt", "RUNNING\n" + string.Join("\n", checks));
    }
    static BoxCollider Block(Vector3 center, Vector3 size)
    {
        var go = new GameObject("Validation obstruction"); go.transform.position = center;
        var box = go.AddComponent<BoxCollider>(); box.size = size; blockers.Add(go);
        layout.exclusions = originalExclusions.Concat(blockers.Select(b => b.GetComponent<Collider>())).ToArray();
        Physics.SyncTransforms(); return box;
    }
    static void Reset(EnemyFormation formation, bool repeat = false)
    {
        foreach (var go in blockers) Object.DestroyImmediate(go);
        blockers.Clear(); layout.exclusions = originalExclusions; warned.Clear();
        fixture.cues.Clear(); fixture.cues.Add(new EnemyEncounterTimeline.Cue { label = "Fixture A", formation = formation, arrivalSeconds = 3, allowedLateness = 3 });
        if (repeat) fixture.cues.Add(new EnemyEncounterTimeline.Cue { label = "Fixture B", formation = formation, arrivalSeconds = 8, allowedLateness = 3 });
        director.RestartTimeline(true);
    }
    static void Step()
    {
        StepMethod.Invoke(director, new object[] { .1f });
        foreach (var slot in director.CueStates.SelectMany(c => c.slots.Concat(c.reinforcements?.slots ?? new())).Where(s => s.announced))
        {
            if (warned.TryGetValue(slot, out var before) && (slot.position - before).sqrMagnitude > .000001f)
                throw new Exception("An announced warning moved");
            warned[slot] = slot.position;
        }
    }
    static IEnumerator Until(float age)
    {
        int ticks = 0;
        while (director.GameplayAge < age) { Step(); if (++ticks % 5 == 0) yield return null; }
    }
    static bool Symmetric(EnemyDirector.CueStatus cue) => cue.slots.Count == 2 &&
        (cue.slots[0].position + cue.slots[1].position - 2 * layout.World(Vector2.zero)).sqrMagnitude < .00001f;

    static IEnumerator Checks()
    {
        yield return null;
        director = Object.FindObjectsByType<EnemyDirector>(FindObjectsSortMode.None).Single(d => d.encounterTimeline && d.arenaLayout && d.arenaLayout.arena.ovalOutline);
        Check(GameFlowContext.Instance.IsTwoVTwo && Object.FindFirstObjectByType<PlayerRosterController>().P2.activeSelf && Object.FindFirstObjectByType<PlayerRosterController>().P4.activeSelf,
            "Composer 2v2 preview starts the actual four-player roster before scene Awake");
        GameFlowContext.Instance.SetMode(GameMode.OneVOne);
        director.enabled = false; layout = director.arenaLayout; originalExclusions = layout.exclusions;
        var original = director.encounterTimeline;
        var forms = original.cues.SelectMany(c => new[] { c.formation, c.fallback }.Concat(c.variants)).Where(f => f).Distinct().ToArray();
        var hashes = forms.Select(AssetDatabase.GetAssetPath).Append(AssetDatabase.GetAssetPath(original)).Append(SceneManager.GetActiveScene().path)
            .ToDictionary(p => p, p => Hash128.Compute(File.ReadAllText(p)));
        Check(forms.All(f => f.integrity == EnemyFormation.Integrity.Flexible), "All timeline formations and fallbacks are flexible");
        Check(forms.Where(f => f.slots.All(s => string.IsNullOrEmpty(s.socket))).All(f => f.maxPositionAdjustment >= 1 && f.blockedSlotGrace >= 3), "Mobile formations have local adjustment and independent grace");
        Check(layout.sockets.Count(s => s.enabled && s.useRange && EnemyArenaLayout.ValidWallRange(s)) == 4, "Four valid wall quadrants configured");
        var turretForm = forms.Single(f => f.name == "05 Turret Top Bottom");
        var seekerForm = forms.Single(f => f.name == "03 Seeker Stagger");
        fixture = Object.Instantiate(original); fixture.twoVTwo = new(); fixture.fitRegulation = false; fixture.duration = 20; fixture.maxAliveTotal = 80; fixture.maxPressure = 0;
        director.encounterTimeline = fixture; director.previewCue = -1; director.timelinePaused = false;
        var scope = new GameObject("Flexible validation scope"); director.ConfigureDemonstration(scope.transform);
        director.TimelineEnemySpawned += enemy =>
        {
            if (!director.CueStates.SelectMany(c => c.slots.Concat(c.reinforcements?.slots ?? new())).Any(s => s.announced && (s.position - enemy.transform.position).sqrMagnitude < .000001f))
                throw new Exception("Enemy missed its telegraph");
            enemy.Pause(true);
        };
        var top = layout.sockets.Single(s => s.id == "TopLeft");
        var bottom = layout.sockets.Single(s => s.id == "BottomRight");
        float radius = Mathf.Max(director.spawnCheckRadiusWorld, turretForm.slots[0].enemy.GetSpawnRadiusWorld());
        foreach (bool oval in new[] { true, false })
        {
            layout.arena.ovalOutline = oval;
            for (int i = 8; i <= 56; i += 8)
            {
                var a = layout.WallPose(top, i / 64f); var b = layout.WallPose(bottom, i / 64f);
                Check((a.position + b.position - 2 * layout.World(Vector2.zero)).sqrMagnitude < .00001f &&
                    Vector3.Dot(a.rotation * Vector3.forward, layout.World(Vector2.zero) - a.position) > 0,
                    $"{(oval ? "Oval" : "Rectangular")} wall fraction {i}/64 is symmetric and faces inward");
            }
        }
        layout.arena.ovalOutline = true;
        var invalid = new EnemyArenaLayout.Socket { useRange = true, rangeStart = Vector2.zero, rangeEnd = Vector2.one };
        Check(!EnemyArenaLayout.ValidWallRange(invalid), "Interior/diagonal ranges are rejected");

        Reset(turretForm, true); yield return null;
        Block(layout.WallPose(top, .5f).clearance, Vector3.one * .3f);
        yield return Until(12);
        Check(director.CueStates.All(c => c.spawned == 2 && c.finished), "Both turret waves spawn while the first wave remains alive");
        Check(director.CueStates.All(Symmetric) && director.CueStates[0].slots.All(s => s.adjusted), "Blocked preferred mount shifts both turrets symmetrically");
        var poses = director.CueStates.SelectMany(c => c.slots).ToArray();
        Check(poses.All(p => poses.All(q => p == q || Vector3.Distance(p.clearance, q.clearance) >= radius * 2 - .001f)), "All repeated wall footprints are separated");

        Reset(turretForm); yield return null;
        for (int i = 0; i <= 32; i += 2) Block(layout.WallPose(top, i / 64f).clearance, Vector3.one * .1f);
        for (int i = 32; i <= 64; i += 2) Block(layout.WallPose(bottom, i / 64f).clearance, Vector3.one * .1f);
        yield return Until(7);
        Check(director.CueStates[0].spawned == 2 && !Symmetric(director.CueStates[0]), "Asymmetric free positions work when no symmetric pair fits");

        Reset(turretForm); yield return null;
        for (int i = 0; i <= 64; i += 2) Block(layout.WallPose(top, i / 64f).clearance, Vector3.one * .1f);
        yield return Until(7);
        Check(director.CueStates[0].spawned == 1 && director.CueStates[0].skipped == 1, "Fully occupied quadrant skips only its turret");

        Reset(turretForm); yield return null; yield return Until(1.2f);
        var announced = director.CueStates[0].slots[0];
        Check(announced.announced, "Turret receives its full advance warning");
        Block(announced.clearance, Vector3.one * .3f); yield return Until(7);
        Check(director.CueStates[0].spawned == 1 && director.CueStates[0].skipped == 1, "A late blocker holds/skips the locked warning instead of moving it");

        Reset(turretForm); yield return null;
        for (int i = 0; i <= 64; i += 2) Block(layout.WallPose(top, i / 64f).clearance, Vector3.one * .1f);
        yield return Until(1.5f);
        foreach (var go in blockers) go.SetActive(false);
        yield return Until(7);
        Check(director.CueStates[0].spawned == 2 && Symmetric(director.CueStates[0]), "Blocked quadrant retries and matches the already-announced partner when space clears");

        Reset(seekerForm); yield return null;
        layout.Resolve(seekerForm.slots[0], false, out var seekerPose, out _);
        Block(seekerPose.clearance, Vector3.one * 3f); yield return Until(8);
        Check(director.CueStates[0].spawned == 1 && director.CueStates[0].skipped == 1, "Blocked Seeker cannot cancel its clear formation partner");
        Reset(seekerForm); yield return null;
        Block(seekerPose.clearance, Vector3.one * .02f); yield return Until(8);
        Check(director.CueStates[0].spawned == 2 && director.CueStates[0].slots.Any(s => s.adjusted), "Mobile formation adjusts locally around a small obstruction");

        Reset(turretForm, true); fixture.cues[1].flipWallOrientation = true; yield return null;
        yield return Until(12);
        Check(director.CueStates.All(c => c.spawned == 2), "Original and flipped turret encounters both spawn using the same shared formation");
        Check(layout.Resolve(turretForm.slots[0], false, out var flippedTop, out _, true) && flippedTop.socket.id == "TopRight" &&
            layout.Resolve(turretForm.slots[1], false, out var flippedBottom, out _, true) && flippedBottom.socket.id == "BottomLeft",
            "Flipping maps TopLeft/BottomRight to TopRight/BottomLeft");
        Check(director.CueStates[1].slots.All(s => (s.position - flippedTop.position).sqrMagnitude < .00001f ||
            (s.position - layout.WallPose(layout.sockets.Single(w => w.id == "BottomLeft"), .5f).position).sqrMagnitude < .00001f),
            "Live flipped arrivals match the Composer's resolved wall quadrants");
        Check(turretForm.slots[0].socket == "TopLeft" && turretForm.slots[1].socket == "BottomRight", "Per-encounter flip leaves the shared formation unchanged");

        var four = AssetDatabase.LoadAssetAtPath<EnemyFormation>(EnemyTurretFormationAuthoring.FourQuadrantsPath);
        Check(four && four.slots.Count == 4 && four.slots.Select(s => s.socket).Distinct().Count() == 4,
            "Four Quadrants is a discoverable formation asset with four distinct mounts");
        Reset(four); yield return null;
        Block(layout.WallPose(top, .5f).clearance, Vector3.one * .3f);
        yield return Until(7);
        Check(director.CueStates[0].spawned == 4 && director.CueStates[0].slots.All(s => s.announced && s.adjusted),
            "Four-quadrant wave searches all four wall ranges and spawns four telegraphed turrets");
        var fourPoses = director.CueStates[0].slots;
        Check((fourPoses[0].position + fourPoses[3].position - 2 * layout.World(Vector2.zero)).sqrMagnitude < .00001f &&
            (fourPoses[1].position + fourPoses[2].position - 2 * layout.World(Vector2.zero)).sqrMagnitude < .00001f,
            "Four-quadrant relocation preserves paired symmetry");

        Reset(seekerForm); fixture.cues[0].placementOverrides.Add(new EnemyEncounterTimeline.SlotPlacement {
            formation = seekerForm, slotIndex = 0, position = new Vector2(.35f, .65f) });
        layout.Resolve(fixture.cues[0].GetSlot(seekerForm, 0), false, out var draggedMobile, out _);
        yield return null; yield return Until(8);
        Check(director.CueStates[0].spawned == 2 && (director.CueStates[0].slots[0].position - draggedMobile.position).sqrMagnitude < .00001f,
            "Live mobile arrival honors the Composer's per-encounter dragged position");

        Reset(turretForm); fixture.cues[0].flipWallOrientation = true;
        fixture.cues[0].placementOverrides.Add(new EnemyEncounterTimeline.SlotPlacement {
            formation = turretForm, slotIndex = 1, overrideWallPosition = true, wallPosition = .35f });
        layout.Resolve(fixture.cues[0].GetSlot(turretForm, 1), false, out var draggedWall, out _, true);
        yield return null; yield return Until(7);
        Check(director.CueStates[0].spawned == 2 && (director.CueStates[0].slots[1].position - draggedWall.position).sqrMagnitude < .00001f,
            "Live turret arrival honors a dragged second slot on its flipped curved wall");

        Reset(turretForm, true); top.useRange = bottom.useRange = false; yield return null;
        yield return Until(12);
        Check(director.CueStates[0].spawned == 2 && director.CueStates[1].spawned == 0, "Legacy fixed mounts retain exclusive occupancy");
        top.useRange = bottom.useRange = true;
        Reset(turretForm); yield return null;
        fixture.maxAliveTotal = 1; director.RestartTimeline(true); yield return Until(7);
        Check(director.CueStates[0].spawned == 0 && director.CueStates[0].reason.Contains("limit"), "Population cap still prevents over-budget formations");

        var match = Object.FindObjectsByType<GameManagerScript>(FindObjectsSortMode.None).Single(m => m.gameObject.scene == director.gameObject.scene);
        var durationField = typeof(GameManagerScript).GetField("_regulationDurationSeconds", Private);
        float previousDuration = match.RegulationDurationSeconds;
        fixture.maxAliveTotal = 80; fixture.duration = 120; fixture.fitRegulation = true;
        var releaseTimes = new List<float>();
        void CaptureRelease(EnemyBase enemy) => releaseTimes.Add(director.GameplayAge);
        director.TimelineEnemySpawned += CaptureRelease;
        foreach (float regulation in new[] { 60f, 120f, 240f })
        {
            durationField.SetValue(match, regulation);
            Reset(seekerForm); fixture.cues[0].arrivalSeconds = 12;
            director.RestartTimeline(true); releaseTimes.Clear(); yield return null;
            float expected = regulation * .1f;
            Check(Mathf.Approximately(director.CueStates[0].arrival, expected) && Mathf.Approximately(director.TimelineDurationSeconds, regulation),
                $"{regulation}s regulation maps arrival to {expected}s and scales the timeline end");
            yield return Until(expected - seekerForm.warningSeconds - .2f);
            Check(director.CueStates[0].slots.All(s => !s.announced), $"{regulation}s regulation does not begin warnings early");
            yield return Until(expected - seekerForm.warningSeconds + .2f);
            Check(director.CueStates[0].slots[0].announced, $"{regulation}s regulation preserves the full real-time warning lead");
            float pausedAt = director.GameplayAge; director.timelinePaused = true; Step();
            Check(director.GameplayAge == pausedAt, $"{regulation}s scaled preview pauses without advancing arrivals");
            director.timelinePaused = false;
            yield return Until(expected + EnemyEncounterAuthoring.LastDelay(seekerForm) + 1);
            Check(director.CueStates[0].spawned == 2 && releaseTimes.Count == 2 && Mathf.Abs(releaseTimes[0] - expected) < .3f &&
                Mathf.Abs(releaseTimes[1] - releaseTimes[0] - EnemyEncounterAuthoring.LastDelay(seekerForm)) < .25f,
                $"{regulation}s regulation spawns at the scaled arrival with unchanged formation stagger");
        }
        float capturedArrival = director.CueStates[0].arrival;
        durationField.SetValue(match, 120f);
        Check(director.CueStates[0].arrival == capturedArrival && director.TimelineTimeScale == 2,
            "Active run holds its captured timing until restart");
        director.previewCue = 0; director.RestartTimeline(true);
        Check(Mathf.Approximately(director.CueStates[0].arrival, seekerForm.warningSeconds + 1), "Play cue still starts promptly with a full warning");
        director.previewCue = -1; director.TimelineEnemySpawned -= CaptureRelease;
        durationField.SetValue(match, previousDuration);
        yield return ScalingChecks();
        Check(hashes.All(h => Hash128.Compute(File.ReadAllText(h.Key)) == h.Value), "Play Mode fixtures leave saved scene, timeline and formation assets unchanged");
        director.RestartTimeline(true); director.encounterTimeline = original;
        Object.Destroy(fixture); Object.Destroy(scope);
    }
}
#endif
