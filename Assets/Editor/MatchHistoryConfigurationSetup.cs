#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Massive.Scoring;
using Massive.Statistics;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class MatchHistoryConfigurationSetup : IPreprocessBuildWithReport
{
    public const string ConfigurationPath = "Assets/Resources/MatchHistoryConfiguration.asset";
    public int callbackOrder => 0;

    [MenuItem("MASSIVE/Statistics/Refresh Configuration")]
    public static void RefreshConfiguration()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/Scripts/CORE/LevelCatalog.asset");
        var economy = AssetDatabase.LoadAssetAtPath<ScoreEconomyProfile>("Assets/Scripts/Scoring/ScoreEconomyProfile.asset");
        if (!catalog || !economy) throw new InvalidOperationException("Canonical level catalog or scoring economy is missing.");
        var configuration = AssetDatabase.LoadAssetAtPath<MatchHistoryConfiguration>(ConfigurationPath);
        if (!configuration)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            configuration = ScriptableObject.CreateInstance<MatchHistoryConfiguration>();
            AssetDatabase.CreateAsset(configuration, ConfigurationPath);
        }
        Undo.RecordObject(configuration, "Refresh match history configuration");
        configuration.catalog = catalog;
        configuration.standardEconomy = economy;
        configuration.standardSettingsHash = MatchHistoryRuntime.SettingsHash(economy);
        configuration.levels = catalog.Levels.Select(level =>
        {
            if (!level) throw new InvalidOperationException("Null level in the canonical catalog.");
            string id = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(level));
            if (string.IsNullOrEmpty(id)) throw new InvalidOperationException("Level has no persistent asset GUID: " + level.name);
            return new MatchHistoryConfiguration.LevelIdentity { level = level, id = id };
        }).ToArray();
        if (configuration.levels.Select(l => l.id).Distinct().Count() != configuration.levels.Length)
            throw new InvalidOperationException("Duplicate levels in the canonical catalog.");
        EditorUtility.SetDirty(configuration);
        AssetDatabase.SaveAssetIfDirty(configuration);
        Debug.Log("[Match History] Configured " + configuration.levels.Length + " main levels; scoring " + economy.RulesetId + " v" + economy.RulesetVersion + ".");
    }

    public void OnPreprocessBuild(BuildReport report) => RefreshConfiguration();
}
#endif
