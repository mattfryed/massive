using System;
using UnityEngine;

namespace Massive.Scoring
{
    public sealed partial class MatchScoreService
    {
        private TimeWeightedMultiplier _lightAmplifierStatistics;
        private TimeWeightedMultiplier _darkAmplifierStatistics;
        private double _lastMultiplierSampleTime;
        private double MultiplierTime => _bonusScoringOnly ? Time.unscaledTimeAsDouble : Time.timeAsDouble;

        // Integrate the OLD values at each change, including changes which award no
        // score. Scaled game time freezes during pause; closed scoring excludes
        // countdowns/transitions. Core Collapse uses unscaled time while its arena
        // is frozen, so that scoring-active bonus still counts toward the average.
        private void SampleMultiplierStatistics()
        {
            double now = MultiplierTime;
            // FixedUpdate and Update can expose slightly different clocks in one frame.
            double elapsed = Math.Max(0d, now - _lastMultiplierSampleTime);
            _lastMultiplierSampleTime = Math.Max(now, _lastMultiplierSampleTime);
            if (!_scoringOpen || elapsed <= 0d) return;

            _lightAmplifierStatistics.Accumulate(GetTeamAmplifierMultiplier(1), elapsed);
            _darkAmplifierStatistics.Accumulate(GetTeamAmplifierMultiplier(2), elapsed);
            foreach (var state in _statistics.Values)
                state.totals.personalMultiplier.Accumulate(state.currentMultiplier, elapsed);
        }

        private void ResetMultiplierStatistics()
        {
            _lightAmplifierStatistics = default;
            _darkAmplifierStatistics = default;
            _lastMultiplierSampleTime = MultiplierTime;
        }

        private void TrackPlayerMultiplier(PlayerControllerScript player)
        {
            var state = EnsurePlayerStatistics(player);
            var chain = player.GetComponent<PlayerScoreChain>();
            if (state.chain == chain) return;
            if (state.chain) state.chain.Changed -= OnPlayerMultiplierChanged;
            state.chain = chain;
            state.currentMultiplier = chain ? chain.CurrentMultiplier : 1d;
            if (chain) chain.Changed += OnPlayerMultiplierChanged;
        }

        private void StopTrackingPlayerMultiplier(PlayerControllerScript player)
        {
            SampleMultiplierStatistics();
            if (!_statistics.TryGetValue(player.playerID, out var state)) return;
            if (state.chain) state.chain.Changed -= OnPlayerMultiplierChanged;
            state.chain = null;
            state.currentMultiplier = 1d;
        }

        private void OnPlayerMultiplierChanged(PlayerScoreChain chain)
        {
            SampleMultiplierStatistics();
            if (chain.Player && _statistics.TryGetValue(chain.Player.playerID, out var state) && state.chain == chain)
                state.currentMultiplier = chain.CurrentMultiplier;
        }

        public TimeWeightedMultiplier GetTeamAmplifierStatistics(int teamID)
        {
            SampleMultiplierStatistics();
            return teamID == 1 ? _lightAmplifierStatistics : teamID == 2 ? _darkAmplifierStatistics : default;
        }
    }
}
