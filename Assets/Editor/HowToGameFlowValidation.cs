#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Massive.Demonstrations;
using Massive.Player;
using Massive.Scoring;
using Massive.Statistics;
using Rewired;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Exercises the shipped route with disposable Rewired input and match records.</summary>
[InitializeOnLoad]
public static class HowToGameFlowValidation
{
    const string Key = "MASSIVE.HowToGameFlowValidation";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();
    static readonly List<string> checks = new List<string>(), errors = new List<string>();
    static readonly HashSet<MatchRuntimePhase> phases = new HashSet<MatchRuntimePhase>();
    static readonly float[] axes = new float[4];
    static readonly int[] buttons = { -1, -1, -1, -1 };
    static CustomController[] controllers;
    static Rewired.InputManager inputManager;
    static MatchHistoryStore store;
    static object previousStore;
    static bool previousBackground, previousIgnoreFocus;
    static double deadline;
    static int lastFrame;
    static string output;
    public static string Status => SessionState.GetString(Key + ".status", "Not run");

    static HowToGameFlowValidation() => EditorApplication.playModeStateChanged += State;

    [MenuItem("MASSIVE/Demonstrations/Validate HOW TO Game Flow")]
    public static void Start() => Start(Path.Combine(Path.GetTempPath(), "MASSIVE-HowToFlow-" + Guid.NewGuid().ToString("N")));

