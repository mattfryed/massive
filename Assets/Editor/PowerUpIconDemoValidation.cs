#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Massive.Demonstrations;
using Massive.PowerUps;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PowerUpIconDemoValidation
{
    const string Key = "MASSIVE.PowerUpIconValidation";
    static IEnumerator routine;
    static int frame;
    static double deadline, nextReport;
    static readonly List<string> checks = new(), errors = new();
    static PowerUpIconDemo[] demos;
    static PowerUpIconDemoValidation() { EditorApplication.playModeStateChanged += State; }
    [MenuItem("MASSIVE/Demonstrations/Validate Power-up Icon Row %#&F3")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        PowerUpIconDemoSetup.Validate();
        PowerUpSettingsValidation.RunEdit();
        Directory.CreateDirectory(PowerUpIconDemoSetup.Output);
        File.WriteAllText(PowerUpIconDemoSetup.Output + "/report.txt", "STARTING\n");
        SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
    }
    static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            checks.Clear(); errors.Clear(); frame = -1; deadline = EditorApplication.timeSinceStartup + 160; nextReport = 0;
            string editReport = PowerUpIconDemoSetup.Output + "/settings-edit.txt";
            if (File.Exists(editReport)) checks.AddRange(File.ReadAllLines(editReport));
            routine = Checks(); Application.logMessageReceived += Log; EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        { SessionState.SetBool(Key, false); EditorApplication.update -= Tick; Application.logMessageReceived -= Log; }
    }
    static void Log(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
    static string Summary() => demos == null ? "Waiting for scene" : string.Join("\n", demos.Select(d => d.name +
        ": natural=" + d.NaturalLoops + " claims=" + d.ConfirmedClaims + " hitLoops=" + d.CompletedHitLoops + " toasts=" + d.ToastsShown +
        " failure=" + d.Failure + " player=" + (d.Player ? d.Player.transform.position.ToString("F2") : "none")));
    static void Tick()
    {
        if (!EditorApplication.isPlaying || routine == null || frame == Time.frameCount) return;
        try
        {
            frame = Time.frameCount;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timed out. " + Summary());
            if (EditorApplication.timeSinceStartup > nextReport)
            { nextReport = EditorApplication.timeSinceStartup + 2; File.WriteAllText(PowerUpIconDemoSetup.Output + "/report.txt", "RUNNING\n" + Summary()); }
            if (!routine.MoveNext()) Finish(null);
        }
        catch (Exception e) { Finish(e); }
    }
    static void Finish(Exception failure)
    {
        routine = null; EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        bool passed = failure == null && errors.Count == 0;
        File.WriteAllText(PowerUpIconDemoSetup.Output + "/report.txt", (passed ? "PASSED" : "FAILED") + "\n" +
            string.Join("\n", checks) + "\n" + Summary() + "\n" + failure + "\n" + string.Join("\n", errors));
        EditorApplication.isPlaying = false; Debug.Log("POWER-UP ICON VALIDATION " + (passed ? "PASSED" : "FAILED: " + failure));
    }
    static void Check(bool value, string label)
    { if (!value) throw new Exception(label); checks.Add("PASS " + label); }
    static IEnumerator Checks()
    {
        var gallery = PowerUpIconDemoSetup.All<PlayerDemoGallery>().Single();
        var lab = PowerUpIconDemoSetup.All<PlayerActionTestArena>().Single();
        lab.SetMode(PlayerActionTestArena.TestMode.AllDemos);
        var match = PowerUpIconDemoSetup.All<GameManagerScript>().Single();
        demos = PowerUpIconDemoSetup.All<PowerUpIconDemo>();
        while (!gallery.IsPlaying) yield return null;
        float clock = match.RegulationRemainingSeconds;
        long light = match.ScoreService.LightScoreMilliElectronVolts, dark = match.ScoreService.DarkScoreMilliElectronVolts;
        var originalActors = demos.Select(d => d.Player).ToArray();
        var initialMass = demos.Select(d => d.Player.massScore).ToArray();
        var claimTimes = demos.ToDictionary(d => d, d => -1f);
        var capturedToasts = new HashSet<PowerUpIconDemo>();
        foreach (var d in demos) d.Player.powerUps.OnEquipped += _ => claimTimes[d] = Time.time;
        Check(gallery.Demos.Count == 7 && demos.Length == 4, "Seven existing action demos plus four pickup pairs");
        bool idleCaptured = false, hitCaptured = false, outCaptured = false;
        var seenEffects = new HashSet<PowerUpIconDemo>();
        var seenShatter = new HashSet<PowerUpIconDemo>();
        var watchedPickups = new HashSet<PowerUpPickup>();
        var immediateClaims = new HashSet<PowerUpIconDemo>();
        while (demos.Any(d => d.NaturalLoops < 2 || d.CompletedHitLoops < 2))
        {
            foreach (var d in demos)
            {
                CheckFailure(d);
                if (d.ConfirmedClaims > 0 && claimTimes[d] < 0) claimTimes[d] = Time.time;
                if (!capturedToasts.Contains(d) && claimTimes[d] >= 0 && Time.time - claimTimes[d] >= .25f)
                {
                    Check(d.GetComponentInChildren<PowerUpPickupToast>() != null, d.name + " toast survives its intro");
                    Capture("toast-" + d.definition.Type + ".png"); capturedToasts.Add(d);
                }
                if (d.Player && d.Player.powerUps.ActiveDefinition == d.definition && d.Player.powerUps.HasActive) seenEffects.Add(d);
                if (d.HitPickup)
                {
                    var pickup = d.HitPickup;
                    if (watchedPickups.Add(pickup)) pickup.Claimed += _ =>
                    { if (PowerUpIconAnimationValidation.IsImmediateClaim(pickup)) immediateClaims.Add(d); };
                    var wire = d.HitPickup.GetComponentInChildren<ParametricPolyhedronWire>();
                    if (wire && wire.shatterProgress > 0) seenShatter.Add(d);
                }
            }
            if (!idleCaptured && demos.All(d => d.NaturalPickup) && Time.timeSinceLevelLoad > 5)
            { Capture("overview.png"); idleCaptured = true; }
            if (!hitCaptured && seenShatter.Count > 0) { Capture("activation.png"); hitCaptured = true; }
            if (!outCaptured && demos.Any(d => d.NaturalPickup && d.NaturalPickup.GetComponent<PowerUpIconManifestAnimator>().IsDespawning))
            { Capture("natural-despawn.png"); outCaptured = true; }
            yield return null;
        }
        foreach (var d in demos)
        {
            Check(d.NaturalLoops >= 2 && d.CompletedHitLoops >= 2, d.name + " repeated natural expiry and real melee claims");
            Check(d.ConfirmedClaims == d.ToastsShown, d.name + " one shared toast per claim");
            Check(seenShatter.Contains(d), d.name + " live wire shatter observed");
            Check(immediateClaims.Contains(d), d.name + " inner outro begins inside actual melee claim callback");
            Check(d.definition is IInstantPowerUpEffect ? d.Player.massScore > initialMass[Array.IndexOf(demos, d)] : seenEffects.Contains(d),
                d.name + " real effect applied");
            Check(d.Player == originalActors[Array.IndexOf(demos, d)], d.name + " persistent player returns through input");
            foreach (var other in originalActors.Where(p => p != d.Player))
                Check(!d.Player.SharesSimulationWith(other), d.name + " isolated from " + other.SimulationRoot.parent.name);
        }
        Check(match.RegulationRemainingSeconds == clock && !match.enabled, "Match clock remains paused");
        Check(match.ScoreService.LightScoreMilliElectronVolts == light && match.ScoreService.DarkScoreMilliElectronVolts == dark, "No match scoring from icon claims");
        Check(gallery.Demos.All(d => d.LastFailure == null && d.SuccessfulLoops > 0), "Existing seven demos continue looping");
        Capture("repeated-loops.png");
        var ownedEffects = demos.SelectMany(d => d.GetComponentsInChildren<SmallMassBlobScript>(true)).ToArray();
        lab.StopTrials(); yield return null; yield return null;
        Check(demos.All(d => !d.Player && !d.NaturalPickup && !d.HitPickup), "Stop clears all icon actors and pickups");
        Check(PowerUpPickup.ActivePickups.Count == 0, "Stop leaves no registered pickups");
        Check(PowerUpIconDemoSetup.All<PowerUpPickupToast>().Length == 0, "Stop leaves no owned pickup toasts");
        Check(ownedEffects.All(b => !b), "Stop destroys any remaining owned Mass Node effects");
        lab.RunTrials();
        while (!gallery.IsPlaying || demos.Any(d => d.ConfirmedClaims == 0)) { foreach (var d in demos) CheckFailure(d); yield return null; }
        Check(demos.All(d => d.Player && d.NaturalPickup && d.ConfirmedClaims > 0), "Restart restores all four pairs and real claims");
        var animations = PowerUpIconAnimationValidation.Run(demos.Select(d => d.definition), Check);
        while (animations.MoveNext()) yield return animations.Current;
        lab.StopTrials(); yield return null; yield return null;
        var settingsChecks = PowerUpSettingsValidation.RunPlay(Check);
        while (settingsChecks.MoveNext()) yield return settingsChecks.Current;
        lab.RunTrials();
        Check(errors.Count == 0, "No runtime Console errors");
    }
    static void CheckFailure(PowerUpIconDemo demo)
    { if (demo.Failure != null) throw new Exception(demo.name + ": " + demo.Failure); }
    static void Capture(string filename)
    {
        var camera = Camera.main; if (!camera) return;
        var previous = camera.targetTexture; var active = RenderTexture.active;
        var target = new RenderTexture(1920, 1080, 24); var pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); pixels.Apply();
            File.WriteAllBytes(PowerUpIconDemoSetup.Output + "/" + filename, pixels.EncodeToPNG());
        }
        finally { camera.targetTexture = previous; RenderTexture.active = active; Object.DestroyImmediate(pixels); target.Release(); Object.DestroyImmediate(target); }
    }
}
#endif
