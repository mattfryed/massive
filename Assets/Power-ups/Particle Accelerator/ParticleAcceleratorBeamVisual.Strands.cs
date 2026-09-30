using UnityEngine;

namespace Massive.PowerUps
{
    public partial class ParticleAcceleratorBeamVisual
    {
        [System.Serializable]
        public struct ThicknessRange
        {
            [Range(0f, 4f)] public float minimum;
            [Range(0f, 4f)] public float maximum;
            public ThicknessRange(float minimum, float maximum) { this.minimum = minimum; this.maximum = maximum; }
            public float Sample(float phase)
            {
                float low = Mathf.Max(0f, Mathf.Min(minimum, maximum));
                float high = Mathf.Max(low, Mathf.Max(minimum, maximum));
                return low == high ? low : Mathf.Lerp(low, high, .5f + .5f * Mathf.Sin(phase));
            }
        }

        // Fixed storage, shared with the shader. Each tube has its own position, radius,
        // material and surface depth. CPU sampling avoids trigonometry at every raymarch step.
        private const int MaxStrands = 8, NodesPerStrand = 65;
        private readonly Vector4[] strandNodes = new Vector4[MaxStrands * NodesPerStrand];
        private readonly Vector4[] strandInfo = new Vector4[MaxStrands];

        private Vector3 BuildPlasmaStrands(float length, float bodyRadius, float tailRadius,
            float headRadius, float invScale, float clock, float waveNumber)
        {
            int count = Mathf.Clamp(strandCount, 3, MaxStrands);
            int blackCount = Mathf.Max(1, count / 3), whiteCount = count - blackCount;
            float frequency = Mathf.Max(.1f, swirlTurnsPerUnit) * 2f * Mathf.PI / _spatialScale;
            int nodeCount = Mathf.Clamp(Mathf.CeilToInt(length * Mathf.Max(6f / _spatialScale, frequency * 1.6f)) + 1, 3, NodesPerStrand);
            float spacing = length / (nodeCount - 1), half = length * .5f;
            float ramp = Mathf.Min(length * .45f, Mathf.Max(.05f, muzzleRampWorld) * _spatialScale);
            Vector3 extent = Vector3.zero;
            for (int s = 0; s < count; s++)
            {
                bool black = s % 3 == 1;
                // At the 3/6-strand boundaries, exactly count/3 strands use the black material.
                if (s >= blackCount * 3) black = false;
                float share = black ? 1f - Mathf.Clamp01(whiteFraction) : Mathf.Clamp01(whiteFraction);
                float radiusWeight = .84f * Mathf.Sqrt(share / (black ? blackCount : whiteCount));
                float seed = s * 2.399963f, bound = 1f;
                Vector3 previous = Vector3.zero; float previousRadius = 0f;
                for (int n = 0; n < nodeCount; n++)
                {
                    float distance = n * spacing, z = distance - half;
                    float flow = distance * frequency - clock * swirlSpeed * (.73f + .11f * s);
                    float bend = .92f * Mathf.Sin(flow * (.19f + .017f * s) + seed)
                        + .43f * Mathf.Sin(flow * (.47f + .023f * s) - seed * 1.7f)
                        + .18f * Mathf.Sin(flow * .91f + seed * 2.3f);
                    float angle = s * 2f * Mathf.PI / count + .21f * Mathf.Sin(seed) + strandWander * bend;
                    float pin = Ease(Mathf.Clamp01(distance / ramp)) * Ease(Mathf.Clamp01((length - distance) / ramp));
                    float rangeTime = clock * Mathf.Max(0f, thicknessVariationSpeed);
                    float profile = Mathf.Lerp(startThickness.Sample(rangeTime + .7f), bodyThickness.Sample(distance * waveNumber - rangeTime), Ease(Mathf.Clamp01(distance / ramp)));
                    profile = Mathf.Lerp(profile, endThickness.Sample(rangeTime + 2.3f), Ease(Mathf.Clamp01((distance - length + ramp) / ramp)));
                    float orbit = bodyRadius * profile * strandSeparation * (.72f + .18f * Mathf.Sin(flow * .31f + seed)) * pin;
                    float centerPin = Mathf.Sin(Mathf.PI * distance / length);
                    Vector3 center = new Vector3(
                        bodyRadius * curveAmplitude * centerPin * Mathf.Sin(distance * waveNumber * .55f - clock * 2.1f),
                        bodyRadius * curveAmplitude * .32f * centerPin * Mathf.Sin(distance * waveNumber * .39f - clock * 1.7f + .9f), z);
                    // Both colors travel through the full cross-section, above and below one another.
                    center += new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * orbit;
                    float life = Mathf.Sin(flow * (.16f + .009f * s) + seed * 1.3f) + .4f * Mathf.Sin(flow * .37f - seed);
                    float taper = Ease(Mathf.Clamp01((life + .48f) / 1.08f));
                    // A thin white feeder prevents the complete bundle disappearing at once.
                    if (s == 0) taper = Mathf.Lerp(.35f, 1f, taper);
                    float radius = Mathf.Lerp(tailRadius, bodyRadius, Ease(Mathf.Clamp01(distance / ramp)));
                    radius = Mathf.Lerp(radius, headRadius, Ease(Mathf.Clamp01((distance - length + ramp) / ramp)));
                    radius *= profile * radiusWeight * Mathf.Lerp(1f, taper, strandTaper);
                    radius *= 1f + Mathf.Clamp01(thicknessWaveAmplitude) * Mathf.Sin(distance * waveNumber - clock * 5f + seed * .8f);
                    strandNodes[s * NodesPerStrand + n] = new Vector4(center.x * invScale, center.y * invScale, center.z * invScale, radius * invScale);
                    extent = Vector3.Max(extent, new Vector3(Mathf.Abs(center.x), Mathf.Abs(center.y), Mathf.Abs(center.z)) + Vector3.one * radius);
                    if (n > 0)
                    {
                        Vector2 offset = new Vector2(center.x - previous.x, center.y - previous.y);
                        // Global Lipschitz bound for this piecewise linear tube, including radius changes.
                        bound = Mathf.Max(bound, 1f + (offset.magnitude + Mathf.Abs(radius - previousRadius)) / spacing);
                    }
                    previous = center; previousRadius = radius;
                }
                strandInfo[s] = new Vector4(black ? 1f : 0f, bound, share > 0f ? 1f : 0f, 0f);
            }
            _properties.SetVectorArray("_PlasmaNodes", strandNodes);
            _properties.SetVectorArray("_PlasmaTubeInfo", strandInfo);
            _properties.SetVector("_PlasmaTubeSampling", new Vector4(count, nodeCount, spacing * invScale, half * invScale));
            return (extent + Vector3.one * Mathf.Max(.01f, paddingWorld * _spatialScale)) * invScale;
        }
    }
}
