using UnityEngine;

namespace Massive.PowerUps
{
    [CreateAssetMenu(fileName = "PU_ParticleAccelerator", menuName = "MASSIVE/PowerUps/Particle Accelerator")]
    public class ParticleAcceleratorPowerUpDefinition : PowerUpDefinition
    {
        [Header("Input")]
        public string activateInputName = "Sword"; // replaces melee while active

        [Header("Charge")]
        public float chargeToMaxSeconds = 1.25f;
        public float minDistanceOnTap = 2.0f;
        public float maxDistance = 10.0f;

        [Header("Cooldown")]
        [Tooltip("Cooldown += chargeSeconds * rechargeMultiplier")]
        public float rechargeMultiplier = 1.1f;
        [Tooltip("Always added to cooldown so quick taps can't spam.")]
        public float baseRechargeDelay = 0.35f;

        [Header("Beam / Projectile")]
        public float beamThicknessMin = 0.18f;
        public float beamThicknessMax = 0.45f;
        public float beamSpeed = 18.0f;

        [Header("Damage / Mass")]
        public float massRemovedMin = 0.06f;
        public float massRemovedMax = 0.16f;
        [Tooltip("If true, removed mass is added to shooter.")]
        public bool transferMassToShooter = true;

        [Header("Prefab")]
        [Tooltip("Projectile prefab. If null, we'll spawn a simple sphere projectile at runtime.")]
        public GameObject beamProjectilePrefab;

        [Header("Aim Assist / Movement")]
        public float selfSlowWhileCharging = 0.55f; // 1 = no slow
        public float aimLockTurnSpeed = 999f; // optional later

        public override PowerUpType Type => PowerUpType.ParticleAccelerator;
    }
}