using System;
using System.Globalization;
using Massive.Scoring;

namespace Massive.Statistics
{
    public enum MatchHistoryStatus { InProgress, Completed, Abandoned, Interrupted }

    /// <summary>Disk contract. Only values and snapshots belong here, never Unity object references.</summary>
    [Serializable]
    public sealed class MatchHistoryRecord
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string matchId;
        public string applicationRunId;
        public int revision;
        public string startedUtc;
        public string lastSavedUtc;
        public string endedUtc;
        public string levelId;
        public string gameVersion;
        public string buildId;
        public bool development;
        public string environment;
        public MatchHistoryStatus status;
        public string endReason;
        public double elapsedSeconds;
        public double activeRegulationSeconds;
        public double activeBonusSeconds;
        public string scoringSettingsHash;
        public bool standardRules;
        public string eligibilityReason;
        public MatchResult result;

        public double ActiveSeconds => activeRegulationSeconds + activeBonusSeconds;
        public bool HighScoreEligible => status == MatchHistoryStatus.Completed && !development && standardRules;
        public string LeaderboardKey => string.Join("/", levelId, result.mode.ToString(), result.scoringRulesetId,
            result.scoringRulesetVersion.ToString(CultureInfo.InvariantCulture), scoringSettingsHash,
            result.regulationDurationSeconds.ToString("R", CultureInfo.InvariantCulture));
    }
}
