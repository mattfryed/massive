using UnityEngine;

namespace Massive.Enemies
{
    public sealed partial class DysonSpherePanels
    {
        [Header("Panel flip wave (Amplified Dyson)")]
        public bool shimmerEnabled;
        [Min(0f), Tooltip("Complete front/back wave cycles per second, independent of breathing.")]
        public float shimmerFrequency = .55f;
        [Range(0f, 180f), Tooltip("180 degrees turns each panel over completely. Zero disables rotation.")]
        public float shimmerAmplitudeDegrees = 180f;
        [Min(.01f), Tooltip("Wave cycles from the visible center (+world Y) around the shell to its reverse pole.")]
        public float shimmerWavelength = 1.5f;
        [Range(.05f, 1f), Tooltip("Fraction of the wave used to transition between the two held faces.")]
        public float shimmerFlipSoftness = .35f;

        private Quaternion ApplyShimmer(Panel panel, Quaternion rotation, float clock)
        {
            if (!shimmerEnabled || shimmerAmplitudeDegrees <= 0f) return rotation;
            // World-up is the gameplay camera's visible center. Shell spin must not drag the wave origin.
            Vector3 normal = transform.TransformDirection(panel.baseNormalLocal).normalized;
            float distance = Mathf.Acos(Mathf.Clamp(Vector3.Dot(normal, Vector3.up), -1f, 1f)) / Mathf.PI;
            float phase = (clock * Mathf.Max(0f, shimmerFrequency) - distance * shimmerWavelength) * Mathf.PI * 2f;
            float softness = Mathf.Max(.05f, shimmerFlipSoftness);
            float flip = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-softness, softness, Mathf.Sin(phase)));
            // BuildPanels points local Y from the centroid toward the triangle's tip.
            // This is the symmetry axis of the 20/80-panel shell: a half-turn swaps
            // the two base corners and returns the reverse face to the same outline.
            return rotation * Quaternion.AngleAxis(shimmerAmplitudeDegrees * flip * panel.spawnEdge01, Vector3.up);
        }
    }
}
