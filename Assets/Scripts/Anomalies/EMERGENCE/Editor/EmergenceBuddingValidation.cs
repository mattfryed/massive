using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Massive.Emergence.Editor
{
    public static class EmergenceBuddingValidation
    {
        static readonly List<string> checks = new();
        static void Check(bool value, string label) { if (!value) throw new Exception(label); checks.Add("PASS " + label); }
        static void Sync(IEmergenceSimulation sim) { if (sim is EmergenceGpuSimulation gpu) gpu.SynchronizeDiagnostics(); }
        static void Write(IEmergenceSimulation sim, EmergenceSimulation.Particle[] state)
        { if (sim is EmergenceGpuSimulation gpu) gpu.SetDiagnosticParticles(state); else Array.Copy(state, sim.Particles, state.Length); }
        static EmergenceProfile Profile()
        {
            var p = ScriptableObject.CreateInstance<EmergenceProfile>();
            for (int i = 0; i < 5; i++) { p.species[i].count = i < 3 ? 4 : 0; p.species[i].diameter = .08f; p.species[i].sizeVariation = 0; }
            p.force = p.repulsion = p.coreRepulsion = p.coreDamping = p.initialSpeed = p.agitation = p.processingPerSecond = 0;
            p.enableSwimming = false; p.minimumBody = 6; p.minimumLocalNeighbours = 1; p.connectionRadius = .5f; p.connectionSizeMultiplier = 20;
            p.maturationSeconds = .1f; p.materialPerNugget = 2; p.fieldSecretionInterval = .05f; p.secretionInterval = 1; p.recognitionInterval = .02f;
            return p;
        }
        static IEmergenceSimulation Create(EmergenceProfile p, bool gpu)
        {
            IEmergenceSimulation sim = gpu ? new EmergenceGpuSimulation(p, new Vector2(8, 4)) : new EmergenceSimulation(p, new Vector2(8, 4), p.seed);
            var state = sim.Particles;
            for (int i = 0; i < state.Length; i++)
            {
                int local = i / 4 * 2 + i % 2;
                state[i].position = new Vector2(i % 4 < 2 ? -2 : 2, 0) + new Vector2(local % 3 * .12f, local / 3 * .12f);
                state[i].velocity = new Vector2(.2f, .07f); state[i].waste = state[i].species == 2 ? 3 : 0;
            }
            Write(sim, state); return sim;
        }
        static void Tick(IEmergenceSimulation sim, int count, float dt, bool allow = true)
        { for (int i = 0; i < count; i++) sim.Step(dt, allow); Sync(sim); }
        static float Material(IEmergenceSimulation sim) => sim.Particles.Sum(p => p.reserve + p.waste) + sim.ExportedMaterial;

        [MenuItem("MASSIVE/EMERGENCE/Validate Reward Budding")]
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run budding model validation in Edit Mode.");
            checks.Clear(); Check(Marshal.SizeOf<EmergenceBud>() == 64, "CPU/GPU bud structure is 64 bytes");
            foreach (bool gpu in new[] { false, true }) foreach (int hz in new[] { 30, 60, 120 })
            {
                var profile = Profile(); string label = (gpu ? "GPU" : "CPU") + " " + hz + " Hz";
                float dt = 1f / hz;
                try
                {
                    using (var sim = Create(profile, gpu))
                    {
                        sim.EmissionSlots = 1; float material = Material(sim);
                        Tick(sim, hz / 3, dt);
                        int slot = Array.FindIndex(sim.RewardBuds, b => b.phase == 1); Check(slot >= 0, label + " starts a bud on mature stored waste");
                        var bud = sim.RewardBuds[slot];
                        Check(sim.Secreted == 0 && sim.ExportedMaterial == 0 && Math.Abs(Material(sim) - material) < .0001f, label + " reserves capacity without exporting or removing material during windup");
                        Check(sim.RewardBuds.Count(b => b.Attached) == 1 && bud.Scale(profile) > 0 && bud.Scale(profile) < 1, label + " one available slot permits only one partly grown bud");
                        Vector2 earlier = bud.position; uint anchorId = bud.anchor;
                        Tick(sim, hz / 6, dt, false); bud = sim.RewardBuds[slot];
                        Check(Vector2.Distance(earlier, bud.position) > .001f && bud.anchor == anchorId, label + " bud follows its moving catalyst");
                        var state = sim.Particles;
                        int hit = Enumerable.Range(0, 4).OrderBy(i => (state[i].position - bud.position).sqrMagnitude).First();
                        state[hit].disruption = 1; state[hit].maturity = 0; Write(sim, state);
                        Tick(sim, 1, dt, false); bud = sim.RewardBuds[slot]; float scale = bud.Scale(profile);
                        Check(bud.phase == 2 && scale > 0, label + " attack on a nearby structural member interrupts the organism's bud");
                        Tick(sim, Mathf.Max(1, hz / 10), dt, false);
                        Check(sim.RewardBuds[slot].Scale(profile) < scale && sim.RewardBuds[slot].Scale(profile) > 0, label + " interrupted bud retracts gradually");
                        Tick(sim, hz / 3, dt, false);
                        Check(sim.Secreted == 0 && sim.PendingCount == 0 && sim.RewardBuds[slot].phase == 4 && sim.RewardBuds[slot].Scale(profile) == 0 && Math.Abs(Material(sim) - material) < .0001f,
                            label + " cancellation reaches zero without losing material or emitting a reward");
                        sim.TryDequeue(out _); state = sim.Particles;
                        for (int i = 0; i < state.Length; i++) { state[i].disruption = 0; state[i].maturity = 0; }
                        Write(sim, state); Tick(sim, hz * 2, dt);
                        Check(sim.Secreted == 1 && sim.PendingCount == 1 && Math.Abs(sim.ExportedMaterial - 2) < .0001f && Math.Abs(Material(sim) - material) < .0001f,
                            label + " recovered organism releases once and exports exactly the configured material");
                        Check(sim.TryDequeue(out var e) && e.budSerial != 0 && e.budSlot >= 0 && !sim.TryDequeue(out _), label + " release retains its bud identity and cannot be dequeued twice");
                    }
                    using (var sim = Create(profile, gpu))
                    {
                        sim.EmissionSlots = 2; Tick(sim, hz / 3, dt);
                        Check(sim.RewardBuds.Count(b => b.phase == 1) == 2, label + " separate organisms can bud concurrently");
                        var state = sim.Particles;
                        for (int i = 0; i < state.Length; i++) state[i].position = new Vector2(i % 4 * .065f, i / 4 * .065f);
                        Write(sim, state); Tick(sim, Mathf.CeilToInt(hz * .04f), dt, false);
                        Check(sim.RewardBuds.Count(b => b.phase == 1) == 1 && sim.RewardBuds.Count(b => b.phase == 2) == 1,
                            label + " merging organisms keep one claim and retract the duplicate");
                        Tick(sim, hz, dt, false);
                        Check(sim.Secreted == 1 && Math.Abs(Material(sim) - 12) < .0001f, label + " merged windups cannot duplicate or destroy stored material");
                    }
                    using (var sim = Create(profile, gpu))
                    {
                        sim.EmissionSlots = 1; Tick(sim, hz / 3, dt); sim.CancelRewardBuds(); Tick(sim, hz, dt, false);
                        Check(sim.Secreted == 0 && Math.Abs(Material(sim) - 12) < .0001f, label + " owner cancellation preserves waste");
                    }
                    using (var sim = Create(profile, gpu))
                    {
                        sim.EmissionSlots = 1; Tick(sim, hz / 3, dt); profile.enableRewardTelegraph = false;
                        Tick(sim, hz, dt);
                        Check(sim.Secreted == 1 && sim.PendingCount == 1 && sim.TryDequeue(out var release) && release.budSerial != 0 && Math.Abs(Material(sim) - 12) < .0001f,
                            label + " disabling the telegraph mid-windup preserves the reserved slot and releases the existing bud once");
                    }
                }
                finally { Object.DestroyImmediate(profile); }
            }
            string result = "PASSED (" + checks.Count + " checks)\n" + string.Join("\n", checks);
            Directory.CreateDirectory("Library/EmergenceValidation"); File.WriteAllText("Library/EmergenceValidation/budding.txt", result); return result;
        }
    }
}
