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

    public bool IsAnomalyRunning => _currentMinigame != null;

    // Fired whenever any anomaly finishes, after rewards are applied.
    // Used by level-specific adapters (e.g. NovaAnomalyAdapter) to react.
    public event Action<AnomalyDefinition, AnomalyResult> OnAnomalyCompleted;

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
        float? forcedDuration = null)
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

        StartCoroutine(RunAnomalyRoutine(def));
    }

    IEnumerator RunAnomalyRoutine(AnomalyDefinition def)
    {
        // 1. Warning / telegraph
        if (ui != null)
            ui.ShowWarning(def);

        SpawnWorldTelegraph(def); // e.g. portal, grid flare, etc.

        float warningDelay = def.GetRandomStartDelay();
        yield return new WaitForSeconds(warningDelay);

        // 2. Activate
        if (ui != null)
            ui.ShowActiveBanner(def);

        ApplyArenaMode(def.arenaMode);

        // If a caller supplied a custom duration, use that; otherwise use the definition.
        float duration = _forcedDuration.HasValue ? _forcedDuration.Value : def.GetRandomDuration();
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

        _currentMinigame.Init(context);
        _currentMinigame.OnCompleted += HandleMinigameCompleted;

        if (ui != null)
            ui.ShowLowerThird(def); // instructions + controls + content root visible

        _currentMinigame.Begin();

        // Wait until the minigame signals completion.
        while (_currentMinigame != null && !_currentMinigame.IsFinished)
        {
            yield return null;
        }

        // RunAnomalyRoutine gives control back once HandleMinigameCompleted clears _currentMinigame.
    }

    void HandleMinigameCompleted(AnomalyResult result)
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
            _currentMinigame.OnCompleted -= HandleMinigameCompleted;
            Destroy(_currentMinigame.gameObject);
            _currentMinigame = null;
        }

        _currentParticipants.Clear();
        _currentDef = null;
        _forcedParticipants = null;
        _forcedDuration = null;
        DespawnWorldTelegraph();
    }

    void ApplyArenaMode(ArenaModeDuringAnomaly mode)
    {
        if (playerManager == null)
            return;

        switch (mode)
        {
            case ArenaModeDuringAnomaly.Unchanged:
                // Return arena to normal play.
                playerManager.SetGlobalPaused(false);
                // TODO: clear any ghost / subspace state here if you add it to PlayerManager.
                break;

            case ArenaModeDuringAnomaly.PauseAllPlayers:
                playerManager.SetGlobalPaused(true);
                break;

            case ArenaModeDuringAnomaly.GhostParticipantsOnly:
                // Leave game running but you can choose to mark participants
                // as "in subspace" / ghosted via your PlayerManager.
                // Example (once you implement it):
                // playerManager.SetGhostFor(_currentParticipants, true);
                break;
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
