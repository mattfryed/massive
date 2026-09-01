#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelCatalog))]
public class LevelCatalogEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var catalog = (LevelCatalog)target;

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);

        if (GUILayout.Button("Scan Project and Populate (All LevelDefinitions)"))
        {
            PopulateCatalog(catalog);
        }

        if (GUILayout.Button("Validate Catalog (Duplicates / Missing Scene / Missing Icon)"))
        {
            ValidateCatalog(catalog);
        }

        if (GUILayout.Button("Add Missing Scenes To Build Settings"))
        {
            AddMissingScenesToBuildSettings(catalog);
        }
    }

    private static void PopulateCatalog(LevelCatalog catalog)
    {
        string[] guids = AssetDatabase.FindAssets("t:LevelDefinition");
        var defs = guids
            .Select(g => AssetDatabase.LoadAssetAtPath<LevelDefinition>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(d => d != null)
            .OrderBy(d => d.levelNumber)
            .ToList();

        var so = new SerializedObject(catalog);
        var prop = so.FindProperty("levels");
        prop.ClearArray();
        for (int i = 0; i < defs.Count; i++)
        {
            prop.InsertArrayElementAtIndex(i);
            prop.GetArrayElementAtIndex(i).objectReferenceValue = defs[i];
        }
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(catalog);
        Debug.Log($"[LevelCatalog] Populated with {defs.Count} LevelDefinitions.");
    }

    private static void ValidateCatalog(LevelCatalog catalog)
    {
        var levelsField = typeof(LevelCatalog).GetField("levels", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var levels = (System.Collections.Generic.List<LevelDefinition>)levelsField.GetValue(catalog);

        if (levels == null || levels.Count == 0)
        {
            Debug.LogWarning("[LevelCatalog] No levels assigned.");
            return;
        }

        // Duplicate stage numbers
        var dupNums = levels
            .Where(l => l != null)
            .GroupBy(l => l.levelNumber)
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var g in dupNums)
            Debug.LogWarning($"[LevelCatalog] Duplicate levelNumber {g.Key}: {string.Join(", ", g.Select(x => x.name))}");

        // Missing icons / scenes
        foreach (var def in levels)
        {
            if (def == null) continue;

            if (def.iconPrefab == null)
                Debug.LogWarning($"[LevelCatalog] {def.name} missing iconPrefab.");

            if (string.IsNullOrEmpty(def.SceneName))
                Debug.LogWarning($"[LevelCatalog] {def.name} missing gameplayScene (SceneAsset not assigned).");
        }

        Debug.Log("[LevelCatalog] Validation complete.");
    }

    private static void AddMissingScenesToBuildSettings(LevelCatalog catalog)
    {
        var levelsField = typeof(LevelCatalog).GetField("levels", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var levels = (System.Collections.Generic.List<LevelDefinition>)levelsField.GetValue(catalog);

        if (levels == null || levels.Count == 0)
        {
            Debug.LogWarning("[LevelCatalog] No levels to sync.");
            return;
        }

        var existing = EditorBuildSettings.scenes.ToList();
        int added = 0;

        foreach (var def in levels)
        {
            if (def == null) continue;
            if (def.gameplayScene == null) continue;

            string path = def.gameplayScene.ScenePath;
            if (string.IsNullOrEmpty(path)) continue;

            bool already = existing.Any(s => s.path == path);
            if (!already)
            {
                existing.Add(new EditorBuildSettingsScene(path, true));
                added++;
            }
        }

        if (added > 0)
        {
            EditorBuildSettings.scenes = existing.ToArray();
            Debug.Log($"[LevelCatalog] Added {added} missing scenes to Build Settings.");
        }
        else
        {
            Debug.Log("[LevelCatalog] No missing scenes to add.");
        }
    }
}
#endif
