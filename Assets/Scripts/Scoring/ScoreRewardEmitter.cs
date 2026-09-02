using System;
using System.Threading;
using UnityEngine;

namespace Massive.Scoring
{
    /// <summary>
    /// Attach this component to a rewardable prefab. The prefab stores only a
    /// reward key; the numeric value remains centralized in ScoreEconomyProfile.
    /// Component presence is the authoring equivalent of a "score on event" checkbox.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScoreRewardEmitter : MonoBehaviour
    {
        [SerializeField] private bool awardScore = true;
        [SerializeField] private string rewardKey = ScoreRewardKeys.PowerUpClaim;
        [SerializeField] private string sourceTokenPrefix;

        private static long _nextRuntimeEmitterId;

        private readonly long _runtimeEmitterId = Interlocked.Increment(ref _nextRuntimeEmitterId);
        private int _sourceLife;

        public string RewardKey => rewardKey;
        public bool AwardScore => awardScore;

        public void SetRewardKey(string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                rewardKey = value.Trim();
        }

        private void OnEnable()
        {
            unchecked
            {
                _sourceLife++;
                if (_sourceLife <= 0) _sourceLife = 1;
            }
        }

        public bool TryAward(PlayerControllerScript earner, Vector3 worldPosition, long quantity = 1L)
        {
            return TryAward(earner, worldPosition, quantity, eventSuffix: null, out _);
        }

        public bool TryAward(
            PlayerControllerScript earner,
            Vector3 worldPosition,
            long quantity,
            string eventSuffix,
            out ScoreAwardResult result)
        {
            result = default;
            if (!awardScore || earner == null) return false;

            MatchScoreService service = MatchScoreService.Instance;
            if (service == null) return false;

            string token = BuildSourceToken(eventSuffix);
            return service.TryAwardToPlayer(rewardKey, earner, token, worldPosition, out result, quantity);
        }

        public bool TryAwardToTeam(
            int teamID,
            PlayerControllerScript multiplierOwner,
            Vector3 worldPosition,
            long quantity = 1L,
            string eventSuffix = null)
        {
            if (!awardScore) return false;

            MatchScoreService service = MatchScoreService.Instance;
            if (service == null) return false;

            string token = BuildSourceToken(eventSuffix);
            return service.TryAwardToTeam(
                rewardKey,
                teamID,
                multiplierOwner,
                token,
                worldPosition,
                out _,
                quantity);
        }

        private string BuildSourceToken(string suffix)
        {
            string authoredPrefix = string.IsNullOrWhiteSpace(sourceTokenPrefix)
                ? name
                : sourceTokenPrefix.Trim();
            string uniquePrefix = $"{authoredPrefix}-{_runtimeEmitterId}";

            if (string.IsNullOrWhiteSpace(suffix))
                return $"{uniquePrefix}|LIFE-{_sourceLife}";

            return $"{uniquePrefix}|LIFE-{_sourceLife}|{suffix.Trim()}";
        }
    }
}
