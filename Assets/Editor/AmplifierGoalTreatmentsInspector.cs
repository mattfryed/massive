using Massive.Multiplier;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AmplifierGoalTreatments))]
public sealed class AmplifierGoalTreatmentsInspector : Editor
{
    public override void OnInspectorGUI()
    {
        var workbench=(AmplifierGoalTreatments)target;
        EditorGUILayout.HelpBox(workbench.Preview?"Preview active: test multiplier and capture animation. Stop Preview returns control to gameplay. Entering Play Mode also stops it automatically.":"Preview stopped. Real gameplay controls the multiplier and captures; your effect settings remain active.",MessageType.Info);
        if(GUILayout.Button("Open Live Preview — no Play Mode needed",GUILayout.Height(32)))AmplifierTreatmentsWindow.Open();
        EditorGUILayout.BeginHorizontal();
        if(workbench.Preview)
        { if(GUILayout.Button("Stop Preview")){Undo.RecordObject(workbench,"Stop amplifier preview");workbench.StopPreview();EditorUtility.SetDirty(workbench);} }
        else if(GUILayout.Button("Start Preview")){workbench.StartPreview();AmplifierTreatmentsWindow.Open();}
        if(GUILayout.Button("Capture now"))workbench.TriggerPreview();
        bool loop=GUILayout.Toggle(workbench.LoopCapture,"Loop Capture","Button");
        if(loop!=workbench.LoopCapture){Undo.RecordObject(workbench,"Loop capture");workbench.LoopCapture=loop;if(loop)workbench.StartPreview();EditorUtility.SetDirty(workbench);AmplifierTreatmentsWindow.Open();}
        EditorGUILayout.EndHorizontal();
        serializedObject.Update();
        foreach(var name in new[]{"previewTeam","heldLevel","playbackSpeed","loopInterval","showControls"})
            EditorGUILayout.PropertyField(serializedObject.FindProperty(name));
        var field=serializedObject.FindProperty("treatment");var end=field.GetEndProperty();field.NextVisible(true);
        while(!SerializedProperty.EqualContents(field,end))
        {
            if(field.name.EndsWith("Shimmer"))field.isExpanded=true;
            EditorGUILayout.PropertyField(field,new GUIContent(field.displayName.Replace("Seam ","Interference ")),true);
            if(!field.NextVisible(false))break;
        }
        serializedObject.ApplyModifiedProperties();
    }
}
