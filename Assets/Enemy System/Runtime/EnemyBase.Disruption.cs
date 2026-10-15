using UnityEngine;

namespace Massive.Enemies
{
    public partial class EnemyBase
    {
        private float disruptionRemaining, disruptionRecovery;
        public bool IsDisrupted => disruptionRemaining > 0f;

        // Distinct from scene pause: disrupted enemies remain vulnerable and keep
        // their normal death/score lifecycle. All sources share the recovery gate.
        public bool TryDisrupt(float seconds, float recoverySeconds)
        {
            if (IsDead || IsPaused || disruptionRecovery > 0f || seconds <= 0f) return false;
            disruptionRemaining = seconds;
            disruptionRecovery = Mathf.Max(seconds, recoverySeconds);
            StopDisruptedMotion();
            return true;
        }

        private void TickDisruption(float dt)
        {
            disruptionRemaining = Mathf.Max(0f, disruptionRemaining - dt);
            disruptionRecovery = Mathf.Max(0f, disruptionRecovery - dt);
        }

        private void ResetDisruption() { disruptionRemaining = disruptionRecovery = 0f; }
        private void FixedUpdate() { if (IsDisrupted && !IsPaused && !IsDead) StopDisruptedMotion(); }
        private void StopDisruptedMotion()
        {
            if (!_rb || _rb.isKinematic) return;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
    }
}
