#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Massive.Multiplier;
using UnityEditor;
using UnityEngine;

namespace Massive.Singularity.Editor
{
    /// <summary>Explicit bounded runtime check of the live encounter, real Core
    /// portal field, lifecycle cancellation and ordinary goal scoring. Never saves
    /// scene/prefab assets; runtime cycle overrides are restored on every exit.</summary>
    public static class SingularityCorePortalPlayValidation
    {
        public static string Result { get; private set; } = "Not run";
        private static AmplifierResonanceSpawner cycle;
        private static SingularityAmplifierEncounter bridge;
        private static SingularityBlackHolePortal portal;
        private static SingularityAmplifierAdapter core;
        private static SingularityRendererBrightness brightness;
        private static string cycleSettings;
        private static bool savedPortalEnabled;
        private static float savedTimeScale, pausedElapsed, initialDistance;
        private static Vector2 pausedPosition;
        private static Vector3 restingScale;
        private static Transform restingParent;
        private static int stage, firstMultiplier, secondMultiplier, initialTransfers, lastPairs;
        private static long firstScore, secondScore;
        private static double began, stageBegan;
        private static readonly List<string> checks = new List<string>();

        public static string Begin()
        {
            if (!Application.isPlaying || cycle != null)
                throw new InvalidOperationException("Requires Play Mode and no running Core portal check.");
            bridge = UnityEngine.Object.FindFirstObjectByType<SingularityAmplifierEncounter>();
            if (bridge == null || bridge.spawner == null || bridge.portal == null)
                throw new InvalidOperationException("Requires the integrated SINGULARITY encounter and portal.");
            cycle = bridge.spawner; portal = bridge.portal;
            cycleSettings = JsonUtility.ToJson(cycle);
            savedTimeScale = Time.timeScale; savedPortalEnabled = portal.enabled;
            try
            {
                cycle.StopCycle(); portal.CancelAll(); portal.enabled = true;
                cycle.UseSharedSettings = false;
                cycle.initialSpawnDelay = 0f;
                cycle.respawnDelay = .2f; cycle.respawnDelayVariation = Vector2.zero;
                cycle.maximumActiveSeconds = 120f;
                cycle.fixedRandomSeed = true; cycle.randomSeed = 345;
                firstMultiplier = cycle.scoreService.GetTeamAmplifierMultiplier(1);
                secondMultiplier = cycle.scoreService.GetTeamAmplifierMultiplier(2);
                firstScore = cycle.scoreService.GetTeamScore(1); secondScore = cycle.scoreService.GetTeamScore(2);
                initialTransfers = portal.CoreTeleportCount;
                checks.Clear(); stage = 0;
                began = stageBegan = EditorApplication.timeSinceStartup;
                Time.timeScale = 1f;
                Check(cycle.StartCycle(), "Live encounter starts");
                Result = "Running live SINGULARITY Core field, transit, brightness, timeout and goal checks";
                EditorApplication.update += Tick;
            }
            catch (Exception e) { Finish("FAIL: " + e.Message); }
            return Result;
        }

        public static string Cancel()
        { if (cycle != null) Finish("Cancelled; runtime overrides restored"); return Result; }

