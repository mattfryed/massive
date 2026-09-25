using System;
using System.Collections.Generic;
using System.Linq;
using Massive.Multiplier;
using UnityEditor;
using UnityEngine;

namespace Massive.Singularity.Editor
{
    /// <summary>Explicit, Play-only check. Only runtime objects are moved/awarded;
    /// it never changes source presets or saves the scene.</summary>
    public static class SingularityEncounterPlayValidation
    {
        public static string Result { get; private set; } = "Not run";
        private static AmplifierResonanceSpawner cycle;
        private static SingularityAmplifierEncounter bridge;
        private static SingularityAmplifierAdapter core;
        private static string settings;
        private static int stage, firstMultiplier, secondMultiplier;
        private static float savedTimeScale, formationProgress;
        private static double began, stageBegan;
        private static bool sawRespawnDelay;
        private static readonly List<string> checks = new List<string>();

        public static string Begin()
        {
            if (!Application.isPlaying || cycle != null) throw new InvalidOperationException("Requires Play Mode and no running check.");
            bridge = UnityEngine.Object.FindFirstObjectByType<SingularityAmplifierEncounter>();
            if (bridge == null || bridge.spawner == null) throw new InvalidOperationException("Integrate the scene first.");
            cycle = bridge.spawner;
            settings = JsonUtility.ToJson(cycle); savedTimeScale = Time.timeScale;
            cycle.StopCycle();
            cycle.UseSharedSettings = false;
            cycle.initialSpawnDelay = 0f; cycle.respawnDelay = .4f; cycle.respawnDelayVariation = Vector2.zero;
            cycle.maximumActiveSeconds = 120f;
            cycle.fixedRandomSeed = true; cycle.randomSeed = 345;
            firstMultiplier = cycle.scoreService.GetTeamAmplifierMultiplier(1);
            secondMultiplier = cycle.scoreService.GetTeamAmplifierMultiplier(2);
            checks.Clear(); stage = 0; sawRespawnDelay = false;
            began = stageBegan = EditorApplication.timeSinceStartup;
            Time.timeScale = 1f;
            cycle.StartCycle();
            Result = "Running SINGULARITY spawn, folded traversal and real goal capture checks";
            EditorApplication.update += Tick;
            return Result;
        }

