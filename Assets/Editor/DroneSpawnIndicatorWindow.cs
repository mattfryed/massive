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
    [SerializeField] private bool dysonMode;
    private struct RuleRow { public bool batch; public int index; public string label; }
    private RuleRow row;
    private Vector2 scroll;
    private EnemySpawnTelegraph indicator;
    private Material glow;

    [MenuItem("MASSIVE/Drone Spawn Indicator")]
    [MenuItem("MASSIVE/Enemies/Drone/Spawn Indicator Settings")]
    public static void Open()
    {
        var window = ShowWindow(); window.dysonMode = false; window.FindProfile();
    }

    [MenuItem("MASSIVE/Enemy Spawn Indicators")]
    public static void OpenAll() => ShowWindow();

    [MenuItem("MASSIVE/Enemies/Dyson Sphere/Spawn Indicator Settings")]
    public static void OpenDyson()
    {
        var window = ShowWindow(); window.dysonMode = true; window.FindProfile();
    }

    private static DroneSpawnIndicatorWindow ShowWindow()
    {
        var window = GetWindow<DroneSpawnIndicatorWindow>("Enemy Spawn Indicators");
        window.minSize = new Vector2(440, 640);
        window.Show(); window.Focus();
        return window;
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
            if (Rows(director.spawnProfile).Count == 0) continue;
            profile = director.spawnProfile; return;
        }
        profile = AssetDatabase.LoadAssetAtPath<EnemySpawnProfile>(DronePrototypeSetup.ProfilePath);
    }

    private bool Matches(EnemyDefinition enemy) => enemy &&
        (dysonMode ? enemy.defeatRewardKey == "ENEMY_DYSON_DEFEAT" : enemy.id == "DRONE");

    private List<RuleRow> Rows(EnemySpawnProfile source)
    {
        var indices = new List<RuleRow>();
        if (!source || source.batches == null) return indices;
        for (int i = 0; i < source.batches.Count; i++)
        {
            var rule = source.batches[i];
            if (rule != null && Matches(rule.enemy)) indices.Add(new RuleRow { batch = true, index = i, label = "Batch " + (i + 1) });
        }
        for (int i = 0; i < source.rules.Count; i++)
            if (Matches(source.rules[i].enemy)) indices.Add(new RuleRow { batch = false, index = i, label = "Single spawn rule " + (i + 1) });
        return indices;
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8);
        EditorGUIUtility.labelWidth = 195f;
        EditorGUILayout.LabelField("Enemy spawn indicators", EditorStyles.boldLabel);
        bool dyson = GUILayout.Toolbar(dysonMode ? 1 : 0, new[] { "Drone", "Dyson Sphere" }) == 1;
        if (dyson != dysonMode) { dysonMode = dyson; batchIndex = 0; if (Rows(profile).Count == 0) FindProfile(); }
        EditorGUILayout.HelpBox("These are the saved spawn assets. Changes apply to new batches in every level using them. Edit outside Play Mode; Undo is supported.", MessageType.Info);
        profile = (EnemySpawnProfile)EditorGUILayout.ObjectField("Spawn profile", profile, typeof(EnemySpawnProfile), false);
        var indices = Rows(profile);
        if (indices.Count == 0)
        {
            EditorGUILayout.HelpBox("Choose a spawn profile containing this enemy type.", MessageType.Warning);
            if (GUILayout.Button("Find spawn profile")) FindProfile();
            return;
        }
        batchIndex = Mathf.Clamp(batchIndex, 0, indices.Count - 1);
        if (indices.Count > 1)
        {
            var names = new string[indices.Count];
            for (int i = 0; i < names.Length; i++) names[i] = indices[i].label;
            batchIndex = EditorGUILayout.Popup("Spawn rule", batchIndex, names);
        }
        row = indices[batchIndex];
        scroll = EditorGUILayout.BeginScrollView(scroll);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            DrawTiming();
            indicator = row.batch ? profile.batches[row.index].telegraphPrefab : profile.rules[row.index].telegraphPrefab;
            glow = indicator ? indicator.GetComponent<MeshRenderer>().sharedMaterial : null;
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
        var rule = serialized.FindProperty(row.batch ? "batches" : "rules").GetArrayElementAtIndex(row.index);
        bool individual = !row.batch;
        if (row.batch)
        {
            var mode = rule.FindPropertyRelative("telegraphMode");
            mode.enumValueIndex = EditorGUILayout.Popup("Indicator mode", mode.enumValueIndex,
                new[] { "Per cluster", dysonMode ? "Per Dyson Sphere" : "Per Drone" });
            individual = mode.enumValueIndex == (int)EnemyBatchTelegraphMode.PerEnemy;
        }
        var prefab = rule.FindPropertyRelative("telegraphPrefab");
        prefab.objectReferenceValue = EditorGUILayout.ObjectField("Indicator", prefab.objectReferenceValue, typeof(EnemySpawnTelegraph), false);
        EditorGUILayout.PropertyField(rule.FindPropertyRelative("telegraphSeconds"), new GUIContent("Warning lead (seconds)", "Minimum active gameplay time from warning to enemy arrival."));
        if (!row.batch) EditorGUILayout.PropertyField(rule.FindPropertyRelative("blockedSpawnTimeout"), new GUIContent("Blocked location timeout"));
        if (serialized.ApplyModifiedProperties()) AssetDatabase.SaveAssetIfDirty(profile);
        EditorGUILayout.LabelField(individual ? "Each marker reserves the exact arrival point and blurs away when its enemy begins spawning. Batch warnings are staggered." :
            "One shared marker blurs away as the first cluster member begins spawning.", EditorStyles.wordWrappedMiniLabel);
    }

    private void DrawAppearance()
    {
        var serialized = new SerializedObject(indicator);
        Field(serialized, "wireframeDelay", "Glow head start (seconds)");
        Field(serialized, "fadeInSeconds", "Wireframe fade in (seconds)");
        Field(serialized, "fadeOutSeconds", "Defocus exit (seconds)");
        Field(serialized, "defocusWidth", "Exit blur width");
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Colors and broad glow", EditorStyles.boldLabel);
        Field(serialized, "wireframeColor", "Wireframe color");
        Field(serialized, "glowColor", "Glow color");
        Field(serialized, "ghostColor", "Ghost color");
        Field(serialized, "diffuseGlowStrength", "Broad glow strength");
        Field(serialized, "diffuseGlowSize", "Broad glow size");
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Shape and motion", EditorStyles.boldLabel);
        var range = serialized.FindProperty("sizeMultiplierRange");
        Vector2 size = EditorGUILayout.Vector2Field(new GUIContent("Size range (min / max)", "Set both values to 1 to match the enemy. Each indicator picks one scale within this range."), range.vector2Value);
        range.vector2Value = new Vector2(Mathf.Max(.1f, size.x), Mathf.Max(.1f, Mathf.Max(size.x, size.y)));
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
            Field(serialized, "ghostEndScale", "Ending scale fraction");
        }
        EditorGUILayout.LabelField("Each copy shrinks and fades while drifting in a fresh random direction. Shorter lifetimes make the movement faster.", EditorStyles.wordWrappedMiniLabel);
        bool changed = serialized.ApplyModifiedProperties();
        if (changed) PrefabUtility.SavePrefabAsset(indicator.gameObject);
    }

    private void DrawGlow()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Wireframe edge glow", EditorStyles.boldLabel);
        if (!glow || !glow.HasProperty("_GlowWidth") || !glow.HasProperty("_Contrast"))
        {
            EditorGUILayout.HelpBox("The indicator needs an EnemySpawnOutline material to expose these controls.", MessageType.Warning);
            return;
        }
        EditorGUI.BeginChangeCheck();
        float intensity = EditorGUILayout.Slider("Brightness", glow.GetFloat("_Intensity"), 0f, 3f);
        float width = EditorGUILayout.Slider("Diffuse width", glow.GetFloat("_GlowWidth"), .005f, .4f);
        float contrast = EditorGUILayout.Slider(new GUIContent("Bright-surface contrast", "A faint dark underlay on the main crystal keeps it readable on white surfaces."), glow.GetFloat("_Contrast"), 0f, 1f);
        if (!EditorGUI.EndChangeCheck()) return;
        Undo.RecordObject(glow, "Adjust enemy spawn glow");
        glow.SetFloat("_Intensity", intensity);
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
