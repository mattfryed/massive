using System;
using System.Collections;
using Massive.Scoring;
using Massive.Statistics;
using UnityEngine;

[DisallowMultipleComponent]
public class GameManagerScript : MonoBehaviour
{
    [Header("Scoring")]
    [Tooltip("Scene-local authoritative score service. If omitted, one is found or created on this object.")]
    [SerializeField] private MatchScoreService scoreService;

    [Tooltip("Central balance asset. If omitted, the service's assigned profile is used; if neither is assigned, temporary runtime defaults are created.")]
    [SerializeField] private ScoreEconomyProfile scoreEconomyProfile;

    [Header("Match Timing")]
    [SerializeField, Min(0f)] private float preMatchCountdownSeconds = 3f;
    [SerializeField] private bool useUnscaledTimeForMatchClock = true;
    [SerializeField] private bool waitForRosterSpawnCompletion = true;

    [Tooltip("Optional level component implementing IMatchFinale. No provider keeps the normal timed-match ending.")]
    [SerializeField] private MonoBehaviour regulationFinale;
    private bool _finaleStarted;
    private bool _presentationFinaleActive;
    private long _regulationLightScore, _regulationDarkScore;
    public bool IsTerminalBonus => _finaleStarted;

    [Header("Post Game Transition")]
    [Tooltip("Delay before loading PostGame so end VFX can play. Uses realtime.")]
    [SerializeField, Min(0f)] private float postGameLoadDelaySeconds = 1f;

    [SerializeField] private bool disableGameplayOnEnd = true;
    [SerializeField] private bool engageDeathSphereOnEnd = true;

    [Header("Cabinet Empty Auto-Return")]
    [SerializeField] private bool autoReturnOnAllPlayersInactive = true;
    [SerializeField, Min(0f)] private float allPlayersInactiveHoldSeconds = 2.5f;
    [SerializeField] private bool useUnscaledTimeForHoldTimers = true;

    [Header("Final Scores (read-only at runtime)")]
    [SerializeField] private long finalLightMilliElectronVolts;
    [SerializeField] private long finalDarkMilliElectronVolts;

    [Header("Debug / Legacy Display (auto-filled from SelectedLevel)")]
    [SerializeField] private string levelName = "UNKNOWN";
    [SerializeField] private string levelNumber = "000";

    private bool _ending;
    private bool _countdownStarted;
    private bool _regulationExpiredDuringBonus;
    private bool _regulationClockRunsDuringBonus;
    private float _allInactiveTimer;
    private float _regulationDurationSeconds;
    private float _regulationRemainingSeconds;
    private float _bonusElapsedSeconds;
    private MatchHistoryRecorder _history;
    private bool _historyApplicationPaused;
    private bool _historyUnfocused;

    private GameObject _deathSphere;
    private GameObject _gameplayObjects;

    private PlayerRosterController _roster;
    private PlayerControllerScript _p1c;
    private PlayerControllerScript _p2c;
    private PlayerControllerScript _p3c;
    private PlayerControllerScript _p4c;

    public event Action<MatchRuntimePhase> PhaseChanged;
    public event Action<float> RegulationTimeChanged;
    public event Action<float> CountdownTimeChanged;

    public MatchRuntimePhase Phase { get; private set; } = MatchRuntimePhase.Preparing;
    public float RegulationRemainingSeconds => Mathf.Max(0f, _regulationRemainingSeconds);
    public float RegulationDurationSeconds => Mathf.Max(1f, _regulationDurationSeconds);
    // Available before Start and in editor previews; uses the same profile precedence as EnsureScoreService.
    public float ConfiguredRegulationDurationSeconds => scoreEconomyProfile ? scoreEconomyProfile.RegulationDurationSeconds
        : scoreService && scoreService.Profile ? scoreService.Profile.RegulationDurationSeconds : 120f;
    public float EncounterRegulationDurationSeconds => _regulationDurationSeconds > 0f
        ? _regulationDurationSeconds : ConfiguredRegulationDurationSeconds;
    public float BonusElapsedSeconds => Mathf.Max(0f, _bonusElapsedSeconds);
    public MatchScoreService ScoreService => scoreService;
    public long FinalLightMilliElectronVolts => finalLightMilliElectronVolts;
    public long FinalDarkMilliElectronVolts => finalDarkMilliElectronVolts;
    public string StartupFailureReason { get; private set; }
    public bool IsStartupBlocked => !string.IsNullOrEmpty(StartupFailureReason);

