using UnityEngine;

namespace Massive.Player
{
    /// <summary>Optional, one-shot joystick reversal assist. Called before ordinary traction.</summary>
    [DisallowMultipleComponent, AddComponentMenu("MASSIVE/Player/Joystick Reversal Assist")]
    public sealed class PlayerMovementReversal : MonoBehaviour
    {
        [Tooltip("Only ordinary stick movement can trigger the assist. Attacks, shields, recoil and protected external motion are excluded.")]
        public bool assistEnabled = true;
        [Tooltip("Full width of the cone behind current horizontal velocity. 100 means 50 degrees either side of directly backward. 0 requires an exact reversal; 180 includes the rear half-plane.")]
        [Range(0f, 180f)] public float backwardConeDegrees = 100f;
        [Tooltip("Momentum kept on entering the backward cone: 0 drops horizontal drift; 1 keeps normal movement. Applied once per stick reversal, not every physics tick. Acceleration and top speed are unchanged.")]
        [Range(0f, 1f)] public float retainedMomentum;

        private Vector3 lastInputDirection;
        private bool hasHistory, hadInput, wasInCone;
        private float protectedUntil = float.NegativeInfinity;
        public int AppliedReversals { get; private set; }
        public Vector3 LastVelocityBefore { get; private set; }
        public Vector3 LastVelocityAfter { get; private set; }

        private void OnEnable()
        {
            // Also safe when entering Play with domain/scene reload disabled.
            protectedUntil = float.NegativeInfinity;
            ResetHistory();
        }
        private void OnDisable() { ResetHistory(); }
        private void OnValidate()
        {
            backwardConeDegrees = Mathf.Clamp(backwardConeDegrees, 0f, 180f);
            retainedMomentum = Mathf.Clamp01(retainedMomentum);
        }

        public void ResetHistory()
        {
            lastInputDirection = Vector3.zero;
            hasHistory = hadInput = wasInCone = false;
        }

        /// <summary>Preserve action/impact momentum, including impulses without a stun state.</summary>
        public void BlockFor(float seconds)
        {
            protectedUntil = Mathf.Max(protectedUntil, Time.time + Mathf.Max(0f, seconds));
            ResetHistory();
        }

        public bool TryApply(Vector3 velocity, Vector3 input, float deadzone, bool joystickOnly, float now, out Vector3 result)
        {
            result = velocity;
            if (!isActiveAndEnabled || !assistEnabled || !joystickOnly || now < protectedUntil)
            {
                ResetHistory();
                return false;
            }

            input.y = 0f;
            if (input.sqrMagnitude <= deadzone * deadzone)
            {
                hadInput = wasInCone = false;
                return false;
            }

            Vector3 direction = input.normalized;
            Vector3 planar = new Vector3(velocity.x, 0f, velocity.z);
            float limit = -Mathf.Cos(Mathf.Clamp(backwardConeDegrees, 0f, 180f) * .5f * Mathf.Deg2Rad);
            bool inside = planar.sqrMagnitude > .0001f && Vector3.Dot(planar.normalized, direction) <= limit + .00001f;
            // A collision changing velocity under an unchanged stick is not a joystick reversal.
            bool freshInput = !hadInput || Vector3.Dot(lastInputDirection, direction) < .99999f;
            bool apply = hasHistory && freshInput && inside && !wasInCone && retainedMomentum < 1f;
            hasHistory = hadInput = true;
            wasInCone = inside;
            lastInputDirection = direction;
            if (!apply) return false;

            planar *= Mathf.Clamp01(retainedMomentum);
            result = new Vector3(planar.x, velocity.y, planar.z);
            LastVelocityBefore = velocity;
            LastVelocityAfter = result;
            AppliedReversals++;
            return true;
        }
    }
}
