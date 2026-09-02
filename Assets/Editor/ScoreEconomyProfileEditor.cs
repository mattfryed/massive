#if UNITY_EDITOR
using Massive.Scoring;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ScoreEconomyProfile))]
public sealed class ScoreEconomyProfileEditor : Editor
{
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
            "Expected active class",
            EnergyScoreMath.GetLabel(EnergyScoreFormatter.GetUnit(expectedBase)));
    }
}
#endif
