using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bridges NovaStarController → AnomalyManager for the NOVA core minigame.
/// </summary>
public class NovaAnomalyAdapter : MonoBehaviour
{
    public NovaStarController star;
    public AnomalyManager anomalyManager;
    public AnomalyDefinition novaCoreAnomalyDefinition;

    [Tooltip("Override minigame duration for NOVA core (seconds). If <= 0, use definition's random duration.")]
    public float overrideDuration = 0f;

    private void OnEnable()
    {
        if (star != null)
            star.OnEntryWindowClosed += HandleEntryWindowClosed;

        if (anomalyManager != null)
            anomalyManager.OnAnomalyCompleted += HandleAnomalyCompleted;
    }

    private void OnDisable()
    {
        if (star != null)
            star.OnEntryWindowClosed -= HandleEntryWindowClosed;

        if (anomalyManager != null)
            anomalyManager.OnAnomalyCompleted -= HandleAnomalyCompleted;
    }

    /// <summary>
    /// Called when the NOVA star's entry window closes for a bounce cycle.
    /// </summary>
    private void HandleEntryWindowClosed(List<PlayerControllerScript> entrants)
    {
        if (anomalyManager == null || novaCoreAnomalyDefinition == null)
            return;

        // No entrants: no anomaly; star will do an immediate bounce on its own.
        if (entrants == null || entrants.Count == 0)
            return;

        // Don't start a new anomaly if one is already running.
        if (anomalyManager.IsAnomalyRunning)
            return;

        float? duration = null;
        if (overrideDuration > 0f)
            duration = overrideDuration;

        // Trigger the NOVA core anomaly for the specific entrants.
        anomalyManager.TriggerAnomaly(novaCoreAnomalyDefinition, entrants, duration);
    }

    /// <summary>
    /// Called whenever any anomaly completes; we only care about the NOVA core one.
    /// </summary>
    private void HandleAnomalyCompleted(AnomalyDefinition def, AnomalyResult result)
    {
        if (def != novaCoreAnomalyDefinition)
            return;

        if (star != null)
        {
            // Now that the NOVA core minigame is done (and rewards/shields applied),
            // tell the star to perform the bounce.
            star.TriggerBounceAfterMinigame();
        }
    }
}
