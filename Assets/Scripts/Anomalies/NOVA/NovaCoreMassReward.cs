using UnityEngine;

/// <summary>
/// NOVA anomaly reward:
/// - Converts captured core mass (AnomalyResult.score) into team score
///   via GameManagerScript.
/// - Sends a "shield" contribution to NovaStarController so the next
///   bounce is less damaging for the winning team.
/// </summary>
[CreateAssetMenu(menuName = "MASSIVE/AnomalyRewards/Nova Core Mass Reward")]
public class NovaCoreMassReward : AnomalyReward
{
    [Header("Score Conversion")]
    [Tooltip("How much game score to grant per unit of captured core mass.")]
    public float scorePerMassUnit = 1f;

    [Header("Bounce Shielding")]
    [Tooltip("How much 'shield' (abstract units) to grant per unit of core mass. NovaStarController decides how that translates into bounce reduction.")]
    public float shieldPerMassUnit = 0.2f;

    public override void Apply(AnomalyResult result, AnomalyManager manager)
    {
        // If there is no winning team or no mass, treat as a no-op.
        if (!result.success || result.winningTeamIndex < 0 || result.score <= 0f)
        {
            // You *could* treat a total failure as a negative effect,
            // but we'll keep rewards one-sided and let the star handle
            // "bad bounces" generically.
            return;
        }

        int teamIndex = result.winningTeamIndex;
        float capturedMass = result.score;

        // 1) Convert captured mass into team score through GameManagerScript.
        var gm = Object.FindObjectOfType<GameManagerScript>();
        if (gm != null)
        {
            float scoreDelta = capturedMass * scorePerMassUnit;

            // Assuming Team 0 → finalScore_1, Team 1 → finalScore_2.
            if (teamIndex == 0)
            {
                gm.finalScore_1 += scoreDelta;
            }
            else if (teamIndex == 1)
            {
                gm.finalScore_2 += scoreDelta;
            }

            // If you ever add more teams, extend this mapping.
        }

        // 2) Send shield contribution to the star controller.
        var star = Object.FindObjectOfType<NovaStarController>();
        if (star != null)
        {
            float shieldAmount = capturedMass * shieldPerMassUnit;
            star.RegisterCoreCapture(teamIndex, shieldAmount);
        }
    }
}
