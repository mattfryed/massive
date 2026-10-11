using UnityEngine;

namespace Massive.Player
{
    public partial class PlayerAttackController
    {
        [Header("Thrust Charge")]
        [SerializeField] private bool thrustChargeEnabled = true;
        [SerializeField, Min(.05f)] private float thrustChargeMaxSeconds = 3f;
        [SerializeField, Min(1f)] private float thrustChargeMaxDistanceMultiplier = 3f;
        [SerializeField, Min(1f)] private float thrustChargeMaxDamageMultiplier = 2f;
        [SerializeField, Min(0f)] private float thrustChargeVibrationAmplitude = .08f;
        [SerializeField, Min(1f)] private float thrustChargeVibrationFrequency = 28f;

        [SerializeField] [Range(0f, 1f)] private float thrustChargeMinMoveMultiplier = .25f;
        [SerializeField]  private bool thrustChargeGuideEnabled = true;
        [SerializeField] [Range(1, 12)] private int thrustChargeGuideArrowCount = 5;
        [SerializeField] [Range(0f, 1f)] private float thrustChargeGuideDistanceFraction = .5f;
        [SerializeField] [Min(0f)] private float thrustChargeGuideStrokeWidth = .035f;
        [SerializeField] [Min(.01f)] private float thrustChargeGuideArrowLength = .25f;
        [SerializeField] [Min(.01f)] private float thrustChargeGuideArrowWidth = .4f;
        [SerializeField] [Min(.01f)] private float thrustChargeGuideRevealSeconds = .15f;
        [SerializeField]  private Color thrustChargeGuideColor = new Color(1f, 1f, 1f, .85f);

        public float ChargeMovementMultiplier => IsCharging ? Mathf.Lerp(1f, Effective_thrustChargeMinMoveMultiplier, Charge01) : 1f;

        public bool IsCharging { get; private set; }
        float chargeStartedAt;
        float pendingCharge01;
        float stageDistanceMultiplier = 1f;
        float stageDamageMultiplier = 1f;
        public float ChargeElapsed => IsCharging ? Mathf.Max(0f, Time.time - chargeStartedAt) : 0f;
        public float Charge01 => IsCharging ? Mathf.Clamp01(ChargeElapsed / Effective_thrustChargeMaxSeconds) : 0f;
        public float CurrentDamageMultiplier => isAttacking && currentStage != null &&
            currentStage.StageType == AttackStageType.PrimaryLunge ? stageDamageMultiplier : 1f;

        // Routed after shield and power-up input. No independent timer can fire
        // a charge while the player input owner is stunned, locked or suppressed.
        public void HandleAttackInput(bool down, bool held, bool up, bool allowed)
        {
            if (!allowed || !isActiveAndEnabled || Profile == null || Profile.Stages.Count == 0) { CancelCharge(); return; }
            if (IsCharging)
            {
                float charge = Charge01;
                if (up || !held || charge >= 1f || !Effective_thrustChargeEnabled)
                {
                    CancelCharge();
                    pendingCharge01 = Effective_thrustChargeEnabled ? charge : 0f;
                    StartStage(0);
                }
                return;
            }
            if (!down) return; // Holding after auto-fire never queues another stage.
            if (isAttacking) { RegisterAttackPress(); return; }
            if (Profile == null || Profile.Stages.Count == 0 || CooldownRemaining > 0f) return;
            if (!Effective_thrustChargeEnabled || Profile.GetStage(0).StageType != AttackStageType.PrimaryLunge)
            { BeginAttack(); return; }
            chargeStartedAt = Time.time;
            IsCharging = true;
            if (up || !held) { CancelCharge(); BeginAttack(); }
        }

        public void CancelCharge()
        {
            IsCharging = false;
            pendingCharge01 = 0f;
        }

        // The guide shows half the unobstructed maximum range; aim uses the same
        // assistance as a release now. Querying it never starts or retargets an attack.
        public void GetChargeGuidePath(out Vector3 origin, out Vector3 direction, out float maxDistance)
        {
            origin = transform.position;
            direction = ComputeAttackDirectionFromInput();
            var stage = Profile != null ? Profile.GetStage(0) : null;
            float baseDistance = stage != null ? stage.TravelDistance * PlayerScaleAdjuster.ActionReachOf(this) : 0f;
            maxDistance = baseDistance * Effective_thrustChargeMaxDistanceMultiplier;
            var motor = ownerController ? ownerController.LatticeMotor : null;
            if (motor && motor.IsQuantized && motor.field)
            {
                var field = motor.field;
                Vector3 local = field.transform.InverseTransformVector(direction);
                var step = Massive.Lattice.LatticePlayerMotor.Direction8(new Vector2(local.x, local.y));
                Vector3 delta = field.transform.TransformVector(new Vector3(step.x, step.y, 0f)) * field.Spacing;
                direction = delta.normalized;
                maxDistance = delta.magnitude * Mathf.Max(1, Mathf.FloorToInt(Effective_thrustChargeMaxDistanceMultiplier + .5f));
            }
            else if (Effective_lockOnEnabled && TryFindLungeLockOn(rb ? rb.position : origin, direction,
                baseDistance * Mathf.Lerp(1f, Effective_thrustChargeMaxDistanceMultiplier, Charge01),
                out _, out var assisted, out _)) direction = assisted;
        }

        void CaptureStageCharge()
        {
            bool thrust = currentStage.StageType == AttackStageType.PrimaryLunge;
            stageDistanceMultiplier = thrust ? Mathf.Lerp(1f, Effective_thrustChargeMaxDistanceMultiplier, pendingCharge01) : 1f;
            stageDamageMultiplier = thrust ? Mathf.Lerp(1f, Effective_thrustChargeMaxDamageMultiplier, pendingCharge01) : 1f;
            pendingCharge01 = 0f;
        }
    }
}
