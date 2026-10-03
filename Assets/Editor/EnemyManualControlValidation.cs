#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Massive.Demonstrations;
using Massive.Enemies;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class EnemyManualControlValidation
{
    private const string Key = "EnemyManualControlValidation.Run", Folder = "Library/EnemyManualControlValidation";
    private static IEnumerator routine;
    private static int frame;
    private static float resume;
    private static double deadline;
    private static readonly List<string> results = new(), errors = new();
    private static readonly List<Object> owned = new();
    static EnemyManualControlValidation()
    {
        EditorApplication.playModeStateChanged += State;
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(Folder + "/run.request") || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Folder + "/run.request"); Run();
        };
    }
    [MenuItem("MASSIVE/Demonstrations/Validate Timeline Manual Control")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        Directory.CreateDirectory(Folder); File.WriteAllText(Folder + "/report.txt", "RUNNING\n");
        var lab = Object.FindFirstObjectByType<EnemyEncounterLab>(FindObjectsInactive.Include);
        var region = lab && lab.director ? lab.director.placementRegion : null;
        File.WriteAllText(Folder + "/preview-context.txt", "Placement region: " + (region ? region.name : "none") + "\n" +
            (region ? EditorJsonUtility.ToJson(region, true) : "") + "\nLayout center: " + (lab && lab.layout ? lab.layout.World(Vector2.zero).ToString("F3") : "none"));
        SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
    }
    private static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            frame = 0; resume = 0; results.Clear(); errors.Clear(); owned.Clear(); deadline = EditorApplication.timeSinceStartup + 240;
            SessionState.SetBool(Key + "Background", Application.runInBackground); Application.runInBackground = true;
            Application.logMessageReceived += Log; routine = Checks(); EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.ExitingPlayMode && routine != null)
        {
            EditorApplication.update -= Tick; Application.logMessageReceived -= Log; routine = null;
            File.WriteAllText(Folder + "/report.txt", "INTERRUPTED\n" + string.Join("\n", results) +
                "\nPlay Mode ended before validation completed. Manual input checks require a focused Game view.\n");
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        { Application.runInBackground = SessionState.GetBool(Key + "Background", true); SessionState.SetBool(Key, false); }
    }
    private static void Log(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || routine == null || Time.frameCount <= frame || Time.time < resume) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Validation timed out");
            if (routine.MoveNext()) { frame = Time.frameCount; resume = Time.time + (routine.Current is float f ? f : 0); }
            else Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log; routine = null;
        bool pass = error == null && errors.Count == 0;
        if (File.Exists(Folder + "/input.request")) File.Delete(Folder + "/input.request");
        File.WriteAllText(Folder + "/report.txt", (pass ? "PASSED" : "FAILED") + "\n" + string.Join("\n", results) + "\n" + error + "\n" + string.Join("\n", errors));
        Debug.Log("[Timeline manual control validation] " + (pass ? "PASSED" : "FAILED: " + error));
        foreach (var obj in owned) if (obj) Object.Destroy(obj);
        Time.timeScale = 1; EditorApplication.isPlaying = false;
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        results.Add("PASS " + message); File.WriteAllText(Folder + "/report.txt", "RUNNING\n" + string.Join("\n", results));
    }
    private static T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    private static IEnumerator Checks()
    {
        var lab = Object.FindFirstObjectByType<EnemyLab>();
        Check(lab && lab.timelinePreview, "Enemy Lab available");
        lab.mode = EnemyLab.LabMode.EncounterTimeline;
        var preview = lab.timelinePreview;
        while (!preview.isActiveAndEnabled || preview.Players.Count != 2) yield return null;
        var director = preview.director;
        var source = director.encounterTimeline;
        string original = EditorJsonUtility.ToJson(source);
        var empty = Own(ScriptableObject.CreateInstance<EnemyEncounterTimeline>()); empty.duration = 1000;
        director.encounterTimeline = empty; director.previewCue = -1; director.timelinePaused = false;
        preview.loop = preview.actorsMove = preview.actorsAttack = false; preview.manualPlayerControl = false;
        preview.RestartPreview();
        var gameView = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")); gameView.Focus();
        // EditorWindow.Focus alone cannot bring the OS application to the foreground.
        // Manual input intentionally locks while the Game view is unfocused.
        while (!Application.isFocused) yield return null;
        yield return .4f;
        var player = preview.Players[0]; var partner = preview.Players[1]; var scope = player.SimulationRoot;
        var body = player.GetComponent<Rigidbody>();
        Check(player.ControlMode == PlayerControlMode.Scripted && partner.ControlMode == PlayerControlMode.Scripted, "Preview defaults to automatic control");
        bool manualFrame = false, manualAimOverride = false;
        player.InputApplied += input =>
        {
            if (player.ControlMode != PlayerControlMode.Rewired) return;
            manualFrame = true; manualAimOverride |= input.hasAimDirWS;
        };
        player.SetScriptedInput(new PlayerInputFrame { moveInput = Vector2.left, attackHeld = true, hasAimDirWS = true, aimDirWS = Vector3.back });
        float age = director.GameplayAge;
        preview.manualPlayerControl = true;
        yield return .2f;
        Check(preview.ManualPlayer == player && player.ControlMode == PlayerControlMode.Rewired && player.playerID == 0,
            "Manual toggle binds the existing left actor to normal Rewired Player 1 (focused=" + Application.isFocused + ", mode=" + player.ControlMode + ")");
        Check(partner.ControlMode == PlayerControlMode.Scripted, "Second preview actor remains automatic");
        Check(preview.Players[0] == player && player.SimulationRoot == scope && director.GameplayAge > age,
            "Taking control preserves actors, targeting scope and timeline progress");
        Check(manualFrame && !manualAimOverride, "Manual input clears old scripted commands and receives no automatic aim");
        Check(player.IsPseudoPlayer && !player.ParticipatesInMatch && player.UsesGameplayTuning, "Controlled preview actor stays isolated from match scoring and uses gameplay tuning");
        Check(PlayerControllerScript.ActivePlayers.Where(p => p && !p.IsPseudoPlayer).All(p => !p.SharesSimulationWith(player)), "Manual control preserves separation from the real roster");
        var rewired = Rewired.ReInput.players.GetPlayer(0);
        File.WriteAllText(Folder + "/keyboard.txt", string.Join("\n", rewired.controllers.maps.GetAllMaps(Rewired.ControllerType.Keyboard)
            .SelectMany(m => m.AllMaps).Select(m => Rewired.ReInput.mapping.GetAction(m.actionId).name + "=" + m.keyCode)));
        Check(rewired.controllers.maps.GetAllMaps(Rewired.ControllerType.Keyboard).Where(m => m.enabled)
            .SelectMany(m => m.AllMaps).Select(m => Rewired.ReInput.mapping.GetAction(m.actionId).name)
            .Distinct().Intersect(new[] { "MoveH", "MoveV", "Sword", "Shield" }).Count() == 4,
            "Active Player 1 keyboard maps contain movement, Sword and Shield actions");
        Check(ReferenceEquals(typeof(PlayerControllerScript).GetField("rewiredPlayer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player), rewired),
            "Controlled actor reads the existing Player 1 input source");
        var shield = player.GetComponent<Massive.Player.PlayerShieldAbility>();
        Check(shield.TryActivate(), "Controlled actor retains its real shield ability");
        shield.SetHeld(true); body.linearVelocity = Vector3.right * 2f;
        director.timelinePaused = true; yield return .15f;
        float pausedAge = director.GameplayAge;
        Check(player.IsMatchInputLocked && player.ControlMode == PlayerControlMode.Disabled && !shield.IsActive && !player.attackController.IsAttacking,
            "Pause locks manual input and cancels held attack/shield state");
        Check(partner.IsMatchInputLocked && body.linearVelocity.sqrMagnitude < .0001f, "Pause also locks automatic actors and stops manual momentum");
        yield return .2f;
        Check(director.GameplayAge == pausedAge, "Manual takeover does not alter timeline pause semantics");
        director.timelinePaused = false; yield return .1f;
        Check(player.ControlMode == PlayerControlMode.Rewired && !player.IsMatchInputLocked, "Resume returns to manual control");
        preview.manualPlayerControl = false; preview.actorsMove = true; yield return .15f;
        Check(!preview.ManualPlayer && player.ControlMode == PlayerControlMode.Scripted && preview.Players[0] == player,
            "Returning to automatic control keeps the same actor");
        preview.manualPlayerControl = true; preview.RestartPreview(); yield return .2f;
        Check(preview.ManualPlayer && preview.ManualPlayer != player && preview.ManualPlayer.ControlMode == PlayerControlMode.Rewired,
            "Restart preserves the manual preference and binds the new actor");
        empty.duration = .5f; preview.loop = true;
        int loops = preview.CompletedLoops;
        while (preview.CompletedLoops == loops) yield return null;
        yield return .1f;
        Check(preview.ManualPlayer && preview.ManualPlayer.ControlMode == PlayerControlMode.Rewired, "Timeline looping restores manual control on the next actor");
        preview.loop = false;
        preview.gameObject.SetActive(false); yield return .1f;
        Check(preview.Players.Count == 0 && !preview.ManualPlayer, "Disabling the Lab removes the manual actor and session");
        preview.gameObject.SetActive(true); yield return .2f;
        Check(preview.ManualPlayer && preview.ManualPlayer.ControlMode == PlayerControlMode.Rewired, "Re-enabling the preview restores manual control cleanly");
        Check(EditorJsonUtility.ToJson(source) == original, "Manual preview does not modify the authored timeline");
        Check(errors.Count == 0, "No runtime errors during manual control validation");
    }
}
#endif
