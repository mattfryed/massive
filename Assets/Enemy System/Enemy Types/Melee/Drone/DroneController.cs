using System.Collections.Generic;
using UnityEngine;

namespace Massive.Enemies
{
    /// <summary>Small contact attacker: local perception, stable targets, bounded steering and swarm separation.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(EnemyBase), typeof(Rigidbody), typeof(EnemyObstacleAvoidance))]
    public sealed class DroneController : MonoBehaviour
    {
        [Header("Standalone prefab (Director supplies the definition when spawned)")]
        public EnemyDefinition definition;
        [Header("Perception")]
        [Min(.1f)] public float detectionRange = 6f;
        [Min(.1f)] public float disengageRange = 8f;
        [Min(.05f)] public float decisionInterval = .2f;
        [Tooltip("Keep the current target unless another is this fraction as far away.")]
        [Range(.1f, 1f)] public float retargetDistanceRatio = .65f;
        [Header("Steering")]
        [Min(.1f)] public float acceleration = 7f;
        [Min(0f)] public float idleSpeed = .25f;
        [Min(.1f)] public float separationRadius = .85f;
        [Range(0f, 3f)] public float separationStrength = 1.1f;
        [Min(.05f)] public float bodyRadius = .23f;
        [Header("Shield response")]
        [Min(0f)] public float shieldRecoilSpeed = 3f;
        [Min(.05f)] public float shieldRecoverySeconds = .45f;
        [Header("Spawn")]
        [Min(0f)] public float spawnGraceSeconds = .5f;

        public PlayerControllerScript Target { get; private set; }
        public bool IsReady => enemy != null && enemy.Definition != null && !enemy.IsDead && !enemy.IsPaused && age >= spawnGraceSeconds;
        private static readonly List<DroneController> active = new List<DroneController>();
        private EnemyBase enemy;
        private Rigidbody body;
        private EnemyObstacleAvoidance avoidance;
        private ArenaBoundsFromVectorGrid bounds;
        private float age, decisionTimer, recoilRemaining;
        private Vector3 separation, idleDirection;
        private readonly RaycastHit[] sweepHits = new RaycastHit[64];
        private readonly Collider[] overlaps = new Collider[64];
        private CapsuleCollider bodyCollider;

        private void Awake()
        {
            enemy = GetComponent<EnemyBase>(); body = GetComponent<Rigidbody>();
            avoidance = GetComponent<EnemyObstacleAvoidance>();
            bodyCollider = GetComponent<CapsuleCollider>();
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
        }
        private void OnEnable()
        {
            if (!active.Contains(this)) active.Add(this);
            Target = null; age = 0f; recoilRemaining = 0f;
            decisionTimer = (GetInstanceID() & 15) * .012f;
            idleDirection = transform.forward;
        }
        private void Start()
        {
            if (enemy.Definition == null && definition != null) enemy.Init(definition, null);
            bounds = enemy.Director != null ? enemy.Director.arenaBounds : null;
            if (bounds == null)
                foreach (var candidate in FindObjectsByType<ArenaBoundsFromVectorGrid>(FindObjectsSortMode.None))
                    if (candidate.gameObject.scene == gameObject.scene) { bounds = candidate; break; }
            if (bounds != null)
            {
                idleDirection = bounds.Current.centerWS - transform.position; idleDirection.y = 0f;
                idleDirection = idleDirection.sqrMagnitude > .001f ? idleDirection.normalized : transform.forward;
            }
        }
        private void OnDisable() { active.Remove(this); Target = null; }

