using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Massive.Emergence.Editor
{
    public static class EmergenceInertValidation
    {
        static readonly List<string> checks = new List<string>();
        static void Check(bool ok, string label) { if (!ok) throw new InvalidOperationException(label); checks.Add("PASS " + label); }
        static EmergenceProfile Profile(params int[] counts)
        {
            var p = ScriptableObject.CreateInstance<EmergenceProfile>();
            for (int i = 0; i < EmergenceProfile.SpeciesCount; i++)
            { p.species[i].count = counts[i]; p.species[i].diameter = .03f; p.species[i].sizeVariation = 0; }
            p.initialSpeed = p.agitation = 0;
            return p;
        }
        static IEmergenceSimulation Create(EmergenceProfile p, bool gpu) => gpu
            ? (IEmergenceSimulation)new EmergenceGpuSimulation(p, new Vector2(4, 4))
            : new EmergenceSimulation(p, new Vector2(4, 4), p.seed);
        static void Write(IEmergenceSimulation s, EmergenceSimulation.Particle[] state)
        { if (s is EmergenceGpuSimulation gpu) gpu.SetDiagnosticParticles(state); else Array.Copy(state, s.Particles, state.Length); }

        [MenuItem("MASSIVE/EMERGENCE/Validate Inert Species")]
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run inert checks in Edit Mode.");
            checks.Clear(); Directory.CreateDirectory("Library/EmergenceValidation");
            try
            {
                Migration();
                foreach (bool gpu in new[] { false, true }) { InertOnly(gpu); BiologicalIsolation(gpu); MatrixResponse(gpu); }
                var p = Profile(8, 8, 8, 8, 8);
                try
                {
                    p.maturationSeconds = 100;
                    for (int i = 0; i < 25; i++) p.attraction[i] = -.5f + i * .035f;
                    using (var cpu = Create(p, false))
                    using (var gpu = new EmergenceGpuSimulation(p, new Vector2(4, 4)))
                    {
                        gpu.SetDiagnosticParticles(cpu.Particles);
                        for (int i = 0; i < 30; i++) { cpu.Step(1f / 60, false); gpu.Step(1f / 60, false); }
                        var actual = gpu.Particles;
                        float error = actual.Select((v, i) => Vector2.Distance(v.position, cpu.Particles[i].position)).Max();
                        Check(error < .0001f, "CPU/GPU agree with all 25 asymmetric matrix entries (max error " + error + ")");
                        Check(actual.Count(v => v.species == 4) == 8, "mixed GPU population retains the fifth species");
                    }
                }
                finally { Object.DestroyImmediate(p); }
                string result = "PASSED (" + checks.Count + " checks)\n" + string.Join("\n", checks);
                File.WriteAllText("Library/EmergenceValidation/inert-species.txt", result); return result;
            }
            catch (Exception e)
            { File.WriteAllText("Library/EmergenceValidation/inert-species.txt", "FAILED\n" + string.Join("\n", checks) + "\n" + e); throw; }
        }

        static void Migration()
        {
            var p = Profile(12, 7, 5, 9, 0);
            try
            {
                p.species = p.species.Take(4).ToArray();
                var oldSpecies = (EmergenceProfile.Species[])p.species.Clone();
                p.attraction = Enumerable.Range(0, 16).Select(i => -.4f + i * .07f).ToArray();
                var oldMatrix = (float[])p.attraction.Clone();
                Check(p.UpgradeLegacySpecies() && p.species.Length == 5 && p.attraction.Length == 25, "legacy four-species profile expands to five and a 5x5 matrix");
                Check(Enumerable.Range(0, 4).All(i => ReferenceEquals(p.species[i], oldSpecies[i])) && p.ParticleCount == 33 && p.species[4].count == 0, "migration preserves existing species and total population");
                Check(Enumerable.Range(0, 16).All(i => p.attraction[(i / 4) * 5 + i % 4] == oldMatrix[i]), "all 16 authored coefficients keep their original row/column meaning");
                Check(Enumerable.Range(0, 4).All(i => p.attraction[20 + i] == 0) && p.attraction[24] < 0, "inert defaults have no cross-species attraction and weak self-repulsion");
                Check(!p.UpgradeLegacySpecies() && p.ConfigurationError() == null, "migration is idempotent and valid");
                p.species[4].count = 17;
                float ratio = 17f / p.ParticleCount;
                EmergenceProfileEditor.ScalePopulation(p, 5000, true);
                Check(p.ParticleCount == 5000 && Mathf.Abs(p.species[4].count - 5000 * ratio) <= 1, "population scaling includes inert particles and preserves proportions");
                var copy = ScriptableObject.CreateInstance<EmergenceProfile>();
                try
                {
                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(p), copy);
                    Check(copy.ParticleCount == p.ParticleCount && copy.attraction.SequenceEqual(p.attraction) && copy.species[4].color == p.species[4].color, "five-species profile survives serialization");
                }
                finally { Object.DestroyImmediate(copy); }
            }
            finally { Object.DestroyImmediate(p); }
        }

        static void InertOnly(bool gpu)
        {
            string name = gpu ? "GPU" : "CPU";
            var p = Profile(0, 0, 0, 0, 64);
            try
            {
                using (var s = Create(p, gpu))
                {
                    for (int i = 0; i < 120; i++) s.Step(1f / 60);
                    var state = s.Particles;
                    Check(state.Length == 64 && state.All(v => v.species == 4 && v.Alive && v.visualScale == 1), name + " seeds and retains a purely inert population");
                    Check(s.BodyCount == 0 && s.Consumed == 0 && s.Replenished == 0 && s.Secreted == 0 && s.StoredMaterial == 0, name + " inert particles never become organisms, food or rewards");
                    Check(state.All(v => !float.IsNaN(v.position.x) && !float.IsInfinity(v.velocity.y)), name + " inert-only motion remains finite");
                }
            }
            finally { Object.DestroyImmediate(p); }
        }

        static void BiologicalIsolation(bool gpu)
        {
            string name = gpu ? "GPU" : "CPU";
            var p = Profile(2, 1, 1, 1, 24);
            try
            {
                p.force = p.repulsion = p.coreRepulsion = p.coreDamping = 0;
                p.maturationSeconds = .1f; p.minimumBody = 5; p.feedingRadius = .3f;
                using (var s = Create(p, gpu))
                {
                    var state = s.Particles;
                    for (int i = 0; i < state.Length; i++) state[i].position = new Vector2(i % 5, i / 5) * .015f;
                    Write(s, state);
                    for (int i = 0; i < 30; i++) s.Step(1f / 60); state = s.Particles;
                    Check(s.BodyCount == 0 && s.Consumed == 0, name + " inert neighbours cannot pad an undersized organism");
                    p.minimumBody = 4;
                    for (int i = 0; i < 120; i++) s.Step(1f / 60); state = s.Particles;
                    Check(s.BodyCount == 1 && s.Consumed == 1, name + " mature catalysts consume only the nutrient alongside nearby inert particles");
                    Check(state.Where(v => v.species == 4).All(v => v.Alive && v.feedingPhase == 0 && v.reserve == 0 && v.waste == 0 && v.maturity == 0), name + " inert particles retain no biological state or material budget");
                }
            }
            finally { Object.DestroyImmediate(p); }
        }

        static void MatrixResponse(bool gpu)
        {
            string name = gpu ? "GPU" : "CPU";
            var p = Profile(1, 0, 0, 0, 1);
            try
            {
                p.repulsion = p.coreRepulsion = p.coreDamping = 0; p.separation = .05f;
                for (int mode = 0; mode < 2; mode++)
                {
                    Array.Clear(p.attraction, 0, p.attraction.Length);
                    p.attraction[mode == 0 ? 4 : 20] = mode == 0 ? .9f : -.7f;
                    using (var s = Create(p, gpu))
                    {
                        var state = s.Particles; state[0].position = new Vector2(-.25f, 0); state[1].position = new Vector2(.25f, 0); Write(s, state);
                        s.Step(1f / 60, false); state = s.Particles;
                        Check(mode == 0 ? state[0].velocity.x > 0 && state[1].velocity == Vector2.zero : state[1].velocity.x > 0 && state[0].velocity == Vector2.zero,
                            name + (mode == 0 ? " fifth column independently controls attraction toward inert particles" : " fifth row independently controls inert response"));
                    }
                }
            }
            finally { Object.DestroyImmediate(p); }
        }
    }
}
