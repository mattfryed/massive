using UnityEngine;

namespace Massive.Singularity
{
    public sealed partial class SingularityGridRenderer
    {
        [Header("Black-hole grid field")]
        [Tooltip("Scene-local source. Radius, pull, feather and rear-face controls are on its Black Hole Portal component, independent of player attraction.")]
        public SingularityBlackHolePortal blackHole;

        private static readonly int BlackHoleFieldId = Shader.PropertyToID("_BlackHoleGridField");
        private static readonly int BlackHoleShapeId = Shader.PropertyToID("_BlackHoleGridShape");
        private static readonly int BlackHoleProfileId = Shader.PropertyToID("_BlackHoleGridProfile");

        private void BindBlackHoleAttraction()
        {
            Vector4 field = Vector4.zero, shape = Vector4.zero, profile = Vector4.zero;
            if (surface != null && blackHole != null && blackHole.isActiveAndEnabled
                && blackHole.surface == surface && blackHole.gameObject.scene == gameObject.scene
                && blackHole.enableGridAttraction)
            {
                Vector3 center = surface.transform.InverseTransformPoint(blackHole.transform.position);
                float radius = blackHole.gridAttractionRadius, pull = blackHole.gridAttractionPull;
                if (Finite(center.x) && Finite(center.z) && Finite(radius) && Finite(pull)
                    && Finite(blackHole.gridAttractionFeather) && radius > 0f && pull > 0f
                    && Mathf.Abs(center.x) < surface.Width * .5f && Mathf.Abs(center.z) < surface.FrontHeight * .5f)
                {
                    field = new Vector4(center.x, center.z, Mathf.Clamp(radius, .1f, 100f), Mathf.Clamp(pull, 0f, 5f));
                    shape = new Vector4(surface.FrontHeight, surface.RearStart, surface.RearScale,
                        blackHole.gridAttractionOnRear ? 1f : 0f);
                    profile.x = Mathf.Clamp(blackHole.gridAttractionFeather, .05f, 1f);
                }
            }
            // Always write zero when unavailable, including disable and deleted references.
            properties.SetVector(BlackHoleFieldId, field);
            properties.SetVector(BlackHoleShapeId, shape);
            properties.SetVector(BlackHoleProfileId, profile);
        }

        /// <summary>CPU reference for the shader's face-local radial compression.
        /// Rear distances use front-sized coordinates, matching the portal field.
        /// No physics or surface geometry changes; each flat face's edges stay fixed.</summary>
        public static Vector2 EvaluateBlackHoleOffset(Vector2 logical, Vector2 center, float radius,
            float pull, float feather, bool affectRear, float width, float frontHeight,
            float rearStart, float rearScale, float loopLength)
        {
            if (!Finite(logical.x) || !Finite(logical.y) || !Finite(center.x) || !Finite(center.y)
                || !Finite(radius) || !Finite(pull) || !Finite(feather) || radius <= 0f || pull <= 0f
                || width <= 0f || frontHeight <= 0f || rearScale <= 0f || loopLength <= 0f
                || Mathf.Abs(center.x) >= width * .5f || Mathf.Abs(center.y) >= frontHeight * .5f) return Vector2.zero;
            radius = Mathf.Clamp(radius, .1f, 100f); pull = Mathf.Clamp(pull, 0f, 5f);
            float s = Mathf.Repeat(logical.y, loopLength);
            bool rear = s >= rearStart && s <= rearStart + frontHeight * rearScale;
            if (s > frontHeight && (!rear || !affectRear)) return Vector2.zero;
            Vector2 point = new Vector2(logical.x, rear
                ? frontHeight * .5f - (s - rearStart) / rearScale : s - frontHeight * .5f);
            float edge = Mathf.Min(width * .5f - Mathf.Abs(point.x), frontHeight * .5f - Mathf.Abs(point.y));
            if (edge <= 0f) return Vector2.zero;
            Vector2 toward = center - point;
            float t = toward.magnitude / radius;
            if (t >= 1f) return Vector2.zero;
            feather = Mathf.Clamp(feather, .05f, 1f);
            float weight = pull * (1f - ResonanceSmooth((t - (1f - feather)) / feather));
            // Displacement vanishes linearly at the center and smoothly at its extent.
            // The compression fraction is always < .85, even for extreme tuning.
            Vector2 offset = toward * (.85f * weight / (.85f + weight))
                * ResonanceSmooth(edge / Mathf.Max(.05f, Mathf.Min(radius * .25f, 1f)));
            return new Vector2(offset.x, rear ? -offset.y * rearScale : offset.y);
        }
    }
}
