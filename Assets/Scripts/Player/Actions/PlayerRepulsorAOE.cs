using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Massive.Enemies;

namespace Massive.Player
{
    /// <summary>
    /// Stage 3 (FinisherRepulsor) visual + hitbox driver.
    ///
    /// - Enables a SphereCollider trigger during the stage activation window.
    /// - Grows the radius over time (matches RepulsorRadiusCurve).
    /// - Optionally drives a ParticleSystem's Shape.radius to match.
    /// - Applies player knockback and profile-controlled NPC damage once per victim.
    ///
    /// Recommended hierarchy:
    /// PlayerRoot
    ///   └─ RepulsorAOE (GameObject with SphereCollider isTrigger=true + this component + optional ParticleSystem)
    ///
    /// World radii are converted to the collider's local frame exactly once.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SphereCollider))]
    public partial class PlayerRepulsorAOE : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerAttackController attackController;
        [SerializeField] private PlayerControllerScript owner;
        [SerializeField] private SphereCollider hitbox;
        [SerializeField] private ParticleSystem repulsorFX;

        [Header("Gating")]
        [Tooltip("If true, the AOE collider + growth are driven only during the stage activation window.\nIf false, they run for the full stage duration.")]
        [SerializeField] private bool gateToStageActivationWindow = true;

        [Header("Target Filtering")]
        [SerializeField] private bool ignoreSelf = true;
        [SerializeField] private bool ignoreTeamMates = true;
        [Tooltip("Tag used for player colliders.")]
        [SerializeField] private string playerTag = "Player";

        [Header("Scene View")]
        [Tooltip("Shows the final hit area when this player or hitbox is selected. The live sphere is shown while the pulse is active.")]
        [SerializeField] private bool showHitArea = true;

        [Header("Knockback")]
        [SerializeField] private bool applyKnockback = true;
        [Tooltip("VelocityChange impulse at strength01=1.")]
        [SerializeField] private float knockbackVelocity = 12f;
        [Tooltip("Clamp victim planar speed after knockback.")]
        [SerializeField] private float maxPlanarSpeedAfterHit = 16f;

        [Header("Optional Stun")]
        [SerializeField] private bool applyStun = false;
        [Range(0f, 1f)]
        [SerializeField] private float stunStrength01 = 0.35f;

        [Header("Optional Mass Loss")]
        [SerializeField] private bool applyMassLoss = false;
        [Range(0f, 1f)]
        [SerializeField] private float massLossScale01 = 0.25f;
        [Tooltip("If true, attacker gains the same scaled amount the victim loses.")]
        [SerializeField] private bool giveAttackerMass = false;

        private Coroutine _routine;
        private readonly HashSet<PlayerControllerScript> _hitVictims = new HashSet<PlayerControllerScript>();
        private readonly HashSet<EnemyBase> _hitEnemies = new HashSet<EnemyBase>();
        private float _enemyDamage;
        private readonly Collider[] _finalOverlap = new Collider[64];
        private PlayerVisualController _visuals;
        private Vector3 _authoredColliderCenter;

        public bool IsPulseActive { get; private set; }
        public float ActiveProgress01 { get; private set; }
        public float StartRadiusWorld { get; private set; }
        public float EndRadiusWorld { get; private set; }
        public float PulseScale { get; private set; } = 1f;
        public Vector3 OriginWorld { get; private set; }
        public float RadiusWorld => hitbox && hitbox.enabled
            ? hitbox.radius * LargestAxis(hitbox.transform.lossyScale) : 0f;
        public event System.Action<PlayerRepulsorAOE> PulseStarted;
        public event System.Action<PlayerRepulsorAOE> PulseEnded;

        private void Reset()
        {
            hitbox = GetComponent<SphereCollider>();
            hitbox.isTrigger = true;

            attackController = GetComponentInParent<PlayerAttackController>();
            owner = GetComponentInParent<PlayerControllerScript>();
        }

