using UnityEngine;

namespace Massive.Enemies
{
    /// <summary>Shared by Carrier route checks and the released Drone's movement.</summary>
    public struct DroneLaunchTrajectory
    {
        public Vector3 direction;
        public float speed, straightSeconds, bendSeconds, turnDegrees;
        public float Duration => straightSeconds + bendSeconds;
        public Vector3 Velocity(float age)
        {
            float turn = bendSeconds > 0f ? Mathf.SmoothStep(0f, 1f, (age - straightSeconds) / bendSeconds) : 0f;
            return Quaternion.AngleAxis(turnDegrees * turn, Vector3.up) * direction * speed;
        }
    }
}
