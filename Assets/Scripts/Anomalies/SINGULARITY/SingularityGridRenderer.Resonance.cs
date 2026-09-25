using Massive.Resonance;
using UnityEngine;

namespace Massive.Singularity
{
    public sealed partial class SingularityGridRenderer
    {
        [Header("Front-face Resonance attraction")]
        [Tooltip("Converts the pattern's force strength to responsive surface displacement. Radius, arc taper, pulse and falloff still come from the active Resonance pattern.")]
        [Min(0f)] public float resonanceAttractionGain = .06f;
        [Tooltip("Soft limit on Resonance-only grid displacement in front-face units. Does not alter player movement or collisions.")]
        [Min(.01f)] public float resonanceMaximumDisplacement = .65f;
        [Tooltip("Fade attraction to zero before the flat face joins either fold, and at the hard side edges. Rear-face points never receive this field.")]
        [Min(.05f)] public float resonanceEdgeFeather = .8f;

        public const int MaximumResonanceSamples = 128;
        public ResonancePatternController ActiveResonancePattern => resonancePattern;
        public int ActiveResonanceSampleCount { get; private set; }
        private ResonancePatternController resonancePattern;
        private readonly ResonancePatternController.GridAttractionSample[] resonanceWorldSamples =
            new ResonancePatternController.GridAttractionSample[MaximumResonanceSamples];
        private readonly Vector4[] resonanceSamples = new Vector4[MaximumResonanceSamples];
        private static readonly int ResonanceSamplesId = Shader.PropertyToID("_SurfaceResonanceSamples");
        private static readonly int ResonanceCountId = Shader.PropertyToID("_SurfaceResonanceCount");
        private static readonly int ResonanceShapeId = Shader.PropertyToID("_SurfaceResonanceShape");
        private static readonly int ResonanceResponseId = Shader.PropertyToID("_SurfaceResonanceResponse");

        /// <summary>Explicit scene-cycle binding. Passing null retires the field;
        /// this renderer never searches for patterns in other loaded scenes.</summary>
        public void SetResonancePattern(ResonancePatternController pattern)
        { resonancePattern = pattern; }

        private void BindResonanceAttraction()
        {
            int count = 0;
            float outer = .8f, inner = .5f;
            if (surface != null && resonancePattern != null && resonancePattern.gameObject.scene == gameObject.scene
                && Finite(resonanceAttractionGain) && resonanceAttractionGain > 0f)
            {
                if (resonancePattern.overrideGridFalloff)
                {
                    outer = Mathf.Clamp01(resonancePattern.gridOuterFeather);
                    inner = Mathf.Clamp01(resonancePattern.gridInnerSoftening);
                }
                // A folded grid has no legacy global Force Falloff. With the
                // override off it uses the same smooth default profile, rather
                // than borrowing a singleton grid from a different scene.
                int copied = resonancePattern.CopyGridAttractionSamples(resonanceWorldSamples, (float)resonancePattern.ContactClock);
                Vector3 scale = surface.transform.lossyScale;
                float units = Mathf.Max(.0001f, (Mathf.Abs(scale.x) + Mathf.Abs(scale.z)) * .5f);
                for (int i = 0; i < copied; i++)
                {
                    var sample = resonanceWorldSamples[i];
                    Vector3 local = surface.transform.InverseTransformPoint(sample.Position);
                    float s = local.z + surface.FrontHeight * .5f;
                    if (!Finite(local.x) || !Finite(local.y) || !Finite(s) || !Finite(sample.Radius)
                        || !Finite(sample.Strength) || sample.Radius <= 0f || sample.Strength <= 0f) continue;
                    // Only samples on the flat front chart are meaningful. This
                    // also rejects a rear-height pattern with the same XZ image.
                    if (s < 0f || s > surface.FrontHeight || Mathf.Abs(local.y) > .25f) continue;
                    resonanceSamples[count++] = new Vector4(local.x, s, sample.Radius / units,
                        sample.Strength * resonanceAttractionGain / units);
                }
            }
            ActiveResonanceSampleCount = count;
            properties.SetInt(ResonanceCountId, count);
            properties.SetVectorArray(ResonanceSamplesId, resonanceSamples);
            properties.SetVector(ResonanceShapeId, new Vector4(surface != null ? surface.FrontHeight : 0f,
                Mathf.Max(.05f, resonanceEdgeFeather), outer, inner));
            properties.SetVector(ResonanceResponseId, new Vector4(Mathf.Max(.01f, resonanceMaximumDisplacement), 0f, 0f, 0f));
        }

        /// <summary>CPU reference for the front-only shader field. Samples use
        /// (across, front chart distance, radius, converted strength). This never
        /// takes a periodic shortest path through the rear projected image.</summary>
        public static Vector2 EvaluateResonanceOffset(Vector2 logical, Vector4[] samples, int count,
            float frontHeight, float width, float edgeFeather, float outerFeather, float innerSoftening, float maximumDisplacement)
        {
            if (samples == null || count <= 0 || logical.y <= 0f || logical.y >= frontHeight) return Vector2.zero;
            float distanceToEdge = Mathf.Min(width * .5f - Mathf.Abs(logical.x), Mathf.Min(logical.y, frontHeight - logical.y));
            if (distanceToEdge <= 0f) return Vector2.zero;
            Vector2 offset = Vector2.zero;
            for (int i = 0; i < Mathf.Min(Mathf.Min(count, samples.Length), MaximumResonanceSamples); i++)
            {
                Vector4 sample = samples[i];
                if (!Finite(sample.x) || !Finite(sample.y) || !Finite(sample.z) || !Finite(sample.w)
                    || sample.z <= 0f || sample.w <= 0f) continue;
                Vector2 toward = new Vector2(sample.x - logical.x, sample.y - logical.y);
                float distance = toward.magnitude, t = distance / sample.z;
                if (t >= 1f) continue;
                float feather = Mathf.Clamp01(outerFeather), soften = Mathf.Clamp01(innerSoftening);
                float outer = feather <= 0f ? 1f : 1f - ResonanceSmooth((t - (1f - feather)) / Mathf.Max(.000001f, feather));
                float inner = soften <= 0f ? 1f : ResonanceSmooth(t / Mathf.Max(.000001f, soften));
                offset += toward / Mathf.Max(.000001f, distance) * (sample.w * outer * inner);
            }
            float maximum = Mathf.Max(.01f, maximumDisplacement);
            offset *= maximum / (maximum + offset.magnitude);
            return offset * ResonanceSmooth(distanceToEdge / Mathf.Max(.05f, edgeFeather));
        }

        private static float ResonanceSmooth(float value)
        { value = Mathf.Clamp01(value); return value * value * value * (value * (value * 6f - 15f) + 10f); }
    }
}
