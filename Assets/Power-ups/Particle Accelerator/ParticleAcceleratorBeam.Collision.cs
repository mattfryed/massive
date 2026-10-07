using UnityEngine;
using Massive.Enemies;
using Massive.Player;

namespace Massive.PowerUps
{
    public sealed partial class ParticleAcceleratorBeam
    {
        readonly RaycastHit[] hits = new RaycastHit[64];
        readonly Collider[] overlaps = new Collider[32];
        Vector3 tracePoint, traceNormal;
        bool CanBlock(Collider c) => CanBlock(c, origin);
        bool CanBlock(Collider c, Vector3 sampleOrigin)
        {
            if (!c || !c.enabled || c.transform.IsChildOf(transform) || c.GetComponentInParent<MatterNuggetScript>()) return false;
            var player = c.GetComponentInParent<PlayerControllerScript>();
            if (player)
            {
                if (player == owner || !owner.SharesSimulationWith(player) || player.temporarilyEliminated) return false;
                if (c.CompareTag("Shield")) return player.shieldOn;
                if (c.GetComponentInParent<PlayerMelee>()) return false;
                return c.CompareTag("Player") && !player.shieldOn;
            }
            var enemy = c.GetComponentInParent<EnemyBase>();
            if (enemy) return !enemy.IsDead && enemy.SharesSimulationWith(owner) && (!c.isTrigger || c.GetComponent<EnemyHurtbox>());
            if (c.GetComponentInParent<EnemyProjectileBase>() || c.GetComponentInParent<ParticleAcceleratorProjectile>()) return false;
            if (c.GetComponent<VectorGridGPU>() || c.bounds.max.y < sampleOrigin.y - .05f) return false;
            return !c.isTrigger;
        }

        // Idle aim is straight because each new shot starts with a fresh aim history.
        // Reuse the beam's target filtering without changing a still-retracting path.
        float TraceAimGuide(Vector3 position, Vector3 aim)
        {
            float width = Mathf.Max(.001f, definition.beamRadius * PlayerScaleAdjuster.SizeOf(owner));
            float limit = Mathf.Max(0, definition.maxDistance) * PlayerScaleAdjuster.ProjectileReachOf(owner);
            Vector3 body = owner.transform.position; body.y = position.y;
            Vector3 muzzle = position - body;
            int count = Physics.SphereCastNonAlloc(body, width, muzzle.normalized, hits, muzzle.magnitude,
                definition.beamCollisionMask, QueryTriggerInteraction.Collide);
            if (count == hits.Length) return 0;
            for (int i = 0; i < count; i++) if (CanBlock(hits[i].collider, position)) return 0;
            count = Physics.OverlapSphereNonAlloc(position, width, overlaps, definition.beamCollisionMask, QueryTriggerInteraction.Collide);
            if (count == overlaps.Length) return 0;
            for (int i = 0; i < count; i++) if (CanBlock(overlaps[i], position)) return 0;
            count = Physics.SphereCastNonAlloc(position, width, aim, hits, limit,
                definition.beamCollisionMask, QueryTriggerInteraction.Collide);
            if (count == hits.Length) return 0;
            for (int i = 0; i < count; i++)
                if (hits[i].distance < limit && CanBlock(hits[i].collider, position)) limit = hits[i].distance;
            if (arena && arena.IsValid && !arena.ContainsWorldPoint(position + aim * limit, width))
            {
                float low = 0, high = limit;
                for (int i = 0; i < 12; i++)
                {
                    float mid = (low + high) * .5f;
                    if (arena.ContainsWorldPoint(position + aim * mid, width)) low = mid; else high = mid;
                }
                limit = low;
            }
            return limit;
        }

        void Sample(float distance, out Vector3 center, out float width) => beamVisual.SampleCollisionPath(
            origin, direction, referenceLength, distance, radius, age, out center, out width);

        // Same swept animated bundle envelope as the turret, sampled along the delayed aim path.
        float Trace(float limit, out Collider contact)
        {
            contact = null;
            Sample(0, out var previous, out float previousRadius);
            tracePoint = previous; traceNormal = -direction;
            if (limit <= .02f || radius <= .00001f) return 0;
            // A muzzle offset must never put the start of a shot through a nearby wall.
            Vector3 body = owner.transform.position; body.y = origin.y;
            Vector3 muzzle = origin - body;
            int muzzleCount = Physics.SphereCastNonAlloc(body, Mathf.Max(.001f, radius), muzzle.normalized,
                hits, muzzle.magnitude, definition.beamCollisionMask, QueryTriggerInteraction.Collide);
            if (muzzleCount == hits.Length) return 0;
            float muzzleNearest = float.PositiveInfinity;
            for (int i = 0; i < muzzleCount; i++)
                if (hits[i].distance < muzzleNearest && CanBlock(hits[i].collider))
                { muzzleNearest = hits[i].distance; contact = hits[i].collider; tracePoint = hits[i].point; traceNormal = hits[i].normal; }
            if (contact) return 0;
            int steps = Mathf.Clamp(Mathf.CeilToInt(limit / .12f), 2, 256);
            float step = limit / steps;
            for (int n = 1; n <= steps; n++)
            {
                Sample(n * step, out var next, out float nextRadius);
                float width = Mathf.Max(.001f, Mathf.Max(previousRadius, nextRadius));
                Vector3 segment = next - previous; float length = segment.magnitude, nearest = length;
                int count = Physics.OverlapSphereNonAlloc(previous, width, overlaps, definition.beamCollisionMask, QueryTriggerInteraction.Collide);
                if (count == overlaps.Length) return (n - 1) * step;
                for (int i = 0; i < count; i++)
                    if (CanBlock(overlaps[i]))
                    { contact = overlaps[i]; tracePoint = contact.ClosestPoint(previous); traceNormal = -segment.normalized; return (n - 1) * step; }
                count = Physics.SphereCastNonAlloc(previous, width, segment.normalized, hits, length, definition.beamCollisionMask, QueryTriggerInteraction.Collide);
                if (count == hits.Length) return (n - 1) * step;
                for (int i = 0; i < count; i++)
                    if (hits[i].distance < nearest && CanBlock(hits[i].collider))
                    { nearest = hits[i].distance; contact = hits[i].collider; tracePoint = hits[i].point; traceNormal = hits[i].normal; }
                if (arena && arena.IsValid && !arena.ContainsWorldPoint(next, width))
                {
                    float low = 0, high = 1;
                    for (int i = 0; i < 10; i++)
                    {
                        float mid = (low + high) * .5f;
                        if (arena.ContainsWorldPoint(Vector3.Lerp(previous, next, mid), width)) low = mid; else high = mid;
                    }
                    if (low * length < nearest)
                    {
                        nearest = low * length; contact = null; tracePoint = Vector3.Lerp(previous, next, low);
                        traceNormal = (arena.ClampWorldPointInside(next, width) - next).normalized;
                    }
                }
                if (nearest < length) return (n - 1 + nearest / Mathf.Max(.00001f, length)) * step;
                previous = next; previousRadius = nextRadius;
            }
            tracePoint = previous; traceNormal = -direction;
            return limit;
        }
    }
}
