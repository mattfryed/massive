using UnityEngine;

namespace Massive.Enemies
{
    public sealed partial class DysonSpherePanels
    {
        [Header("Panel point ripple (Amplified Dyson)")]
        public bool shimmerEnabled;
        [Min(0f), InspectorName("Wave Frequency"), Tooltip("Outward ripples per second, independent of breathing. Each ripple turns the shell over once. The next ripple waits until every panel has finished. Zero disables flipping.")]
        public float shimmerFrequency = .55f;
        [Range(0f, 180f), InspectorName("Flip Amplitude Degrees"), Tooltip("180 degrees turns each panel over completely. Zero disables rotation.")]
        public float shimmerAmplitudeDegrees = 180f;
        [Min(.01f), InspectorName("Wave Travel Seconds"), Tooltip("Time for the ripple to travel along the shell from its origin to the opposite pole. The visible hemisphere takes roughly half this time.")]
        public float shimmerWaveTravelSeconds = 1.8f;
        [Min(.01f), InspectorName("Panel Flip Seconds"), Tooltip("How long each panel takes to turn over once the ripple reaches it. Shorter values make a narrower, sharper wave front.")]
        public float shimmerPanelFlipSeconds = .28f;
        [InspectorName("Wave Origin Direction"), Tooltip("World-space direction from the sphere's center to the ripple's starting point. (0, 1, 0) starts at the visible center in the top-down view. Captured once per ripple so shell spin cannot reorder the flips.")]
        public Vector3 shimmerOriginDirection = Vector3.up;

        // Retain the old serialized values for existing prefab/scene overrides.
        // The sine-band controls no longer drive the point ripple.
        [HideInInspector]
        public float shimmerWavelength = 1.5f;
        [HideInInspector]
        public float shimmerFlipSoftness = .35f;

        private bool _shimmerWaveStarted, _shimmerReverseWave;
        private float _shimmerLastClock, _shimmerWaveStart, _shimmerWavePeriod, _shimmerFlipSeconds;

        private void ResetShimmerWave()
        {
            _shimmerWaveStarted = false;
            _shimmerReverseWave = false;
        }

        // Called once per pose, rather than per panel. Arrival times remain fixed
        // while the shell spins, and repeated updates at the same clock are harmless.
        private void PrepareShimmerWave(float clock)
        {
            if (!shimmerEnabled || shimmerAmplitudeDegrees <= 0f || shimmerFrequency <= 0f || IsSpawning)
            {
                ResetShimmerWave();
                return;
            }

            if (_shimmerWaveStarted && clock < _shimmerLastClock)
                ResetShimmerWave();
            _shimmerLastClock = clock;

            if (!_shimmerWaveStarted)
            {
                _shimmerWaveStarted = true;
                _shimmerWaveStart = clock;
                CaptureShimmerWave();
            }
            else if (clock - _shimmerWaveStart >= _shimmerWavePeriod)
            {
                int waves = Mathf.FloorToInt((clock - _shimmerWaveStart) / _shimmerWavePeriod);
                _shimmerWaveStart += waves * _shimmerWavePeriod;
                if ((waves & 1) != 0) _shimmerReverseWave = !_shimmerReverseWave;
                CaptureShimmerWave();
            }
        }

        private void CaptureShimmerWave()
        {
            float travel = Mathf.Max(.01f, shimmerWaveTravelSeconds);
            _shimmerFlipSeconds = Mathf.Max(.01f, shimmerPanelFlipSeconds);
            _shimmerWavePeriod = Mathf.Max(1f / shimmerFrequency, travel + _shimmerFlipSeconds);
            Vector3 direction = shimmerOriginDirection.sqrMagnitude > .0001f ? shimmerOriginDirection.normalized : Vector3.up;
            Vector3 originLocal = transform.InverseTransformDirection(direction).normalized;
            foreach (var panel in _panels)
            {
                float distance = Mathf.Acos(Mathf.Clamp(Vector3.Dot(panel.baseNormalLocal, originLocal), -1f, 1f)) / Mathf.PI;
                panel.shimmerArrivalSeconds = distance * travel;
            }
        }

        private Quaternion ApplyShimmer(Panel panel, Quaternion rotation, float clock)
        {
            if (!_shimmerWaveStarted || !shimmerEnabled || shimmerAmplitudeDegrees <= 0f || shimmerFrequency <= 0f)
                return rotation;
            float progress = Mathf.Clamp01((clock - _shimmerWaveStart - panel.shimmerArrivalSeconds) / _shimmerFlipSeconds);
            float flip = Mathf.SmoothStep(0f, 1f, progress);
            return RotateShimmerPanel(panel, rotation, _shimmerReverseWave ? 1f - flip : flip);
        }

        private Quaternion RotateShimmerPanel(Panel panel, Quaternion rotation, float flip)
        {
            // BuildPanels points local Y from the centroid toward the triangle's tip.
            // This is the symmetry axis of the 20/80-panel shell: a half-turn swaps
            // the two base corners and returns the reverse face to the same outline.
            return rotation * Quaternion.AngleAxis(shimmerAmplitudeDegrees * flip * panel.spawnEdge01, Vector3.up);
        }
    }
}
