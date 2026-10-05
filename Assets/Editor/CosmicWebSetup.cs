#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Massive.Cosmos;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CosmicWebSetup
{
    public const string Output = "Library/CosmosWeb";
    [MenuItem("MASSIVE/COSMOS/Cosmic web/Install background %#&F7")]
    public static void Install()
    {
        var scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != CosmosLayoutSetup.ScenePath)
            throw new InvalidOperationException("Open COSMOS in Edit Mode.");
        Directory.CreateDirectory(Output);
        if (!File.Exists(Output + "/BeforeWeb.unity")) File.Copy(scene.path, Output + "/BeforeWeb.unity");
        var poses = CosmosLayoutSetup.All<Transform>().ToDictionary(t => t, t => (t.localPosition, t.localRotation, t.localScale));
        var cameras = CosmosLayoutSetup.All<Camera>().ToDictionary(c => c, c => EditorJsonUtility.ToJson(c));
        var grid = CosmosLayoutSetup.All<CosmosArenaLayout>().Single();
        var bounds = grid.GetComponent<ArenaBoundsFromVectorGrid>();
        Vector2 size = bounds.Grid.size;
        var web = grid.GetComponent<CosmicWebBackground>();
        if (!web) web = Undo.AddComponent<CosmicWebBackground>(grid.gameObject);
        Undo.RecordObject(web, "Set up COSMOS cosmic web");
        web.match = CosmosLayoutSetup.All<GameManagerScript>().Single();
        web.particleShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Scripts/Anomalies/COSMOS/CosmicWebParticles.shader");
        web.Rebuild();
        foreach (var entry in poses)
            if (entry.Value != (entry.Key.localPosition, entry.Key.localRotation, entry.Key.localScale))
                throw new InvalidOperationException("Existing pose changed: " + entry.Key.name);
        foreach (var entry in cameras)
            if (entry.Value != EditorJsonUtility.ToJson(entry.Key)) throw new InvalidOperationException("Camera changed.");
        if (bounds.Grid.size != size) throw new InvalidOperationException("User oval dimensions changed.");
        EditorUtility.SetDirty(web);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
        File.WriteAllText(Output + "/preservation.txt", $"PASS {poses.Count} transforms, {cameras.Count} cameras and user grid size {size} unchanged.\n");
        Selection.activeGameObject = grid.gameObject;
        UnityEditorInternal.InternalEditorUtility.SetIsInspectorExpanded(web, true);
        EditorApplication.QueuePlayerLoopUpdate(); SceneView.RepaintAll();
        Debug.Log($"COSMOS cosmic web installed: {web.RenderedParticleCount} particles, {web.FilamentCount} filaments, {web.padding} world-unit inset.");
    }
}
#endif
