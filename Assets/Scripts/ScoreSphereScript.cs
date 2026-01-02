using UnityEngine;

public class ScoreSphereScript : MonoBehaviour
{
    [Header("Identity")]
    public int teamID = 1;

    [Header("Score State (0..1 = 0..100)")]
    [SerializeField, Range(0f, 1f)] private float score01 = 0.5f;

    [Header("Visuals")]
    public GameObject sphereGraphic;   // the thing that scales
    public GameObject scoreboard;      // has ScoreboardManagerScript
    [SerializeField] private float maxSize = 11f; // “100%” size of the score graphic

    [Header("Respawn Penalty")]
    [SerializeField, Range(0f, 1f)]
    private float respawnPenalty01 = 0.05f;

    [Header("Legacy Deposit Mode (OFF for Mass v2)")]
    [SerializeField] private bool enableGoalDeposit = false;

    // old pacing knob retained so deposit feels identical if re-enabled later
    [SerializeField] private float sizeChangeOnGoalHit = 0.01f;

    public float Score01 => score01;
    public float MaxSize => maxSize;

    private void Start()
    {
        // Treat score01 as authoritative at runtime.
        ApplyVisuals();
    }

    private void ApplyVisuals()
    {
        if (sphereGraphic != null)
        {
            float s = score01 * maxSize;

            if (sphereGraphic.TryGetComponent<RectTransform>(out var rt))
                rt.localScale = new Vector3(s, s, s);
            else
                sphereGraphic.transform.localScale = new Vector3(s, s, s);
        }

        if (scoreboard != null)
        {
            var sb = scoreboard.GetComponent<ScoreboardManagerScript>();
            if (sb != null) sb.UpdateScoreboard(score01);
        }
    }

    public void SetScore01(float newScore01)
    {
        score01 = Mathf.Clamp01(newScore01);
        ApplyVisuals();
    }

    public void AddScore01(float delta01)
    {
        if (Mathf.Approximately(delta01, 0f)) return;
        SetScore01(score01 + delta01);
    }

    public void RemoveScore01(float delta01)
    {
        if (Mathf.Approximately(delta01, 0f)) return;
        SetScore01(score01 - delta01);
    }

    // ---- Compatibility: PlayerControllerScript currently calls LoseScore(teamID) on death ----
    public void LoseScore(int whichTeam)
    {
        if (teamID != whichTeam) return;
        RemoveScore01(respawnPenalty01);
    }

    // Optional explicit penalty API (useful later)
    public void LoseScore01(float penalty01)
    {
        RemoveScore01(penalty01);
    }

    // ---- Legacy deposit loop (disabled for Mass v2) ----
    private void OnTriggerStay(Collider other)
    {
        if (!enableGoalDeposit) return;
        if (!other.CompareTag("Player")) return;

        var pcs = other.GetComponent<PlayerControllerScript>();
        if (pcs == null || pcs.teamID != teamID) return;

        if (pcs.GoalShrink())
        {
            // old behavior was: add sizeChangeOnGoalHit to scale.
            // In score01 space, that’s:
            AddScore01(sizeChangeOnGoalHit / Mathf.Max(0.0001f, maxSize));
        }
    }
}
