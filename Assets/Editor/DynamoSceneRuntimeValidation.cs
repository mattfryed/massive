#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Massive.Dynamo;
using Massive.Enemies;
using Massive.Levels;
using Massive.Multiplier;
using Massive.Scoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Read-only scene observations, plus an explicitly invoked Play Mode capture check.</summary>
public static class DynamoSceneRuntimeValidation
{
    const string ScenePath = "Assets/Scenes/S-6_DYNAMO.unity";

    public static string CaptureCheckResult { get; private set; } = "Not started";
    static bool captureCheckRunning, captureCompleted;
    static double captureDeadline;
    static int captureSceneHandle, capturedCoreId, capturedPatternId, pairsAtCapture;
    static string captureEvidence;

    /// <summary>
    /// Explicit Play Mode QA: capture the next ready managed Core through its real
    /// gameplay API, then observe the normal retirement and next-pair cycle.
    /// This advances one live team multiplier; it does not edit serialized assets.
    /// </summary>
    public static void BeginCaptureCheck()
    {
        var scene = SceneManager.GetActiveScene();
        if (!Application.isPlaying || scene.path != ScenePath)
            throw new InvalidOperationException("Run the capture check in Play Mode in Dynamo.");
        if (captureCheckRunning) throw new InvalidOperationException("A Dynamo capture check is already running.");
        captureCheckRunning = true;
        captureCompleted = false;
        captureSceneHandle = scene.handle;
        capturedCoreId = capturedPatternId = pairsAtCapture = 0;
        captureEvidence = "";
        captureDeadline = EditorApplication.timeSinceStartup + 55d;
        CaptureCheckResult = "RUNNING: waiting for Regulation and a fully spawned managed Core (55-second realtime limit).";
        EditorApplication.update += TickCaptureCheck;
        EditorApplication.playModeStateChanged += OnCaptureCheckPlayState;
        AssemblyReloadEvents.beforeAssemblyReload += OnCaptureCheckAssemblyReload;
    }

    static void OnCaptureCheckPlayState(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
            FinishCaptureCheck("CANCELLED: Play Mode stopped. " + captureEvidence);
    }

    static void OnCaptureCheckAssemblyReload() => FinishCaptureCheck("CANCELLED: scripts reloaded. " + captureEvidence);

    static void FinishCaptureCheck(string result)
    {
        captureCheckRunning = false;
        CaptureCheckResult = result;
        EditorApplication.update -= TickCaptureCheck;
        EditorApplication.playModeStateChanged -= OnCaptureCheckPlayState;
        AssemblyReloadEvents.beforeAssemblyReload -= OnCaptureCheckAssemblyReload;
    }

