#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Massive.Multiplier;
using Massive.Scoring;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.Singularity.Editor
{
    /// <summary>
    /// Opt-in, bounded test of the real carousel/instructions/match/result route.
    /// Call Begin in Play Mode in level select; poll Result; then leave Play Mode.
    /// No assets are edited or saved. Hardware input is not synthesized: the public
    /// navigation methods are used only after their normal gates have elapsed.
    /// </summary>
    public static class SingularityPlayableFlowValidation
    {
        private const string DefinitionPath = "Assets/Scripts/Level Select/LevelDefinition-SINGULARITY.asset";
        private const string GameplayScene = "S-7_SINGULARITY";
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly List<string> checks = new List<string>();
        private static readonly List<string> screenshots = new List<string>();
        private static readonly Dictionary<ScoreEconomyProfile, string> profiles = new Dictionary<ScoreEconomyProfile, string>();
        private static LevelDefinition definition;
        private static LevelCarouselController carousel;
        private static GameManagerScript manager;
        private static PlayerRosterController roster;
        private static MatchScoreService scores;
        private static SingularityAmplifierEncounter encounter;
        private static SingularityBlackHolePortal portal;
        private static SingularityPlayerAdapter player;
        private static AmplifierCoreGameplay depositedCore;
        private static float savedTimeScale, initialClock, expectedDuration;
        private static bool running, savedAutoReturn, hasAutoReturnOverride;
        private static bool sawPreparing, sawCountdown, sawResolving, sawComplete;
        private static bool sawPreparingGate, sawCountdownGate;
        private static int stage, steps, matchNumber, playerLife, initialTransfers, initialCaptures, screenshotFrame;
        private static long expectedLightScore, expectedDarkScore;
        private static double began, stageBegan, nextStep;
        private static double postGameReadyAt;
        private static Vector2 playerStart;
        private static Vector3 playerRestingScale;
        private static string callbackFailure;

        public static string Result { get; private set; } = "Not run";

        public static string Begin()
        {
            if (!Application.isPlaying || running)
                throw new InvalidOperationException("Requires Play Mode with no flow validation already running.");
            if (SceneManager.GetActiveScene().name != SceneFlow.LevelSelectScene)
                throw new InvalidOperationException("Start in " + SceneFlow.LevelSelectScene + ".");
            definition = AssetDatabase.LoadAssetAtPath<LevelDefinition>(DefinitionPath);
            if (!definition || definition.SceneName != GameplayScene)
                throw new InvalidOperationException("SINGULARITY's playable definition is missing or points to the wrong scene.");

            checks.Clear(); profiles.Clear(); screenshots.Clear(); callbackFailure = null;
            stage = steps = matchNumber = 0;
            postGameReadyAt = 0d;
            savedTimeScale = Time.timeScale;
            began = stageBegan = nextStep = EditorApplication.timeSinceStartup;
            running = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EditorApplication.update += Tick;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            try
            {
                Time.timeScale = 1f;
                GameFlowContext.EnsureExists();
                GameFlowContext.Instance.SetMode(GameMode.OneVOne);
                GameFlowContext.Instance.ClearLastMatchResult();
                Check(definition.levelNumber == 6 && definition.levelTitle == "SINGULARITY",
                    "Definition identifies STAGE_006 / SINGULARITY");
                Check(definition.iconPrefab && definition.instructionsPanelPrefab,
                    "Definition provides an icon and a dedicated instructions panel");
                SetStage(0, "Navigating the real level carousel");
            }
            catch (Exception e) { Finish("FAIL: " + e.Message); }
            return Result;
        }

        public static string Cancel()
        {
            if (running) Finish("Cancelled; temporary runtime settings restored");
            return Result;
        }

        private static void Tick()
        {
            if (!running) return;
            try
            {
                if (!Application.isPlaying) { Finish("Interrupted by leaving Play Mode"); return; }
                if (callbackFailure != null) throw new InvalidOperationException(callbackFailure);
                double now = EditorApplication.timeSinceStartup;
                if (now - began > 90d)
                    throw new TimeoutException("90-second flow deadline; " + Status());
                if (now - stageBegan > 27d)
                    throw new TimeoutException("Stage deadline; " + Status());
                ObserveClosedMatchGates();

                switch (stage)
                {
                    case 0:
                        carousel = UnityEngine.Object.FindFirstObjectByType<LevelCarouselController>();
                        if (!carousel || !carousel.SelectedLevel || now < nextStep) return;
                        if (carousel.SelectedLevel != definition)
                        {
                            if (++steps > 24) throw new InvalidOperationException("SINGULARITY is not reachable in the live catalog.");
                            carousel.StepNextStage(); nextStep = now + .3d; return;
                        }
                        Check(carousel.SelectedLevel == definition, "Real StepNextStage navigation reaches SINGULARITY");
                        Check(Read<Transform>(carousel, "carouselRoot").GetComponentsInChildren<SingularityLevelIcon>(true).Length > 0,
                            "Carousel instantiates the SINGULARITY 3D icon");
                        SetStage(1, "Waiting for normal carousel confirm lockout and rotation");
                        break;
                    case 1:
                        if (now - stageBegan < .6d) return;
                        bool unscaled = Read<bool>(carousel, "confirmLockoutUsesUnscaledTime");
                        if ((unscaled ? Time.unscaledTime : Time.time) < Read<float>(carousel, "_confirmAllowedAt")) return;
                        if (!CaptureAndWait("menu")) return;
                        SetStage(2, "Confirming selection and waiting for instructions gate");
                        carousel.ConfirmSelection();
                        break;
                    case 2:
                        if (SceneManager.GetActiveScene().name != SceneFlow.InstructionsScene) return;
                        var instructions = UnityEngine.Object.FindFirstObjectByType<InstructionsScreenController>();
                        if (!instructions || !Read<bool>(instructions, "_armed")) return;
                        if (!CaptureAndWait("instructions")) return;
                        Check(GameFlowContext.Instance.SelectedLevel == definition,
                            "ConfirmSelection carries SINGULARITY into the instructions scene");
                        Check(Header(instructions, "titleText") == "SINGULARITY" && Header(instructions, "stageText") == "STAGE_006",
                            "Instructions render the correct stage and title");
                        var anchor = Read<Transform>(instructions, "dynamicPanelAnchor");
                        Check(anchor && Enumerable.Range(0, anchor.childCount).Any(i =>
                            anchor.GetChild(i).name.StartsWith(definition.instructionsPanelPrefab.name, StringComparison.Ordinal)),
                            "Dedicated SINGULARITY instructions panel is instantiated");
                        Check(Read<bool>(instructions, "_armed"), "Mandatory instructions wait completes normally");
                        SetStage(3, "Launching 1v1 through the same scene-flow action as the armed instructions button");
                        SceneFlow.GoToSelectedGameplay();
                        break;
                    case 3:
                        if (!RegulationReady()) return;
                        if (!CaptureAndWait("gameplay")) return;
                        ValidateRegulation(false);
                        initialClock = manager.RegulationRemainingSeconds;
                        SetStage(4, "Checking live regulation clock");
                        break;
                    case 4:
                        if (now - stageBegan < .3d || manager.RegulationRemainingSeconds >= initialClock) return;
                        Check(manager.RegulationRemainingSeconds < initialClock, "Regulation clock advances without a prototype free-play session");
                        var timer = UnityEngine.Object.FindFirstObjectByType<MatchTimerPresenter>();
                        Check(timer && Read<GameManagerScript>(timer, "gameManager") == manager &&
                            !string.IsNullOrWhiteSpace(Header(timer, "timerText")), "Live timer presenter is bound and displaying the match clock");
                        BeginPlayerTransit();
                        SetStage(5, "Observing the real player's black-hole entry");
                        break;
                    case 5:
                        if (portal.PhaseOf(player) != SingularityBlackHolePortal.TransitPhase.Entering || portal.ElapsedOf(player) < .06f) return;
                        Check(player.HasPortalVisual && player.PortalVisualStretch > 1f && player.PortalVisualScale < 1f &&
                            player.Player.IsSingularityTransitControlled, "Live player enters with tidal deformation and a portal movement lease");
                        SetStage(6, "Waiting for player transfer and restored exit shape");
                        break;
                    case 6:
                        if (portal.TeleportCount <= initialTransfers || portal.IsInTransit(player)) return;
                        Check(player.RearWeight > .99f && portal.LastTeleportedPlayer == player,
                            "Live player transfers from the front to the back plane");
                        var body = player.GetComponent<Rigidbody>();
                        Check(!player.HasPortalVisual && !player.Player.IsSingularityTransitControlled &&
                            !body.isKinematic && body.detectCollisions && player.transform.localScale == playerRestingScale,
                            "Portal exit restores player shape, motion and collision ownership");
                        Check(player.Player.LifeSequence == playerLife && !player.Player.temporarilyEliminated,
                            "Portal traversal does not kill or respawn the player");
                        player.SetSurfacePosition(playerStart);
                        SetStage(7, "Waiting for the encounter's unmodified initial Core spawn");
                        break;
                    case 7:
                        var cycle = encounter.spawner;
                        if (cycle.Phase != AmplifierEncounterPhase.Active || !cycle.ActiveCore || cycle.ActiveCore.IsSpawning) return;
                        Check(cycle.ActivePattern && cycle.ActivePattern.InteractionEnabled && cycle.PairsSpawned == 1,
                            "Normal scoring-gated formation produces the first active Resonance/Core pair");
                        depositedCore = cycle.ActiveCore;
                        Check(depositedCore.GetComponent<SingularityAmplifierAdapter>() &&
                            depositedCore.GetComponent<SingularityRendererBrightness>(),
                            "Spawned Core receives folding, portal and backside presentation adapters");
                        Check(scores.GetTeamAmplifierMultiplier(1) == 1 && scores.GetTeamAmplifierMultiplier(2) == 1,
                            "Fresh match starts both team Amplifiers at x1");
                        initialCaptures = cycle.CapturesObserved;
                        var goal = UnityEngine.Object.FindObjectsByType<AmplifierGoalCapture>(FindObjectsSortMode.None)
                            .First(g => g.TeamID == 1 && g.gameObject.scene == manager.gameObject.scene);
                        depositedCore.Body.linearVelocity = Vector3.zero;
                        depositedCore.Body.angularVelocity = Vector3.zero;
                        depositedCore.Body.position = goal.CapturePoint.position;
                        depositedCore.transform.position = depositedCore.Body.position;
                        Physics.SyncTransforms();
                        SetStage(8, "Waiting for the normal Light goal capture");
                        break;
                    case 8:
                        if (encounter.spawner.CapturesObserved <= initialCaptures) return;
                        Check(depositedCore && depositedCore.HasBeenCaptured &&
                            scores.GetTeamAmplifierMultiplier(1) == 2 && scores.GetTeamAmplifierMultiplier(2) == 1,
                            "Real goal contact captures the Core and awards Light x2 only");
                        Check(encounter.spawner.Phase == AmplifierEncounterPhase.Dissolving,
                            "Goal capture dissolves the paired Resonance through its existing lifecycle");
                        ScoreAwardResult award;
                        Check(scores.TryAwardToPlayer(ScoreRewardKeys.EnergyPickupSmall, player.Player,
                            "singularity-playable-flow-" + began, player.transform.position, out award) && award.teamAmplifierMultiplier == 2,
                            "A registered player's normal score award uses the captured team multiplier");
                        ExpireRegulation();
                        SetStage(9, "Waiting for the real regulation-end and POSTGAME route");
                        break;
                    case 9:
                        if (!PostGameReady()) return;
                        if (postGameReadyAt == 0d) postGameReadyAt = now;
                        if (now - postGameReadyAt < 7d) return;
                        if (!CaptureAndWait("results")) return;
                        ValidatePostGame(GameMode.OneVOne);
                        GameFlowContext.Instance.SetMode(GameMode.TwoVTwo);
                        GameFlowContext.Instance.SelectLevel(definition);
                        GameFlowContext.Instance.ClearLastMatchResult();
                        SetStage(10, "Launching a second SINGULARITY match in 2v2");
                        SceneFlow.GoToSelectedGameplay();
                        break;
                    case 10:
                        if (!RegulationReady()) return;
                        ValidateRegulation(true);
                        initialClock = manager.RegulationRemainingSeconds;
                        SetStage(11, "Checking the 2v2 clock before normal match resolution");
                        break;
                    case 11:
                        if (now - stageBegan < .3d || manager.RegulationRemainingSeconds >= initialClock) return;
                        Check(manager.RegulationRemainingSeconds < initialClock, "2v2 regulation clock advances normally");
                        ExpireRegulation();
                        SetStage(12, "Waiting for 2v2 POSTGAME metadata");
                        break;
                    case 12:
                        if (!PostGameReady()) return;
                        ValidatePostGame(GameMode.TwoVTwo);
                        Finish("PASS: " + checks.Count + " playable SINGULARITY flow checks");
                        break;
                }
            }
            catch (Exception e) { Finish("FAIL at " + Status() + ": " + e.Message); }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!running || scene.name != GameplayScene) return;
            try
            {
                DetachManager();
                manager = UnityEngine.Object.FindFirstObjectByType<GameManagerScript>();
                roster = UnityEngine.Object.FindFirstObjectByType<PlayerRosterController>();
                scores = UnityEngine.Object.FindFirstObjectByType<MatchScoreService>();
                encounter = UnityEngine.Object.FindFirstObjectByType<SingularityAmplifierEncounter>();
                portal = encounter ? encounter.portal : null;
                Check(manager && roster && scores && encounter && encounter.spawner && portal,
                    "Playable scene supplies manager, roster, scoring, encounter and black-hole portal");
                var freePlay = UnityEngine.Object.FindFirstObjectByType<SingularityGameplaySession>();
                Check(freePlay == null || !freePlay.enabled,
                    "Prototype free-play session is absent or disabled in the production match");
                matchNumber++;
                sawPreparing = manager.Phase == MatchRuntimePhase.Preparing;
                sawCountdown = sawResolving = sawComplete = sawPreparingGate = sawCountdownGate = false;
                manager.PhaseChanged += OnPhaseChanged;
                savedAutoReturn = Read<bool>(manager, "autoReturnOnAllPlayersInactive");
                Write(manager, "autoReturnOnAllPlayersInactive", false);
                hasAutoReturnOverride = true;
                if (scores.Profile && !profiles.ContainsKey(scores.Profile)) profiles.Add(scores.Profile, JsonUtility.ToJson(scores.Profile));
                ObserveClosedMatchGates();
            }
            catch (Exception e) { callbackFailure = "sceneLoaded: " + e.Message; }
        }

        private static void OnPhaseChanged(MatchRuntimePhase phase)
        {
            if (phase == MatchRuntimePhase.Preparing) sawPreparing = true;
            if (phase == MatchRuntimePhase.Countdown) sawCountdown = true;
            if (phase == MatchRuntimePhase.Resolving) sawResolving = true;
            if (phase == MatchRuntimePhase.Complete) sawComplete = true;
        }

        private static void ObserveClosedMatchGates()
        {
            if (!manager || !scores || !encounter || !encounter.spawner) return;
            if (manager.Phase != MatchRuntimePhase.Preparing && manager.Phase != MatchRuntimePhase.Countdown) return;
            if (scores.IsScoringOpen || encounter.spawner.ActiveCore)
                throw new InvalidOperationException("Scoring or Core spawning opened before regulation in " + manager.Phase + ".");
            if (manager.Phase == MatchRuntimePhase.Preparing) sawPreparingGate = true;
            if (manager.Phase == MatchRuntimePhase.Countdown) sawCountdownGate = true;
        }

        private static bool RegulationReady() => manager && manager.Phase == MatchRuntimePhase.Regulation && scores && scores.IsScoringOpen;

        private static void ValidateRegulation(bool twoVTwo)
        {
            Check(sawPreparing && sawCountdown && sawPreparingGate && sawCountdownGate,
                "Match " + matchNumber + " observes Preparing and Countdown with scoring closed and no Core");
            Check(roster.IsRosterReady && roster.P1 && roster.P2 && roster.P3 && roster.P4,
                "Match " + matchNumber + " roster has all four authored player references and completed its spawn gate");
            Check(roster.P1.activeInHierarchy && roster.P3.activeInHierarchy &&
                roster.P2.activeInHierarchy == twoVTwo && roster.P4.activeInHierarchy == twoVTwo,
                twoVTwo ? "2v2 activates P1, P2, P3 and P4" : "1v1 activates P1 and P3 only");
            var active = new[] { roster.P1, roster.P2, roster.P3, roster.P4 }.Where(go => go.activeInHierarchy).ToArray();
            Check(active.All(go => go.GetComponent<SingularityPlayerAdapter>() &&
                go.GetComponent<SingularityPlayerAdapter>().Surface == encounter.surface &&
                portal.players.Contains(go.GetComponent<SingularityPlayerAdapter>())),
                "All active players are connected to the same folded surface and black-hole portal");
            Check(encounter.surface && encounter.grid && encounter.grid.blackHole == portal && portal.surface == encounter.surface,
                "Folded grid, portal attraction and encounter share the same surface");
            Check(encounter.spawner.waitForScoring && encounter.spawner.scoreService == scores && scores.IsChainClockRunning,
                "Encounter and score-chain clocks follow the authoritative match score service");
            Check(scores.Profile && Mathf.Approximately(manager.RegulationDurationSeconds, scores.Profile.RegulationDurationSeconds),
                "Regulation duration comes from the unchanged shared score economy");
            expectedDuration = manager.RegulationDurationSeconds;
        }

        private static void BeginPlayerTransit()
        {
            player = roster.P1.GetComponent<SingularityPlayerAdapter>();
            Check(player && player.Player.CanStartSingularityTransit, "Spawned Light player is available for a normal portal traversal");
            playerStart = player.SurfacePosition; playerRestingScale = player.transform.localScale;
            playerLife = player.Player.LifeSequence; initialTransfers = portal.TeleportCount;
            Vector3 local = encounter.surface.transform.InverseTransformPoint(portal.transform.position);
            player.SetSurfacePosition(portal.FaceToSurface(new Vector2(local.x - portal.EffectiveCaptureRadius * .8f, local.z), false));
            Physics.SyncTransforms();
        }

        private static void ExpireRegulation()
        {
            expectedLightScore = scores.LightScoreMilliElectronVolts;
            expectedDarkScore = scores.DarkScoreMilliElectronVolts;
            // Exercise the ordinary Update -> EndMatch -> PostGame path, without
            // mutating the shared economy's duration or calling private end logic.
            Write(manager, "_regulationRemainingSeconds", .05f);
        }

        private static bool PostGameReady()
        {
            if (SceneManager.GetActiveScene().name != SceneFlow.PostGameScene ||
                !GameFlowContext.Instance || !GameFlowContext.Instance.HasLastMatchResult) return false;
            var screen = UnityEngine.Object.FindFirstObjectByType<PostGameScreenController>();
            // sceneLoaded precedes Start; wait until the real result presenter
            // applies its header rather than racing the first rendered frame.
            return screen && Header(screen, "titleText") == "SINGULARITY" && Header(screen, "stageText") == "STAGE_006";
        }

        private static void ValidatePostGame(GameMode mode)
        {
            Check(sawResolving && sawComplete, "Match " + matchNumber + " resolves and completes through the normal manager phases");
            var result = GameFlowContext.Instance.LastMatchResult;
            Check(result.mode == mode && result.stageNumber == 6 && result.stageTitle == "SINGULARITY" &&
                result.gameplaySceneName == GameplayScene, "POSTGAME receives the correct SINGULARITY stage, scene and " + mode + " metadata");
            Check(result.LightScore == expectedLightScore && result.DarkScore == expectedDarkScore &&
                Mathf.Approximately(result.regulationDurationSeconds, expectedDuration),
                "POSTGAME retains authoritative scores and full authored regulation duration");
            var screen = UnityEngine.Object.FindFirstObjectByType<PostGameScreenController>();
            Check(Header(screen, "titleText") == "SINGULARITY" && Header(screen, "stageText") == "STAGE_006",
                "POSTGAME displays STAGE_006 and SINGULARITY");
        }

        private static string Header(object owner, string field)
        { var text = Read<TMP_Text>(owner, field); return text ? text.text : null; }
        private static T Read<T>(object owner, string field) => (T)FindField(owner, field).GetValue(owner);
        private static void Write(object owner, string field, object value) => FindField(owner, field).SetValue(owner, value);
        private static FieldInfo FindField(object owner, string field)
        {
            var member = owner.GetType().GetField(field, PrivateInstance);
            if (member == null) throw new MissingFieldException(owner.GetType().FullName, field);
            return member;
        }
        private static void Check(bool pass, string label)
        { if (!pass) throw new InvalidOperationException(label); checks.Add(label); }
        private static string Status() => "stage " + stage + ", scene " + SceneManager.GetActiveScene().name +
            (manager ? ", phase " + manager.Phase : "") + (encounter && encounter.spawner ? ", encounter " + encounter.spawner.Status : "");
        private static void SetStage(int value, string description)
        {
            stage = value; stageBegan = EditorApplication.timeSinceStartup; screenshotFrame = -1;
            Result = "Running: " + description + " (" + checks.Count + " checks passed)";
        }
        private static bool CaptureAndWait(string label)
        {
            if (screenshotFrame < 0)
            {
                string directory = System.IO.Path.Combine(Application.dataPath, "Screenshots");
                System.IO.Directory.CreateDirectory(directory);
                string stem = "SINGULARITY-playable-" + label;
                string path = System.IO.Path.Combine(directory, stem + ".png");
                // Re-running a failed flow must not overwrite an earlier capture.
                if (System.IO.File.Exists(path))
                    path = System.IO.Path.Combine(directory, stem + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".png");
                ScreenCapture.CaptureScreenshot(path);
                screenshots.Add(path); screenshotFrame = Time.frameCount;
            }
            // CaptureScreenshot completes at end of frame. Keep this scene alive
            // across rendering before the next normal scene-flow action.
            return Time.frameCount >= screenshotFrame + 2;
        }
        private static void BeforeReload() { if (running) Finish("Interrupted by script reload"); }
        private static void OnPlayModeChanged(PlayModeStateChange state)
        { if (running && state == PlayModeStateChange.ExitingPlayMode) Finish("Interrupted by leaving Play Mode"); }
        private static void DetachManager()
        {
            if (manager)
            {
                manager.PhaseChanged -= OnPhaseChanged;
                if (hasAutoReturnOverride) Write(manager, "autoReturnOnAllPlayersInactive", savedAutoReturn);
            }
            hasAutoReturnOverride = false;
        }
        private static void Finish(string summary)
        {
            if (!running) return;
            running = false;
            EditorApplication.update -= Tick;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            AssemblyReloadEvents.beforeAssemblyReload -= BeforeReload;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            try
            {
                DetachManager();
                if (portal && player && portal.IsInTransit(player)) portal.CancelAll();
                Time.timeScale = savedTimeScale;
                foreach (var pair in profiles)
                    if (pair.Key && JsonUtility.ToJson(pair.Key) != pair.Value)
                        summary = "FAIL: shared score economy changed during validation. " + summary;
            }
            catch (Exception e) { summary = "FAIL during cleanup: " + e.Message + ". " + summary; }
            finally
            {
                manager = null; roster = null; scores = null; encounter = null; portal = null; player = null;
                carousel = null; depositedCore = null; definition = null;
                Result = summary + "\n" + string.Join("\n", checks) +
                    (screenshots.Count > 0 ? "\nScreenshots:\n" + string.Join("\n", screenshots) : "") +
                    "\nScope: public menu actions after their gates; real runtime scene, roster, portal, Core goal, match timer and POSTGAME. " +
                    "No hardware/controller input or player build tested. Temporary runtime settings restored; leave Play Mode to discard test-match state.";
                Debug.Log(Result);
            }
        }
    }
}
#endif
