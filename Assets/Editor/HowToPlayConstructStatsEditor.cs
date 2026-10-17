using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HowToPlayConstructStats)), CanEditMultipleObjects]
public sealed class HowToPlayConstructStatsEditor : Editor
{
    private static readonly string[] Stats = { "health", "attack", "speed", "range", "score" };

    private void OnEnable()
    {
        var rows = serializedObject.FindProperty("constructs");
        for (int i = 0; i < rows.arraySize; i++) rows.GetArrayElementAtIndex(i).isExpanded = true;
        Undo.undoRedoPerformed += RefreshAfterUndo;
    }

    private void OnDisable() => Undo.undoRedoPerformed -= RefreshAfterUndo;

    private void RefreshAfterUndo()
    {
        foreach (var item in targets) if (item) ((HowToPlayConstructStats)item).ApplyValues(true);
        SceneView.RepaintAll(); Repaint();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        EditorGUILayout.HelpBox("Stat fill lengths: 0% is empty, 100% is full. These are presentation values only. Nonzero bars retain their subtle idle animation.", MessageType.Info);
        var rows = serializedObject.FindProperty("constructs");
        bool valuesChanged = false;
        for (int i = 0; i < rows.arraySize; i++)
        {
            var row = rows.GetArrayElementAtIndex(i);
            row.isExpanded = EditorGUILayout.Foldout(row.isExpanded, row.FindPropertyRelative("label").stringValue, true);
            if (!row.isExpanded) continue;
            EditorGUI.indentLevel++;
            EditorGUI.BeginChangeCheck();
            foreach (var stat in Stats)
                EditorGUILayout.Slider(row.FindPropertyRelative(stat), 0f, 100f,
                    new GUIContent(ObjectNames.NicifyVariableName(stat) + " (%)"));
            valuesChanged |= EditorGUI.EndChangeCheck();
            EditorGUI.indentLevel--; EditorGUILayout.Space(4f);
        }
        if (!valuesChanged) return;

        var bars = new List<HowToPlayStatBarIdle>();
        var changedObjects = new List<Object>();
        var previousWidths = new List<float>();
        var previousActive = new List<bool>();
        foreach (var item in targets)
            foreach (var bar in ((HowToPlayConstructStats)item).BoundBars())
            {
                bars.Add(bar); changedObjects.Add(bar.Fill); changedObjects.Add(bar.gameObject);
                previousWidths.Add(bar.Fill.Width); previousActive.Add(bar.gameObject.activeSelf);
            }
        Undo.RecordObjects(changedObjects.ToArray(), "Change construct stat bars");
        serializedObject.ApplyModifiedProperties();
        // Apply explicitly to support both Prefab Mode and direct Project asset inspection.
        foreach (var item in targets) ((HowToPlayConstructStats)item).ApplyValues(true);
        for (int i = 0; i < bars.Count; i++)
        {
            var bar = bars[i];
            if (!Mathf.Approximately(previousWidths[i], bar.Fill.Width))
            {
                EditorUtility.SetDirty(bar.Fill);
                PrefabUtility.RecordPrefabInstancePropertyModifications(bar.Fill);
            }
            if (previousActive[i] != bar.gameObject.activeSelf)
            {
                EditorUtility.SetDirty(bar.gameObject);
                PrefabUtility.RecordPrefabInstancePropertyModifications(bar.gameObject);
            }
        }
        SceneView.RepaintAll();
    }
}
