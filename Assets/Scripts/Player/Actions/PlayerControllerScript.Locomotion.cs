using UnityEngine;

public partial class PlayerControllerScript
{
    // Both continuous physics and LATTICE's distance clock use the same traction
    // calculation. Keep mass, analog input, braking and tuning in one place.
    private Vector3 CalculateJoystickAcceleration(Vector3 input, Vector3 velocity, float moveMultiplier)
    {
        input.y = 0;
        float magnitude = input.magnitude;
        bool hasInput = magnitude > Effective_moveDeadzone;
        Vector3 direction = hasInput ? input / Mathf.Max(.0001f, magnitude) : Vector3.zero;
        float strength = hasInput ? Mathf.InverseLerp(Effective_moveDeadzone, 1f, Mathf.Clamp01(magnitude)) : 0;
        Vector3 acceleration = Vector3.zero;
        if (hasInput && velocity.sqrMagnitude > .0001f)
        {
            Vector3 lateral = velocity - Vector3.Project(velocity, direction);
            acceleration += EnemyHitBraking(-lateral * Effective_lateralFriction);
            float speed = velocity.magnitude;
            if (speed > .001f && Vector3.Dot(velocity / speed, direction) < Effective_reverseDotThreshold)
                acceleration += EnemyHitBraking(-velocity * Effective_reverseBrake);
        }
        else acceleration += EnemyHitBraking(-velocity * Effective_idleBrake);
        if (hasInput) acceleration += direction * (strength * Effective_movePower * moveMultiplier / rb.mass);
        return acceleration;
    }

    private Vector3 ClampJoystickVelocity(Vector3 velocity, float multiplier)
    {
        if (!Effective_clampSpeed) return velocity;
        Vector3 planar = new Vector3(velocity.x, 0, velocity.z);
        float max = Mathf.Max(.1f, Effective_maxMoveSpeed * multiplier);
        if (planar.sqrMagnitude > max * max) planar = planar.normalized * max;
        return new Vector3(planar.x, velocity.y, planar.z);
    }

    internal Vector3 IntegrateLatticeVelocity(Vector3 velocity, float multiplier, float dt)
    {
        Vector3 planar = velocity;
        ApplyJoystickReversal(ref velocity, ref planar, false);
        Vector3 acceleration = CalculateJoystickAcceleration(movement, planar, multiplier);
        // The ordinary controller caps existing velocity before Unity integrates
        // this tick's forces, then PhysX applies linear damping.
        velocity = ClampJoystickVelocity(velocity, multiplier);
        return (velocity + acceleration * dt) * Mathf.Max(0, 1 - rb.linearDamping * dt);
    }
}
