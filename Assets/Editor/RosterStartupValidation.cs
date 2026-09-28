#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Massive.Scoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Fault injection in disposable Play Mode scene instances; no assets are saved.</summary>
public static class RosterStartupValidation
{
    static readonly string[] Cases = {
        "Awake missing P3", "All bindings missing", "Missing P3", "Missing controller",
        "Missing spawn anchor", "Missing goal", "Duplicate slots", "Wrong slot ID",
        "Demo actor", "Inactive parent", "Disabled controller", "Missing roster",
        "Disabled roster", "Wait bypass", "Countdown binding lost", "Ambiguous fallback",
        "Missing unused 1v1 slots"
    };
    static readonly List<string> results = new();
    static readonly List<string> errors = new();
    static int index, assertions, readyEvents;
    static bool loaded, reachedRegulation, injected, background, running;
    static double deadline, blockedAt;
    static string outputPath, expected;
    static GameManagerScript match;
    static PlayerRosterController roster;
    static PlayerControllerScript player3;
    static float remaining;
    public static string Status { get; private set; } = "Not run";
    static void Set(Object o, string field, object value) => MassiveDemoMigration.Set(o, field, value);
    static void Check(bool condition, string label)
    { if (!condition) throw new InvalidOperationException(Cases[index] + ": " + label); assertions++; }

