using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central registry for all PlayerControllerScript instances in the scene,
/// plus simple global pause/ghost helpers for systems like AnomalyManager.
/// Attach this to your "Players" root object or another scene-level GameObject.
/// </summary>
public class PlayerManager : MonoBehaviour
{
    [Tooltip("If left empty, this will auto-populate using PlayerControllerScript components in children on Awake.")]
    [SerializeField] private List<PlayerControllerScript> players = new List<PlayerControllerScript>();

    /// <summary>
    /// The list of players known to this manager. AnomalyManager uses this.
    /// </summary>
    public IReadOnlyList<PlayerControllerScript> ActivePlayers => players;

    private bool _isGloballyPaused;
    private readonly HashSet<PlayerControllerScript> _ghostedPlayers = new HashSet<PlayerControllerScript>();

    private void Awake()
    {
        if (players.Count == 0)
        {
            RefreshPlayersFromChildren();
        }
    }

    /// <summary>
    /// (Optional utility) Re-scan children for PlayerControllerScript components.
    /// Handy if you add/remove players dynamically.
    /// </summary>
    public void RefreshPlayersFromChildren()
    {
        players.Clear();
        GetComponentsInChildren(true, players); // include inactive
    }

    /// <summary>
    /// Enables/disables PlayerControllerScript on all known players and
    /// zeroes their Rigidbody velocity when paused.
    /// This is what AnomalyManager calls for ArenaModeDuringAnomaly.PauseAllPlayers.
    /// </summary>
    public void SetGlobalPaused(bool paused)
    {
        if (_isGloballyPaused == paused)
            return;

        _isGloballyPaused = paused;

        foreach (var p in players)
        {
            if (p == null) continue;

            p.enabled = !paused;

            var rb = p.GetComponent<Rigidbody>();
            if (rb != null && paused)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }

    /// <summary>
    /// Marks a subset of players as "ghosted" for anomaly subspace modes.
    /// For now this only tracks state; other systems can query IsGhost().
    /// </summary>
    public void SetGhostFor(IReadOnlyList<PlayerControllerScript> subset, bool ghosted)
    {
        if (subset == null) return;

        foreach (var p in subset)
        {
            if (p == null) continue;

            if (ghosted)
                _ghostedPlayers.Add(p);
            else
                _ghostedPlayers.Remove(p);
        }
    }

    public bool IsGhost(PlayerControllerScript player)
    {
        return player != null && _ghostedPlayers.Contains(player);
    }
}
