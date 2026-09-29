using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Massive.Player
{
    /// <summary>Tap for the original parry; keep holding for a limited, full damage block.</summary>
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
        [Tooltip("Hard lockout after the shield ends. During this time, Shield cannot be activated at all.")]
        [SerializeField, Min(0f)] private float activationCooldownSeconds = 0.35f;


        [Tooltip("Tap lifetime and opening parry window, including when held.")]
        [SerializeField, Min(0.05f)] private float durationSeconds = 0.65f;
        [SerializeField, Min(0.05f)] private float cooldownSeconds = 2.0f;

        [Tooltip("Total protection time from button-down while the button remains held.")]
        [SerializeField, Min(.05f)] private float maxHoldSeconds = 3f;

        [Header("Parry contact")]
        [Tooltip("Allow the lunge to close this fraction of the remaining body gap before the registered parry resolves.")]
        [SerializeField, Range(0f, 1f)] private float contactAdvanceFraction = .5f;
        [Tooltip("Maximum extra lunge time. The original contact reserves the parry even if its window expires meanwhile.")]
        [SerializeField, Range(0f, .15f)] private float maxContactDelay = .08f;

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

        public bool IsHolding => IsActive && inputHeld && Time.time < endTime;
        public bool BlocksAllDamage => IsHolding;
        public bool IsParryWindow => IsActive && Time.time < lastUseTime + durationSeconds && Time.time < endTime;
        public float MaxHoldSeconds => maxHoldSeconds;
        public float HoldRemaining => IsHolding ? Mathf.Max(0f, endTime - Time.time) : 0f;
        public float CooldownSeconds => cooldownSeconds;
        public float DurationSeconds => durationSeconds;

        public float ActivationCooldownSeconds => activationCooldownSeconds;
        public float ActivationCooldownRemaining => Mathf.Max(0f, nextAllowedActivateTime - Time.time);

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
        private bool inputHeld, released;
        private PlayerNuggetsGPU nuggets;
        private readonly HashSet<PlayerControllerScript> pendingParries = new HashSet<PlayerControllerScript>();
        private Vector3 lastFaceDirWS = Vector3.right;
        private float nextAllowedActivateTime = -999f;


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

            nuggets = owner ? owner.GetComponentInChildren<PlayerNuggetsGPU>(true) : null;

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
            StopAllCoroutines();
            pendingParries.Clear();
            if (ringsVfx) ringsVfx.Stop(immediate: true);
            if (nuggets) nuggets.ClearParryFeedback();
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
                Vector3 face = owner.AimDirectionWS;
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
            // Hard lockout (prevents spam-tapping to chain shields)
            if (Time.time < nextAllowedActivateTime) return false;

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

        /// <summary>Called for both Rewired and scripted input. Release never re-arms without another press.</summary>
        public void SetHeld(bool held)
        {
            if (!IsActive || released) return;
            inputHeld = held;
            if (!held) released = true;
            endTime = lastUseTime + (held ? Mathf.Max(.05f, maxHoldSeconds) : Mathf.Max(.05f, durationSeconds));
            if (ringsVfx && !SuppressDefaultShieldVfx)
                ringsVfx.SetHeld(held, maxHoldSeconds);
            if (Time.time >= endTime) ForceStopShield();
        }

        /// <summary>Reserve the opening parry at contact; ordinary attack movement closes part of the gap.</summary>
        public bool QueueMeleeParry(PlayerControllerScript attacker, Action<float, bool> onImpact)
        {
            if (!IsParryWindow || !attacker || attacker.IsStunned || !pendingParries.Add(attacker)) return false;
            StartCoroutine(ApproachThenParry(attacker, CurrentStrength01, BlocksAllDamage, onImpact));
            return true;
        }

        private IEnumerator ApproachThenParry(PlayerControllerScript attacker, float strength, bool fullyBlocked,
            Action<float, bool> onImpact)
        {
            float distance = PlanarDistance(attacker.transform.position, transform.position);
            float bodyDistance = PlayerScaleAdjuster.BodyRadiusOf(attacker) + PlayerScaleAdjuster.BodyRadiusOf(owner);
            float targetDistance = distance - Mathf.Max(0f, distance - bodyDistance) * contactAdvanceFraction;
            float deadline = Time.time + maxContactDelay;
            try
            {
                // No teleport or additional force: collision resolution continues to constrain the lunge.
                while (attacker && owner && !owner.temporarilyEliminated && !attacker.temporarilyEliminated &&
                    !attacker.IsStunned && attacker.attackController && attacker.attackController.IsAttacking &&
                    Time.time < deadline && PlanarDistance(attacker.transform.position, transform.position) > targetDistance + .01f)
                    yield return new WaitForFixedUpdate();

                if (!attacker || !owner || owner.temporarilyEliminated || attacker.temporarilyEliminated || attacker.IsStunned)
                    yield break;
                CompleteParry(attacker, transform.position, strength);
                onImpact?.Invoke(strength, fullyBlocked);
            }
            finally { pendingParries.Remove(attacker); }
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
        { a.y = b.y = 0f; return Vector3.Distance(a, b); }

        public bool TryParryProjectile(PlayerControllerScript attacker, Vector3 impactPosition)
        {
            if (!IsParryWindow || !attacker) return false;
            CompleteParry(attacker, impactPosition, CurrentStrength01);
            return true;
        }

        private void CompleteParry(PlayerControllerScript attacker, Vector3 impactPosition, float strength)
        {
            attacker.Stun(impactPosition, strength);
            if (strength <= .0001f) return;
            if (ringsVfx && !SuppressDefaultShieldVfx) ringsVfx.PlayParryBlast(strength);
            if (nuggets) nuggets.PlayParryFeedback(attacker.stunTime * strength, strength);
            AudioSystem.I?.Play(AudioEventId.Player_Parry, transform.position);
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
            inputHeld = false;
            released = false;
            lastUseTime = Time.time;
            endTime = Time.time + Mathf.Max(0.05f, durationSeconds);

            CurrentStrength01 = Mathf.Clamp01(strength01);
            IsActive = true;

            // Legacy hook: other combat code checks PlayerControllerScript.shieldOn
            if (owner != null)
                owner.shieldOn = true;

            if (shieldColliderObject != null)
                shieldColliderObject.SetActive(true);
            if (!SuppressDefaultShieldVfx && ringsVfx != null)
                ringsVfx.Play(CurrentStrength01, durationSeconds);

            AudioSystem.I?.Play(AudioEventId.Player_ShieldUp, transform.position);
            ShieldStarted?.Invoke(this);
        }

        public void ForceStopShield()
        {
            if (!IsActive) return;


            // Start hard lockout AFTER the shield finishes.
            nextAllowedActivateTime = Time.time + Mathf.Max(0f, activationCooldownSeconds);

            IsActive = false;
            inputHeld = false;
            released = true;
            CurrentStrength01 = 0f;

            if (shieldColliderObject != null)
                shieldColliderObject.SetActive(false);


            // Only stop default rings if NOT suppressed
            if (!SuppressDefaultShieldVfx && ringsVfx != null)
                ringsVfx.Stop(immediate: false);

            if (owner != null)
                owner.shieldOn = false;


            ShieldEnded?.Invoke(this);
        }

        private void OnOwnerDeathStarted(PlayerControllerScript pcs)
        {
            ForceStopShield();
            StopAllCoroutines();
            pendingParries.Clear();
            if (ringsVfx) ringsVfx.Stop(immediate: true);
            if (nuggets) nuggets.ClearParryFeedback();
        }

        private void OnOwnerRespawnCompleted(PlayerControllerScript pcs)
        {
            // On respawn: shield ready immediately
            lastUseTime = Time.time - cooldownSeconds;
            ForceStopShield();
            nextAllowedActivateTime = Time.time - 999f;
        }
    }
}