    public static string StartRun(string resultsPath)
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
        Cleanup(); results.Clear(); index = assertions = 0; outputPath = resultsPath;
        background = Application.runInBackground; Application.runInBackground = true;
        running = true;
        Application.logMessageReceived += OnLog;
        SceneManager.sceneLoaded += OnLoaded;
        EditorApplication.update += Tick;
        Next();
        return Status;
    }
    static void Next()
    {
        DetachSceneEvents();
        loaded = reachedRegulation = injected = false; readyEvents = 0; blockedAt = 0;
        errors.Clear(); match = null; roster = null; player3 = null;
        GameFlowContext.Instance.SetMode(index == Cases.Length - 1 ? GameMode.OneVOne : GameMode.TwoVTwo);
        deadline = EditorApplication.timeSinceStartup + 20;
        Status = "Running " + (index + 1) + "/" + Cases.Length + ": " + Cases[index];
        SceneManager.LoadScene("S-3_NOVA");
    }
    static void OnLog(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message); }
    static void OnLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "S-3_NOVA") return;
        try
        {
            match = Object.FindFirstObjectByType<GameManagerScript>();
            roster = Object.FindFirstObjectByType<PlayerRosterController>();
            player3 = roster.P3.GetComponent<PlayerControllerScript>();
            Set(match, "preMatchCountdownSeconds", .4f);
            Set(roster, "autoAssignByPlayerIdIfMissing", false);
            match.PhaseChanged += OnPhaseChanged;
            expected = "P3 (player3)";
            switch (Cases[index])
            {
                case "Awake missing P3":
                    var slots = new[] { roster.P1, roster.P2, roster.P3, roster.P4 };
                    Object.DestroyImmediate(roster);
                    var root = new GameObject("Startup validation roster"); root.SetActive(false);
                    roster = root.AddComponent<PlayerRosterController>();
                    Set(roster, "autoAssignByPlayerIdIfMissing", false);
                    for (int i = 0; i < 4; i++) Set(roster, "player" + (i + 1), i == 2 ? null : slots[i]);
                    root.SetActive(true);
                    break;
                case "All bindings missing":
                    for (int i = 1; i <= 4; i++) Set(roster, "player" + i, null);
                    break;
                case "Missing P3": Set(roster, "player3", null); break;
                case "Missing controller":
                    roster.P3.SetActive(false);
                    Set(roster, "player3", new GameObject("P3 without controller"));
                    expected = "missing PlayerControllerScript"; break;
                case "Missing spawn anchor": Set(player3, "respawnPointOverride", null); expected = "respawnPointOverride"; break;
                case "Missing goal": player3.goalZone = null; expected = "goalZone"; break;
                case "Duplicate slots": Set(roster, "player3", roster.P1); expected = "duplicates another"; break;
                case "Wrong slot ID": player3.playerID = 0; expected = "playerID is 0; expected 2"; break;
                case "Demo actor": Set(player3, "isPseudoPlayer", true); expected = "demonstration/non-match"; break;
                case "Inactive parent":
                    var parent = new GameObject("Inactive player parent"); parent.SetActive(false);
                    player3.transform.SetParent(parent.transform, true); expected = "inactive"; break;
                case "Disabled controller": player3.enabled = false; expected = "PlayerControllerScript is disabled"; break;
                case "Missing roster": Object.DestroyImmediate(roster); expected = "Missing PlayerRosterController"; break;
                case "Disabled roster": roster.enabled = false; expected = "PlayerRosterController is disabled"; break;
                case "Wait bypass": Set(match, "waitForRosterSpawnCompletion", false); Set(roster, "player3", null); break;
                case "Countdown binding lost": expected = "respawnPointOverride"; break;
                case "Ambiguous fallback":
                    player3.gameObject.SetActive(false);
                    Object.Instantiate(player3.gameObject).name = "Duplicate P3 candidate";
                    player3.gameObject.SetActive(true);
                    Set(roster, "player3", null);
                    typeof(PlayerRosterController).GetMethod("AutoAssignIfMissing", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(roster, null);
                    expected = "auto-assignment is ambiguous"; break;
                case "Missing unused 1v1 slots":
                    Set(roster, "player2", null); Set(roster, "player4", null); break;
            }
            if (roster) roster.RosterReady += OnRosterReady;
            loaded = true;
        }
        catch (Exception e) { Fail(e); }
    }
    static void OnPhaseChanged(MatchRuntimePhase phase)
    {
        if (!running || index >= Cases.Length) return;
        if (phase == MatchRuntimePhase.Regulation) reachedRegulation = true;
        if (Cases[index] == "Countdown binding lost" && phase == MatchRuntimePhase.Countdown && !injected)
        { injected = true; Set(player3, "respawnPointOverride", null); }
    }
    static void OnRosterReady() { if (running) readyEvents++; }
    static void DetachSceneEvents()
    {
        if (match) match.PhaseChanged -= OnPhaseChanged;
        if (roster) roster.RosterReady -= OnRosterReady;
    }
    static void Tick()
    {
        try
        {
            if (!Application.isPlaying) { Cleanup(); return; }
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException(Status);
            if (!loaded || !match) return;
            if (index == Cases.Length - 1)
            {
                if (!reachedRegulation) return;
                Check(!match.IsStartupBlocked && roster.IsRosterReady && readyEvents == 1, "unused slots do not block 1v1");
                Check(match.ScoreService.IsScoringOpen && !roster.P1.GetComponent<PlayerControllerScript>().IsMatchInputLocked, "valid match opens scoring and input");
                Check(errors.Count == 0, "no unexpected error logs");
                CompleteCase(); return;
            }
            if (reachedRegulation) throw new InvalidOperationException(Cases[index] + ": invalid roster reached regulation");
            if (!match.IsStartupBlocked) return;
            if (blockedAt == 0) { blockedAt = EditorApplication.timeSinceStartup; remaining = match.RegulationRemainingSeconds; }
            if (EditorApplication.timeSinceStartup - blockedAt < .75) return;
            Check(!reachedRegulation, "invalid roster never reaches regulation");
            Check(match.Phase == MatchRuntimePhase.Preparing, "blocked phase stays Preparing");
            Check(!match.ScoreService.IsScoringOpen && match.RegulationRemainingSeconds == remaining, "score and regulation clock stay closed");
            Check(match.StartupFailureReason.Contains(expected) && match.StartupFailureReason.Contains("S-3_NOVA"), "diagnostic identifies scene and failed binding");
            Check(errors.Count == 1 && errors[0] == match.StartupFailureReason, "one actionable error, no repeated logs");
            Check(!roster || !roster.IsRosterReady, "invalid roster never remains ready");
            Check(readyEvents == (injected ? 1 : 0), "no premature readiness event");
            Check(Object.FindObjectsByType<PlayerControllerScript>(FindObjectsSortMode.None).Where(p => p.ParticipatesInMatch).All(p => p.IsMatchInputLocked), "match actors remain input-locked");
            if (Cases[index] == "All bindings missing")
                for (int i = 1; i <= 4; i++) Check(match.StartupFailureReason.Contains("P" + i + " (player" + i + ")"), "all missing slots reported");
            if (roster)
            {
                Check(!roster.ValidateForMatch(), "startup rejection is latched until reload");
                Check(errors.Count == 1, "revalidation does not spam errors");
            }
            CompleteCase();
        }
        catch (Exception e) { Fail(e); }
    }
    static void CompleteCase()
    {
        results.Add("PASS: " + Cases[index] + (errors.Count > 0 ? "\n" + errors[0] : ""));
        index++;
        if (index < Cases.Length) { Next(); return; }
        Status = "PASS: " + Cases.Length + " startup scenarios; " + assertions + " assertions. Expected injected failures were captured.";
        WriteResults(); Cleanup();
    }
    static void Fail(Exception e)
    { Status = "FAIL: " + e.Message; WriteResults(); Cleanup(); Debug.LogException(e); }
    static void WriteResults()
    { if (!string.IsNullOrEmpty(outputPath)) File.WriteAllText(outputPath, string.Join("\n\n", results) + "\n\n" + Status); }
    static void Cleanup()
    {
        DetachSceneEvents();
        EditorApplication.update -= Tick; SceneManager.sceneLoaded -= OnLoaded; Application.logMessageReceived -= OnLog;
        if (running) Application.runInBackground = background;
        running = false;
    }
}
#endif
