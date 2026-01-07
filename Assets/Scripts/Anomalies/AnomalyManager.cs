using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime context passed into an AnomalyMinigameBase when it starts.
/// </summary>
public struct AnomalyContext
{
    public AnomalyDefinition definition;
    public AnomalyManager manager;

    public IReadOnlyList<PlayerControllerScript> allPlayers;
    public IReadOnlyList<PlayerControllerScript> participants;

    // Lower-third UI rect where the minigame can spawn content.
    public RectTransform uiArea;

    // Duration chosen for this run (seconds).
    public float duration;
}

/// <summary>
/// Result reported by an anomaly when it finishes.
/// </summary>
public struct AnomalyResult
{
    public bool success;

    // Optional: who "won" or drove the success.
    public PlayerControllerScript winningPlayer;
    public int winningTeamIndex;

    // Optional metric (alignment %, accuracy, etc.)
    public float score;

    // Optional arbitrary payload for rewards / post screens.
    // Example: NovaCoreMinigame can attach a NovaCoreWrapUpPayload here.
    public object payload;
}

/// <summary>
/// Base class for all anomaly minigame logic.
/// Concrete minigames derive from this and call Complete() when done.
/// </summary>
public abstract class AnomalyMinigameBase : MonoBehaviour
{
    protected AnomalyContext Context;

    public bool IsFinished { get; private set; }

    public event System.Action<AnomalyResult> OnCompleted;

    /// <summary>Called by the AnomalyManager immediately after instantiation.</summary>
    public virtual void Init(AnomalyContext context)
    {
        Context = context;
    }

    /// <summary>Called after Init; start any animations or timers here.</summary>
    public virtual void Begin() { }

    protected void Complete(AnomalyResult result)
    {
        if (IsFinished) return;

        IsFinished = true;
        OnCompleted?.Invoke(result);
    }
}

/// <summary>
/// Owns scheduling, spawning and cleanup of anomaly minigames for a stage.
/// One AnomalyManager should live in the stage scene.
/// </summary>
public class AnomalyManager : MonoBehaviour
{
    [Header("Config")]
    public StageProfile stageProfile;

    [Header("Arena Mode (Hard Disable)")]
    [SerializeField] private bool hardDisablePlayerObjectsWhilePaused = true;

    // Track what we disabled so we can restore exactly those objects.
    private readonly List<GameObject> _hardDisabledPlayerRoots = new();


    [Header("Scheduling")]
    [Tooltip("If true, anomalies are auto-scheduled using StageProfile. If false, anomalies only start when TriggerAnomaly() is called.")]
    public bool autoSchedule = true;

    [Header("References")]
    public AnomalyUIController ui;
    public PlayerManager playerManager;      // your existing player registry

    AnomalyDefinition _currentDef;
    AnomalyMinigameBase _currentMinigame;
    readonly List<PlayerControllerScript> _currentParticipants = new List<PlayerControllerScript>();

    Coroutine _scheduleRoutine;

    // Optional overrides for externally-triggered anomalies:
    IReadOnlyList<PlayerControllerScript> _forcedParticipants;
    float? _forcedDuration;
	IReadOnlyList<PlayerControllerScript> _forcedOnTimeParticipants;

    public bool IsAnomalyRunning => _currentMinigame != null;

    // Fired whenever any anomaly finishes, after rewards are applied.
    // Used by level-specific adapters (e.g. NovaAnomalyAdapter) to react.
    public event Action<AnomalyDefinition, AnomalyResult> OnAnomalyCompleted;

    private void HardDisableActivePlayers()
{
    _hardDisabledPlayerRoots.Clear();

    if (playerManager == null) return;

    var players = playerManager.ActivePlayers;
    if (players == null) return;

    foreach (var pcs in players)
    {
        if (pcs == null) continue;

        var go = pcs.gameObject;
        if (go != null && go.activeSelf)
        {
            _hardDisabledPlayerRoots.Add(go);
            go.SetActive(false);
        }
    }
}

private void RestoreHardDisabledPlayers()
{
    for (int i = 0; i < _hardDisabledPlayerRoots.Count; i++)
    {
        var go = _hardDisabledPlayerRoots[i];
        if (go != null)
            go.SetActive(true);
    }

    _hardDisabledPlayerRoots.Clear();
}


