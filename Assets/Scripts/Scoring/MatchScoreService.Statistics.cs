using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Massive.Scoring
{
    public sealed partial class MatchScoreService
    {
        private sealed class PlayerStatisticsState
        {
            public PlayerMatchStatistics totals;
            public Rigidbody body;
            public Vector3 previousPosition;
            public int previousLife;
            public bool hasPosition;
        }

        private readonly Dictionary<int, PlayerStatisticsState> _statistics = new();
        private readonly HashSet<string> _countedDeaths = new();
        private readonly HashSet<string> _countedEnemyDefeats = new();

        private PlayerStatisticsState EnsurePlayerStatistics(PlayerControllerScript player)
        {
            if (!_statistics.TryGetValue(player.playerID, out var state))
            {
                state = new PlayerStatisticsState
                {
                    totals = new PlayerMatchStatistics { playerID = player.playerID, teamID = player.teamID },
                    body = player.GetComponent<Rigidbody>()
                };
                _statistics.Add(player.playerID, state);
            }
            return state;
        }

        private void ResetStatistics()
        {
            _statistics.Clear();
            _countedDeaths.Clear();
            _countedEnemyDefeats.Clear();
            foreach (var player in _registeredPlayers)
                if (player && player.isActiveAndEnabled && !player.IsPseudoPlayer)
                    EnsurePlayerStatistics(player);
        }

        private void FixedUpdate() => SampleDistances();

        private void SampleDistances()
        {
            foreach (var player in _registeredPlayers)
            {
                if (!player || player.IsPseudoPlayer) continue;
                var state = EnsurePlayerStatistics(player);
                Vector3 position = state.body ? state.body.position : player.transform.position;
                bool eligible = _scoringOpen && Time.timeScale > 0f && player.isActiveAndEnabled &&
                    !player.IsSpawning && !player.IsMatchInputLocked && !player.IsSingularityTransitControlled;
                if (eligible && state.hasPosition && state.previousLife == player.LifeSequence)
                    state.totals.distanceTraveledMeters += Vector3.Distance(state.previousPosition, position);
                state.previousPosition = position;
                state.previousLife = player.LifeSequence;
                state.hasPosition = eligible;
            }
        }

        private void ResetDistanceSamples()
        {
            foreach (var state in _statistics.Values) state.hasPosition = false;
        }

        public void ResetPlayerDistanceSample(PlayerControllerScript player)
        {
            if (player && _statistics.TryGetValue(player.playerID, out var state))
                state.hasPosition = false;
        }

        private bool CanRecordStatistics(PlayerControllerScript player) =>
            _scoringOpen && player && player.isActiveAndEnabled && !player.IsPseudoPlayer &&
            (player.teamID == 1 || player.teamID == 2) && _registeredPlayers.Contains(player);

        private void RecordPlayerDeath(PlayerDeathContext death)
        {
            if (!CanRecordStatistics(death.victim)) return;
            string token = $"{death.victim.GetInstanceID()}:{death.victimLifeSequence}";
            if (!_countedDeaths.Add(token)) return;
            var victim = EnsurePlayerStatistics(death.victim);
            if (victim.hasPosition)
                victim.totals.distanceTraveledMeters += Vector3.Distance(victim.previousPosition,
                    victim.body ? victim.body.position : death.worldPosition);
            victim.totals.deaths++;
            victim.hasPosition = false;
            if (CanRecordStatistics(death.killer) && death.killer != death.victim &&
                death.killer.teamID != death.victim.teamID)
                EnsurePlayerStatistics(death.killer).totals.playerKills++;
        }

        // Counts confirmed defeats independently of reward tuning or multipliers.
        public void RecordEnemyDefeat(PlayerControllerScript player, string lifeToken)
        {
            if (!CanRecordStatistics(player) || string.IsNullOrEmpty(lifeToken) ||
                !_countedEnemyDefeats.Add(lifeToken)) return;
            EnsurePlayerStatistics(player).totals.enemiesKilled++;
        }

        public PlayerMatchStatistics[] GetStatisticsSnapshot() =>
            _statistics.Values.OrderBy(state => state.totals.playerID).Select(state => state.totals).ToArray();
    }
}
