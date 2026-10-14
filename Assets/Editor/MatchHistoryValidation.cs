#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Massive.Scoring;
using Massive.Statistics;
using UnityEditor;
using UnityEngine;

public static class MatchHistoryValidation
{
    [Serializable] private sealed class Envelope { public int formatVersion; public string checksum, payload; }
    private static readonly List<string> checks = new List<string>();
    private static string root;

    [MenuItem("MASSIVE/Statistics/Validate Storage and Reports")]
    public static void RunMenu() => Debug.Log(Run());

    public static string Run()
    {
        checks.Clear();
        root = Path.Combine(Path.GetTempPath(), "MASSIVE-MatchHistory-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Storage(); Recovery(); ClocksAndLifecycle(); Reports(); Configuration();
            string report = "PASSED " + checks.Count + " checks\n" + string.Join("\n", checks) + "\nFixtures: " + root;
            File.WriteAllText(Path.Combine(root, "report.txt"), report);
            return report;
        }
        catch (Exception e)
        {
            File.WriteAllText(Path.Combine(root, "report.txt"), "FAILED\n" + string.Join("\n", checks) + "\n" + e);
            throw;
        }
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        checks.Add("PASS " + message);
    }

    private static MatchHistoryStore Store(string name) => new MatchHistoryStore(Path.Combine(root, name));
    private static string PathOf(MatchHistoryStore store, MatchHistoryRecord r) => Path.Combine(store.DirectoryPath, r.matchId + ".json");
    private static MatchHistoryRecord Record(GameMode mode = GameMode.OneVOne)
    {
        var average = new TimeWeightedMultiplier { multiplierSeconds = 122.75, durationSeconds = 15.5 };
        return new MatchHistoryRecord
        {
            matchId = Guid.NewGuid().ToString("N"), applicationRunId = "old-run", startedUtc = "2026-10-14T01:00:00.0000000Z",
            lastSavedUtc = "2026-10-14T01:00:15.0000000Z", levelId = "stable-level-id", revision = 1,
            scoringSettingsHash = "rules-hash", standardRules = true, elapsedSeconds = 15, activeRegulationSeconds = 12,
            result = new MatchResult
            {
                mode = mode, stageTitle = "HIGGS", gameplaySceneName = "S-3_HIGGS", stageNumber = 2,
                regulationDurationSeconds = 180, scoringRulesetId = "MASSIVE_SCORE_V1", scoringRulesetVersion = 6,
                winner = TeamSide.Dark, lightMilliElectronVolts = 9007199254740993L, darkMilliElectronVolts = long.MaxValue - 17,
                lightAmplifier = average, darkAmplifier = average, hasBonusBreakdown = true,
                regulationLightMilliElectronVolts = 100, regulationDarkMilliElectronVolts = 200,
                bonusLightMilliElectronVolts = 300, bonusDarkMilliElectronVolts = 400,
                playerStatistics = new[] { new PlayerMatchStatistics { playerID = 0, teamID = 1, playerKills = 3, deaths = 2, enemiesKilled = 4, distanceTraveledMeters = 123.456, personalMultiplier = average } },
                playerContributions = new[] { new PlayerScoreContribution { playerID = 0, teamID = 1, finalMilliElectronVolts = 9007199254740993L, baseMilliElectronVolts = 100, acceptedAwardCount = 4, highestMultiplier = 12.5 } },
                scoreTelemetry = new[] { new ScoreTelemetryRecord { rewardKey = "PLAYER_DEFEAT", lightAwardCount = 3, darkAwardCount = 4, lightFinalMilliElectronVolts = 321, darkFinalMilliElectronVolts = 456 } }
            }
        };
    }

