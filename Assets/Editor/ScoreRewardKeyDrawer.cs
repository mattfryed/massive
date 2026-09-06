#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Massive.Scoring;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ScoreRewardKeyAttribute))]
public sealed class ScoreRewardKeyDrawer : PropertyDrawer
{
    private static ScoreEconomyProfile _previewProfile;
    public static ScoreEconomyProfile PreviewProfile
    {
        get
        {
            if (_previewProfile == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:ScoreEconomyProfile");
                Array.Sort(guids, StringComparer.Ordinal);
                if (guids.Length > 0)
                    _previewProfile = AssetDatabase.LoadAssetAtPath<ScoreEconomyProfile>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            return _previewProfile;
        }
        set => _previewProfile = value;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => EditorGUIUtility.singleLineHeight * 3f + EditorGUIUtility.standardVerticalSpacing * 2f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        float step = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        position.height = EditorGUIUtility.singleLineHeight;
        PreviewProfile = (ScoreEconomyProfile)EditorGUI.ObjectField(position,
            new GUIContent("Preview Economy", "Authoring preview only. The match selects its actual profile."),
            PreviewProfile, typeof(ScoreEconomyProfile), false);
        position.y += step;

        var keys = new List<string> { "" };
        var labels = new List<string> { "None (no reward)" };
        if (PreviewProfile != null)
            foreach (ScoreRewardRule rule in PreviewProfile.Rewards)
                if (rule != null && !string.IsNullOrWhiteSpace(rule.key) && !keys.Contains(rule.key.Trim()))
                {
                    keys.Add(rule.key.Trim());
                    labels.Add(rule.key.Trim());
                }
        string current = property.stringValue ?? "";
        int index = keys.FindIndex(key => string.Equals(key, current.Trim(), StringComparison.OrdinalIgnoreCase));
        bool missing = index < 0;
        if (missing)
        {
            index = keys.Count;
            keys.Add(current);
            labels.Add(current + " (missing)");
        }
        EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
        EditorGUI.BeginChangeCheck();
        int selected = EditorGUI.Popup(position, label.text, index, labels.ToArray());
        if (EditorGUI.EndChangeCheck()) property.stringValue = keys[selected];
        EditorGUI.showMixedValue = false;
        position.y += step;
        string message = "Values are read from the active match economy.";
        bool warning = missing || PreviewProfile == null;
        if (PreviewProfile == null) message = "Create a ScoreEconomyProfile to select rewards.";
        else if (missing) message = "This key is missing from the preview economy.";
        else if (PreviewProfile.TryGetReward(current, out ScoreRewardRule reward))
        {
            message = EnergyScoreFormatter.FormatWithUnit(reward.BaseMilliElectronVolts) + " / " + reward.chainEffect;
            if (!reward.enabled) { message = "Reward is disabled in this economy."; warning = true; }
        }
        EditorGUI.HelpBox(position, message, warning ? MessageType.Warning : MessageType.None);
        EditorGUI.EndProperty();
    }
}
#endif