        private static void Tick()
        {
            try
            {
                if (!Application.isPlaying || cycle == null) { Finish("Interrupted"); return; }
                double now = EditorApplication.timeSinceStartup;
                if (now - began > 100) throw new TimeoutException(cycle.Phase + ": " + cycle.Status);
                switch (stage)
                {
                    case 0:
                        if (cycle.Phase != AmplifierEncounterPhase.FormingPattern) return;
                        Check(cycle.ActiveCore == null && !cycle.ActivePattern.InteractionEnabled,
                            "Formation has no Core and no active pattern collisions");
                        formationProgress = cycle.ActiveManifestation.NormalizedProgress;
                        Time.timeScale = 0f; stageBegan = now; stage = 1; break;
                    case 1:
                        if (now - stageBegan < .25) return;
                        Check(Mathf.Abs(cycle.ActiveManifestation.NormalizedProgress - formationProgress) < .001f,
                            "Pause freezes particle formation");
                        Time.timeScale = 1f; stage = 2; break;
                    case 2:
                        if (!ReadyCore()) return;
                        core = cycle.ActiveCore.GetComponent<SingularityAmplifierAdapter>();
                        Check(core != null && core.Surface == bridge.surface, "Spawned Core has the real folded-surface adapter");
                        Check(cycle.ActiveManifestation.IsIdle && cycle.ActivePattern.InteractionEnabled,
                            "Core appears only after Resonance settles");
                        Check(bridge.grid.ActiveResonancePattern == cycle.ActivePattern && bridge.grid.ActiveResonanceSampleCount > 0,
                            "Live pattern feeds the folded grid attraction");
                        var previous = cycle.spawnRegion.ignoredColliders;
                        cycle.spawnRegion.ignoredColliders = cycle.ActiveCore.GetComponentsInChildren<Collider>();
                        string reason;
                        bool safe = cycle.spawnRegion.IsValidSpawnPoint(cycle.ActiveCore.transform.position,
                            cycle.CorePlacementRadius, cycle.ActivePattern, out reason);
                        cycle.spawnRegion.ignoredColliders = previous;
                        Check(safe, "Actual neutral spawn avoids black hole, players, goals and Resonance: " + reason);
                        Check(CountCores() == 1 && !bridge.editPreview.gameObject.activeSelf,
                            "One owned Core; edit preview suspended");
                        // Real Rigidbody motion, starting just before the upper fold.
                        core.SetSurfacePosition(new Vector2(-6f, bridge.surface.FrontHeight - .12f));
                        core.Core.Body.linearVelocity = bridge.surface.transform.TransformVector(new Vector3(0f, 0f, 6f));
                        stage = 3; stageBegan = now; break;
                    case 3:
                        if (core.SurfacePosition.y <= bridge.surface.FrontHeight + .1f) return;
                        Check(core.Core.Body.linearVelocity.z > 0f && core.SurfacePosition.y < bridge.surface.RearStart,
                            "Core physically enters upper fold with forward momentum");
                        Check(Vector3.Distance(core.VisualRoot.position, core.RenderWorldPosition) < .02f,
                            "Core visual follows the bend while physics stays on chart");
                        core.SetSurfacePosition(new Vector2(-6f, bridge.surface.RearStart + bridge.surface.FrontHeight * bridge.surface.RearScale * .5f));
                        core.RefreshPresentation();
                        Check(core.RearWeight > .99f && core.RenderWorldPosition.y < bridge.surface.transform.position.y - 2f,
                            "Rear Core renders below front face");
                        Check(Mathf.Abs(core.Core.Body.position.z - core.RenderWorldPosition.z) > 5f,
                            "Rear physics is separate from overlapping front actors and goals");
                        core.SetSurfacePosition(new Vector2(-6f, bridge.surface.LoopLength - .08f));
                        core.Core.Body.linearVelocity = bridge.surface.transform.TransformVector(new Vector3(0f, 0f, 5f));
                        stage = 4; break;
                    case 4:
                        if (core.SurfacePosition.y > 1f) return;
                        Check(core.Core.Body.linearVelocity.z > 0f, "Bottom seam wraps into front without reversing momentum");
                        Deposit(1); stage = 5; break;
                    case 5:
                        if (cycle.CapturesObserved < 1) return;
                        Check(cycle.scoreService.GetTeamAmplifierMultiplier(1) > firstMultiplier,
                            "Returned Core scores through Light's real goal");
                        Check(cycle.Phase == AmplifierEncounterPhase.Dissolving && !cycle.ActivePattern.InteractionEnabled,
                            "Capture dissolves Resonance and disables its interaction");
                        Check(!cycle.ActiveCore.TryCapture(Goal(1)), "Captured Core cannot award a second time");
                        stage = 6; break;
                    case 6:
                        if (cycle.Phase == AmplifierEncounterPhase.Delay) sawRespawnDelay = true;
                        if (!ReadyCore() || cycle.PairsSpawned < 2) return;
                        Check(sawRespawnDelay && Mathf.Abs(cycle.LastRespawnDelay - .4f) < .001f,
                            "Capture advances through dissolution, delay and a new formation");
                        Check(CountCores() == 1 && cycle.ActiveCore.GetComponent<SingularityAmplifierAdapter>() != null,
                            "Replacement Core is unique and wraps too");
                        Deposit(2); stage = 7; break;
                    case 7:
                        if (cycle.CapturesObserved < 2) return;
                        Check(cycle.scoreService.GetTeamAmplifierMultiplier(2) > secondMultiplier,
                            "Dark's real goal also awards its multiplier");
                        cycle.StopCycle();
                        Check(cycle.ActiveCore == null && cycle.ActivePattern == null && bridge.editPreview.gameObject.activeSelf,
                            "Stopping removes owned pair and restores edit preview");
                        Finish("PASS: " + checks.Count + " SINGULARITY runtime checks\n" + string.Join("\n", checks));
                        break;
                }
            }
            catch (Exception e) { Finish("FAIL: " + e.Message + "\n" + string.Join("\n", checks)); }
        }
        private static bool ReadyCore() => cycle.Phase == AmplifierEncounterPhase.Active
            && cycle.ActiveCore != null && !cycle.ActiveCore.IsCaptured;
        private static AmplifierGoalCapture Goal(int team) => UnityEngine.Object.FindObjectsByType<AmplifierGoalCapture>(FindObjectsSortMode.None)
            .First(g => g.TeamID == team && g.gameObject.scene == cycle.gameObject.scene);
        private static void Deposit(int team)
        {
            var body = cycle.ActiveCore.Body;
            body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
            body.position = Goal(team).CapturePoint.position;
            cycle.ActiveCore.transform.position = body.position;
            Physics.SyncTransforms();
        }
        private static int CountCores() => AmplifierCoreGameplay.ActiveCores.Count(c => c != null
            && c.gameObject.scene == cycle.gameObject.scene && c.gameObject.activeInHierarchy && !c.IsPresentationOnly);
        private static void Check(bool condition, string label)
        { if (!condition) throw new InvalidOperationException(label); checks.Add(label); }
        private static void Finish(string result)
        {
            EditorApplication.update -= Tick;
            Time.timeScale = savedTimeScale;
            if (cycle != null) { cycle.StopCycle(); JsonUtility.FromJsonOverwrite(settings, cycle); }
            cycle = null; core = null; bridge = null;
            Result = result;
            Debug.Log(result);
        }
    }
}