    void Start()
    {
        if (stageProfile == null)
        {
            Debug.LogWarning($"{nameof(AnomalyManager)} on {name} has no StageProfile assigned; anomalies will not auto-run.");
            // We still allow TriggerAnomaly() to be used even if there is no StageProfile.
        }

        if (autoSchedule && stageProfile != null && _scheduleRoutine == null)
        {
            _scheduleRoutine = StartCoroutine(ScheduleLoop());
        }
        
    }

    void OnDestroy()
    {
        if (_scheduleRoutine != null)
        {
            StopCoroutine(_scheduleRoutine);
            _scheduleRoutine = null;
        }
    }

    IEnumerator ScheduleLoop()
    {
        while (true)
        {
            // Wait a random amount of time between anomalies.
            float wait = UnityEngine.Random.Range(stageProfile.globalMinInterval,
                                                  stageProfile.globalMaxInterval);
            yield return new WaitForSeconds(wait);

            TryStartRandomAnomaly();
        }
    }

    void TryStartRandomAnomaly()
    {
        if (IsAnomalyRunning) return;
        if (stageProfile == null || stageProfile.anomalyPool == null || stageProfile.anomalyPool.Count == 0)
            return;

        var nextDef = PickWeightedRandom(stageProfile.anomalyPool);
        if (nextDef == null) return;

        _currentDef = nextDef;
        StartCoroutine(RunAnomalyRoutine(_currentDef));
    }

    /// <summary>
    /// Public entry point for level-specific systems (e.g. NovaStarController)
    /// to trigger a specific anomaly manually.
    /// - def: which AnomalyDefinition to run.
    /// - forcedParticipants: optional explicit participants instead of using
    ///   def.participantSelection.
    /// - forcedDuration: optional duration override instead of def.GetRandomDuration().
    /// </summary>
    public void TriggerAnomaly(
        AnomalyDefinition def,
        IReadOnlyList<PlayerControllerScript> forcedParticipants = null,
		float? forcedDuration = null,
		IReadOnlyList<PlayerControllerScript> onTimeParticipants = null)
    {
        if (IsAnomalyRunning)
        {
            Debug.LogWarning($"[AnomalyManager] Tried to trigger anomaly '{def?.name}' but another anomaly is already running.");
            return;
        }

        if (def == null)
        {
            Debug.LogWarning("[AnomalyManager] TriggerAnomaly called with null definition.");
            return;
        }

        _currentDef = def;
        _forcedParticipants = forcedParticipants;
        _forcedDuration = forcedDuration;
		_forcedOnTimeParticipants = onTimeParticipants;

        StartCoroutine(RunAnomalyRoutine(def));
    }

