using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerRosterController : MonoBehaviour
{
    [Header("Scene Player Objects")]
    [SerializeField] private GameObject player1;
    [SerializeField] private GameObject player2;
    [SerializeField] private GameObject player3;
    [SerializeField] private GameObject player4;

    [Tooltip("If true, 1v1 uses Player 1 vs Player 3 (disables 2 & 4).")]
    [SerializeField] private bool oneVOneUsesPlayers1And3 = true;

    [Header("Safety")]
    [SerializeField] private bool autoAssignByPlayerIdIfMissing = true;

    private readonly HashSet<PlayerControllerScript> _pendingMatchSpawns = new HashSet<PlayerControllerScript>();
    private bool _rosterReady;
    private readonly HashSet<int> _ambiguousAutoAssignSlots = new HashSet<int>();

    public event Action RosterReady;
    public event Action<string> StartupFailed;
    public string StartupFailureReason { get; private set; }
    public bool HasStartupFailed => !string.IsNullOrEmpty(StartupFailureReason);

    public GameObject P1 => player1;
    public GameObject P2 => player2;
    public GameObject P3 => player3;
    public GameObject P4 => player4;
    public bool IsRosterReady => _rosterReady;
    public bool IsRostered(GameObject player)
    {
        if (!player) return false;
        if (GameFlowContext.Instance && GameFlowContext.Instance.IsTwoVTwo)
            return player == player1 || player == player2 || player == player3 || player == player4;
        return player == player1 || player == (oneVOneUsesPlayers1And3 ? player3 : player2);
    }

    private void Awake()
    {
        GameFlowContext.EnsureExists();

        if (autoAssignByPlayerIdIfMissing)
            AutoAssignIfMissing();

        bool is2v2 = GameFlowContext.Instance.IsTwoVTwo;
        if (!ValidateForMatch(requireActive: false)) return;
        ApplyRoster(is2v2);

        Debug.Log(
            $"[Roster] Mode={GameFlowContext.Instance.Mode} | Active: " +
            $"P1={IsActive(player1)} P2={IsActive(player2)} P3={IsActive(player3)} P4={IsActive(player4)}",
            this);
    }

    private void Start()
    {
        if (!HasStartupFailed) StartCoroutine(BeginMatchSpawnAfterOneFrame());
    }

    private void OnDestroy()
    {
        UnsubscribeFromPendingPlayers();
    }

    private IEnumerator BeginMatchSpawnAfterOneFrame()
    {
        yield return null;

        if (!ValidateForMatch()) yield break;

        _rosterReady = false;
        _pendingMatchSpawns.Clear();

        CollectPendingPlayer(player1);
        CollectPendingPlayer(player2);
        CollectPendingPlayer(player3);
        CollectPendingPlayer(player4);

        if (_pendingMatchSpawns.Count == 0)
        {
            RejectStartup("No required players were available to spawn.");
            yield break;
        }

        PlayerControllerScript[] players = new PlayerControllerScript[_pendingMatchSpawns.Count];
        _pendingMatchSpawns.CopyTo(players);

        for (int i = 0; i < players.Length; i++)
            players[i].MatchSpawnCompleted += OnPlayerMatchSpawnCompleted;

        for (int i = 0; i < players.Length; i++)
            players[i].BeginMatchStartSpawn();

        while (_pendingMatchSpawns.Count > 0 && !HasStartupFailed)
        {
            yield return null;
            if (!ValidateForMatch()) yield break;
        }
    }

    private void CollectPendingPlayer(GameObject playerObject)
    {
        if (playerObject == null || !playerObject.activeInHierarchy)
            return;

        PlayerControllerScript player = playerObject.GetComponent<PlayerControllerScript>();
        if (player != null)
            _pendingMatchSpawns.Add(player);
    }

    private void OnPlayerMatchSpawnCompleted(PlayerControllerScript player)
    {
        if (HasStartupFailed || !_pendingMatchSpawns.Contains(player)) return;
        if (player != null)
            player.MatchSpawnCompleted -= OnPlayerMatchSpawnCompleted;

        _pendingMatchSpawns.Remove(player);

        if (_pendingMatchSpawns.Count == 0)
            MarkRosterReady();
    }

    private void MarkRosterReady()
    {
        if (_rosterReady || !ValidateForMatch()) return;
        _rosterReady = true;
        RosterReady?.Invoke();
    }

    private void UnsubscribeFromPendingPlayers()
    {
        foreach (PlayerControllerScript player in _pendingMatchSpawns)
        {
            if (player != null)
                player.MatchSpawnCompleted -= OnPlayerMatchSpawnCompleted;
        }

        _pendingMatchSpawns.Clear();
    }

    private void AutoAssignIfMissing()
    {
        if (player1 && player2 && player3 && player4) return;

        PlayerControllerScript[] players = FindObjectsByType<PlayerControllerScript>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        var candidates = new GameObject[4];
        var counts = new int[4];
        _ambiguousAutoAssignSlots.Clear();
        foreach (PlayerControllerScript player in players)
        {
            if (!player || !player.ParticipatesInMatch || player.gameObject.scene != gameObject.scene) continue;
            if (player.playerID < 0 || player.playerID > 3) continue;
            candidates[player.playerID] = player.gameObject;
            counts[player.playerID]++;
        }

        for (int slot = 0; slot < 4; slot++)
        {
            if (counts[slot] > 1) _ambiguousAutoAssignSlots.Add(slot);
            if (counts[slot] != 1) continue;
            // Unity's missing serialized objects are not CLR-null. Null-coalescing
            // keeps those stale wrappers instead of rebinding the migrated actor.
            switch (slot)
            {
                case 0: if (!player1) player1 = candidates[slot]; break;
                case 1: if (!player2) player2 = candidates[slot]; break;
                case 2: if (!player3) player3 = candidates[slot]; break;
                case 3: if (!player4) player4 = candidates[slot]; break;
            }
        }
    }

    /// <summary>Validate only slots required by the selected match mode.</summary>
    public bool TryValidateRequiredBindings(out string issues, bool requireActive = true)
    {
        var errors = new List<string>();
        if (requireActive && !isActiveAndEnabled) errors.Add("PlayerRosterController is disabled or its parent hierarchy is inactive.");
        var seen = new HashSet<GameObject>();
        var slots = new[] { player1, player2, player3, player4 };
        bool twoVTwo = GameFlowContext.Instance && GameFlowContext.Instance.IsTwoVTwo;
        for (int slot = 0; slot < slots.Length; slot++)
        {
            if (!twoVTwo && slot != 0 && slot != (oneVOneUsesPlayers1And3 ? 2 : 1)) continue;
            string label = $"P{slot + 1} (player{slot + 1})";
            GameObject go = slots[slot];
            if (!go)
            {
                errors.Add(label + ": missing GameObject binding" + (_ambiguousAutoAssignSlots.Contains(slot) ? "; auto-assignment is ambiguous, assign the intended scene player explicitly." : "."));
                continue;
            }
            if (!seen.Add(go)) errors.Add(label + ": duplicates another required player binding.");
            if (go.scene != gameObject.scene) errors.Add(label + ": player belongs to a different scene.");
            if (requireActive && !go.activeInHierarchy) errors.Add(label + ": player is inactive (check its parent hierarchy).");
            var player = go.GetComponent<PlayerControllerScript>();
            if (!player) { errors.Add(label + ": missing PlayerControllerScript."); continue; }
            if (!player.enabled) errors.Add(label + ": PlayerControllerScript is disabled.");
            if (player.playerID != slot) errors.Add(label + $": playerID is {player.playerID}; expected {slot}.");
            if (!player.ParticipatesInMatch) errors.Add(label + ": configured as a demonstration/non-match actor.");
            if (!player.MatchSpawnAnchor) errors.Add(label + ": missing respawnPointOverride (roster spawn anchor).");
            else if (player.MatchSpawnAnchor.gameObject.scene != gameObject.scene) errors.Add(label + ": respawnPointOverride belongs to a different scene.");
            if (!player.goalZone) errors.Add(label + ": missing goalZone binding.");
            else if (player.goalZone.scene != gameObject.scene) errors.Add(label + ": goalZone belongs to a different scene.");
        }
        issues = string.Join("\n - ", errors);
        return errors.Count == 0;
    }

    public bool ValidateForMatch(bool requireActive = true)
    {
        if (HasStartupFailed) return false;
        if (TryValidateRequiredBindings(out string issues, requireActive)) return true;
        RejectStartup(issues);
        return false;
    }

    private void RejectStartup(string issues)
    {
        if (HasStartupFailed) return;
        _rosterReady = false;
        var mode = GameFlowContext.Instance ? GameFlowContext.Instance.Mode : GameMode.OneVOne;
        StartupFailureReason = $"[Roster] Match startup blocked in scene '{gameObject.scene.name}', roster '{name}', mode {mode}:\n - {issues}\nFix the required bindings and reload the scene.";
        UnsubscribeFromPendingPlayers();
        foreach (var player in FindObjectsByType<PlayerControllerScript>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (player && player.gameObject.scene == gameObject.scene && player.ParticipatesInMatch) player.SetMatchInputLocked(true);
        Debug.LogError(StartupFailureReason, this);
        StartupFailed?.Invoke(StartupFailureReason);
    }

    public void ApplyRoster(bool is2v2)
    {
        if (is2v2)
        {
            SetActiveSafe(player1, true);
            SetActiveSafe(player2, true);
            SetActiveSafe(player3, true);
            SetActiveSafe(player4, true);
            return;
        }

        if (oneVOneUsesPlayers1And3)
        {
            SetActiveSafe(player1, true);
            SetActiveSafe(player3, true);
            SetActiveSafe(player2, false);
            SetActiveSafe(player4, false);
        }
        else
        {
            SetActiveSafe(player1, true);
            SetActiveSafe(player2, true);
            SetActiveSafe(player3, false);
            SetActiveSafe(player4, false);
        }
    }

    private static void SetActiveSafe(GameObject target, bool active)
    {
        if (target != null)
            target.SetActive(active);
    }

    private static bool IsActive(GameObject target)
    {
        return target != null && target.activeInHierarchy;
    }
}