        private static void Tick()
        {
            try
            {
                if (!Application.isPlaying || cycle == null) { Finish("Interrupted; runtime overrides restored"); return; }
                double now = EditorApplication.timeSinceStartup;
                if (now - began > 95) throw new TimeoutException("Stage " + stage + ": " + cycle.Phase + ": " + cycle.Status);
                switch (stage)
                {
                    case 0:
                        if (!ReadyCore()) return;
                        BindCore(); ValidateBrightness();
                        var center = PortalCenter();
                        float distance = Mathf.Lerp(portal.EffectiveCaptureRadius, portal.EffectiveAttractionRadius, .45f);
                        core.SetSurfacePosition(portal.FaceToSurface(center + Vector2.left * distance, false));
                        initialDistance = distance;
                        AdvanceStage(1, now); break;
                    case 1:
                        if (now - stageBegan < .22) return;
                        Vector2 fieldPoint; bool fieldRear;
                        Check(portal.TryGetFaceCoordinates(core.SurfacePosition, out fieldPoint, out fieldRear) && !fieldRear,
                            "Natural field begins on front face");
                        Check(Vector2.Distance(fieldPoint, PortalCenter()) < initialDistance - .001f,
                            "Real physics field pulls a resting Core inward");
                        Check(core.SurfaceVelocity.x > .001f, "Core receives inward acceleration without an attack impulse");
                        if (Mathf.Abs(portal.swirlAcceleration) > .001f)
                            Check(core.SurfaceVelocity.y * portal.swirlAcceleration < 0f, "Core also receives the portal's signed tangential swirl");
                        PlaceInMouth(false); AdvanceStage(2, now); break;
                    case 2:
                        if (!AtPhase(SingularityBlackHolePortal.TransitPhase.Entering) || portal.ElapsedOf(core) < .1f) return;
                        Check(core.Core.IsInExternalTransit && core.HasPortalVisual && core.Body.isKinematic,
                            "Automatic capture leases Core motion and starts portal shape");
                        Check(core.PortalVisualScale < 1f && core.PortalVisualStretch > 1f,
                            "Live entry shrinks and stretches Core");
                        Check(core.VisualRoot.localScale == restingScale && core.VisualRoot.parent != restingParent,
                            "Portal deformation is separate from lifecycle-owned scale");
                        pausedPosition = core.SurfacePosition; pausedElapsed = portal.ElapsedOf(core);
                        Time.timeScale = 0f; AdvanceStage(3, now); break;
                    case 3:
                        if (now - stageBegan < .25) return;
                        Check(Vector2.Distance(core.SurfacePosition, pausedPosition) < .00001f && Mathf.Abs(portal.ElapsedOf(core) - pausedElapsed) < .00001f,
                            "Pause freezes Core travel and portal timer");
                        Time.timeScale = 1f; AdvanceStage(4, now); break;
                    case 4:
                        if (!AtPhase(SingularityBlackHolePortal.TransitPhase.Exiting)) return;
                        Check(core.RearWeight > .99f && portal.CoreTeleportCount == initialTransfers + 1,
                            "Entry transfers exactly once to rear face");
                        CheckNoScore("First portal transfer awards no score or multiplier");
                        AdvanceStage(5, now); break;
                    case 5:
                        if (portal.IsInTransit(core)) return;
                        CheckRestored("Rear exit");
                        Vector2 rearPoint; bool isRear;
                        Check(portal.TryGetFaceCoordinates(core.SurfacePosition, out rearPoint, out isRear) && isRear &&
                            Vector2.Distance(rearPoint, PortalCenter()) > portal.EffectiveAttractionRadius,
                            "Rear exit releases outside attraction radius");
                        Check(core.SurfaceVelocity.x > 0f, "Exit release goes opposite the approach side");
                        core.SetSurfacePosition(portal.FaceToSurface(PortalCenter() + Vector2.right * (portal.EffectiveAttractionRadius + .5f), true));
                        AdvanceStage(6, now); break;
                    case 6:
                        if (now - stageBegan < portal.cooldownSeconds + .2f) return;
                        PlaceInMouth(true); AdvanceStage(7, now); break;
                    case 7:
                        if (portal.CoreTeleportCount < initialTransfers + 2 || portal.IsInTransit(core)) return;
                        Check(core.RearWeight < .01f, "Same Core completes rear-to-front return trip");
                        CheckRestored("Front return");
                        CheckNoScore("Round-trip teleport does not count as a goal");
                        Deposit(); AdvanceStage(8, now); break;
                    case 8:
                        if (cycle.CapturesObserved < 1) return;
                        Check(cycle.scoreService.GetTeamAmplifierMultiplier(1) > firstMultiplier &&
                            cycle.scoreService.GetTeamAmplifierMultiplier(2) == secondMultiplier,
                            "Returned Core is accepted by real Light goal and awards only that team");
                        Check(cycle.Phase == AmplifierEncounterPhase.Dissolving,
                            "Normal goal capture still retires Resonance and advances cycle");
                        firstMultiplier = cycle.scoreService.GetTeamAmplifierMultiplier(1);
                        firstScore = cycle.scoreService.GetTeamScore(1); secondScore = cycle.scoreService.GetTeamScore(2);
                        lastPairs = cycle.PairsSpawned; AdvanceStage(9, now); break;
                    case 9:
                        if (!ReadyCore() || cycle.PairsSpawned <= lastPairs) return;
                        BindCore(); PlaceInMouth(false); AdvanceStage(10, now); break;
                    case 10:
                        if (!AtPhase(SingularityBlackHolePortal.TransitPhase.Entering) || portal.ElapsedOf(core) < .1f) return;
                        cycle.maximumActiveSeconds = .001f; AdvanceStage(11, now); break;
                    case 11:
                        if (cycle.Phase != AmplifierEncounterPhase.Dissolving) return;
                        CheckTimeoutCleanup("Entry timeout");
                        cycle.maximumActiveSeconds = 120f; lastPairs = cycle.PairsSpawned;
                        AdvanceStage(12, now); break;
                    case 12:
                        if (!ReadyCore() || cycle.PairsSpawned <= lastPairs) return;
                        BindCore(); PlaceInMouth(false); AdvanceStage(13, now); break;
                    case 13:
                        if (!AtPhase(SingularityBlackHolePortal.TransitPhase.Exiting) || portal.ElapsedOf(core) < .1f) return;
                        cycle.maximumActiveSeconds = .001f; AdvanceStage(14, now); break;
                    case 14:
                        if (cycle.Phase != AmplifierEncounterPhase.Dissolving) return;
                        CheckTimeoutCleanup("Exit timeout");
                        cycle.maximumActiveSeconds = 120f; lastPairs = cycle.PairsSpawned;
                        AdvanceStage(15, now); break;
                    case 15:
                        if (!ReadyCore() || cycle.PairsSpawned <= lastPairs) return;
                        BindCore(); PlaceInMouth(false); AdvanceStage(16, now); break;
                    case 16:
                        if (!AtPhase(SingularityBlackHolePortal.TransitPhase.Entering) || portal.ElapsedOf(core) < .1f) return;
                        portal.enabled = false;
                        CheckRestored("Disabled portal");
                        Check(!portal.IsInTransit(core) && !core.Core.IsCaptured,
                            "Disabling portal never strands or despawns an active Core");
                        core.SetSurfacePosition(portal.FaceToSurface(PortalCenter() + Vector2.right * (portal.EffectiveAttractionRadius + .5f), false));
                        portal.enabled = true;
                        CheckNoScore("Interruptions never award a multiplier or score");
                        Check(cycle.CapturesObserved == 1, "Only the deliberate goal counted as capture");
                        Finish("PASS: " + checks.Count + " live Core portal checks\n" + string.Join("\n", checks));
                        break;
                }
            }
            catch (Exception e) { Finish("FAIL at stage " + stage + ": " + e.Message + "\n" + string.Join("\n", checks)); }
        }

