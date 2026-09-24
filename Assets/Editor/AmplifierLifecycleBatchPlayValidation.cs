using System;
using System.Collections.Generic;
using Massive.Resonance;
using Massive.Scoring;
using UnityEditor;
using UnityEngine;

namespace Massive.Multiplier.Editor
{
    /// <summary>Explicit, bounded live-scene check. Uses real Update/coroutines; never saves scene or prefab assets.</summary>
    public static class AmplifierLifecycleBatchPlayValidation
    {
        public static string Result { get; private set; } = "Not run";
        public static bool IsRunning => spawner != null;
        private static AmplifierResonanceSpawner spawner;
        private static MatchScoreService scores;
        private static AmplifierCoreGameplay firstCore;
        private static string savedSpawner;
        private static float savedTimeScale, activeStart;
        private static bool savedScoring, savedChainClock, sawSpawn, sawWarning, timingStarted;
        private static int savedLightTier, savedDarkTier, captureEvents, stage;
        private static double began, stageBegan;
        private static readonly List<string> checks = new List<string>();
        private const float Lifetime = 2f, Warning = .7f;

        public static string Begin()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            if (IsRunning) throw new InvalidOperationException("Lifecycle validation is already running.");
            spawner = UnityEngine.Object.FindFirstObjectByType<AmplifierResonanceSpawner>();
            if (spawner == null) throw new InvalidOperationException("No active Amplifier/Resonance spawner.");
            scores = spawner.scoreService != null ? spawner.scoreService : MatchScoreService.Instance;
            if (scores == null) { spawner = null; throw new InvalidOperationException("No score service."); }
            savedSpawner = JsonUtility.ToJson(spawner); savedTimeScale = Time.timeScale;
            savedScoring = scores.IsScoringOpen; savedChainClock = scores.IsChainClockRunning;
            savedLightTier = scores.GetTeamAmplifierTierIndex(1); savedDarkTier = scores.GetTeamAmplifierTierIndex(2);
            checks.Clear(); stage = captureEvents = 0; sawSpawn = sawWarning = timingStarted = false;
            firstCore = null; began = stageBegan = EditorApplication.timeSinceStartup;
            try
            {
                Check(Mathf.Abs(spawner.maximumActiveSeconds - 30f) < .001f && Mathf.Abs(spawner.timeoutWarningSeconds - 5f) < .001f,
                    "saved scene defaults are thirty seconds with a five-second warning");
                ResonancePatternController pattern = null;
                foreach (var entry in spawner.patternOrder)
                    if (entry != null && entry.patternPrefab != null && entry.patternPrefab.definition != null) { pattern = entry.patternPrefab; break; }
                Check(pattern != null, "configured pattern prefab is available");
                spawner.StopCycle(); scores.OpenScoring(); scores.ResetMultipliers(); Time.timeScale = 1f;
                spawner.initialSpawnDelay = 0f; spawner.respawnDelay = .2f; spawner.respawnDelayVariation = Vector2.zero;
                spawner.maximumActiveSeconds = Lifetime; spawner.timeoutWarningSeconds = Warning;
                spawner.fixedRandomSeed = true; spawner.randomSeed = 345; spawner.startingPattern = 0; spawner.loopPatternOrder = true;
                spawner.patternOrder = new List<ResonanceSpawnEntry> {
                    new ResonanceSpawnEntry { label = "Timeout validation first", patternPrefab = pattern },
                    new ResonanceSpawnEntry { label = "Timeout validation second", patternPrefab = pattern }
                };
                Check(spawner.StartCycle(), "real sequence cycle starts");
                Result = "Running: real timeout, warning, cap suppression, and reset lifecycle";
                EditorApplication.update += Tick;
                return Result;
            }
            catch (Exception e) { Finish("FAIL: " + e.Message); return Result; }
        }

