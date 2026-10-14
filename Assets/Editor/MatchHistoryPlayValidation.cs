#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Massive.Scoring;
using Massive.Statistics;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Real gameplay scenes with disposable runtime changes; Unity restores the user's edit-mode scene on exit.</summary>
[InitializeOnLoad]
public static class MatchHistoryPlayValidation
{
    private const string Key = "MASSIVE.MatchHistoryPlayValidation";
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();
    private static readonly List<string> checks = new List<string>();
    private static readonly List<string> errors = new List<string>();
    private static MatchHistoryStore store;
    private static double deadline;
    private static int frame;
    private static bool previousBackground;
    public static string Status => SessionState.GetString(Key + ".status", "Not run");

    static MatchHistoryPlayValidation() { EditorApplication.playModeStateChanged += OnState; }

    [MenuItem("MASSIVE/Statistics/Validate Gameplay Recording")]
    public static void Start()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start gameplay validation from Edit Mode.");
        SessionState.SetString(Key + ".scene", SceneManager.GetActiveScene().path);
        SessionState.SetBool(Key + ".dirty", SceneManager.GetActiveScene().isDirty);
        SessionState.SetString(Key + ".folder", Path.Combine(Path.GetTempPath(), "MASSIVE-MatchHistory-Play-" + Guid.NewGuid().ToString("N")));
        SessionState.SetBool(Key, true);
        SessionState.SetString(Key + ".status", "Entering Play Mode");
        EditorApplication.isPlaying = true;
    }

    private static void OnState(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            checks.Clear(); errors.Clear(); routines.Clear();
            store = new MatchHistoryStore(SessionState.GetString(Key + ".folder", ""));
            // Redirect only this disposable play session; SubsystemRegistration resets it next session.
            typeof(MatchHistoryRuntime).GetField("store", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, store);
            previousBackground = Application.runInBackground;
            Application.runInBackground = true;
            deadline = EditorApplication.timeSinceStartup + 300;
            frame = -1;
            Application.logMessageReceived += OnLog;
            routines.Push(Scenarios());
            EditorApplication.update += Tick;
            SessionState.SetString(Key + ".status", "Running gameplay scenarios");
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            bool preserved = SceneManager.GetActiveScene().path == SessionState.GetString(Key + ".scene", "") &&
                SceneManager.GetActiveScene().isDirty == SessionState.GetBool(Key + ".dirty", false);
            string message = preserved ? "PASS Original scene and unsaved-state flag restored" : "FAIL Original scene state was not restored";
            string report = Path.Combine(SessionState.GetString(Key + ".folder", ""), "report.txt");
            File.AppendAllText(report, "\n" + message);
            if (!preserved) SessionState.SetString(Key + ".status", "FAILED: edit-mode scene restoration");
            SessionState.SetBool(Key, false);
            if (Application.isBatchMode) EditorApplication.Exit(preserved && Status.StartsWith("PASSED", StringComparison.Ordinal) ? 0 : 1);
        }
    }

    private static void OnLog(string message, string trace, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + trace); }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) { Finish(false, "Play Mode ended before validation finished."); return; }
        if (Time.frameCount == frame) return;
        frame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Gameplay validation timed out.");
            while (routines.Count > 0)
            {
                var top = routines.Peek();
                if (!top.MoveNext()) { routines.Pop(); continue; }
                if (top.Current is IEnumerator nested) { routines.Push(nested); continue; }
                return;
            }
            Check(errors.Count == 0, "No runtime errors or exceptions");
            Finish(true, "");
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }

    private static void Finish(bool pass, string detail)
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        routines.Clear();
        Application.runInBackground = previousBackground;
        Time.timeScale = 1;
        Directory.CreateDirectory(store.DirectoryPath);
        string result = (pass ? "PASSED " : "FAILED after ") + checks.Count + " checks";
        File.WriteAllText(Path.Combine(store.DirectoryPath, "report.txt"), result + "\n" + string.Join("\n", checks) + "\n" + detail + "\n" + string.Join("\n", errors));
        SessionState.SetString(Key + ".status", result + " — " + store.DirectoryPath + (pass ? "" : " — " + detail));
        if (Application.isBatchMode)
        {
            Directory.CreateDirectory("Library/MatchHistoryValidation");
            File.WriteAllText("Library/MatchHistoryValidation/result.txt", SessionState.GetString(Key + ".status", ""));
            File.Copy(Path.Combine(store.DirectoryPath, "report.txt"), "Library/MatchHistoryValidation/report.txt", true);
        }
        EditorApplication.isPlaying = false;
    }

    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException(label);
        checks.Add("PASS " + label);
    }

    private static object Read(object target, string field) => target.GetType().GetField(field, Fields).GetValue(target);
    private static void Write(object target, string field, object value) => target.GetType().GetField(field, Fields).SetValue(target, value);
    private static IEnumerator Until(Func<bool> predicate, double timeout, string label)
    {
        double end = EditorApplication.timeSinceStartup + timeout;
        while (!predicate()) { if (EditorApplication.timeSinceStartup >= end) throw new TimeoutException(label); yield return null; }
    }
    private static IEnumerator Seconds(double duration)
    { double end = Time.realtimeSinceStartupAsDouble + duration; while (Time.realtimeSinceStartupAsDouble < end) yield return null; }

    private static IEnumerator Load(string title, GameMode mode)
    {
        var configuration = Resources.Load<MatchHistoryConfiguration>("MatchHistoryConfiguration");
        var level = configuration.catalog.Levels.Single(l => l.levelTitle == title);
        GameFlowContext.EnsureExists();
        GameFlowContext.Instance.SetMode(mode);
        GameFlowContext.Instance.SelectLevel(level);
        SceneFlow.GoToSelectedGameplay();
        yield return Until(() => Object.FindFirstObjectByType<GameManagerScript>(), 20, "Match scene loads");
        var manager = Object.FindFirstObjectByType<GameManagerScript>();
        Write(manager, "autoReturnOnAllPlayersInactive", false);
        yield return Until(() => manager && manager.Phase == MatchRuntimePhase.Regulation, 25, "Roster/countdown reaches regulation");
        Check(!manager.IsStartupBlocked, title + " roster starts correctly in " + mode);
    }

    private static IEnumerator Scenarios()
    {
        Check(store.ReadAll().Count == 0, "No playcount from the menu/instructions scene");
        yield return Complete("HIGGS", GameMode.OneVOne, false);
        yield return Complete("HIGGS", GameMode.TwoVTwo, false);
        yield return Complete("NOVA", GameMode.TwoVTwo, true);
        yield return Load("HIGGS", GameMode.OneVOne);
        var manager = Object.FindFirstObjectByType<GameManagerScript>();
        var recorder = (MatchHistoryRecorder)Read(manager, "_history");
        string id = recorder.Record.matchId;
        foreach (var player in Object.FindObjectsByType<PlayerControllerScript>(FindObjectsSortMode.None)) player.isActive = false;
        Write(manager, "allPlayersInactiveHoldSeconds", .05f);
        Write(manager, "autoReturnOnAllPlayersInactive", true);
        yield return Until(() => SceneManager.GetActiveScene().name == SceneFlow.ChooseModeScene, 15, "Inactive cabinet returns to Attract");
        Check(store.ReadAll().Single(r => r.matchId == id).status == MatchHistoryStatus.Abandoned, "Inactive-cabinet path persists abandonment");
        yield return Load("HIGGS", GameMode.TwoVTwo);
        recorder = (MatchHistoryRecorder)Read(Object.FindFirstObjectByType<GameManagerScript>(), "_history"); id = recorder.Record.matchId;
        SceneFlow.GoToChooseMode();
        yield return Until(() => SceneManager.GetActiveScene().name == SceneFlow.ChooseModeScene, 15, "Unexpected scene exit completes");
        yield return null;
        Check(store.ReadAll().Single(r => r.matchId == id).status == MatchHistoryStatus.Interrupted, "Scene unload persists an interrupted partial result");
        var records = store.ReadAll();
        Check(records.Count == 5 && records.Count(r => r.status == MatchHistoryStatus.Completed) == 3, "Five starts produce exactly three completions, one abandonment and one interruption");
        Check(records.All(r => r.development && !r.HighScoreEligible), "All validation matches remain ineligible for production high scores");
        MatchHistoryReport.Export(Path.Combine(store.DirectoryPath, "export"), records);
        Check(Directory.GetFiles(Path.Combine(store.DirectoryPath, "export"), "*.csv").Length == 4, "Real match records export into all four CSV tables");
    }

    private static IEnumerator Complete(string title, GameMode mode, bool bonus)
    {
        yield return Load(title, mode);
        var manager = Object.FindFirstObjectByType<GameManagerScript>();
        var recorder = (MatchHistoryRecorder)Read(manager, "_history");
        Check(recorder != null, title + " creates a recorder at regulation start");
        string id = recorder.Record.matchId;
        Check(store.ReadAll().Single(r => r.matchId == id).status == MatchHistoryStatus.InProgress, title + " start reaches disk");
        var player = Object.FindObjectsByType<PlayerControllerScript>(FindObjectsSortMode.None).First(p => p.playerID == 0 && p.ParticipatesInMatch);
        Check(manager.ScoreService.TryAwardToPlayer(ScoreRewardKeys.EnemyDefeat, player, id, player.transform.position, out _), title + " accepts a normal attributed score");
        yield return Seconds(.4);
        if (!bonus)
        {
            double active = recorder.Record.activeRegulationSeconds;
            Time.timeScale = 0;
            yield return Seconds(.4);
            Check(Math.Abs(recorder.Record.activeRegulationSeconds - active) < .05, "Paused regulation adds no active gameplay time");
            Time.timeScale = 1;
        }
        else
        {
            Object.FindFirstObjectByType<NovaStarController>().entryWindowDuration = .5f;
            Object.FindFirstObjectByType<NovaAnomalyAdapter>().overrideDuration = 4f;
        }
        // Accelerate the fixture by expiring the remaining clock; do not alter any authored asset.
        Write(manager, "_regulationRemainingSeconds", .01f);
        if (bonus)
        {
            yield return Until(() => manager.Phase == MatchRuntimePhase.Bonus, 20, "NOVA enters bonus");
            Check(Time.timeScale == 0 && manager.ScoreService.IsScoringOpen, "NOVA uses scoring-active frozen world");
            var bridge = Object.FindFirstObjectByType<NovaMinigameScoring>();
            yield return Until(() => bridge && (bool)Read(bridge, "accepting"), 12, "NOVA accepts capture interactions after intro");
            Check(bridge && bridge.TryAwardCapture(player, 987654, 3, out _), "NOVA bonus score is credited");
            yield return Seconds(.5);
            Check(recorder.Record.activeBonusSeconds > .25, "Frozen-world bonus accumulates active play time");
        }
        yield return Until(() => SceneManager.GetActiveScene().name == SceneFlow.PostGameScene, 45, title + " reaches PostGame");
        var snapshot = GameFlowContext.Instance.LastMatchResult;
        var saved = store.ReadAll().Single(r => r.matchId == id);
        Check(saved.status == MatchHistoryStatus.Completed && saved.result.mode == mode && saved.result.stageTitle == title, title + " completion retains level and mode");
        Check(saved.result.LightScore == snapshot.LightScore && saved.result.DarkScore == snapshot.DarkScore && saved.result.winner == snapshot.winner, title + " saved score/outcome equals PostGame");
        Check(saved.result.playerStatistics.Length == (mode == GameMode.TwoVTwo ? 4 : 2) && saved.result.playerContributions.Length > 0, title + " active roster stats and contributions are preserved");
        Check(saved.result.lightAmplifier.multiplierSeconds == snapshot.lightAmplifier.multiplierSeconds && saved.result.scoreTelemetry.Length > 0, title + " multiplier and reward snapshots are preserved");
        if (bonus) Check(saved.result.hasBonusBreakdown && saved.result.bonusLightMilliElectronVolts > 0 && saved.activeBonusSeconds > 0, "NOVA final bonus breakdown survives scene transition");
        Check(saved.elapsedSeconds >= saved.ActiveSeconds - .1 && saved.activeRegulationSeconds > 0, title + " active/elapsed clocks are plausible");
        SceneFlow.GoToChooseMode();
        yield return Until(() => SceneManager.GetActiveScene().name == SceneFlow.ChooseModeScene, 15, "Return to Attract");
        Check(store.ReadAll().Single(r => r.matchId == id).revision == saved.revision, title + " results cleanup cannot record completion twice");
    }
}
#endif
