#if UNITY_EDITOR
using Massive.Scoring;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ScoreEconomyProfile))]
public sealed class ScoreEconomyProfileEditor : Editor
{
    private List<Massive.Enemies.EnemyDefinition> _enemies;
    private void OnEnable()
    {
        _enemies = EnemyScoringValidation.FindEnemies();
        EditorApplication.projectChanged += RefreshEnemies;
    }
    private void OnDisable() => EditorApplication.projectChanged -= RefreshEnemies;
    private void RefreshEnemies() => _enemies = EnemyScoringValidation.FindEnemies();

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.ObjectField("Script", MonoScript.FromScriptableObject((ScoreEconomyProfile)target), typeof(MonoScript), false);

        DrawPropertiesExcluding(serializedObject, "m_Script", "rewards");

        EditorGUILayout.Space(8f);
        SerializedProperty rewards = serializedObject.FindProperty("rewards");
        EditorGUILayout.PropertyField(rewards, includeChildren: true);

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(10f);
        DrawProjection((ScoreEconomyProfile)target);
        DrawEnemyRewards((ScoreEconomyProfile)target);
    }

    private void DrawEnemyRewards(ScoreEconomyProfile profile)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Enemy Reward Mappings", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Definitions store keys only. Expected counts belong to reward rows; enemies sharing a key share that planning count.", MessageType.Info);
        foreach (var enemy in _enemies)
        {
            EditorGUILayout.ObjectField(enemy.name + " / " + enemy.category, enemy, typeof(Massive.Enemies.EnemyDefinition), false);
            if (string.IsNullOrWhiteSpace(enemy.defeatRewardKey))
                EditorGUILayout.LabelField("Defeat scoring", "None");
            else if (profile.TryGetReward(enemy.defeatRewardKey, out ScoreRewardRule rule))
                EditorGUILayout.LabelField(enemy.defeatRewardKey,
                    EnergyScoreFormatter.FormatWithUnit(rule.BaseMilliElectronVolts) + " / expected " + rule.expectedOccurrencesPerRound);
        }
        foreach (string issue in EnemyScoringValidation.Validate(profile, _enemies))
            EditorGUILayout.HelpBox(issue, MessageType.Warning);
    }

    private static void DrawProjection(ScoreEconomyProfile profile)
    {
        if (profile == null) return;

        EditorGUILayout.LabelField("Balance Projection", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Expected counts are editor-only planning values. The totals below are not runtime caps and do not include mode/stage availability.",
            MessageType.Info);

        long expectedBase = 0L;
        long expectedAtMaxChain = 0L;
        int maxMultiplier = profile.ChainSettings.GetMultiplier(profile.ChainSettings.MaxIndex);
        int maxAmplifier = profile.TeamAmplifierSettings.GetMultiplier(
            profile.TeamAmplifierSettings.MaxIndex);

        IReadOnlyList<ScoreRewardRule> rewards = profile.Rewards;
        if (rewards != null)
        {
            for (int i = 0; i < rewards.Count; i++)
            {
                ScoreRewardRule rule = rewards[i];
                if (rule == null || !rule.enabled) continue;

                long count = Mathf.Max(0, rule.expectedOccurrencesPerRound);
                long projected = EnergyScoreMath.SaturatingMultiply(rule.BaseMilliElectronVolts, count);
                long projectedMax = rule.multiplierEligible
                    ? EnergyScoreMath.SaturatingMultiply(projected, maxMultiplier)
                    : projected;

                expectedBase = EnergyScoreMath.SaturatingAdd(expectedBase, projected);
                expectedAtMaxChain = EnergyScoreMath.SaturatingAdd(expectedAtMaxChain, projectedMax);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        string.IsNullOrWhiteSpace(rule.key) ? "<UNKEYED>" : rule.key,
                        GUILayout.MinWidth(150f));
                    EditorGUILayout.LabelField(
                        $"{EnergyScoreFormatter.FormatWithUnit(rule.BaseMilliElectronVolts)} × {count}",
                        GUILayout.MinWidth(150f));
                    EditorGUILayout.LabelField(
                        EnergyScoreFormatter.FormatWithUnit(projected),
                        GUILayout.MinWidth(110f));
                }
            }
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Expected unchained total", EnergyScoreFormatter.FormatWithUnit(expectedBase));
        EditorGUILayout.LabelField(
            $"Upper projection at x{maxMultiplier}",
            EnergyScoreFormatter.FormatWithUnit(expectedAtMaxChain));
        EditorGUILayout.LabelField(
            $"Upper projection with x{maxAmplifier} Amplifier",
            EnergyScoreFormatter.FormatWithUnit(
                EnergyScoreMath.SaturatingMultiply(expectedAtMaxChain, maxAmplifier)));
        EditorGUILayout.LabelField(
            "Expected active class",
            EnergyScoreMath.GetLabel(EnergyScoreFormatter.GetUnit(expectedBase)));
    }
}
#endif
