using Massive.Multiplier;
using UnityEditor;
using UnityEngine;

public sealed class AmplifierTreatmentsWindow : EditorWindow
{
    private AmplifierGoalTreatments target;
    private Vector2 scroll;
    private double last;
    [MenuItem("MASSIVE/Amplifier Treatments")]
    public static void Open() { var w=GetWindow<AmplifierTreatmentsWindow>("Amplifier Treatments");w.minSize=new Vector2(440,540);w.Show();w.Focus(); }
    private void OnEnable() { last = EditorApplication.timeSinceStartup; EditorApplication.update += Tick; }
    private void OnDisable() { EditorApplication.update -= Tick; }
    private void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        if (target != null && target.isActiveAndEnabled && target.Preview && !target.PreviewPaused && !Application.isPlaying && !AmplifierTreatmentRecorder.IsRecording)
        {
            target.AdvancePreview((float)Mathf.Min(0.05f, (float)(now - last)));
            EditorApplication.QueuePlayerLoopUpdate(); SceneView.RepaintAll(); Repaint();
        }
        last = now;
    }
    private void OnGUI()
    {
        if (target == null) target = Object.FindFirstObjectByType<AmplifierGoalTreatments>();
        if (target == null)
        {
            EditorGUILayout.HelpBox("Open Dynamo Prototype, then install the scene treatment workbench.", MessageType.Info);
            if (GUILayout.Button("Set up Dynamo treatments")) target = Install();
            return;
        }
        EditorGUILayout.LabelField(target.Preview?"LIVE PREVIEW — VISUAL SIMULATION":"PREVIEW STOPPED — GAMEPLAY CONTROLS THE EFFECTS", EditorStyles.boldLabel);
        Undo.RecordObject(target,"Adjust amplifier preview");
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if(!target.Preview)
        { if(GUILayout.Button("Start Preview",EditorStyles.toolbarButton))target.StartPreview(); }
        else
        {
            if(GUILayout.Button(target.PreviewPaused?"Resume":"Pause",EditorStyles.toolbarButton))target.PreviewPaused=!target.PreviewPaused;
            if(GUILayout.Button("Stop Preview",EditorStyles.toolbarButton)){target.StopPreview();EditorUtility.SetDirty(target);}
        }
        if(GUILayout.Button("Capture now",EditorStyles.toolbarButton))target.TriggerPreview();
        DrawLoopToggle();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        target.LoopInterval=EditorGUILayout.Slider("Repeat every (seconds)",target.LoopInterval,1,12);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        int hold=GUILayout.Toolbar(target.HeldLevel,new[]{"×1","×2","×4","×8"});
        if(hold!=target.HeldLevel){target.HeldLevel=hold;target.Preview=true;}
        target.PreviewTeam=GUILayout.Toolbar(target.PreviewTeam-1,new[]{"Light","Dark"})+1;
        EditorGUILayout.EndHorizontal();
        target.Settings.allowEffectsOverGrid=EditorGUILayout.Toggle("Allow Effects Over Grid",target.Settings.allowEffectsOverGrid);
        if(EditorGUI.EndChangeCheck()){target.RenderTreatment();EditorUtility.SetDirty(target);}
        EditorGUILayout.HelpBox(target.Preview?"Pause holds the frame. Stop removes the preview core and test multiplier. Entering Play Mode stops preview automatically.":"Preview is off. Your effect settings are retained for real captures. Start Preview or Capture now to test visuals again.",MessageType.None);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("Color interference — spectral edge strands",EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        var seamMode=(AmplifierSeamMode)GUILayout.Toolbar((int)target.Settings.seamMode,AmplifierTreatmentSettings.SeamNames);
        bool seamCapture=EditorGUILayout.Toggle("Interference on capture",target.Settings.seamCapture);
        bool seamHeld=EditorGUILayout.Toggle("Interference while held",target.Settings.seamHeld);
        if(EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(target,"Change chromatic seam state");
            target.Settings.seamMode=seamMode;target.Settings.seamCapture=seamCapture;target.Settings.seamHeld=seamHeld;
            target.RenderTreatment();EditorUtility.SetDirty(target);
        }
        EditorGUILayout.HelpBox("Clustered fringe gathers color into edge patches. Drifting interference moves and mixes those patches. Neither paints the goal interior.",MessageType.None);
        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < 4; i++) if (GUILayout.Button((i + 1).ToString())) { Undo.RecordObject(target, "Change amplifier treatment"); target.ApplyPreset(i); EditorUtility.SetDirty(target); }
        EditorGUILayout.EndHorizontal();
        for (int i = 0; i < 4; i++) EditorGUILayout.LabelField((i + 1) + " · " + AmplifierTreatmentSettings.Names[i], EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Capture")) target.TriggerPreview();
        if (GUILayout.Button("Reset event")) target.ResetPreview();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < 4; i++) if (GUILayout.Button("Hold ×" + (1 << i))) { Undo.RecordObject(target, "Hold amplifier multiplier"); target.Preview = true; target.HeldLevel = i; EditorUtility.SetDirty(target); }
        EditorGUILayout.EndHorizontal();
        var serialized = new SerializedObject(target);
        serialized.Update();
        foreach (string name in new[] { "playbackSpeed", "showControls", "legacyCaptureFeedback" })
            EditorGUILayout.PropertyField(serialized.FindProperty(name), true);
        var field=serialized.FindProperty("treatment");var end=field.GetEndProperty();
        field.NextVisible(true);
        while(!SerializedProperty.EqualContents(field,end))
        {
            string label=field.displayName.Replace("Seam ","Interference ");
            if(field.name.EndsWith("Shimmer"))field.isExpanded=true;
            EditorGUILayout.PropertyField(field,new GUIContent(label),true);
            if(!field.NextVisible(false))break;
        }
        if(serialized.ApplyModifiedProperties())target.RenderTreatment();
        EditorGUILayout.EndScrollView();
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if(GUILayout.Button("Capture now",EditorStyles.toolbarButton))target.TriggerPreview();
        DrawLoopToggle();
        if(GUILayout.Button("Stop Preview",EditorStyles.toolbarButton)){target.StopPreview();EditorUtility.SetDirty(target);}
        if(GUILayout.Button("Reset",EditorStyles.toolbarButton))target.ResetPreview();
        EditorGUILayout.EndHorizontal();
    }
    private void DrawLoopToggle()
    {
        bool loop=GUILayout.Toggle(target.LoopCapture,"Loop Capture",EditorStyles.toolbarButton);
        if(loop==target.LoopCapture)return;
        target.LoopCapture=loop;
        if(loop)target.StartPreview();
    }
    public static AmplifierGoalTreatments Install()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "S-8_DYNAMO-PROTOTYPE")
            throw new System.InvalidOperationException("Install only in S-8_DYNAMO-PROTOTYPE.");
        var grid = Object.FindFirstObjectByType<VectorGridGPU>();
        if (grid == null) throw new System.InvalidOperationException("Dynamo grid missing.");
        var existing = grid.GetComponent<AmplifierGoalTreatments>();
        var goals = Object.FindObjectsByType<AmplifierGoalCapture>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var result = existing != null ? existing : Undo.AddComponent<AmplifierGoalTreatments>(grid.gameObject);
        Undo.RecordObject(result, "Configure amplifier treatments");
        foreach (var goal in goals) Undo.RecordObject(goal, "Connect amplifier treatments");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Power-ups/Amplifier Core/Amplifier Core.prefab");
        result.Configure(grid, goals, Shader.Find("MASSIVE/AmplifierTreatment"), prefab != null ? prefab.GetComponent<AmplifierCoreGameplay>() : null);
        foreach (var goal in goals) { EditorUtility.SetDirty(goal); PrefabUtility.RecordPrefabInstancePropertyModifications(goal); }
        EditorUtility.SetDirty(result);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(grid.gameObject.scene);
        Selection.activeObject = result;
        return result;
    }
}

// This hook is independent of the window, including when domain/scene reload is disabled.
[InitializeOnLoad]
internal static class AmplifierPreviewPlayModeLifecycle
{
    static AmplifierPreviewPlayModeLifecycle() { EditorApplication.playModeStateChanged += OnPlayModeChanged; }
    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.ExitingEditMode)return;
        if(AmplifierTreatmentRecorder.IsRecording)AmplifierTreatmentRecorder.Stop();
        foreach(var t in Object.FindObjectsByType<AmplifierGoalTreatments>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            t.StopPreview();
    }
}
