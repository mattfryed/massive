using Massive.Enemies;
using Massive.Scoring;
using UnityEngine;

/// <summary>Conduit endpoint and aggregated receipt feedback. Never calculates or awards score.</summary>
public class SymmetryKnotGoalMouth : MonoBehaviour
{
    public int teamID = 1;
    [SerializeField] private Transform flowAnchor;
    [SerializeField] private EnemyScoreToast receiptToastPrefab;
    [SerializeField, Min(.5f)] private float receiptIntervalSeconds = 1f;
    private long pendingEnergy;
    private float receiptAge;
    public Transform FlowAnchor => flowAnchor != null ? flowAnchor : transform;
    public long ReceivedMilliElectronVolts { get; private set; }
    public int ReceiptsShown { get; private set; }

    public void ReceiveEnergy(ScoreAwardResult award)
    {
        if (!award.accepted || award.teamID != teamID || award.finalMilliElectronVolts <= 0) return;
        pendingEnergy = EnergyScoreMath.SaturatingAdd(pendingEnergy, award.finalMilliElectronVolts);
        ReceivedMilliElectronVolts = EnergyScoreMath.SaturatingAdd(ReceivedMilliElectronVolts, award.finalMilliElectronVolts);
    }

    private void Update()
    {
        if (pendingEnergy <= 0) { receiptAge = 0; return; }
        if (MatchScoreService.Instance != null && !MatchScoreService.Instance.IsChainClockRunning &&
            MatchScoreService.Instance.IsScoringOpen) return;
        receiptAge += Time.deltaTime;
        if (receiptAge < receiptIntervalSeconds) return;
        var receipt = new ScoreAwardResult { accepted = true, teamID = teamID,
            finalMilliElectronVolts = pendingEnergy, worldPosition = FlowAnchor.position };
        if (EnemyScoreToast.Show(receiptToastPrefab, receipt, gameObject.scene) != null) ReceiptsShown++;
        pendingEnergy = 0; receiptAge = 0;
    }

    private void OnDisable() { pendingEnergy = 0; receiptAge = 0; }
}
