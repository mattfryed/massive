using System;
using System.IO;
using Massive.Scoring;
using UnityEngine;

namespace Massive.Statistics
{
    public static class MatchHistoryRuntime
    {
        private static MatchHistoryStore store;
        private static MatchHistoryConfiguration configuration;
        private static string runId;
        public static string RootDirectory => Path.Combine(Application.persistentDataPath, "MatchHistory");
        public static bool IsDevelopment => Application.isEditor || Debug.isDebugBuild || Application.isBatchMode;
        public static string CurrentDirectory => Path.Combine(RootDirectory, Application.isBatchMode ? "Validation" : IsDevelopment ? "Development" : "Production");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            store = null;
            configuration = null;
            runId = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (store != null) return;
            runId = Guid.NewGuid().ToString("N");
            store = new MatchHistoryStore(CurrentDirectory);
            configuration = Resources.Load<MatchHistoryConfiguration>("MatchHistoryConfiguration");
            try
            {
                store.RecoverInterrupted(runId);
                foreach (string issue in store.Issues) Debug.LogWarning("[Match History] " + issue);
            }
            catch (Exception e) { Debug.LogWarning("[Match History] Recovery failed; original files retained: " + e.Message); }
        }

        public static string SettingsHash(ScoreEconomyProfile profile) => profile ? MatchHistoryStore.Hash(JsonUtility.ToJson(profile)) : "missing";

        public static MatchHistoryRecorder Begin(LevelDefinition level, string actualScene, ScoreEconomyProfile profile, MatchResult snapshot)
        {
            Initialize();
            if (!configuration)
            {
                Debug.LogWarning("[Match History] Configuration is missing. Run MASSIVE > Statistics > Refresh Configuration.");
                return null;
            }
            string id = configuration.GetLevelId(level);
            // Catalog membership and actual scene identity exclude action labs, demos and stale selection.
            if (string.IsNullOrEmpty(id) || !level || actualScene != level.SceneName) return null;
            string hash = SettingsHash(profile);
            bool standard = hash == configuration.standardSettingsHash && profile && configuration.standardEconomy &&
                snapshot.regulationDurationSeconds == configuration.standardEconomy.RegulationDurationSeconds;
            var record = new MatchHistoryRecord
            {
                matchId = Guid.NewGuid().ToString("N"), applicationRunId = runId,
                startedUtc = DateTime.UtcNow.ToString("O"), levelId = id,
                gameVersion = Application.version, buildId = Application.buildGUID,
                development = IsDevelopment,
                environment = Application.isBatchMode ? "Validation" : Application.isEditor ? "Editor" : Debug.isDebugBuild ? "Development build" : "Player",
                status = MatchHistoryStatus.InProgress, scoringSettingsHash = hash, standardRules = standard,
                eligibilityReason = !standard ? "Nonstandard scoring settings or round length." : IsDevelopment ? "Development/test match." : string.Empty,
                result = snapshot
            };
            return new MatchHistoryRecorder(store, record, Time.realtimeSinceStartupAsDouble, Time.unscaledTimeAsDouble,
                message => Debug.LogWarning("[Match History] " + message));
        }
    }
}
