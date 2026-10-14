#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Massive.Enemies;
using Massive.Levels;
using Massive.PowerUps;
using Massive.Scoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Exercises real scene reloads across three Play sessions without reloading the domain.
/// Changes only disposable runtime instances; never saves scenes, assets, or player preferences.</summary>
public static class FastPlayModeValidation
{
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly List<string> checks = new();
    private static readonly List<string> errors = new();
    private static readonly Stopwatch startup = new();
    private static IEnumerator routine;
    private static LevelCatalog catalog;
    private static bool running, completedSession, failed, background, pendingSession, includeGameplay;
    private static int session, lastFrame;
    private static double deadline;
    private static string reportPath, originalScene;
    public static string Status { get; private set; } = "Not run";

    [MenuItem("MASSIVE/Validation/Validate Fast Play Mode")]
    public static void Run() => Run("Library/FastPlayModeValidation.txt");

    public static string Run(string outputPath, bool gameplay = true)
    {
        if (running || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Start in Edit Mode with no validation running.");
        if (!EditorSettings.enterPlayModeOptionsEnabled ||
            EditorSettings.enterPlayModeOptions != EnterPlayModeOptions.DisableDomainReload)
            throw new InvalidOperationException("Select Reload Scene only in the Editor settings first.");
        var scene = SceneManager.GetActiveScene();
        if (SceneManager.sceneCount != 1 || scene.isDirty || scene.name != SceneFlow.LevelSelectScene)
            throw new InvalidOperationException("Start from the saved Level Select scene alone.");
        catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/Scripts/CORE/LevelCatalog.asset");
        if (!catalog || catalog.Count == 0) throw new InvalidOperationException("Level catalog missing.");
        originalScene = scene.path;
        reportPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
        checks.Clear(); errors.Clear(); session = 0; failed = false; running = true;
        includeGameplay = gameplay;
        background = Application.runInBackground;
        Application.logMessageReceived += OnLog;
        EditorApplication.playModeStateChanged += OnState;
        EditorApplication.update += Tick;
        BeginSession();
        return Status;
    }

    private static void BeginSession()
    {
        pendingSession = false;
        // Poison prior-session state so this catches missing reset hooks, not just clean defaults.
        typeof(PowerUpVFXWarmup).GetField("_ran", StaticPrivate).SetValue(null, true);
        typeof(InstructionsScreenController).GetField("_lastFallbackIndex", StaticPrivate).SetValue(null, 7);
        typeof(SceneFlow).GetField("preloadedScene", StaticPrivate).SetValue(null, "stale-session");
        completedSession = false;
        Status = "Entering session " + (session + 1) + "/3";
        Flush(); startup.Restart(); EditorApplication.isPlaying = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CheckStartupResets()
    {
        if (!running) return;
        try
        {
            Check(!(bool)typeof(PowerUpVFXWarmup).GetField("_ran", StaticPrivate).GetValue(null), "VFX warmup resets");
            Check((int)typeof(InstructionsScreenController).GetField("_lastFallbackIndex", StaticPrivate).GetValue(null) == -1,
                "Instructions history resets");
            Check(typeof(SceneFlow).GetField("preloadedScene", StaticPrivate).GetValue(null) == null &&
                typeof(SceneFlow).GetField("gameplayPreload", StaticPrivate).GetValue(null) == null, "Scene preload resets");
        }
        catch (Exception exception) { failed = true; checks.Add(exception.ToString()); }
    }

    private static void OnState(PlayModeStateChange state)
    {
        if (!running) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            checks.Add("Session " + (session + 1) + " entered Play Mode: " + startup.Elapsed.TotalMilliseconds.ToString("F1") + " ms");
            Application.runInBackground = true;
            lastFrame = -1; deadline = EditorApplication.timeSinceStartup + 240;
            routine = Flatten(SessionChecks());
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            routine = null;
            Application.runInBackground = background;
            if (!completedSession || failed) { Finish(false); return; }
            session++;
            // Editor delayCall can wait for an Inspector repaint while Unity is in the background.
            // Use the existing update callback so automated restart checks continue without focus.
            if (session < 3) pendingSession = true;
            else Finish(true);
        }
    }

    private static void Tick()
    {
        if (!running) return;
        try
        {
            if (pendingSession && !EditorApplication.isPlayingOrWillChangePlaymode) { BeginSession(); return; }
            if (!EditorApplication.isPlaying || routine == null) return;
            if (failed || errors.Count != 0) throw new Exception("Runtime validation reported errors.");
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException(Status);
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            if (routine.MoveNext()) return;
            completedSession = true; routine = null; EditorApplication.isPlaying = false;
        }
        catch (Exception exception)
        {
            failed = true; checks.Add("FAIL " + exception); routine = null;
            Flush(); EditorApplication.isPlaying = false;
        }
    }

    private static IEnumerator SessionChecks()
    {
        yield return null; yield return null;
        Check(GameFlowContext.Instance && GameFlowContext.Instance.Mode == GameMode.OneVOne &&
            !GameFlowContext.Instance.HasLastMatchResult, "Fresh mode and match result");
        CheckMenu();
        var carousel = Object.FindFirstObjectByType<LevelCarouselController>();
        int index = carousel.SelectedIndex;
        carousel.StepNextStage(); yield return Wait(.4f);
        Check(carousel.SelectedIndex == (index + 1) % catalog.Count, "Carousel advances");
        carousel.StepPrevStage(); yield return Wait(.4f);
        Check(carousel.SelectedIndex == index, "Carousel returns");
        if (session == 2 || !includeGameplay)
        {
            GameFlowContext.Instance.SetMode(GameMode.TwoVTwo);
            GameFlowContext.Instance.SetLastMatchResult(new MatchResult { stageTitle = "RESET-PROBE" });
            yield break;
        }

        bool two = session == 1;
        GameFlowContext.Instance.SetMode(two ? GameMode.TwoVTwo : GameMode.OneVOne);
        for (int i = 0; i < catalog.Count; i++)
        {
            var level = catalog.Get(i);
            Status = "Session " + (session + 1) + ": " + level.levelTitle + (two ? " 2v2" : " 1v1");
            Flush(); GameFlowContext.Instance.SelectLevel(level);
            if (i == 0)
            {
                SceneFlow.GoToInstructions();
                yield return Until(() => SceneManager.GetActiveScene().name == SceneFlow.InstructionsScene, 30, "Instructions load");
                yield return Until(() => SceneFlow.IsSelectedGameplayReady, 45, "Gameplay preload completes");
            }
            SceneFlow.GoToSelectedGameplay();
            yield return Until(() => SceneManager.GetActiveScene().name == level.SceneName, 45, "Gameplay loads");
            yield return null; yield return null;
            var match = Object.FindFirstObjectByType<GameManagerScript>();
            Check(match, "Match owner exists");
            Set(match, "autoReturnOnAllPlayersInactive", false);
            yield return Until(() => match.Phase == MatchRuntimePhase.Regulation || match.IsStartupBlocked, 25, "Countdown finishes");
            Check(!match.IsStartupBlocked, "Roster startup succeeds: " + match.StartupFailureReason);
            var players = PlayerControllerScript.ActivePlayers.Where(p => p && !p.IsPseudoPlayer).ToArray();
            Check(players.Length == (two ? 4 : 2) && players.All(p => !p.IsMatchInputLocked && !p.IsSpawning), "Correct unlocked player roster");
            Check(PlayerControllerScript.ActivePlayers.All(p => p && p.gameObject.scene == match.gameObject.scene), "No stale player registrations");
            CheckServices();
            var score = match.ScoreService;
            Check(score == MatchScoreService.Instance && score.IsScoringOpen &&
                Object.FindObjectsByType<MatchScoreService>(FindObjectsSortMode.None).Length == 1, "Single active score authority");
            Check(Time.timeScale == 1 && !SceneSfxPolicy.IsSuppressed && !AudioListener.pause, "World time and audio restored");
            var player = players.First(p => p.teamID == 1);
            long before = score.LightScoreMilliElectronVolts;
            const string token = "FAST-PLAY-RESTART-VALIDATION";
            Check(score.TryAwardToPlayer(ScoreRewardKeys.EnemyDefeat, player, token, player.transform.position, out _) &&
                score.LightScoreMilliElectronVolts > before, "Fresh scoring accepts reused cross-scene token");
            Check(!score.TryAwardToPlayer(ScoreRewardKeys.EnemyDefeat, player, token, player.transform.position, out _), "Duplicate award rejected within match");
            // PULSAR's authored scene currently keeps the rhythm prototype inactive.
            Check(PulsarRhythmSession.Instance == Object.FindFirstObjectByType<PulsarRhythmSession>(), "Rhythm singleton matches active scene");
            yield return Wait(.3f);
            Check(EnemyBase.ActiveEnemies.All(e => e && e.gameObject.scene == match.gameObject.scene), "No stale enemy registrations");
            if (level.levelTitle == "NOVA")
            {
                Check(match.BeginBonusRound(), "Can enter bonus");
                Check(Time.timeScale == 0, "Bonus pauses world before scene change");
            }
            if (i == catalog.Count - 1)
            {
                Set(match, "_regulationRemainingSeconds", .05f);
                yield return Until(() => SceneManager.GetActiveScene().name == SceneFlow.PostGameScene, 15, "Match reaches PostGame");
                Check(GameFlowContext.Instance.HasLastMatchResult && GameFlowContext.Instance.LastMatchResult.stageTitle == level.levelTitle,
                    "Result reaches PostGame");
            }
        }
        SceneFlow.GoToLevelSelect();
        yield return Until(() => SceneManager.GetActiveScene().name == SceneFlow.LevelSelectScene, 30, "Return to carousel");
        yield return null; yield return null;
        CheckMenu();
        Check(PlayerControllerScript.ActivePlayers.Count == 0 && EnemyBase.ActiveEnemies.Count == 0 &&
            PowerUpPickup.ActivePickups.Count == 0 && !MatchScoreService.Instance && !PulsarRhythmSession.Instance,
            "Gameplay registries and scene singletons cleaned up");
        Check(Time.timeScale == 1 && !AudioListener.pause, "Menu time and audio restored");
        // Leave deliberately different state for the next Play session to discard.
        GameFlowContext.Instance.SetMode(GameMode.TwoVTwo);
    }

    private static void CheckMenu()
    {
        CheckServices();
        Check(Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t => t.name.StartsWith("LevelIcon_")) == catalog.Count,
            "Exactly one carousel icon per catalog level");
        Check(Object.FindObjectsByType<GameFlowContext>(FindObjectsSortMode.None).Length == 1, "Single flow context");
        Check(Object.FindObjectsByType<LiveParticleIcon>(FindObjectsSortMode.None).All(icon =>
            icon.GetType().GetField("texture", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(icon) is RenderTexture rt && rt.IsCreated()),
            "Live particle icon textures recreated");
    }

    private static void CheckServices()
    {
        Check(Rewired.ReInput.isReady && Object.FindObjectsByType<Rewired.InputManager>(FindObjectsSortMode.None).Length == 1, "Single ready input manager");
        Check(BGMManager.Instance && Object.FindObjectsByType<BGMManager>(FindObjectsSortMode.None).Length == 1, "Single music manager");
        Check(Object.FindObjectsByType<AudioSystem>(FindObjectsSortMode.None).Length <= 1, "No duplicate SFX service");
        Check(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(a => a.isActiveAndEnabled) == 1, "Single audio listener");
    }

    private static void Check(bool ok, string description)
    {
        checks.Add((ok ? "PASS " : "FAIL ") + "session " + (session + 1) + ": " + description);
        if (!ok) throw new InvalidOperationException(description);
    }
    private static void Set(object owner, string field, object value) =>
        owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
    private static IEnumerator Until(Func<bool> condition, float seconds, string label)
    {
        double end = EditorApplication.timeSinceStartup + seconds;
        while (!condition()) { if (EditorApplication.timeSinceStartup > end) throw new TimeoutException(label); yield return null; }
        Check(true, label);
    }
    private static IEnumerator Wait(float seconds)
    {
        double end = EditorApplication.timeSinceStartup + seconds;
        while (EditorApplication.timeSinceStartup < end) yield return null;
    }
    private static IEnumerator Flatten(IEnumerator source)
    {
        while (source.MoveNext())
        {
            if (source.Current is IEnumerator inner)
            { var nested = Flatten(inner); while (nested.MoveNext()) yield return nested.Current; }
            else yield return source.Current;
        }
    }
    private static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack);
    }
    private static void Flush() => File.WriteAllText(reportPath, Status + "\n" + string.Join("\n", checks) + "\nErrors:\n" + string.Join("\n", errors));
    private static void Finish(bool success)
    {
        running = false; pendingSession = false; routine = null;
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= OnState;
        Application.logMessageReceived -= OnLog;
        Application.runInBackground = background;
        bool restored = SceneManager.GetActiveScene().path == originalScene && !SceneManager.GetActiveScene().isDirty;
        Status = success && restored && errors.Count == 0 ? "PASSED" : "FAILED";
        checks.Add("Original clean scene restored: " + restored);
        Flush(); UnityEngine.Debug.Log("Fast Play Mode validation " + Status + ": " + reportPath);
    }
}
#endif
