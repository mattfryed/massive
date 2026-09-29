#if UNITY_EDITOR
using System;
using Massive.Demonstrations;
using Massive.Scoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Batch validation of the actual authored arena and its ordinary input path.</summary>
[InitializeOnLoad]
public static class PlayerActionTestArenaValidation
{
    const string Pending = "Massive.ActionArenaValidation.Pending";
    static double deadline;
    static int step;
    static PlayerActionTestArena lab;
    static PlayerRosterController roster;
    static GameManagerScript match;
    static long scoreBefore;
    static int lastTrial;
    static double baselineStarted;
    static PlayerActionTestArenaValidation()
    { EditorApplication.playModeStateChanged += StateChanged; }
    public static void RunBatch()
    {
        SessionState.SetBool(Pending + ".Baseline", false);
        PlayerActionTestArenaSetup.Validate();
        EditorSceneManager.OpenScene(PlayerActionTestArenaSetup.ScenePath);
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }
    public static void RunTemplateBaselineBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Templates/Level-Planar.unity");
        SessionState.SetBool(Pending + ".Baseline", true);
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }
    static void CaptureArena(string path)
    {
        var camera = Camera.main;
        if (!camera) return;
        Canvas.ForceUpdateCanvases();
        foreach (var text in Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None))
            text.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        var target = RenderTexture.GetTemporary(1600, 900, 24);
        var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            image.Apply();
            System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);
            Object.DestroyImmediate(image);
        }
    }
    static void StateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Pending, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            baselineStarted = 0; step = 0; deadline = EditorApplication.timeSinceStartup + 150;
            EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Pending, false);
            EditorApplication.Exit(SessionState.GetBool(Pending + ".Passed", false) ? 0 : 1);
        }
    }
    static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    static void Tick()
    {
        try
        {
            Check(EditorApplication.timeSinceStartup < deadline, "Runtime validation timed out: " + (lab ? lab.Status : "startup"));
            if (!Application.isPlaying) return;
            if (!match) match = Object.FindFirstObjectByType<GameManagerScript>();
            if (!match) return;
            Check(!match.IsStartupBlocked, match.StartupFailureReason);
            if (match.Phase != MatchRuntimePhase.Regulation) return;
            if (SessionState.GetBool(Pending + ".Baseline", false))
            {
                if (baselineStarted == 0) baselineStarted = EditorApplication.timeSinceStartup;
                if (EditorApplication.timeSinceStartup - baselineStarted > 10)
                {
                    CaptureArena("Library/ActionArenaTemplateBaseline.png");
                    Debug.Log("PLAYER ACTION TEMPLATE BASELINE reached regulation.");
                    Finish(true);
                }
                return;
            }
            if (!lab) lab = Object.FindFirstObjectByType<PlayerActionTestArena>();
            if (!roster) roster = Object.FindFirstObjectByType<PlayerRosterController>();
            Check(lab && roster && roster.IsRosterReady, "Missing ready roster / lab");
            Check(match.ScoreService.IsScoringOpen, "Scoring should be open");
            var p1 = roster.P1.GetComponent<PlayerControllerScript>();
            var p3 = roster.P3.GetComponent<PlayerControllerScript>();
            if (step == 0)
            {
                if (lab.Mode == PlayerActionTestArena.TestMode.AllDemos) lab.SetMode(PlayerActionTestArena.TestMode.Manual);
                Check(lab.Mode == PlayerActionTestArena.TestMode.Manual && p1.ControlMode == PlayerControlMode.Rewired, "Default manual input");
                Check(p1.ParticipatesInMatch && p3.ParticipatesInMatch, "Actors must score");
                Check(!roster.P2.activeSelf && !roster.P4.activeSelf, "1v1 roster");
                var settings = new SerializedObject(lab);
                settings.FindProperty("repeat").boolValue = false;
                settings.ApplyModifiedPropertiesWithoutUndo();
                lab.SetMode(PlayerActionTestArena.TestMode.AttackAndShield);
                lab.UseDemoSpacingAndTiming();
                lab.RunTrials(); step = 1;
            }
            else if (step == 1 && !lab.IsRunning)
            {
                Check(!lab.HasFailed && lab.CompletedTrials == 1, "Shield trial: " + lab.Status);
                Check(lab.ShieldContacts > 0, "Expected an actual shield contact using demo spacing / timing");
                lab.SetMode(PlayerActionTestArena.TestMode.Manual);
                Check(p1.ControlMode == PlayerControlMode.Rewired && p3.ControlMode == PlayerControlMode.Rewired, "Manual control restored");
                scoreBefore = match.ScoreService.LightScoreMilliElectronVolts;
                lab.SetMode(PlayerActionTestArena.TestMode.Combo);
                lastTrial = lab.CompletedTrials; lab.RunTrials(); step = 2;
            }
            else if (step == 2 && !lab.IsRunning)
            {
                Check(!lab.HasFailed && lab.CompletedTrials > lastTrial, "Combo trial: " + lab.Status);
                lastTrial = lab.CompletedTrials;
                if (match.ScoreService.LightScoreMilliElectronVolts <= scoreBefore && lastTrial < 12)
                { lab.RunTrials(); return; }
                Check(match.ScoreService.LightScoreMilliElectronVolts > scoreBefore, "Ordinary combat should award banked energy");
                lab.SetMode(PlayerActionTestArena.TestMode.Manual);
                Check(p1.ControlMode == PlayerControlMode.Rewired, "Manual restored after combo");
                CaptureArena("Library/ActionArenaValidation.png");
                step = 3;
                deadline = EditorApplication.timeSinceStartup + 5;
            }
            else if (step == 3)
            {
                Debug.Log("PLAYER ACTION ARENA PLAY VALIDATION PASSED: startup, input modes, shield contact, combo and combat scoring.");
                Finish(true);
            }
        }
        catch (Exception ex) { Debug.LogException(ex); CaptureArena("Library/ActionArenaFailure.png"); Finish(false); }
    }
    static void Finish(bool passed)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(Pending + ".Passed", passed);
        EditorApplication.isPlaying = false;
    }
}
#endif




