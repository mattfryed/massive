// Assets/VectorGridNu/Editor/GridInteractionProfileEditor.cs
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(GridInteractionProfile))]
public class GridInteractionProfileEditor : Editor
{
    SerializedProperty _modules;
    SerializedProperty _strengthMult, _radiusMult;

    ReorderableList _list;
    readonly Dictionary<int,bool> _fold = new();

    // Presets for the "+ Add Module" dropdown
    static readonly string[] _presetNames = {
        "Player Wake (Directional)",
        "Dash Burst (One-shot)",
        "Score Pulse (Envelope)",
        "Vortex (Spin)",
        "Jiggle (Noise)",
        "Traveling Wave (Ring)",
        "Celestial Body (Constant Radial)"
    };

    void OnEnable()
    {
        _modules      = serializedObject.FindProperty("modules");
        _strengthMult = serializedObject.FindProperty("strengthMultiplier");
        _radiusMult   = serializedObject.FindProperty("radiusMultiplier");

        _list = new ReorderableList(serializedObject, _modules, true, true, true, true);
        _list.drawHeaderCallback    = DrawHeader;
        _list.elementHeightCallback = GetElementHeight;
        _list.drawElementCallback   = DrawElement;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Preset row
        EditorGUILayout.Space(4);
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("+ Add Module", GUILayout.Width(100));
            int sel = EditorGUILayout.Popup(-1, _presetNames);
            if (sel >= 0) AddPreset(sel);
            if (GUILayout.Button("Refresh", GUILayout.Width(80))) Repaint();
        }

        // Modules list
        EditorGUILayout.Space(4);
        _list.DoLayoutList();

        // Multipliers
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Profile Multipliers", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_strengthMult);
        EditorGUILayout.PropertyField(_radiusMult);

        // Play-mode info
        if (!EditorApplication.isPlaying)
            EditorGUILayout.HelpBox("Enter Play Mode to use Trigger / On–Off buttons per module.", MessageType.Info);