    private static void Storage()
    {
        var store = Store("roundtrip"); var r = Record();
        Check(store.ReadAll().Count == 0 && !Directory.Exists(store.DirectoryPath), "Missing store reads as empty without creating files");
        store.Save(r);
        var loaded = store.ReadAll().Single();
        Check(loaded.result.lightMilliElectronVolts == 9007199254740993L && loaded.result.darkMilliElectronVolts == long.MaxValue - 17, "Int64 scores remain exact beyond floating-point precision");
        Check(loaded.result.playerContributions[0].finalMilliElectronVolts == r.result.playerContributions[0].finalMilliElectronVolts, "Individual scores round-trip exactly");
        Check(loaded.result.playerStatistics[0].distanceTraveledMeters == 123.456 && loaded.result.playerStatistics[0].playerKills == 3, "Combat and distance snapshots round-trip");
        Check(loaded.result.lightAmplifier.multiplierSeconds == 122.75 && loaded.result.playerStatistics[0].personalMultiplier.durationSeconds == 15.5, "Multiplier integrals and sample times round-trip");
        Check(loaded.result.bonusDarkMilliElectronVolts == 400 && loaded.result.scoreTelemetry[0].darkAwardCount == 4, "Bonus breakdown and reward-source telemetry round-trip");
        r.status = MatchHistoryStatus.Completed; r.revision++; store.Save(r);
        Check(File.Exists(PathOf(store, r) + ".bak") && !File.Exists(PathOf(store, r) + ".tmp"), "Replacement retains backup and consumes temporary file");
        Check(store.ReadAll().Count == 1 && store.ReadAll()[0].status == MatchHistoryStatus.Completed, "Backups do not double-count a match");
        r.matchId = "../outside";
        bool rejected = false; try { store.Save(r); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, "Invalid/path-traversal match IDs are rejected");
        r = Record(); r.elapsedSeconds = double.NaN;
        rejected = false; try { store.Save(r); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, "Invalid duration data cannot be written");
    }

    private static void Recovery()
    {
        var store = Store("recovery"); var r = Record(); store.Save(r);
        r.revision++; r.elapsedSeconds = 30; store.Save(r);
        File.WriteAllText(PathOf(store, r), "{ broken json");
        var recovered = store.ReadAll().Single();
        Check(recovered.elapsedSeconds == 15 && store.Issues.Count > 0, "Corrupt primary falls back to a valid backup with a warning");
        Check(store.RecoverInterrupted("new-run") == 1, "Previous-run unfinished match becomes interrupted");
        recovered = store.ReadAll().Single();
        Check(recovered.status == MatchHistoryStatus.Interrupted && recovered.elapsedSeconds == 15 && recovered.endedUtc == r.lastSavedUtc, "Crash recovery retains last checkpoint time without inventing downtime");
        Check(store.RecoverInterrupted("new-run") == 0 && Directory.GetFiles(store.DirectoryPath, "*.recovered-*").Length >= 2, "Recovery is idempotent and preserves original candidates");

        store = Store("temporary"); Directory.CreateDirectory(store.DirectoryPath); r = Record();
        File.WriteAllText(PathOf(store, r) + ".tmp", MatchHistoryStore.Encode(r));
        Check(store.ReadAll().Single().matchId == r.matchId, "First-write temporary file can be recovered before initial rename");
        Check(store.RecoverInterrupted("new-run") == 1 && store.ReadAll().Single().status == MatchHistoryStatus.Interrupted, "Temporary-only record is safely finalized on restart");

        store = Store("newer-temporary"); r = Record(); store.Save(r);
        r.revision++; r.status = MatchHistoryStatus.Completed;
        File.WriteAllText(PathOf(store, r) + ".tmp", MatchHistoryStore.Encode(r));
        Check(store.ReadAll().Single().status == MatchHistoryStatus.Completed, "Newer fully-written final temporary record wins over older primary");

        store = Store("unsupported"); r = Record(); store.Save(r); r.revision++; store.Save(r);
        string future = MatchHistoryStore.Encode(r).Replace("\"formatVersion\": 1", "\"formatVersion\": 2");
        File.WriteAllText(PathOf(store, r), future);
        Check(store.ReadAll().Count == 0 && store.Issues.Any(i => i.Contains("Unknown file format")), "Newer format is skipped rather than replaced by an older backup");
        store.RecoverInterrupted("new-run");
        Check(File.ReadAllText(PathOf(store, r)) == future, "Unsupported future data stays byte-for-byte intact");
        var envelope = JsonUtility.FromJson<Envelope>(MatchHistoryStore.Encode(r));
        envelope.payload = envelope.payload.Replace("\"schemaVersion\":1", "\"schemaVersion\":2"); envelope.checksum = MatchHistoryStore.Hash(envelope.payload);
        string schemaFuture = JsonUtility.ToJson(envelope); File.WriteAllText(PathOf(store, r), schemaFuture);
        Check(store.ReadAll().Count == 0 && store.Issues.Any(i => i.Contains("Newer save schema")), "Newer payload schema is preserved");

        store = Store("empty-primary"); r = Record(); store.Save(r); r.revision++; store.Save(r); File.WriteAllText(PathOf(store, r), "{}");
        Check(store.ReadAll().Count == 1, "Missing envelope fields are corruption, allowing backup recovery");
        store = Store("current-run"); r = Record(); store.Save(r);
        Check(store.RecoverInterrupted("old-run") == 0 && store.ReadAll()[0].status == MatchHistoryStatus.InProgress, "Recovery never finalizes an active record from this run");
    }

