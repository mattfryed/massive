#if UNITY_EDITOR
using System.Collections.Generic;
using Massive.Enemies;
using UnityEditor;
using UnityEngine;

/// <summary>Edits the existing spawn assets directly; no duplicate settings or scene overrides.</summary>
public sealed class DroneSpawnIndicatorWindow : EditorWindow
{
    [SerializeField] private EnemySpawnProfile profile;
    [SerializeField] private int batchIndex;
    private Vector2 scroll;
    private EnemySpawnTelegraph indicator;
    private Material glow;

    [MenuItem("MASSIVE/Drone Spawn Indicator")]
    [MenuItem("MASSIVE/Enemies/Drone/Spawn Indicator Settings")]
    public static void Open()
    {
        var window = GetWindow<DroneSpawnIndicatorWindow>("Drone Spawn Indicator");
        window.minSize = new Vector2(440, 640);
        window.Show(); window.Focus();
    }

    private void OnEnable()
    {
        if (!profile) FindProfile();
        Undo.undoRedoPerformed += OnUndo;
    }

    private void OnDisable() => Undo.undoRedoPerformed -= OnUndo;

    private void FindProfile()
    {
        foreach (var director in Object.FindObjectsByType<EnemyDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (DroneBatches(director.spawnProfile).Count == 0) continue;
            profile = director.spawnProfile; return;
        }
        profile = AssetDatabase.LoadAssetAtPath<EnemySpawnProfile>(DronePrototypeSetup.ProfilePath);
    }

