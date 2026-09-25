using UnityEngine;

namespace Massive.Singularity
{
    public sealed partial class SingularityBlackHolePortal
    {
        /// <summary>Entry pose from separately advancing near/far silhouette ends.
        /// Returns distance from the mouth, uniform scale, and axial stretch.
        /// The leading end is drawn in first; the tail accelerates after it. The
        /// midpoint and shape therefore cannot run on conflicting animation clocks.
        /// No collider, mass or authored visual scale is modified by this calculation.</summary>
        public static Vector3 EvaluateEntryPose(float normalizedTime, float exponent,
            float startDistance, float visualRadius, float horizonScale, float maxStretch)
        {
            float t = Safe(normalizedTime, 0f, 1f);
            float distance = Safe(startDistance, 0f, 1000f);
            float radius = Safe(visualRadius, .001f, 1000f);
            float minimum = Safe(horizonScale, .001f, .3f);
            float maximum = Safe(maxStretch, 1f, 8f);
            if (t <= 0f) return new Vector3(distance, 1f, 1f);
            if (t >= 1f) return new Vector3(0f, minimum, maximum);

            // Increasing the existing entry exponent increases the near/tail
            // timing separation, rather than holding the whole actor still.
            // Already-deep captures simply converge: they must never retreat
            // from the mouth to establish an artificial stretching run-up.
            float power = distance <= radius ? 1f
                : 1f + .25f * Safe(exponent, .1f, 8f) * Mathf.Clamp01(maximum - 1f);
            float horizonHalfLength = radius * minimum * maximum;
            float near = -horizonHalfLength + (distance - radius + horizonHalfLength) * Mathf.Pow(1f - t, power);
            float far = horizonHalfLength + (distance + radius - horizonHalfLength) * (1f - Mathf.Pow(t, power));
            float midpoint = Mathf.Clamp((near + far) * .5f, 0f, distance);
            float axialRatio = Mathf.Clamp((far - near) / (2f * radius), .001f, maximum);

            // Retain enough length for the lagging tail. Scale may shrink only
            // as far as the maximum stretch can support the endpoint separation.
            // Small actors still obey the authored stretch limit.
            float desiredScale = Mathf.Lerp(1f, minimum, Smooth01(t));
            float scale = Mathf.Min(axialRatio, Mathf.Max(desiredScale, axialRatio / maximum));
            return new Vector3(midpoint, scale, axialRatio / scale);
        }
    }
}
