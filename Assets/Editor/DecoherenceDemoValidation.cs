#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Massive.Demonstrations;
using Massive.Enemies;
using Massive.Player;
using Massive.PowerUps;
using Massive.Scoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Runs the saved action lab, observes real loops, then verifies every mode transition.</summary>
[InitializeOnLoad]
public static class DecoherenceDemoValidation
{
    const string Key = "MASSIVE.DecoherenceDemoValidation";
    const string Folder = "Library/DecoherenceDemoValidation";
    static readonly List<string> checks = new(), errors = new();
    static IEnumerator routine;
    static int frame;
    static double deadline;
    static bool previousBackground;
    static DecoherenceDemoValidation() => EditorApplication.playModeStateChanged += State;

    [MenuItem("MASSIVE/Demonstrations/Validate Decoherence Demo")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Use Edit Mode after compilation.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != PlayerActionTestArenaSetup.ScenePath || scene.isDirty)
            throw new InvalidOperationException("Open and save the Player Action Lab before validation.");
        SessionState.SetString(Key + ".Previous", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(Key, true);
        Directory.CreateDirectory(Folder); File.WriteAllText(Folder + "/report.txt", "STARTING");
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path);
        EditorApplication.isPlaying = true;
    }
    static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            checks.Clear(); errors.Clear(); frame = -1; deadline = EditorApplication.timeSinceStartup + 180;
            previousBackground = Application.runInBackground; Application.runInBackground = true;
            routine = Expand(Checks()); Application.logMessageReceived += Log; EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
            (routine as IDisposable)?.Dispose(); routine = null;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key + ".Previous", ""));
            SessionState.SetBool(Key, false);
        }
    }
    static void Log(string text, string stack, LogType type)
    { if (type is LogType.Error or LogType.Exception or LogType.Assert) errors.Add(text + "\n" + stack); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || routine == null || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Validation timed out");
            if (!routine.MoveNext()) Finish(null);
        }
        catch (Exception e) { Finish(e); }
    }
    static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; (routine as IDisposable)?.Dispose(); routine = null;
        Application.runInBackground = previousBackground; Application.logMessageReceived -= Log;
        string report = (error == null && errors.Count == 0 ? "PASSED" : "FAILED") + " (" + checks.Count + " checks)\n" +
            string.Join("\n", checks) + "\n" + error + "\n" + string.Join("\n", errors);
        File.WriteAllText(Folder + "/report.txt", report); Debug.Log("DECOHERENCE DEMO " + report);
        EditorApplication.isPlaying = false;
    }
    static IEnumerator Expand(IEnumerator root)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(root);
        try
        {
            while (stack.Count > 0)
            {
                var top = stack.Peek();
                if (!top.MoveNext()) { (top as IDisposable)?.Dispose(); stack.Pop(); continue; }
                if (top.Current is IEnumerator nested) { stack.Push(nested); continue; }
                yield return top.Current;
            }
        }
        finally { while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose(); }
    }
    static void Check(bool pass, string label)
    { if (!pass) throw new Exception(label); checks.Add("PASS " + label); }
    static IEnumerator Until(Func<bool> condition, float timeout = 60f)
    {
        float end = Time.time + timeout;
        while (!condition() && Time.time < end) yield return null;
        if (!condition()) throw new Exception("Condition timed out");
    }
    static IEnumerator Wait(float seconds)
    { float end = Time.time + seconds; while (Time.time < end) yield return null; }
    static void Capture(string name)
    {
        var camera = Camera.main;
        var target = new RenderTexture(1920, 1080, 24);
        var priorTarget = camera.targetTexture; var priorActive = RenderTexture.active;
        var pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); pixels.Apply();
            File.WriteAllBytes(Folder + "/" + name + ".png", pixels.EncodeToPNG());
        }
        finally { camera.targetTexture = priorTarget; RenderTexture.active = priorActive; Object.DestroyImmediate(pixels); Object.DestroyImmediate(target); }
    }
    static MeshRenderer[] EchoRenderers() => Resources.FindObjectsOfTypeAll<MeshRenderer>()
        .Where(r => r && r.gameObject.scene.IsValid() && r.name == "Decoherence grid echoes").ToArray();
    static IEnumerator Checks()
    {
        var arena = Object.FindFirstObjectByType<PlayerActionTestArena>();
        var gallery = Object.FindFirstObjectByType<PlayerDemoGallery>();
        var match = Object.FindFirstObjectByType<GameManagerScript>();
        var score = Object.FindFirstObjectByType<MatchScoreService>();
        yield return Until(() => match.Phase == MatchRuntimePhase.Regulation && gallery.IsPlaying, 20f);
        Check(arena.Mode == PlayerActionTestArena.TestMode.Decoherence, "Saved scene starts in Decoherence mode");
        var lanes = Object.FindObjectsByType<DecoherenceDemoLane>(FindObjectsSortMode.None);
        Check(lanes.Length == 3 && lanes.All(l => l.Primary && l.Session), "Three samples start with canonical players");
        Check(lanes.Select(l => l.Primary.SimulationRoot).Distinct().Count() == 3, "Each lane owns an isolated simulation scope");
        Check(PlayerControllerScript.ActivePlayers.Count(p => p.IsPseudoPlayer) == 4, "Exactly four demonstration players; original gallery is inactive");
        Check(lanes.All(l => l.Primary.IsPseudoPlayer && l.Primary.UsesGameplayTuning), "Demonstration players use shared gameplay tuning and do not score");
        float clock = match.RegulationRemainingSeconds;
        long firstScore = score.GetTeamScore(1), secondScore = score.GetTeamScore(2);
        var drones = lanes.Single(l => l.example == DecoherenceDemoLane.Example.DroneSweep);
        var carrier = lanes.Single(l => l.example == DecoherenceDemoLane.Example.CarrierExposure);
        var players = lanes.Single(l => l.example == DecoherenceDemoLane.Example.PlayerDefense);
        yield return Until(() => carrier.Primary.powerUps.DecoherenceFieldActive && carrier.Enemies.Count == 1 &&
            carrier.Enemies[0] && carrier.Enemies[0].HealthRemaining < carrier.enemyDefinition.healthMassEq);
        Capture("field-exposure");
        var echoes = EchoRenderers();
        Check(echoes.Any(r => r.enabled) && echoes.All(r => r.sharedMaterial && r.sharedMaterial.shader.isSupported &&
            r.GetComponent<MeshFilter>().sharedMesh == r.transform.parent.GetComponent<MeshFilter>().sharedMesh),
            "Grid echoes render the actual arena mesh with a supported shader");
        Check(lanes.Where(l => l != players).SelectMany(l => l.Enemies).All(e => !e || (!e.DemonstrationCounterkill && e.HoldPosition && !e.AttacksEnabled)),
            "Stationary samples use ordinary health without counterkill shortcuts");
        yield return Until(() => !carrier.Primary.powerUps.DecoherenceFieldActive && carrier.Enemies[0] && !carrier.Enemies[0].IsDead);
        var heldEnemy = carrier.Enemies[0]; float heldHealth = heldEnemy.HealthRemaining;
        yield return Wait(.25f);
        Check(!heldEnemy || heldEnemy.HealthRemaining < heldHealth, "Paint keeps damaging the Carrier after release while charge refills");
        yield return Until(() => players.ComparingShield && players.Primary.powerUps.DecoherenceFieldActive && players.Partner.GetComponent<PlayerShieldAbility>().IsHolding);
        Capture("opponent-shield");
        yield return Until(() => lanes.All(l => l.CompletedLoops >= 2 || l.Failure != null), 90f);
        foreach (var lane in lanes)
        {
            Check(lane.Failure == null && lane.CompletedLoops >= 2, lane.example + " completes two real loops");
            Check(lane.FieldMovementDistance > 1f, lane.example + " moves while its field is active");
        }
        Check(drones.ConfirmedDefeats >= 6 && carrier.ConfirmedDefeats >= 2, "Real field defeats three Drones and one Carrier per loop");
        Check(players.LastExposedLoss > .02f && players.LastBlockedLoss < .001f, "Opponent loses mass exposed and preserves mass while shielding");
        Check(!drones.health.text.Contains("/ 0.0s") && !carrier.health.text.Contains("/ 0.0s"), "Health-scaled exposure labels resolve runtime hurtboxes");
        Check(Mathf.Approximately(match.RegulationRemainingSeconds, clock) && !match.enabled, "Match clock remains paused");
        Check(score.GetTeamScore(1) == firstScore && score.GetTeamScore(2) == secondScore, "Both match scores remain unchanged");
        Capture("repeated-loops");
        arena.StopTrials(); yield return Wait(.15f);
        Check(lanes.All(l => !l.Session && !l.Primary) && !Object.FindFirstObjectByType<DecoherenceFieldVisual>(), "Stop removes owned actors, paint and field visuals");
        Check(EchoRenderers().Length == 0, "Stop removes borrowed-grid renderers and releases their paint masks");
        Check(match.enabled && !gallery.IsPlaying, "Stop restores the match clock");
        arena.RunTrials(); yield return Until(() => gallery.IsPlaying && lanes.All(l => l.Primary));
        Check(PlayerControllerScript.ActivePlayers.Count(p => p.IsPseudoPlayer) == 4, "Run restarts exactly four fresh demonstration players");
        arena.SetMode(PlayerActionTestArena.TestMode.AllDemos);
        yield return Until(() => gallery.IsPlaying && gallery.Demos.All(d => d.Primary)); yield return Wait(.1f);
        Check(lanes.All(l => !l.Session) && gallery.Demos.Count == 7 && gallery.Demos.All(d => d.IsRunning), "All demos restores the seven existing action lanes");
        yield return Until(() => gallery.Demos[3].ConfirmedDecoherenceParries > 0 && gallery.Demos[6].ConfirmedBlocks > 0, 30f);
        Check(gallery.Demos[3].LastFailure == null && gallery.Demos[6].LastFailure == null, "Existing melee and beam Decoherence demos still register real shield contacts");
        arena.SetMode(PlayerActionTestArena.TestMode.Manual); yield return Wait(.15f);
        Check(match.enabled && PlayerControllerScript.ActivePlayers.All(p => !p.IsPseudoPlayer) &&
            PlayerControllerScript.ActivePlayers.All(p => p.ControlMode == PlayerControlMode.Rewired), "Manual removes demo actors and restores normal roster input");
        arena.SetMode(PlayerActionTestArena.TestMode.Decoherence); yield return Until(() => gallery.IsPlaying && lanes.All(l => l.Primary));
        Check(PlayerControllerScript.ActivePlayers.Count(p => p.IsPseudoPlayer) == 4, "Decoherence restarts cleanly after other modes");
        Check(errors.Count == 0, "No runtime errors during demo validation");
    }
}
#endif
