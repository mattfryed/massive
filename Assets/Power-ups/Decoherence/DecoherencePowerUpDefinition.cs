using UnityEngine;

namespace Massive.PowerUps
{
    [CreateAssetMenu(fileName = "PU_Decoherence", menuName = "MASSIVE/PowerUps/Decoherence")]
    public class DecoherencePowerUpDefinition : PowerUpDefinition
    {
        [Header("Input")]
        public string activateInputName = "Shield"; // defensive input

        [Header("Shield Replacement VFX")]
        public GameObject shieldVfxPrefab;   // prefab with MetaballSDFInstance + MetaballManifest + PowerupDecoherenceVisual

        [Header("Tuning")]
        [Tooltip("How close attacker must be for decoherence to trigger.")]
        public float distanceWindow = 1.25f;

        [Tooltip("How long attacker is stunned on a successful decoherence.")]
        public float attackerStunSeconds = 0.55f;

        [Tooltip("How far attacker gets 'phased through' defender on success.")]
        public float passThroughDistance = 1.0f;

        [Tooltip("How long to ignore collisions between attacker/defender after success.")]
        public float ignoreCollisionSeconds = 0.25f;

        public override PowerUpType Type => PowerUpType.Decoherence;
    }
}