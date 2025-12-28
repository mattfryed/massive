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

    public GameObject P1 => player1;
    public GameObject P2 => player2;
    public GameObject P3 => player3;
    public GameObject P4 => player4;

    public bool IsTwoVTwo { get; private set; }

    private void Awake()
    {
        GameFlowContext.EnsureExists();
    }

    private void Start()
    {
        IsTwoVTwo = GameFlowContext.Instance.IsTwoVTwo;
        ApplyRoster(IsTwoVTwo);
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
}
