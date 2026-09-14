#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Massive.Scoring;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Runs inside the existing isolated multiplier Play Mode fixture.</summary>
public static class FluidMultiplierValidation
{
    public static void RunChecks(MatchScoreService service, PlayerControllerScript player,
        PlayerControllerScript opponent, Action<bool, string> check, List<Object> owned)
    {
        PlayerScoreChain chain = player.GetComponent<PlayerScoreChain>();
        long[] expectedScores = { 100, 106, 113 };
        for (int i = 0; i < expectedScores.Length; i++)
        {
            check(service.TryAwardToPlayer(ScoreRewardKeys.DroneDefeat, player,
                    "decimal-drone-" + i, Vector3.zero, out ScoreAwardResult award) &&
                  award.finalMilliElectronVolts == expectedScores[i],
                "Drone award uses the previous fractional multiplier with nearest-meV rounding");
        }
        check(chain.CurrentMultiplier == 1.1875 && chain.IsChaining &&
              PlayerScoreChainPresenter.FormatMultiplier(chain.CurrentMultiplier) == "1.19",
            "Three Drone kills display 1.19 while retaining full scoring precision");
        check(service.GetTeamScore(1) == 319 &&
              Mathf.Approximately(chain.Progress01, 0.0125f),
            "Fractional charge changes both actual score and full-range progress");
        service.AdvanceTeamAmplifier(1);
        check(service.TryAwardToPlayer(ScoreRewardKeys.DroneDefeat, player,
                "decimal-amplified", Vector3.zero, out ScoreAwardResult combined) &&
              combined.multiplier == 1.1875 && combined.combinedMultiplier == 2.375 &&
              combined.finalMilliElectronVolts == 238,
            "Fractional personal and integer team multipliers compose before rounding");
        check(service.GetContributionSnapshot()[0].highestMultiplier == 1.25 &&
              opponent.GetComponent<PlayerScoreChain>().CurrentMultiplier == 1d,
            "Contribution telemetry retains decimals and other players remain independent");
        double beforeDuplicate = chain.CurrentMultiplier;
        check(!service.TryAwardToPlayer(ScoreRewardKeys.DroneDefeat, player,
                  "decimal-amplified", Vector3.zero, out _) && chain.CurrentMultiplier == beforeDuplicate,
            "Duplicate awards cannot grow fractional charge");

        check(EnergyScoreMath.SaturatingScale(1000000000000001L, 1.25) == 1250000000000001L &&
              EnergyScoreMath.SaturatingScale(long.MaxValue, 1d) == long.MaxValue &&
              EnergyScoreMath.SaturatingScale(long.MaxValue, 0.5) == 4611686018427387904L,
            "Fractional score math preserves integer precision at TeV and Int64 limits");
        check(EnergyScoreMath.SaturatingScale(long.MaxValue / 2, 3d) == long.MaxValue &&
              EnergyScoreMath.SaturatingScale(100, double.PositiveInfinity) == long.MaxValue &&
              EnergyScoreMath.SaturatingScale(100, double.NaN) == 0 &&
              EnergyScoreMath.SaturatingScale(100, -1d) == 0,
            "Invalid multipliers and overflow cannot corrupt score totals");
        check(PlayerScoreChainPresenter.FormatMultiplier(1d) == "1" &&
              PlayerScoreChainPresenter.FormatMultiplier(2.5) == "2.5" &&
              PlayerScoreChainPresenter.FormatMultiplier(15.994) == "15.99" &&
              PlayerScoreChainPresenter.FormatMultiplier(16d) == "16",
            "UI shows at most two decimals without trailing zero clutter");

        chain.ResetChain();
        float previousProgress = 0f;
        bool monotonic = true;
        for (int i = 1; i <= 112; i++)
        {
            chain.ApplyAward(ScoreChainAwardMode.AdvanceAndRefresh, 0.25f);
            float progress = chain.Progress01;
            monotonic &= progress > previousProgress && progress <= 1f;
            previousProgress = progress;
        }
        check(monotonic, "All 112 Drone-sized increments advance the bar through former tier boundaries");
        chain.ApplyAward(ScoreChainAwardMode.AdvanceAndRefresh, 1000f);
        check(chain.CurrentMultiplier == 16d && chain.IsAtMaxMultiplier && chain.Progress01 == 1f,
            "Full charge and overshoot clamp at the authored maximum");

        GameObject field = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PLAYING FIELD.prefab");
        PlayerScoreChainPresenter[] widgets = field.GetComponentsInChildren<PlayerScoreChainPresenter>(true);
        check(widgets.Length == 4, "Four player widgets are available");
        foreach (PlayerScoreChainPresenter source in widgets)
        {
            var holder = new GameObject("Isolated multiplier widget");
            holder.SetActive(false);
            owned.Add(holder);
            GameObject widget = Object.Instantiate(source.gameObject, holder.transform, false);
            var presenter = widget.GetComponent<PlayerScoreChainPresenter>();
            var settings = new SerializedObject(presenter);
            settings.FindProperty("chain").objectReferenceValue = chain;
            settings.ApplyModifiedPropertiesWithoutUndo();
            holder.SetActive(true);
            // A newly bound maxed player should present full charge immediately.
            presenter.SendMessage("Update");
            TMP_Text text = (TMP_Text)settings.FindProperty("multiplierText").objectReferenceValue;
            var bar = (Shapes.Rectangle)settings.FindProperty("progressRectangle").objectReferenceValue;
            var track = (Shapes.Rectangle)settings.FindProperty("progressTrackRectangle").objectReferenceValue;
            var border = track.transform.parent.GetComponent<Shapes.Rectangle>();
            check(text.text == "16" && presenter.MaximumEffectsActive &&
                  Mathf.Approximately(bar.Width, track.Width) &&
                  Mathf.Approximately(track.Width, border.Width),
                "Player widget fills its visible frame and shows maximum effects at the cap");
            text.text = "15.99";
            text.ForceMeshUpdate();
            check(!text.isTextOverflowing && text.GetPreferredValues("15.99").x <= text.rectTransform.rect.width,
                "Five-character decimal value fits the number badge");
            int childCount = bar.transform.childCount;
            presenter.enabled = false;
            check(!presenter.MaximumEffectsActive, "Disabling the presenter hides maximum effects");
            presenter.enabled = true;
            presenter.SendMessage("Update");
            check(presenter.MaximumEffectsActive && bar.transform.childCount == childCount,
                "Re-enabling reuses the existing maximum effects");
            chain.ResetChain();
            check(!presenter.MaximumEffectsActive && text.text == "1",
                "Reset immediately clears the maximum effect and restores the base value");
            widget.SetActive(false);
            chain.ApplyAward(ScoreChainAwardMode.AdvanceAndRefresh, 1000f);
        }

        var custom = ScoreChainSettings.RecommendedDefaults();
        custom.multiplierSteps = new[] { 1, 3, 9 };
        custom.chargeRequiredPerTier = new[] { 2f, 6f };
        custom.hitPenalty = ScoreChainHitPenalty.Reset;
        chain.Configure(custom);
        chain.ApplyAward(ScoreChainAwardMode.AdvanceAndRefresh, 1f);
        check(chain.CurrentMultiplier == 2d && Mathf.Approximately(chain.Progress01, 0.125f),
            "Custom control points and cap drive continuous growth and normalized progress");
        chain.SendMessage("OnHitAccepted", new PlayerHitResult { accepted = true, disruptsScoreChain = true });
        check(chain.CurrentMultiplier == 1d, "Hit reset also handles charge below the first old tier");
        custom.hitPenalty = ScoreChainHitPenalty.LoseTime;
        custom.hitTimePenaltySeconds = 1f;
        chain.Configure(custom);
        chain.ApplyAward(ScoreChainAwardMode.AdvanceAndRefresh, 1000f);
        chain.SendMessage("OnHitAccepted", new PlayerHitResult { accepted = true, disruptsScoreChain = true });
        check(chain.CurrentMultiplier == 8d && !chain.IsAtMaxMultiplier,
            "Optional charge penalty can leave the maximum and cross former tier boundaries");
        chain.ApplyAward(ScoreChainAwardMode.AdvanceAndRefresh, float.NaN);
        check(chain.CurrentMultiplier == 8d, "Invalid charge cannot poison multiplier state");
        chain.Configure(ScoreChainSettings.RecommendedDefaults());
    }
}
#endif