        private void OnEnable()
        {
            if (!hitbox)
                hitbox = GetComponent<SphereCollider>();

            hitbox.isTrigger = true;
            hitbox.enabled = false;
            hitbox.radius = 0f;
            _authoredColliderCenter = hitbox.center;

            if (!attackController)
                attackController = GetComponentInParent<PlayerAttackController>();

            if (!owner)
                owner = GetComponentInParent<PlayerControllerScript>();
            _visuals = owner ? owner.visualsController : GetComponentInParent<PlayerVisualController>();

            if (attackController != null)
            {
                attackController.OnStageStarted.AddListener(OnStageStarted);
                attackController.OnStageCompleted.AddListener(OnStageCompleted);
                attackController.StageCancelled += OnStageCancelled;
            }

            if (repulsorFX)
                repulsorFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void OnDisable()
        {
            if (attackController != null)
            {
                attackController.OnStageStarted.RemoveListener(OnStageStarted);
                attackController.OnStageCompleted.RemoveListener(OnStageCompleted);
                attackController.StageCancelled -= OnStageCancelled;
            }

            StopAndReset();

            if (repulsorFX)
                repulsorFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void OnStageStarted(AttackStage stage)
        {
            if (stage == null || stage.StageType != AttackStageType.FinisherRepulsor)
                return;

            StopAndReset();
            _routine = StartCoroutine(DriveRepulsor(stage));
        }

        private void OnStageCompleted(AttackStage stage)
        {
            if (stage == null || stage.StageType != AttackStageType.FinisherRepulsor)
                return;

            // A slow frame may complete the whole stage before the coroutine gets
            // its last active tick. Cancellation never takes this completion path.
            if (IsPulseActive && IsStageLive(stage)) CompleteFinalCoverage();
            StopAndReset();
        }

        private void OnStageCancelled(AttackStage stage)
        {
            if (stage == null || stage.StageType != AttackStageType.FinisherRepulsor)
                return;
            StopAndReset();
        }

        private void StopAndReset()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            FinishPulse();
        }

        private void FinishPulse()
        {
            bool wasActive = IsPulseActive;
            IsPulseActive = false;
            if (hitbox)
            {
                hitbox.enabled = false;
                hitbox.radius = 0f;
                hitbox.center = _authoredColliderCenter;
            }
            _hitVictims.Clear();
            _hitEnemies.Clear();
            _enemyDamage = 0f;
            if (repulsorFX)
                repulsorFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (wasActive) PulseEnded?.Invoke(this);
        }

        private bool IsStageLive(AttackStage stage)
        {
            return attackController && attackController.isActiveAndEnabled &&
                attackController.IsAttacking && attackController.CurrentStage == stage &&
                owner && owner.isActiveAndEnabled && !owner.temporarilyEliminated && !owner.IsStunned &&
                !owner.IsExternallyStunned && !owner.IsMatchInputLocked;
        }

        private static float LargestAxis(Vector3 scale)
        {
            return Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        }

        public float EffectiveActivationStart(AttackStage stage)
        {
            return gateToStageActivationWindow && stage != null ? stage.ActivationStartNormalized : 0f;
        }

        public float EffectiveActivationEnd(AttackStage stage)
        {
            return gateToStageActivationWindow && stage != null ? stage.ActivationEndNormalized : 1f;
        }

        private void FixedUpdate()
        {
            if (!IsPulseActive || !hitbox) return;
            if (!attackController || !IsStageLive(attackController.CurrentStage))
            {
                StopAndReset();
                return;
            }
            // The wave remains at its release point even if the player moves.
            hitbox.center = hitbox.transform.InverseTransformPoint(OriginWorld);
        }

        private float GetOutlineRadiusWorld()
        {
            if (_visuals)
            {
                Transform frame = _visuals.visuals ? _visuals.visuals : _visuals.transform;
                return Mathf.Max(0f, _visuals.baseRadius + _visuals.outlineHalf) *
                    LargestAxis(frame.lossyScale) * _visuals.RepulsorVisualScale;
            }
            return PlayerScaleAdjuster.BodyRadiusOf(owner);
        }

        public static AttackStage FindRepulsorStage(PlayerAttackProfile profile)
        {
            if (profile != null)
                foreach (var stage in profile.Stages)
                    if (stage != null && stage.StageType == AttackStageType.FinisherRepulsor) return stage;
            return null;
        }

        /// <summary>The expected release outline and scaled reach used by edit preview and its hit-area guide.</summary>
        public float PreviewEndRadiusWorld(AttackStage stage)
        {
            if (!owner) owner = GetComponentInParent<PlayerControllerScript>();
            var visuals = owner ? owner.visualsController : null;
            if (!visuals && owner) visuals = owner.GetComponentInChildren<PlayerVisualController>(true);
            float outline = PlayerScaleAdjuster.BodyRadiusOf(owner);
            if (visuals)
            {
                Transform frame = visuals.visuals ? visuals.visuals : visuals.transform;
                outline = Mathf.Max(0f, visuals.baseRadius + visuals.outlineHalf) * LargestAxis(frame.lossyScale);
            }
            var feedback = owner ? owner.GetComponent<PlayerRepulsorFeedback>() : null;
            if (feedback && feedback.isActiveAndEnabled && feedback.bodyPulseEnabled && EffectiveActivationStart(stage) > 0f)
                outline *= 1f - feedback.contraction;
            return stage != null ? stage.GetRepulsorRadius(PlayerScaleAdjuster.SizeOf(owner), outline) : outline;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!showHitArea) return;
            var player = owner ? owner : GetComponentInParent<PlayerControllerScript>();
            if (!UnityEditor.Selection.Contains(gameObject) && (!player || !UnityEditor.Selection.Contains(player.gameObject))) return;
            var attack = attackController ? attackController : GetComponentInParent<PlayerAttackController>();
            var stage = FindRepulsorStage(attack ? attack.Profile : null);
            if (stage == null) return;
            Vector3 center = IsPulseActive ? OriginWorld : player ? player.transform.position : transform.position;
            float radius = IsPulseActive ? EndRadiusWorld : PreviewEndRadiusWorld(stage);
            var previousColor = UnityEditor.Handles.color;
            UnityEditor.Handles.color = new Color(.2f, .85f, 1f, .85f);
            UnityEditor.Handles.DrawWireDisc(center, Vector3.up, radius);
            UnityEditor.Handles.Label(center + Vector3.right * radius,
                "Repulsor hit radius " + radius.ToString("0.##") + " | enemy damage " + stage.RepulsorEnemyDamage.ToString("0.##"));
            if (IsPulseActive)
            {
                UnityEditor.Handles.color = new Color(1f, .75f, .15f, 1f);
                UnityEditor.Handles.DrawWireDisc(center, Vector3.up, RadiusWorld);
            }
            UnityEditor.Handles.color = previousColor;
        }
#endif

