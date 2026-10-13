using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Massive.Emergence.Editor
{
    public static class EmergenceFeedingValidation
    {
        static readonly List<string> checks = new List<string>();
        static void Check(bool value, string label)
        { if (!value) throw new InvalidOperationException(label); checks.Add("PASS " + label); }

        static EmergenceProfile Profile(int structure, int motor, int catalyst, int nutrient)
        {
            var p = ScriptableObject.CreateInstance<EmergenceProfile>();
            // This fixture measures approach against a stationary catalyst; swimming has its own moving-feeding checks.
            p.enableSwimming = false;
            int[] counts = { structure, motor, catalyst, nutrient };
            for (int i = 0; i < 4; i++) { p.species[i].count = counts[i]; p.species[i].diameter = .03f; p.species[i].sizeVariation = 0; }
            p.initialSpeed = p.agitation = p.force = p.repulsion = p.coreRepulsion = p.coreDamping = 0;
            p.minimumBody = 4; p.maturationSeconds = .1f; p.feedingRadius = .2f;
            p.processingPerSecond = 10; p.reservePerCatalyst = 1; p.nutrientReturnSeconds = 1;
            return p;
        }
        static IEmergenceSimulation Create(EmergenceProfile p, bool gpu) => gpu
            ? (IEmergenceSimulation)new EmergenceGpuSimulation(p, new Vector2(8, 4))
            : new EmergenceSimulation(p, new Vector2(8, 4), p.seed);
        static void Write(IEmergenceSimulation s, EmergenceSimulation.Particle[] state)
        {
            if (s is EmergenceGpuSimulation gpu) gpu.SetDiagnosticParticles(state);
            else Array.Copy(state, s.Particles, state.Length);
        }

        [MenuItem("MASSIVE/EMERGENCE/Validate Visible Feeding")]
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run feeding validation in Edit Mode.");
            checks.Clear(); Directory.CreateDirectory("Library/EmergenceValidation");
            try
            {
                foreach (bool gpu in new[] { false, true })
                {
                    string backend = gpu ? "GPU" : "CPU";
                    var p = Profile(8, 4, 4, 0);
                    try
                    {
                        using (var s = Create(p, gpu))
                        {
                            var state = s.Particles;
                            for (int i = 0; i < state.Length; i++) state[i].position = new Vector2((i - 8) * .12f, 0);
                            Write(s, state); s.Step(1f / 60, false); state = s.Particles;
                            Check(s.BodyCount == 0, backend + " rejects a sparse chain despite close consecutive links");
                        }
                        p.minimumLocalNeighbours = 0;
                        using (var s = Create(p, gpu))
                        {
                            var state = s.Particles;
                            for (int i = 0; i < state.Length; i++) state[i].position = new Vector2((i - 8) * .2f, 0);
                            Write(s, state); s.Step(1f / 60, false); state = s.Particles;
                            Check(s.BodyCount == 0, backend + " caps organism links by visible particle size");
                        }
                    }
                    finally { Object.DestroyImmediate(p); }
                    foreach (int hz in new[] { 30, 60, 120 }) Lifecycle(gpu, hz);
                }
                string result = "PASSED (" + checks.Count + " checks)\n" + string.Join("\n", checks);
                File.WriteAllText("Library/EmergenceValidation/visible-feeding.txt", result); return result;
            }
            catch (Exception e)
            { File.WriteAllText("Library/EmergenceValidation/visible-feeding.txt", "FAILED\n" + string.Join("\n", checks) + "\n" + e); throw; }
        }

        static void Lifecycle(bool gpu, int hz)
        {
            string label = (gpu ? "GPU " : "CPU ") + hz + " Hz: ";
            var p = Profile(2, 1, 1, 1); float dt = 1f / hz;
            try
            {
                using (var s = Create(p, gpu))
                {
                    var state = s.Particles;
                    state[0].position = new Vector2(-.06f, 0); state[1].position = new Vector2(-.06f, .06f);
                    state[2].position = new Vector2(0, .06f); state[3].position = Vector2.zero; state[4].position = new Vector2(.15f, 0);
                    Write(s, state);
                    int tick = 0;
                    do { s.Step(dt, false); state = s.Particles; } while (state[4].feedingPhase == 0 && ++tick < hz);
                    Check(state[4].feedingPhase == 1 && state[4].feedingTarget == 3 && s.Consumed == 1, label + "compact mature body captures once with an explicit catalyst target");
                    Check(state[4].visualScale == 1 && Mathf.Abs(state[4].position.x - .15f) < .00001f, label + "capture does not hide or teleport the nutrient");
                    float previousDistance = .15f; bool moved = false;
                    while (state[4].feedingPhase == 1 && ++tick < hz * 3)
                    {
                        s.Step(dt, false); state = s.Particles; float distance = state[4].position.magnitude;
                        if (distance > previousDistance + .00001f || state[4].visualScale != 1) throw new Exception(label + "approach is not continuous at full size");
                        moved |= distance < .13f && distance > .04f; previousDistance = distance;
                    }
                    Check(moved && state[4].feedingPhase == 2 && Mathf.Abs(previousDistance - .03f) < .00001f, label + "visible approach reaches the catalyst surface before shrinking");
                    float scale = 1; bool intermediate = false;
                    while (state[4].feedingPhase == 2 && ++tick < hz * 4)
                    {
                        s.Step(dt, false); state = s.Particles;
                        if (state[4].visualScale > scale || state[4].visualScale < 0) throw new Exception(label + "shrink scale is not monotonic");
                        scale = state[4].visualScale; intermediate |= scale > .2f && scale < .8f;
                    }
                    Check(intermediate && scale == 0 && !state[4].Alive, label + "death eases through intermediate scales to exactly zero");
                    Check(state[4].returnIn >= p.nutrientReturnSeconds - dt * 1.1f, label + "replenishment delay begins after shrinking finishes");
                    while (!state[4].Alive && ++tick < hz * 6) { s.Step(dt, false); state = s.Particles; }
                    Check(state[4].feedingPhase == 3 && state[4].visualScale == 0 && s.Replenished == 1, label + "respawn begins at zero size");
                    // Place the newborn inside the hungry catalyst's range to exercise growth protection.
                    state[4].position = new Vector2(.1f, 0); Write(s, state); scale = 0; intermediate = false;
                    while (state[4].feedingPhase == 3 && ++tick < hz * 8)
                    {
                        s.Step(dt, false); state = s.Particles;
                        if (state[4].visualScale < scale || state[4].visualScale > 1 || s.Consumed != 1) throw new Exception(label + "growth was interrupted or consumed");
                        scale = state[4].visualScale; intermediate |= scale > .2f && scale < .8f;
                    }
                    Check(intermediate && scale == 1 && state[4].Alive, label + "respawn grows smoothly to full size without immediate recapture");
                    for (int i = 0; i < 3 && s.Consumed < 2; i++) { s.Step(dt, false); state = s.Particles; }
                    Check(s.Consumed == 2 && state[4].feedingPhase == 1 && state[4].visualScale == 1, label + "fully grown nutrient becomes edible again without popping");
                    Check(Mathf.Abs(state.Sum(v => v.reserve + v.waste) - s.Consumed) < .0001f, label + "animated consumption preserves once-only material accounting");
                    Check(state.All(v => Mathf.Abs(v.radius - .015f) < .000001f), label + "animation leaves authored physical radii unchanged");
                }
            }
            finally { Object.DestroyImmediate(p); }
        }
    }
}
