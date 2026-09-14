using UnityEngine;

namespace Massive.Enemies
{
    public sealed partial class DroneController
    {
        [Header("Idle swarm")]
        [Min(.5f)] public float swarmNeighborRadius = 3.5f;
        [Min(0f)] public float swarmCohesion = 1.2f;
        [Min(0f)] public float swarmAlignment = .3f;
        [Min(0f)] public float swarmCircling = .9f;
        [Min(0f)] public float swarmWander = .45f;
        [Tooltip("Soft roaming radius around the spawn location or the point where pursuit ended.")]
        [Min(1f)] public float swarmRoamRadius = 4f;

        private Vector3 idleAnchor;
        private float swarmPhase;

        private void ResetIdleSwarm()
        {
            idleAnchor = transform.position;
            // Distinct smooth paths without changing the gameplay random stream.
            swarmPhase = (uint)GetInstanceID() % 997 * (Mathf.PI * 2f / 997f);
            idleDirection = new Vector3(Mathf.Cos(swarmPhase), 0f, Mathf.Sin(swarmPhase));
            separation = Vector3.zero;
        }

        private void UpdateIdleSwarm(Vector3 center, Vector3 alignment, int neighbors)
        {
            Vector3 inward = center - body.position; inward.y = 0f;
            float phase = swarmPhase + age * .65f + Mathf.Sin(age * .27f + swarmPhase) * .9f;
            Vector3 wander = new Vector3(Mathf.Cos(phase), 0f, Mathf.Sin(phase));
            Vector3 radial = inward.sqrMagnitude > .04f ? -inward.normalized : wander;
            Vector3 circle = Vector3.Cross(Vector3.up, radial);
            Vector3 cohesion = Vector3.ClampMagnitude(inward / Mathf.Max(.5f, swarmNeighborRadius), 1f);
            alignment.y = 0f;
            Vector3 direction = cohesion * swarmCohesion + circle * swarmCircling + wander * swarmWander;
            if (neighbors > 0 && alignment.sqrMagnitude > .01f) direction += alignment.normalized * swarmAlignment;
            Vector3 home = idleAnchor - body.position; home.y = 0f;
            float leash = Mathf.InverseLerp(swarmRoamRadius * .5f, Mathf.Max(1f, swarmRoamRadius), home.magnitude);
            direction += home.normalized * (leash * 2.5f);
            idleDirection = direction.sqrMagnitude > .001f ? direction.normalized : wander;
        }
    }
}