        private void SetWorldRadius(float radius)
        {
            hitbox.radius = radius / Mathf.Max(.0001f, LargestAxis(hitbox.transform.lossyScale));
            hitbox.center = hitbox.transform.InverseTransformPoint(OriginWorld);
            if (repulsorFX)
            {
                var shape = repulsorFX.shape;
                shape.radius = radius / Mathf.Max(.0001f, LargestAxis(repulsorFX.transform.lossyScale));
            }
        }

        private IEnumerator DriveRepulsor(AttackStage stage)
        {
            if (!attackController || !hitbox || stage == null)
                yield break;

            // Optional pre-wait until activation window opens.
            if (gateToStageActivationWindow)
            {
                while (IsStageLive(stage) &&
                       attackController.StageNormalizedTime < EffectiveActivationStart(stage))
                {
                    yield return null;
                }
            }

            if (!IsStageLive(stage))
                yield break;

            _hitVictims.Clear();
            _hitEnemies.Clear();
            _enemyDamage = stage.RepulsorEnemyDamage;
            PulseScale = stage.RepulsorScale;
            ActiveProgress01 = 0f;
            OriginWorld = _visuals && _visuals.visuals ? _visuals.visuals.position : owner.transform.position;
            if (_visuals) OriginWorld += _visuals.RepulsorVisualOffsetWS;
            StartRadiusWorld = GetOutlineRadiusWorld();
            EndRadiusWorld = stage.GetRepulsorRadius(PlayerScaleAdjuster.SizeOf(owner), StartRadiusWorld);
            SetWorldRadius(StartRadiusWorld);
            hitbox.enabled = true;
            IsPulseActive = true;

            if (repulsorFX)
            {
                repulsorFX.Clear(true);
                repulsorFX.Play(true);
            }

            PulseStarted?.Invoke(this);

            float endNorm = EffectiveActivationEnd(stage);

            while (IsPulseActive && IsStageLive(stage) &&
                   attackController.StageNormalizedTime <= endNorm)
            {
                float stageT = attackController.StageNormalizedTime;

                float t01;
                if (gateToStageActivationWindow)
                {
                    float a0 = EffectiveActivationStart(stage);
                    float a1 = Mathf.Max(a0 + 1e-4f, endNorm);
                    t01 = Mathf.Clamp01(Mathf.InverseLerp(a0, a1, stageT));
                }
                else
                {
                    t01 = Mathf.Clamp01(stageT);
                }

                ActiveProgress01 = t01;
                float r01 = stage.RepulsorRadiusCurve != null ? stage.RepulsorRadiusCurve.Evaluate(t01) : t01;
                SetWorldRadius(Mathf.Lerp(StartRadiusWorld, EndRadiusWorld, Mathf.Clamp01(r01)));

                yield return null;
            }

            if (IsPulseActive && IsStageLive(stage)) CompleteFinalCoverage();
            _routine = null;
            FinishPulse();
        }

