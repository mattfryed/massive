#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Massive.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Moves planar mask presentation to the Surface without changing collision geometry.</summary>
[InitializeOnLoad]
public static class ArenaOutsideMaskSetup
{
    public const string Surface = "Assets/Prefabs/Levels/Arena_Planar_Surface.prefab";
    public const string Bounds = "Assets/Prefabs/Levels/Arena_Planar_Bounds.prefab";
    public const string ShaderPath = "Assets/Scripts/Anomalies/COSMOS/CosmosArenaMask.shader";
    public const string Folder = "Library/ArenaOutsideMask";
    public const string Pulsar = "Assets/Scenes/S-5_PULSAR.unity";
    public const string LegacyField = "Assets/Prefabs/PLAYING FIELD.prefab";

    static ArenaOutsideMaskSetup() => EditorApplication.delayCall += () =>
    {
        if (File.Exists(Folder + "/pulsar.request") && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            File.Delete(Folder + "/pulsar.request");
            try { InstallPulsar(); ArenaOutsideMaskValidation.Run(); }
            catch (Exception e) { File.WriteAllText(Folder + "/pulsar-install.txt", "FAILED\n" + e); Debug.LogException(e); }
            return;
        }
        string request = Folder + "/install.request";
        if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(request);
        try { Install(); ArenaOutsideMaskValidation.Run(); }
        catch (Exception e) { File.WriteAllText(Folder + "/install.txt", "FAILED\n" + e); Debug.LogException(e); }
    };

    [MenuItem("MASSIVE/Levels/Install Shared Surface Outside Mask")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Install from Edit Mode.");
        Directory.CreateDirectory(Folder);
        foreach (string path in new[] { Surface, Bounds })
        {
            string backup = Folder + "/Before-" + Path.GetFileName(path);
            if (!File.Exists(backup)) File.Copy(path, backup);
        }
        var surface = PrefabUtility.LoadPrefabContents(Surface);
        try
        {
            var arena = surface.GetComponentInChildren<ArenaBoundsFromVectorGrid>(true);
            if (!arena || !arena.Grid) throw new InvalidOperationException("Surface has no live grid bounds.");
            var mask = arena.GetComponent<ArenaOutsideMask>();
            if (!mask) mask = arena.gameObject.AddComponent<ArenaOutsideMask>();
            var so = new SerializedObject(mask);
            so.FindProperty("arena").objectReferenceValue = arena;
            so.FindProperty("maskShader").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(surface, Surface);
        }
        finally { PrefabUtility.UnloadPrefabContents(surface); }
        var bounds = PrefabUtility.LoadPrefabContents(Bounds);
        try
        {
            var colliders = bounds.GetComponentsInChildren<Collider>(true).ToDictionary(c => c, c => EditorJsonUtility.ToJson(c));
            var strips = bounds.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name.StartsWith("mask plane ")).ToArray();
            if (strips.Length != 4) throw new InvalidOperationException("Expected the four legacy planar mask strips.");
            foreach (var strip in strips) strip.enabled = false;
            if (colliders.Any(c => EditorJsonUtility.ToJson(c.Key) != c.Value)) throw new InvalidOperationException("Collision geometry changed.");
            PrefabUtility.SaveAsPrefabAsset(bounds, Bounds);
        }
        finally { PrefabUtility.UnloadPrefabContents(bounds); }
        File.WriteAllText(Folder + "/install.txt", "INSTALLED\nSurface owns the generated exterior mask. Four legacy Bounds renderers disabled; colliders unchanged.\n");
        EditorApplication.QueuePlayerLoopUpdate(); SceneView.RepaintAll();
    }

    [MenuItem("MASSIVE/Levels/Install PULSAR Outside Mask")]
    public static void InstallPulsar()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != Pulsar || scene.isDirty)
            throw new InvalidOperationException("Open saved PULSAR in Edit Mode.");
        Directory.CreateDirectory(Folder);
        string backup = Folder + "/Before-S-5_PULSAR.unity";
        if (!File.Exists(backup)) File.Copy(Pulsar, backup);
        var components = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Component>(true)).Where(c => c).ToArray();
        var grid = components.OfType<VectorGridGPU>().Single();
        var arena = grid.GetComponent<ArenaBoundsFromVectorGrid>();
        if (!arena || arena.Grid != grid) throw new InvalidOperationException("PULSAR grid has no local arena bounds.");
        var strips = components.OfType<MeshRenderer>().Where(r => r.name.StartsWith("mask plane ")).ToArray();
        if (strips.Length != 4) throw new InvalidOperationException("Expected four PULSAR mask strips.");
        // Verify that existing geometry, gameplay components and bindings are preserved.
        var preserved = components.Where(c => c is Collider || c is MonoBehaviour && c is not ArenaOutsideMask ||
            c is Transform || c is UnityEngine.Playables.PlayableDirector).ToDictionary(c => c, c => EditorJsonUtility.ToJson(c));
        string prefabBefore = File.ReadAllText(LegacyField);
        Undo.IncrementCurrentGroup(); int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Install PULSAR shared outside mask");
        var mask = grid.GetComponent<ArenaOutsideMask>();
        if (!mask)
        {
            mask = Undo.AddComponent<ArenaOutsideMask>(grid.gameObject);
            var template = AssetDatabase.LoadAssetAtPath<GameObject>(Surface).GetComponentInChildren<ArenaOutsideMask>(true);
            EditorUtility.CopySerialized(template, mask);
        }
        var serialized = new SerializedObject(mask);
        serialized.FindProperty("arena").objectReferenceValue = arena;
        serialized.FindProperty("maskShader").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        serialized.ApplyModifiedProperties();
        foreach (var strip in strips)
        {
            Undo.RecordObject(strip, "Retire fixed PULSAR mask"); strip.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(strip);
        }
        if (preserved.Any(p => EditorJsonUtility.ToJson(p.Key) != p.Value) || File.ReadAllText(LegacyField) != prefabBefore)
        { Undo.RevertAllDownToGroup(undoGroup); throw new InvalidOperationException("PULSAR setup changed unrelated data."); }
        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = grid.gameObject;
        File.WriteAllText(Folder + "/pulsar-install.txt", "INSTALLED\nPULSAR uses the shared mask and Surface defaults with local bounds. Four fixed renderers retired. Existing colliders, transforms, gameplay components, rhythm bindings and PLAYING FIELD prefab preserved.\n");
        EditorApplication.QueuePlayerLoopUpdate(); SceneView.RepaintAll();
    }
}
#endif
