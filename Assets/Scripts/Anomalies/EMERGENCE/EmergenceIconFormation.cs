using System;
using UnityEngine;

namespace Massive.Emergence
{
    /// <summary>
    /// A small, cached 3D relaxation of the ecology's pair forces. Menu instances share
    /// the settled shape; no solver, physics bodies or metabolism run each frame.
    /// </summary>
    internal static class EmergenceIconFormation
    {
        internal sealed class Shape
        {
            public Vector3[] centers;
            public float[] sizes;
            public float maximumSize;
        }

        static int cachedKey;
        static Shape cached;

        public static Shape Build(EmergenceProfile profile, int[] counts, int steps)
        {
            int key = Key(profile, counts, steps);
            if (cached != null && key == cachedKey) return cached;
            int count = 0;
            foreach (int n in counts) count += n;
            var positions = new Vector3[count]; var velocities = new Vector3[count];
            var forces = new Vector3[count]; var kinds = new int[count];
            var radii = new float[count]; var sizes = new float[count];
            var response = new float[4]; var damping = new float[4]; var speeds = new float[4];
            var attraction = new float[16];
            float range = profile ? Mathf.Max(.005f, profile.interactionRadius) : .855f;
            float separation = profile ? Mathf.Clamp(profile.separation, .001f, range * .99f) : .139f;
            float repulsion = profile ? Mathf.Max(0, profile.repulsion) : 12.82f;
            float gap = profile ? Mathf.Max(0, profile.particleGap) : .0064f;
            float coreMultiple = profile ? Mathf.Clamp(profile.coreRangeMultiplier, 1, 4) : 3;
            float coreForce = profile ? Mathf.Max(0, profile.coreRepulsion) : 42.75f;
            float coreDamping = profile ? Mathf.Max(0, profile.coreDamping) : 6;
            float maxForce = profile ? Mathf.Max(.1f, profile.maximumForce) : 24;
            const float dt = 1f / 30;
            var random = new System.Random(profile ? profile.seed : 1729);
            float baseDiameter = Species(profile, 0)?.diameter ?? .0267f;
            baseDiameter = Mathf.Max(.005f, baseDiameter);
            float maximumSize = 1;
            int index = 0;
            for (int s = 0; s < 4; s++)
            {
                var species = Species(profile, s);
                float diameter = Mathf.Max(.005f, species?.diameter ?? (s == 1 ? .04f : .0267f));
                response[s] = (profile ? Mathf.Max(0, profile.force) : 2.137f) * Mathf.Max(0, species?.forceMultiplier ?? 1);
                damping[s] = Mathf.Exp(-(profile ? Mathf.Max(.1f, profile.drag) : 25) * Mathf.Max(.01f, species?.dragMultiplier ?? 1) * dt);
                speeds[s] = (profile ? Mathf.Max(.1f, profile.speedLimit) : .267f) * Mathf.Max(.01f, species?.speedMultiplier ?? 1);
                for (int t = 0; t < 4; t++)
                    attraction[s * 4 + t] = profile && profile.attraction != null && profile.attraction.Length == 25
                        ? profile.attraction[s * EmergenceProfile.SpeciesCount + t] : (s == t ? .18f : .35f);
                for (int i = 0; i < counts[s]; i++, index++)
                {
                    // Every species starts throughout the FULL volume; there is no membrane cutaway.
                    float y = (float)random.NextDouble() * 2 - 1;
                    float angle = (float)random.NextDouble() * Mathf.PI * 2;
                    float radial = Mathf.Sqrt(1 - y * y);
                    positions[index] = new Vector3(Mathf.Cos(angle) * radial, y, Mathf.Sin(angle) * radial)
                        * (range * .65f * Mathf.Pow((float)random.NextDouble(), 1f / 3));
                    kinds[index] = s;
                    radii[index] = diameter * .5f * (1 + ((float)random.NextDouble() * 2 - 1) * Mathf.Clamp(species?.sizeVariation ?? .12f, 0, .75f));
                    sizes[index] = Mathf.Clamp(diameter / baseDiameter, .25f, 4);
                    maximumSize = Mathf.Max(maximumSize, sizes[index]);
                }
            }

            // Same separation, triangular attraction band and damped soft core as gameplay,
            // extended to 3D. Motors represent a fed colony. A soft outer tether retains food.
            int bodyCount = counts[0] + counts[1] + counts[2];
            for (int step = 0; step < steps; step++)
            {
                Array.Clear(forces, 0, count);
                for (int i = 0; i < count; i++) for (int j = i + 1; j < count; j++)
                {
                    Vector3 delta = positions[j] - positions[i];
                    float distance2 = delta.sqrMagnitude;
                    float coreRange = (radii[i] + radii[j] + gap) * coreMultiple;
                    if (distance2 >= range * range && distance2 >= coreRange * coreRange) continue;
                    float distance = Mathf.Sqrt(Mathf.Max(distance2, .000001f));
                    Vector3 direction = distance2 < .000001f ? Vector3.right : delta / distance;
                    float shared = distance < separation ? -repulsion * (1 - distance / separation) : 0;
                    if (distance < coreRange)
                    {
                        float compression = 1 - distance / coreRange;
                        float closing = Mathf.Max(0, Vector3.Dot(velocities[i] - velocities[j], direction));
                        shared -= coreForce * compression * compression + coreDamping * closing * compression;
                    }
                    float band = distance >= separation && distance < range
                        ? 1 - Mathf.Abs(2 * (distance - separation) / (range - separation) - 1) : 0;
                    int a = kinds[i], b = kinds[j];
                    forces[i] += direction * (shared + attraction[a * 4 + b] * response[a] * band);
                    forces[j] -= direction * (shared + attraction[b * 4 + a] * response[b] * band);
                }
                Vector3 center = Vector3.zero, drift = Vector3.zero;
                for (int i = 0; i < count; i++)
                {
                    float distance = positions[i].magnitude;
                    if (distance > range) forces[i] -= positions[i] * ((distance - range) * maxForce / Mathf.Max(distance, .001f));
                    int s = kinds[i];
                    velocities[i] = Vector3.ClampMagnitude((velocities[i] + Vector3.ClampMagnitude(forces[i], maxForce) * dt) * damping[s], speeds[s]);
                    positions[i] += velocities[i] * dt;
                    if (i < bodyCount) { center += positions[i]; drift += velocities[i]; }
                }
                center /= bodyCount; drift /= bodyCount;
                for (int i = 0; i < count; i++) { positions[i] -= center; velocities[i] -= drift; }
            }
            // Frame the organism, not the distance its uneaten food has wandered. Retain each
            // food particle's direction but bring distant ones to the edge of this small preview.
            float extent = .001f;
            for (int i = 0; i < bodyCount; i++) extent = Mathf.Max(extent, positions[i].magnitude);
            for (int i = 0; i < count; i++)
            {
                positions[i] /= extent;
                if (i >= bodyCount) positions[i] = Vector3.ClampMagnitude(positions[i], 1.08f);
                positions[i] /= 1.08f;
            }
            cachedKey = key;
            return cached = new Shape { centers = positions, sizes = sizes, maximumSize = maximumSize };
        }

