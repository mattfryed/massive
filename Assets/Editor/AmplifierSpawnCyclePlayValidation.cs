using System;
using System.Collections.Generic;
using Massive.Scoring;
using UnityEditor;
using UnityEngine;

namespace Massive.Multiplier.Editor
{
    /// <summary>Explicit Play-only smoke test. Deposits real runtime Cores; never edits prefab assets or saves a scene.</summary>
    public static class AmplifierSpawnCyclePlayValidation
    {
        public static string Result { get; private set; } = "Not run";
        private static AmplifierResonanceSpawner s;
        private static string saved, savedRegion;
        private static float scale, pausedProgress;
        private static double began, stageTime;
        private static int stage;
        private static bool sawDelay;
        private static AmplifierCoreGameplay deposited;
        private static readonly List<string> checks = new List<string>();

        public static string Begin()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            if (s != null) throw new InvalidOperationException("Validation is already running.");
            s = UnityEngine.Object.FindFirstObjectByType<AmplifierResonanceSpawner>();
            if (s == null) throw new InvalidOperationException("Install the spawn coordinator first.");
            saved = JsonUtility.ToJson(s); savedRegion = JsonUtility.ToJson(s.spawnRegion); scale = Time.timeScale;
            s.StopCycle(); s.initialSpawnDelay = 0f; s.respawnDelay = .35f; s.respawnDelayVariation = Vector2.zero;
            s.startingPattern = 0; s.loopPatternOrder = true; s.fixedRandomSeed = true; s.randomSeed = 345;
            s.patternOrder = new List<ResonanceSpawnEntry> {
                new ResonanceSpawnEntry { label = "Test first", patternPrefab = s.patternOrder[0].patternPrefab },
                new ResonanceSpawnEntry { label = "Test second", patternPrefab = s.patternOrder[0].patternPrefab } };
            checks.Clear(); sawDelay = false; stage = 0; began = EditorApplication.timeSinceStartup;
            s.StartCycle(); Result = "Running real capture / respawn smoke test";
            EditorApplication.update += Tick;
            return Result;
        }
        private static void Check(bool condition, string text)
        { if (!condition) throw new InvalidOperationException(text); checks.Add(text); }
        private static void Tick()
        {
            try
            {
                if (!Application.isPlaying || s == null) { Finish("Interrupted"); return; }
                double now = EditorApplication.timeSinceStartup;
                if (now - began > 40) throw new TimeoutException("Phase " + s.Phase + ": " + s.Status);
                switch (stage)
                {
                    case 0:
                        if (s.Phase != AmplifierEncounterPhase.FormingPattern) return;
                        Check(s.ActiveCore == null && !s.ActivePattern.InteractionEnabled, "forming pattern has no Core or live collision/grid authority");
                        pausedProgress = s.ActiveManifestation.NormalizedProgress; Time.timeScale = 0f; stageTime = now; stage = 1; break;
                    case 1:
                        if (now - stageTime < .2) return;
                        Check(Mathf.Abs(s.ActiveManifestation.NormalizedProgress - pausedProgress) < .0001f, "pause freezes sand formation");
                        Time.timeScale = scale; stage = 2; break;
                    case 2:
                        if (s.Phase != AmplifierEncounterPhase.Active || s.ActiveCore.IsSpawning) return;
                        Check(s.ActiveManifestation.IsIdle && s.ActivePattern.InteractionEnabled, "Core appears only after settled pattern");
                        var oldIgnored = s.spawnRegion.ignoredColliders;
                        s.spawnRegion.ignoredColliders = s.ActiveCore.GetComponentsInChildren<Collider>();
                        string why; bool safe = s.spawnRegion.IsValidSpawnPoint(s.ActiveCore.transform.position, s.CorePlacementRadius, s.ActivePattern, out why);
                        s.spawnRegion.ignoredColliders = oldIgnored;
                        Check(safe, "actual Core position is clear and neutral: " + why);
                        Check(CountCores() == 1 && !s.comparison.enabled, "only one gameplay Core and one active pattern system");
                        Deposit(1); stage = 3; break;
                    case 3:
                        if (s.CapturesObserved != 1) return;
                        Check(s.Phase == AmplifierEncounterPhase.Dissolving && !s.ActivePattern.InteractionEnabled, "real goal capture immediately dissolves and disables pattern interaction");
                        Check(deposited.HasBeenCaptured && !deposited.TryCapture(FindGoal(1)), "captured Core cannot award twice");
                        stage = 4; break;
                    case 4:
                        if (s.Phase == AmplifierEncounterPhase.Delay) sawDelay = true;
                        if (s.Phase != AmplifierEncounterPhase.Active || s.ActiveCore.IsSpawning) return;
                        Check(sawDelay && Mathf.Abs(s.LastRespawnDelay - .35f) < .001f, "respawn waits after complete dissolution/absorption");
                        Check(s.CurrentPatternIndex == 1 && s.PairsSpawned == 2 && CountCores() == 1, "second ordered entry creates one fresh Core");
                        s.spawnRegion.neutralWidthFraction = .01f; Deposit(2); stage = 5; break;
                    case 5:
                        if (s.Phase != AmplifierEncounterPhase.FindingPlacement) return;
                        stageTime = now; stage = 6; break;
                    case 6:
                        if (now - stageTime < .8) return;
                        Check(s.Phase == AmplifierEncounterPhase.FindingPlacement && s.ActiveCore == null && s.PairsSpawned == 2, "fully blocked territory retries without unsafe fallback");
                        JsonUtility.FromJsonOverwrite(savedRegion, s.spawnRegion); stage = 7; break;
                    case 7:
                        if (s.Phase != AmplifierEncounterPhase.Active || s.ActiveCore.IsSpawning) return;
                        Check(s.CurrentPatternIndex == 0 && s.PairsSpawned == 3 && s.CapturesObserved == 2 && CountCores() == 1, "cleared territory resumes and order loops to first entry");
                        s.enabled = false;
                        Check(s.ActiveCore == null && s.ActivePattern == null && s.comparison.enabled, "disable removes managed pair and restores standalone demonstrations");
                        Finish("PASS: " + checks.Count + " runtime checks\n" + string.Join("\n", checks)); break;
                }
            }
            catch (Exception e) { Finish("FAIL: " + e.Message + "\n" + string.Join("\n", checks)); }
        }
        private static AmplifierGoalCapture FindGoal(int team)
        {
            foreach (var goal in UnityEngine.Object.FindObjectsByType<AmplifierGoalCapture>(FindObjectsSortMode.None)) if (goal.TeamID == team) return goal;
            throw new InvalidOperationException("Missing team goal " + team);
        }
        private static void Deposit(int team)
        {
            deposited = s.ActiveCore;
            deposited.Body.linearVelocity = Vector3.zero; deposited.Body.angularVelocity = Vector3.zero;
            deposited.Body.position = FindGoal(team).CapturePoint.position;
            Physics.SyncTransforms();
        }
        private static int CountCores()
        {
            int count = 0;
            foreach (var core in AmplifierCoreGameplay.ActiveCores) if (core != null && core.gameObject.activeInHierarchy && !core.IsPresentationOnly) count++;
            return count;
        }
        private static void Finish(string result)
        {
            EditorApplication.update -= Tick; Time.timeScale = scale;
            if (s != null)
            {
                s.StopCycle(); JsonUtility.FromJsonOverwrite(saved, s); JsonUtility.FromJsonOverwrite(savedRegion, s.spawnRegion);
                s.enabled = true;
                if (Application.isPlaying) s.StartCycle();
            }
            s = null; Result = result;
        }
    }
}
