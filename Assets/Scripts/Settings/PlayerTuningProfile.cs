using Massive.Player;
using UnityEngine;

namespace Massive.Settings
{
    [CreateAssetMenu(menuName = "MASSIVE/Shared Settings/Player Tuning")]
    public sealed class PlayerTuningProfile : SharedSettingsProfile
    {
        public PlayerAttackProfile attackProfile;
        public PlayerLocomotionSettings movement = new PlayerLocomotionSettings();
        public PlayerCombatSettings combat = new PlayerCombatSettings();
        public PlayerRepulsorImpactSettings repulsor = new PlayerRepulsorImpactSettings();
    }

    [System.Serializable]
    public sealed class PlayerRepulsorImpactSettings
    {
        public bool applyKnockback = true;
        [Min(0)] public float knockbackVelocity = 12f;
        [Min(0)] public float maxPlanarSpeedAfterHit = 16f;
        public bool applyStun;
        [Range(0, 1)] public float stunStrength01 = .35f;
        public bool applyMassLoss = true;
        [Tooltip("Outer-zone player damage as a fraction of a normal hit. The inner-zone multiplier is applied afterward.")]
        [Range(0, 1)] public float massLossScale01 = 1f;
        public bool giveAttackerMass;
    }

    [System.Serializable]
    public sealed class PlayerLocomotionSettings
    {
        [Min(0)] public float movePower = 10f;
        [Min(.1f)] public float maxMoveSpeed = 7.5f;
        public bool clampSpeed = true;
        [Min(0)] public float lateralFriction = 35f;
        [Min(0)] public float reverseBrake = 45f;
        [Range(-1, 1)] public float reverseDotThreshold;
        [Min(0)] public float idleBrake = 8f;
        [Range(.05f, .6f)] public float moveDeadzone = .15f;
        [Range(.1f, 1)] public float attackingMoveScale = .6f;
        [Range(.05f, 1)] public float shieldMoveMultiplier = .4f;
    }

    [System.Serializable]
    public sealed class PlayerCombatSettings
    {
        [Min(0)] public float attackCooldown = .25f;
        [Min(0)] public float comboInputBuffer = .15f;
        [Tooltip("Fallback for stages without a custom engagement window.")]
        public bool useSharedComboWindow = true;
        [Min(0)] public float sharedComboWindowSeconds = .15f;
        public bool comboWindowAfterActivationWindow = true;
        [Range(0, 1)] public float comboWindowEndNormalized = 1f;
        [Tooltip("Easing across the Swipe damage window: 0 is the left edge, 1 is the right edge.")]
        public AnimationCurve swipeArcCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [Tooltip("Maximum turn to either side of Thrust's starting heading, after aim assistance. 0 locks aim; 180 removes the limit.")]
        [Range(0f, 180f)] public float thrustMaxTurnDegrees = 15f;
        public bool visualDirectionFollowsCombatFacing = true;
        public bool lockOnEnabled = true;
        [Range(0, 90)] public float lockOnConeHalfAngleDeg = 25f;
        [Min(0)] public float lockOnMaxDistanceOverride;
        [Range(0, 1)] public float lockOnDirectionBlend = .85f;
    }
}