    private void Start()
    {
        GameFlowContext.EnsureExists();

        LevelDefinition def = GameFlowContext.Instance.SelectedLevel;
        if (def != null)
        {
            levelName = def.levelTitle;
            levelNumber = def.levelNumber.ToString("000");
        }

        _deathSphere = GameObject.FindWithTag("DeathSphere");
        _gameplayObjects = GameObject.FindWithTag("GameplayObjects");

        int rosterCount = 0;
        foreach (var roster in FindObjectsByType<PlayerRosterController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (roster.gameObject.scene != gameObject.scene) continue;
            _roster = roster;
            rosterCount++;
        }
        CachePlayerControllers();
        EnsureScoreService();
        RegisterActivePlayersWithScoreService();

        _regulationDurationSeconds = scoreService.Profile.RegulationDurationSeconds;
        _regulationRemainingSeconds = _regulationDurationSeconds;
        _bonusElapsedSeconds = 0f;
        finalLightMilliElectronVolts = 0L;
        finalDarkMilliElectronVolts = 0L;

        scoreService.ResetForMatch(openScoring: false);
        SetPlayersMatchInputLocked(true);
        SetPhase(MatchRuntimePhase.Preparing);
        RegulationTimeChanged?.Invoke(_regulationRemainingSeconds);

        if (rosterCount != 1)
        {
            RejectStartup(rosterCount == 0 ? "Missing PlayerRosterController binding in this scene." : $"Found {rosterCount} PlayerRosterControllers; expected exactly one scene roster.");
            return;
        }
        _roster.StartupFailed += OnRosterStartupFailed;
        if (!EnsureStartupRoster()) return;

        if (waitForRosterSpawnCompletion)
        {
            _roster.RosterReady += OnRosterReady;
            if (_roster.IsRosterReady)
                OnRosterReady();
        }
        else
        {
            StartCoroutine(BeginCountdownAfterOneFrame());
        }
    }

    private void OnDestroy()
    {
        InterruptHistory("Gameplay scene unloaded before completion.");
        if (_roster != null)
        {
            _roster.RosterReady -= OnRosterReady;
            _roster.StartupFailed -= OnRosterStartupFailed;
        }
    }

    private void Update()
    {
        SampleHistoryClock();
        if (_history != null && _history.CheckpointDue)
            _history.Checkpoint(BuildMatchResult(TeamSide.Tie), MatchHistoryRuntime.SettingsHash(scoreService.Profile));
        if (_ending || IsStartupBlocked) return;
        if (_presentationFinaleActive)
        {
            // A disabled/destroyed presentation must not strand a finished match.
            if (!regulationFinale || !regulationFinale.isActiveAndEnabled)
                EndMatchToPostGame(presentationComplete: true);
            return;
        }
        if ((Phase == MatchRuntimePhase.Preparing || Phase == MatchRuntimePhase.Countdown) &&
            (!_roster || !_roster.isActiveAndEnabled || _roster.HasStartupFailed) && !EnsureStartupRoster()) return;

        if (autoReturnOnAllPlayersInactive)
        {
            CheckForInactivePlayers();
            if (_ending) return;
        }

        if (Phase == MatchRuntimePhase.Regulation)
        {
            TickRegulationClock();
        }
        else if (Phase == MatchRuntimePhase.Bonus)
        {
            _bonusElapsedSeconds += MatchDeltaTime();

            if (_regulationClockRunsDuringBonus)
                TickRegulationClock(deferEndUntilBonusCompletes: true);
        }
    }