    IEnumerator RunAnomalyRoutine(AnomalyDefinition def)
    {
        // 1. Warning / telegraph
        float warningDelay = def.GetRandomStartDelay();
        if (ui != null)
            ui.ShowWarning(def, warningDelay);

        SpawnWorldTelegraph(def);
        yield return new WaitForSeconds(warningDelay);

        // 2. Activate
        // Duration for the minigame
        float duration = _forcedDuration.HasValue ? _forcedDuration.Value : def.GetRandomDuration();

		// NOTE: we intentionally start the Active banner/countdown *after* the intro transition,
		// so we do not call ui.ShowActiveBanner(def, duration) here.
		ApplyArenaMode(def.arenaMode);

        var context = BuildContextFor(def, duration, _forcedParticipants);

        if (def.minigameLogicPrefab == null)
        {
            Debug.LogWarning($"AnomalyDefinition '{def.name}' has no minigameLogicPrefab assigned.");
            HandleMinigameCompleted(new AnomalyResult { success = false });
            yield break;
        }

        // spawn / wire minigame logic under the lower-third content root
        if (ui == null || ui.minigameContentRoot == null)
        {
            Debug.LogWarning("AnomalyUIController or its minigameContentRoot is not assigned.");
            HandleMinigameCompleted(new AnomalyResult { success = false });
            yield break;
        }

        GameObject go = Instantiate(def.minigameLogicPrefab, ui.minigameContentRoot);
        _currentMinigame = go.GetComponent<AnomalyMinigameBase>();

        

        if (_currentMinigame == null)
        {
            Debug.LogError($"Minigame prefab '{def.minigameLogicPrefab.name}' does not have an AnomalyMinigameBase component.");
            Destroy(go);
            HandleMinigameCompleted(new AnomalyResult { success = false });
            yield break;
        }

		// Optional hand-off: some minigames (like NOVA CORE COLLAPSE) need to know who was
		// "on-time" before the anomaly transition started.
		if (_forcedOnTimeParticipants != null && _currentMinigame is IOnTimeParticipantsReceiver onTimeReceiver)
		{
			onTimeReceiver.SetOnTimeParticipants(_forcedOnTimeParticipants);
		}

		_currentMinigame.Init(context);
        

        
        // Begin minigame lifecycle so it can exist, but keep gameplay disabled
        _currentMinigame.OnCompleted += HandleMinigameCompleted;
        _currentMinigame.Begin();

                // find transition on the minigame root
        var transition = go.GetComponent<NovaMinigameTransition>();

        
if (transition != null && ui != null)
{
var seq =
    ui.GetComponent<AnomalyUISequencer>() ??
    ui.GetComponentInParent<AnomalyUISequencer>() ??
    FindFirstObjectByType<AnomalyUISequencer>(FindObjectsInactive.Include);

transition.BindUISequencer(seq);
}
        if (ui != null)
            ui.ShowLowerThird(def);

        if (transition != null)
        {
            // Intro animation runs here
            yield return StartCoroutine(transition.PlayIntro());
        }

        // NOW start the in-minigame countdown (gameplay time only)
        if (ui != null)
            ui.ShowActiveBanner(def, duration);


            





        // Wait until the minigame signals completion.
        while (_currentMinigame != null && !_currentMinigame.IsFinished)
        {
            yield return null;
        }

        // RunAnomalyRoutine gives control back once HandleMinigameCompleted clears _currentMinigame.
    }

    void HandleMinigameCompleted(AnomalyResult result)
    {

        // If the minigame has a transition component, play outro before finalizing.
        var transition = _currentMinigame != null ? _currentMinigame.GetComponent<NovaMinigameTransition>() : null;
        if (transition != null)
        {
            StartCoroutine(FinalizeAfterOutro(transition, result));
            return;
        }

        // Prevent double handling if Complete() is somehow called twice.
        if (!IsAnomalyRunning && _currentDef == null)
            return;

        ApplyArenaMode(ArenaModeDuringAnomaly.Unchanged);

        if (ui != null)
        {
            ui.HideActiveBanner();
            ui.HideLowerThird();
            ui.ShowResult(_currentDef, result);
        }

        ApplyRewards(result);

        // Fire completion event so NovaAnomalyAdapter (and any other
        // systems) can react.
        OnAnomalyCompleted?.Invoke(_currentDef, result);

        if (_currentMinigame != null)
        {
            _currentMinigame.OnCompleted -= HandleMinigameCompleted;
            Destroy(_currentMinigame.gameObject);
            _currentMinigame = null;
        }

        _currentParticipants.Clear();
        _currentDef = null;
        _forcedParticipants = null;
        _forcedDuration = null;
		_forcedOnTimeParticipants = null;
        DespawnWorldTelegraph();
    }

        void HandleMinigameCompleted_NoOutro(AnomalyResult result)
    {
        // Prevent double handling if Complete() is somehow called twice.
        if (!IsAnomalyRunning && _currentDef == null)
            return;

        ApplyArenaMode(ArenaModeDuringAnomaly.Unchanged);

        if (ui != null)
        {
            ui.HideActiveBanner();
            ui.HideLowerThird();
            ui.ShowResult(_currentDef, result);
        }

        ApplyRewards(result);

        // Fire completion event so NovaAnomalyAdapter (and any other
        // systems) can react.
        OnAnomalyCompleted?.Invoke(_currentDef, result);

        if (_currentMinigame != null)
        {
            _currentMinigame.OnCompleted -= HandleMinigameCompleted_NoOutro;
            _currentMinigame.OnCompleted -= HandleMinigameCompleted;

            Destroy(_currentMinigame.gameObject);
            _currentMinigame = null;
        }

        _currentParticipants.Clear();
        _currentDef = null;
		_forcedParticipants = null;
		_forcedDuration = null;
		_forcedOnTimeParticipants = null;
        DespawnWorldTelegraph();
    }

