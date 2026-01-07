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

    public GameObject P1 => player1;
    public GameObject P2 => player2;
    public GameObject P3 => player3;
    public GameObject P4 => player4;

    private void Awake()
    {
        GameFlowContext.EnsureExists();

        if (autoAssignByPlayerIdIfMissing)
            AutoAssignIfMissing();

        bool is2v2 = GameFlowContext.Instance.IsTwoVTwo;
        ApplyRoster(is2v2);

        // Optional: log once so you can confirm it’s working in play mode.
        Debug.Log($"[Roster] Mode={GameFlowContext.Instance.Mode} | Active: " +
                  $"P1={IsActive(player1)} P2={IsActive(player2)} P3={IsActive(player3)} P4={IsActive(player4)}", this);
    }

    private void Start()
{
    // Start AFTER Awake has activated/deactivated players.
    // Wait one frame so newly enabled players have run Awake/OnEnable.
    StartCoroutine(BeginMatchSpawnAfterOneFrame());
}

private System.Collections.IEnumerator BeginMatchSpawnAfterOneFrame()
{
    yield return null;

    TryBegin(player1);
    TryBegin(player2);
    TryBegin(player3);
    TryBegin(player4);
}

private void TryBegin(GameObject playerGO)
{
    if (!playerGO || !playerGO.activeInHierarchy) return;

    var pc = playerGO.GetComponent<PlayerControllerScript>();
    if (pc) pc.BeginMatchStartSpawn();
}

    private void AutoAssignIfMissing()
    {
        if (player1 && player2 && player3 && player4) return;

        // IMPORTANT: includes inactive objects so “all players off” still wires.
        var players = FindObjectsByType<PlayerControllerScript>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (!p) continue;

            switch (p.playerID)
            {
                case 0: player1 ??= p.gameObject; break;
                case 1: player2 ??= p.gameObject; break;
                case 2: player3 ??= p.gameObject; break;
                case 3: player4 ??= p.gameObject; break;
            }
        }

        if (!player1 || !player2 || !player3 || !player4)
            Debug.LogWarning($"[Roster] Missing player refs after auto-assign. " +
                             $"P1={player1} P2={player2} P3={player3} P4={player4}", this);
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

    private static void SetActiveSafe(GameObject go, bool active)
    {
        if (go != null) go.SetActive(active);
    }

    private static bool IsActive(GameObject go) => go != null && go.activeInHierarchy;
}
