using System;
using System.Collections.Generic;
using UnityEngine;

namespace Massive.Scoring
{
    public enum ScoreChainAwardMode
    {
        None = 0,
        RefreshOnly = 1,
        AdvanceAndRefresh = 2
    }

    public enum ScoreRepeatPolicy
    {
        Unlimited = 0,
        OncePerSourceToken = 1,
        OncePerMatch = 2
    }

    public enum ScoreChainHitPenalty
    {
        None = 0,
        LoseTime = 1,
        DropOneTier = 2,
        Reset = 3
    }

    [Serializable]
    public struct ScoreChainSettings
    {
        [Tooltip("Multiplier values in ascending order. Index 0 should normally be x1.")]
        public int[] multiplierSteps;

        [Min(0.05f)]
        public float timeoutSeconds;

        public bool useUnscaledTime;
        public ScoreChainHitPenalty hitPenalty;

        [Tooltip("Used only when Hit Penalty is Lose Time.")]
        [Min(0f)]
        public float hitTimePenaltySeconds;

        public int GetMultiplier(int index)
        {
            if (multiplierSteps == null || multiplierSteps.Length == 0)
                return 1;

            index = Mathf.Clamp(index, 0, multiplierSteps.Length - 1);
            return Mathf.Max(1, multiplierSteps[index]);
        }

        public int MaxIndex => multiplierSteps == null || multiplierSteps.Length == 0
            ? 0
            : multiplierSteps.Length - 1;

        public static ScoreChainSettings RecommendedDefaults()
        {
            return new ScoreChainSettings
            {
                multiplierSteps = new[] { 1, 2, 4, 8, 16 },
                timeoutSeconds = 3.25f,
                useUnscaledTime = true,
                hitPenalty = ScoreChainHitPenalty.DropOneTier,
                hitTimePenaltySeconds = 1f
            };
        }
    }

    [Serializable]
    public class ScoreRewardRule
    {
        [Tooltip("Stable identifier referenced by reward-emitter components.")]
        public string key;

        public bool enabled = true;

        [Tooltip("Human-readable amount entered in the selected unit. The runtime converts this to canonical meV.")]
        [Min(0)]
        public long amount = 1L;

        public EnergyUnit unit = EnergyUnit.ElectronVolt;
        public bool multiplierEligible = true;
        public ScoreChainAwardMode chainEffect = ScoreChainAwardMode.AdvanceAndRefresh;
        public ScoreRepeatPolicy repeatPolicy = ScoreRepeatPolicy.OncePerSourceToken;

        [Tooltip("Optional presentation/telemetry category. It does not affect score math.")]
        public string feedbackId;

        [Tooltip("Editor balancing note only; runtime does not enforce this count.")]
        [Min(0)]
        public int expectedOccurrencesPerRound;

        public long BaseMilliElectronVolts => EnergyScoreMath.ToRawMilliElectronVolts(amount, unit);
    }

    public static class ScoreRewardKeys
    {
        public const string PlayerDefeat = "PLAYER_DEFEAT";
        public const string EnemyDefeat = "ENEMY_DEFEAT";
        public const string PowerUpClaim = "POWER_UP_CLAIM";
        public const string EnergyPickupSmall = "ENERGY_PICKUP_SMALL";
        public const string ObjectiveTick = "OBJECTIVE_TICK";
    }

    [CreateAssetMenu(fileName = "ScoreEconomyProfile", menuName = "MASSIVE/Scoring/Score Economy Profile")]
    public sealed class ScoreEconomyProfile : ScriptableObject
    {
        [Header("Ruleset Identity")]
        [SerializeField] private string rulesetId = "MASSIVE_SCORE_V1";
        [SerializeField, Min(1)] private int rulesetVersion = 1;

        [Header("Round")]
        [SerializeField, Min(1f)] private float regulationDurationSeconds = 120f;

        [Header("Personal Chain")]
        [SerializeField] private ScoreChainSettings chainSettings = default;

        [Header("Central Reward Table")]
        [SerializeField] private List<ScoreRewardRule> rewards = new List<ScoreRewardRule>();

        private Dictionary<string, ScoreRewardRule> _lookup;

        public string RulesetId => string.IsNullOrWhiteSpace(rulesetId) ? "UNVERSIONED" : rulesetId.Trim();
        public int RulesetVersion => Mathf.Max(1, rulesetVersion);
        public float RegulationDurationSeconds => Mathf.Max(1f, regulationDurationSeconds);
        public ScoreChainSettings ChainSettings => SanitizedChainSettings(chainSettings);
        public IReadOnlyList<ScoreRewardRule> Rewards => rewards;

        private void Reset()
        {
            ResetToRecommendedDefaults();
        }

        private void OnEnable()
        {
            if (chainSettings.multiplierSteps == null || chainSettings.multiplierSteps.Length == 0)
                chainSettings = ScoreChainSettings.RecommendedDefaults();

            _lookup = null;
        }

        private void OnValidate()
        {
            regulationDurationSeconds = Mathf.Max(1f, regulationDurationSeconds);
            rulesetVersion = Mathf.Max(1, rulesetVersion);
            chainSettings = SanitizedChainSettings(chainSettings);
            _lookup = null;
        }