        private bool IsValidTarget(PlayerControllerScript p)
        {
            return p != null && p.isActiveAndEnabled && p.gameObject.scene == gameObject.scene && !p.IsPseudoPlayer
                && !p.temporarilyEliminated && !p.IsMatchInputLocked && p.massScore > p.massScoreMin
                && (enemy.OwnerTeamId < 0 || p.teamID != enemy.OwnerTeamId);
        }
        private static float DistanceSquared(Vector3 a, Vector3 b) { a.y = b.y; return (a - b).sqrMagnitude; }
        private void Decide()
        {
            float currentDistance = IsValidTarget(Target) ? DistanceSquared(Target.transform.position, body.position) : float.PositiveInfinity;
            if (currentDistance > Mathf.Pow(Mathf.Max(detectionRange, disengageRange), 2f)) Target = null;
            float best = Target != null ? currentDistance * retargetDistanceRatio * retargetDistanceRatio : detectionRange * detectionRange;
            foreach (var p in PlayerControllerScript.ActivePlayers)
            {
                if (!IsValidTarget(p)) continue;
                float distance = DistanceSquared(p.transform.position, body.position);
                if (distance < best && distance <= detectionRange * detectionRange) { best = distance; Target = p; }
            }
            separation = Vector3.zero;
            float radius2 = separationRadius * separationRadius;
            foreach (var other in active)
            {
                if (other == this || other == null || other.gameObject.scene != gameObject.scene || other.enemy == null || other.enemy.IsDead) continue;
                Vector3 away = body.position - other.transform.position; away.y = 0f;
                float d2 = away.sqrMagnitude;
                if (d2 > radius2) continue;
                if (d2 < .00001f) away = GetInstanceID() > other.GetInstanceID() ? Vector3.right : Vector3.left;
                else away *= (1f - Mathf.Sqrt(d2) / separationRadius) / Mathf.Sqrt(d2);
                separation += away;
            }
            separation = Vector3.ClampMagnitude(separation, 1f);
        }
        private void FixedUpdate()
        {
            if (enemy == null || enemy.IsDead || enemy.IsPaused || enemy.Definition == null || body.isKinematic) return;
            float dt = Time.fixedDeltaTime;
            age += dt;
            if (!IsReady) { body.linearVelocity = Vector3.zero; return; }
            decisionTimer -= dt;
            if (decisionTimer <= 0f) { decisionTimer = Mathf.Max(.05f, decisionInterval); Decide(); }
            if (Target != null && !IsValidTarget(Target)) Target = null;
            Vector3 velocity;
            if (recoilRemaining > 0f)
            {
                recoilRemaining -= dt;
                velocity = Vector3.MoveTowards(body.linearVelocity, Vector3.zero, acceleration * dt);
            }
            else
            {
                Vector3 desired = Target != null ? Target.transform.position - body.position : idleDirection;
                desired.y = 0f;
                desired = desired.normalized + separation * separationStrength;
                if (bounds != null)
                {
                    Vector3 inset = bounds.ClampWorldPointInside(body.position + desired.normalized * .8f, bodyRadius + .2f);
                    Vector3 correction = inset - (body.position + desired.normalized * .8f); correction.y = 0f;
                    desired += correction * 3f;
                }
                desired = avoidance.AdjustDirection(desired, out _);
                float speed = Target != null ? Mathf.Max(0f, enemy.Definition.moveSpeed) : idleSpeed;
                speed *= enemy.ExternalMovementMultiplier;
                velocity = Vector3.MoveTowards(body.linearVelocity, desired * speed, acceleration * dt);
            }
            velocity.y = 0f;
            ResolveObstacleOverlap();
            // A short swept guard prevents trigger-only bodies from crossing thin solid/no-go obstacles.
            for (int pass = 0; pass < 2; pass++)
            {
                float distanceToMove = velocity.magnitude * dt;
                if (distanceToMove <= .00001f) break;
                Capsule(out Vector3 a, out Vector3 b, out float r);
                int count = Physics.CapsuleCastNonAlloc(a, b, r, velocity.normalized, sweepHits,
                    distanceToMove + .04f, avoidance.ObstacleMask, QueryTriggerInteraction.Collide);
                float allowed = distanceToMove;
                Vector3 normal = Vector3.zero;
                for (int i = 0; i < count; i++)
                {
                    if (!avoidance.ShouldAvoid(sweepHits[i].collider)) continue;
                    float clearance = Mathf.Max(0f, sweepHits[i].distance - .04f);
                    if (clearance < allowed) { allowed = clearance; normal = sweepHits[i].normal; }
                }
                if (count == sweepHits.Length) { velocity = Vector3.zero; break; }
                if (allowed >= distanceToMove) break;
                normal.y = 0f; normal.Normalize();
                if (pass == 0 && normal.sqrMagnitude > .1f)
                {
                    // Keep safe tangential movement; scaling the entire velocity to zero pins trigger bodies at edges.
                    velocity -= normal * Mathf.Min(0f, Vector3.Dot(velocity, normal));
                    continue;
                }
                velocity *= allowed / distanceToMove;
            }
            if (bounds != null)
            {
                Vector3 clamped = bounds.ClampWorldPointInside(body.position + velocity * dt, bodyRadius);
                velocity = (clamped - body.position) / dt; velocity.y = 0f;
            }
            body.linearVelocity = velocity;
            if (velocity.sqrMagnitude > .001f)
                body.MoveRotation(Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(velocity, Vector3.up),
                    Mathf.Max(0f, enemy.Definition.turnSpeed) * dt));
        }

