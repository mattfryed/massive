using Massive.Scoring;
using UnityEngine;

namespace Massive.PowerUps
{
    [CreateAssetMenu(fileName = "PU_AmplifierNode", menuName = "MASSIVE/PowerUps/Amplifier Node")]
    public sealed class AmplifierNodePowerUpDefinition : PowerUpDefinition, IInstantPowerUpEffect
    {
        [Header("Shared Mass Grant")]
        [Tooltip("Uses this Mass Node's mass gain, sound and collection effects. Tuning Mass Node also tunes Amplifier Node.")]
        public MassNodePowerUpDefinition massNode;

        public override PowerUpType Type => PowerUpType.AmplifierNode;

        public void ApplyInstant(PlayerControllerScript player, PlayerPowerUpController powerUps, GameObject pickupGO)
        {
            if (!player) return;

            if (massNode) massNode.ApplyInstant(player, powerUps, pickupGO);

            // Registration is idempotent and ensures the chain uses this match's economy.
            // Instant collection leaves any equipped ability and its timer untouched.
            if (player.ParticipatesInMatch && MatchScoreService.Instance)
                MatchScoreService.Instance.RegisterPlayer(player);

            var chain = player.GetComponent<PlayerScoreChain>();
            if (chain) chain.AdvanceToNextStep();
        }
    }
}