    IEnumerator FinalizeAfterOutro(NovaMinigameTransition transition, AnomalyResult result)
{
    yield return StartCoroutine(transition.PlayOutro());
    // proceed with your existing cleanup path
    HandleMinigameCompleted_NoOutro(result);
}


void ApplyArenaMode(ArenaModeDuringAnomaly mode)
{
    if (playerManager == null) return;

    switch (mode)
    {
        case ArenaModeDuringAnomaly.Unchanged:
            // Restore objects first, then unpause
            if (hardDisablePlayerObjectsWhilePaused)
                RestoreHardDisabledPlayers();

            playerManager.SetGlobalPaused(false);
            break;

        case ArenaModeDuringAnomaly.PauseAllPlayers:
            // Pause first, then optionally hard-disable
            playerManager.SetGlobalPaused(true);

            if (hardDisablePlayerObjectsWhilePaused)
                HardDisableActivePlayers();
            break;

        // keep your other cases as-is...
    }
}


    void ApplyRewards(AnomalyResult result)
    {
        if (_currentDef == null)
            return;

        var rewards = result.success
            ? _currentDef.rewardsOnSuccess
            : _currentDef.rewardsOnFail;

        if (rewards == null) return;

        foreach (var r in rewards)
        {
            if (r != null)
                r.Apply(result, this);
        }
    }

    AnomalyContext BuildContextFor(
        AnomalyDefinition def,
        float duration,
        IReadOnlyList<PlayerControllerScript> forcedParticipants = null)
    {
        var allPlayers = playerManager != null
            ? playerManager.ActivePlayers
            : new List<PlayerControllerScript>();

        _currentParticipants.Clear();

        if (forcedParticipants != null && forcedParticipants.Count > 0)
        {
            // Use the explicit list provided by the caller (e.g. NovaStarController).
            _currentParticipants.AddRange(forcedParticipants);
        }
        else
        {
            // Fall back to the definition's participantSelection rules.
            _currentParticipants.AddRange(SelectParticipants(def, allPlayers));
        }

        return new AnomalyContext
        {
            definition = def,
            manager = this,
            allPlayers = allPlayers,
            participants = _currentParticipants,
            uiArea = ui != null ? ui.minigameContentRoot : null,
            duration = duration
        };
    }

    List<PlayerControllerScript> SelectParticipants(AnomalyDefinition def, IReadOnlyList<PlayerControllerScript> allPlayers)
    {
        var result = new List<PlayerControllerScript>();

        if (allPlayers == null || allPlayers.Count == 0)
            return result;

        switch (def.participantSelection)
        {
            case ParticipantSelection.AllPlayers:
                for (int i = 0; i < allPlayers.Count; i++)
                {
                    result.Add(allPlayers[i]);
                }
                break;

            case ParticipantSelection.RandomSingle:
                int idx = UnityEngine.Random.Range(0, allPlayers.Count);
                result.Add(allPlayers[idx]);
                break;

            default:
                Debug.LogWarning($"ParticipantSelection mode '{def.participantSelection}' not fully implemented; falling back to AllPlayers.");
                for (int i = 0; i < allPlayers.Count; i++)
                {
                    result.Add(allPlayers[i]);
                }
                break;
        }

        return result;
    }

    AnomalyDefinition PickWeightedRandom(IList<AnomalyDefinition> pool)
    {
        if (pool == null || pool.Count == 0)
            return null;

        float totalWeight = 0f;
        foreach (var a in pool)
        {
            if (a != null && a.selectionWeight > 0f)
                totalWeight += a.selectionWeight;
        }

        if (totalWeight <= 0f)
            return null;

        float r = UnityEngine.Random.value * totalWeight;
        float running = 0f;

        foreach (var a in pool)
        {
            if (a == null || a.selectionWeight <= 0f) continue;

            running += a.selectionWeight;
            if (r <= running)
                return a;
        }

        // Fallback: return last valid one.
        for (int i = pool.Count - 1; i >= 0; i--)
        {
            if (pool[i] != null)
                return pool[i];
        }

        return null;
    }

    void SpawnWorldTelegraph(AnomalyDefinition def)
    {
        // TODO: hook this into your grid / hazard telegraph visuals.
        // For now this is a no-op placeholder.
    }

    void DespawnWorldTelegraph()
    {
        // TODO: clean up any telegraph visuals spawned in SpawnWorldTelegraph.
    }
}
