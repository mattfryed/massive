using System;
using Massive.Scoring;
using UnityEditor;
using UnityEngine;

namespace Massive.Multiplier.Editor
{
    /// <summary>Announcement eligibility and authored tier scaling, independent of live match state.</summary>
    public static class TeamAmplifierToastValidation
    {
        [MenuItem("MASSIVE/Amplifier/Validate Capture Toast Rules")]
        public static void ValidateMenu() { Debug.Log(Run()); }

        public static string Run()
        {
            int passed = 0;
            var snapshot = new TeamAmplifierSnapshot {
                teamID = 1, previousTierIndex = 0, currentTierIndex = 1,
                previousMultiplier = 1, currentMultiplier = 2
            };
            Check(TeamAmplifierToastPresenter.IsCaptureIncrease(snapshot), "Light capture accepted", ref passed);
            snapshot.teamID = 2;
            Check(TeamAmplifierToastPresenter.IsCaptureIncrease(snapshot), "Dark capture accepted", ref passed);
            snapshot.teamID = 3;
            Check(!TeamAmplifierToastPresenter.IsCaptureIncrease(snapshot), "Invalid team rejected", ref passed);
            snapshot.teamID = 1;
            snapshot.currentTierIndex = 0; snapshot.currentMultiplier = 1;
            Check(!TeamAmplifierToastPresenter.IsCaptureIncrease(snapshot), "Initial reset is silent", ref passed);
            snapshot.previousTierIndex = 3; snapshot.previousMultiplier = 8;
            Check(!TeamAmplifierToastPresenter.IsCaptureIncrease(snapshot), "Returning from max to base is silent", ref passed);
            snapshot.currentTierIndex = 3; snapshot.currentMultiplier = 8;
            Check(!TeamAmplifierToastPresenter.IsCaptureIncrease(snapshot), "Repeated capped value is silent", ref passed);
            Check(TeamAmplifierToastPresenter.GetEffectStrength(2, 1, 1) == 0f, "A configured ×2 maximum still has no effects", ref passed);
            Check(TeamAmplifierToastPresenter.GetEffectStrength(2, 1, 3) == 0f, "×2 is white", ref passed);
            Check(Mathf.Approximately(TeamAmplifierToastPresenter.GetEffectStrength(4, 2, 3), .5f), "Intermediate tier is subdued", ref passed);
            Check(TeamAmplifierToastPresenter.GetEffectStrength(8, 3, 3) == 1f, "Configured default cap is full intensity", ref passed);
            Check(TeamAmplifierToastPresenter.GetEffectStrength(16, 4, 4) == 1f, "Configured ×16 cap is full intensity", ref passed);
            Check(TeamAmplifierToastPresenter.GetEffectStrength(8, 3, 4) < 1f, "×8 below an authored ×16 cap is not treated as maximum", ref passed);
            Check(TeamAmplifierToastPresenter.GetEffectStrength(3, 1, 1) == 1f, "Single enhanced tier reaches its own configured maximum", ref passed);
            return "Amplifier capture toast: " + passed + " rule checks passed. Runtime typography, events, and cleanup still require Play Mode validation.";
        }

        private static void Check(bool condition, string label, ref int passed)
        {
            if (!condition) throw new InvalidOperationException("Amplifier toast validation: " + label);
            passed++;
        }
    }
}
