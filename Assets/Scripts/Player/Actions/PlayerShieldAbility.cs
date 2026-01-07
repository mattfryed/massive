using System;
using UnityEngine;

namespace Massive.Player
{
    /// <summary>
    /// Timed shield ability with a soft cooldown.
    ///
    /// Design:
    /// - Shield is activated on button DOWN (not held)
    /// - Shield stays active for a fixed duration
    /// - Shield can be re-triggered during cooldown, but strength scales with cooldown progress
    /// - Strength is exposed for combat logic (partial stun + partial damage leak)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerShieldAbility : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private PlayerControllerScript owner;
        [SerializeField] private PlayerAttackController attackController;
        [SerializeField] private GameObject shieldColliderObject;
        [SerializeField] private ShieldRingsVfx_Shapes ringsVfx;

        public bool SuppressDefaultShieldVfx { get; set; } = false;

        [Header("Timing")]
        [SerializeField, Min(0.05f)] private float durationSeconds = 0.65f;
        [SerializeField, Min(0.05f)] private float cooldownSeconds = 2.0f;

        [Header("Strength (soft cooldown)")]
        [Tooltip("When you press Shield while still on cooldown, this is the minimum strength you can get. Set to 0 for 'no shield if spammed'.")]
        [SerializeField, Range(0f, 1f)] private float minStrengthWhenEmpty = 0.15f;

        [Tooltip("Maps cooldown progress (0..1) -> strength (0..1) before the min-strength floor is applied.")]
        [SerializeField] private AnimationCurve chargeToStrength = AnimationCurve.Linear(0, 0, 1, 1);

        [Header("Rules")]
        [SerializeField] private bool requireNotAttacking = true;
        [SerializeField] private bool cancelIfOwnerStunned = true;
        [SerializeField] private bool cancelIfOwnerExternallyStunned = true;

        public bool IsActive { get; private set; }

        /// <summary>
        /// Valid while active. 0..1
        /// </summary>
        public float CurrentStrength01 { get; private set; } = 1f;

        public float CooldownSeconds => cooldownSeconds;
        public float DurationSeconds => durationSeconds;

        /// <summary>
        /// 0 right after using, 1 when fully cooled down.
        /// </summary>
        public float CooldownProgress01
        {
            get
            {
                if (cooldownSeconds <= 0.0001f) return 1f;
                return Mathf.Clamp01((Time.time - lastUseTime) / cooldownSeconds);
            }
        }

        public float CooldownRemaining
        {
            get
            {
                if (cooldownSeconds <= 0.0001f) return 0f;
                return Mathf.Max(0f, (lastUseTime + cooldownSeconds) - Time.time);
            }
        }

        public event Action<PlayerShieldAbility> ShieldStarted;
        public event Action<PlayerShieldAbility> ShieldEnded;

        private float lastUseTime = -999f;
        private float endTime;
        private Vector3 lastFaceDirWS = Vector3.right;


        private void Reset()
        {
            owner = GetComponentInParent<PlayerControllerScript>();
            attackController = GetComponentInParent<PlayerAttackController>();
            if (owner != null && shieldColliderObject == null)
                shieldColliderObject = owner.shield;
            ringsVfx = GetComponentInChildren<ShieldRingsVfx_Shapes>(true);
        }

        private void Awake()
        {
            if (!owner) owner = GetComponentInParent<PlayerControllerScript>();
            if (!attackController) attackController = GetComponentInParent<PlayerAttackController>();
            if (!shieldColliderObject && owner != null) shieldColliderObject = owner.shield;
            if (!ringsVfx) ringsVfx = GetComponentInChildren<ShieldRingsVfx_Shapes>(true);

            // Ensure rings follow the correct player (prevents duplicate/prefab offset issues)
            if (ringsVfx != null && owner != null)
            {
                var vc = owner.visualsController;
                Transform follow = owner.transform;
                if (vc != null && vc.visuals != null)
                    follow = vc.visuals;

                ringsVfx.Bind(follow, vc);
            }
            

            // Start disabled
            if (shieldColliderObject != null)
                shieldColliderObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (owner != null)
            {
                owner.DeathStarted += OnOwnerDeathStarted;
                owner.RespawnCompleted += OnOwnerRespawnCompleted;
            }
        }

