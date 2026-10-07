#if UNITY_EDITOR
using Massive.Demonstrations;
using Massive.Enemies;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EnemyDirector))]
public sealed class EnemyDirectorInspector : Editor
{
    public override void OnInspectorGUI()
    {
        var director = (EnemyDirector)target;
        if (GUILayout.Button("Open Encounter Composer")) EnemyEncounterComposer.ShowForDirector(director);
        DrawDefaultInspector();
    }
}

[CustomEditor(typeof(EnemyLab))]
public sealed class EnemyLabInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector(); var lab = (EnemyLab)target;
        if (!lab.timelinePreview) return;
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Columns: the existing six-enemy showcase. Encounter Timeline: shared-arena formations with authored arrival times. Preview settings live on the child below.", MessageType.Info);
        if (GUILayout.Button("Select timeline preview controls")) Selection.activeGameObject = lab.timelinePreview.gameObject;
        if (GUILayout.Button("Open Encounter Composer")) EnemyEncounterComposer.Show(lab.timelinePreview.director.encounterTimeline, lab.timelinePreview);
    }
}
[CustomEditor(typeof(EnemyEncounterLab))]
public sealed class EnemyEncounterLabInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector(); var lab = (EnemyEncounterLab)target;
        if (!lab.director) return;
        var director = lab.director;
        if (GUILayout.Button("Open Encounter Composer")) EnemyEncounterComposer.Show(director.encounterTimeline, lab);
        if (lab.manualPlayerControl)
            EditorGUILayout.HelpBox("Manual P1: click the Game view to control the left actor with normal Player 1 bindings. The other actor stays automated. Toggle off to return to automatic control without restarting.", MessageType.Info);
        EditorGUILayout.HelpBox("Layout presets are fixtures for testing placement, not replicas of the six levels. Changing preset, cue, seed or timeline restarts the preview. Preview players restore lost mass.", MessageType.Info);
        var so = new SerializedObject(director); so.Update();
        EditorGUILayout.PropertyField(so.FindProperty("encounterTimeline"));
        EditorGUILayout.PropertyField(so.FindProperty("encounterSeed"));
        string[] choices = new string[1 + (director.encounterTimeline ? director.encounterTimeline.cues.Count : 0)]; choices[0] = "Full timeline";
        for (int i = 1; i < choices.Length; i++) choices[i] = director.encounterTimeline.cues[i - 1].label;
        so.FindProperty("previewCue").intValue = EditorGUILayout.Popup("Preview", director.previewCue + 1, choices) - 1;
        so.ApplyModifiedProperties();
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button(director.timelinePaused ? "Resume timeline" : "Pause timeline")) director.timelinePaused = !director.timelinePaused;
            if (GUILayout.Button("Restart preview")) lab.RestartPreview();
        }
        if (Application.isPlaying)
        {
            EditorGUILayout.LabelField($"Clock {director.GameplayAge:0.0}s   Alive {director.AliveCount}   Reserved {director.TimelinePendingCount}   Pressure {director.CurrentPressure:0.0}");
            foreach (var cue in director.CueStates)
                EditorGUILayout.LabelField(cue.label + ": " + cue.state + " — " + cue.Progress + (string.IsNullOrEmpty(cue.reason) ? "" : "\n" + cue.reason), EditorStyles.wordWrappedLabel);
            Repaint();
        }
    }
    private void OnSceneGUI()
    {
        var lab = (EnemyEncounterLab)target; var d = lab.director;
        if (!d || !d.encounterTimeline || !lab.layout) return;
        for (int i = 0; i < d.encounterTimeline.cues.Count; i++)
        {
            if (d.previewCue >= 0 && d.previewCue != i) continue;
            var formation = d.ChooseFormation(i, out bool mirror); if (!formation) continue;
            for (int slotIndex = 0; slotIndex < formation.slots.Count; slotIndex++)
            {
                var slot = d.encounterTimeline.cues[i].GetSlot(formation, slotIndex);
                if (lab.layout.Resolve(slot, mirror, out var pose, out _, d.encounterTimeline.cues[i].flipWallOrientation) && slot.enemy)
                {
                    Handles.color = lab.layout.Clear(pose, slot.enemy.spawnRadiusWorld, d.borderBufferWorld, out _) ? Color.cyan : Color.red;
                    Handles.DrawWireDisc(pose.clearance, Vector3.up, slot.enemy.spawnRadiusWorld);
                    Handles.DrawLine(pose.position, pose.position + pose.rotation * Vector3.forward);
                    float scale = Application.isPlaying ? d.TimelineTimeScale : d.PreviewTimelineTimeScale(d.encounterTimeline);
                    Handles.Label(pose.position + Vector3.up * .2f, $"{d.encounterTimeline.cues[i].arrivalSeconds * scale + slot.releaseDelay:0.0}s {slot.enemy.name}");
                }
            }
        }
    }
}
[CustomEditor(typeof(EnemyEncounterTimeline))]
public sealed class EnemyEncounterTimelineInspector : Editor
{
    public override void OnInspectorGUI()
    {
        if (GUILayout.Button("Open Encounter Composer")) EnemyEncounterComposer.Show((EnemyEncounterTimeline)target);
        DrawDefaultInspector();
    }
}
#endif
