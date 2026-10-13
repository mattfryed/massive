using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Massive.Emergence.Editor
{
    public static partial class EmergenceValidation
    {
        struct SpacingSample
        {
            public int overlappingParticles;
            public float maximumOverlap;
        }

        // Independent geometric audit, outside the timed solver. Soft spacing allows transient overlaps.
        static SpacingSample MeasureSpacing(IEmergenceSimulation sim)
        {
            var particles = sim.Particles;
            var overlaps = new bool[particles.Length];
            var result = new SpacingSample();
            for (int i = 0; i < particles.Length; i++)
            {
                var p = particles[i]; if (!p.Alive) continue;
                for (int j = i + 1; j < particles.Length; j++)
                {
                    var q = particles[j]; if (!q.Alive) continue;
                    float dx = p.position.x - q.position.x, dy = p.position.y - q.position.y;
                    float distance2 = dx * dx + dy * dy, radii = p.radius + q.radius;
                    if (distance2 >= radii * radii) continue;
                    overlaps[i] = overlaps[j] = true;
                    result.maximumOverlap = Mathf.Max(result.maximumOverlap, radii - Mathf.Sqrt(distance2));
                }
            }
            foreach (bool overlap in overlaps) if (overlap) result.overlappingParticles++;
            return result;
        }

        static void ValidateSoftSpacing(EmergenceProfile source)
        {
            var p = Object.Instantiate(source);
            try
            {
                foreach (var species in p.species) { species.count = 0; species.sizeVariation = 0; }
                p.species[0].count = 2; p.species[0].diameter = .1f;
                p.agitation = p.initialSpeed = p.force = p.repulsion = 0;
                p.coreDamping = 0; p.coreRepulsion = 80; p.coreRangeMultiplier = 3;
                p.maximumForce = 45;
                foreach (int hz in new[] { 30, 60, 120 })
                {
                    var sim = Pair(p, .025f);
                    for (int i = 0; i < hz; i++) sim.Step(1f / hz);
                    Check(Vector2.Distance(sim.Particles[0].position, sim.Particles[1].position) > .1f,
                        "repulsive core separates compressed particles without species forces at " + hz + " Hz");
                }
                var coincident = Pair(p, 0);
                for (int i = 0; i < 60; i++) coincident.Step(1f / 60);
                Check(Vector2.Distance(coincident.Particles[0].position, coincident.Particles[1].position) > .1f,
                    "coincident centers recover with finite outward motion");

                p.coreRepulsion = 0;
                var passive = Pair(p, .025f); passive.Step(1f / 60);
                Check(Vector2.Distance(passive.Particles[0].position, passive.Particles[1].position) < .1f,
                    "disabling repulsion permits overlap: no hidden contact correction");
                p.coreRepulsion = 80;
                var small = Pair(p, .25f); small.Step(1f / 60);
                p.species[0].diameter = .2f;
                var large = Pair(p, .25f); large.Step(1f / 60);
                Check(large.Particles[0].velocity.sqrMagnitude > small.Particles[0].velocity.sqrMagnitude,
                    "larger visible particles produce a larger repulsive core");

                p.interactionRadius = .3f; p.separation = .1f;
                var beyondAttraction = Pair(p, .25f); beyondAttraction.Step(1f / 60);
                Check(beyondAttraction.Particles[0].velocity.x < 0,
                    "size-based spacing works beyond the species interaction radius");

                p.coreRepulsion = 0; p.coreDamping = 0;
                var undamped = Pair(p, .1f, 1); undamped.Step(1f / 60);
                p.coreDamping = 12;
                var damped = Pair(p, .1f, 1); damped.Step(1f / 60);
                Check(damped.Particles[0].velocity.x < undamped.Particles[0].velocity.x,
                    "core damping reduces closing speed");
                var separating = Pair(p, .1f, -1); separating.Step(1f / 60);
                p.coreDamping = 0;
                var separatingControl = Pair(p, .1f, -1); separatingControl.Step(1f / 60);
                Check(separating.Particles[0].velocity == separatingControl.Particles[0].velocity,
                    "core damping leaves separating motion alone");

                var wall = Pair(p, 1);
                wall.Particles[0].position = new Vector2(3.899f, 0);
                wall.Particles[0].velocity = Vector2.right * 2;
                wall.Step(1f / 60);
                Check(wall.Particles[0].position.x <= 3.9f && wall.Particles[0].velocity.x < 0,
                    "arena walls still contain and bounce particles");
                p.coreRangeMultiplier = float.NaN;
                Check(p.ConfigurationError() != null, "invalid core settings report a configuration error");
            }
            finally { Object.DestroyImmediate(p); }
        }

        static EmergenceSimulation Pair(EmergenceProfile p, float halfDistance, float closingSpeed = 0)
        {
            var sim = new EmergenceSimulation(p, new Vector2(4, 4), 1729);
            sim.Particles[0].position = Vector2.left * halfDistance;
            sim.Particles[1].position = Vector2.right * halfDistance;
            sim.Particles[0].velocity = Vector2.right * closingSpeed;
            sim.Particles[1].velocity = Vector2.left * closingSpeed;
            return sim;
        }

        [MenuItem("MASSIVE/EMERGENCE/Benchmark Soft Spacing")]
        public static string BenchmarkSpacing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Benchmark in Edit Mode.");
            var profile = AssetDatabase.LoadAssetAtPath<EmergenceProfile>(EmergenceLevelSetup.ProfilePath);
            var sim = new EmergenceSimulation(profile, new Vector2(13.6f, 5.6f), profile.seed);
            var times = new List<double>(); var watch = new System.Diagnostics.Stopwatch(); long pairs = 0;
            // Match the recorded 60 Hz hard-contact baseline: 5 seconds warmup + 5 seconds measurement.
            for (int i = 0; i < 600; i++)
            {
                watch.Restart(); sim.Step(1f / 60); watch.Stop();
                if (i >= 300) { times.Add(watch.Elapsed.TotalMilliseconds); pairs += sim.PairChecks; }
                while (sim.TryDequeue(out _)) { }
            }
            times.Sort(); var spacing = MeasureSpacing(sim);
            string result = "Soft spacing; Editor isolated solver, 60 Hz, seed " + profile.seed + ", particles=" + profile.ParticleCount +
                "\nMedian ms=" + times[150].ToString("F3") + "; p95 ms=" + times[285].ToString("F3") + "; mean accepted pairs=" + pairs / 300 +
                "\nFinal overlapping particles=" + spacing.overlappingParticles + "; max visible overlap=" + spacing.maximumOverlap.ToString("F5") +
                "; groups=" + sim.BodyCount + "; consumed=" + sim.Consumed + "; secreted=" + sim.Secreted;
            Directory.CreateDirectory(Folder); File.WriteAllText(Folder + "/soft-spacing-after.txt", result);
            return result;
        }
    }
}
