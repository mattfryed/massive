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

    public event Action RosterReady;

    public GameObject P1 => player1;
    public GameObject P2 => player2;
    public GameObject P3 => player3;
    public GameObject P4 => player4;
    public bool IsRosterReady => _rosterReady;

    private void Awake()
    {
        GameFlowContext.EnsureExists();

        if (autoAssignByPlayerIdIfMissing)
            AutoAssignIfMissing();

        bool is2v2 = GameFlowContext.Instance.IsTwoVTwo;
        ApplyRoster(is2v2);

        Debug.Log(
            $"[Roster] Mode={GameFlowContext.Instance.Mode} | Active: " +
            $"P1={IsActive(player1)} P2={IsActive(player2)} P3={IsActive(player3)} P4={IsActive(player4)}",
            this);
    }

    private void Start()
    {
        StartCoroutine(BeginMatchSpawnAfterOneFrame());
    }

    private void OnDestroy()
    {
        UnsubscribeFromPendingPlayers();
    }

    private IEnumerator BeginMatchSpawnAfterOneFrame()
    {
        yield return null;

        _rosterReady = false;
        _pendingMatchSpawns.Clear();

        CollectPendingPlayer(player1);
        CollectPendingPlayer(player2);
        CollectPendingPlayer(player3);
        CollectPendingPlayer(player4);

        if (_pendingMatchSpawns.Count == 0)
        {
            MarkRosterReady();
            yield break;
        }

        PlayerControllerScript[] players = new PlayerControllerScript[_pendingMatchSpawns.Count];
        _pendingMatchSpawns.CopyTo(players);

        for (int i = 0; i < players.Length; i++)
            players[i].MatchSpawnCompleted += OnPlayerMatchSpawnCompleted;

        for (int i = 0; i < players.Length; i++)
            players[i].BeginMatchStartSpawn();
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
        if (player != null)
            player.MatchSpawnCompleted -= OnPlayerMatchSpawnCompleted;

        _pendingMatchSpawns.Remove(player);

        if (_pendingMatchSpawns.Count == 0)
            MarkRosterReady();
    }

    private void MarkRosterReady()
    {
        if (_rosterReady) return;
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

        foreach (PlayerControllerScript player in players)
        {
            if (!player) continue;

            switch (player.playerID)
            {
                case 0: player1 ??= player.gameObject; break;
                case 1: player2 ??= player.gameObject; break;
                case 2: player3 ??= player.gameObject; break;
                case 3: player4 ??= player.gameObject; break;
            }
        }

        if (!player1 || !player2 || !player3 || !player4)
        {
            Debug.LogWarning(
                $"[Roster] Missing player refs after auto-assign. " +
                $"P1={player1} P2={player2} P3={player3} P4={player4}",
                this);
        }
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
