using UnityEngine;

namespace Massive.Player
{
    /// <summary>
    /// Rotates a weapon/sword pivot during the ComboSwipe stage so the visual + hitbox sweep in an arc.
    ///
    /// Setup (recommended):
    /// CombatFacingRoot (from PlayerVisualController)
    ///   └─ SwordPivot (this component lives here, pivot at player center)
    ///        └─ SwordHitbox (offset outward, has BoxCollider + PlayerMelee)
    ///        └─ EnergyCrackleFX (optional ParticleSystem)
    /// </summary>
    [DisallowMultipleComponent]
    public class AttackSwordArcDriver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerAttackController attackController;

        [Tooltip("Pivot to rotate for the swing. If null, uses this.transform.")]
        [SerializeField] private Transform pivot;

        [Header("Rotation")]
        [Tooltip("If true, rotate ONLY during ComboSwipe. If false, rotation still only applies when the controller reports a non-zero arc offset.")]
        [SerializeField] private bool driveDuringComboSwipeOnly = true;

        [Tooltip("If true, crackle particles only play while the activation window is open.")]
        [SerializeField] private bool particlesOnlyDuringActivationWindow = true;

        [Header("Energy / Crackle Particles (optional)")]
        [SerializeField] private ParticleSystem energyCrackle;

        private Quaternion _baseLocalRotation;
        private bool _baseCached;

        private void Reset()
        {
            pivot = transform;
            attackController = GetComponentInParent<PlayerAttackController>();
        }

        private void Awake()
        {
            if (!attackController)
                attackController = GetComponentInParent<PlayerAttackController>();

            CacheBase();
        }

        private void OnEnable()
        {
            if (!attackController)
                attackController = GetComponentInParent<PlayerAttackController>();

            CacheBase();

            // Start disabled/neutral
            ApplyYawOffset(0f);
            StopCrackle(clear: true);
        }

        private void OnDisable()
        {
            ApplyYawOffset(0f);
            StopCrackle(clear: true);
        }

        private void LateUpdate()
        {
            if (!attackController)
                return;

            if (!pivot)
                pivot = transform;

            CacheBase();

            AttackStage stage = attackController.CurrentStage;
            bool attacking = attackController.IsAttacking && stage != null;

            bool isComboSwipe = attacking && stage.StageType == AttackStageType.ComboSwipe;
            bool shouldDriveRotation = attacking && (!driveDuringComboSwipeOnly || isComboSwipe);

            float yawOffsetDeg = (shouldDriveRotation && isComboSwipe)
                ? attackController.CurrentWeaponYawOffsetDeg
                : 0f;

            ApplyYawOffset(yawOffsetDeg);

            // Optional crackle particles
            if (energyCrackle)
            {
                bool shouldPlay = attacking;

                if (driveDuringComboSwipeOnly)
                    shouldPlay &= isComboSwipe;

                if (particlesOnlyDuringActivationWindow && stage != null)
                {
                    float t = attackController.StageNormalizedTime;
                    shouldPlay &= (t >= stage.ActivationStartNormalized && t <= stage.ActivationEndNormalized);
                }

                if (shouldPlay)
                {
                    if (!energyCrackle.isPlaying)
                        energyCrackle.Play(true);
                }
                else
                {
                    if (energyCrackle.isPlaying)
                        energyCrackle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }

        private void CacheBase()
        {
            if (_baseCached)
                return;

            if (!pivot)
                pivot = transform;

            _baseLocalRotation = pivot.localRotation;
            _baseCached = true;
        }

        /// <summary>
        /// If you re-orient the pivot in-editor and want that to become the new "neutral" rotation,
        /// call this (or toggle the component).
        /// </summary>
        public void RecacheBaseNow()
        {
            if (!pivot)
                pivot = transform;

            _baseLocalRotation = pivot.localRotation;
            _baseCached = true;
        }

        private void ApplyYawOffset(float yawOffsetDeg)
        {
            if (!pivot)
                pivot = transform;

            // Compose yaw around local/world up. In this project, we only care about planar yaw.
            pivot.localRotation = Quaternion.AngleAxis(yawOffsetDeg, Vector3.up) * _baseLocalRotation;
        }

        private void StopCrackle(bool clear)
        {
            if (!energyCrackle)
                return;

            energyCrackle.Stop(true, clear ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
