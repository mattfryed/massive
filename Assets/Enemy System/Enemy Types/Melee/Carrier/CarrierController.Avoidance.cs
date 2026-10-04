using UnityEngine;

namespace Massive.Enemies
{
    public sealed partial class CarrierController
    {
        [Header("Soft turret lane avoidance")]
        public bool avoidTurretLanes = true;
        [Min(.05f)] public float turretAvoidanceSpeed = .8f;
        [Min(.05f)] public float turretAvoidanceEaseSeconds = .35f;
        [Min(0f), Tooltip("Extra room beyond the shell radius and turret beam radius.")]
        public float turretLanePadding = .25f;
        [Min(.02f), Tooltip("Finish slightly beyond the clear lane so small aim changes do not cause jitter.")]
        public float turretLaneSettleMargin = .15f;
        [Min(.05f)] public float turretLaneCheckInterval = .3f;
        public LayerMask turretAvoidanceObstacleMask = ~0;

        public bool IsClearingTurretLane { get; private set; }
        public Vector3 TurretAvoidanceDestination => avoidanceDestination;
        private Rigidbody avoidanceBody;
        private ParticleBeamTurretController avoidedTurret;
        private EnemyBase avoidedTurretEnemy;
        private Vector3 avoidanceDestination, avoidanceVelocity;
        private Vector3 arrivalYieldVelocity;
        private float laneCheckRemaining;
        private readonly RaycastHit[] avoidanceHits = new RaycastHit[64];
        private readonly Collider[] avoidanceOverlaps = new Collider[64];

