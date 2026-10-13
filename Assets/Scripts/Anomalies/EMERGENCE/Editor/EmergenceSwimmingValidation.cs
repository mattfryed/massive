using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Massive.Emergence.Editor
{
    public static class EmergenceSwimmingValidation
    {
        static readonly List<string> checks = new List<string>();
        static void Check(bool ok, string message)
        { if (!ok) throw new InvalidOperationException(message); checks.Add("PASS " + message); }

        static EmergenceProfile Profile()
        {
            var p = ScriptableObject.CreateInstance<EmergenceProfile>();
            int[] counts = { 2, 1, 1, 1, 1 };
            for (int i = 0; i < counts.Length; i++)
            { p.species[i].count = counts[i]; p.species[i].diameter = .03f; p.species[i].sizeVariation = 0; }
            p.initialSpeed = p.agitation = p.force = p.repulsion = p.coreRepulsion = p.coreDamping = 0;
            p.minimumBody = 4; p.minimumLocalNeighbours = 3; p.connectionRadius = .4f; p.connectionSizeMultiplier = 20;
            p.maturationSeconds = .2f; p.recognitionInterval = .02f; p.feedingRadius = .18f;
            p.drag = 25; p.speedLimit = .5f; p.swimSpeedFraction = .8f;
            p.swimWanderStrength = p.swimFoodBias = p.swimPulseAmount = 0;
            return p;
        }
        static IEmergenceSimulation Create(EmergenceProfile p, bool gpu)
        {
            IEmergenceSimulation sim = gpu ? (IEmergenceSimulation)new EmergenceGpuSimulation(p, new Vector2(8, 8)) : new EmergenceSimulation(p, new Vector2(8, 8), p.seed);
            var state = sim.Particles;
            for (int i = 0; i < 4; i++) { state[i].position = new Vector2(i % 2, i / 2) * .05f; state[i].velocity = Vector2.zero; }
            state[4].position = new Vector2(6, 6); state[5].position = new Vector2(-6, -6);
            Set(sim, state); return sim;
        }
        static void Set(IEmergenceSimulation sim, EmergenceSimulation.Particle[] state)
        { if (sim is EmergenceGpuSimulation gpu) gpu.SetDiagnosticParticles(state); else Array.Copy(state, sim.Particles, state.Length); }
        static void Advance(IEmergenceSimulation sim, int steps, int hz = 60)
        { for (int i = 0; i < steps; i++) sim.Step(1f / hz, false); if (sim is EmergenceGpuSimulation gpu) gpu.SynchronizeDiagnostics(); }
        static Vector2 Center(IEmergenceSimulation sim) => sim.Particles.Take(4).Aggregate(Vector2.zero, (v, p) => v + p.position) / 4;
        static Vector2 Velocity(IEmergenceSimulation sim) => sim.Particles.Take(4).Aggregate(Vector2.zero, (v, p) => v + p.velocity) / 4;

        [MenuItem("MASSIVE/EMERGENCE/Validate Organism Swimming")]
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run model checks in Edit Mode.");
            checks.Clear();
            try
            {
                foreach (int hz in new[] { 30, 60, 120 })
                {
                    Vector2 cpuPosition = Vector2.zero;
                    foreach (bool gpu in new[] { false, true })
                    {
                        string label = (gpu ? "GPU" : "CPU") + " " + hz + "Hz";
                        var p = Profile();
                        try
                        {
                            using (var sim = Create(p, gpu))
                            {
                                Vector2 start = Center(sim);
                                Advance(sim, hz / 10, hz);
                                Check(Vector2.Distance(start, Center(sim)) < .00001f, label + " remains passive before maturation");
                                Advance(sim, hz * 3, hz);
                                Vector2 end = Center(sim);
                                Check(Vector2.Distance(start, end) > .8f && Velocity(sim).magnitude > .35f, label + " swims with no food, reserves or particle attraction");
                                var state = sim.Particles;
                                Check(Mathf.Abs(Vector2.Distance(state[0].position, state[3].position) - Mathf.Sqrt(.005f)) < .0001f, label + " coherent swimming preserves body shape");
                                Check(state[4].position == new Vector2(6, 6) && state[5].position == new Vector2(-6, -6), label + " nutrients and inert particles receive no propulsion");
                                Check(sim.Consumed == 0 && sim.Secreted == 0 && sim.StoredMaterial == 0, label + " swimming creates no material or rewards");
                                if (!gpu) cpuPosition = end;
                                else Check(Vector2.Distance(cpuPosition, end) < .001f, label + " matches CPU swimming trajectory");
                                p.enableSwimming = false;
                                Advance(sim, hz * 3, hz);
                                Check(Velocity(sim).magnitude < .003f, label + " disabling swimming eases propulsion to zero");
                            }
                            p.enableSwimming = true; p.minimumBody = 5;
                            using (var sim = Create(p, gpu))
                            { Vector2 start = Center(sim); Advance(sim, hz * 2, hz); Check(Vector2.Distance(start, Center(sim)) < .00001f, label + " undersized clusters cannot swim"); }
                        }
                        finally { Object.DestroyImmediate(p); }
                    }
                }
                foreach (bool gpu in new[] { false, true })
                {
                    string label = gpu ? "GPU" : "CPU"; var p = Profile();
                    try
                    {
                        using (var empty = Create(p, gpu))
                        using (var fed = Create(p, gpu))
                        {
                            var state = fed.Particles; state[3].reserve = 6; Set(fed, state);
                            Advance(empty, 240); Advance(fed, 240);
                            Check(Vector2.Distance(Center(empty), Center(fed)) < .0001f, label + " cruising is independent of food reserves");
                        }
                        using (var sim = Create(p, gpu))
                        {
                            Advance(sim, 120); Vector2 direction = Velocity(sim).normalized;
                            var state = sim.Particles; state[4].position = Center(sim) + direction * .6f; Set(sim, state);
                            Advance(sim, 150);
                            Check(sim.Consumed == 1, label + " moving organism captures food along its path");
                            Check(Mathf.Abs(sim.Particles.Sum(v => v.reserve + v.waste) - sim.Consumed) < .001f, label + " moving feeding preserves material accounting");
                            p.minimumBody = 5; Advance(sim, 180);
                            Check(Velocity(sim).magnitude < .003f && sim.BodyCount == 0, label + " loss of eligibility stops propulsion");
                            p.minimumBody = 4; Advance(sim, 180);
                            Check(Velocity(sim).magnitude > .35f, label + " reformed eligible organism resumes swimming");
                        }
                        p.swimWanderStrength = .55f;
                        using (var sim = Create(p, gpu))
                        {
                            Advance(sim, 120); Vector2 first = Velocity(sim);
                            Advance(sim, 240);
                            Check(Vector2.Angle(first, Velocity(sim)) > 5 && Velocity(sim).magnitude > .35f, label + " wanders and turns without food");
                        }
                        p.swimWanderStrength = 0; p.swimFoodBias = 1;
                        using (var seek = Create(p, gpu))
                        using (var control = Create(p, gpu))
                        {
                            Advance(seek, 120); Advance(control, 120);
                            Vector2 dir = Velocity(seek).normalized, food = Center(seek) + new Vector2(-dir.y, dir.x) * 1.5f;
                            var state = seek.Particles; state[4].position = food; Set(seek, state);
                            Advance(seek, 240); Advance(control, 240);
                            Check(Vector2.Distance(Center(seek), food) < Vector2.Distance(Center(control), food) - .6f, label + " free nutrients bias its course toward food");
                        }
                        using (var growing = Create(p, gpu))
                        using (var control = Create(p, gpu))
                        {
                            var state = growing.Particles; state[4].position = Vector2.right;
                            state[4].feedingPhase = 3; state[4].visualScale = 0; state[5].position = Vector2.up; Set(growing, state);
                            p.particleGrowSeconds = 100;
                            Advance(growing, 180); Advance(control, 180);
                            Check(Vector2.Distance(Center(growing), Center(control)) < .001f, label + " growing nutrients and inert particles do not attract food steering");
                        }
                        p.swimFoodBias = 0;
                        using (var sim = Create(p, gpu))
                        {
                            Advance(sim, 120); Vector2 dir = Velocity(sim).normalized;
                            Vector2 nearWall = dir * (7.3f / Mathf.Max(Mathf.Abs(dir.x), Mathf.Abs(dir.y)));
                            var state = sim.Particles; Vector2 offset = nearWall - Center(sim);
                            for (int i = 0; i < 4; i++) state[i].position += offset;
                            Set(sim, state); Advance(sim, 600);
                            state = sim.Particles;
                            Check(state.All(v => !float.IsNaN(v.position.x) && Mathf.Abs(v.position.x) < 8 && Mathf.Abs(v.position.y) < 8), label + " remains finite and inside arena near walls");
                            Check(Vector2.Dot(Center(sim) - nearWall, dir) < -.3f, label + " turns away from wall and swims inward");
                        }
                        p.species[0].count = 4; p.species[1].count = p.species[2].count = 2; p.species[3].count = p.species[4].count = 0;
                        p.swimTurnSpeed = 0;
                        using (IEmergenceSimulation sim = gpu ? (IEmergenceSimulation)new EmergenceGpuSimulation(p, new Vector2(8, 8)) : new EmergenceSimulation(p, new Vector2(8, 8), p.seed))
                        {
                            var state = sim.Particles;
                            for (int i = 0; i < 8; i++) state[i].position = new Vector2(i % 4, i / 4) * .05f;
                            Set(sim, state); Advance(sim, 180); state = sim.Particles;
                            Vector2 inherited = state[0].velocity;
                            int[] left = { 0, 1, 4, 6 }, right = { 2, 3, 5, 7 };
                            for (int i = 0; i < 4; i++)
                            {
                                Vector2 local = new Vector2(i % 2, i / 2) * .05f;
                                state[left[i]].position = local - Vector2.right;
                                state[right[i]].position = local + Vector2.right;
                            }
                            Set(sim, state); Advance(sim, 3); state = sim.Particles;
                            Check(sim.BodyCount == 2 && Vector2.Angle(inherited, state[2].velocity) < .1f, label + " viable split inherits swimming direction after its root changes");
                            for (int i = 0; i < 8; i++) state[i].position = new Vector2(i % 4, i / 4) * .05f;
                            Set(sim, state); Advance(sim, 3); state = sim.Particles;
                            Check(sim.BodyCount == 1 && Vector2.Angle(inherited, state[0].velocity) < .1f && sim.StoredMaterial == 0, label + " merging preserves direction and creates no material");
                            for (int i = 0; i < 8; i++) { state[i].disruption = 2; state[i].maturity = 0; }
                            Set(sim, state); Advance(sim, 90);
                            Check(Velocity(sim).magnitude < .04f, label + " disruption suppresses autonomous swimming");
                            Advance(sim, 150);
                            Check(Velocity(sim).magnitude > .35f, label + " swimming returns after disruption and rematuration");
                        }
                    }
                    finally { Object.DestroyImmediate(p); }
                }
                var invalid = Profile();
                try { invalid.swimResponseSeconds = 0; Check(invalid.ConfigurationError() != null, "rejects zero swimming response time"); invalid.swimResponseSeconds = .6f; invalid.swimFoodBias = float.NaN; Check(invalid.ConfigurationError() != null, "rejects nonfinite swimming settings"); }
                finally { Object.DestroyImmediate(invalid); }
                return Save("PASS");
            }
            catch (Exception exception) { Save("FAIL " + exception); throw; }
        }
        static string Save(string result)
        {
            string report = result + "\n" + string.Join("\n", checks);
            Directory.CreateDirectory("Library/EmergenceValidation"); File.WriteAllText("Library/EmergenceValidation/swimming.txt", report);
            return report;
        }
    }
}