    /// <summary>
    /// Enters a scoring-active bonus phase. By default regulation time pauses,
    /// matching the intended NOVA bonus-round behavior.
    /// </summary>
    public bool BeginBonusRound(bool pauseRegulationClock = true, bool chainClockRuns = false)
    {
        if (_ending || _presentationFinaleActive || (Phase != MatchRuntimePhase.Regulation && Phase != MatchRuntimePhase.FinaleEntry))
            return false;

        _regulationClockRunsDuringBonus = !pauseRegulationClock;
        _regulationExpiredDuringBonus = false;
        SetPhase(MatchRuntimePhase.Bonus);
        scoreService.SetScoringState(scoringOpen: true, chainClockRunning: chainClockRuns);
        scoreService.SetBonusScoringOnly(true);
        return true;
    }

    public void EndBonusRound()
    {
        if (_ending || Phase != MatchRuntimePhase.Bonus)
            return;

        _regulationClockRunsDuringBonus = false;

        if (_finaleStarted || _regulationExpiredDuringBonus || _regulationRemainingSeconds <= 0f)
        {
            EndMatchToPostGame();
            return;
        }

        SetPhase(MatchRuntimePhase.Regulation);
        scoreService.SetBonusScoringOnly(false);
        scoreService.SetScoringState(scoringOpen: true, chainClockRunning: true);
    }

    private IEnumerator BeginCountdownAfterOneFrame()
    {
        yield return null;
        BeginCountdown();
    }

    private void OnRosterReady()
    {
        if (_roster != null)
            _roster.RosterReady -= OnRosterReady;

        CachePlayerControllers();
        RegisterActivePlayersWithScoreService();
        BeginCountdown();
    }

    private void BeginCountdown()
    {
        if (_ending || _countdownStarted)
            return;
        if (!EnsureStartupRoster()) return;

        _countdownStarted = true;
        StartCoroutine(CountdownRoutine());
    }

    private IEnumerator CountdownRoutine()
    {
        SetPhase(MatchRuntimePhase.Countdown);
        scoreService.SetScoringState(scoringOpen: false, chainClockRunning: false);
        SetPlayersMatchInputLocked(true);

        float remaining = Mathf.Max(0f, preMatchCountdownSeconds);
        CountdownTimeChanged?.Invoke(remaining);

        while (remaining > 0f && !_ending && !IsStartupBlocked)
        {
            remaining = Mathf.Max(0f, remaining - MatchDeltaTime());
            CountdownTimeChanged?.Invoke(remaining);
            yield return null;
        }

        if (!_ending && !IsStartupBlocked)
            BeginRegulation();
    }

    private void BeginRegulation()
    {
        if (!EnsureStartupRoster()) return;
        _regulationRemainingSeconds = _regulationDurationSeconds;
        _regulationExpiredDuringBonus = false;
        _regulationClockRunsDuringBonus = false;

        SetPlayersMatchInputLocked(false);
        SetPhase(MatchRuntimePhase.Regulation);
        scoreService.OpenScoring(chainClockRunning: true);
        if (_history == null)
            _history = MatchHistoryRuntime.Begin(GameFlowContext.Instance.SelectedLevel, gameObject.scene.name,
                scoreService.Profile, BuildMatchResult(TeamSide.Tie));
        RegulationTimeChanged?.Invoke(_regulationRemainingSeconds);
    }

    private bool EnsureStartupRoster()
    {
        if (IsStartupBlocked) return false;
        if (!_roster)
        {
            RejectStartup("Missing PlayerRosterController binding in this scene.");
            return false;
        }
        if (_roster.ValidateForMatch()) return true;
        OnRosterStartupFailed(_roster.StartupFailureReason);
        return false;
    }

    private void OnRosterStartupFailed(string reason) => RejectStartup(reason, logError: false);

