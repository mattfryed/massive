using UnityEditor;
using UnityEngine;
using Massive.AttractStudy;

[CustomEditor(typeof(AttractCachedGrid)), CanEditMultipleObjects]
public sealed class AttractCachedGridEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("Grid layout and attraction edits rebuild their cache automatically. Width and colors update immediately. Rotation and tilt do not rebake the grid.", MessageType.Info);
        if (GUILayout.Button("Rebake Grid"))
        {
            foreach (var item in targets) ((AttractCachedGrid)item).Rebake();
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
        if (targets.Length == 1 && ((AttractCachedGrid)target).SamplingLimited)
            EditorGUILayout.HelpBox("Curve sampling was reduced to keep this layout within the vertex budget. Increase Grid Spacing or reduce Grid Size for denser curves.", MessageType.Warning);
    }
}