        private static void BindCore()
        {
            core = cycle.ActiveCore.GetComponent<SingularityAmplifierAdapter>();
            brightness = cycle.ActiveCore.GetComponent<SingularityRendererBrightness>();
            Check(core != null && brightness != null, "Spawned Core receives folding and shared dimming");
            restingParent = core.VisualRoot.parent; restingScale = core.VisualRoot.localScale;
        }
        private static void ValidateBrightness()
        {
            var surface = bridge.surface;
            float[] positions = { surface.FrontHeight * .5f, (surface.TopStart + surface.RearStart) * .5f,
                (surface.RearStart + surface.BottomStart) * .5f, (surface.BottomStart + surface.LoopLength) * .5f };
            var properties = new MaterialPropertyBlock();
            for (int i = 0; i < positions.Length; i++)
            {
                core.SetSurfacePosition(new Vector2(-7f, positions[i])); brightness.RefreshPresentation();
                float expected = surface.EvaluateBrightness(positions[i]);
                Check(Mathf.Abs(brightness.CurrentBrightness - expected) < .00001f,
                    "Core brightness follows shared surface gradient sample " + i);
                foreach (var renderer in core.VisualRoot.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.GetPropertyBlock(properties);
                    Check(Mathf.Abs(properties.GetFloat("_SingularityBrightness") - expected) < .00001f,
                        "Core render layer receives brightness: " + renderer.name + " / " + i);
                }
            }
        }
        private static void CheckRestored(string label)
        {
            Check(!core.Core.IsInExternalTransit && !core.HasPortalVisual && !core.Body.isKinematic && core.Body.detectCollisions,
                label + " restores normal motion/collision and clears visual ownership");
            Check(core.VisualRoot.parent == restingParent && core.VisualRoot.localScale == restingScale,
                label + " restores resting hierarchy and exact lifecycle-owned scale");
        }
        private static void CheckTimeoutCleanup(string label)
        {
            Check(core != null && core.Core.IsDespawning && !core.Core.HasBeenCaptured,
                label + " uses real non-scoring timeout lifecycle");
            Check(!portal.IsInTransit(core) && !core.Core.IsInExternalTransit && !core.HasPortalVisual,
                label + " synchronously clears portal lease and deformation");
            Check(core.VisualRoot.parent == restingParent && core.Body.isKinematic,
                label + " returns visual parent before lifecycle takes physics ownership");
            CheckNoScore(label + " awards no score or multiplier");
        }
        private static void CheckNoScore(string label)
        {
            Check(cycle.scoreService.GetTeamAmplifierMultiplier(1) == firstMultiplier &&
                cycle.scoreService.GetTeamAmplifierMultiplier(2) == secondMultiplier &&
                cycle.scoreService.GetTeamScore(1) == firstScore && cycle.scoreService.GetTeamScore(2) == secondScore, label);
        }
        private static Vector2 PortalCenter()
        {
            Vector3 local = bridge.surface.transform.InverseTransformPoint(portal.transform.position);
            return new Vector2(local.x, local.z);
        }
        private static void PlaceInMouth(bool rear)
        {
            core.SetSurfacePosition(portal.FaceToSurface(PortalCenter() + Vector2.left * (portal.EffectiveCaptureRadius * .8f), rear));
            Physics.SyncTransforms();
        }
        private static void Deposit()
        {
            var goal = UnityEngine.Object.FindObjectsByType<AmplifierGoalCapture>(FindObjectsSortMode.None)
                .First(g => g.TeamID == 1 && g.gameObject.scene == cycle.gameObject.scene);
            core.Body.linearVelocity = Vector3.zero; core.Body.angularVelocity = Vector3.zero;
            core.Body.position = goal.CapturePoint.position; core.transform.position = core.Body.position;
            Physics.SyncTransforms();
        }
        private static bool ReadyCore() => cycle.Phase == AmplifierEncounterPhase.Active && cycle.ActiveCore != null && !cycle.ActiveCore.IsCaptured;
        private static bool AtPhase(SingularityBlackHolePortal.TransitPhase phase) => core != null && portal.PhaseOf(core) == phase;
        private static void AdvanceStage(int value, double now) { stage = value; stageBegan = now; }
        private static void Check(bool passed, string message)
        { if (!passed) throw new InvalidOperationException(message); checks.Add(message); }
        private static void Finish(string result)
        {
            EditorApplication.update -= Tick;
            try
            {
                Time.timeScale = savedTimeScale;
                if (portal != null) { portal.CancelAll(); portal.enabled = savedPortalEnabled; }
                if (cycle != null) { cycle.StopCycle(); JsonUtility.FromJsonOverwrite(cycleSettings, cycle); }
            }
            finally
            {
                cycle = null; bridge = null; portal = null; core = null; brightness = null;
                Result = result; Debug.Log(result);
            }
        }
    }
}
#endif