    private static void ClocksAndLifecycle()
    {
        var store = Store("lifecycle"); var r = Record(); r.elapsedSeconds = r.activeRegulationSeconds = 0;
        var errors = new List<string>();
        var recorder = new MatchHistoryRecorder(store, r, 100, 10, errors.Add);
        Check(store.ReadAll().Single().status == MatchHistoryStatus.InProgress, "Start is saved before the first checkpoint");
        recorder.Sample(105, 15, MatchRuntimePhase.Regulation, true, true, false);
        recorder.Sample(115, 25, MatchRuntimePhase.Regulation, true, false, false);
        recorder.Sample(117, 27, MatchRuntimePhase.FinaleEntry, false, false, false);
        recorder.Sample(120, 30, MatchRuntimePhase.Bonus, true, false, false);
        recorder.Sample(140, 50, MatchRuntimePhase.Bonus, true, false, true);
        Check(r.elapsedSeconds == 40 && r.activeRegulationSeconds == 5 && r.activeBonusSeconds == 3, "Elapsed, paused regulation, transitions, frozen-world bonus and app suspension are distinct");
        Check(recorder.CheckpointDue, "Checkpoint becomes due after fifteen elapsed seconds");
        recorder.Checkpoint(r.result, r.scoringSettingsHash);
        var result = r.result; result.lightMilliElectronVolts = 123456;
        recorder.Finish(MatchHistoryStatus.Completed, "Finished", result, r.scoringSettingsHash);
        var loaded = store.ReadAll().Single();
        Check(loaded.status == MatchHistoryStatus.Completed && loaded.result.LightScore == 123456, "Final save waits for a queued checkpoint and wins");
        int revision = loaded.revision;
        recorder.Finish(MatchHistoryStatus.Abandoned, "Duplicate callback", result, r.scoringSettingsHash);
        recorder.Checkpoint(result, r.scoringSettingsHash);
        recorder.Sample(200, 100, MatchRuntimePhase.Regulation, true, true, false);
        Check(store.ReadAll().Single().revision == revision && r.elapsedSeconds == 40, "Repeated completion, late checkpoint and late clock callbacks cannot overwrite a final result");
        Check(loaded.HighScoreEligible, "Completed production match with standard rules qualifies for later leaderboards");
        r.development = true; Check(!r.HighScoreEligible, "Development matches are ineligible");
        r.development = false; r.status = MatchHistoryStatus.Abandoned; Check(!r.HighScoreEligible, "Abandoned matches are ineligible");
        r = Record(); recorder = new MatchHistoryRecorder(Store("changed-rules"), r, 0, 0, errors.Add);
        recorder.Finish(MatchHistoryStatus.Completed, "Finished", r.result, "changed-hash");
        Check(!r.HighScoreEligible && r.eligibilityReason.Contains("changed"), "Mid-match scoring changes permanently exclude high scores");
        r = Record(); recorder = new MatchHistoryRecorder(Store("changed-duration"), r, 0, 0, errors.Add); result = r.result; result.regulationDurationSeconds = 1200;
        recorder.Finish(MatchHistoryStatus.Completed, "Finished", result, r.scoringSettingsHash);
        Check(!r.HighScoreEligible && r.result.regulationDurationSeconds == 180, "Changed match duration is excluded and original identity is retained");
        string blocked = Path.Combine(root, "not-a-directory"); File.WriteAllText(blocked, "fixture");
        var blockedRecorder = new MatchHistoryRecorder(new MatchHistoryStore(blocked), Record(), 0, 0, errors.Add);
        blockedRecorder.Finish(MatchHistoryStatus.Interrupted, "Quit", blockedRecorder.Record.result, blockedRecorder.Record.scoringSettingsHash);
        Check(errors.Count == 1 && File.ReadAllText(blocked) == "fixture", "Unwritable storage reports once, preserves existing files and does not stop gameplay");
    }

