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
        [Tooltip("Seconds to ease toward a new idle steering direction.")]
        [Min(.05f)] public float swarmSteeringSmoothTime = .45f;
        [Tooltip("Maximum idle heading speed, in degrees per second.")]
        [Min(1f)] public float swarmTurnSpeed = 100f;
        [Tooltip("Maximum change of heading speed, in degrees per second squared.")]
        [Min(1f)] public float swarmTurnAcceleration = 240f;

        private Vector3 idleAnchor, smoothIdleDirection, idleSteeringVelocity;
        private float swarmPhase, idleYawSpeed;

        private void ResetIdleSwarm()
        {
            idleAnchor = transform.position;
            // Distinct smooth paths without changing the gameplay random stream.
            swarmPhase = (uint)GetInstanceID() % 997 * (Mathf.PI * 2f / 997f);
            idleDirection = new Vector3(Mathf.Cos(swarmPhase), 0f, Mathf.Sin(swarmPhase));
            smoothIdleDirection = transform.forward; smoothIdleDirection.y = 0f;
            idleSteeringVelocity = Vector3.zero; idleYawSpeed = 0f;
            separation = Vector3.zero;
        }

        private void UpdateIdleSwarm(Vector3 center, Vector3 alignment, int neighbors)
        {
            Vector3 inward = center - body.position; inward.y = 0f;
            float phase = swarmPhase + age * .65f + Mathf.Sin(age * .27f + swarmPhase) * .9f;
            Vector3 wander = new Vector3(Mathf.Cos(phase), 0f, Mathf.Sin(phase));
            // Fade the orbit force near its center instead of flipping to a different radial axis.
            Vector3 radial = -inward / Mathf.Max(.4f, inward.magnitude);
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

        private Vector3 SmoothIdleSteering(Vector3 desired, float dt)
        {
            smoothIdleDirection = Vector3.SmoothDamp(smoothIdleDirection, desired, ref idleSteeringVelocity,
                Mathf.Max(.05f, swarmSteeringSmoothTime), Mathf.Infinity, dt);
            return smoothIdleDirection; // Preserve speed through a reversal; normalizing would create a snap.
        }

        private void TurnSmoothly(Vector3 facing, float dt)
        {
            float limit = Mathf.Max(0f, enemy.Definition.turnSpeed);
            if (Target != null || launchRemaining > 0f || recoilRemaining > 0f)
            {
                smoothIdleDirection = body.linearVelocity / Mathf.Max(.01f, idleSpeed);
                idleSteeringVelocity = Vector3.zero;
                if (facing.sqrMagnitude <= .001f) return;
                Quaternion next = Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(facing, Vector3.up), limit * dt);
                idleYawSpeed = Mathf.DeltaAngle(body.rotation.eulerAngles.y, next.eulerAngles.y) / dt;
                body.MoveRotation(next); return;
            }
            float error = facing.sqrMagnitude > .001f
                ? Mathf.DeltaAngle(body.rotation.eulerAngles.y, Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg) : 0f;
            float maxSpeed = Mathf.Min(limit, swarmTurnSpeed);
            float desiredSpeed = Mathf.Clamp(error / Mathf.Max(.05f, swarmSteeringSmoothTime), -maxSpeed, maxSpeed);
            idleYawSpeed = Mathf.MoveTowards(idleYawSpeed, desiredSpeed, swarmTurnAcceleration * dt);
            body.MoveRotation(Quaternion.AngleAxis(idleYawSpeed * dt, Vector3.up) * body.rotation);
        }
    }
}
