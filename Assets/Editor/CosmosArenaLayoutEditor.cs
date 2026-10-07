#if UNITY_EDITOR
using System.Linq;
using Massive.Cosmos;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(CosmosArenaLayout))]
public sealed class CosmosArenaLayoutEditor : Editor
{
    static readonly GUIContent WidthLabel = new GUIContent("Overall Oval Width",
        "Full width in world units, including both goal caps. Keeps the goal separators at the same proportion of the ellipse.");
    static readonly GUIContent HeightLabel = new GUIContent("Overall Oval Height",
        "Maximum height in world units, at the centre. Goal height, score growth and capture effects scale together.");

    void OnEnable() { Undo.undoRedoPerformed += RefreshAfterUndo; }
    void OnDisable() { Undo.undoRedoPerformed -= RefreshAfterUndo; }

    public override void OnInspectorGUI()
    {
        var layout = (CosmosArenaLayout)target;
        var bounds = layout.GetComponent<ArenaBoundsFromVectorGrid>();
        var grid = bounds.Grid;
        if (grid && bounds.ovalOutline)
        {
            EditorGUILayout.LabelField("Oval Dimensions", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorUtility.IsPersistent(layout)))
            {
                Vector3 scale = grid.transform.lossyScale;
                float width = 2 * bounds.OvalHalfWidthLocal * Mathf.Abs(scale.x);
                float height = grid.size.y * Mathf.Abs(scale.y);
                EditorGUI.BeginChangeCheck();
                width = EditorGUILayout.Slider(WidthLabel, width, 16, 48);
                height = EditorGUILayout.Slider(HeightLabel, height, 6, 24);
                if (EditorGUI.EndChangeCheck()) Resize(layout, width, height);
            }
            EditorGUILayout.HelpBox("Includes both goals. Adjust in Edit Mode; changes preview immediately and support Undo.", MessageType.None);
            EditorGUILayout.Space();
        }
        if (DrawDefaultInspector())
        {
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
    }

    internal static void Resize(CosmosArenaLayout layout, float width, float height)
    {
        var bounds = layout.GetComponent<ArenaBoundsFromVectorGrid>();
        var grid = bounds.Grid;
        var scores = layout.gameObject.scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<ScoreSphereScript>(true))
            .Where(score => score.transform.parent.GetComponentsInChildren<CosmosGoalShape>(true)
                .Any(shape => shape.arena == bounds)).ToArray();
        var changed = scores.SelectMany(score => score.transform.parent.GetComponentsInChildren<Component>(true))
            .Where(component => component).Cast<Object>().Concat(new Object[] { grid, bounds }).Distinct().ToArray();
        Undo.SetCurrentGroupName("Resize COSMOS oval");
        // Several legacy goal children are RectTransforms. Capture their complete
        // serialized state so both anchors and world-position changes undo together.
        Undo.RegisterCompleteObjectUndo(changed, "Resize COSMOS oval");

        // Grid size and the existing ellipse profile remain the single source of
        // truth: no duplicate dimension fields or scene migration are needed.
        Vector3 scale = grid.transform.lossyScale;
        float seamRatio = grid.size.x / (2 * bounds.OvalHalfWidthLocal);
        grid.size = new Vector2(width * seamRatio / Mathf.Abs(scale.x), height / Mathf.Abs(scale.y));
        CosmosLayoutSetup.FitGoals(layout, scores);
        Refresh(layout);
        foreach (var value in changed)
        {
            EditorUtility.SetDirty(value);
            if (PrefabUtility.IsPartOfPrefabInstance(value)) PrefabUtility.RecordPrefabInstancePropertyModifications(value);
        }
        EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
    }

    void RefreshAfterUndo()
    {
        if (target && !EditorApplication.isPlayingOrWillChangePlaymode) Refresh((CosmosArenaLayout)target);
        Repaint();
    }

    static void Refresh(CosmosArenaLayout layout)
    {
        var bounds = layout.GetComponent<ArenaBoundsFromVectorGrid>();
        bounds.RefreshNow();
        if (bounds.Grid) bounds.Grid.RefreshLayout();
        layout.Rebuild();
        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
    }

    [MenuItem("MASSIVE/COSMOS/Select layout controls %#&F8")]
    static void SelectLayout()
    {
        var layout = CosmosLayoutSetup.All<CosmosArenaLayout>().FirstOrDefault();
        if (!layout) return;
        Selection.activeGameObject = layout.gameObject;
        EditorGUIUtility.PingObject(layout);
        UnityEditorInternal.InternalEditorUtility.SetIsInspectorExpanded(layout, true);
    }
}
#endif