        public bool TryGetReward(string key, out ScoreRewardRule rule)
        {
            EnsureLookup();
            if (string.IsNullOrWhiteSpace(key))
            {
                rule = null;
                return false;
            }

            return _lookup.TryGetValue(key.Trim(), out rule) && rule != null;
        }

        [ContextMenu("Reset To Recommended First-Slice Defaults")]
        public void ResetToRecommendedDefaults()
        {
            rulesetId = "MASSIVE_SCORE_V1";
            rulesetVersion = 1;
            regulationDurationSeconds = 120f;
            chainSettings = ScoreChainSettings.RecommendedDefaults();

            rewards = new List<ScoreRewardRule>
            {
                new ScoreRewardRule
                {
                    key = ScoreRewardKeys.PlayerDefeat,
                    enabled = true,
                    amount = 2,
                    unit = EnergyUnit.ElectronVolt,
                    multiplierEligible = true,
                    chainEffect = ScoreChainAwardMode.AdvanceAndRefresh,
                    repeatPolicy = ScoreRepeatPolicy.OncePerSourceToken,
                    feedbackId = "PLAYER_DEFEAT",
                    expectedOccurrencesPerRound = 4
                },
                new ScoreRewardRule
                {
                    key = ScoreRewardKeys.EnemyDefeat,
                    enabled = true,
                    amount = 1,
                    unit = EnergyUnit.ElectronVolt,
                    multiplierEligible = true,
                    chainEffect = ScoreChainAwardMode.AdvanceAndRefresh,
                    repeatPolicy = ScoreRepeatPolicy.OncePerSourceToken,
                    feedbackId = "ENEMY_DEFEAT",
                    expectedOccurrencesPerRound = 20
                },
                new ScoreRewardRule
                {
                    key = ScoreRewardKeys.PowerUpClaim,
                    enabled = true,
                    amount = 500,
                    unit = EnergyUnit.MilliElectronVolt,
                    multiplierEligible = true,
                    chainEffect = ScoreChainAwardMode.AdvanceAndRefresh,
                    repeatPolicy = ScoreRepeatPolicy.OncePerSourceToken,
                    feedbackId = "PICKUP",
                    expectedOccurrencesPerRound = 6
                },
                new ScoreRewardRule
                {
                    key = ScoreRewardKeys.EnergyPickupSmall,
                    enabled = true,
                    amount = 125,
                    unit = EnergyUnit.MilliElectronVolt,
                    multiplierEligible = true,
                    chainEffect = ScoreChainAwardMode.AdvanceAndRefresh,
                    repeatPolicy = ScoreRepeatPolicy.OncePerSourceToken,
                    feedbackId = "ENERGY_PICKUP",
                    expectedOccurrencesPerRound = 12
                },
                new ScoreRewardRule
                {
                    key = ScoreRewardKeys.ObjectiveTick,
                    enabled = true,
                    amount = 25,
                    unit = EnergyUnit.MilliElectronVolt,
                    multiplierEligible = true,
                    chainEffect = ScoreChainAwardMode.RefreshOnly,
                    repeatPolicy = ScoreRepeatPolicy.Unlimited,
                    feedbackId = "OBJECTIVE",
                    expectedOccurrencesPerRound = 20
                }
            };

            _lookup = null;
        }

        public static ScoreEconomyProfile CreateRuntimeDefaults()
        {
            var profile = CreateInstance<ScoreEconomyProfile>();
            profile.hideFlags = HideFlags.HideAndDontSave;
            profile.ResetToRecommendedDefaults();
            return profile;
        }

        private void EnsureLookup()
        {
            if (_lookup != null) return;

            _lookup = new Dictionary<string, ScoreRewardRule>(StringComparer.OrdinalIgnoreCase);
            if (rewards == null) return;

            for (int i = 0; i < rewards.Count; i++)
            {
                ScoreRewardRule reward = rewards[i];
                if (reward == null || string.IsNullOrWhiteSpace(reward.key))
                    continue;

                string normalized = reward.key.Trim();
                if (_lookup.ContainsKey(normalized))
                {
                    Debug.LogWarning($"[{name}] Duplicate score reward key '{normalized}'. The first entry remains authoritative.", this);
                    continue;
                }

                _lookup.Add(normalized, reward);
            }
        }

        private static ScoreChainSettings SanitizedChainSettings(ScoreChainSettings settings)
        {
            if (settings.multiplierSteps == null || settings.multiplierSteps.Length == 0)
                settings.multiplierSteps = new[] { 1 };

            settings.multiplierSteps[0] = 1;
            for (int i = 1; i < settings.multiplierSteps.Length; i++)
            {
                settings.multiplierSteps[i] = Mathf.Max(
                    settings.multiplierSteps[i - 1],
                    settings.multiplierSteps[i]);
            }

            settings.timeoutSeconds = Mathf.Max(0.05f, settings.timeoutSeconds);
            settings.hitTimePenaltySeconds = Mathf.Max(0f, settings.hitTimePenaltySeconds);
            return settings;
        }
    }
}
