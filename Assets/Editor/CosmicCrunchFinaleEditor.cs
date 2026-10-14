#if UNITY_EDITOR
using Massive.Cosmos;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CosmicCrunchFinale))]
public sealed class CosmicCrunchFinaleEditor : Editor
{
    bool playing;
    double lastTick;
    void OnEnable() { EditorApplication.update += Tick; }
    void OnDisable()
    {
        EditorApplication.update -= Tick;
        if (target && !Application.isPlaying) ((CosmicCrunchFinale)target).StopPreview();
        Refresh();
    }
    public override void OnInspectorGUI()
    {
        if (DrawDefaultInspector()) Refresh();
        var finale = (CosmicCrunchFinale)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Big Crunch Preview", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || !finale.isActiveAndEnabled))
        {
            EditorGUI.BeginChangeCheck();
            float p = EditorGUILayout.Slider("Crunch Progress", finale.PreviewActive ? finale.Progress : 0, 0, 1);
            if (EditorGUI.EndChangeCheck()) { playing = false; finale.SetPreview(p); Refresh(); }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(playing ? "Pause" : "Play"))
                {
                    playing = !playing;
                    if (playing) { finale.SetPreview(finale.Progress >= 1 ? 0 : finale.Progress); lastTick = EditorApplication.timeSinceStartup; }
                }
                if (GUILayout.Button("Stop / Restore Web")) { playing = false; finale.StopPreview(); Refresh(); }
            }
        }
        EditorGUILayout.HelpBox("Preview holds the web at Age 1. Gameplay/HUD stay visible in Edit Mode. Stop restores the authored Age and palette. Preview progress is not saved.", MessageType.None);
    }
    void Tick()
    {
        if (!target || EditorApplication.isPlayingOrWillChangePlaymode) { playing = false; return; }
        if (!playing) return;
        var finale = (CosmicCrunchFinale)target;
        double now = EditorApplication.timeSinceStartup;
        finale.SetPreview(finale.Progress + (float)(now - lastTick) / Mathf.Max(.25f, finale.duration));
        lastTick = now;
        if (finale.Progress >= 1) playing = false;
        Refresh(); Repaint();
    }
    static void Refresh() { EditorApplication.QueuePlayerLoopUpdate(); SceneView.RepaintAll(); }
}
#endif