        internal static EmergenceProfile.Species Species(EmergenceProfile profile, int index) =>
            profile && profile.species != null && profile.species.Length > index ? profile.species[index] : null;

        public static int Key(EmergenceProfile p, int[] counts, int steps)
        {
            unchecked
            {
                // Increment when formation generation changes, invalidating saved preview data.
                int h = 2 * 31 + steps;
                foreach (int n in counts) h = h * 31 + n;
                if (!p) return h;
                h = h * 31 + p.seed;
                h = h * 31 + p.interactionRadius.GetHashCode(); h = h * 31 + p.separation.GetHashCode();
                h = h * 31 + p.force.GetHashCode(); h = h * 31 + p.repulsion.GetHashCode();
                h = h * 31 + p.drag.GetHashCode(); h = h * 31 + p.speedLimit.GetHashCode();
                h = h * 31 + p.maximumForce.GetHashCode(); h = h * 31 + p.particleGap.GetHashCode();
                h = h * 31 + p.coreRangeMultiplier.GetHashCode(); h = h * 31 + p.coreRepulsion.GetHashCode();
                h = h * 31 + p.coreDamping.GetHashCode();
                if (p.attraction != null) foreach (float f in p.attraction) h = h * 31 + f.GetHashCode();
                for (int i = 0; i < 4; i++)
                {
                    var s = Species(p, i); if (s == null) continue;
                    h = h * 31 + s.count; h = h * 31 + s.diameter.GetHashCode(); h = h * 31 + s.sizeVariation.GetHashCode();
                    h = h * 31 + s.forceMultiplier.GetHashCode(); h = h * 31 + s.dragMultiplier.GetHashCode();
                    h = h * 31 + s.speedMultiplier.GetHashCode();
                }
                return h;
            }
        }
    }
}
