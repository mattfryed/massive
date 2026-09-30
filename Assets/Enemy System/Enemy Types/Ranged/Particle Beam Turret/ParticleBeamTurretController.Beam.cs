using UnityEngine;
using Massive.Player;

namespace Massive.Enemies
{
    public sealed partial class ParticleBeamTurretController
    {
        private readonly RaycastHit[] hits = new RaycastHit[64];
        private readonly Collider[] overlaps = new Collider[32];
        private Vector3 tracePoint, traceNormal;
        private float retractDistance;
        private bool CanBlock(Collider c)
        {
            if (!c || !c.enabled || c.transform.IsChildOf(transform)) return false;
            var p = c.GetComponentInParent<PlayerControllerScript>();
            if (p)
            {
                if (!Eligible(p)) return false;
                if (c.CompareTag("Shield")) return p.shieldOn;
                if (c.GetComponentInParent<PlayerMelee>()) return false;
                return c.CompareTag("Player") && !p.shieldOn;
            }
            if (c.GetComponentInParent<EnemyProjectileBase>() || c.GetComponentInParent<Massive.PowerUps.ParticleAcceleratorProjectile>()) return false;
            if (c.GetComponent<VectorGridGPU>() || c.bounds.max.y < BeamOrigin.y - .05f) return false;
            return !c.isTrigger;
        }
        private float ArenaDistance(Vector3 direction, float limit)
        {
            if (!arenaBounds || !arenaBounds.IsValid) return limit;
            Transform grid = arenaBounds.Grid.transform;
            Vector3 p = grid.InverseTransformPoint(BeamOrigin), d = grid.InverseTransformVector(direction);
            Vector2 half = arenaBounds.Current.halfSizeLocal;
            if (Mathf.Abs(d.x) > .00001f) limit = Mathf.Min(limit, ((d.x > 0f ? half.x : -half.x) - p.x) / d.x);
            if (Mathf.Abs(d.y) > .00001f) limit = Mathf.Min(limit, ((d.y > 0f ? half.y : -half.y) - p.y) / d.y);
            return Mathf.Max(0f, limit);
        }
        private float Trace(Vector3 direction, float limit, float radius, out Collider contact)
        {
            contact = null; float nearest = ArenaDistance(direction, limit);
            tracePoint = BeamOrigin + direction * nearest; traceNormal = -direction;
            int count = Physics.OverlapSphereNonAlloc(BeamOrigin, Mathf.Max(.001f, radius), overlaps, collisionMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++) if (CanBlock(overlaps[i])) { contact = overlaps[i]; return 0f; }
            if (count == overlaps.Length) return 0f;
            count = Physics.SphereCastNonAlloc(BeamOrigin, Mathf.Max(.001f, radius), direction, hits, nearest, collisionMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
                if (hits[i].distance < nearest && CanBlock(hits[i].collider))
                { nearest = hits[i].distance; contact = hits[i].collider; tracePoint = hits[i].point; traceNormal = hits[i].normal; }
            // A full query cannot prove which obstacle is nearest. Never damage through an omitted wall.
            if (count == hits.Length) { contact = null; return 0f; }
            return nearest;
        }
        private bool CanSee(PlayerControllerScript p)
        {
            Vector3 d = p.transform.position - BeamOrigin; d.y = 0f;
            if (d.sqrMagnitude < .0001f) return true;
            Trace(d.normalized, d.magnitude, .015f, out var contact);
            return !contact || contact.GetComponentInParent<PlayerControllerScript>() == p;
        }
        private void UpdateAimEndpoint()
        { BeamLength = Trace(BeamDirection, beamRange, beamRadius, out _); BeamEnd = BeamOrigin + BeamDirection * BeamLength; }
        private void FireBeam(float dt)
        {
            float rise = Mathf.SmoothStep(0f, 1f, (PhaseAge + dt) / Mathf.Max(.05f, beamGrowSeconds));
            float fade = Mathf.SmoothStep(0f, 1f, (fireSeconds - PhaseAge) / Mathf.Max(.05f, beamFadeSeconds));
            BeamEnvelope = rise * fade;
            float opening = Mathf.Lerp(openingRadiusMultiplier, 1f, Mathf.SmoothStep(0f, 1f, PhaseAge / Mathf.Max(.05f, openingSettleSeconds)));
            float radius = beamRadius * BeamEnvelope * opening;
            float fullLength = Trace(BeamDirection, beamRange * rise, radius, out var contact);
            bool touching = contact || fullLength + .001f < beamRange * rise;
            if (fade >= .999f) retractDistance = fullLength;
            // Stop damage/emission as the tip leaves the surface. Never damage along an invisible remainder.
            BeamLength = fade >= .999f ? fullLength : Mathf.Min(fullLength, retractDistance) * fade;
            BeamEnd = BeamOrigin + BeamDirection * BeamLength;
            if (contact && fade >= .999f)
            {
                var player = contact.GetComponentInParent<PlayerControllerScript>();
                if (player)
                {
                    float amount = Mathf.Max(0f, damagePerSecond) * dt * BeamEnvelope;
                    if (player.shieldOn)
                    {
                        var shield = player.GetComponent<PlayerShieldAbility>();
                        amount *= shield && shield.IsActive && !shield.BlocksAllDamage ? 1f - shield.CurrentStrength01 : 0f;
                    }
                    if (amount > 0f) player.ApplyExternalMassDelta(-amount, gameObject, allowDeath: true);
                    if (!player.shieldOn && player.visualsController)
                    {
                        Vector3 edge = player.visualsController.OutlinePointTowards(BeamOrigin);
                        float distance = Vector3.Dot(edge - BeamOrigin, BeamDirection);
                        BeamLength = Mathf.Clamp(distance - radius * .5f, 0f, beamRange);
                        BeamEnd = BeamOrigin + BeamDirection * BeamLength;
                        tracePoint = BeamOrigin + BeamDirection * distance;
                    }
                }
            }
            if (beamVisual)
            {
                beamVisual.gameObject.SetActive(true);
                beamVisual.SetSegment(BeamOrigin, BeamEnd, radius, 1f);
                beamVisual.SetAnimationTime(PhaseAge);
            }
            if (contactPlasma)
            {
                if (touching && fade >= .999f && fullLength > .01f) contactPlasma.SetContact(tracePoint, traceNormal, BeamEnvelope);
                else contactPlasma.StopEmission();
            }
            if (corePlasma) corePlasma.SetContact(BeamOrigin + BeamDirection * corePlasmaForwardOffset, BeamDirection, BeamEnvelope);
        }
        private void HideBeam(bool clearContact = false)
        {
            BeamEnvelope = BeamLength = retractDistance = 0f;
            if (beamVisual) beamVisual.gameObject.SetActive(false);
            if (contactPlasma) { if (clearContact) contactPlasma.Clear(); else contactPlasma.StopEmission(); }
            if (corePlasma) { if (clearContact) corePlasma.Clear(); else corePlasma.StopEmission(); }
        }
    }
}
