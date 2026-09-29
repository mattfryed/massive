#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Massive.Demonstrations;
using Massive.Player;
using Massive.PowerUps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PlayerDemoGalleryValidation
{
    const string Key = "MASSIVE.GalleryValidation.";
    static double deadline;
    static int step;
    static PlayerDemoGallery gallery;
    static PlayerActionTestArena lab;
    static GameManagerScript match;
    static int[] beforeRestart;
    static float clockBefore;
    static long lightScore, darkScore;
    static bool captured;
    static int[] soloStages = new int[3];
    static int soloFinishes, soloPulses;
    static PlayerDemoGalleryValidation()
    {
        EditorApplication.update += BeginWhenReady;
        EditorApplication.playModeStateChanged += StateChanged;
    }
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new Exception("Use the isolated batch editor.");
        SessionState.SetBool(Key + "Start", true);
    }
    static void BeginWhenReady()
    {
        if (!SessionState.GetBool(Key + "Start", false) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        SessionState.SetBool(Key + "Start", false);
        try
        {
            EditorSceneManager.OpenScene(PlayerActionTestArenaSetup.ScenePath);
            PlayerDemoGallerySetup.Install(); PlayerDemoGallerySetup.Validate(); PlayerActionTestArenaSetup.Validate();
            SessionState.SetBool(Key + "Pending", true); SessionState.SetBool(Key + "Passed", false);
            EditorApplication.isPlaying = true;
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void StateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key + "Pending", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            step = 0; captured = false; deadline = EditorApplication.timeSinceStartup + 180;
            EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key + "Pending", false); EditorApplication.update -= Tick;
            EditorApplication.Exit(SessionState.GetBool(Key + "Passed", false) ? 0 : 1);
        }
    }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Tick()
    {
        try
        {
            Check(EditorApplication.timeSinceStartup < deadline, "Gallery validation timed out: " + Summary());
            if (!gallery) gallery = Object.FindFirstObjectByType<PlayerDemoGallery>();
            if (!lab) lab = Object.FindFirstObjectByType<PlayerActionTestArena>();
            if (!match) match = Object.FindFirstObjectByType<GameManagerScript>();
            if (!gallery || !lab || !match) return;
            Check(!match.IsStartupBlocked, match.StartupFailureReason);
            if (step == 0)
            {
                if (!gallery.IsPlaying) return;
                Check(lab.Mode == PlayerActionTestArena.TestMode.AllDemos, "Gallery is the default action-lab mode");
                Check(gallery.Demos.Count == 7 && !match.enabled, "Seven lanes with match timer paused");
                clockBefore = match.RegulationRemainingSeconds;
                lightScore = match.ScoreService.LightScoreMilliElectronVolts; darkScore = match.ScoreService.DarkScoreMilliElectronVolts;
                soloStages = new int[3]; soloFinishes = soloPulses = 0;
                var solo = gallery.Demos[2].Primary;
                solo.attackController.OnStageStarted.AddListener(s => soloStages[(int)s.StageType]++);
                solo.attackController.OnStageCompleted.AddListener(s => { if (s.StageType == AttackStageType.FinisherRepulsor) soloFinishes++; });
                solo.GetComponentInChildren<PlayerRepulsorAOE>(true).PulseStarted += _ => soloPulses++;
                step = 1;
            }
            foreach (var demo in gallery.Demos)
                Check(demo.FailedLoops == 0 && demo.LastFailure == null, demo.name + ": " + demo.LastFailure);
            if (step == 1)
            {
                Check(gallery.Demos.All(d => d.Primary && !d.Primary.ParticipatesInMatch && d.Primary.UsesGameplayTuning &&
                    (d.Scenario.kind == PlayerDemoKind.SoloCombo ? !d.Partner :
                        d.Partner && !d.Partner.ParticipatesInMatch && d.Primary.SharesSimulationWith(d.Partner))), "All 13 actors use isolated scopes and shared tuning");
                Check(Physics.GetIgnoreCollision(gallery.Demos[2].Primary.GetComponent<Collider>(), gallery.Demos[3].Primary.GetComponent<Collider>()), "Solo body remains isolated from adjacent pairs");
                Check(!gallery.Demos[0].Primary.SharesSimulationWith(gallery.Demos[1].Partner), "Adjacent pairs have separate target scopes");
                Check(Physics.GetIgnoreCollision(gallery.Demos[0].Primary.GetComponent<Collider>(), gallery.Demos[1].Primary.GetComponent<Collider>()), "Adjacent pair bodies ignore one another");
                if (!captured && gallery.Demos.All(d => d.SuccessfulLoops >= 1)) { Capture("overview"); captured = true; }
                if (!gallery.Demos.All(d => d.SuccessfulLoops >= 3)) return;
                Check(gallery.Demos[0].ConfirmedParries >= 3, "Melee tap parries");
                Check(gallery.Demos[1].ConfirmedLateBlocks >= 3 && gallery.Demos[1].LastContactShieldAge > .65f, "Melee held blocks after parry window");
                Check(!gallery.Demos[2].Partner && soloStages.All(n => n >= 3) && soloFinishes >= 3 && soloPulses >= 3, "Solo actor loops all three real attack stages and Repulsor pulses without a defender");
                Check(gallery.Demos[3].ConfirmedDecoherenceParries >= 3, "Decoherence melee parries and return route");
                Check(gallery.Demos[4].ConfirmedProjectileContacts >= 3 && gallery.Demos[4].ConfirmedParries >= 3, "Real accelerator shots are parried");
                Check(gallery.Demos[5].ConfirmedLateBlocks >= 3 && gallery.Demos[5].LastContactShieldAge > .65f, "Real accelerator shots are blocked after parry window");
                Check(gallery.Demos[6].ConfirmedProjectileContacts >= 3 && gallery.Demos[6].ConfirmedDecoherenceParries >= 3, "Decoherence defender parries real accelerator shots");
                Check(Mathf.Approximately(clockBefore, match.RegulationRemainingSeconds), "Demo loops do not consume match time");
                Check(lightScore == match.ScoreService.LightScoreMilliElectronVolts && darkScore == match.ScoreService.DarkScoreMilliElectronVolts, "Demo damage does not award match score");
                Capture("looping");
                Directory.CreateDirectory("Library/DemoGalleryValidation");
                File.WriteAllText("Library/DemoGalleryValidation/loops.txt", Summary());
                beforeRestart = gallery.Demos.Select(d => d.SuccessfulLoops).ToArray();
                lab.SetMode(PlayerActionTestArena.TestMode.Manual); step = 2;
            }
            else if (step == 2)
            {
                Check(!gallery.IsPlaying && match.enabled, "Manual mode resumes the match clock");
                Check(Object.FindObjectsByType<PlayerControllerScript>(FindObjectsSortMode.None).Count(p => p.IsPseudoPlayer) == 0, "Stopping removes all gallery actors");
                var roster = Object.FindFirstObjectByType<PlayerRosterController>();
                Check(roster.P1.GetComponent<PlayerControllerScript>().ControlMode == PlayerControlMode.Rewired &&
                    roster.P3.GetComponent<PlayerControllerScript>().ControlMode == PlayerControlMode.Rewired, "Manual input restored");
                lab.SetMode(PlayerActionTestArena.TestMode.AllDemos); step = 3;
            }
            else if (step == 3)
            {
                if (!gallery.IsPlaying || gallery.Demos.Where((d, i) => d.SuccessfulLoops <= beforeRestart[i]).Any()) return;
                Check(Object.FindObjectsByType<PlayerControllerScript>(FindObjectsSortMode.None).Count(p => p.IsPseudoPlayer) == 13, "Restart creates exactly 13 live actors");
                Finish(true, "PASSED: seven concurrent loops, real contacts, late holds, solo three-stage combo, isolation, score/time preservation, manual switch and restart.");
            }
        }
        catch (Exception e) { Capture("failure"); Finish(false, e.ToString() + "\n" + Summary()); }
    }
    static string Summary() => gallery == null ? "Gallery not ready" : string.Join("\n", gallery.Demos.Select((d, i) =>
        (i + 1) + " " + d.name + " | loops=" + d.SuccessfulLoops + " parries=" + d.ConfirmedParries + " held=" + d.ConfirmedLateBlocks +
        " solo stages=" + (i == 2 ? string.Join("/", soloStages) : "n/a") + " hits=" + d.ConfirmedHits + " shots=" + d.ConfirmedShots + " shot contacts=" + d.ConfirmedProjectileContacts +
        " deaths=" + d.ConfirmedKills + " respawns=" + d.ConfirmedRespawns + " age=" + d.LastContactShieldAge.ToString("F3") + " phase=" + d.Phase));
    static void Finish(bool success, string report)
    {
        Directory.CreateDirectory("Library/DemoGalleryValidation");
        File.WriteAllText("Library/DemoGalleryValidation/report.txt", report);
        Debug.Log("PLAYER DEMO GALLERY VALIDATION: " + report);
        EditorApplication.update -= Tick; SessionState.SetBool(Key + "Passed", success); EditorApplication.isPlaying = false;
    }
    static void Capture(string name)
    {
        var camera = Camera.main; if (!camera) return;
        foreach (var text in Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None)) text.ForceMeshUpdate();
        var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
        var target = RenderTexture.GetTemporary(1600, 900, 24); var texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); texture.Apply();
            Directory.CreateDirectory("Library/DemoGalleryValidation"); File.WriteAllBytes("Library/DemoGalleryValidation/" + name + ".png", texture.EncodeToPNG());
        }
        finally { camera.targetTexture = oldTarget; RenderTexture.active = oldActive; RenderTexture.ReleaseTemporary(target); Object.Destroy(texture); }
    }
}
#endif