        private void CompleteFinalCoverage()
        {
            if (!IsPulseActive || !hitbox || !hitbox.enabled) return;
            ActiveProgress01 = 1f;
            SetWorldRadius(EndRadiusWorld);
            // Physics can sample just before the final radius, particularly on a
            // low frame rate. Resolve that final sphere once without leaving a
            // damaging collider behind after the activation window.
            int count = Physics.OverlapSphereNonAlloc(OriginWorld, EndRadiusWorld,
                _finalOverlap, Physics.AllLayers, QueryTriggerInteraction.Collide);
            if (count == _finalOverlap.Length)
            {
                // Rare crowded scenes must not silently omit players or enemies.
                Collider[] all = Physics.OverlapSphere(OriginWorld, EndRadiusWorld,
                    Physics.AllLayers, QueryTriggerInteraction.Collide);
                foreach (Collider other in all) TryHit(other);
            }
            else
            {
                for (int i = 0; i < count; i++) TryHit(_finalOverlap[i]);
            }
            System.Array.Clear(_finalOverlap, 0, count);
        }

        private void OnTriggerEnter(Collider other)
        {
            TryHit(other);
        }

        private void OnTriggerStay(Collider other)
        {
            // Covers a player already overlapping when this pulse enables.
            TryHit(other);
        }

        private void TryHit(Collider other)
        {
            if (!IsPulseActive || !hitbox || !hitbox.enabled)
                return;

            if (!owner)
                return;

            if (!other) return;

            var enemyHurtbox = other.GetComponent<EnemyHurtbox>();
            var enemy = enemyHurtbox && enemyHurtbox.isActiveAndEnabled ? enemyHurtbox.Enemy : null;
            if (enemy)
            {
                if (_enemyDamage <= 0f || !enemy.isActiveAndEnabled || enemy.IsDead || enemy.IsPaused || !enemy.Definition ||
                    (ignoreTeamMates && enemy.OwnerTeamId == owner.teamID) || !_hitEnemies.Add(enemy)) return;
                enemy.TakeDamage(_enemyDamage, EnemyDamageSource.Repulsor, owner);
                return;
            }

            if (!other.CompareTag(playerTag)) return;

            var victim = other.GetComponentInParent<PlayerControllerScript>();
            if (!victim)
                return;
            if (victim.temporarilyEliminated)
                return;

            if (ignoreSelf && victim == owner)
                return;

            if (ignoreTeamMates && victim.teamID == owner.teamID)
                return;

            if (_hitVictims.Contains(victim))
                return;

            _hitVictims.Add(victim);

            // Strength based on distance from owner, clamped.
            Vector3 dir = victim.transform.position - OriginWorld;
            dir.y = 0f;
            float dist = dir.magnitude;
            if (dist > 0.001f) dir /= dist;
            else dir = Vector3.right;

            // The advancing front touches a victim's collider before its center.
            // Use final reach for falloff so that first contact is not a zero hit.
            float maxR = Mathf.Max(0.001f, EndRadiusWorld);
            float strength01 = Mathf.Clamp01(1f - (dist / maxR));

            if (Effective_applyKnockback)
            {
                var rb = victim.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    float kick = Effective_knockbackVelocity * Mathf.Clamp01(strength01);
                    victim.ProtectActionMomentum(.25f);
                    rb.AddForce(dir * kick, ForceMode.VelocityChange);

                    // Clamp planar speed to avoid extreme launches.
                    Vector3 v = rb.linearVelocity;
                    Vector2 planar = new Vector2(v.x, v.z);
                    float spd = planar.magnitude;
                    if (spd > Effective_maxPlanarSpeedAfterHit)
                    {
                        planar = planar.normalized * Effective_maxPlanarSpeedAfterHit;
                        rb.linearVelocity = new Vector3(planar.x, v.y, planar.y);
                    }
                }
            }

            if (Effective_applyStun)
            {
                float s = Mathf.Clamp01(Effective_stunStrength01 * strength01);
                victim.Stun(OriginWorld, Mathf.Lerp(0.1f, 1f, s));
            }

            if (Effective_applyMassLoss)
            {
                float s = Mathf.Clamp01(Effective_massLossScale01 * strength01);
                victim.ShrinkScaled(owner.gameObject, s);
                if (Effective_giveAttackerMass)
                    owner.GrowScaled(s);
            }
        }
    }
}
