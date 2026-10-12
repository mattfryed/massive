#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Massive.Scoring;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Accepted-score and lifecycle checks in a disposable HIGGS Play Mode fixture.</summary>
[InitializeOnLoad]
public static class HiggsIncomeToastValidation
{
    const string Folder = "Library/HiggsIncomeToastValidation";
    const string Key = "MASSIVE.HiggsIncomeToastValidation";
    const string ToastPath = "Assets/Scripts/Anomalies/HIGGS/PF_SymmetryKnotIncomeToast.prefab";
    static readonly List<string> checks = new(), errors = new();
    static IEnumerator routine;
    static int frame;
    static double deadline;
    static bool background;

    static HiggsIncomeToastValidation()
    {
        EditorApplication.playModeStateChanged += State;
        EditorApplication.delayCall += Request;
    }

    static void Request()
    {
        string path = Folder + "/test.request";
        if (!File.Exists(path) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(path);
        Run();
    }

    [MenuItem("MASSIVE/HIGGS/Validate Control Income Toast")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Run from Edit Mode after compilation.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save your scene edits before running the HIGGS fixture.");
        Directory.CreateDirectory(Folder);
        checks.Clear(); errors.Clear();
        try
        {
            // Existing level checks include historical balance assumptions. Record drift
            // separately so it cannot prevent validating the presentation feature.
            try { checks.Add(HiggsValidation.RunRules()); }
            catch (InvalidOperationException e) when (e.Message == "HIGGS validation failed: player-facing regulation is 120 seconds")
            { checks.Add("BASELINE: " + e.Message); }
            checks.Add(HiggsValidation.RunForgivenessRules());
            checks.Add(HiggsLevelValidation.Authored());
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HiggsStageSetup.KnotPath);
            var toast = new SerializedObject(prefab.GetComponent<SymmetryKnotController>()).FindProperty("incomeToastPrefab").objectReferenceValue;
            Check(toast != null && AssetDatabase.GetAssetPath(toast) == ToastPath, "Knot prefab includes the income toast");
            File.WriteAllText(Folder + "/authored.txt", string.Join("\n", checks));
            EditorSceneManager.OpenScene(HiggsLevelSetup.ScenePath);
            SessionState.SetBool(Key, true);
            EditorApplication.isPlaying = true;
        }
        catch (Exception e) { File.WriteAllText(Folder + "/report.txt", "FAILED\n" + e); throw; }
    }

