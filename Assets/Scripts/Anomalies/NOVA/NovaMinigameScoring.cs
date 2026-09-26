using System.Collections.Generic;
using Massive.Scoring;
using UnityEngine;

/// <summary>One route for NOVA interactions to the match economy. Receipts are
/// presentation data; MatchScoreService remains the only score authority.</summary>
[DisallowMultipleComponent]
public sealed class NovaMinigameScoring : MonoBehaviour
{
    public const string CaptureRewardKey = ScoreRewardKeys.NovaCoreCapture;
    [SerializeField, ScoreRewardKey] private string captureRewardKey = CaptureRewardKey;
    private MatchScoreService service;
    private readonly HashSet<PlayerControllerScript> participants = new();
    private readonly HashSet<string> acceptedInteractions = new();
    private readonly Dictionary<int, long> receipts = new();
    private int session;
    private bool accepting;
    public bool IsSessionBound => service != null;
    public long AwardedToTeam(int team) => receipts.TryGetValue(team, out var value) ? value : 0L;

    public void BeginSession(MatchScoreService scores, IReadOnlyList<PlayerControllerScript> roster)
    {
        service = scores;
        session++;
        participants.Clear();
        acceptedInteractions.Clear();
        receipts.Clear();
        accepting = false;
        if (roster != null)
            foreach (var player in roster)
                if (player && !player.IsPseudoPlayer) participants.Add(player);
    }

    public void SetAcceptingInteractions(bool value) => accepting = value;

    public bool TryAwardCapture(PlayerControllerScript player, int particleId, int subparticleCount,
        out ScoreAwardResult result) =>
        TryAwardInteraction(captureRewardKey, player, "PARTICLE-" + particleId, subparticleCount, out result);

    // Future NOVA interactions supply a central reward key and a stable event ID,
    // never raw score, normalized sphere progress or an independently applied multiplier.
    public bool TryAwardInteraction(string rewardKey, PlayerControllerScript player, string interactionId,
        long quantity, out ScoreAwardResult result)
    {
        result = default;
        if (!accepting || !service || !player || !participants.Contains(player) ||
            string.IsNullOrWhiteSpace(rewardKey) || string.IsNullOrWhiteSpace(interactionId) || quantity <= 0)
            return false;
        string key = rewardKey.Trim().ToUpperInvariant() + "|" + interactionId.Trim();
        if (acceptedInteractions.Contains(key)) return false;
        string token = "NOVA-" + GetInstanceID() + "|SESSION-" + session + "|" + key;
        if (!service.TryAwardToPlayer(rewardKey, player, token, player.transform.position, out result, quantity))
            return false;
        acceptedInteractions.Add(key);
        receipts[player.teamID] = EnergyScoreMath.SaturatingAdd(AwardedToTeam(player.teamID), result.finalMilliElectronVolts);
        return true;
    }

    private void OnDisable() => accepting = false;
}
