using UnityEngine;

namespace Massive.Enemies
{
    public sealed partial class SeekerController
    {
        private readonly RaycastHit[] hits = new RaycastHit[64];
        private readonly Collider[] overlaps = new Collider[64];
        private const float Skin = .005f;
        private float Radius => shellCollider ? shellCollider.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y)) : .27f;
        private float HalfSegment => shellCollider ? Mathf.Max(0f, shellCollider.height * Mathf.Abs(transform.lossyScale.z) * .5f - Radius) : .38f;
        private float Clearance => Radius + HalfSegment;
        private void Capsule(out Vector3 front, out Vector3 back)
        {
            Vector3 center = body.position + (shellCollider ? body.rotation * Vector3.Scale(shellCollider.center, transform.lossyScale) : Vector3.zero);
            Vector3 forward = body.rotation * Vector3.forward;
            front = center + forward * HalfSegment; back = center - forward * HalfSegment;
        }
        private bool CanSee(PlayerControllerScript p)
        {
            if (!p) return false;
            Vector3 direction = Flat(p.transform.position - body.position); float length = direction.magnitude;
            if (length < .001f) return true;
            int count = Physics.RaycastNonAlloc(body.position, direction / length, hits, length, avoidance.ObstacleMask, QueryTriggerInteraction.Collide);
            if (count == hits.Length) return false;
            for (int i = 0; i < count; i++) if (avoidance.ShouldAvoid(hits[i].collider)) return false;
            return true;
        }
        private bool IsContact(Collider other, bool lunging, out PlayerControllerScript player)
        {
            player = null;
            if (!other || !other.enabled || other.transform.IsChildOf(transform)) return false;
            var candidate = other.GetComponentInParent<PlayerControllerScript>();
            if (candidate)
            {
                if (!lunging || !Eligible(candidate) || other.GetComponentInParent<PlayerMelee>()) return false;
                if (other.CompareTag("Shield") ? !candidate.shieldOn : !other.CompareTag("Player")) return false;
                player = candidate; return true;
            }
            return avoidance.ShouldAvoid(other);
        }
        // Both initial overlaps and a full swept hull are checked: a fast lunge cannot skip a player or thin wall.
        private float MoveSafely(Vector3 delta, bool lunging)
        {
            float distance = delta.magnitude; if (distance < .000001f) return 0f;
            Vector3 direction = delta / distance;
            Capsule(out var front, out var back);
            Collider contact = null; PlayerControllerScript player = null;
            float nearest = distance;
            int count = Physics.OverlapCapsuleNonAlloc(front, back, Radius, overlaps, ~0, QueryTriggerInteraction.Collide);
            if (count == overlaps.Length) { if (lunging) Enter(AttackPhase.Hit); return 0f; }
            for (int i = 0; i < count; i++)
            {
                if (!IsContact(overlaps[i], lunging, out var p)) continue;
                if (p) { contact = overlaps[i]; player = p; nearest = 0f; break; }
                if (shellCollider && Physics.ComputePenetration(shellCollider, body.position, body.rotation, overlaps[i], overlaps[i].transform.position,
                    overlaps[i].transform.rotation, out var away, out var depth))
                {
                    Vector3 correction = Flat(away) * (depth + Skin);
                    body.position += correction; front += correction; back += correction;
                    if (lunging) { contact = overlaps[i]; nearest = 0f; break; }
                }
            }
            if (!contact)
            {
                count = Physics.CapsuleCastNonAlloc(front, back, Radius, direction, hits, distance + Skin, ~0, QueryTriggerInteraction.Collide);
                if (count == hits.Length) { if (lunging) Enter(AttackPhase.Hit); return 0f; }
                for (int i = 0; i < count; i++)
                {
                    if (!IsContact(hits[i].collider, lunging, out var p) || hits[i].distance > nearest + Skin) continue;
                    nearest = Mathf.Max(0f, hits[i].distance - Skin); contact = hits[i].collider; player = p;
                }
            }
            Vector3 next = body.position + direction * nearest;
            if (arenaBounds && arenaBounds.IsValid)
            {
                Vector3 clamped = arenaBounds.ClampWorldPointInside(next, Clearance); clamped.y = body.position.y;
                if ((next - clamped).sqrMagnitude > .000001f && lunging) Enter(AttackPhase.Hit);
                next = clamped;
            }
            float moved = Vector3.Distance(body.position, next); body.position = next;
            if (contact)
            {
                velocity = Vector3.zero;
                if (lunging)
                {
                    // Resolve before any further physics callback: at most one player hit per lunge, including shield contacts.
                    Enter(AttackPhase.Hit);
                    if (player && !player.shieldOn && !player.IsInvulnerable
                        && player.ApplyExternalMassDelta(-enemy.Definition.damageToPlayerMass01, gameObject, true, true).accepted)
                    { TotalHits++; LastAttackHitPlayer = true; }
                }
            }
            return moved;
        }
    }
}
