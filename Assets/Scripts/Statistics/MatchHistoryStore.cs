using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Massive.Statistics
{
    /// <summary>One checksummed file per match. Reads are non-mutating; recovery is an explicit startup step.</summary>
    public sealed class MatchHistoryStore
    {
        [Serializable]
        private sealed class Envelope
        {
            public int formatVersion;
            public string checksum;
            public string payload;
        }

        private readonly object gate = new object();
        public string DirectoryPath { get; }
        public List<string> Issues { get; } = new List<string>();

        public MatchHistoryStore(string directoryPath) { DirectoryPath = Path.GetFullPath(directoryPath); }

        public static string Hash(string value)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
        }

        // Serialize snapshots on Unity's main thread before handing the immutable string to the writer.
        public static string Encode(MatchHistoryRecord record)
        {
            Validate(record);
            string payload = JsonUtility.ToJson(record);
            return JsonUtility.ToJson(new Envelope { formatVersion = 1, checksum = Hash(payload), payload = payload }, true);
        }

        public void Save(MatchHistoryRecord record) => SaveEncoded(record.matchId, Encode(record));

        public void SaveEncoded(string matchId, string contents)
        {
            ValidateId(matchId);
            lock (gate)
            {
                Directory.CreateDirectory(DirectoryPath);
                string path = Path.Combine(DirectoryPath, matchId + ".json");
                string temporary = path + ".tmp";
                // Flush the new contents before replacing the last committed record. Never truncate the live file.
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(contents);
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
                else File.Move(temporary, path);
            }
        }

        public List<MatchHistoryRecord> ReadAll()
        {
            lock (gate)
            {
                Issues.Clear();
                var records = new List<MatchHistoryRecord>();
                if (!Directory.Exists(DirectoryPath)) return records;
                var paths = Directory.GetFiles(DirectoryPath, "*.json*")
                    .Where(p => p.EndsWith(".json", StringComparison.Ordinal) || p.EndsWith(".json.tmp", StringComparison.Ordinal) || p.EndsWith(".json.bak", StringComparison.Ordinal))
                    .Select(p => p.EndsWith(".json", StringComparison.Ordinal) ? p : p.Substring(0, p.Length - 4))
                    .Distinct().OrderBy(p => p, StringComparer.Ordinal);
                foreach (string path in paths)
                {
                    MatchHistoryRecord newest = null;
                    string source = null;
                    bool unsupported = false;
                    foreach (string candidate in new[] { path, path + ".tmp", path + ".bak" })
                    {
                        if (!File.Exists(candidate)) continue;
                        try
                        {
                            var envelope = JsonUtility.FromJson<Envelope>(File.ReadAllText(candidate));
                            if (envelope != null && envelope.formatVersion > 1)
                                throw new NotSupportedException("Unknown file format; preserved without modification.");
                            if (envelope == null || envelope.formatVersion != 1 || string.IsNullOrEmpty(envelope.payload) || envelope.checksum != Hash(envelope.payload))
                                throw new InvalidDataException("Missing payload or checksum mismatch.");
                            var record = JsonUtility.FromJson<MatchHistoryRecord>(envelope.payload);
                            if (record != null && record.schemaVersion > MatchHistoryRecord.CurrentSchemaVersion)
                                throw new NotSupportedException("Newer save schema; preserved without modification.");
                            Validate(record);
                            if (Path.GetFileNameWithoutExtension(path) != record.matchId)
                                throw new InvalidDataException("Match ID does not match its filename.");
                            if (newest == null || record.revision > newest.revision) { newest = record; source = candidate; }
                        }
                        catch (NotSupportedException e) { unsupported = true; Issues.Add(Path.GetFileName(candidate) + ": " + e.Message); }
                        catch (Exception e) when (e is IOException || e is InvalidDataException || e is ArgumentException || e is UnauthorizedAccessException || e is InvalidOperationException)
                        { Issues.Add(Path.GetFileName(candidate) + ": " + e.Message); }
                    }
                    // Never roll a future-version record back to an older backup.
                    if (unsupported || newest == null) continue;
                    if (source != path) Issues.Add(Path.GetFileName(path) + ": using recovered " + Path.GetExtension(source) + " revision.");
                    records.Add(newest);
                }
                return records.OrderByDescending(r => r.startedUtc, StringComparer.Ordinal).ToList();
            }
        }

        public int RecoverInterrupted(string currentRunId)
        {
            lock (gate)
            {
                var records = ReadAll();
                int count = 0;
                foreach (var record in records)
                {
                    if (record.status != MatchHistoryStatus.InProgress || record.applicationRunId == currentRunId) continue;
                    // Preserve every recovery candidate before a replacement rotates the backup.
                    string path = Path.Combine(DirectoryPath, record.matchId + ".json");
                    foreach (string candidate in new[] { path, path + ".tmp", path + ".bak" })
                        if (File.Exists(candidate)) File.Copy(candidate, candidate + ".recovered-" + Guid.NewGuid().ToString("N"));
                    record.status = MatchHistoryStatus.Interrupted;
                    record.endReason = "Application stopped before a final result; time and stats end at the last checkpoint.";
                    record.endedUtc = record.lastSavedUtc;
                    record.revision++;
                    Save(record);
                    count++;
                }
                return count;
            }
        }

        private static void ValidateId(string id)
        {
            if (!Guid.TryParseExact(id, "N", out _)) throw new InvalidDataException("Invalid match ID.");
        }

        private static void Validate(MatchHistoryRecord record)
        {
            if (record == null || record.schemaVersion != MatchHistoryRecord.CurrentSchemaVersion)
                throw new InvalidDataException("Missing or unsupported record schema.");
            ValidateId(record.matchId);
            if (string.IsNullOrEmpty(record.levelId) || string.IsNullOrEmpty(record.startedUtc) || string.IsNullOrEmpty(record.lastSavedUtc) ||
                !Enum.IsDefined(typeof(MatchHistoryStatus), record.status) || !Enum.IsDefined(typeof(GameMode), record.result.mode) ||
                !FiniteNonnegative(record.elapsedSeconds) || !FiniteNonnegative(record.activeRegulationSeconds) || !FiniteNonnegative(record.activeBonusSeconds))
                throw new InvalidDataException("Incomplete or invalid match record.");
        }

        private static bool FiniteNonnegative(double value) => value >= 0d && !double.IsInfinity(value) && !double.IsNaN(value);
    }
}
