using System.Collections.Generic;
using UnityEngine;

/// <summary>Final entry window to Core Collapse. All rostered players participate.</summary>
public class NovaAnomalyAdapter : MonoBehaviour
{
    public NovaStarController star;
    public AnomalyManager anomalyManager;
    public AnomalyDefinition novaCoreAnomalyDefinition;
    [Min(0f)] public float overrideDuration;
    public bool showWarningOnEntryOpen = true;

    private void OnEnable()
    {
        if (star)
        {
            star.OnEntryWindowOpened += HandleEntryOpened;
            star.OnEntryWindowClosed += HandleEntryClosed;
        }
        if (anomalyManager) anomalyManager.OnAnomalyCompleted += HandleAnomalyCompleted;
    }
    private void OnDisable()
    {
        if (star)
        {
            star.OnEntryWindowOpened -= HandleEntryOpened;
            star.OnEntryWindowClosed -= HandleEntryClosed;
        }
        if (anomalyManager) anomalyManager.OnAnomalyCompleted -= HandleAnomalyCompleted;
    }
    private void HandleEntryOpened()
    {
        if (showWarningOnEntryOpen && anomalyManager && anomalyManager.ui && novaCoreAnomalyDefinition)
            anomalyManager.ui.ShowWarning(novaCoreAnomalyDefinition, star.entryWindowDuration);
    }
    private void HandleEntryClosed(List<PlayerControllerScript> entrants)
    {
        if (!anomalyManager || !novaCoreAnomalyDefinition || !star)
        { Debug.LogError("NOVA finale is missing its anomaly configuration.", this); return; }
        if (anomalyManager.IsAnomalyRunning) anomalyManager.CancelCurrentAnomaly();
        anomalyManager.TriggerAnomaly(novaCoreAnomalyDefinition, star.GetRosteredPlayers(),
            overrideDuration > 0f ? overrideDuration : (float?)null, entrants);
    }
    private void HandleAnomalyCompleted(AnomalyDefinition definition, AnomalyResult result)
    {
        if (definition == novaCoreAnomalyDefinition && star) star.CompleteFinale();
    }
}
