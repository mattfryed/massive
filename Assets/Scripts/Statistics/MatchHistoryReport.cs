using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Massive.Scoring;

namespace Massive.Statistics
{
    public sealed class LevelPlayStatistics
    {
        public string levelId, title;
        public GameMode mode;
        public int started, completed, abandoned, interrupted, inProgress, lightWins, darkWins, ties;
        public double elapsedSeconds, activeSeconds;
        public long bestCompletedScore;
    }

    public static class MatchHistoryReport
    {
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        public static List<LevelPlayStatistics> Summarize(IEnumerable<MatchHistoryRecord> records)
        {
            return records.GroupBy(r => (r.levelId, r.result.mode)).Select(group =>
            {
                var row = new LevelPlayStatistics { levelId = group.Key.levelId, mode = group.Key.mode,
                    title = group.OrderByDescending(r => r.startedUtc, StringComparer.Ordinal).First().result.stageTitle };
                foreach (var record in group)
                {
                    row.started++;
                    row.elapsedSeconds += record.elapsedSeconds;
                    row.activeSeconds += record.ActiveSeconds;
                    switch (record.status)
                    {
                        case MatchHistoryStatus.Completed:
                            row.completed++;
                            if (record.result.winner == TeamSide.Light) row.lightWins++;
                            else if (record.result.winner == TeamSide.Dark) row.darkWins++;
                            else row.ties++;
                            row.bestCompletedScore = Math.Max(row.bestCompletedScore, Math.Max(record.result.LightScore, record.result.DarkScore));
                            break;
                        case MatchHistoryStatus.Abandoned: row.abandoned++; break;
                        case MatchHistoryStatus.Interrupted: row.interrupted++; break;
                        default: row.inProgress++; break;
                    }
                }
                return row;
            }).OrderBy(r => r.title, StringComparer.Ordinal).ThenBy(r => r.mode).ToList();
        }

        public static string Mode(GameMode mode) => mode == GameMode.TwoVTwo ? "2v2" : "1v1";
        public static string Duration(double seconds) => TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss", Culture);

        /// <summary>Four related tables keep player/reward arrays useful without flattening or dropping them.</summary>
        public static void Export(string directory, IReadOnlyList<MatchHistoryRecord> records)
        {
            Directory.CreateDirectory(directory);
            Write(Path.Combine(directory, "level-summary.csv"), SummaryCsv(records));
            Write(Path.Combine(directory, "matches.csv"), MatchesCsv(records));
            Write(Path.Combine(directory, "players.csv"), PlayersCsv(records));
            Write(Path.Combine(directory, "rewards.csv"), RewardsCsv(records));
        }

        private static void Write(string path, string contents) => File.WriteAllText(path, contents, new UTF8Encoding(true));

        public static string SummaryCsv(IEnumerable<MatchHistoryRecord> records)
        {
            var csv = new StringBuilder();
            Row(csv, "level_id", "level", "mode", "started", "completed", "abandoned", "interrupted", "in_progress",
                "light_wins", "dark_wins", "ties", "elapsed_seconds", "active_seconds", "best_completed_team_score_meV");
            foreach (var r in Summarize(records)) Row(csv, r.levelId, r.title, Mode(r.mode), r.started, r.completed, r.abandoned,
                r.interrupted, r.inProgress, r.lightWins, r.darkWins, r.ties, r.elapsedSeconds, r.activeSeconds, r.bestCompletedScore);
            return csv.ToString();
        }

