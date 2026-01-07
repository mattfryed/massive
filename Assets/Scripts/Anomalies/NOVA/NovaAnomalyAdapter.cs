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

// private void HandleEntryClosed(List<PlayerControllerScript> entrants)
// {
//     if (anomalyManager == null || novaCoreAnomalyDefinition == null)
//         return;

//     var ui = anomalyManager.ui;

//     // No entrants -> no minigame
//     if (entrants == null || entrants.Count == 0)
//     {
//         if (hideWarningIfNoEntrants && ui != null)
//             ui.HideTop();

//         return;
//     }

// // Entrants exist: trigger the anomaly
// float? duration = null;
// if (overrideDuration > 0f)
//     duration = overrideDuration;

// // All players should be loaded into the minigame
// var allPlayers = (anomalyManager.playerManager != null)
//     ? anomalyManager.playerManager.ActivePlayers
//     : entrants;

// // Entrants = the "on-time" list used for late penalties
// anomalyManager.TriggerAnomaly(
//     novaCoreAnomalyDefinition,
//     forcedParticipants: allPlayers,
//     forcedDuration: duration,
//     onTimeParticipants: entrants
// );

// }

// private void HandleEntryClosed(List<PlayerControllerScript> entrants)
// {
//     if (anomalyManager == null || novaCoreAnomalyDefinition == null) return;

//     if (entrants == null || entrants.Count == 0)
//     {
//         if (hideWarningIfNoEntrants && anomalyManager.ui != null)
//             anomalyManager.ui.HideTop();
//         return;
//     }

//     if (setActiveStateOnEntryClose && anomalyManager.ui != null)
//         anomalyManager.ui.SetTopState(novaCoreAnomalyDefinition, AnomalyTopState.Active, 0f);

//     if (anomalyManager.IsAnomalyRunning) return;

//     float? duration = overrideDuration > 0f ? overrideDuration : null;

//     IReadOnlyList<PlayerControllerScript> allPlayers = null;
//     if (anomalyManager.playerManager != null)
//         allPlayers = anomalyManager.playerManager.ActivePlayers;

//     // Fallback safety (if playerManager isn't assigned or returns empty)
//     if (allPlayers == null || allPlayers.Count == 0)
//     {
// #if UNITY_6000_0_OR_NEWER
//         allPlayers = FindObjectsByType<PlayerControllerScript>(FindObjectsSortMode.None);
// #else
//         allPlayers = FindObjectsOfType<PlayerControllerScript>();
// #endif
//     }

//     // Force everyone into the minigame, but ONLY entrants are "on-time"
//     anomalyManager.TriggerAnomaly(novaCoreAnomalyDefinition, allPlayers, duration, entrants);
// }

private void HandleEntryClosed(List<PlayerControllerScript> entrants)
{
    if (anomalyManager == null || novaCoreAnomalyDefinition == null)
        return;

    var ui = anomalyManager.ui;

    // No entrants -> no anomaly
    if (entrants == null || entrants.Count == 0)
    {
        if (hideWarningIfNoEntrants && ui != null)
            ui.HideTop(); // <-- real method
        return;
    }

    // Entrants exist: optionally flip banner to ACTIVE immediately
    if (setActiveStateOnEntryClose && ui != null)
        ui.SetTopState(novaCoreAnomalyDefinition, AnomalyTopState.Active, 0f);

    // Don't stomp if something else is already running
    if (anomalyManager.IsAnomalyRunning)
        return;

    // Duration override
    float? duration = (overrideDuration > 0f) ? overrideDuration : (float?)null;

    // IMPORTANT: load *all rostered players* into the minigame (fixes "only entrants participate")
    var rosteredPlayers = GetRosteredWorldPlayers();
    if (rosteredPlayers == null || rosteredPlayers.Count == 0)
        rosteredPlayers = new List<PlayerControllerScript>(entrants);

    // On-time list must be a subset of rostered players (defensive)
    var rosteredSet = new HashSet<PlayerControllerScript>(rosteredPlayers);
    var onTime = new List<PlayerControllerScript>(entrants.Count);
    foreach (var p in entrants)
    {
        if (p != null && rosteredSet.Contains(p))
            onTime.Add(p);
    }

    // This will feed your late-join system (AnomalyManager pushes forced on-time list into the minigame)
    anomalyManager.TriggerAnomaly(
        novaCoreAnomalyDefinition,
        forcedParticipants: rosteredPlayers,
        forcedDuration: duration,
        onTimeParticipants: onTime
    );
}



private void HandleAnomalyCompleted(AnomalyDefinition def, AnomalyResult result)
{
    if (def != novaCoreAnomalyDefinition) return;

    // Let NovaCoreMinigame restore its own cached world players.
    if (star != null)
        star.TriggerBounceAfterMinigame();
}


private void RestoreWorldPlayers()
{
#if UNITY_6000_0_OR_NEWER
    var players = FindObjectsByType<PlayerControllerScript>(FindObjectsSortMode.None);
#else
    var players = FindObjectsOfType<PlayerControllerScript>();
#endif

    foreach (var p in players)
    {
        if (p == null) continue;
        p.SetWorldGameplaySuppressed(false);
    }
}

private static bool IsRosteredWorldPlayer(PlayerControllerScript p)
{
    if (p == null) return false;
    if (!p.isActiveAndEnabled) return false; // activeInHierarchy + enabled

    // If you're using GameplayRigRoot to "turn off" unused players in 1v1,
    // this prevents those players from being included.
    var rig = p.transform.Find("GameplayRigRoot");
    if (rig != null && !rig.gameObject.activeInHierarchy) return false;

    return true;
}

private List<PlayerControllerScript> GetRosteredWorldPlayers()
{
    var result = new List<PlayerControllerScript>();
    var seen = new HashSet<PlayerControllerScript>();

    var fromManager = anomalyManager?.playerManager?.ActivePlayers;
    if (fromManager != null)
    {
        foreach (var p in fromManager)
        {
            if (!IsRosteredWorldPlayer(p)) continue;
            if (seen.Add(p)) result.Add(p);
        }
    }

    // Fallback: scene query (active objects only)
    if (result.Count == 0)
    {
        foreach (var p in FindObjectsByType<PlayerControllerScript>(FindObjectsSortMode.None))
        {
            if (!IsRosteredWorldPlayer(p)) continue;
            if (seen.Add(p)) result.Add(p);
        }
    }

    // Deterministic order (helps debugging & UI consistency)
    result.Sort((a, b) => a.playerID.CompareTo(b.playerID));
    return result;
}


}