    static void TickCaptureCheck()
    {
        if (!captureCheckRunning) return;
        try
        {
            var scene = SceneManager.GetActiveScene();
            if (!Application.isPlaying || scene.handle != captureSceneHandle || scene.path != ScenePath)
            {
                FinishCaptureCheck("CANCELLED: Dynamo gameplay scene changed. " + captureEvidence);
                return;
            }
            if (EditorApplication.timeSinceStartup >= captureDeadline)
            {
                FinishCaptureCheck("FAIL: capture/cycle check exceeded 55 realtime seconds. " + CaptureCheckResult);
                return;
            }
            var match = All<GameManagerScript>(scene).SingleOrDefault();
            var spawner = All<AmplifierResonanceSpawner>(scene).SingleOrDefault();
            var scores = All<MatchScoreService>(scene).SingleOrDefault();
            var toast = All<TeamAmplifierToastPresenter>(scene).SingleOrDefault();
            var goal = All<AmplifierGoalCapture>(scene).SingleOrDefault(g => g.TeamID == 1);
            if (!match || !spawner || !scores || !toast || !goal)
            {
                FinishCaptureCheck("FAIL: capture check requires one local match, spawner, score service, toast presenter and team 1 goal.");
                return;
            }
            if (match.IsStartupBlocked || match.Phase == MatchRuntimePhase.Resolving || match.Phase == MatchRuntimePhase.Complete)
            {
                FinishCaptureCheck("FAIL: match cannot finish capture check in " + match.Phase + "; startup=" + match.StartupFailureReason + ". " + captureEvidence);
                return;
            }
            if (match.Phase != MatchRuntimePhase.Regulation)
            {
                CaptureCheckResult = "RUNNING: waiting for Regulation; current=" + match.Phase + ". " + captureEvidence;
                return;
            }
            if (!captureCompleted)
            {
                var core = spawner.ActiveCore;
                if (spawner.Phase != AmplifierEncounterPhase.Active || !core || core.IsSpawning || core.IsDespawning ||
                    core.HasBeenCaptured || core.IsInExternalTransit || !spawner.ActivePattern)
                {
                    CaptureCheckResult = "RUNNING: waiting for a ready managed Core; phase=" + spawner.Phase + "; status=" + spawner.Status;
                    return;
                }
                if (!scores.IsScoringOpen || scores.IsTeamAmplifierMaxed(1))
                {
                    FinishCaptureCheck("FAIL: scoring must be open and team 1 must have room for another amplifier tier.");
                    return;
                }
                int multiplierBefore = scores.GetTeamAmplifierMultiplier(1);
                int capturesBefore = spawner.CapturesObserved;
                pairsAtCapture = spawner.PairsSpawned;
                capturedCoreId = core.GetInstanceID();
                capturedPatternId = spawner.ActivePattern.GetInstanceID();
                bool accepted = core.TryCapture(goal);
                int multiplierAfter = scores.GetTeamAmplifierMultiplier(1);
                int capturesAfter = spawner.CapturesObserved;
                captureEvidence = "TryCapture=" + accepted + "; team 1 multiplier=" + multiplierBefore + " -> " + multiplierAfter +
                    "; captures=" + capturesBefore + " -> " + capturesAfter + "; immediateToasts=" + toast.ActiveToastCount +
                    "; retirementPhase=" + spawner.Phase + ". ";
                if (!accepted || !core.HasBeenCaptured || multiplierAfter <= multiplierBefore || capturesAfter != capturesBefore + 1 ||
                    toast.ActiveToastCount <= 0 || spawner.Phase != AmplifierEncounterPhase.Dissolving)
                {
                    FinishCaptureCheck("FAIL: actual capture did not propagate to all shared services. " + captureEvidence);
                    return;
                }
                captureCompleted = true;
                CaptureCheckResult = "RUNNING: capture passed; waiting for the normal next pair. " + captureEvidence;
                return;
            }
            var nextCore = spawner.ActiveCore;
            var nextPattern = spawner.ActivePattern;
            if (spawner.PairsSpawned > pairsAtCapture && spawner.Phase == AmplifierEncounterPhase.Active && nextCore && nextPattern &&
                nextCore.GetInstanceID() != capturedCoreId && nextPattern.GetInstanceID() != capturedPatternId &&
                nextCore.isActiveAndEnabled && nextPattern.isActiveAndEnabled && !nextCore.IsSpawning && !nextCore.IsDespawning && !nextCore.HasBeenCaptured)
            {
                bool local = nextCore.gameObject.scene == scene && nextPattern.gameObject.scene == scene && spawner.spawnRegion &&
                    nextPattern.arenaBounds == spawner.spawnRegion.arenaBounds && nextPattern.arenaBounds &&
                    nextPattern.grid == nextPattern.arenaBounds.Grid;
                FinishCaptureCheck((local ? "PASS: " : "FAIL: ") + captureEvidence + "Next fully spawned pair=" + spawner.PairsSpawned +
                    " (previous=" + pairsAtCapture + "); new Core/pattern instances; local grid/bounds=" + local + ".");
                return;
            }
            CaptureCheckResult = "RUNNING: waiting for natural retirement/respawn; phase=" + spawner.Phase + "; pairs=" + spawner.PairsSpawned + ". " + captureEvidence;
        }
        catch (Exception error)
        {
            FinishCaptureCheck("FAIL: capture check exception: " + error + "\n" + captureEvidence);
        }
    }

    public static string Inspect() => Capture(false).Text;

    /// <summary>Call once the real match reaches Regulation. No clocks, scores or scene state are changed.</summary>
    public static string ValidateRegulation()
    {
        var report = Capture(true);
        if (report.Failures > 0) throw new InvalidOperationException(report.Text);
        return report.Text;
    }

    sealed class Report
    {
        readonly StringBuilder lines = new StringBuilder();
        public int Failures;
        public int Waiting;
        public string Text => "Dynamo scene runtime: " + Failures + " failures, " + Waiting + " waiting observations\n" + lines;
        public void Note(string text) => lines.AppendLine("INFO " + text);
        public void Check(bool condition, string text)
        {
            if (!condition) Failures++;
            lines.AppendLine((condition ? "PASS " : "FAIL ") + text);
        }
        public void Wait(string text) { Waiting++; lines.AppendLine("WAIT " + text); }
    }