        public static void Stop() { if (IsRunning) Finish("Interrupted by caller"); }
        private static void Captured(AmplifierCoreGameplay core) { captureEvents++; }
        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException(description);
            checks.Add(description);
        }
        private static void Require(bool condition, string description)
        { if (!condition) throw new InvalidOperationException(description); }

        private static void Tick()
        {
            try
            {
                if (!Application.isPlaying || spawner == null || scores == null) { Finish("Interrupted: Play Mode or required object ended"); return; }
                double now = EditorApplication.timeSinceStartup;
                if (now - began > 45) throw new TimeoutException("45-second limit at stage " + stage + ", " + spawner.Phase + ": " + spawner.Status);
                switch (stage)
                {
                    case 0:
                        if (spawner.Phase != AmplifierEncounterPhase.Active) return;
                        if (firstCore == null)
                        {
                            firstCore = spawner.ActiveCore; firstCore.Captured += Captured;
                        }
                        if (firstCore.IsSpawning)
                        {
                            sawSpawn = true;
                            Require(Mathf.Abs(spawner.ActiveSecondsRemaining - Lifetime) < .001f && spawner.TimeoutWarning01 == 0f,
                                "Core appearance must not consume the playable lifetime or show warning");
                            return;
                        }
                        Check(sawSpawn, "Core appearance preserves the complete playable lifetime");
                        // Hold the test Core in neutral territory while validating time, not collisions.
                        if (firstCore.Body != null) firstCore.Body.constraints = RigidbodyConstraints.FreezeAll;
                        activeStart = Time.time; timingStarted = true; stage = 1;
                        break;
                    case 1:
                        Require(captureEvents == 0 && spawner.CapturesObserved == 0, "timeout cannot synthesize a capture");
                        Require(scores.GetTeamAmplifierTierIndex(1) == 0 && scores.GetTeamAmplifierTierIndex(2) == 0,
                            "unclaimed pair cannot advance either multiplier");
                        if (spawner.Phase == AmplifierEncounterPhase.Active)
                        {
                            float remaining = spawner.ActiveSecondsRemaining;
                            if (remaining > Warning + .03f) Require(spawner.TimeoutWarning01 == 0f, "surface warning cannot begin early");
                            if (remaining < Warning - .08f && remaining > .08f)
                            {
                                float expected = (Warning - remaining) / Warning;
                                Require(Mathf.Abs(spawner.TimeoutWarning01 - expected) < .035f, "warning follows the configured final-window progress");
                                sawWarning |= spawner.TimeoutWarning01 > 0f;
                            }
                            return;
                        }
                        Require(spawner.Phase == AmplifierEncounterPhase.Dissolving, "timeout should transition to dissolution");
                        Check(timingStarted && Mathf.Abs(Time.time - activeStart - Lifetime) < .35f, "time-out occurs after the complete playable lifetime");
                        Check(sawWarning, "surface warning rises only inside the final configured window");
                        Check(captureEvents == 0 && !firstCore.HasBeenCaptured && firstCore.IsCaptured,
                            "expiry blocks interaction without capture event or goal credit");
                        Check(!spawner.ActivePattern.InteractionEnabled, "expiry immediately removes Resonance collision authority");
                        stage = 2; break;
                    case 2:
                        if (spawner.Phase != AmplifierEncounterPhase.Delay) return;
                        Check(spawner.ActiveCore == null && spawner.ActivePattern == null && spawner.ActiveManifestation == null,
                            "both Core and pattern finish retiring before the next delay");
                        Check(captureEvents == 0 && spawner.CapturesObserved == 0 && spawner.PairsSpawned == 1,
                            "expired pair awards no capture and creates no duplicate Core");
                        Check(Mathf.Abs(spawner.LastRespawnDelay - .2f) < .001f, "normal respawn delay follows timeout cleanup");
                        stage = 3; break;
                    case 3:
                        if (spawner.Phase != AmplifierEncounterPhase.FormingPattern) return;
                        Check(spawner.CurrentPatternIndex == 1 && spawner.ActiveCore == null, "timeout advances to the next ordered pattern");
                        CapTeams(); stage = 4; stageBegan = now; break;
                    case 4:
                        if (spawner.Phase != AmplifierEncounterPhase.WaitingForAmplifierCapacity) return;
                        Check(spawner.ActivePattern == null && spawner.ActiveCore == null && spawner.PairsSpawned == 1,
                            "reaching both caps during formation dissolves it without spawning another Core");
                        spawner.SpawnNow(); stage = 5; stageBegan = now; break;
                    case 5:
                        if (now - stageBegan < .35) return;
                        Check(spawner.Phase == AmplifierEncounterPhase.WaitingForAmplifierCapacity && spawner.ActivePattern == null,
                            "Spawn Now cannot bypass both teams' caps");
                        Check(spawner.StartCycle(), "new cycle request accepted while capped");
                        spawner.SpawnNow(); stage = 6; stageBegan = now; break;
                    case 6:
                        if (now - stageBegan < .35) return;
                        Check(spawner.Phase == AmplifierEncounterPhase.WaitingForAmplifierCapacity && spawner.PairsSpawned == 0 && spawner.ActivePattern == null,
                            "immediate start while capped creates no Resonance sequence");
                        scores.ResetMultipliers(); stage = 7; break;
                    case 7:
                        if (spawner.Phase != AmplifierEncounterPhase.FormingPattern) return;
                        Check(spawner.CurrentPatternIndex == 0 && spawner.ActivePattern != null,
                            "resetting multipliers releases the capacity hold and starts requested order");
                        spawner.StopCycle();
                        Check(spawner.Phase == AmplifierEncounterPhase.Stopped && spawner.ActiveCore == null && spawner.ActivePattern == null,
                            "explicit Stop cleans all managed objects");
                        Finish("PASS: " + checks.Count + " live lifecycle checks"); break;
                }
            }
            catch (Exception e) { Finish("FAIL: " + e.Message); }
        }

        private static void CapTeams()
        {
            for (int team = 1; team <= 2; team++)
                for (int attempt = 0; attempt < 32 && !scores.IsTeamAmplifierMaxed(team); attempt++)
                    Require(scores.AdvanceTeamAmplifier(team), "team multiplier must advance to cap through authoritative service");
            Check(scores.AreAllTeamsAmplifierMaxed, "both teams reach their configured caps");
        }

        private static void Finish(string result)
        {
            EditorApplication.update -= Tick;
            if (firstCore != null) firstCore.Captured -= Captured;
            if (spawner != null)
            {
                spawner.StopCycle();
                if (!string.IsNullOrEmpty(savedSpawner)) JsonUtility.FromJsonOverwrite(savedSpawner, spawner);
            }
            if (Application.isPlaying && scores != null)
            {
                scores.OpenScoring(); scores.ResetMultipliers();
                for (int i = 0; i < savedLightTier; i++) scores.AdvanceTeamAmplifier(1);
                for (int i = 0; i < savedDarkTier; i++) scores.AdvanceTeamAmplifier(2);
                scores.SetScoringState(savedScoring, savedChainClock);
            }
            Time.timeScale = savedTimeScale;
            spawner = null; scores = null; firstCore = null;
            Result = result + "\n" + string.Join("\n", checks) + "\nSpawner settings, team tiers, scoring state and time scale restored; managed cycle stopped.";
        }
    }
}
