#if UNITY_EDITOR
using System;
using Massive.Enemies;
using Massive.Multiplier;
using Massive.Resonance;
using Massive.Scoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HiggsStageSetup
{
    public const string ScenePath = "Assets/Scenes/S-9_HIGGS.unity";
    public const string KnotPath = "Assets/Scripts/Anomalies/HIGGS/PF_SymmetryKnot.prefab";

    [MenuItem("MASSIVE/HIGGS/Apply Accepted Stage Setup")]
    public static void Apply()
    {
        if (Application.isPlaying || SceneManager.GetActiveScene().path != ScenePath)
            throw new InvalidOperationException("Open S-9_HIGGS outside Play Mode.");
        var scene = SceneManager.GetActiveScene();
        var manager = UnityEngine.Object.FindFirstObjectByType<HiggsExcitationKnotManager>();
        var field = UnityEngine.Object.FindFirstObjectByType<HiggsFieldGPU>();
        var bounds = UnityEngine.Object.FindFirstObjectByType<ArenaBoundsFromVectorGrid>();
        if (manager == null || field == null || bounds == null) throw new InvalidOperationException("Missing HIGGS stage owners.");
        var knotAsset = AssetDatabase.LoadAssetAtPath<GameObject>(KnotPath);
        var prefab = PrefabUtility.LoadPrefabContents(KnotPath);
        try
        {
            var data = new SerializedObject(prefab.GetComponent<SymmetryKnotController>());
            data.FindProperty("unclaimedSeconds").floatValue = 6;
            data.FindProperty("claimedSeconds").floatValue = 10;
            data.FindProperty("captureRampSeconds").floatValue = 2;
            data.FindProperty("captureRadius").floatValue = 1.15f;
            data.FindProperty("rewardKey").stringValue = ScoreRewardKeys.HiggsControlTick;
            data.FindProperty("tickIntervalSeconds").floatValue = .25f;
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(prefab, KnotPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }

        var encounter = UnityEngine.Object.FindFirstObjectByType<AmplifierResonanceSpawner>();
        if (encounter == null) encounter = new GameObject("HIGGS Amplifier Encounter").AddComponent<AmplifierResonanceSpawner>();
        encounter.UseSharedSettings = true;
        encounter.corePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resonance/Spawn Patterns/Amplifier Core - Spawn Cycle.prefab").GetComponent<AmplifierCoreGameplay>();
        var pattern = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resonance/Spawn Patterns/345 Hz Particle Encounter.prefab").GetComponent<ResonancePatternController>();
        encounter.patternOrder = new System.Collections.Generic.List<ResonanceSpawnEntry> { new ResonanceSpawnEntry { label = "345 Hz", patternPrefab = pattern } };
        encounter.spawnRegion = encounter.GetComponent<AmplifierSpawnRegion>();
        ConfigureRegion(encounter.spawnRegion, bounds, field, pattern, .3f);

        var region = manager.GetComponent<AmplifierSpawnRegion>();
        if (region == null) region = manager.gameObject.AddComponent<AmplifierSpawnRegion>();
        ConfigureRegion(region, bounds, field, pattern, .65f);
        region.clearanceWorld = .2f;
        region.minDistanceFromPlayers = 1.5f;

        var config = new SerializedObject(manager);
        config.FindProperty("spawnRegion").objectReferenceValue = region;
        config.FindProperty("amplifierEncounter").objectReferenceValue = encounter;
        config.FindProperty("initialKnotSpawnDelaySeconds").floatValue = 8;
        config.FindProperty("respawnCooldownSeconds").floatValue = 10;
        config.FindProperty("excitationTailSeconds").floatValue = .75f;
        config.ApplyModifiedPropertiesWithoutUndo();

        var fieldConfig = new SerializedObject(field);
        fieldConfig.FindProperty("worldSizeXZ").vector2Value = bounds.Grid.size;
        fieldConfig.FindProperty("gameplayExcitationsPerSecondOverride").floatValue = 0;
        fieldConfig.FindProperty("rampSeconds").floatValue = 120;
        fieldConfig.ApplyModifiedPropertiesWithoutUndo();
        var toast = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy System/Enemy Types/Melee/Drone/Drone Score Toast.prefab").GetComponent<EnemyScoreToast>();
        foreach (var mouth in UnityEngine.Object.FindObjectsByType<SymmetryKnotGoalMouth>(FindObjectsSortMode.None))
        {
            var data = new SerializedObject(mouth);
            data.FindProperty("receiptToastPrefab").objectReferenceValue = toast;
            data.FindProperty("receiptIntervalSeconds").floatValue = 1;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Scripts/Level Select/LevelDefinition-HIGGS.asset");
        level.anomalyTypeName = "SYMMETRY KNOTS"; EditorUtility.SetDirty(level);
        var economy = AssetDatabase.LoadAssetAtPath<ScoreEconomyProfile>("Assets/Scripts/Scoring/ScoreEconomyProfile.asset");
        var economyData = new SerializedObject(economy);
        economyData.FindProperty("regulationDurationSeconds").floatValue = 120;
        economyData.FindProperty("rulesetVersion").intValue = Mathf.Max(2, economy.RulesetVersion);
        economyData.ApplyModifiedPropertiesWithoutUndo();
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scripts/Anomalies/HIGGS/MASSIVE_HIGGS_UnderlayTopo_URP.mat");
        mat.SetFloat("_ExcitationGlowStrength", 1.25f); mat.SetFloat("_ExcitationGlowPower", 2);
        mat.SetFloat("_Opacity", .65f); EditorUtility.SetDirty(mat);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    private static void ConfigureRegion(AmplifierSpawnRegion region, ArenaBoundsFromVectorGrid bounds,
        HiggsFieldGPU field, ResonancePatternController pattern, float fraction)
    {
        region.arenaBounds = bounds;
        region.neutralWidthFraction = fraction; region.spawnHeightWorld = 0;
        region.previewPattern = pattern;
        region.ignoredColliders = field.GetComponentsInChildren<Collider>(true);
        region.drawZones = false;
        EditorUtility.SetDirty(region);
    }
}
#endif
