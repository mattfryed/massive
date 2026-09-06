using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Massive.Scoring
{
    [DisallowMultipleComponent]
    public sealed class MatchScoreService : MonoBehaviour
    {
        private sealed class MutableTelemetry
        {
            public int lightCount;
            public int darkCount;
            public long lightBase;
            public long darkBase;
            public long lightFinal;
            public long darkFinal;
        }

        private sealed class MutableContribution
        {
            public int playerID;
            public int teamID;
            public int count;
            public int highestMultiplier = 1;
            public long baseScore;
            public long finalScore;
        }

        public static MatchScoreService Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField] private ScoreEconomyProfile profile;
        [SerializeField] private bool rejectPseudoPlayers = true;
        [SerializeField] private bool logAcceptedAwards;
        [SerializeField] private bool logRejectedAwards;

        private readonly HashSet<string> _deduplicationKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<PlayerControllerScript> _registeredPlayers = new HashSet<PlayerControllerScript>();
        private readonly Dictionary<string, MutableTelemetry> _telemetry = new Dictionary<string, MutableTelemetry>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, MutableContribution> _contributions = new Dictionary<int, MutableContribution>();
        private readonly HashSet<int> _warnedMissingChainPlayerIds = new HashSet<int>();

        private ScoreEconomyProfile _runtimeFallbackProfile;
        private long _lightScore;
        private long _darkScore;
        private int _lightAmplifierTierIndex;
        private int _darkAmplifierTierIndex;
        private bool _scoringOpen;
        private bool _chainClockRunning;

        public event Action<TeamScoreSnapshot> TeamScoreChanged;
        public event Action<ScoreAwardResult> ScoreAwarded;
        public event Action<EnergyTierPromotion> TierPromoted;
        public event Action<TeamAmplifierSnapshot> TeamAmplifierChanged;
        public event Action ScoresReset;

        public ScoreEconomyProfile Profile => profile;
        public bool IsScoringOpen => _scoringOpen;
        public bool IsChainClockRunning => _chainClockRunning;
        public long LightScoreMilliElectronVolts => _lightScore;
        public long DarkScoreMilliElectronVolts => _darkScore;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("[MatchScoreService] More than one service exists in this gameplay scene.", this);
                enabled = false;
                return;
            }

            Instance = this;
            EnsureProfile();
            ResetForMatch(openScoring: false);
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            // Unity can reload scripts while Play Mode remains active. Static
            // fields and the non-serialized registration set are rebuilt, but
            // Awake/Start are not guaranteed to run again on the surviving
            // scene objects. Restore the service handoff here so live tuning
            // does not silently drop score-chain or HUD bindings.
            if (Instance != null && Instance != this)
                return;

            Instance = this;
            EnsureProfile();

            PlayerControllerScript[] activePlayers =
                FindObjectsByType<PlayerControllerScript>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            for (int i = 0; i < activePlayers.Length; i++)
                RegisterPlayer(activePlayers[i]);
        }

        private void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }

        private void OnDestroy()
        {
            foreach (PlayerControllerScript player in _registeredPlayers.ToArray())
                UnregisterPlayer(player);

            if (Instance == this)
                Instance = null;

            if (_runtimeFallbackProfile != null)
                Destroy(_runtimeFallbackProfile);
        }

        public void Configure(ScoreEconomyProfile newProfile, bool resetForMatch = true)
        {
            if (newProfile != null)
            {
                profile = newProfile;
                if (_runtimeFallbackProfile != null && newProfile != _runtimeFallbackProfile)
                {
                    Destroy(_runtimeFallbackProfile);
                    _runtimeFallbackProfile = null;
                }
            }

            EnsureProfile();

            foreach (PlayerControllerScript player in _registeredPlayers)
                EnsureAndConfigureChain(player, resetForMatch);

            if (resetForMatch)
                ResetForMatch(openScoring: false);
        }

        public void RegisterPlayer(PlayerControllerScript player)
        {
            if (player == null || player.IsPseudoPlayer || _registeredPlayers.Contains(player))
                return;

            _registeredPlayers.Add(player);
            player.DeathResolved += OnPlayerDeathResolved;
            EnsureAndConfigureChain(player, reset: true);
        }

        public void UnregisterPlayer(PlayerControllerScript player)
        {
            if (player == null || !_registeredPlayers.Remove(player))
                return;

            player.DeathResolved -= OnPlayerDeathResolved;
        }

        public void ResetForMatch(bool openScoring)
        {
            _lightScore = 0L;
            _darkScore = 0L;
            _lightAmplifierTierIndex = 0;
            _darkAmplifierTierIndex = 0;
            _deduplicationKeys.Clear();
            _telemetry.Clear();
            _contributions.Clear();
            _warnedMissingChainPlayerIds.Clear();
            _scoringOpen = openScoring;
            _chainClockRunning = openScoring;

            foreach (PlayerControllerScript player in _registeredPlayers)
            {
                PlayerScoreChain chain = player != null ? player.GetComponent<PlayerScoreChain>() : null;
                if (chain != null)
                    chain.Configure(profile.ChainSettings, reset: true);
            }

            ScoresReset?.Invoke();
            EmitTeamSnapshot(1, 0L, 0L);
            EmitTeamSnapshot(2, 0L, 0L);
            EmitTeamAmplifierSnapshot(1, 0, 0);
            EmitTeamAmplifierSnapshot(2, 0, 0);
        }

        public void SetScoringState(bool scoringOpen, bool chainClockRunning)
        {
            _scoringOpen = scoringOpen;
            _chainClockRunning = scoringOpen && chainClockRunning;
        }

        public void OpenScoring(bool chainClockRunning = true)
        {
            SetScoringState(true, chainClockRunning);
        }

        public void CloseScoring()
        {
            SetScoringState(false, false);
        }

        public long GetTeamScore(int teamID)
        {
            return teamID == 1 ? _lightScore : teamID == 2 ? _darkScore : 0L;
        }

        public int GetTeamAmplifierTierIndex(int teamID)
        {
            return teamID == 1
                ? _lightAmplifierTierIndex
                : teamID == 2
                    ? _darkAmplifierTierIndex
                    : 0;
        }

        public int GetTeamAmplifierMultiplier(int teamID)
        {
            return profile.TeamAmplifierSettings.GetMultiplier(
                GetTeamAmplifierTierIndex(teamID));
        }

        public void ResetMultipliers()
        {
            foreach (PlayerControllerScript player in _registeredPlayers)
            {
                PlayerScoreChain chain = player != null
                    ? player.GetComponent<PlayerScoreChain>()
                    : null;
                chain?.ResetChain();
            }

            int previousLight = _lightAmplifierTierIndex;
            int previousDark = _darkAmplifierTierIndex;
            _lightAmplifierTierIndex = 0;
            _darkAmplifierTierIndex = 0;
            EmitTeamAmplifierSnapshot(1, previousLight, 0);
            EmitTeamAmplifierSnapshot(2, previousDark, 0);
        }

        public PlayerScoreChain GetPlayerChain(int playerID)
        {
            foreach (PlayerControllerScript player in _registeredPlayers)
            {
                if (player != null && player.playerID == playerID)
                    return player.GetComponent<PlayerScoreChain>();
            }

            return null;
        }

        /// <summary>
        /// Advances the shared Amplifier tier for a team. Returns false only
        /// when scoring is closed, the team is invalid, or the team is already
        /// at its configured cap.
        /// </summary>
        public bool AdvanceTeamAmplifier(int teamID)
        {
            if (!_scoringOpen || (teamID != 1 && teamID != 2))
                return false;

            TeamAmplifierSettings settings = profile.TeamAmplifierSettings;
            int previousIndex = GetTeamAmplifierTierIndex(teamID);
            int currentIndex = Mathf.Min(previousIndex + 1, settings.MaxIndex);
            if (currentIndex == previousIndex)
                return false;

            if (teamID == 1)
                _lightAmplifierTierIndex = currentIndex;
            else
                _darkAmplifierTierIndex = currentIndex;

            EmitTeamAmplifierSnapshot(teamID, previousIndex, currentIndex);
            return true;
        }

        public bool TryAwardToPlayer(
            string rewardKey,
            PlayerControllerScript earner,
            string sourceToken,
            Vector3 worldPosition,
            out ScoreAwardResult result,
            long quantity = 1L)
        {
            int teamID = earner != null ? earner.teamID : 0;
            return TryAwardInternal(rewardKey, teamID, earner, sourceToken, worldPosition, quantity, out result);
        }

        public bool TryAwardToTeam(
            string rewardKey,
            int teamID,
            PlayerControllerScript multiplierOwner,
            string sourceToken,
            Vector3 worldPosition,
            out ScoreAwardResult result,
            long quantity = 1L)
        {
            return TryAwardInternal(rewardKey, teamID, multiplierOwner, sourceToken, worldPosition, quantity, out result);
        }

        public ScoreTelemetryRecord[] GetTelemetrySnapshot()
        {
            return _telemetry
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => new ScoreTelemetryRecord
                {
                    rewardKey = pair.Key,
                    lightAwardCount = pair.Value.lightCount,
                    darkAwardCount = pair.Value.darkCount,
                    lightBaseMilliElectronVolts = pair.Value.lightBase,
                    darkBaseMilliElectronVolts = pair.Value.darkBase,
                    lightFinalMilliElectronVolts = pair.Value.lightFinal,
                    darkFinalMilliElectronVolts = pair.Value.darkFinal
                })
                .ToArray();
        }

        public PlayerScoreContribution[] GetContributionSnapshot()
        {
            return _contributions.Values
                .OrderBy(value => value.playerID)
                .Select(value => new PlayerScoreContribution
                {
                    playerID = value.playerID,
                    teamID = value.teamID,
                    acceptedAwardCount = value.count,
                    highestMultiplier = value.highestMultiplier,
                    baseMilliElectronVolts = value.baseScore,
                    finalMilliElectronVolts = value.finalScore
                })
                .ToArray();
        }

        private bool TryAwardInternal(
            string rewardKey,
            int teamID,
            PlayerControllerScript earner,
            string sourceToken,
            Vector3 worldPosition,
            long quantity,
            out ScoreAwardResult result)
        {
            result = new ScoreAwardResult
            {
                accepted = false,
                rejection = ScoreAwardRejection.None,
                rewardKey = rewardKey,
                sourceToken = sourceToken,
                teamID = teamID,
                playerID = earner != null ? earner.playerID : -1,
                worldPosition = worldPosition,
                quantity = quantity,
                multiplier = 1,
                teamAmplifierMultiplier = 1,
                combinedMultiplier = 1
            };

            if (!_scoringOpen)
                return Reject(ref result, ScoreAwardRejection.ScoringClosed);

            if (string.IsNullOrWhiteSpace(rewardKey) || !profile.TryGetReward(rewardKey, out ScoreRewardRule rule))
                return Reject(ref result, ScoreAwardRejection.InvalidRewardKey);

            result.rewardKey = rule.key;

            if (!rule.enabled)
                return Reject(ref result, ScoreAwardRejection.RewardDisabled);

            if (teamID != 1 && teamID != 2)
                return Reject(ref result, ScoreAwardRejection.InvalidTeam);

            if (earner != null && earner.teamID != teamID)
                return Reject(ref result, ScoreAwardRejection.AttributionMismatch);

            if (quantity <= 0L)
                return Reject(ref result, ScoreAwardRejection.InvalidQuantity);

            if (rule.BaseMilliElectronVolts <= 0L)
                return Reject(ref result, ScoreAwardRejection.InvalidRewardValue);

            if (rule.repeatPolicy == ScoreRepeatPolicy.OncePerSourceToken &&
                string.IsNullOrWhiteSpace(sourceToken))
            {
                return Reject(ref result, ScoreAwardRejection.MissingSourceToken);
            }

            if (rejectPseudoPlayers && earner != null && earner.IsPseudoPlayer)
                return Reject(ref result, ScoreAwardRejection.PseudoPlayer);

            string dedupeKey = BuildDeduplicationKey(rule, sourceToken);
            if (dedupeKey != null && !_deduplicationKeys.Add(dedupeKey))
                return Reject(ref result, ScoreAwardRejection.Duplicate);

            long baseScore = EnergyScoreMath.SaturatingMultiply(rule.BaseMilliElectronVolts, quantity);
            int personalMultiplier = 1;
            PlayerScoreChain chain = null;

            if (earner != null && (rule.multiplierEligible || rule.chainEffect != ScoreChainAwardMode.None))
            {
                chain = earner.GetComponent<PlayerScoreChain>();
                if (chain != null)
                {
                    if (rule.multiplierEligible)
                        personalMultiplier = Mathf.Max(1, chain.CurrentMultiplier);
                }
                else if (_warnedMissingChainPlayerIds.Add(earner.playerID))
                {
                    Debug.LogWarning(
                        $"[MatchScoreService] Player {earner.playerID} has no PlayerScoreChain. Awards will use x1.",
                        earner);
                }
            }

            int teamAmplifierMultiplier = GetTeamAmplifierMultiplier(teamID);
            int combinedMultiplier = SaturatingIntMultiply(
                personalMultiplier,
                teamAmplifierMultiplier);
            long finalScore = EnergyScoreMath.SaturatingMultiply(baseScore, combinedMultiplier);
            long previousTotal = GetTeamScore(teamID);
            long newTotal = EnergyScoreMath.SaturatingAdd(previousTotal, finalScore);

            if (teamID == 1) _lightScore = newTotal;
            else _darkScore = newTotal;

            EnergyUnit previousUnit = EnergyScoreFormatter.GetUnit(previousTotal);
            EnergyUnit newUnit = EnergyScoreFormatter.GetUnit(newTotal);

            result.accepted = true;
            result.rejection = ScoreAwardRejection.None;
            result.baseMilliElectronVolts = baseScore;
            // Keep multiplier as the personal value for compatibility with
            // contribution telemetry and existing presentation code.
            result.multiplier = personalMultiplier;
            result.teamAmplifierMultiplier = teamAmplifierMultiplier;
            result.combinedMultiplier = combinedMultiplier;
            result.finalMilliElectronVolts = finalScore;
            result.previousTeamTotalMilliElectronVolts = previousTotal;
            result.newTeamTotalMilliElectronVolts = newTotal;
            result.previousUnit = previousUnit;
            result.newUnit = newUnit;

            RecordTelemetry(result);

            // The current multiplier applies to this award; chain progression applies
            // to the next qualifying award.
            chain?.ApplyAward(rule.chainEffect, Mathf.Max(0f, rule.chainCharge));
            int highestMultiplierReached = chain != null
                ? Mathf.Max(personalMultiplier, chain.CurrentMultiplier)
                : personalMultiplier;
            RecordContribution(result, earner, highestMultiplierReached);

            ScoreAwarded?.Invoke(result);
            EmitTeamSnapshot(teamID, previousTotal, newTotal);

            if (newUnit > previousUnit)
            {
                TierPromoted?.Invoke(new EnergyTierPromotion
                {
                    teamID = teamID,
                    previousUnit = previousUnit,
                    currentUnit = newUnit,
                    totalMilliElectronVolts = newTotal
                });
            }

            if (logAcceptedAwards)
            {
                string playerLabel = earner != null ? $"P{earner.playerID + 1}" : "TEAM";
                Debug.Log(
                    $"[Score] {playerLabel} / Team {teamID}: {rule.key} +" +
                    $"{EnergyScoreFormatter.FormatWithUnit(finalScore)} (base " +
                    $"{EnergyScoreFormatter.FormatWithUnit(baseScore)} " +
                    $"x{personalMultiplier} personal x{teamAmplifierMultiplier} amplifier) => " +
                    EnergyScoreFormatter.FormatWithUnit(newTotal),
                    this);
            }

            return true;
        }

        private bool Reject(ref ScoreAwardResult result, ScoreAwardRejection rejection)
        {
            result.rejection = rejection;
            if (logRejectedAwards)
            {
                Debug.LogWarning(
                    $"[Score] Rejected '{result.rewardKey}' for team {result.teamID}: {rejection}.",
                    this);
            }
            return false;
        }

        private string BuildDeduplicationKey(ScoreRewardRule rule, string sourceToken)
        {
            switch (rule.repeatPolicy)
            {
                case ScoreRepeatPolicy.Unlimited:
                    return null;

                case ScoreRepeatPolicy.OncePerMatch:
                    return $"MATCH|{rule.key}";

                case ScoreRepeatPolicy.OncePerSourceToken:
                default:
                    return $"SOURCE|{rule.key}|{sourceToken}";
            }
        }

        private void EmitTeamSnapshot(int teamID, long previousTotal, long currentTotal)
        {
            TeamScoreChanged?.Invoke(new TeamScoreSnapshot
            {
                teamID = teamID,
                previousMilliElectronVolts = previousTotal,
                currentMilliElectronVolts = currentTotal,
                previousUnit = EnergyScoreFormatter.GetUnit(previousTotal),
                currentUnit = EnergyScoreFormatter.GetUnit(currentTotal),
                tierProgress01 = EnergyScoreFormatter.GetTierProgress01(currentTotal)
            });
        }

        private void EmitTeamAmplifierSnapshot(
            int teamID,
            int previousTierIndex,
            int currentTierIndex)
        {
            TeamAmplifierSettings settings = profile.TeamAmplifierSettings;
            TeamAmplifierChanged?.Invoke(new TeamAmplifierSnapshot
            {
                teamID = teamID,
                previousTierIndex = previousTierIndex,
                currentTierIndex = currentTierIndex,
                previousMultiplier = settings.GetMultiplier(previousTierIndex),
                currentMultiplier = settings.GetMultiplier(currentTierIndex),
                isAtMaximum = currentTierIndex >= settings.MaxIndex
            });
        }

        private static int SaturatingIntMultiply(int a, int b)
        {
            long value = (long)Mathf.Max(1, a) * Mathf.Max(1, b);
            return value >= int.MaxValue ? int.MaxValue : (int)value;
        }

        private void RecordTelemetry(ScoreAwardResult result)
        {
            if (!_telemetry.TryGetValue(result.rewardKey, out MutableTelemetry stats))
            {
                stats = new MutableTelemetry();
                _telemetry.Add(result.rewardKey, stats);
            }

            if (result.teamID == 1)
            {
                stats.lightCount++;
                stats.lightBase = EnergyScoreMath.SaturatingAdd(stats.lightBase, result.baseMilliElectronVolts);
                stats.lightFinal = EnergyScoreMath.SaturatingAdd(stats.lightFinal, result.finalMilliElectronVolts);
            }
            else
            {
                stats.darkCount++;
                stats.darkBase = EnergyScoreMath.SaturatingAdd(stats.darkBase, result.baseMilliElectronVolts);
                stats.darkFinal = EnergyScoreMath.SaturatingAdd(stats.darkFinal, result.finalMilliElectronVolts);
            }
        }

        private void RecordContribution(
            ScoreAwardResult result,
            PlayerControllerScript earner,
            int highestMultiplierReached)
        {
            if (earner == null) return;

            if (!_contributions.TryGetValue(earner.playerID, out MutableContribution contribution))
            {
                contribution = new MutableContribution
                {
                    playerID = earner.playerID,
                    teamID = earner.teamID
                };
                _contributions.Add(earner.playerID, contribution);
            }

            contribution.count++;
            contribution.highestMultiplier = Mathf.Max(
                contribution.highestMultiplier,
                Mathf.Max(result.multiplier, highestMultiplierReached));
            contribution.baseScore = EnergyScoreMath.SaturatingAdd(contribution.baseScore, result.baseMilliElectronVolts);
            contribution.finalScore = EnergyScoreMath.SaturatingAdd(contribution.finalScore, result.finalMilliElectronVolts);
        }

        private void OnPlayerDeathResolved(PlayerDeathContext death)
        {
            PlayerControllerScript killer = death.killer;
            PlayerControllerScript victim = death.victim;

            if (killer == null || victim == null) return;
            if (killer == victim) return;
            if (killer.teamID == victim.teamID) return;
            if (killer.IsPseudoPlayer) return;

            string token = $"PLAYER-{victim.GetInstanceID()}-LIFE-{death.victimLifeSequence}";
            TryAwardToPlayer(
                ScoreRewardKeys.PlayerDefeat,
                killer,
                token,
                death.worldPosition,
                out _);
        }

        private void EnsureProfile()
        {
            if (profile != null) return;

            if (_runtimeFallbackProfile == null)
                _runtimeFallbackProfile = ScoreEconomyProfile.CreateRuntimeDefaults();

            profile = _runtimeFallbackProfile;
            Debug.LogWarning(
                "[MatchScoreService] No ScoreEconomyProfile was assigned. Using temporary runtime defaults. " +
                "Create and assign an asset before balance work.",
                this);
        }

        private void EnsureAndConfigureChain(PlayerControllerScript player, bool reset)
        {
            if (player == null) return;

            PlayerScoreChain chain = player.GetComponent<PlayerScoreChain>();
            if (chain == null)
                chain = player.gameObject.AddComponent<PlayerScoreChain>();

            chain.Configure(profile.ChainSettings, reset);
        }
    }
}