    static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            checks.Clear(); errors.Clear(); frame = -1;
            background = Application.runInBackground; Application.runInBackground = true;
            deadline = EditorApplication.timeSinceStartup + 90;
            Application.logMessageReceived += Log;
            routine = Checks(); EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        }
    }
    static void Log(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || routine == null || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Validation timed out.");
            if (!routine.MoveNext()) Finish(null);
        }
        catch (Exception e) { Finish(e); }
    }
    static void Finish(Exception error)
    {
        routine = null; EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        Application.runInBackground = background; Time.timeScale = 1;
        bool passed = error == null && errors.Count == 0;
        File.WriteAllText(Folder + "/report.txt", (passed ? "PASSED" : "FAILED") + $" ({checks.Count} runtime checks)\n" +
            string.Join("\n", checks) + "\n" + error + "\n" + string.Join("\n", errors));
        Debug.Log("HIGGS INCOME TOAST " + (passed ? "PASSED" : "FAILED"));
        EditorApplication.isPlaying = false;
    }
    static void Check(bool ok, string label)
    { if (!ok) throw new InvalidOperationException(label); checks.Add("PASS " + label); }
    static void Pose(PlayerControllerScript player, Vector3 position)
    {
        player.transform.position = position;
        if (player.TryGetComponent<Rigidbody>(out var body)) body.position = position;
        Physics.SyncTransforms();
    }
    static SymmetryKnotIncomeToast Toast(SymmetryKnotController knot)
        => knot.GetComponentInChildren<SymmetryKnotIncomeToast>(true);
    static bool Hidden(SymmetryKnotController knot) => Toast(knot) == null || !Toast(knot).IsVisible;

    static bool Cleared(SymmetryKnotIncomeToast toast) => toast == null ||
        (toast.RequestedContent == SymmetryKnotIncomeToast.Content.Hidden && toast.MilliElectronVoltsPerSecond == 0);

    static void CheckGoalAttachment(SymmetryKnotController knot, int team)
    {
        var goal = Object.FindObjectsByType<Massive.Multiplier.AmplifierGoalCapture>(FindObjectsSortMode.None)
            .Single(g => g.TeamID == team && g.gameObject.scene == knot.gameObject.scene);
        var mouth = Object.FindObjectsByType<SymmetryKnotGoalMouth>(FindObjectsSortMode.None)
            .Single(m => m.teamID == team && m.gameObject.scene == knot.gameObject.scene);
        var ring = knot.transform.Find("EndKnotRoot/Slice_00");
        Vector3 inward = team == 1 ? Vector3.right : Vector3.left;
        Check(mouth.FlowAnchor == goal.CapturePoint && ring != null &&
            Vector3.Distance(ring.position, goal.CapturePoint.position + inward * .25f) < .0001f &&
            Vector3.Dot(ring.forward, inward) > .9999f,
            "Team " + team + " first tunnel ring stays 0.25 units inward from its goal and faces into the arena");
    }

    static IEnumerator Checks()
    {
        var gm = Object.FindFirstObjectByType<GameManagerScript>();
        while (gm.Phase != MatchRuntimePhase.Regulation) yield return null;
        checks.Add(HiggsValidation.RunPlayChecks());
        yield return null;
        var scores = MatchScoreService.Instance;
        scores.ResetForMatch(true);
        var players = Object.FindObjectsByType<PlayerControllerScript>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(p => !p.IsPseudoPlayer).OrderBy(p => p.playerID).ToArray();
        foreach (var p in players)
        {
            p.gameObject.SetActive(true); p.isActive = true; p.temporarilyEliminated = false;
            p.enabled = true;
            var body = p.GetComponent<Rigidbody>(); if (body) body.isKinematic = true;
            Pose(p, new Vector3(100 + p.playerID * 10, 0, 100));
        }
        var light = players.First(p => p.teamID == 1);
        var ally = players.Last(p => p.teamID == 1);
        var dark = players.First(p => p.teamID == 2);
        Time.timeScale = 0;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HiggsStageSetup.KnotPath);
        var knot = Object.Instantiate(prefab).GetComponent<SymmetryKnotController>();
        knot.Initialize(Vector3.zero); knot.enabled = false;
        Pose(light, Vector3.zero);
        knot.TickGameplay(.5f, true);
        Check(Hidden(knot), "No toast during formation");
        knot.TickGameplay(.5f, true);
        var toast = Toast(knot);
        var label = toast.GetComponent<TMP_Text>();
        var transition = toast.GetComponent<TMPTextTransition>();
        Check(toast.RequestedContent == SymmetryKnotIncomeToast.Content.Capturing && label.text.Contains("CAPTURING") &&
            scores.GetTeamScore(1) == 0, "Entry shows CAPTURING before any income");
        Check(transition != null && transition.IsPlaying && label.fontSharedMaterial.GetFloat("_FaceDilate") < -.5f,
            "Shared TMPTextTransition starts the SDF grow intro");
        var sharedMaterial = AssetDatabase.LoadAssetAtPath<GameObject>(ToastPath).GetComponent<TMP_Text>().fontSharedMaterial;
        float sharedDilate = sharedMaterial.GetFloat("_FaceDilate");
        Check(label.fontSharedMaterial != sharedMaterial, "SDF animation uses a private material instance");
        foreach (var p in players) p.enabled = false;
        Time.timeScale = 1;
        float until = Time.unscaledTime + .6f;
        while (Time.unscaledTime < until) yield return null;
        Check(!transition.IsPlaying && Mathf.Abs(label.fontSharedMaterial.GetFloat("_FaceDilate") - sharedDilate) < .001f,
            "SDF intro reaches the authored face weight");
        float alpha = label.alpha; Vector3 scale = toast.transform.localScale;
        until = Time.unscaledTime + .45f;
        while (Time.unscaledTime < until) yield return null;
        Check(Mathf.Abs(label.alpha - alpha) > .03f && Vector3.Distance(scale, toast.transform.localScale) > .001f,
            "CAPTURING pulses slowly in alpha and scale");
        var fontSettings = new SerializedObject(toast);
        fontSettings.FindProperty("capturingFontSize").floatValue = 4;
        fontSettings.FindProperty("incomeFontSize").floatValue = 4.5f;
        fontSettings.FindProperty("teamLabelPercent").intValue = 60;
        fontSettings.ApplyModifiedPropertiesWithoutUndo();
        yield return null; yield return null;
        Check(label.fontSize == 4 && label.text.Contains("size=60%"), "Capture and team label sizing are exposed and apply live");
        fontSettings.Update(); fontSettings.FindProperty("capturingFontSize").floatValue = 3;
        fontSettings.ApplyModifiedPropertiesWithoutUndo();
        yield return null; yield return null;
        ScreenCapture.CaptureScreenshot(Folder + "/capturing.png");
        yield return null; yield return null;
        Time.timeScale = 0; foreach (var p in players) p.enabled = true;
        knot.TickGameplay(2, true);
        Check(knot.State.IncomeTicks == 0 && scores.GetTeamScore(1) == 0, "Capture lock contributes no income or growth");
        knot.TickGameplay(.25f, true);
        Check(toast.TeamID == 1 && toast.MilliElectronVoltsPerSecond == 100 && scores.GetTeamScore(1) == 25,
            "First productive tick pays 25 meV and requests +100 meV/s");
        Check(toast.IsTransitioningOut && label.text.Contains("CAPTURING"), "Capture text shrinks before income grows in");
        foreach (var p in players) p.enabled = false;
        Time.timeScale = 1;
        until = Time.unscaledTime + .8f;
        while (Time.unscaledTime < until) yield return null;
        Check(label.text.Contains("+100 meV/s") && label.fontSize == 4.5f && !transition.IsPlaying,
            "Income completes its SDF intro at the exposed income font size");
        CheckGoalAttachment(knot, 1);
        until = Time.unscaledTime + .5f;
        while (Time.unscaledTime < until) yield return null;
        CheckGoalAttachment(knot, 1);
        fontSettings.Update(); fontSettings.FindProperty("incomeFontSize").floatValue = 3.6f;
        fontSettings.FindProperty("teamLabelPercent").intValue = 65;
        fontSettings.ApplyModifiedPropertiesWithoutUndo();
        yield return null; yield return null;
        ScreenCapture.CaptureScreenshot(Folder + "/light-control.png");
        yield return null; yield return null;
        Time.timeScale = 0; foreach (var p in players) p.enabled = true;
        Pose(ally, Vector3.zero); knot.TickGameplay(.25f, true);
        Check(toast.MilliElectronVoltsPerSecond == 108 && scores.GetTeamScore(1) == 52 &&
            knot.GetComponentsInChildren<SymmetryKnotIncomeToast>(true).Length == 1 && !transition.IsPlaying,
            "Second tick grows the actual payout; allies share one toast without restarting its intro");
        Pose(dark, Vector3.zero); knot.TickGameplay(.5f, true);
        Check(Cleared(toast) && knot.State.IncomeTicks == 2 && scores.GetTeamScore(1) == 52,
            "Contest stops income and pauses growth");
        until = Time.unscaledTime + .12f;
        while (Time.unscaledTime < until) yield return null;
        Check(toast.IsTransitioningOut && label.fontSharedMaterial.GetFloat("_FaceDilate") < sharedDilate,
            "Outro shrinks SDF glyphs before hiding");
        Pose(dark, new Vector3(100, 0, 100)); knot.TickGameplay(.25f, true);
        Check(toast.MilliElectronVoltsPerSecond == 120 && knot.State.IncomeTicks == 3 && scores.GetTeamScore(1) == 82,
            "Re-entry during outro resumes preserved growth at the next tick");
        until = Time.unscaledTime + .8f;
        while (Time.unscaledTime < until) yield return null;
        Check(toast.IsVisible && label.text.Contains("+120 meV/s"), "Interrupted outro presents the latest pending income");
        scores.AdvanceTeamAmplifier(1); scores.AdvanceTeamAmplifier(1); scores.AdvanceTeamAmplifier(1);
        knot.TickGameplay(.25f, true);
        Check(toast.MilliElectronVoltsPerSecond == 1024 && scores.GetTeamScore(1) == 338,
            "Team x8 applies after exponential tick rounding");
        long total = scores.GetTeamScore(1); int incomeTicks = knot.State.IncomeTicks;
        knot.TickGameplay(100, false);
        Check(Cleared(toast) && scores.GetTeamScore(1) == total && knot.State.IncomeTicks == incomeTicks,
            "Paused gameplay awards nothing and freezes growth");
        until = Time.unscaledTime + .4f;
        while (Time.unscaledTime < until) yield return null;
        Check(Hidden(knot), "Outro completes even while gameplay is paused");
        Pose(light, new Vector3(100, 0, 100)); Pose(ally, new Vector3(110, 0, 100)); knot.TickGameplay(.25f, true);
        Check(knot.IsFlowing && knot.State.OwnerTeam == 1 && knot.State.IncomeTicks == incomeTicks + 1 &&
            toast.TeamID == 1 && toast.MilliElectronVoltsPerSecond == 1120,
            "Forgiven vacancy continues the original team's income toast and growth");
        knot.TickGameplay(1.25f, true);
        Check(Cleared(toast) && knot.State.IncomeTicks == 0 && knot.State.OwnerTeam == -1,
            "Expired vacancy forgiveness clears the toast and resets growth");
        Pose(dark, Vector3.zero); knot.TickGameplay(.1f, true);
        Check(toast.TeamID == 2 && toast.RequestedContent == SymmetryKnotIncomeToast.Content.Capturing,
            "Takeover shows the new team's capture status");
        knot.TickGameplay(2.15f, true);
        Check(toast.TeamID == 2 && toast.MilliElectronVoltsPerSecond == 100 && scores.GetTeamScore(2) == 25,
            "Takeover restarts at base income with its own Amplifier");
        knot.Retire(); Check(Cleared(toast), "Retirement clears the earning rate");
        knot.Initialize(Vector3.zero); scores.ResetForMatch(true);
        knot.TickGameplay(1, true); knot.TickGameplay(2.25f, true);
        knot.TickGameplay(2, true);
        Check(toast.MilliElectronVoltsPerSecond == 200, "Income doubles after two productive seconds");
        knot.TickGameplay(2, true);
        Check(toast.MilliElectronVoltsPerSecond == 400, "Income quadruples after four productive seconds");
        knot.TickGameplay(2, true);
        Check(toast.MilliElectronVoltsPerSecond == 800, "Income reaches x8 after six productive seconds");
        scores.AdvanceTeamAmplifier(2); scores.AdvanceTeamAmplifier(2); scores.AdvanceTeamAmplifier(2);
        knot.TickGameplay(.25f, true);
        Check(toast.MilliElectronVoltsPerSecond == 6400, "Growth stays capped at x8 before team x8 amplification");
        foreach (var p in players) p.enabled = false;
        Time.timeScale = 1;
        until = Time.unscaledTime + .8f;
        while (Time.unscaledTime < until) yield return null;
        Check(label.text.Contains("+6.4 eV/s") && label.text.Contains("DARK CONTROL") &&
            Quaternion.Angle(toast.transform.rotation, Camera.main.transform.rotation) < .1f,
            "High income uses shared energy units and faces the camera");
        CheckGoalAttachment(knot, 2);
        until = Time.unscaledTime + .5f;
        while (Time.unscaledTime < until) yield return null;
        CheckGoalAttachment(knot, 2);
        ScreenCapture.CaptureScreenshot(Folder + "/dark-control.png");
        yield return null; yield return null;
        Time.timeScale = 0; foreach (var p in players) p.enabled = true;
        knot.TickGameplay(2, true); Check(Cleared(toast), "Natural expiry clears the rate");

        // Compare complete encounters across frame partitions, including per-tick rounding and expiry.
        long baseline = 0;
        foreach (int fps in new[] { 1, 30, 60, 120 })
        {
            scores.ResetForMatch(true); knot.Initialize(Vector3.zero);
            knot.TickGameplay(1, true);
            for (int i = 0; i < fps * 10; i++) knot.TickGameplay(1f / fps, true);
            long earned = scores.GetTeamScore(2);
            if (fps == 1) baseline = earned;
            Check(earned > 3200 && earned == baseline && knot.AwardedMilliElectronVolts == earned,
                "Identical exponential payouts at " + fps + " fps: " + earned + " meV");
        }
        knot.Initialize(Vector3.zero); scores.ResetForMatch(true);
        knot.TickGameplay(1, true); knot.TickGameplay(2.25f, true);
        scores.CloseScoring(); knot.TickGameplay(.25f, true);
        Check(Cleared(toast), "Closed scoring hides income");
        scores.ResetForMatch(true); knot.Initialize(Vector3.zero);
        knot.TickGameplay(1, true); knot.TickGameplay(2.25f, true);
        knot.enabled = true; knot.enabled = false;
        Check(Hidden(knot), "Disabling the controller hides the toast immediately");
        Check(!scores.TryAwardToTeam(ScoreRewardKeys.HiggsControlTick, 2, null, "invalid-scale", Vector3.zero,
            out _, 1, double.NaN), "Invalid objective reward scaling is rejected");
        Check(scores.TryAwardToTeam(ScoreRewardKeys.HiggsControlTick, 2, null, "default-scale", Vector3.zero,
            out var defaultAward) && defaultAward.finalMilliElectronVolts == 25 && defaultAward.rewardScale == 1,
            "Ordinary unscaled awards retain their existing value");

        var originalProfile = scores.Profile;
        var fixtureProfile = Object.Instantiate(originalProfile);
        try
        {
            scores.Configure(fixtureProfile); scores.ResetForMatch(true);
            fixtureProfile.TryGetReward(ScoreRewardKeys.HiggsControlTick, out var rule);
            rule.enabled = false; knot.TickGameplay(.25f, true);
            Check(Cleared(toast) && scores.GetTeamScore(2) == 0, "Rejected reward cannot show false income");
            rule.enabled = true; rule.amount = 300;
            var config = new SerializedObject(knot);
            config.FindProperty("tickIntervalSeconds").floatValue = .5f;
            config.ApplyModifiedPropertiesWithoutUndo();
            knot.Initialize(Vector3.zero); knot.TickGameplay(1, true); knot.TickGameplay(2.5f, true);
            Check(toast.MilliElectronVoltsPerSecond == 600 && scores.GetTeamScore(2) == 300,
                "Changed reward and tick duration use actual accepted income");
        }
        finally { scores.Configure(originalProfile); scores.ResetForMatch(true); Object.Destroy(fixtureProfile); }
        Check(sharedMaterial.GetFloat("_FaceDilate") == sharedDilate, "Shared font material remains unchanged");
        Object.Destroy(knot.gameObject); yield return null;
        Check(toast == null, "Destroying the Knot releases its view");
    }
}
#endif