    public static void Start(string directory)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start from Edit Mode.");
        SessionState.SetString(Key + ".scene", SceneManager.GetActiveScene().path);
        SessionState.SetBool(Key + ".dirty", SceneManager.GetActiveScene().isDirty);
        SessionState.SetString(Key + ".startScene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetString(Key + ".output", Path.GetFullPath(directory));
        SessionState.SetString(Key + ".status", "Starting at Attract");
        SessionState.SetBool(Key, true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/" + SceneFlow.ChooseModeScene + ".unity");
        EditorApplication.isPlaying = true;
    }

    static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            output = SessionState.GetString(Key + ".output", "");
            Directory.CreateDirectory(output);
            checks.Clear(); errors.Clear(); routines.Clear(); phases.Clear();
            controllers = null; inputManager = null;
            Array.Clear(axes, 0, axes.Length);
            for (int i = 0; i < buttons.Length; i++) buttons[i] = -1;
            store = new MatchHistoryStore(Path.Combine(output, "matches"));
            var field = typeof(MatchHistoryRuntime).GetField("store", BindingFlags.Static | BindingFlags.NonPublic);
            previousStore = field.GetValue(null);
            field.SetValue(null, store);
            previousBackground = Application.runInBackground;
            previousIgnoreFocus = ReInput.configuration.ignoreInputWhenAppNotInFocus;
            Application.runInBackground = true;
            deadline = EditorApplication.timeSinceStartup + 300;
            lastFrame = -1;
            Application.logMessageReceived += OnLog;
            routines.Push(Run());
            EditorApplication.update += Tick;
        }
        else if (state == PlayModeStateChange.ExitingPlayMode && routines.Count > 0)
            Finish(false, "Play Mode stopped before the route completed.");
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key + ".startScene", ""));
            bool restored = SceneManager.GetActiveScene().path == SessionState.GetString(Key + ".scene", "") &&
                SceneManager.GetActiveScene().isDirty == SessionState.GetBool(Key + ".dirty", false);
            File.AppendAllText(Path.Combine(SessionState.GetString(Key + ".output", ""), "report.txt"),
                "\n" + (restored ? "PASS" : "FAIL") + " Original scene and unsaved-state flag restored");
            if (!restored) SessionState.SetString(Key + ".status", "FAILED: original scene state was not restored");
            SessionState.SetBool(Key, false);
        }
    }

    static void OnLog(string message, string trace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            errors.Add(message + "\n" + trace);
    }

    static void Tick()
    {
        if (Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Flow validation deadline. " + Status);
            var manager = Object.FindFirstObjectByType<GameManagerScript>();
            if (manager)
            {
                phases.Add(manager.Phase);
                if (manager.Phase == MatchRuntimePhase.Countdown && manager.ScoreService.IsScoringOpen)
                    throw new InvalidOperationException("Scoring opened before the countdown finished.");
            }
            while (routines.Count > 0)
            {
                var routine = routines.Peek();
                if (!routine.MoveNext()) { routines.Pop(); continue; }
                if (routine.Current is IEnumerator nested) { routines.Push(nested); continue; }
                return;
            }
            Check(errors.Count == 0, "No runtime errors or exceptions");
            Finish(true, "");
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }

    static void Finish(bool passed, string detail)
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        routines.Clear();
        if (controllers != null && ReInput.isReady)
            foreach (var controller in controllers) ReInput.controllers.DestroyCustomController(controller);
        if (ReInput.isReady) ReInput.configuration.ignoreInputWhenAppNotInFocus = previousIgnoreFocus;
        typeof(MatchHistoryRuntime).GetField("store", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, previousStore);
        Application.runInBackground = previousBackground;
        string status = (passed ? "PASSED " : "FAILED after ") + checks.Count + " checks";
        File.WriteAllText(Path.Combine(output, "report.txt"), status + "\n" + string.Join("\n", checks) +
            "\n" + detail + "\n" + string.Join("\n", errors));
        SessionState.SetString(Key + ".status", status + " — " + output + (passed ? "" : " — " + detail));
        EditorApplication.isPlaying = false;
    }

    static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        checks.Add("PASS " + label);
    }
    static T Read<T>(object owner, string field) => (T)owner.GetType().GetField(field, Private).GetValue(owner);
    static void Write(object owner, string field, object value) => owner.GetType().GetField(field, Private).SetValue(owner, value);
    static IEnumerator Wait(double seconds)
    {
        double until = Time.realtimeSinceStartupAsDouble + seconds;
        while (Time.realtimeSinceStartupAsDouble < until) yield return null;
    }
    static IEnumerator Until(Func<bool> ready, string label, double seconds = 30)
    {
        SessionState.SetString(Key + ".status", label);
        double until = EditorApplication.timeSinceStartup + seconds;
        while (!ready())
        {
            if (EditorApplication.timeSinceStartup >= until) throw new TimeoutException(label);
            yield return null;
        }
    }

    static void BindInput()
    {
        ReInput.configuration.ignoreInputWhenAppNotInFocus = false;
        var current = Object.FindFirstObjectByType<Rewired.InputManager>();
        if (inputManager == current && controllers != null) return;
        inputManager = current;
        var data = current.userData;
        data.AddCustomController(Guid.NewGuid());
        var definition = data.GetCustomController(data.GetCustomControllerIds().Length - 1);
        definition.AddButton(); definition.AddButton(); definition.AddAxis();
        controllers = new CustomController[4];
        for (int slot = 0; slot < controllers.Length; slot++)
        {
            int playerId = slot;
            var controller = ReInput.controllers.CreateCustomController(definition.id);
            var player = ReInput.players.GetPlayer(slot);
            player.controllers.AddController(controller, false);
            var map = new CustomControllerMap { sourceControllerId = definition.id, enabled = true };
            // Rewired exposes these authoring setters internally; only this disposable map is changed.
            typeof(ControllerMap).GetProperty("categoryId").SetValue(map, 0, null);
            typeof(ControllerMap).GetProperty("layoutId").SetValue(map, 0, null);
            string[] actions = { "Sword", "Shield" };
            for (int i = 0; i < actions.Length; i++)
                map.CreateElementMap(ReInput.mapping.GetActionId(actions[i]), Pole.Positive,
                    definition.GetButtonElementIdentifierIds()[i], ControllerElementType.Button, AxisRange.Full, false);
            map.CreateElementMap(ReInput.mapping.GetActionId("MoveH"), Pole.Positive,
                definition.GetAxisElementIdentifierIds()[0], ControllerElementType.Axis, AxisRange.Full, false);
            player.controllers.maps.AddMap(controller, map);
            controller.SetAxisUpdateCallback(index => axes[playerId]);
            controller.SetButtonUpdateCallback(index => buttons[playerId] == index);
            controllers[slot] = controller;
        }
    }
    static IEnumerator Press(int player, int button)
    {
        BindInput(); buttons[player] = button;
        yield return Wait(.12);
        buttons[player] = -1;
        yield return Wait(.12);
    }
    static IEnumerator Flick(int player, float direction)
    {
        BindInput(); axes[player] = direction;
        yield return Wait(.15);
        axes[player] = 0;
        yield return Wait(.25);
    }
    static void Capture(string name) => ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png"));

    static IEnumerator Run()
    {
        var enabled = EditorBuildSettings.scenes.Where(s => s.enabled).ToArray();
        Check(Path.GetFileNameWithoutExtension(enabled[1].path) == SceneFlow.HowToPlayScene, "New HOW TO occupies the original enabled build index");
        Check(EditorBuildSettings.scenes.Any(s => s.path == "Assets/Scenes/S-0_HOW-TO-PLAY.unity" && !s.enabled), "Old HOW TO remains available outside the shipped scene flow");
        foreach (var mode in new[] { GameMode.OneVOne, GameMode.TwoVTwo })
        {
            bool teams = mode == GameMode.TwoVTwo;
            string title = teams ? "HIGGS" : "NOVA", prefix = teams ? "2v2" : "1v1";
            yield return Until(() => SceneManager.GetActiveScene().name == SceneFlow.ChooseModeScene && ReInput.isReady, "Attract startup");
            yield return null;
            BindInput();
            var bgm = BGMManager.Instance;
            var music = Read<BGMManager.SceneMusicOverride[]>(bgm, "sceneOverrides");
            var menuProfile = music.Single(m => m.sceneName == SceneFlow.HowToPlayScene).profile;
            var choose = Object.FindFirstObjectByType<ChooseModeScript>();
            Check(choose, prefix + " Attract has the normal mode-selection handler");
            choose.MoveToNextScene(teams);
            yield return Until(() => SceneManager.GetActiveScene().name == SceneFlow.HowToPlayScene, prefix + " mode selection enters new HOW TO");
            var howTo = Object.FindFirstObjectByType<HowToPlayScreenController>();
            var panes = Object.FindFirstObjectByType<HowToPlayCarousel>();
            Check(howTo && panes && panes.PaneCount == 4, prefix + " loads the four-pane carousel and Continue controller");
            Check(GameFlowContext.Instance.Mode == mode && !GameFlowContext.Instance.SelectedLevel, prefix + " mode survives onboarding with no stale level selection");
            Check(!Read<bool>(howTo, "_armed"), prefix + " Continue starts behind its mandatory wait");
            yield return Press(teams ? 3 : 0, teams ? 1 : 0);
            Check(SceneManager.GetActiveScene().name == SceneFlow.HowToPlayScene, prefix + " early button cannot skip the mandatory wait");
            yield return Until(() => Read<bool>(howTo, "_armed"), prefix + " Continue prompt arms normally");
            Check(Read<GameObject>(howTo, "pressAnyPromptRoot").activeInHierarchy, prefix + " PRESS ANY BUTTON prompt is visible");
            Check(SceneSfxPolicy.IsSuppressed && AudioListener.pause, prefix + " HOW TO SFX stay silent");
            Check(Read<LevelAudioProfile>(bgm, "_currentProfile") == menuProfile && bgm.GetComponentsInChildren<AudioSource>().Any(s => s.isPlaying && s.ignoreListenerPause), prefix + " menu music plays through HOW TO silence");
            if (!teams)
            {
                yield return Until(() => panes.CurrentIndex == 1 && !panes.IsTransitioning, "Automatic pane advancement", 25);
                Check(true, "Progress timer automatically advances the carousel");
                yield return Flick(0, -1);
                yield return Until(() => panes.CurrentIndex == 0 && !panes.IsTransitioning, "Joystick previous pane");
                Check(true, "Left joystick returns to previous pane");
                for (int i = 1; i < 4; i++)
                {
                    yield return Flick(0, 1);
                    int expected = i;
                    yield return Until(() => panes.CurrentIndex == expected && !panes.IsTransitioning, "Joystick next pane " + i);
                }
                Check(true, "Right joystick reaches all four prefab panes");
            }
            Capture(prefix + "-how-to"); yield return Wait(.1);
            yield return Press(teams ? 3 : 0, teams ? 1 : 0);
            yield return Until(() => SceneManager.GetActiveScene().name == SceneFlow.LevelSelectScene, prefix + " Continue button enters level select");
            Check(!SceneSfxPolicy.IsSuppressed && !AudioListener.pause, prefix + " leaving HOW TO restores gameplay audio");
            Check(Object.FindObjectsByType<PlayerDemoDirector>(FindObjectsSortMode.None).Length == 0 && !Object.FindFirstObjectByType<HowToPlayCarousel>(), prefix + " demonstration sessions and carousel unload");
            var carousel = Object.FindFirstObjectByType<LevelCarouselController>();
            yield return Until(() => carousel.SelectedLevel, prefix + " level catalog loads");
            for (int steps = 0; carousel.SelectedLevel.levelTitle != title && steps < 15; steps++) yield return Flick(0, 1);
            Check(carousel.SelectedLevel.levelTitle == title, prefix + " joystick selects " + title);
            var selected = carousel.SelectedLevel;
            yield return Until(() => (Read<bool>(carousel, "confirmLockoutUsesUnscaledTime") ? Time.unscaledTime : Time.time) >=
                Read<float>(carousel, "_confirmAllowedAt"), prefix + " level selection confirm gate");
            yield return Press(0, 0);
            yield return Until(() => SceneManager.GetActiveScene().name == SceneFlow.InstructionsScene, prefix + " level confirm opens instructions");
            var instructions = Object.FindFirstObjectByType<InstructionsScreenController>();
            yield return Until(() => instructions && Read<bool>(instructions, "_armed"), prefix + " instructions complete their normal input gate");
            Check(GameFlowContext.Instance.SelectedLevel == selected && GameFlowContext.Instance.Mode == mode, prefix + " instructions retain selected level and mode");
            var instructionProfile = music.FirstOrDefault(m => m.sceneName == SceneFlow.InstructionsScene).profile;
            Check(Read<LevelAudioProfile>(bgm, "_currentProfile") == (instructionProfile ? instructionProfile : selected.audioProfile), prefix + " instructions preserve their authored music mapping");
            Capture(prefix + "-instructions"); yield return Wait(.1);
            phases.Clear();
            yield return Press(teams ? 3 : 0, teams ? 1 : 0);
            yield return Until(() => SceneManager.GetActiveScene().name == selected.SceneName, prefix + " instructions button activates selected gameplay");
            var manager = Object.FindFirstObjectByType<GameManagerScript>();
            yield return Until(() => manager && manager.Phase == MatchRuntimePhase.Regulation, prefix + " spawn and countdown reach regulation");
            Check(!manager.IsStartupBlocked && phases.Contains(MatchRuntimePhase.Countdown), prefix + " normal startup/countdown succeeds");
            Check(Read<LevelAudioProfile>(bgm, "_currentProfile") == selected.audioProfile, prefix + " gameplay switches to selected stage music");
            var roster = Object.FindFirstObjectByType<PlayerRosterController>();
            var players = Object.FindObjectsByType<PlayerControllerScript>(FindObjectsSortMode.None).Where(p => p.ParticipatesInMatch).ToArray();
            Check(roster && roster.IsRosterReady && !roster.HasStartupFailed && players.Length == (teams ? 4 : 2), prefix + " correct active player roster");
            Check(players.All(p => !p.IsMatchInputLocked && !p.SimulationRoot) && manager.ScoreService.IsScoringOpen, prefix + " actual players accept input and scoring");
            Check(manager.ScoreService.GetTeamAmplifierMultiplier(1) == 1 && manager.ScoreService.GetTeamAmplifierMultiplier(2) == 1, prefix + " demo multipliers do not leak into gameplay");
            BindInput();
            var player = players.Single(p => p.playerID == 0);
            Vector3 initial = player.transform.position;
            float initialClock = manager.RegulationRemainingSeconds;
            axes[0] = 1; yield return Wait(.5); axes[0] = 0;
            Check(Vector3.Distance(initial, player.transform.position) > .1f && manager.RegulationRemainingSeconds < initialClock, prefix + " real joystick moves player and match clock advances");
            bool attacked = false;
            player.attackController.OnStageStarted.AddListener(stage => attacked = true);
            yield return Press(0, 0);
            Check(attacked, prefix + " actual Attack button starts a gameplay thrust");
            yield return Until(() => !player.attackController.IsAttacking, prefix + " thrust completes");
            yield return Press(0, 1);
            Check(player.GetComponent<PlayerShieldAbility>().IsActive, prefix + " actual Shield button defends");
            Check(manager.ScoreService.TryAwardToPlayer(ScoreRewardKeys.EnemyDefeat, player, Guid.NewGuid().ToString(), player.transform.position, out _), prefix + " live score service accepts an attributed reward");
            Capture(prefix + "-gameplay"); yield return Wait(.2);
            if (!teams)
            {
                Object.FindFirstObjectByType<NovaStarController>().entryWindowDuration = .5f;
                Object.FindFirstObjectByType<NovaAnomalyAdapter>().overrideDuration = 4f;
            }
            // Expire only this test match's clock; authored durations and gameplay settings are unchanged.
            Write(manager, "_regulationRemainingSeconds", .01f);
            yield return Until(() => SceneManager.GetActiveScene().name == SceneFlow.PostGameScene, prefix + " normal match completion reaches results", 60);
            var result = GameFlowContext.Instance.LastMatchResult;
            Check(GameFlowContext.Instance.HasLastMatchResult && result.mode == mode && result.stageTitle == title && result.LightScore > 0, prefix + " results retain stage, mode and awarded score");
            if (!teams) Check(phases.Contains(MatchRuntimePhase.Bonus), "NOVA terminal bonus remains in the route");
            Check(!SceneSfxPolicy.IsSuppressed && !AudioListener.pause && Mathf.Approximately(Time.timeScale, 1), prefix + " results restore normal time and audio");
            var results = Object.FindFirstObjectByType<PostGameScreenController>();
            yield return Until(() => Read<bool>(results, "_armed"), prefix + " results return gate");
            yield return Wait(.5);
            Check(Read<TMPro.TMP_Text>(results, "titleText").text.Contains(title), prefix + " results display the selected stage title");
            Capture(prefix + "-results"); yield return Wait(.2);
            if (!teams)
            {
                yield return Press(0, 1);
            }
            yield return Until(() => SceneManager.GetActiveScene().name == SceneFlow.ChooseModeScene, prefix + (teams ? " results auto-return" : " results button return"), 20);
            Check(!GameFlowContext.Instance.HasLastMatchResult && !GameFlowContext.Instance.SelectedLevel, prefix + " return to Attract clears previous match state");
            Check(!Object.FindFirstObjectByType<GameManagerScript>() && !SceneSfxPolicy.IsSuppressed, prefix + " gameplay cleans up on return to Attract");
        }
        Check(store.ReadAll().Count(r => r.status == MatchHistoryStatus.Completed) == 2, "Both test matches produce complete isolated records");
    }
}
#endif