    static T[] All<T>(Scene scene) where T : Component => scene.IsValid() && scene.isLoaded
        ? scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray()
        : Array.Empty<T>();

    static T One<T>(Scene scene, Report report) where T : Component
    {
        var values = All<T>(scene);
        report.Check(values.Length == 1, typeof(T).Name + " scene count=" + values.Length);
        return values.Length == 1 ? values[0] : null;
    }

    static Object Reference(Object owner, string name)
    {
        if (!owner) return null;
        var property = new SerializedObject(owner).FindProperty(name);
        return property != null && property.propertyType == SerializedPropertyType.ObjectReference
            ? property.objectReferenceValue : null;
    }

    static string Label(Object value)
    {
        if (!value) return "null";
        var component = value as Component;
        var transform = component ? component.transform : (value as GameObject)?.transform;
        return transform ? AnimationUtility.CalculateTransformPath(transform, null) : value.name;
    }

    static Report Capture(bool requireRegulation)
    {
        var report = new Report();
        var scene = SceneManager.GetActiveScene();
        bool playing = Application.isPlaying;
        report.Note("scene=" + scene.path + "; playing=" + playing + "; timeScale=" + Time.timeScale);
        report.Check(scene.path == ScenePath, "Active scene is Dynamo");
        if (scene.path != ScenePath) return report;
        if (requireRegulation) report.Check(playing, "Play Mode is running");
        else if (!playing) report.Wait("Edit Mode: runtime startup has not been evaluated");

        var context = One<LevelSceneContext>(scene, report);
        var match = One<GameManagerScript>(scene, report);
        var scores = One<MatchScoreService>(scene, report);
        var roster = One<PlayerRosterController>(scene, report);
        var anomalies = One<AnomalyManager>(scene, report);
        var enemy = One<EnemyDirector>(scene, report);
        var resonance = One<AmplifierResonanceSpawner>(scene, report);
        var timer = One<MatchTimerPresenter>(scene, report);
        var toast = One<TeamAmplifierToastPresenter>(scene, report);
        var storm = One<DynamoStormController>(scene, report);
        var flow = One<DynamoFlowBlanketRenderer>(scene, report);
        var bloom = One<DynamoSelectiveBloom>(scene, report);
        var bounds = All<ArenaBoundsFromVectorGrid>(scene).FirstOrDefault(b => b.isActiveAndEnabled);
        report.Check(bounds && bounds.Grid, "Live arena bounds reference the gameplay grid");

        bool regulation = playing && match && match.Phase == MatchRuntimePhase.Regulation;
        if (match)
        {
            report.Note("phase=" + match.Phase + "; remaining=" + match.RegulationRemainingSeconds.ToString("0.0") +
                "; blocked=" + match.IsStartupBlocked + "; reason=" + match.StartupFailureReason);
            report.Check(!match.IsStartupBlocked, "Match startup has no rejection");
            report.Check(Reference(match, "scoreService") == scores && scores, "Match has the explicit scene score authority");
            report.Check(scores && Reference(match, "scoreEconomyProfile") == scores.Profile && scores.Profile,
                "Match and score authority share the economy asset");
        }
        if (requireRegulation) report.Check(regulation, "Match reached Regulation");
        else if (playing && !regulation) report.Wait("Regulation-only assertions deferred during " + (match ? match.Phase.ToString() : "missing match"));
        if (context)
        {
            report.Check(context.level && context.level.levelTitle == "DYNAMO" && context.level.SceneName == scene.name,
                "Dynamo identity resolves the current scene");
            report.Check(context.stage && context.stage.displayName == "DYNAMO" && anomalies && anomalies.stageProfile == context.stage,
                "Context and anomaly runtime share Dynamo stage identity");
            report.Check(context.match == match && match && context.roster == roster && roster && context.anomalies == anomalies && anomalies,
                "Context connects the existing match, roster and anomaly owner");
            report.Check(context.inputManagerPrefab, "Context has the shared input prefab");
            report.Check(context.spawners && context.spawners.transform.parent && context.spawners.transform.parent.CompareTag("GameplayObjects"),
                "Context owns spawners under tagged GameplayObjects");
            report.Check(context.stageTitleText && context.stageNumberText && context.stageTitleText.gameObject.activeInHierarchy &&
                context.stageNumberText.gameObject.activeInHierarchy, "Stage HUD targets are live");
            if (playing && context.level)
            {
                report.Check(GameFlowContext.Instance && GameFlowContext.Instance.SelectedLevel == context.level, "Global session retains Dynamo");
                report.Check(context.stageTitleText && context.stageTitleText.text == context.level.levelTitle && context.stageNumberText &&
                    context.stageNumberText.text == "STAGE_" + context.level.levelNumber.ToString("000"), "Stage HUD shows catalog identity");
                if (match && context.spawners)
                {
                    bool expected = match.Phase == MatchRuntimePhase.Regulation || (match.Phase == MatchRuntimePhase.Bonus && !match.IsTerminalBonus);
                    report.Check(context.spawners.activeSelf == expected, "Spawner activation follows match phase");
                }
            }
        }
        if (anomalies) report.Check(anomalies.match == match && match && anomalies.playerManager,
            "Anomaly owner connects match and player registry");
        if (roster)
        {
            var actors = new[] { roster.P1, roster.P2, roster.P3, roster.P4 };
            report.Check(actors.All(p => p && p.scene == scene) && actors.Distinct().Count() == 4, "Four distinct roster actor references are scene-local");
            report.Note("rosterReady=" + roster.IsRosterReady + "; activeRosterActors=" + actors.Count(p => p && p.activeInHierarchy));
            if (regulation) report.Check(roster.IsRosterReady && !roster.HasStartupFailed, "Roster completed its real spawn sequence");
        }
        if (playing)
        {
            var inputs = Object.FindObjectsByType<Rewired.InputManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var globalScores = Object.FindObjectsByType<MatchScoreService>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var listeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(c => c.enabled);
            report.Note("globalInputCount=" + inputs.Length + "; inputReady=" + Rewired.ReInput.isReady + "; globalScoreCount=" + globalScores.Length + "; activeListeners=" + listeners);
            if (regulation)
            {
                report.Check(inputs.Length == 1 && Rewired.ReInput.isReady, "One ready global input manager");
                report.Check(globalScores.Length == 1 && MatchScoreService.Instance == scores && scores.IsScoringOpen && scores.IsChainClockRunning,
                    "One open scene scoring authority and running chain clock");
                report.Check(listeners == 1, "One active audio listener");
            }
        }
        report.Check(All<Rewired.Initializer>(scene).Length == 0, "No legacy initializer can persist a systems parent");

        if (enemy)
        {
            report.Check(enemy.arenaBounds == bounds && bounds && enemy.arenaLayout && enemy.arenaLayout.arena == bounds,
                "Enemy director and layout use Dynamo bounds");
            report.Check(enemy.spawnProfile && enemy.encounterTimeline && enemy.waitForScoring && enemy.anomalyManager == anomalies &&
                enemy.resonanceSpawner == resonance && resonance && enemy.enemyRoot && enemy.enemyRoot.gameObject.scene == scene,
                "Enemy timeline connects shared match, anomaly and resonance services");
            report.Note("enemyTimeline=" + Label(enemy.encounterTimeline) + "; active=" + enemy.isActiveAndEnabled + "; age=" +
                enemy.GameplayAge.ToString("0.0") + "; totalSpawned=" + enemy.TotalSpawned + "; alive=" + enemy.AliveCount +
                "; pending=" + enemy.TimelinePendingCount + "; finished=" + enemy.TimelineFinished);
            if (regulation) report.Check(enemy.isActiveAndEnabled, "Enemy director is enabled during Regulation");
        }
        if (resonance)
        {
            report.Check(resonance.spawnRegion && resonance.spawnRegion.arenaBounds == bounds && bounds && resonance.scoreService == scores && scores,
                "Amplifier/Resonance uses Dynamo bounds and the scene score service");
            report.Check(resonance.corePrefab && !resonance.corePrefab.IsPresentationOnly && resonance.patternOrder != null &&
                resonance.patternOrder.Any(p => p != null && p.patternPrefab && p.patternPrefab.definition), "Gameplay core and at least one valid shared pattern are configured");
            report.Note("resonancePhase=" + resonance.Phase + "; status=" + resonance.Status + "; pairs=" + resonance.PairsSpawned +
                "; captures=" + resonance.CapturesObserved + "; pattern=" + Label(resonance.ActivePattern) + "; core=" + Label(resonance.ActiveCore));
            if (regulation) report.Check(resonance.isActiveAndEnabled, "Amplifier/Resonance cycle is enabled during Regulation");
            if (resonance.ActivePattern)
                report.Check(resonance.ActivePattern.gameObject.scene == scene && resonance.ActivePattern.arenaBounds == bounds &&
                    bounds && resonance.ActivePattern.grid == bounds.Grid, "Live Resonance pattern is fitted to Dynamo's grid and bounds");
            else if (regulation) report.Wait("No active pattern at this cycle phase; sample again after the spawn delay");
            if (resonance.ActiveCore)
                report.Check(resonance.ActiveCore.gameObject.scene == scene && !resonance.ActiveCore.IsPresentationOnly,
                    "Managed amplifier core is a local gameplay actor");
        }
        var goals = All<AmplifierGoalCapture>(scene);
        report.Check(goals.Length == 2 && goals.Select(g => g.TeamID).OrderBy(i => i).SequenceEqual(new[] { 1, 2 }), "Both team amplifier goals exist");
        foreach (var goal in goals) report.Check(Reference(goal, "arenaBounds") == bounds && bounds, "Goal " + goal.TeamID + " has Dynamo bounds");
        if (timer) report.Check(Reference(timer, "gameManager") == match && match && Reference(timer, "timerText") && Reference(timer, "phaseText"),
            "Timer presenter connects match and both HUD labels");
        if (toast) report.Note("captureToasts=" + toast.ActiveToastCount + "; maximumNotices=" + toast.ActiveMaxNoticeCount);

        var field = storm ? storm.GameplayField : null;
        report.Check(field && field.gameObject.scene == scene && field.fieldCS && field.lineMaterial, "Storm owns the original GPU field with its resources");
        if (field) report.Note("fieldActive=" + field.isActiveAndEnabled + "; canDraw=" + field.CanRecordBloom + "; presentationOnly=" + field.ExternalPresentation);
        if (storm)
        {
            report.Check(storm.UseFlowBlanket && storm.StormCamera && storm.StormCamera.gameObject.scene == scene && Reference(storm, "flowBlanket") == flow && flow,
                "Storm owns the flow blanket and gameplay camera");
            report.Note("stormActive=" + storm.StormActive + "; stormCount=" + storm.StormCount + "; strength=" + storm.StormStrength01.ToString("0.000"));
        }
        if (flow)
        {
            report.Check(flow.controller == storm && storm && flow.field == field && field && flow.flowCompute && flow.flowMaterial,
                "Flow blanket connects the same storm, field and GPU resources");
            report.Note("flowActive=" + flow.isActiveAndEnabled + "; buffers=" + flow.HasBuffers + "; drawing=" + flow.CanRecordBloom + "; sharpCore=" + flow.HasCoreCommands);
        }
        if (bloom)
        {
            report.Check(bloom.field == field && field && bloom.flow == flow && flow && storm && bloom.targetCamera == storm.StormCamera && bloom.bloomShader,
                "Selective bloom connects both gameplay sources and the same camera");
            report.Note("bloomActive=" + bloom.isActiveAndEnabled + "; commandBuffer=" + bloom.HasCommandBuffer + "; channels=" + bloom.RecordedChannels);
        }
        if (regulation) report.Check(field && field.isActiveAndEnabled && !field.ExternalPresentation && flow && flow.isActiveAndEnabled && !flow.ExternalPresentation,
            "Gameplay field and flow run their own simulation during Regulation");
        var pushes = All<PlayerStormPush>(scene);
        report.Note("PlayerStormPush count=" + pushes.Length);
        foreach (var push in pushes)
        {
            var player = Reference(push, "player") as PlayerControllerScript;
            report.Check(Reference(push, "storm") == storm && storm && player && player.gameObject.scene == scene &&
                (push.transform == player.transform || push.transform.IsChildOf(player.transform)) && !Reference(push, "scientificField"),
                "PlayerStormPush owns its player and gameplay storm: " + Label(push));
        }
        if (roster)
            foreach (var actor in new[] { roster.P1, roster.P2, roster.P3, roster.P4 }.Where(p => p))
                report.Check(actor.GetComponentsInChildren<PlayerStormPush>(true).Length == 1, "One storm push adapter on " + actor.name);
        return report;
    }
}
#endif