    private static List<int> DroneBatches(EnemySpawnProfile source)
    {
        var indices = new List<int>();
        if (!source || source.batches == null) return indices;
        for (int i = 0; i < source.batches.Count; i++)
        {
            var rule = source.batches[i];
            if (rule != null && rule.enemy && rule.enemy.id == "DRONE") indices.Add(i);
        }
        return indices;
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Drone spawn indicator", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("These are the saved spawn assets. Changes apply to new batches in every level using them. Edit outside Play Mode; Undo is supported.", MessageType.Info);
        profile = (EnemySpawnProfile)EditorGUILayout.ObjectField("Spawn profile", profile, typeof(EnemySpawnProfile), false);
        var indices = DroneBatches(profile);
        if (indices.Count == 0)
        {
            EditorGUILayout.HelpBox("Choose a spawn profile containing a Drone batch.", MessageType.Warning);
            if (GUILayout.Button("Find Drone profile")) FindProfile();
            return;
        }
        if (!indices.Contains(batchIndex)) batchIndex = indices[0];
        if (indices.Count > 1)
        {
            var names = new string[indices.Count];
            for (int i = 0; i < names.Length; i++) names[i] = "Drone batch " + (indices[i] + 1);
            batchIndex = indices[EditorGUILayout.Popup("Batch", indices.IndexOf(batchIndex), names)];
        }
        indicator = profile.batches[batchIndex].telegraphPrefab;
        glow = indicator ? indicator.GetComponent<MeshRenderer>().sharedMaterial : null;
        scroll = EditorGUILayout.BeginScrollView(scroll);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            DrawTiming();
            if (indicator)
            {
                DrawAppearance();
                DrawGlow();
            }
            else EditorGUILayout.HelpBox("Assign a spawn indicator prefab to enable the warning.", MessageType.Warning);
        }
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Find the assets", EditorStyles.boldLabel);
        Locate("Timing asset", profile);
        Locate("Indicator prefab", indicator ? indicator.gameObject : null);
        Locate("Glow material", glow);
        EditorGUILayout.EndScrollView();
    }

    private void DrawTiming()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Timing", EditorStyles.boldLabel);
        var serialized = new SerializedObject(profile);
        var rule = serialized.FindProperty("batches").GetArrayElementAtIndex(batchIndex);
        var prefab = rule.FindPropertyRelative("telegraphPrefab");
        prefab.objectReferenceValue = EditorGUILayout.ObjectField("Indicator", prefab.objectReferenceValue, typeof(EnemySpawnTelegraph), false);
        EditorGUILayout.PropertyField(rule.FindPropertyRelative("telegraphSeconds"), new GUIContent("Warning lead (seconds)", "Minimum active gameplay time before the first Drone arrives."));
        if (serialized.ApplyModifiedProperties()) AssetDatabase.SaveAssetIfDirty(profile);
        EditorGUILayout.LabelField("Stays until the last Drone spawns, then fades out.", EditorStyles.wordWrappedMiniLabel);
    }

    private void DrawAppearance()
    {
        var serialized = new SerializedObject(indicator);
        Field(serialized, "fadeInSeconds", "Fade in (seconds)");
        Field(serialized, "fadeOutSeconds", "Fade out (seconds)");
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Shape and motion", EditorStyles.boldLabel);
        var shape = new SerializedObject(indicator.transform);
        var scale = shape.FindProperty("m_LocalScale");
        EditorGUI.BeginChangeCheck();
        float size = EditorGUILayout.FloatField(new GUIContent("Size multiplier", "2.8 means 2.8 times the Drone's crystal size."), scale.vector3Value.x);
        if (EditorGUI.EndChangeCheck()) scale.vector3Value = Vector3.one * Mathf.Max(.1f, size);
        Field(serialized, "rollDegreesPerSecond", "Axial rotation (degrees/sec)");
        Field(serialized, "breathScale", "Breathing amount");
        Field(serialized, "breathFrequency", "Breathing cycles/sec");
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Outward ghosts", EditorStyles.boldLabel);
        Field(serialized, "ghostsEnabled", "Enable ghosts");
        using (new EditorGUI.DisabledScope(!serialized.FindProperty("ghostsEnabled").boolValue))
        {
            Field(serialized, "ghostCount", "Ghost count");
            Field(serialized, "ghostOpacity", "Ghost opacity");
            Field(serialized, "ghostLifetime", "Ghost lifetime (seconds)");
            Field(serialized, "ghostDistance", "Travel distance (world units)");
            Field(serialized, "ghostVariation", "Lifetime / distance variation");
        }
        EditorGUILayout.LabelField("Each copy drifts in a fresh random direction around the crystal, fading as it travels. Shorter lifetimes make the movement faster.", EditorStyles.wordWrappedMiniLabel);
        bool changed = serialized.ApplyModifiedProperties();
        changed |= shape.ApplyModifiedProperties();
        if (changed) PrefabUtility.SavePrefabAsset(indicator.gameObject);
    }

    private void DrawGlow()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Glow", EditorStyles.boldLabel);
        if (!glow || !glow.HasProperty("_GlowWidth") || !glow.HasProperty("_Contrast"))
        {
            EditorGUILayout.HelpBox("The indicator needs a Drone Spawn Glow material to expose these controls.", MessageType.Warning);
            return;
        }
        EditorGUI.BeginChangeCheck();
        Color color = EditorGUILayout.ColorField("Glow color", glow.GetColor("_Color"));
        float intensity = EditorGUILayout.Slider("Brightness", glow.GetFloat("_Intensity"), 0f, 3f);
        float width = EditorGUILayout.Slider("Diffuse width", glow.GetFloat("_GlowWidth"), .005f, .4f);
        float contrast = EditorGUILayout.Slider(new GUIContent("Bright-surface contrast", "A faint dark underlay on the main crystal keeps it readable on white surfaces."), glow.GetFloat("_Contrast"), 0f, 1f);
        if (!EditorGUI.EndChangeCheck()) return;
        Undo.RecordObject(glow, "Adjust Drone spawn glow");
        glow.SetColor("_Color", color); glow.SetFloat("_Intensity", intensity);
        glow.SetFloat("_GlowWidth", width); glow.SetFloat("_Contrast", contrast);
        EditorUtility.SetDirty(glow); AssetDatabase.SaveAssetIfDirty(glow);
    }

    private static void Field(SerializedObject source, string name, string label) =>
        EditorGUILayout.PropertyField(source.FindProperty(name), new GUIContent(label));

    private static void Locate(string label, Object asset)
    {
        using (new EditorGUI.DisabledScope(!asset))
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.ObjectField(label, asset, typeof(Object), false);
            if (GUILayout.Button("Locate", GUILayout.Width(58)))
            { Selection.activeObject = asset; EditorGUIUtility.PingObject(asset); }
            EditorGUILayout.EndHorizontal();
        }
    }

    private void OnUndo()
    {
        if (profile) AssetDatabase.SaveAssetIfDirty(profile);
        if (glow) AssetDatabase.SaveAssetIfDirty(glow);
        if (indicator && (EditorUtility.IsDirty(indicator) || EditorUtility.IsDirty(indicator.transform)))
            PrefabUtility.SavePrefabAsset(indicator.gameObject);
        Repaint();
    }
}
#endif
