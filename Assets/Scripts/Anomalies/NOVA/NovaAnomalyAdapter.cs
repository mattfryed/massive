using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bridges NovaStarController entry window events → Anomaly UI telegraph + AnomalyManager trigger.
/// - On entry window open: shows warning banner countdown (duration = star.entryWindowDuration)
/// - On entry window close:
///    - if no entrants: hides banner
///    - if entrants: triggers the NOVA anomaly for those entrants
/// - On anomaly complete: tells star to bounce after minigame.
/// </summary>
public class NovaAnomalyAdapter : MonoBehaviour
{
    [Header("Refs")]
    public NovaStarController star;
    public AnomalyManager anomalyManager;
    public AnomalyDefinition novaCoreAnomalyDefinition;

    [Header("Duration")]
    [Tooltip("Override minigame duration (seconds). If <= 0, use definition duration.")]
    public float overrideDuration = 0f;

    [Header("UI Telegraph")]
    [Tooltip("If true, show the ANOMALY DETECTED banner when the entry window opens.")]
    public bool showWarningOnEntryOpen = true;

    [Tooltip("If true, hide the top banner when entry closes with no entrants.")]
    public bool hideWarningIfNoEntrants = true;

    [Tooltip("If true, switch top banner to ACTIVE state (without countdown) when entrants exist.")]
    public bool setActiveStateOnEntryClose = true;

    private void OnEnable()
    {
        if (star != null)
        {
            star.OnEntryWindowOpened += HandleEntryOpened;
            star.OnEntryWindowClosed += HandleEntryClosed;
        }

        if (anomalyManager != null)
        {
            anomalyManager.OnAnomalyCompleted += HandleAnomalyCompleted;
        }
    }

    private void OnDisable()
    {
        if (star != null)
        {
            star.OnEntryWindowOpened -= HandleEntryOpened;
            star.OnEntryWindowClosed -= HandleEntryClosed;
        }

        if (anomalyManager != null)
        {
            anomalyManager.OnAnomalyCompleted -= HandleAnomalyCompleted;
        }
    }

    private void HandleEntryOpened()
    {
        if (!showWarningOnEntryOpen) return;
        if (anomalyManager == null || novaCoreAnomalyDefinition == null) return;

        // Don’t stomp UI if an anomaly is already running.
        if (anomalyManager.IsAnomalyRunning) return;

        var ui = anomalyManager.ui;
        if (ui == null) return;

        // Countdown should match entry window duration (countdown to intro start).
        ui.ShowWarning(novaCoreAnomalyDefinition, star != null ? star.entryWindowDuration : 0f);
    }

    private void HandleEntryClosed(List<PlayerControllerScript> entrants)
    {
        if (anomalyManager == null || novaCoreAnomalyDefinition == null)
            return;

        var ui = anomalyManager.ui;

        // No entrants: hide the warning banner and do nothing else.
        if (entrants == null || entrants.Count == 0)
        {
            if (hideWarningIfNoEntrants && ui != null)
                ui.HideTop();

            return;
        }

        // Entrants exist: optionally flip to ACTIVE (with no countdown yet).
        if (setActiveStateOnEntryClose && ui != null)
        {
            // Countdown for gameplay should start after intro finishes (AnomalyManager will do that).
            ui.SetTopState(novaCoreAnomalyDefinition, AnomalyTopState.Active, 0f);
        }

        if (anomalyManager.IsAnomalyRunning)
            return;

        float? duration = null;
        if (overrideDuration > 0f)
            duration = overrideDuration;

        // Trigger the anomaly with forced participants
        anomalyManager.TriggerAnomaly(novaCoreAnomalyDefinition, entrants, duration);
    }

    private void HandleAnomalyCompleted(AnomalyDefinition def, AnomalyResult result)
    {
        if (def != novaCoreAnomalyDefinition)
            return;

        if (star != null)
            star.TriggerBounceAfterMinigame();
    }
}
