using UnityEngine;
using UnityEngine.Serialization;

namespace Massive.PowerUps
{
    [CreateAssetMenu(fileName = "PU_ParticleAccelerator", menuName = "MASSIVE/PowerUps/Particle Accelerator")]
    public class ParticleAcceleratorPowerUpDefinition : PowerUpDefinition
    {
        [Header("Input")]
        public string activateInputName = "Sword"; // replaces melee while active

        [Header("Energy meter")]
        [Tooltip("Continuous firing time available from a full meter. Every acquisition starts full.")]
        [Min(.01f)] public float fullMeterFireSeconds = 6f;
        [Tooltip("Time to refill from empty to full whenever not firing. Refilling starts immediately.")]
        [Min(.01f)] public float emptyToFullRefillSeconds = 4f;
        [Tooltip("Minimum duration per press, including a quick tap. Available energy always limits the burst.")]
        [Min(0f)] public float minimumBurstSeconds = 1f;

        [Header("Firing movement")]
        [InspectorName("Movement Speed Scale While Firing")]
        [Tooltip("Movement speed and propulsion scale during the active beam, including a minimum tap burst. 0 = no movement input, 0.5 = half speed, 1 = full speed.")]
        [Range(0f, 1f)] public float movementWhileFiring = .65f;
        [InspectorName("Turning Speed Scale While Firing")]
        [Tooltip("Scale of the player's normal maximum turn speed for beam aim and facing during firing. 0 locks the firing direction; 0.5 gives half turn speed; 1 gives full turn speed. Separate from distance-based beam arcing.")]
        [Range(0f, 1f)] public float turningWhileFiring = 1f;
        [Tooltip("Time for firing aim to ease into a turn, brake, and reverse direction. Higher values feel softer; zero disables easing. Turning Speed Scale still limits the maximum speed.")]
        [Min(0f)] public float aimDirectionEaseSeconds = .2f;

        // Retained for serialized legacy assets; the energy meter replaces charge/cooldown.
        [HideInInspector] public float chargeToMaxSeconds = 1.25f;
        [HideInInspector] public float minDistanceOnTap = 2.0f;
        [Header("Range")]
        public float maxDistance = 10.0f;

        [Header("Cooldown")]
        [Tooltip("Cooldown += chargeSeconds * rechargeMultiplier")]
        [HideInInspector] public float rechargeMultiplier = 1.1f;
        [Tooltip("Always added to cooldown so quick taps can't spam.")]
        [HideInInspector] public float baseRechargeDelay = 0.35f;

        [Header("Recoil")]
[HideInInspector] public float recoilVelocityMin = 0.0f;
[HideInInspector] public float recoilVelocityMax = 2.5f;


        [Header("Beam / Projectile")]
[HideInInspector] public float beamVisualMinLength = 2.5f;
[HideInInspector] public float beamVisualMaxLength = 6.0f;

        [HideInInspector] public float beamThicknessMin = 0.18f;
        [HideInInspector] public float beamThicknessMax = 0.45f;
        [HideInInspector] public float beamSpeed = 18.0f;
        

        [HideInInspector] public float massRemovedMin = 0.06f;
        [HideInInspector] public float massRemovedMax = 0.16f;
        [Header("Damage / Mass")]
        [FormerlySerializedAs("damagePerSecond")]
        [Tooltip("Player mass removed per second of full-strength beam contact, before shield reduction. Does not affect enemy health.")]
        [Min(0f)] public float playerDamagePerSecond = .08f;
        [Tooltip("Enemy health removed per second of full-strength beam contact. Independent of player damage and mass transfer.")]
        [Min(0f)] public float enemyDamagePerSecond = .08f;
        [Tooltip("If true, mass removed from another player is added to the shooter. Enemy damage does not transfer mass.")]
        public bool transferMassToShooter = true;

        [Header("Prefab")]
        [Tooltip("Projectile prefab. If null, we'll spawn a simple sphere projectile at runtime.")]
        [HideInInspector] public GameObject beamProjectilePrefab;

        [Header("Aim Assist / Movement")]
        [HideInInspector] public float selfSlowWhileCharging = 0.55f; // legacy
        [HideInInspector] public float aimLockTurnSpeed = 999f;

        [Header("Energy ring appearance")]
        [Tooltip("Shared ring prefab. The old charge orb and aim ray are disabled in energy-meter mode.")]
        public GameObject vfxModulePrefab;

        [Header("Telegraph")]
        [HideInInspector] public float preFireTelegraphSeconds = 0.15f;

        [Header("Blocking")]
        [HideInInspector] public LayerMask blockMask;

        [Header("Sustained turret plasma beam")]
        public ParticleAcceleratorBeam sustainedBeamPrefab;
        [Min(.001f)] public float beamRadius = .1f;
        [Tooltip("Time for the beam tip to grow from zero to full range. The same growth resumes from its blocked length when a target moves away; damage and impact plasma wait for the tip to arrive.")]
        [Min(.01f)] public float beamGrowSeconds = .12f;
        [Min(.01f)] public float beamFadeSeconds = .36f;
        [Min(1f)] public float openingRadiusMultiplier = 1.7f;
        [Min(.01f)] public float openingSettleSeconds = .32f;
        [Min(0f)] public float muzzleOffset = .6f;
        public float muzzleHeight = .2f;
        public LayerMask beamCollisionMask = ~0;

        [Header("Turn propagation / arc")]
        [Tooltip("Aim changes travel this many world units per second. Higher = straighter, faster response.")]
        [Min(1f)] public float turnPropagationSpeed = 45f;
        [Tooltip("Maximum aim history delay at the distant end of the beam. Zero disables arcing.")]
        [Range(0f, 1f)] public float maxTurnDelay = .35f;
        [Tooltip("Limits the difference between current aim and delayed beam tangents, preventing foldback on sharp turns.")]
        [Range(0f, 60f)] public float maxBendAngle = 35f;


        public override PowerUpType Type => PowerUpType.ParticleAccelerator;
    }
}