    private void RejectStartup(string reason, bool logError = true)
    {
        if (IsStartupBlocked) return;
        StartupFailureReason = logError ? $"[Match] Startup blocked in scene '{gameObject.scene.name}': {reason} Fix the bindings and reload the scene." : reason;
        StopAllCoroutines();
        scoreService.CloseScoring();
        foreach (var player in FindObjectsByType<PlayerControllerScript>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (player && player.gameObject.scene == gameObject.scene && player.ParticipatesInMatch) player.SetMatchInputLocked(true);
        SetPhase(MatchRuntimePhase.Preparing);
        if (logError) Debug.LogError(StartupFailureReason, this);
    }

    private void TickRegulationClock(bool deferEndUntilBonusCompletes = false)
    {
        if (_regulationRemainingSeconds <= 0f)
        {
            if (deferEndUntilBonusCompletes)
                _regulationExpiredDuringBonus = true;
            else
                FinishRegulation();
            return;
        }

        _regulationRemainingSeconds = Mathf.Max(0f, _regulationRemainingSeconds - MatchDeltaTime());
        RegulationTimeChanged?.Invoke(_regulationRemainingSeconds);

        if (_regulationRemainingSeconds > 0f)
            return;

        if (deferEndUntilBonusCompletes)
            _regulationExpiredDuringBonus = true;
        else
            FinishRegulation();
    }

    private void FinishRegulation()
    {
        if (_ending || _finaleStarted) return;
        scoreService.CloseScoring();
        _regulationLightScore = scoreService.LightScoreMilliElectronVolts;
        _regulationDarkScore = scoreService.DarkScoreMilliElectronVolts;
        if (regulationFinale && regulationFinale.isActiveAndEnabled && regulationFinale is IMatchFinale finale)
        {
            _finaleStarted = true;
            SetPhase(MatchRuntimePhase.FinaleEntry);
            finale.BeginMatchFinale();
        }
        else EndMatchToPostGame();
    }

    /// <summary>Claim the selected finale as a non-scoring presentation. NOVA's bonus path is unchanged.</summary>
    public bool BeginPresentationFinale(MonoBehaviour owner)
    {
        if (!owner || owner != regulationFinale || !_finaleStarted || _ending ||
            Phase != MatchRuntimePhase.FinaleEntry) return false;
        _presentationFinaleActive = true;
        scoreService.CloseScoring();
        SetPlayersMatchInputLocked(true);
        if (_gameplayObjects) _gameplayObjects.SetActive(false);
        return true;
    }

    public void CompletePresentationFinale(MonoBehaviour owner)
    {
        if (owner != regulationFinale || !_presentationFinaleActive || _ending) return;
        EndMatchToPostGame(presentationComplete: true);
    }

    private float MatchDeltaTime()
    {
        return useUnscaledTimeForMatchClock ? Time.unscaledDeltaTime : Time.deltaTime;
    }

    private void EnsureScoreService()
    {
        if (scoreService == null)
            scoreService = FindFirstObjectByType<MatchScoreService>();

        if (scoreService == null)
            scoreService = gameObject.AddComponent<MatchScoreService>();

        if (scoreEconomyProfile != null)
            scoreService.Configure(scoreEconomyProfile, resetForMatch: true);
    }

    private void RegisterActivePlayersWithScoreService()
    {
        if (scoreService == null) return;

        RegisterIfActive(_p1c);
        RegisterIfActive(_p2c);
        RegisterIfActive(_p3c);
        RegisterIfActive(_p4c);
    }

    private void RegisterIfActive(PlayerControllerScript player)
    {
        if (player == null || !player.gameObject.activeInHierarchy)
            return;

        scoreService.RegisterPlayer(player);
    }

    private void CachePlayerControllers()
    {
        if (_roster == null)
        {
            GameObject p1 = GameObject.Find("Players/Player 1");
            GameObject p2 = GameObject.Find("Players/Player 2");
            GameObject p3 = GameObject.Find("Players/Player 3");
            GameObject p4 = GameObject.Find("Players/Player 4");

            _p1c = p1 != null ? p1.GetComponent<PlayerControllerScript>() : null;
            _p2c = p2 != null ? p2.GetComponent<PlayerControllerScript>() : null;
            _p3c = p3 != null ? p3.GetComponent<PlayerControllerScript>() : null;
            _p4c = p4 != null ? p4.GetComponent<PlayerControllerScript>() : null;
            return;
        }

        _p1c = _roster.P1 != null ? _roster.P1.GetComponent<PlayerControllerScript>() : null;
        _p2c = _roster.P2 != null ? _roster.P2.GetComponent<PlayerControllerScript>() : null;
        _p3c = _roster.P3 != null ? _roster.P3.GetComponent<PlayerControllerScript>() : null;
        _p4c = _roster.P4 != null ? _roster.P4.GetComponent<PlayerControllerScript>() : null;
    }

    private void SetPlayersMatchInputLocked(bool locked)
    {
        if (_p1c) _p1c.SetMatchInputLocked(locked);
        if (_p2c) _p2c.SetMatchInputLocked(locked);
        if (_p3c) _p3c.SetMatchInputLocked(locked);
        if (_p4c) _p4c.SetMatchInputLocked(locked);
    }

    private void CheckForInactivePlayers()
    {
        bool allInactive = AreAllActivePlayersInactive(out int activePlayerCount);

        if (activePlayerCount <= 0)
        {
            _allInactiveTimer = 0f;
            return;
        }

        if (allInactive)
        {
            float dt = useUnscaledTimeForHoldTimers ? Time.unscaledDeltaTime : Time.deltaTime;
            _allInactiveTimer += dt;

            if (_allInactiveTimer >= allPlayersInactiveHoldSeconds)
                AbortMatchToAttract();
        }
        else
        {
            _allInactiveTimer = 0f;
        }
    }

    private bool AreAllActivePlayersInactive(out int activeCount)
    {
        int active = 0;
        int inactive = 0;

        CountActiveInactive(_p1c, ref active, ref inactive);
        CountActiveInactive(_p2c, ref active, ref inactive);
        CountActiveInactive(_p3c, ref active, ref inactive);
        CountActiveInactive(_p4c, ref active, ref inactive);

        activeCount = active;
        return active > 0 && inactive == active;
    }

    private static void CountActiveInactive(PlayerControllerScript player, ref int active, ref int inactive)
    {
        if (player == null || !player.gameObject.activeInHierarchy)
            return;

        active++;
        if (!player.isActive)
            inactive++;
    }

    private void AbortMatchToAttract()
    {
        if (_ending) return;
        SampleHistoryClock();
        _history?.Finish(MatchHistoryStatus.Abandoned, "All players inactive; returned to Attract.",
            BuildMatchResult(TeamSide.Tie), MatchHistoryRuntime.SettingsHash(scoreService.Profile));
        _ending = true;

        scoreService?.CloseScoring();

        GameFlowContext.EnsureExists();
        GameFlowContext.Instance.ClearLastMatchResult();
        GameFlowContext.Instance.ClearSelection();

        SceneFlow.GoToChooseMode();
    }

    private void EndMatchToPostGame(bool presentationComplete = false)
    {
        if (_ending) return;
        _ending = true;
        _presentationFinaleActive = false;

        SetPhase(MatchRuntimePhase.Resolving);
        scoreService.CloseScoring();
        SetPlayersMatchInputLocked(true);

        finalLightMilliElectronVolts = scoreService.LightScoreMilliElectronVolts;
        finalDarkMilliElectronVolts = scoreService.DarkScoreMilliElectronVolts;
        if (!_finaleStarted)
        {
            _regulationLightScore = finalLightMilliElectronVolts;
            _regulationDarkScore = finalDarkMilliElectronVolts;
        }

        TeamSide winner = DetermineWinner(finalLightMilliElectronVolts, finalDarkMilliElectronVolts);
        MatchResult result = BuildMatchResult(winner);
        _history?.Finish(MatchHistoryStatus.Completed, "Match completed.", result, MatchHistoryRuntime.SettingsHash(scoreService.Profile));


        if (disableGameplayOnEnd && _gameplayObjects != null)
            _gameplayObjects.SetActive(false);

        if (!presentationComplete && engageDeathSphereOnEnd && _deathSphere != null)
        {
            DSScript deathSphere = _deathSphere.GetComponent<DSScript>();
            if (deathSphere != null)
                deathSphere.Engage();
        }

        GameFlowContext.EnsureExists();
        GameFlowContext.Instance.SetLastMatchResult(result);

        StartCoroutine(LoadPostGameAfterDelay(presentationComplete));
    }

    private IEnumerator LoadPostGameAfterDelay(bool presentationComplete)
    {
        if (!presentationComplete && postGameLoadDelaySeconds > 0f)
            yield return new WaitForSecondsRealtime(postGameLoadDelaySeconds);

        SetPhase(MatchRuntimePhase.Complete);
        SceneFlow.GoToPostGame();
    }

    private static TeamSide DetermineWinner(long lightScore, long darkScore)
    {
        if (lightScore > darkScore) return TeamSide.Light;
        if (darkScore > lightScore) return TeamSide.Dark;
        return TeamSide.Tie;
    }

    private MatchResult BuildMatchResult(TeamSide winner)
    {
        GameFlowContext context = GameFlowContext.Instance;
        LevelDefinition def = context.SelectedLevel;
        ScoreEconomyProfile profile = scoreService.Profile;

        return new MatchResult
        {
            winner = winner,
            lightMilliElectronVolts = scoreService.LightScoreMilliElectronVolts,
            darkMilliElectronVolts = scoreService.DarkScoreMilliElectronVolts,
            lightAmplifier = scoreService.GetTeamAmplifierStatistics(1),
            darkAmplifier = scoreService.GetTeamAmplifierStatistics(2),
            hasBonusBreakdown = _finaleStarted,
            regulationLightMilliElectronVolts = _finaleStarted ? _regulationLightScore : scoreService.LightScoreMilliElectronVolts,
            regulationDarkMilliElectronVolts = _finaleStarted ? _regulationDarkScore : scoreService.DarkScoreMilliElectronVolts,
            bonusLightMilliElectronVolts = _finaleStarted ? Math.Max(0L, scoreService.LightScoreMilliElectronVolts - _regulationLightScore) : 0L,
            bonusDarkMilliElectronVolts = _finaleStarted ? Math.Max(0L, scoreService.DarkScoreMilliElectronVolts - _regulationDarkScore) : 0L,
            mode = context.Mode,

            stageNumber = def != null ? def.levelNumber : 0,
            stageTitle = def != null ? def.levelTitle : "UNKNOWN",
            gameplaySceneName = def != null ? def.SceneName : null,

            regulationDurationSeconds = _regulationDurationSeconds,
            bonusDurationSeconds = _bonusElapsedSeconds,
            scoringRulesetId = profile != null ? profile.RulesetId : "UNVERSIONED",
            scoringRulesetVersion = profile != null ? profile.RulesetVersion : 0,
            playerContributions = scoreService.GetContributionSnapshot(),
            playerStatistics = scoreService.GetStatisticsSnapshot(),
            scoreTelemetry = scoreService.GetTelemetrySnapshot()
        };
    }

    private void SetPhase(MatchRuntimePhase phase)
    {
        if (Phase == phase) return;
        SampleHistoryClock();
        Phase = phase;
        PhaseChanged?.Invoke(Phase);
    }

    private void SampleHistoryClock()
    {
        _history?.Sample(Time.realtimeSinceStartupAsDouble, Time.unscaledTimeAsDouble, Phase,
            scoreService && scoreService.IsScoringOpen, Time.timeScale > 0f,
            _historyApplicationPaused || (_historyUnfocused && !Application.runInBackground));
    }

    private void InterruptHistory(string reason)
    {
        if (_history == null || _history.IsFinished) return;
        SampleHistoryClock();
        // On destruction the score service/context may already be gone. Keep the latest checkpoint then.
        var snapshot = scoreService && GameFlowContext.Instance ? BuildMatchResult(TeamSide.Tie) : _history.Record.result;
        _history.Finish(MatchHistoryStatus.Interrupted, reason, snapshot,
            scoreService ? MatchHistoryRuntime.SettingsHash(scoreService.Profile) : _history.Record.scoringSettingsHash);
    }

    private void OnApplicationQuit() => InterruptHistory("Application quit before completion.");

    private void OnApplicationPause(bool paused)
    {
        SampleHistoryClock();
        _historyApplicationPaused = paused;
        if (paused && _history != null && !_history.IsFinished && scoreService)
            _history.Checkpoint(BuildMatchResult(TeamSide.Tie), MatchHistoryRuntime.SettingsHash(scoreService.Profile), synchronous: true);
    }

    private void OnApplicationFocus(bool focused)
    {
        SampleHistoryClock();
        _historyUnfocused = !focused;
    }
}
