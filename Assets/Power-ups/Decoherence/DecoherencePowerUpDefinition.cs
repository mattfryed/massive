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

        [Header("Equipped appearance — double contour")]
        public bool showEquippedContours = true;
        [Range(0f, 1f)] public float contourOpacity = .38f;
        [Tooltip("Maximum sideways offset of each contour, as a fraction of the body radius.")]
        [Range(0f, .6f)] public float contourSeparation = .24f;
        [Range(1f, 1.3f)] public float contourScale = 1.08f;
        [Range(.2f, 2f)] public float contourWidthScale = .65f;
        [Min(.2f)] public float contourCycleSeconds = 1.8f;
        [Header("Successful phase-through pulse")]
        [Min(.05f)] public float phasePulseSeconds = .28f;
        [Tooltip("Additional separation of the existing shield copies on a successful phase-through.")]
        [Range(0f, 1f)] public float phaseSeparationBoost = .3f;

        public override PowerUpType Type => PowerUpType.Decoherence;
    }
}
