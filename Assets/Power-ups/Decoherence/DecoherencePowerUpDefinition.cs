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

        [Header("Rechargeable paint")]
        [Min(.05f), Tooltip("Continuous painting time available from a full meter. Acquisition starts full.")]
        public float fullMeterPaintSeconds = 6f;
        [Min(.05f), Tooltip("Seconds from empty to full while not painting. Refill starts on release or exhaustion.")]
        public float emptyToFullRefillSeconds = 4f;
        [Range(.01f, 1f), Tooltip("Minimum charge required for a fresh Shield press. Exhaustion requires release before another activation.")]
        public float minimumActivationCharge = .15f;
        [Min(.1f), Tooltip("How long each area remains on the arena after it is painted. Repainting refreshes only the new brush footprint.")]
        public float paintLifetimeSeconds = 4f;
        [Min(.01f), Tooltip("Final portion of each paint deposit's lifetime spent smoothly shrinking to zero. Opacity stays constant; damage and grid echoes follow the shrinking area.")]
        public float paintRecedeSeconds = .24f;
        [Tooltip("Use the same energy-ring prefab as Particle Accelerator.")]
        public GameObject energyRingPrefab;

        [Header("Painted disruption field")]
        [Min(.1f), Tooltip("Brush radius at full charge and player size 1. New paint scales linearly with remaining charge; existing paint keeps its radius until its final contraction.")]
        public float fieldRadius = 2.2f;
        [Min(0f), Tooltip("Seconds for each activation to ease from the player's body outline to its live charge radius. This grows the actual painted area, including damage.")]
        public float activationEaseSeconds = .24f;
        [Min(.05f), Tooltip("Exposure needed to deal one ordinary sword hit. Time to defeat = remaining health / sword-hit damage × this value. Leaving the field stops damage; it does not heal the enemy.")]
        public float secondsPerSwordHit = 1f;
        [Range(.05f, 1f), Tooltip("Replaces the normal shield movement scale while the field is active.")]
        public float fieldMovementMultiplier = .9f;
        [Min(0f)] public float disruptionSeconds = .25f;
        [Min(.1f), Tooltip("Minimum interval between disruptions of one enemy, shared by all Decoherence players. Each activation can disrupt an enemy only once.")]
        public float disruptionRecoverySeconds = 2f;
        [Min(0f), Tooltip("Separate player mass damage per second. Shields retain their ordinary damage protection. Never transfers mass or repeatedly stuns players.")]
        public float playerDamagePerSecond = .04f;
        [Header("Field appearance")]
        [Range(0f, 1f)] public float fieldOpacity = .55f;
        [Min(.001f)] public float fieldLineWidth = .018f;
        [Min(0f), Tooltip("Subtle world-space outline displacement at player size 1. Cosmetic only; the brush footprint remains stable.")]
        public float outlineNoiseAmount = .045f;
        [Min(.01f)] public float outlineNoiseFrequency = 1.35f;
        [Min(0f)] public float outlineNoiseSpeed = .45f;
        [Range(0, 4)] public int ghostOutlineCount = 3;
        [Min(0f)] public float ghostOutlineNoiseAmount = .13f;
        [Range(0f, 1f), Tooltip("Opacity of each ghost relative to the main outline. Each copy uses its own smooth noise pattern.")]
        public float ghostOutlineOpacity = .24f;
        [Min(0f)] public float enemySplitDistance = .16f;

        [Header("Displaced grid echoes")]
        [Tooltip("Renders two faint copies of the actual arena grid, clipped to painted ground.")]
        public Material gridEchoMaterial;
        [Range(0f, 1f)] public float gridEchoOpacity = .16f;
        [Min(0f), Tooltip("World displacement at player size 1. Purely visual; never changes grid physics.")]
        public float gridEchoDisplacement = .18f;
        [Min(0f)] public float gridEchoSpeed = .4f;

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
