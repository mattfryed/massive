using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Massive.Player
{
    /// <summary>
    /// Stage 3 (FinisherRepulsor) visual + hitbox driver.
    ///
    /// - Enables a SphereCollider trigger during the stage activation window.
    /// - Grows the radius over time (matches RepulsorRadiusCurve).
    /// - Optionally drives a ParticleSystem's Shape.radius to match.
    /// - Applies a configurable knockback (and optional stun/mass loss) on first contact per victim.
    ///
    /// Recommended hierarchy:
    /// PlayerRoot
    ///   └─ RepulsorAOE (GameObject with SphereCollider isTrigger=true + this component + optional ParticleSystem)
    ///
    /// NOTE: SphereCollider.radius is in LOCAL units. Keep this object at uniform scale (1,1,1)
    /// if you want 'radius' to be in world units.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SphereCollider))]
    public class PlayerRepulsorAOE : MonoBehaviour
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

            if (!attackController)
                attackController = GetComponentInParent<PlayerAttackController>();

            if (!owner)
                owner = GetComponentInParent<PlayerControllerScript>();

            if (attackController != null)
            {
                attackController.OnStageStarted.AddListener(OnStageStarted);
                attackController.OnStageCompleted.AddListener(OnStageCompleted);
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
            }

            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            hitbox.enabled = false;
            hitbox.radius = 0f;
            _hitVictims.Clear();

            if (repulsorFX)
                repulsorFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void OnStageStarted(AttackStage stage)
        {
            if (stage == null || stage.StageType != AttackStageType.FinisherRepulsor)
                return;

            if (_routine != null)
                StopCoroutine(_routine);

            _routine = StartCoroutine(DriveRepulsor(stage));
        }

        private void OnStageCompleted(AttackStage stage)
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

            if (hitbox)
            {
                hitbox.enabled = false;
                hitbox.radius = 0f;
            }

            _hitVictims.Clear();

            if (repulsorFX)
                repulsorFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private IEnumerator DriveRepulsor(AttackStage stage)
        {
            if (!attackController || !hitbox || stage == null)
                yield break;

            // Optional pre-wait until activation window opens.
            if (gateToStageActivationWindow)
            {
                while (attackController.CurrentStage == stage &&
                       attackController.StageNormalizedTime < stage.ActivationStartNormalized)
                {
                    yield return null;
                }
            }

            if (attackController.CurrentStage != stage)
                yield break;

            _hitVictims.Clear();

            hitbox.radius = 0f;
            hitbox.enabled = true;

            if (repulsorFX)
            {
                repulsorFX.Clear(true);
                repulsorFX.Play(true);
            }

            float endNorm = gateToStageActivationWindow ? stage.ActivationEndNormalized : 1f;

            while (attackController.CurrentStage == stage &&
                   attackController.StageNormalizedTime <= endNorm)
            {
                float stageT = attackController.StageNormalizedTime;

                float t01;
                if (gateToStageActivationWindow)
                {
                    float a0 = stage.ActivationStartNormalized;
                    float a1 = Mathf.Max(a0 + 1e-4f, stage.ActivationEndNormalized);
                    t01 = Mathf.Clamp01(Mathf.InverseLerp(a0, a1, stageT));
                }
                else
                {
                    t01 = Mathf.Clamp01(stageT);
                }

                float r01 = stage.RepulsorRadiusCurve != null ? stage.RepulsorRadiusCurve.Evaluate(t01) : t01;
                float radius = stage.RepulsorMaxRadius * Mathf.Clamp01(r01);

                hitbox.radius = radius;

                // Best-effort: drive ParticleSystem shape radius to match.
                if (repulsorFX)
                {
                    var shape = repulsorFX.shape;
                    shape.radius = radius;
                }

                yield return null;
            }

            // Shut off at the end of the window.
            hitbox.enabled = false;
            hitbox.radius = 0f;

            if (repulsorFX)
                repulsorFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            _routine = null;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!hitbox || !hitbox.enabled)
                return;

            if (!owner)
                return;

            if (!other || !other.CompareTag(playerTag))
                return;

            var victim = other.GetComponentInParent<PlayerControllerScript>();
            if (!victim)
                return;

            if (ignoreSelf && victim == owner)
                return;

            if (ignoreTeamMates && victim.teamID == owner.teamID)
                return;

            if (_hitVictims.Contains(victim))
                return;

            _hitVictims.Add(victim);

            // Strength based on distance from owner, clamped.
            Vector3 dir = victim.transform.position - owner.transform.position;
            dir.y = 0f;
            float dist = dir.magnitude;
            if (dist > 0.001f) dir /= dist;
            else dir = Vector3.right;

            float maxR = Mathf.Max(0.001f, hitbox.radius);
            float strength01 = Mathf.Clamp01(1f - (dist / maxR));

            if (applyKnockback)
            {
                var rb = victim.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    float kick = knockbackVelocity * Mathf.Clamp01(strength01);
                    rb.AddForce(dir * kick, ForceMode.VelocityChange);

                    // Clamp planar speed to avoid extreme launches.
                    Vector3 v = rb.linearVelocity;
                    Vector2 planar = new Vector2(v.x, v.z);
                    float spd = planar.magnitude;
                    if (spd > maxPlanarSpeedAfterHit)
                    {
                        planar = planar.normalized * maxPlanarSpeedAfterHit;
                        rb.linearVelocity = new Vector3(planar.x, v.y, planar.y);
                    }
                }
            }

            if (applyStun)
            {
                float s = Mathf.Clamp01(stunStrength01 * strength01);
                victim.Stun(owner.transform.position, Mathf.Lerp(0.1f, 1f, s));
            }

            if (applyMassLoss)
            {
                float s = Mathf.Clamp01(massLossScale01 * strength01);
                victim.ShrinkScaled(owner.gameObject, s);
                if (giveAttackerMass)
                    owner.GrowScaled(s);
            }
        }
    }
}
