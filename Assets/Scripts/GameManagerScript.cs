using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class GameManagerScript : MonoBehaviour
{
    [Header("Score Source (Preferred)")]
    [Tooltip("Authoritative score holders. If left empty, we'll try to auto-find by teamID (1=Light, 2=Dark).")]
    [SerializeField] private ScoreSphereScript lightScoreSphere; // teamID = 1
    [SerializeField] private ScoreSphereScript darkScoreSphere;  // teamID = 2

    [Header("Score Bars (Fallback)")]
    [Tooltip("Legacy fallback if ScoreSpheres aren't assigned/found. Raw score is bar X scale.")]
    [SerializeField] private GameObject Score_1; // LIGHT visual bar
    [SerializeField] private GameObject Score_2; // DARK  visual bar

    [Header("Win / Lose Thresholds (Score01 space)")]
    [Tooltip("Winning threshold in Score01 space. Typical win is 1.0 (100%). If you want 10%, set 0.1.")]
    [Range(0f, 1f)]
    [SerializeField] private float winScore01Threshold = 1.0f;

    [Tooltip("If a team's score01 hits this (usually 0), they lose.")]
    [Range(0f, 1f)]
    [SerializeField] private float loseScore01Threshold = 0.0f;

    [Header("Normalization (for PostGame UI)")]
    [Tooltip("Used as 'winScale' in MatchResult. We store raw = score01 * winScale so MatchResult.Light01/Dark01 still work.")]
    [SerializeField] private float winScale = 10.9f;

    [Header("Post Game Transition")]
    [Tooltip("Delay before loading PostGame (lets VFX / DeathSphere play). Uses realtime so it works even if timeScale=0.")]
    [SerializeField] private float postGameLoadDelaySeconds = 1.0f;

    [Tooltip("Disable GameplayObjects root when match ends.")]
    [SerializeField] private bool disableGameplayOnEnd = true;

    [Tooltip("Engage DeathSphere on match end (if found).")]
    [SerializeField] private bool engageDeathSphereOnEnd = true;

    [Header("Cabinet Empty Auto-Return")]
    [Tooltip("If everyone walks away mid-match (all active players inactive), return to Attract/Mode Select.")]
    [SerializeField] private bool autoReturnOnAllPlayersInactive = true;

    [Tooltip("How long all active players must be inactive before returning to Attract.")]
    [SerializeField] private float allPlayersInactiveHoldSeconds = 2.5f;

    [Tooltip("Use unscaled time for inactivity hold timer (recommended for arcade reliability).")]
    [SerializeField] private bool useUnscaledTimeForHoldTimers = true;

    [Header("Final Scores (written on match end)")]
    public float finalScore_1; // Light raw (score01 * winScale)
    public float finalScore_2; // Dark raw  (score01 * winScale)

    [Header("Debug / Legacy Display (auto-filled from SelectedLevel)")]
    [SerializeField] private string levelName = "UNKNOWN";
    [SerializeField] private string levelNumber = "000";

    private bool _ending;
    private float _allInactiveTimer;

    private GameObject _gmm;
    private GameObject _deathSphere;
    private GameObject _gameplayObjects;

    private PlayerRosterController _roster;
    private PlayerControllerScript _p1c, _p2c, _p3c, _p4c;

    private void Start()
    {
        GameFlowContext.EnsureExists();

        // Auto-fill stage info from selected level (for debug / legacy UI usage)
        var def = GameFlowContext.Instance.SelectedLevel;
        if (def != null)
        {
            levelName = def.levelTitle;
            levelNumber = def.levelNumber.ToString("000");
        }

        _gmm = GameObject.FindWithTag("GameMusicManager");
        _deathSphere = GameObject.FindWithTag("DeathSphere");
        _gameplayObjects = GameObject.FindWithTag("GameplayObjects");

        _roster = FindFirstObjectByType<PlayerRosterController>();
        CachePlayerControllers();

        // Prefer ScoreSphereScript as the authoritative score state
        AutoFindScoreSpheresIfNeeded();
    }

    private void CachePlayerControllers()
    {
        if (_roster == null)
        {
            // Fallback: one-time scene find
            var p1 = GameObject.Find("Players/Player 1");
            var p2 = GameObject.Find("Players/Player 2");
            var p3 = GameObject.Find("Players/Player 3");
            var p4 = GameObject.Find("Players/Player 4");

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

    private void AutoFindScoreSpheresIfNeeded()
    {
        if (lightScoreSphere != null && darkScoreSphere != null) return;

        // Unity 6+ safe runtime find
        var spheres = FindObjectsByType<ScoreSphereScript>(FindObjectsSortMode.None);
        if (spheres == null || spheres.Length == 0) return;

        foreach (var s in spheres)
        {
            if (s == null) continue;
            if (lightScoreSphere == null && s.teamID == 1) lightScoreSphere = s;
            if (darkScoreSphere == null && s.teamID == 2) darkScoreSphere = s;
        }
    }

    private void Update()
    {
        if (_ending) return;

        // 1) Abandon check (cabinet empty)
        if (autoReturnOnAllPlayersInactive)
        {
            CheckForInactivePlayers();
            if (_ending) return;
        }

        // 2) Win check
        if (!TryGetScores01(out float light01, out float dark01))
            return;

        if (HasWinCondition01(light01, dark01))
        {
            EndMatchToPostGame(light01, dark01);
        }
    }

    // --------------------------
    // Score reading
    // --------------------------

    private bool TryGetScores01(out float light01, out float dark01)
    {
        light01 = 0f;
        dark01 = 0f;

        // Preferred source: ScoreSphereScript (authoritative score state)
        if (lightScoreSphere != null && darkScoreSphere != null)
        {
            light01 = lightScoreSphere.Score01;
            dark01  = darkScoreSphere.Score01;
            return true;
        }

        // Fallback: legacy visual bars by localScale.x
        if (Score_1 == null || Score_2 == null)
        {
            Debug.LogError("[GameManagerScript] No score sources found. Assign ScoreSphereScript refs, or assign Score_1/Score_2 fallback bars.");
            return false;
        }

        // Convert legacy raw scale into score01 using winScale.
        float lightRaw = Score_1.transform.localScale.x;
        float darkRaw  = Score_2.transform.localScale.x;

        if (winScale <= 0.0001f)
        {
            Debug.LogError("[GameManagerScript] winScale must be > 0 to convert legacy bar scale to Score01.");
            return false;
        }

        light01 = Mathf.Clamp01(lightRaw / winScale);
        dark01  = Mathf.Clamp01(darkRaw  / winScale);
        return true;
    }

    private bool HasWinCondition01(float light01, float dark01)
    {
        // Win when either side reaches threshold (typically 1.0).
        if (light01 >= winScore01Threshold) return true;
        if (dark01  >= winScore01Threshold) return true;

        // Lose when either hits 0 (or custom lower threshold)
        // if (light01 <= loseScore01Threshold) return true;
        // if (dark01  <= loseScore01Threshold) return true;

        return false;
    }

    // --------------------------
    // Cabinet empty logic
    // --------------------------

    private void CheckForInactivePlayers()
    {
        bool allInactive = AreAllActivePlayersInactive(out int activePlayerCount);

        if (activePlayerCount <= 0)
        {
            // Probably setup error; don't auto-return.
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

        if (active <= 0) return false;
        return inactive == active;
    }

    private static void CountActiveInactive(PlayerControllerScript pc, ref int active, ref int inactive)
    {
        if (pc == null) return;

        // Ignore players disabled by roster (e.g., P2/P4 in 1v1)
        if (!pc.gameObject.activeInHierarchy) return;

        active++;
        if (!pc.isActive) inactive++;
    }

    private void AbortMatchToAttract()
    {
        if (_ending) return;
        _ending = true;

        StopGameMusic();

        // Reset session state for a clean attract flow
        GameFlowContext.EnsureExists();
        GameFlowContext.Instance.ClearLastMatchResult();
        GameFlowContext.Instance.ClearSelection();

        SceneFlow.GoToChooseMode();
    }

    // --------------------------
    // End match -> PostGame
    // --------------------------

    private void EndMatchToPostGame(float light01, float dark01)
    {
        if (_ending) return;
        _ending = true;

        // Store final raw values in the same space MatchResult expects:
        // raw = score01 * winScale, so raw/winScale == score01
        finalScore_1 = Mathf.Clamp01(light01) * winScale;
        finalScore_2 = Mathf.Clamp01(dark01)  * winScale;

        StopGameMusic();

        if (disableGameplayOnEnd && _gameplayObjects != null)
            _gameplayObjects.SetActive(false);

        if (engageDeathSphereOnEnd && _deathSphere != null)
        {
            var ds = _deathSphere.GetComponent<DSScript>();
            if (ds != null) ds.Engage();
        }

        var winner = DetermineWinner01(light01, dark01);
        var result = BuildMatchResult(winner);

        GameFlowContext.EnsureExists();
        GameFlowContext.Instance.SetLastMatchResult(result);

        StartCoroutine(LoadPostGameAfterDelay());
    }

    private void StopGameMusic()
    {
        if (_gmm == null) return;

        var mm = _gmm.GetComponent<MusicManagerScript>();
        if (mm != null) mm.StopMusic();
    }

    private IEnumerator LoadPostGameAfterDelay()
    {
        if (postGameLoadDelaySeconds > 0f)
            yield return new WaitForSecondsRealtime(postGameLoadDelaySeconds);

        SceneFlow.GoToPostGame();
    }

    private TeamSide DetermineWinner01(float light01, float dark01)
    {
        // Prefer deterministic threshold logic.
        bool lightWon = light01 >= winScore01Threshold && dark01 < winScore01Threshold;
        bool darkWon  = dark01  >= winScore01Threshold && light01 < winScore01Threshold;

        if (lightWon) return TeamSide.Light;
        if (darkWon)  return TeamSide.Dark;

        // If someone hit the lose threshold, the other wins.
        // if (light01 <= loseScore01Threshold && dark01 > loseScore01Threshold) return TeamSide.Dark;
        // if (dark01  <= loseScore01Threshold && light01 > loseScore01Threshold) return TeamSide.Light;

        // Otherwise compare (fallback)
        if (light01 > dark01) return TeamSide.Light;
        if (dark01 > light01) return TeamSide.Dark;

        return TeamSide.Tie;
    }

    private MatchResult BuildMatchResult(TeamSide winner)
    {
        var ctx = GameFlowContext.Instance;
        var def = ctx.SelectedLevel;

        return new MatchResult
        {
            winner = winner,
            lightRaw = finalScore_1,
            darkRaw  = finalScore_2,
            winScale = winScale,
            mode = ctx.Mode,

            stageNumber = def != null ? def.levelNumber : 0,
            stageTitle  = def != null ? def.levelTitle  : "UNKNOWN",
            gameplaySceneName = def != null ? def.SceneName : null
        };
    }
}