        public static string MatchesCsv(IEnumerable<MatchHistoryRecord> records)
        {
            var csv = new StringBuilder();
            Row(csv, "match_id", "application_run_id", "started_utc", "last_saved_utc", "ended_utc", "level_id", "level", "stage_number", "scene", "mode",
                "status", "end_reason", "elapsed_seconds", "active_regulation_seconds", "active_bonus_seconds", "configured_regulation_seconds", "match_bonus_clock_seconds",
                "winner", "light_score_meV", "dark_score_meV", "has_bonus", "regulation_light_meV", "regulation_dark_meV", "bonus_light_meV", "bonus_dark_meV",
                "light_average_amplifier", "dark_average_amplifier", "light_amplifier_seconds", "light_amplifier_sample_seconds", "dark_amplifier_seconds", "dark_amplifier_sample_seconds",
                "ruleset_id", "ruleset_version", "scoring_settings_hash", "game_version", "build_id", "environment", "development", "high_score_eligible", "eligibility_reason", "leaderboard_key");
            foreach (var r in records)
            {
                var s = r.result;
                Row(csv, r.matchId, r.applicationRunId, r.startedUtc, r.lastSavedUtc, r.endedUtc, r.levelId, s.stageTitle, s.stageNumber, s.gameplaySceneName, Mode(s.mode),
                    r.status, r.endReason, r.elapsedSeconds, r.activeRegulationSeconds, r.activeBonusSeconds, s.regulationDurationSeconds, s.bonusDurationSeconds,
                    r.status == MatchHistoryStatus.Completed ? s.winner.ToString() : "", s.LightScore, s.DarkScore, s.hasBonusBreakdown,
                    s.regulationLightMilliElectronVolts, s.regulationDarkMilliElectronVolts, s.bonusLightMilliElectronVolts, s.bonusDarkMilliElectronVolts,
                    s.lightAmplifier.Average, s.darkAmplifier.Average, s.lightAmplifier.multiplierSeconds, s.lightAmplifier.durationSeconds,
                    s.darkAmplifier.multiplierSeconds, s.darkAmplifier.durationSeconds, s.scoringRulesetId, s.scoringRulesetVersion, r.scoringSettingsHash,
                    r.gameVersion, r.buildId, r.environment, r.development, r.HighScoreEligible, r.eligibilityReason, r.LeaderboardKey);
            }
            return csv.ToString();
        }

        public static string PlayersCsv(IEnumerable<MatchHistoryRecord> records)
        {
            var csv = new StringBuilder();
            Row(csv, "match_id", "level", "mode", "status", "player_slot", "team_id", "individual_score_meV", "base_score_meV", "accepted_awards",
                "highest_scoring_multiplier", "kills", "deaths", "enemies_killed", "distance_meters", "average_personal_multiplier", "multiplier_seconds", "multiplier_sample_seconds");
            foreach (var r in records)
            {
                var stats = r.result.playerStatistics ?? Array.Empty<PlayerMatchStatistics>();
                var scores = r.result.playerContributions ?? Array.Empty<PlayerScoreContribution>();
                var ids = stats.Select(p => (p.playerID, p.teamID)).Union(scores.Select(p => (p.playerID, p.teamID))).OrderBy(p => p.playerID);
                foreach (var id in ids)
                {
                    var p = stats.FirstOrDefault(s => s.playerID == id.playerID && s.teamID == id.teamID);
                    var s = scores.FirstOrDefault(c => c.playerID == id.playerID && c.teamID == id.teamID);
                    Row(csv, r.matchId, r.result.stageTitle, Mode(r.result.mode), r.status, id.playerID + 1, id.teamID,
                        s.finalMilliElectronVolts, s.baseMilliElectronVolts, s.acceptedAwardCount, s.highestMultiplier,
                        p.playerKills, p.deaths, p.enemiesKilled, p.distanceTraveledMeters, p.personalMultiplier.Average,
                        p.personalMultiplier.multiplierSeconds, p.personalMultiplier.durationSeconds);
                }
            }
            return csv.ToString();
        }

        public static string RewardsCsv(IEnumerable<MatchHistoryRecord> records)
        {
            var csv = new StringBuilder();
            Row(csv, "match_id", "level", "mode", "status", "reward_key", "light_awards", "dark_awards", "light_base_meV", "dark_base_meV", "light_final_meV", "dark_final_meV");
            foreach (var r in records)
                foreach (var s in r.result.scoreTelemetry ?? Array.Empty<ScoreTelemetryRecord>())
                    Row(csv, r.matchId, r.result.stageTitle, Mode(r.result.mode), r.status, s.rewardKey, s.lightAwardCount, s.darkAwardCount,
                        s.lightBaseMilliElectronVolts, s.darkBaseMilliElectronVolts, s.lightFinalMilliElectronVolts, s.darkFinalMilliElectronVolts);
            return csv.ToString();
        }

        private static void Row(StringBuilder csv, params object[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0) csv.Append(',');
                string value = Convert.ToString(values[i], Culture) ?? string.Empty;
                // Treat authored text as text when opened in a spreadsheet, not as an executable formula.
                if (values[i] is string && value.Length > 0 && "=+-@\t\r\n".IndexOf(value[0]) >= 0) value = "'" + value;
                csv.Append('"').Append(value.Replace("\"", "\"\"")).Append('"');
            }
            csv.Append("\r\n");
        }
    }
}