        serializedObject.ApplyModifiedProperties();
    }

    // ---------- ReorderableList callbacks ----------

    void DrawHeader(Rect r) => EditorGUI.LabelField(r, "Modules (emitted by this object)");

    float GetElementHeight(int index)
    {
        var el = _modules.GetArrayElementAtIndex(index);
        float header = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        bool open = _fold.TryGetValue(index, out var v) ? v : true;
        if (!open) return header + 4;

        // Let Unity compute full height of the struct
        float body = EditorGUI.GetPropertyHeight(el, includeChildren: true) + 4;

        // One extra row for play-mode buttons/info
        float controls = EditorGUIUtility.singleLineHeight + 4;

        return header + body + controls + 4;
    }

    void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
    {
        var el = _modules.GetArrayElementAtIndex(index);
        var lh = EditorGUIUtility.singleLineHeight;

        // Header / foldout
        var typeProp = el.FindPropertyRelative("type");
        string title = $"[{index}] {typeProp.enumDisplayNames[typeProp.enumValueIndex]}";
        bool open = _fold.TryGetValue(index, out var v) ? v : true;
        open = EditorGUI.Foldout(new Rect(rect.x, rect.y, rect.width, lh), open, title, true);
        _fold[index] = open;

        float y = rect.y + lh + 2;

        if (open)
        {
            // Draw struct body automatically
            var body = new Rect(rect.x, y, rect.width, EditorGUI.GetPropertyHeight(el, includeChildren: true));
            EditorGUI.indentLevel++;
            EditorGUI.PropertyField(body, el, GUIContent.none, includeChildren: true);
            EditorGUI.indentLevel--;
            y += body.height + 4;
        }

        // Play-mode controls row
        var tagProp = el.FindPropertyRelative("tag");
        string tag = tagProp.stringValue;
        var t = (GridModuleType) typeProp.enumValueIndex;
        var row = new Rect(rect.x, y, rect.width, lh);

        if (EditorApplication.isPlaying)
        {
            if (t == GridModuleType.BurstRadial || t == GridModuleType.Pulse || t == GridModuleType.TravelingWave)
            {
                if (GUI.Button(new Rect(row.x, row.y, 90, lh), "▶ Trigger"))
                    TriggerAll(tag);
            }
            else
            {
                if (GUI.Button(new Rect(row.x, row.y, 60, lh), "On"))
                    SetActiveAll(tag, true);
                if (GUI.Button(new Rect(row.x + 66, row.y, 60, lh), "Off"))
                    SetActiveAll(tag, false);
            }
        }
        else
        {
            EditorGUI.LabelField(row, "Play Mode: Trigger / On–Off buttons appear here", EditorStyles.miniLabel);
        }
    }

    // ---------- Trigger helpers (operate on all active interactors using this profile) ----------

    void TriggerAll(string tag)
    {
        var prof = (GridInteractionProfile)target;
        foreach (var gi in FindObjectsByType<GridInteractor>(FindObjectsSortMode.None)
                 .Where(i => i && i.enabled && i.gameObject.activeInHierarchy && i.profile == prof))
            gi.Trigger(tag);
    }

    void SetActiveAll(string tag, bool active)
    {
        var prof = (GridInteractionProfile)target;
        foreach (var gi in FindObjectsByType<GridInteractor>(FindObjectsSortMode.None)
                 .Where(i => i && i.enabled && i.gameObject.activeInHierarchy && i.profile == prof))
            gi.SetActive(tag, active);
    }

    // ---------- Presets ----------

    void AddPreset(int sel)
    {
        serializedObject.Update();
        int i = _modules.arraySize;
        _modules.InsertArrayElementAtIndex(i);
        var el = _modules.GetArrayElementAtIndex(i);
        InitDefaults(el);

        switch (sel)
        {
            case 0: // Player Wake (Directional)
                SetEnum (el,"type",(int)GridModuleType.DirectionalWake);
                SetString(el,"tag","Wake");
                SetFloat (el,"radius",3f); SetFloat(el,"strength",10f);
                SetBool  (el,"useVelocity",true); SetBool(el,"pullAgainstVelocity",true);
                break;

            case 1: // Dash Burst
                SetEnum (el,"type",(int)GridModuleType.BurstRadial);
                SetString(el,"tag","Dash");
                SetFloat (el,"radius",4.5f); SetFloat(el,"strength",18f);
                SetFloat (el,"duration",0.18f);
                SetCurve (el,"envelope",AnimationCurve.EaseInOut(0,1,1,0));
                SetBool  (el,"scaleBySpeed",false);
                break;

            case 2: // Score Pulse
                SetEnum (el,"type",(int)GridModuleType.Pulse);
                SetString(el,"tag","Score");
                SetFloat (el,"radius",4.5f); SetFloat(el,"strength",14f);
                SetFloat (el,"duration",0.5f);
                SetBool  (el,"loopPulse",false);
                SetBool  (el,"scaleBySpeed",false);
                break;

            case 3: // Vortex
                SetEnum (el,"type",(int)GridModuleType.Vortex);
                SetString(el,"tag","Vortex");
                SetFloat (el,"radius",3.5f); SetFloat(el,"strength",10f);
                SetFloat (el,"spinDegPerSec",180f);
                SetBool  (el,"scaleBySpeed",false);
                break;

            case 4: // Jiggle
                SetEnum (el,"type",(int)GridModuleType.Jiggle);
                SetString(el,"tag","Jiggle");
                SetFloat (el,"radius",2.5f); SetFloat(el,"strength",6f);
                SetFloat (el,"noiseAmplitude",0.35f);
                SetFloat (el,"noiseFrequency",8f);
                break;

            case 5: // Traveling Wave
                SetEnum (el,"type",(int)GridModuleType.TravelingWave);
                SetString(el,"tag","Wave");
                SetFloat (el,"radius",1f); SetFloat(el,"strength",12f);
                SetFloat (el,"waveSpeed",8f); SetFloat(el,"waveThickness",0.7f);
                SetBool  (el,"scaleBySpeed",false);
                break;

            case 6: // Celestial Body
                SetEnum (el,"type",(int)GridModuleType.ConstantRadial);
                SetString(el,"tag","Gravity");
                SetFloat (el,"radius",6f); SetFloat(el,"strength",20f);
                SetFloat (el,"innerFrac",0.15f);
                SetBool  (el,"scaleBySpeed",false);
                break;
        }

        serializedObject.ApplyModifiedProperties();
    }

    void InitDefaults(SerializedProperty el)
    {
        SetEnum (el,"type",(int)GridModuleType.ConstantRadial);
        SetString(el,"tag","");
        SetFloat (el,"radius",3f);
        SetFloat (el,"strength",10f);
        SetFloat (el,"innerFrac",0f);
        SetBool  (el,"directional",false);
        SetBool  (el,"useVelocity",false);
        SetBool  (el,"pullAgainstVelocity",true);
        SetVec3  (el,"fixedDirection",Vector3.right);
        SetBool  (el,"scaleBySpeed",true);
        SetCurve (el,"radiusOverSpeed",   AnimationCurve.Linear(0,1,10,1.2f));
        SetCurve (el,"strengthOverSpeed", AnimationCurve.Linear(0,0.7f,10,1.2f));
        SetFloat (el,"spinDegPerSec",180f);
        SetFloat (el,"noiseAmplitude",0.35f);
        SetFloat (el,"noiseFrequency",8f);
        SetFloat (el,"duration",0.18f);
        SetBool  (el,"loopPulse",false);
        SetCurve (el,"envelope",AnimationCurve.EaseInOut(0,1,1,0));
        SetFloat (el,"waveSpeed",8f);
        SetFloat (el,"waveThickness",0.7f);
    }

    // tiny setters
    void SetEnum (SerializedProperty el, string name, int v)         => el.FindPropertyRelative(name).enumValueIndex = v;
    void SetString(SerializedProperty el, string name, string v)     => el.FindPropertyRelative(name).stringValue    = v;
    void SetFloat(SerializedProperty el, string name, float v)       => el.FindPropertyRelative(name).floatValue     = v;
    void SetBool (SerializedProperty el, string name, bool v)        => el.FindPropertyRelative(name).boolValue      = v;
    void SetVec3 (SerializedProperty el, string name, Vector3 v)     => el.FindPropertyRelative(name).vector3Value   = v;
    void SetCurve(SerializedProperty el, string name, AnimationCurve c) => el.FindPropertyRelative(name).animationCurveValue = c;
}