        private void OnDisable()
        {
            if (owner != null)
            {
                owner.DeathStarted -= OnOwnerDeathStarted;
                owner.RespawnCompleted -= OnOwnerRespawnCompleted;
            }

            ForceStopShield();
        }

        private void Update()
        {
            if (!IsActive) return;

            if (owner != null)
            {
                if (cancelIfOwnerExternallyStunned && owner.IsExternallyStunned)
                {
                    ForceStopShield();
                    return;
                }

                if (cancelIfOwnerStunned && owner.IsStunned)
                {
                    ForceStopShield();
                    return;
                }

                if (owner.temporarilyEliminated)
                {
                    ForceStopShield();
                    return;
                }
            }

            if (Time.time >= endTime)
            {
                ForceStopShield();
                return;
            }

            // Future-proofing: if shield collider becomes directional, keep it facing movement.
            if (shieldColliderObject != null && owner != null)
            {
                Vector3 face = (owner.movement.sqrMagnitude > 0.001f) ? owner.movement : lastFaceDirWS;
                face.y = 0f;
                if (face.sqrMagnitude > 0.0001f)
                {
                    lastFaceDirWS = face.normalized;
                    shieldColliderObject.transform.rotation = Quaternion.LookRotation(lastFaceDirWS, Vector3.up);
                }
            }
        }

        public bool TryActivate()
        {
            if (!enabled || !gameObject.activeInHierarchy) return false;
            if (IsActive) return false; // no stacking

            if (owner != null)
            {
                if (owner.temporarilyEliminated) return false;
                if (cancelIfOwnerExternallyStunned && owner.IsExternallyStunned) return false;
                if (cancelIfOwnerStunned && owner.IsStunned) return false;
            }

            if (requireNotAttacking && attackController != null && attackController.IsAttacking)
                return false;

            BeginShield(ComputeStrength01());
            return true;
        }

        private float ComputeStrength01()
        {
            float charge01 = CooldownProgress01;
            float curved = chargeToStrength != null ? Mathf.Clamp01(chargeToStrength.Evaluate(charge01)) : charge01;
            float strength = Mathf.Lerp(minStrengthWhenEmpty, 1f, curved);
            return Mathf.Clamp01(strength);
        }

        private void BeginShield(float strength01)
        {
            lastUseTime = Time.time;
            endTime = Time.time + Mathf.Max(0.05f, durationSeconds);

            CurrentStrength01 = Mathf.Clamp01(strength01);
            IsActive = true;

            // Legacy hook: other combat code checks PlayerControllerScript.shieldOn
            if (owner != null)
                owner.shieldOn = true;

    if (shieldColliderObject != null)
        shieldColliderObject.SetActive(true);

    // Only play default rings if NOT suppressed
    if (!SuppressDefaultShieldVfx && ringsVfx != null)
        ringsVfx.Play(CurrentStrength01, durationSeconds);

        AudioSystem.I?.Play(AudioEventId.Player_ShieldUp, transform.position);

    ShieldStarted?.Invoke(this);
        }

        public void ForceStopShield()
        {
            if (!IsActive) return;

            IsActive = false;
            CurrentStrength01 = 0f;

            if (shieldColliderObject != null)
                shieldColliderObject.SetActive(false);


            // Only stop default rings if NOT suppressed
            if (!SuppressDefaultShieldVfx && ringsVfx != null)
                ringsVfx.Stop(immediate: false);

            if (owner != null)
                owner.shieldOn = false;

            if (ringsVfx != null)
            ringsVfx.Stop(immediate: false);

            ShieldEnded?.Invoke(this);
        }

        private void OnOwnerDeathStarted(PlayerControllerScript pcs)
        {
            ForceStopShield();
        }

        private void OnOwnerRespawnCompleted(PlayerControllerScript pcs)
        {
            // On respawn: shield ready immediately
            lastUseTime = Time.time - cooldownSeconds;
            ForceStopShield();
        }
    }
}
