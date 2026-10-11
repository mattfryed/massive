#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Massive.Enemies;
using Massive.Multiplier;
using Massive.Scoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Disposable integration fixtures in the saved HIGGS scene, using its real encounters.</summary>
[InitializeOnLoad]
public static class HiggsEnemyValidation
{
    const string Key = "MASSIVE.HiggsEnemyValidation", Folder = HiggsEnemySetup.Folder;
    static readonly List<string> checks = new(), errors = new();
    static readonly Dictionary<EnemyDirector.SlotStatus, Vector3> warned = new();
    static EnemyDirector director;
    static IEnumerator routine;
    static int frame;
    static double deadline;
    static bool background;
    static string Report => Folder + (SessionState.GetInt(Key + ".mode", 0) == 0 ? "/one-v-one.txt" : "/two-v-two.txt");

    static HiggsEnemyValidation()
    {
        EditorApplication.playModeStateChanged += State;
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(Folder + "/test.request") || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Folder + "/test.request"); Run();
        };
    }
    [MenuItem("MASSIVE/HIGGS/Validate Enemy Integration (1v1 and 2v2)")]
    public static void Run()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty || scene.path != HiggsLevelSetup.ScenePath)
            throw new InvalidOperationException("Open the saved HIGGS scene in Edit Mode.");
        Directory.CreateDirectory(Folder); checks.Clear(); errors.Clear();
        director = HiggsEnemySetup.All<EnemyDirector>(scene).Single();
        var layout = HiggsEnemySetup.All<EnemyArenaLayout>(scene).Single();
        checks.Add(HiggsLevelValidation.CheckScene(scene));
        Check(director.isActiveAndEnabled && director.waitForScoring && director.previewCue == -1, "One active, match-gated Director plays the full timeline");
        Check(director.encounterTimeline && director.spawnProfile && director.spawnProfile.enabled, "Timeline and enabled spawn profile assigned");
        Check(director.arenaLayout == layout && layout.arena == director.arenaBounds && director.enemyRoot &&
            director.enemyRoot.IsChildOf(director.transform), "Arena and enemy container belong to HIGGS");
        Check(director.anomalyManager == HiggsEnemySetup.All<AnomalyManager>(scene).Single() &&
            director.resonanceSpawner == HiggsEnemySetup.All<AmplifierResonanceSpawner>(scene).Single(), "Local anomaly and Resonance bindings");
        Check(director.placementRegion == null && layout.allowResonanceAndAmplifierOverlap, "Shared timeline overlap policy retained");
        Check(layout.sockets.Count(s => s.enabled && s.useRange && EnemyArenaLayout.ValidWallRange(s)) == 4 &&
            layout.sockets.Count(s => s.enabled && s.id.StartsWith("Side")) == 4, "Four wall quadrants and four goal-safe side fallback mounts");
        var forms = Forms(director.encounterTimeline);
        Check(forms.All(f => f.slots.All(s => s.enemy && s.enemy.prefab && s.telegraph &&
            (string.IsNullOrEmpty(s.socket) ? layout.regions.Any(r => r.id == s.region) : layout.sockets.Any(m => m.enabled && m.id == s.socket)))),
            "Every formation, variant and fallback has its enemy, warning, region or wall mount");
        File.WriteAllText(Folder + "/authored.txt", string.Join("\n", checks));
        // Concurrent file edits must not trigger package reinitialization halfway
        // through a fixture. Release the refresh hold on completion or manual stop.
        AssetDatabase.DisallowAutoRefresh(); SessionState.SetBool(Key + ".refreshHeld", true);
        StartMode(0);
    }
    static EnemyFormation[] Forms(EnemyEncounterTimeline timeline) => timeline.cues
        .SelectMany(c => new[] { c.formation, c.fallback }.Concat(c.variants)).Where(f => f).Distinct().ToArray();
    static void StartMode(int mode)
    {
        SessionState.SetBool(Key, true); SessionState.SetInt(Key + ".mode", mode);
        SessionState.SetBool(Key + ".next", false);
        EnemyEncounterPreviewMode.Request(mode == 1); EditorApplication.isPlaying = true;
    }
    static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            checks.Clear(); errors.Clear(); warned.Clear(); frame = -1;
            background = Application.runInBackground; Application.runInBackground = true;
            deadline = EditorApplication.timeSinceStartup + 180;
            Application.logMessageReceived += Log; routine = Nested(Checks()); EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false);
            if (SessionState.GetBool(Key + ".next", false)) EditorApplication.delayCall += () => StartMode(1);
            else if (SessionState.GetBool(Key + ".refreshHeld", false))
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
    static IEnumerable<EnemyDirector.SlotStatus> Slots() => director.CueStates
        .SelectMany(c => c.slots.Concat(c.reinforcements?.slots ?? new List<EnemyDirector.SlotStatus>()));
    static EnemyBase[] Alive() => Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None)
        .Where(e => e.Director == director && !e.IsDead).ToArray();
    static void Tick()
    {
        if (!EditorApplication.isPlaying || routine == null || frame == Time.frameCount) return;
        try
        {
            frame = Time.frameCount;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Fixture timed out");
            if (!routine.MoveNext()) { Finish(null); return; }
            if (!director) return;
            foreach (var s in Slots().Where(s => s.announced))
            {
                if (warned.TryGetValue(s, out var p) && Vector3.Distance(p, s.position) > .0001f)
                    throw new Exception("An announced warning moved");
                warned[s] = s.position;
            }
        }
        catch (Exception e) { Finish(e); }
    }
    static string Cues() => director ? string.Join("\n", director.CueStates.Select(c => c.label + ": " + c.state + " " + c.Progress + " " + c.reason +
        " " + string.Join("; ", c.slots.Where(s => !string.IsNullOrEmpty(s.reason)).Select(s => s.reason)))) : "";
    static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException(label + "\n" + Cues());
        checks.Add("PASS " + label); File.WriteAllText(Report, "RUNNING\n" + string.Join("\n", checks));
    }
    static void Finish(Exception error)
    {
        routine = null; EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        Application.runInBackground = background; Time.timeScale = 1;
        bool passed = error == null && errors.Count == 0;
        File.WriteAllText(Report, (passed ? "PASSED" : "FAILED") + $" ({checks.Count} checks)\n" + string.Join("\n", checks) + "\n" + error +
            "\nConsole errors: " + errors.Count + "\n" + string.Join("\n", errors));
        SessionState.SetBool(Key + ".next", passed && SessionState.GetInt(Key + ".mode", 0) == 0);
        Debug.Log("HIGGS ENEMIES " + (passed ? "PASSED" : "FAILED")); EditorApplication.isPlaying = false;
    }
    static IEnumerator Wait(float seconds)
    { float end = Time.unscaledTime + seconds; while (Time.unscaledTime < end) yield return null; }
    static IEnumerator Until(Func<bool> condition, float seconds, string label)
    {
        float end = Time.unscaledTime + seconds;
        while (!condition() && Time.unscaledTime < end) yield return null;
        Check(condition(), label);
    }
    static IEnumerator Checks()
    {
        director = Object.FindFirstObjectByType<EnemyDirector>();
        var gm = Object.FindFirstObjectByType<GameManagerScript>();
        // An unattended fixture has no cabinet input. Keep the real match running
        // without changing the saved scene's ordinary inactivity/Attract behavior.
        var matchSettings = new SerializedObject(gm);
        matchSettings.FindProperty("allPlayersInactiveHoldSeconds").floatValue = 600;
        matchSettings.ApplyModifiedPropertiesWithoutUndo();
        var scores = gm.ScoreService;
        var knot = Object.FindFirstObjectByType<HiggsExcitationKnotManager>();
        var pair = Object.FindFirstObjectByType<AmplifierResonanceSpawner>();
        var timeline = director.encounterTimeline;
        var paths = Forms(timeline).Select(AssetDatabase.GetAssetPath).Append(AssetDatabase.GetAssetPath(timeline))
            .Append(AssetDatabase.GetAssetPath(director.spawnProfile)).Append(HiggsEnemySetup.ReferenceScene).Append(HiggsLevelSetup.ScenePath);
        var hashes = paths.Distinct().ToDictionary(p => p, p => Hash128.Compute(File.ReadAllText(p)));
        bool two = SessionState.GetInt(Key + ".mode", 0) == 1;
        Check(GameFlowContext.Instance.IsTwoVTwo == two, "Requested match mode applied before scene Awake");
        while (gm.Phase != MatchRuntimePhase.Regulation)
        {
            if (director.GameplayAge > 0 || director.TotalSpawned > 0) throw new Exception("Enemies advanced before regulation");
            yield return null;
        }
        yield return null;
        Check(director.GameplayAge < .2f, "Countdown held enemy clock and arrivals");
        var players = Object.FindObjectsByType<PlayerControllerScript>(FindObjectsSortMode.None).Where(p => !p.IsPseudoPlayer).ToArray();
        Check(players.Length == (two ? 4 : 2) && Rewired.ReInput.isReady, "Real roster and input initialized (players " + players.Length +
            ", input ready " + Rewired.ReInput.isReady + ", managers " + Object.FindObjectsByType<Rewired.InputManager>(FindObjectsSortMode.None).Length + ")");
        var limits = EnemyEncounterScaling.Limits.Capture(timeline, two);
        Check(director.TimelineIsTwoVTwo == two && director.TimelineLimits.total == limits.total && director.TimelineLimits.pressure == limits.pressure,
            "Population limits reflect match mode (total " + limits.total + ")");
        Check(Mathf.Abs(director.TimelineDurationSeconds - gm.EncounterRegulationDurationSeconds) < .01f, "Timeline fits the actual shared regulation duration");
        yield return Until(() => director.ActiveTelegraphCount > 0 && director.TotalSpawned == 0, 5, "Visible warnings precede first enemies");
        yield return Until(() => Alive().Length > 0, 6, "First authored wave arrives through the live Director");
        Check(Alive().All(e => e.transform.IsChildOf(director.enemyRoot) && e.GetComponent<EnemyScoreReward>() && !e.IsPaused),
            "Spawned enemies have local ownership, defeat scoring and active AI");
        var victim = Alive().First(); var player = players.First(p => p.teamID == 1);
        long before = scores.GetTeamScore(player.teamID);
        victim.TakeDamage(victim.HealthRemaining + 1, EnemyDamageSource.Sword, player);
        long after = scores.GetTeamScore(player.teamID);
        victim.TakeDamage(1000, EnemyDamageSource.Sword, player);
        Check(after > before && scores.GetTeamScore(player.teamID) == after, "Player-attributed defeat credits central score exactly once");
        yield return Until(() => director.GameplayAge >= 10, 12, "Opening wave and warnings complete");
        Check(two ? director.CueStates[0].reinforcements?.spawned > 0 : director.CueStates[0].reinforcements == null,
            "Opening wave uses appropriate 1v1/2v2 reinforcement policy");
        scores.SetScoringState(true, false); yield return null; yield return null;
        float age = director.GameplayAge; int count = director.TotalSpawned;
        yield return Wait(.4f);
        Check(Mathf.Abs(director.GameplayAge - age) < .0001f && director.TotalSpawned == count && Alive().All(e => e.IsPaused), "Paused match clock freezes enemies and arrivals");
        scores.SetScoringState(true, true); yield return null; yield return null;
        Check(director.GameplayAge > age && Alive().All(e => !e.IsPaused), "Enemies resume with the match clock");
        yield return Until(() => knot.ActiveKnot && pair.ActiveCore && pair.ActivePattern && Alive().Length > 0, 35,
            "Enemies coexist with the live Knot, Amplifier Core and Resonance pattern");
        ScreenCapture.CaptureScreenshot(Folder + (two ? "/two-v-two.png" : "/one-v-one.png")); yield return Wait(.15f);
        // Exercise the other families without changing the shared timeline or waiting
        // for a full match. Real Update, warning duration, placement and AI still run.
        for (int i = 1; i <= 5; i++)
        {
            warned.Clear(); director.previewCue = i; director.RestartTimeline(true); yield return null;
            yield return Until(() => director.TotalSpawned > 0, 10, "Runtime cue: " + timeline.cues[i].label);
            yield return Wait(.25f);
            Check(Alive().Any(e => e.Definition == director.CueStates[0].formation.slots[0].enemy), "Correct enemy family for " + timeline.cues[i].label);
        }
        Check(warned.Count > 0, "Announced warning positions remained fixed throughout fixtures");
        director.previewCue = 0; director.RestartTimeline(true); yield return null;
        yield return Until(() => director.TotalSpawned > 0 && director.TimelinePendingCount > 0, 8, "Closing fixture has live enemies and pending arrivals");
        scores.CloseScoring(); yield return null; yield return null;
        age = director.GameplayAge; count = director.TotalSpawned; yield return Wait(.4f);
        Check(director.TimelinePendingCount == 0 && director.ActiveTelegraphCount == 0 && director.TotalSpawned == count &&
            Mathf.Abs(director.GameplayAge - age) < .0001f && Alive().All(e => e.IsPaused), "Closing scoring cancels warnings and freezes remaining enemies");
        Check(hashes.All(h => Hash128.Compute(File.ReadAllText(h.Key)) == h.Value), "Saved HIGGS, LATTICE and shared encounter assets unchanged by Play Mode fixtures");
        Check(errors.Count == 0, "No runtime errors, exceptions or assertions");
    }
}
#endif