        private void ResetLaneAvoidance()
        {
            avoidanceBody = GetComponent<Rigidbody>();
            IsClearingTurretLane = false; avoidedTurret = null; avoidedTurretEnemy = null;
            avoidanceVelocity = Vector3.zero; laneCheckRemaining = 0f;
            arrivalYieldVelocity = Vector3.zero;
        }
        private float AvoidanceRadius
        {
            get
            {
                if (!shellCollider) return enemy.Definition ? enemy.Definition.spawnRadiusWorld : 1f;
                var size = shellCollider.bounds.extents;
                // Circumscribe the solid shell so its rotating corners also clear the lane.
                return Mathf.Max(.1f, new Vector2(size.x, size.z).magnitude);
            }
        }
        private void FixedUpdate()
        {
            if (!enemy || enemy.IsDead || !enemy.Definition || !IsSpawnReady || !avoidanceBody) return;
            if (enemy.IsPaused) { avoidanceVelocity = arrivalYieldVelocity = Vector3.zero; return; }
            if (enemy.HoldPosition)
            { ResetLaneAvoidance(); return; }
            float dt = Time.fixedDeltaTime;
            if (!avoidTurretLanes)
            {
                IsClearingTurretLane = false; avoidedTurret = null; avoidedTurretEnemy = null;
                avoidanceVelocity = Vector3.zero; laneCheckRemaining = 0f;
                YieldToArrival(dt); return;
            }
            laneCheckRemaining -= dt;
            if (IsClearingTurretLane && (!avoidedTurret || !avoidedTurret.isActiveAndEnabled || !avoidedTurretEnemy ||
                avoidedTurretEnemy.IsDead || avoidedTurretEnemy.IsPaused || !enemy.SharesSimulationWith(avoidedTurretEnemy)))
            { IsClearingTurretLane = false; avoidanceVelocity = Vector3.zero; }
            if (!IsClearingTurretLane && laneCheckRemaining <= 0f)
            {
                laneCheckRemaining = Mathf.Max(.05f, turretLaneCheckInterval);
                FindTurretSidestep();
            }
            if (!IsClearingTurretLane) { YieldToArrival(dt); return; }
            arrivalYieldVelocity = Vector3.zero;
            Vector3 next = Vector3.SmoothDamp(avoidanceBody.position, avoidanceDestination, ref avoidanceVelocity,
                Mathf.Max(.05f, turretAvoidanceEaseSeconds), Mathf.Max(.05f, turretAvoidanceSpeed) * enemy.ExternalMovementMultiplier, dt);
            next.y = avoidanceBody.position.y;
            // Recheck each physics step: an actor may enter a route after it was chosen.
            if (!ClearSidestep(next, AvoidanceRadius))
            { IsClearingTurretLane = false; avoidanceVelocity = Vector3.zero; laneCheckRemaining = turretLaneCheckInterval; return; }
            avoidanceBody.MovePosition(next);
            if ((next - avoidanceDestination).sqrMagnitude < .0004f)
            {
                IsClearingTurretLane = false; avoidanceVelocity = Vector3.zero;
                laneCheckRemaining = Mathf.Max(.1f, turretLaneCheckInterval);
            }
        }
        private void YieldToArrival(float dt)
        {
            Vector3 bias = enemy.Director ? enemy.Director.ArrivalAvoidance(enemy, Vector3.zero) : Vector3.zero;
            if (bias.sqrMagnitude < .0001f) { arrivalYieldVelocity = Vector3.zero; return; }
            Vector3 destination = avoidanceBody.position + Vector3.ClampMagnitude(bias, 1f);
            Vector3 next = Vector3.SmoothDamp(avoidanceBody.position, destination, ref arrivalYieldVelocity,
                Mathf.Max(.05f, turretAvoidanceEaseSeconds), Mathf.Max(.05f, turretAvoidanceSpeed) * enemy.ExternalMovementMultiplier, dt);
            if (ClearSidestep(next, AvoidanceRadius)) avoidanceBody.MovePosition(next);
            else arrivalYieldVelocity = Vector3.zero;
        }
        private void FindTurretSidestep()
        {
            float bestDistance = float.PositiveInfinity;
            float radius = AvoidanceRadius;
            foreach (var other in EnemyBase.ActiveEnemies)
            {
                if (!other || other == enemy || other.gameObject.scene != gameObject.scene || !enemy.SharesSimulationWith(other) ||
                    other.IsDead || other.IsPaused || enemy.OwnerTeamId != other.OwnerTeamId ||
                    !other.TryGetComponent<ParticleBeamTurretController>(out var turret)) continue;
                if (!turret.TryGetObstructedLane(enemy, out var origin, out var end)) continue;
                Vector3 direction = end - origin; direction.y = 0f;
                if (direction.sqrMagnitude < .001f) continue;
                direction.Normalize(); Vector3 side = Vector3.Cross(Vector3.up, direction);
                Vector3 center = shellCollider ? shellCollider.bounds.center : avoidanceBody.position;
                float lateral = Vector3.Dot(center - origin, side);
                float clearance = radius + Mathf.Max(.015f, turret.beamRadius) + Mathf.Max(0f, turretLanePadding) + Mathf.Max(.02f, turretLaneSettleMargin);
                // Test both exits. An edge or obstruction can make the farther side the only safe one.
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    var destination = avoidanceBody.position + side * (sign * clearance - lateral);
                    destination.y = avoidanceBody.position.y;
                    float distance = (destination - avoidanceBody.position).sqrMagnitude;
                    if (distance >= bestDistance || !ClearSidestep(destination, radius)) continue;
                    bestDistance = distance; avoidanceDestination = destination;
                    avoidedTurret = turret; avoidedTurretEnemy = other; IsClearingTurretLane = true;
                }
            }
        }
        private bool BlocksSidestep(Collider collider, Vector3 center, float radius)
        {
            if (!collider || !collider.enabled || collider.isTrigger || collider.transform.IsChildOf(transform)) return false;
            if (collider.GetComponentInParent<MatterNuggetScript>()) return false;
            if (collider.GetComponent<VectorGridGPU>() || collider.bounds.max.y < center.y - radius + .02f) return false;
            if (collider.GetComponentInParent<EnemyProjectileBase>()) return false;
            var other = collider.GetComponentInParent<EnemyBase>();
            if (other && (other.IsDead || !enemy.SharesSimulationWith(other))) return false;
            var player = collider.GetComponentInParent<PlayerControllerScript>();
            return !player || enemy.SharesSimulationWith(player);
        }
        private bool ClearSidestep(Vector3 destination, float radius)
        {
            const float skin = .035f;
            if (!bounds || !bounds.IsValid || !bounds.ContainsWorldPoint(destination, radius + skin)) return false;
            Vector3 start = shellCollider ? shellCollider.bounds.center : avoidanceBody.position;
            Vector3 delta = destination - avoidanceBody.position;
            // The explicit layout exclusions can include non-solid volumes.
            if (enemy.Director && enemy.Director.arenaLayout)
                foreach (var exclusion in enemy.Director.arenaLayout.exclusions)
                    if (exclusion && exclusion.enabled && exclusion.gameObject.activeInHierarchy &&
                        (exclusion.ClosestPoint(start + delta) - start - delta).sqrMagnitude < Mathf.Pow(radius + skin, 2)) return false;
            int count = Physics.OverlapSphereNonAlloc(start + delta, radius + skin, avoidanceOverlaps,
                turretAvoidanceObstacleMask, QueryTriggerInteraction.Ignore);
            if (count == avoidanceOverlaps.Length) return false;
            for (int i = 0; i < count; i++) if (BlocksSidestep(avoidanceOverlaps[i], start, radius)) return false;
            if (delta.sqrMagnitude < .000001f) return true;
            count = Physics.SphereCastNonAlloc(start, radius, delta.normalized, avoidanceHits,
                delta.magnitude + skin, turretAvoidanceObstacleMask, QueryTriggerInteraction.Ignore);
            if (count == avoidanceHits.Length) return false;
            for (int i = 0; i < count; i++) if (BlocksSidestep(avoidanceHits[i].collider, start, radius)) return false;
            return true;
        }
    }
}
