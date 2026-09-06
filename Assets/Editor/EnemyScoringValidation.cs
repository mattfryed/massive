#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Massive.Enemies;
using Massive.Scoring;
using UnityEditor;
using UnityEngine;

public static class EnemyScoringValidation
{
    public static List<string> Validate(ScoreEconomyProfile profile, IEnumerable<EnemyDefinition> enemies)
    {
        var issues = new List<string>();
        if (profile == null) { issues.Add("No economy profile selected."); return issues; }
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (ScoreRewardRule rule in profile.Rewards)
        {
            if (rule == null || string.IsNullOrWhiteSpace(rule.key))
            { issues.Add("Reward table contains an empty key."); continue; }
            if (!keys.Add(rule.key.Trim())) issues.Add("Duplicate reward key: " + rule.key);
            if (rule.enabled && rule.BaseMilliElectronVolts <= 0)
                issues.Add("Enabled reward has no positive value: " + rule.key);
        }
        foreach (EnemyDefinition enemy in enemies)
        {
            if (enemy == null || string.IsNullOrWhiteSpace(enemy.defeatRewardKey)) continue;
            if (!profile.TryGetReward(enemy.defeatRewardKey, out ScoreRewardRule rule))
                issues.Add(enemy.name + ": missing reward " + enemy.defeatRewardKey);
            else if (!rule.enabled)
                issues.Add(enemy.name + ": defeat reward is disabled.");
            else if (rule.repeatPolicy != ScoreRepeatPolicy.OncePerSourceToken)
                issues.Add(enemy.name + ": defeat reward must use OncePerSourceToken.");
        }
        return issues;
    }

    public static List<EnemyDefinition> FindEnemies()
    {
        var enemies = new List<EnemyDefinition>();
        foreach (string guid in AssetDatabase.FindAssets("t:EnemyDefinition"))
        {
            var enemy = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (enemy != null) enemies.Add(enemy);
        }
        enemies.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
        return enemies;
    }

    [MenuItem("MASSIVE/Scoring/Validate Enemy Rewards")]
    public static void ValidateSelected()
    {
        var profile = Selection.activeObject as ScoreEconomyProfile;
        if (profile == null) profile = ScoreRewardKeyDrawer.PreviewProfile;
        List<string> issues = Validate(profile, FindEnemies());
        if (issues.Count == 0) Debug.Log("[Enemy Scoring] All enemy reward mappings are valid.", profile);
        else foreach (string issue in issues) Debug.LogWarning("[Enemy Scoring] " + issue, profile);
    }
}
#endif
