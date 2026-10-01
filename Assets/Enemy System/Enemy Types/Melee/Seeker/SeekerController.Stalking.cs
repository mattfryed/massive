using UnityEngine;

namespace Massive.Enemies
{
    public sealed partial class SeekerController
    {
        private void TickStalking(float dt)
        {
            if (PhaseAge >= Mathf.Max(0f, stalkSeconds)) { Enter(AttackPhase.Seeking); return; }
            decisionAge -= dt;
            if (decisionAge <= 0f) { decisionAge = Mathf.Max(.05f, decisionInterval); Acquire(); }
            TickAvoidance(dt);
            Vector3 toTarget = Eligible(Target) ? Flat(Target.transform.position - body.position) : Vector3.zero;
            Vector3 desired = Vector3.zero;
            if (toTarget.sqrMagnitude > .0001f)
            {
                float distance = toTarget.magnitude;
                Vector3 radial = toTarget / distance, tangent = Vector3.Cross(Vector3.up, radial) * orbitSign;
                float min = Mathf.Max(.5f, Mathf.Min(stalkDistanceRange.x, stalkDistanceRange.y));
                float max = Mathf.Max(min + .1f, Mathf.Max(stalkDistanceRange.x, stalkDistanceRange.y));
                float desiredDistance = (min + max) * .5f;
                float radialSpeed = Mathf.Clamp((distance - desiredDistance) * 2f, -stalkRadialSpeed, stalkRadialSpeed);
                float strafe = Mathf.Sin(PhaseAge * stalkStrafeFrequency * Mathf.PI * 2f) * stalkStrafeSpeed;
                desired = radial * radialSpeed + tangent * (stalkOrbitSpeed + strafe);
                Turn(toTarget, enemy.Definition.turnSpeed, dt);
            }
            if (IsStrafing) desired = dodgeDirection * strafeSpeed;
            if (desired.sqrMagnitude > .0001f)
            {
                float speed = desired.magnitude;
                Vector3 direction = avoidance.AdjustDirection(desired / speed, out _);
                if (arenaBounds && arenaBounds.IsValid)
                {
                    Vector3 ahead = body.position + direction * 1.2f;
                    Vector3 correction = Flat(arenaBounds.ClampWorldPointInside(ahead, Clearance) - ahead);
                    direction = (direction + correction * 2f).normalized;
                }
                desired = direction * speed;
            }
            velocity = Vector3.MoveTowards(velocity, desired * enemy.ExternalMovementMultiplier, acceleration * dt);
            MoveSafely(velocity * dt, false);
        }
    }
}
