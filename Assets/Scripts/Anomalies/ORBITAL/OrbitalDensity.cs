using System;
using UnityEngine;

namespace Massive.Orbital
{
    /// <summary>Orbital-inspired |psi| squared, shared by the cut face and collision hazard.
    /// Original f/d mixture plus five hydrogen-inspired radial/angular formations.
    /// This is a normalized game field, not a calibrated atomic wavefunction.</summary>
    public static class OrbitalDensity
    {
        private const int RadialResolution = 2048;
        private static readonly float[][] radialProfiles = BuildRadialProfiles();
        public static float SupportRadius(float tailExtension) => 1f + Mathf.Clamp(tailExtension, 0f, .5f);

        public static float Evaluate(Vector3 normalizedPosition, float tailExtension = 0f, OrbitalFormation formation = OrbitalFormation.Original)
        {
            float r = normalizedPosition.magnitude;
            float extent = SupportRadius(tailExtension);
            if (r <= .0001f || r >= extent) return 0f;
            float c = normalizedPosition.z / r;
            float envelopeRadius = r <= .78f ? r : .78f + (r - .78f) * .22f / (extent - .78f);
            float edge = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.78f, 1f, envelopeRadius));
            if (formation != OrbitalFormation.Original)
            {
                float c2 = c * c, s2 = Mathf.Max(0f, 1f - c2);
                float angularDensity;
                switch (formation)
                {
                    case OrbitalFormation.Shells300: angularDensity = 1f; break;
                    case OrbitalFormation.Lobes322: angularDensity = s2 * s2; break;
                    case OrbitalFormation.Lobes411: angularDensity = s2; break;
                    case OrbitalFormation.Lobes420: angularDensity = .25f * (3f * c2 - 1f) * (3f * c2 - 1f); break;
                    case OrbitalFormation.Lobes421: angularDensity = 4f * s2 * c2; break;
                    default: return 0f;
                }
                float coordinate = Mathf.Clamp01(envelopeRadius) * RadialResolution;
                int lo = Mathf.Min((int)coordinate, RadialResolution - 1);
                float[] profile = radialProfiles[(int)formation - 1];
                return Mathf.Clamp01(Mathf.Lerp(profile[lo], profile[lo + 1], coordinate - lo) * angularDensity * edge);
            }
            float p3 = .5f * c * (5f * c * c - 3f);
            float p2 = .5f * (3f * c * c - 1f);
            float angular = .78f * p3 * p3 + .22f * p2 * p2;
            // Only stretch the sparse outer taper; the lobes and all inner densities stay in place.
            float scaled = envelopeRadius / .43f;
            float radial = Mathf.Pow(scaled, 4f) * Mathf.Exp(4f * (1f - scaled));
            return Mathf.Clamp01(radial * angular * edge);
        }

        private static float[][] BuildRadialProfiles()
        {
            var result = new float[5][];
            for (int state = 0; state < result.Length; state++)
            {
                result[state] = new float[RadialResolution + 1];
                float peak = 0f;
                for (int i = 0; i <= RadialResolution; i++)
                {
                    float rho = i / (float)RadialResolution * (state == 0 ? 14f : state == 1 ? 9f : state == 2 ? 18f : 15f);
                    int l = state == 0 ? 0 : state == 2 ? 1 : 2;
                    float laguerre = state == 0 ? .5f * (rho * rho - 6f * rho + 6f) :
                        state == 1 ? 1f : state == 2 ? .5f * (rho * rho - 10f * rho + 20f) : 6f - rho;
                    // r^2 radial probability weighting and compressed contrast keep outer shells readable in-game.
                    // Nodes and |Y_lm|^2 angular structure are retained; this is not an SI wavefunction plot.
                    float value = Mathf.Pow(rho, 2 * l + 2) * Mathf.Exp(-rho) * laguerre * laguerre;
                    result[state][i] = value; peak = Mathf.Max(peak, value);
                }
                for (int i = 0; i <= RadialResolution; i++) result[state][i] = Mathf.Pow(result[state][i] / peak, .65f);
            }
            return result;
        }

        public static float StrikeProbability(float density, float peakRate, float seconds)
        {
            return 1f - Mathf.Exp(-Mathf.Max(0f, peakRate) * Mathf.Clamp01(density) * Mathf.Max(0f, seconds));
        }

        public static Vector3 SampleDirection(Vector3 radial, System.Random random)
        {
            radial.y = 0f;
            if (radial.sqrMagnitude < .0001f) radial = Vector3.forward;
            radial.Normalize();
            Vector3 tangent = new Vector3(-radial.z, 0f, radial.x);
            if (random.NextDouble() < .5) tangent = -tangent;
            // Independently sampled radial spread and handedness, with no orbit phase/state.
            return (tangent + radial * ((float)random.NextDouble() * 1.6f - .8f)).normalized;
        }

        /// <summary>Tabulated sampling avoids rejection loops in the particle update.</summary>
        public sealed class FaceSampler
        {
            private const int Resolution = 160;
            private readonly float[] cumulative = new float[Resolution * Resolution];
            private readonly float total;
            private readonly float extent;
            public float TotalWeight => total;

            public FaceSampler(bool weightForRevolution = false, float tailExtension = 0f, OrbitalFormation formation = OrbitalFormation.Original)
            {
                extent = SupportRadius(tailExtension);
                float sum = 0f;
                for (int i = 0; i < cumulative.Length; i++)
                {
                    Vector3 p = new Vector3(((i % Resolution) + .5f) * 2f / Resolution - 1f,
                        0f, ((i / Resolution) + .5f) * 2f / Resolution - 1f) * extent;
                    // A revolved cell represents a cylindrical shell: weight by its distance from Z.
                    sum += Evaluate(p, tailExtension, formation) * (weightForRevolution ? Mathf.Abs(p.x) : 1f);
                    cumulative[i] = sum;
                }
                total = sum;
            }

            public Vector3 Sample(System.Random random)
            {
                float value = (float)random.NextDouble() * total;
                int lo = 0, hi = cumulative.Length - 1;
                while (lo < hi)
                {
                    int mid = (lo + hi) / 2;
                    if (cumulative[mid] <= value) lo = mid + 1; else hi = mid;
                }
                return new Vector3(((lo % Resolution) + (float)random.NextDouble()) * 2f / Resolution - 1f,
                    0f, ((lo / Resolution) + (float)random.NextDouble()) * 2f / Resolution - 1f) * extent;
            }
        }

        /// <summary>Full 3D density sampled by revolving the axisymmetric field around local Z.</summary>
        public sealed class VolumeSampler
        {
            private readonly FaceSampler cylindrical;

            public float TotalWeight => cylindrical.TotalWeight;
            public VolumeSampler(float tailExtension = 0f, OrbitalFormation formation = OrbitalFormation.Original)
            { cylindrical = new FaceSampler(true, tailExtension, formation); }

            public Vector3 Sample(System.Random random)
            {
                Vector3 p = cylindrical.Sample(random);
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float distance = Mathf.Abs(p.x);
                return new Vector3(distance * Mathf.Cos(angle), distance * Mathf.Sin(angle), p.z);
            }
        }
    }
}
