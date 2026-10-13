using System;
using System.Collections.Generic;
using UnityEngine;

namespace Massive.Emergence
{
    // Fine placement bins avoid scanning the much larger attraction neighbourhood at startup.
    static class EmergenceParticleSeed
    {
        public static EmergenceSimulation.Particle[] Create(EmergenceProfile profile, Vector2 half)
        {
            var particles = new EmergenceSimulation.Particle[profile.ParticleCount];
            var random = new System.Random(profile.seed);
            float cell = Mathf.Max(.005f, 2 * profile.MaximumRadius + profile.particleGap + .0004f);
            int columns = Mathf.CeilToInt(2 * half.x / cell) + 1;
            var bins = new Dictionary<long, List<int>>();
            float Range(float a, float b) => Mathf.Lerp(a, b, (float)random.NextDouble());
            long Key(int x, int y) => (long)y * columns + x;
            int type = 0, end = profile.species[0].count;
            for (int i = 0; i < particles.Length; i++)
            {
                while (i >= end && type < EmergenceProfile.SpeciesCount - 1) end += profile.species[++type].count;
                var species = profile.species[type];
                float radius = species.diameter * .5f * Range(1 - species.sizeVariation, 1 + species.sizeVariation);
                float angle = Range(0, 2 * Mathf.PI), speed = Mathf.Sqrt(Range(0, 1)) * profile.initialSpeed;
                var p = new EmergenceSimulation.Particle { species = type, radius = radius, visualScale = 1, feedingTarget = -1,
                    velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed };
                Vector2 extent = half * profile.initialCoverage - Vector2.one * (radius + .0002f);
                bool placed = false;
                if (extent.x <= 0 || extent.y <= 0) throw new ArgumentException("Particle sizes do not fit the starting area.");
                for (int attempt = 0; attempt < 4096 && !placed; attempt++)
                {
                    p.position = new Vector2(Range(-extent.x, extent.x), Range(-extent.y, extent.y));
                    int x = (int)((p.position.x + half.x) / cell), y = (int)((p.position.y + half.y) / cell);
                    bool clear = true;
                    for (int yy = y - 1; yy <= y + 1 && clear; yy++)
                        for (int xx = x - 1; xx <= x + 1 && clear; xx++)
                            if (bins.TryGetValue(Key(xx, yy), out var neighbours))
                                foreach (int j in neighbours)
                                {
                                    float spacing = radius + particles[j].radius + profile.particleGap + .0002f;
                                    if ((p.position - particles[j].position).sqrMagnitude < spacing * spacing) { clear = false; break; }
                                }
                    if (!clear) continue;
                    long key = Key(x, y);
                    if (!bins.TryGetValue(key, out var bin)) bins.Add(key, bin = new List<int>(4));
                    bin.Add(i); particles[i] = p; placed = true;
                }
                if (!placed) throw new ArgumentException("Particle sizes/counts do not fit the starting area. Reduce diameter/gap or increase coverage.");
            }
            return particles;
        }
    }
}
