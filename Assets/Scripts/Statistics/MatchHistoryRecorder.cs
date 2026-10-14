using System;
using System.Threading.Tasks;
using Massive.Scoring;
using UnityEngine;

namespace Massive.Statistics
{
    /// <summary>Owned by one GameManager. All snapshots and clock sampling happen on the main thread.</summary>
    public sealed class MatchHistoryRecorder
    {
        public const double CheckpointIntervalSeconds = 15d;
        private readonly MatchHistoryStore store;
        private readonly Action<string> reportError;
        private Task pendingWrite;
        private double lastElapsedClock, lastActiveClock, nextCheckpoint;
        private bool reportedWriteFailure;
        public MatchHistoryRecord Record { get; }
        public bool IsFinished => Record.status != MatchHistoryStatus.InProgress;
        public bool CheckpointDue => !IsFinished && Record.elapsedSeconds >= nextCheckpoint && (pendingWrite == null || pendingWrite.IsCompleted);

        public MatchHistoryRecorder(MatchHistoryStore store, MatchHistoryRecord record, double elapsedClock, double activeClock, Action<string> reportError)
        {
            this.store = store;
            this.reportError = reportError;
            Record = record;
            lastElapsedClock = elapsedClock;
            lastActiveClock = activeClock;
            nextCheckpoint = CheckpointIntervalSeconds;
            Save(synchronous: true);
        }

        public void Sample(double elapsedClock, double activeClock, MatchRuntimePhase phase, bool scoringOpen, bool worldRunning, bool suspended)
        {
            if (IsFinished) return;
            Record.elapsedSeconds += Math.Max(0d, elapsedClock - lastElapsedClock);
            double active = Math.Max(0d, activeClock - lastActiveClock);
            lastElapsedClock = elapsedClock;
            lastActiveClock = activeClock;
            if (!scoringOpen || suspended) return;
            if (phase == MatchRuntimePhase.Regulation && worldRunning) Record.activeRegulationSeconds += active;
            // NOVA deliberately freezes world time while bonus scoring and controls continue.
            else if (phase == MatchRuntimePhase.Bonus) Record.activeBonusSeconds += active;
        }

        public void Checkpoint(MatchResult snapshot, string settingsHash, bool synchronous = false)
        {
            if (IsFinished) return;
            Capture(snapshot, settingsHash);
            nextCheckpoint = Record.elapsedSeconds + CheckpointIntervalSeconds;
            Save(synchronous);
        }

        public void Finish(MatchHistoryStatus status, string reason, MatchResult snapshot, string settingsHash)
        {
            if (IsFinished) return;
            if (status == MatchHistoryStatus.InProgress) throw new ArgumentException("Finish requires a terminal status.");
            Capture(snapshot, settingsHash);
            Record.status = status;
            Record.endReason = reason;
            Record.endedUtc = DateTime.UtcNow.ToString("O");
            Save(synchronous: true);
        }

        private void Capture(MatchResult snapshot, string settingsHash)
        {
            var start = Record.result;
            if (Record.scoringSettingsHash != settingsHash || start.regulationDurationSeconds != snapshot.regulationDurationSeconds ||
                start.mode != snapshot.mode || start.gameplaySceneName != snapshot.gameplaySceneName)
            {
                Record.standardRules = false;
                Record.eligibilityReason = "Scoring settings, mode, level or round length changed during the match.";
            }
            // Identity is captured at start, independent of subsequent front-end navigation.
            snapshot.mode = start.mode;
            snapshot.stageNumber = start.stageNumber;
            snapshot.stageTitle = start.stageTitle;
            snapshot.gameplaySceneName = start.gameplaySceneName;
            snapshot.scoringRulesetId = start.scoringRulesetId;
            snapshot.scoringRulesetVersion = start.scoringRulesetVersion;
            snapshot.regulationDurationSeconds = start.regulationDurationSeconds;
            Record.result = snapshot;
        }

        private void Save(bool synchronous)
        {
            Record.lastSavedUtc = DateTime.UtcNow.ToString("O");
            Record.revision++;
            try
            {
                if (pendingWrite != null)
                {
                    try { pendingWrite.GetAwaiter().GetResult(); }
                    catch (Exception e) { Report(e); }
                    pendingWrite = null;
                }
                string encoded = MatchHistoryStore.Encode(Record);
                if (synchronous) store.SaveEncoded(Record.matchId, encoded);
                else pendingWrite = Task.Run(() => store.SaveEncoded(Record.matchId, encoded));
            }
            catch (Exception e) { Report(e); }
        }

        private void Report(Exception error)
        {
            if (reportedWriteFailure) return;
            reportedWriteFailure = true;
            reportError?.Invoke("Match history could not be saved to " + store.DirectoryPath + ": " + error.Message);
        }
    }
}