        private void Capsule(out Vector3 a, out Vector3 b, out float r)
        {
            float scale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
            r = Mathf.Max(bodyRadius, bodyCollider != null ? bodyCollider.radius : bodyRadius) * scale;
            Vector3 center = body.position + body.rotation * (bodyCollider != null ? Vector3.Scale(bodyCollider.center, transform.lossyScale) : Vector3.zero);
            float half = bodyCollider != null ? Mathf.Max(0f, bodyCollider.height * scale * .5f - r) : 0f;
            Vector3 axis = body.rotation * Vector3.forward;
            a = center + axis * half; b = center - axis * half;
        }

        private void ResolveObstacleOverlap()
        {
            if (bodyCollider == null) return;
            // Casts alone cannot detect an obstacle that moves into the trigger body or an initial overlap.
            Capsule(out Vector3 a, out Vector3 b, out float r);
            int count = Physics.OverlapCapsuleNonAlloc(a, b, r, overlaps, avoidance.ObstacleMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider c = overlaps[i]; if (!avoidance.ShouldAvoid(c)) continue;
                if (!Physics.ComputePenetration(bodyCollider, body.position, body.rotation, c, c.transform.position, c.transform.rotation,
                    out Vector3 direction, out float distance)) continue;
                direction.y = 0f;
                if (direction.sqrMagnitude > .001f) body.position += direction.normalized * (distance + .01f);
            }
        }

        private void OnTriggerEnter(Collider other) => ResolveContact(other);
        private void OnTriggerStay(Collider other) => ResolveContact(other);
        private void ResolveContact(Collider other)
        {
            if (!IsReady || other == null) return;
            // Sword collisions are owned exclusively by EnemyHurtbox, including scoring attribution.
            if (other.GetComponentInParent<PlayerMelee>() != null) return;
            var player = other.GetComponentInParent<PlayerControllerScript>();
            if (!IsValidTarget(player) || player.IsInvulnerable) return;
            if (player.shieldOn)
            {
                if (recoilRemaining > 0f) return;
                Vector3 away = body.position - player.transform.position; away.y = 0f;
                if (away.sqrMagnitude < .001f) away = -transform.forward;
                body.linearVelocity = away.normalized * shieldRecoilSpeed;
                recoilRemaining = shieldRecoverySeconds;
                return;
            }
            if (player.attackController != null && player.attackController.IsAttacking) return;
            float damage = Mathf.Max(0f, enemy.Definition.damageToPlayerMass01);
            if (damage <= 0f) return;
            var result = player.ApplyExternalMassDelta(-damage, gameObject, allowDeath: true, disruptsScoreChain: true);
            if (!result.accepted) return;
            enemy.PlayAttackSfx();
            enemy.Kill(EnemyDamageSource.Environmental);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(.3f, .8f, 1f, .5f); Gizmos.DrawWireSphere(transform.position, detectionRange);
            Gizmos.color = new Color(1f, 1f, 1f, .25f); Gizmos.DrawWireSphere(transform.position, disengageRange);
        }
    }
}
