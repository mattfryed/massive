using UnityEngine;

/// <summary>
/// Retained only so legacy scene references do not become missing scripts during
/// the scoring migration. Physical goal deposit is not part of the timed energy
/// economy and this component no longer mutates player mass or team score.
/// </summary>
[DisallowMultipleComponent]
public class ScoreScript : MonoBehaviour
{
    [SerializeField] private bool logLegacyContact;
    public GameObject Goal;

    private bool _warned;

    private void OnTriggerStay(Collider other)
    {
        if (!logLegacyContact || _warned || other == null || !other.CompareTag("Body"))
            return;

        _warned = true;
        Debug.LogWarning(
            "[ScoreScript] Legacy physical deposit was contacted. This component is intentionally inert; " +
            "route scoring through MatchScoreService/ScoreRewardEmitter instead.",
            this);
    }
}