    private static void Reports()
    {
        var a = Record(); a.status = MatchHistoryStatus.Completed;
        var b = Record(); b.status = MatchHistoryStatus.Abandoned; b.result.stageTitle = "RENAMED HIGGS";
        var c = Record(GameMode.TwoVTwo); c.status = MatchHistoryStatus.Interrupted;
        var d = Record(GameMode.TwoVTwo);
        var rows = MatchHistoryReport.Summarize(new[] { a, b, c, d });
        var solo = rows.Single(r => r.mode == GameMode.OneVOne); var teams = rows.Single(r => r.mode == GameMode.TwoVTwo);
        Check(rows.Count == 2 && solo.started == 2 && solo.completed == 1 && solo.abandoned == 1, "Stable level ID groups renamed stages and keeps outcomes distinct");
        Check(teams.started == 2 && teams.interrupted == 1 && teams.inProgress == 1, "Modes and in-progress/interrupted counts stay separate");
        Check(solo.darkWins == 1 && solo.ties == 0 && teams.bestCompletedScore == 0, "Partial results never become wins, ties or completed-score records");
        string originalKey = a.LeaderboardKey; a.result.mode = GameMode.TwoVTwo;
        Check(a.LeaderboardKey != originalKey, "Leaderboard identity separates modes");
        a.result.mode = GameMode.OneVOne; a.result.scoringRulesetVersion++;
        Check(a.LeaderboardKey != originalKey, "Leaderboard identity separates scoring revisions");
        a.result.stageTitle = "=Quoted, \"level\"\nnext";
        string matches = MatchHistoryReport.MatchesCsv(new[] { a });
        Check(matches.Contains("9007199254740993") && matches.Contains((long.MaxValue - 17).ToString()), "CSV retains full decimal score precision");
        Check(matches.Contains("\"'=Quoted, \"\"level\"\"\nnext\""), "CSV quotes commas/newlines/quotes and neutralizes formula-like text");
        string output = Path.Combine(root, "export"); MatchHistoryReport.Export(output, new[] { a, b, c, d });
        Check(Directory.GetFiles(output, "*.csv").Length == 4, "Export creates summary, match, player and reward tables");
        Check(File.ReadAllText(Path.Combine(output, "players.csv")).Contains("123.456") && File.ReadAllText(Path.Combine(output, "rewards.csv")).Contains("PLAYER_DEFEAT"), "Export includes existing per-player statistics and reward telemetry");
        var legacy = Record(); legacy.result.playerContributions = null; legacy.result.playerStatistics = null; legacy.result.scoreTelemetry = null;
        Check(MatchHistoryReport.PlayersCsv(new[] { legacy }).Split('\n').Length == 2 && MatchHistoryReport.RewardsCsv(new[] { legacy }).Split('\n').Length == 2, "Missing optional arrays export as empty tables");
    }

    private static void Configuration()
    {
        var configuration = Resources.Load<MatchHistoryConfiguration>("MatchHistoryConfiguration");
        Check(configuration && configuration.catalog && configuration.standardEconomy, "Runtime configuration and canonical references are present");
        Check(configuration.standardSettingsHash == MatchHistoryRuntime.SettingsHash(configuration.standardEconomy), "Standard scoring fingerprint matches authored settings");
        Check(configuration.catalog.Levels.All(level => configuration.GetLevelId(level) == AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(level))), "Every catalog level resolves to its stable asset GUID");
        var lab = AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Scripts/Player/Demonstrations/Action Test Level.asset");
        Check(string.IsNullOrEmpty(configuration.GetLevelId(lab)), "Action lab is excluded from the main gameplay catalog");
        Check(MatchHistoryRuntime.CurrentDirectory.EndsWith(Application.isBatchMode ? "Validation" : "Development"), "Editor/test data uses a separate directory from Production");
    }
}
#endif
