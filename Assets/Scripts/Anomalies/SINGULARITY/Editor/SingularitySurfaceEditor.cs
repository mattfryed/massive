using Massive.Singularity;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SingularitySurface))]
public sealed class SingularitySurfaceEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("Front → top bend → rear → bottom bend. Positive vertical input always advances around this loop. This prototype does not change the other levels.", MessageType.Info);
        DrawDefaultInspector();
        var surface = (SingularitySurface)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Loop length", surface.LoopLength.ToString("0.00") + " units (centerline)");
        EditorGUILayout.LabelField("Full XZ footprint", surface.Width.ToString("0.###") + " × " +
            surface.ProjectedHeight.ToString("0.###") + " units (including turns)");
        var player = surface.GetComponentInChildren<SingularityPlayerMotor>();
        if (!player) return;
        if (Application.isPlaying)
        {
            EditorGUILayout.LabelField("Player region", surface.Region(player.SurfacePosition.y));
            if (GUILayout.Button("Reset player to start")) player.ResetToStart();
            return;
        }
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Edit Mode Player Preview", EditorStyles.boldLabel);
        var so = new SerializedObject(player); so.Update();
        var preview = so.FindProperty("usePreviewPosition");
        var across = so.FindProperty("previewAcross");
        var fraction = so.FindProperty("previewLoopFraction");
        EditorGUILayout.PropertyField(preview, new GUIContent("Enable scrub preview"));
        using (new EditorGUI.DisabledScope(!preview.boolValue))
        {
            EditorGUILayout.Slider(across, -surface.Width * .5f + player.Radius, surface.Width * .5f - player.Radius, "Across");
            EditorGUILayout.Slider(fraction, 0f, 1f, "Around loop");
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Front")) SetPreview(preview, fraction, surface.TopStart * .5f / surface.LoopLength);
            if (GUILayout.Button("Top bend")) SetPreview(preview, fraction, (surface.TopStart + surface.RearStart) * .5f / surface.LoopLength);
            if (GUILayout.Button("Rear")) SetPreview(preview, fraction, (surface.RearStart + surface.BottomStart) * .5f / surface.LoopLength);
            if (GUILayout.Button("Bottom bend")) SetPreview(preview, fraction, (surface.BottomStart + surface.LoopLength) * .5f / surface.LoopLength);
        }
        if (so.ApplyModifiedProperties())
        {
            player.RefreshVisual();
            EditorApplication.QueuePlayerLoopUpdate(); SceneView.RepaintAll();
        }
        EditorGUILayout.HelpBox("Preview does not change the Play Mode start. Grid colors, dashes and line weight are on Loop Grid; movement and attraction are on Player - Surface Probe.", MessageType.None);
    }

    private static void SetPreview(SerializedProperty enabled, SerializedProperty fraction, float value)
    { enabled.boolValue = true; fraction.floatValue = value; }
}
