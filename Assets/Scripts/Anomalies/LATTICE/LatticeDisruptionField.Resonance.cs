using Massive.Resonance;
using UnityEngine;

namespace Massive.Lattice
{
    public sealed partial class LatticeDisruptionField : IResonanceGridPresentation
    {
        [Header("Smooth Resonance deformation")]
        [Tooltip("Use SINGULARITY's continuous, softly bounded attraction along the strands. Turn off to compare the original spring response.")]
        public bool smoothResonance = true;
        [Tooltip("Converts the active Resonance pattern's force strength into visual displacement.")]
        [Min(0f)] public float resonanceAttractionGain = .06f;
        [Tooltip("Soft limit on Resonance displacement in world units. Disconnected dots retain their movement coordinates.")]
        [Min(.01f)] public float resonanceMaximumDisplacement = .65f;
        [Tooltip("World-space distance over which Resonance deformation fades before reaching the arena boundary.")]
        [Min(.05f)] public float resonanceEdgeFeather = .8f;

        const int MaximumResonanceSamples = 128;
        readonly ResonancePatternController.GridAttractionSample[] resonanceWorldSamples =
            new ResonancePatternController.GridAttractionSample[MaximumResonanceSamples];
        readonly Vector4[] resonanceSamples = new Vector4[MaximumResonanceSamples];
        readonly Vector4[] resonanceProfiles = new Vector4[MaximumResonanceSamples];
        int resonanceFrame = -1, resonanceCount;
        public int ActiveResonanceSampleCount => smoothResonance && resonanceFrame == Time.frameCount ? resonanceCount : 0;

        public bool TryPresentResonance(ResonancePatternController pattern)
        {
            if (!isActiveAndEnabled || !smoothResonance || !IsReady || !pattern
                || pattern.gameObject.scene != gameObject.scene) return false;
            if (resonanceFrame != Time.frameCount)
            { resonanceCount = 0; resonanceFrame = Time.frameCount; }
            int copied = pattern.CopyGridAttractionSamples(resonanceWorldSamples, (float)pattern.ContactClock);
            // Same default profile as SINGULARITY; an authored override still wins.
            Vector4 profile = new Vector4(pattern.overrideGridFalloff ? Mathf.Clamp01(pattern.gridOuterFeather) : .8f,
                pattern.overrideGridFalloff ? Mathf.Clamp01(pattern.gridInnerSoftening) : .5f, 0f, 0f);
            for (int i = 0; i < copied && resonanceCount < MaximumResonanceSamples; i++)
            {
                var sample = resonanceWorldSamples[i];
                Vector3 local = transform.InverseTransformPoint(sample.Position);
                float strength = sample.Strength * Mathf.Max(0f, resonanceAttractionGain);
                if (!float.IsFinite(local.x) || !float.IsFinite(local.y) || !float.IsFinite(sample.Radius)
                    || !float.IsFinite(strength) || sample.Radius <= 0f || strength <= 0f) continue;
                resonanceSamples[resonanceCount] = new Vector4(local.x, local.y, sample.Radius, strength);
                resonanceProfiles[resonanceCount++] = profile;
            }
            // Consume even a zero-gain field: adding legacy forces as well would
            // reintroduce the simulation's pinching and double the response.
            return true;
        }

        void BindResonanceAttraction()
        {
            Vector3 scale = transform.lossyScale;
            block.SetInt("_LatticeResonanceCount", ActiveResonanceSampleCount);
            block.SetVectorArray("_LatticeResonanceSamples", resonanceSamples);
            block.SetVectorArray("_LatticeResonanceProfiles", resonanceProfiles);
            block.SetVector("_LatticeResonanceMetric", new Vector4(Mathf.Abs(scale.x), Mathf.Abs(scale.y), 0f, 0f));
            block.SetVector("_LatticeResonanceResponse", new Vector4(Mathf.Max(.01f, resonanceMaximumDisplacement),
                Mathf.Max(.05f, resonanceEdgeFeather), 0f, 0f));
        }

        void ResetResonanceAttraction() { resonanceCount = 0; resonanceFrame = -1; }
    }
}
