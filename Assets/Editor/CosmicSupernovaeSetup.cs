#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Massive.Cosmos;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CosmicSupernovaeSetup
{
    public const string Output = "Library/CosmosSupernovae";
    [MenuItem("MASSIVE/COSMOS/Supernovae/Apply cloud and oval mask revision %#&F4")]
    public static void ApplyCloudRevision()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != CosmosLayoutSetup.ScenePath)
            throw new InvalidOperationException("Open COSMOS in Edit Mode.");
        Directory.CreateDirectory(Output);
        if (!File.Exists(Output + "/BeforeCloudRevision.unity")) File.Copy(scene.path, Output + "/BeforeCloudRevision.unity");
        var layout = CosmosLayoutSetup.All<CosmosArenaLayout>().Single();
        var web = layout.GetComponent<CosmicWebBackground>();
        string webState = EditorJsonUtility.ToJson(web);
        var poses = CosmosLayoutSetup.All<Transform>().Where(t => (t.gameObject.hideFlags & HideFlags.DontSave) == 0)
            .ToDictionary(t => t, t => (t.localPosition, t.localRotation, t.localScale));
        var cameras = CosmosLayoutSetup.All<Camera>().ToDictionary(c => c, c => EditorJsonUtility.ToJson(c));
        Undo.RecordObject(layout, "Add continuous oval mask");
        layout.outsideMaskShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Scripts/Anomalies/COSMOS/CosmosArenaMask.shader");
        if (!layout.outsideMaskShader) throw new InvalidOperationException("COSMOS mask shader is missing.");
        layout.maskOutsideOval = true; layout.Rebuild(); EditorUtility.SetDirty(layout);
        // New cloud fields retain their initialized values or any subsequent user tuning.
        foreach (var layer in layout.GetComponents<CosmicSupernovae>()) EditorUtility.SetDirty(layer);
        if (EditorJsonUtility.ToJson(web) != webState || poses.Any(p => p.Value != (p.Key.localPosition,p.Key.localRotation,p.Key.localScale)) ||
            cameras.Any(c => c.Value != EditorJsonUtility.ToJson(c.Key))) throw new InvalidOperationException("Authored web, pose or camera changed.");
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = layout.gameObject;
        File.WriteAllText(Output + "/cloud-installation.txt", $"PASS {poses.Count} authored poses, web and {cameras.Count} cameras preserved. Continuous black oval mask installed; cloud controls saved.\n");
        Debug.Log("COSMOS cloud and continuous oval mask revision saved.");
    }
    [MenuItem("MASSIVE/COSMOS/Supernovae/Install superluminous layer %#&F6")]
    public static void Install()
    {
        var scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != CosmosLayoutSetup.ScenePath)
            throw new InvalidOperationException("Open COSMOS in Edit Mode.");
        Directory.CreateDirectory(Output);
        if (!File.Exists(Output + "/BeforeSuperluminous.unity")) File.Copy(scene.path, Output + "/BeforeSuperluminous.unity");
        var normal = CosmosLayoutSetup.All<CosmicSupernovae>().Single(s => s.kind == CosmicSupernovae.SupernovaKind.Normal);
        var webState = EditorJsonUtility.ToJson(normal.web);
        var normalState = EditorJsonUtility.ToJson(normal);
        var poses = CosmosLayoutSetup.All<Transform>().ToDictionary(t => t, t => (t.localPosition, t.localRotation, t.localScale));
        var cameras = CosmosLayoutSetup.All<Camera>().ToDictionary(c => c, c => EditorJsonUtility.ToJson(c));
        var super = normal.GetComponents<CosmicSupernovae>().SingleOrDefault(s => s.kind == CosmicSupernovae.SupernovaKind.Superluminous);
        if (!super)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Scripts/Anomalies/ORBITAL/Orbital Mass Nugget.prefab");
            if (!prefab || !prefab.TryGetComponent<MatterNuggetScript>(out var nugget)) throw new InvalidOperationException("Full mass nugget prefab is missing.");
            super = Undo.AddComponent<CosmicSupernovae>(normal.gameObject);
            EditorUtility.CopySerialized(normal, super);
            super.kind = CosmicSupernovae.SupernovaKind.Superluminous;
            super.nuggletPrefab = nugget;
            var keys = normal.frequencyOverAge.keys;
            for (int i = 0; i < keys.Length; i++)
            { keys[i].value *= .5f; keys[i].inTangent *= .5f; keys[i].outTangent *= .5f; }
            super.frequencyOverAge = new AnimationCurve(keys) { preWrapMode = normal.frequencyOverAge.preWrapMode, postWrapMode = normal.frequencyOverAge.postWrapMode };
            super.randomSeed = normal.randomSeed + 101;
            super.randomizeColor = false;
            super.flashColor = new Color(.12f, .55f, 1);
            super.flashRadius = normal.flashRadius * 2.4f;
            super.flashBrightness = normal.flashBrightness * 2;
            super.flashSeconds = normal.flashSeconds * 1.625f;
            super.dieOffSeconds = Mathf.Max(normal.dieOffSeconds * 2.5f, .75f);
            EditorUtility.SetDirty(super);
        }
        if (EditorJsonUtility.ToJson(normal.web) != webState || EditorJsonUtility.ToJson(normal) != normalState)
            throw new InvalidOperationException("Existing web or normal supernova settings changed.");
        foreach (var entry in poses)
            if (entry.Value != (entry.Key.localPosition, entry.Key.localRotation, entry.Key.localScale))
                throw new InvalidOperationException("Existing pose changed: " + entry.Key.name);
        foreach (var entry in cameras)
            if (entry.Value != EditorJsonUtility.ToJson(entry.Key)) throw new InvalidOperationException("Camera changed.");
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        File.WriteAllText(Output + "/installation.txt", $"PASS {poses.Count} existing transforms, {cameras.Count} cameras, web and normal supernova settings preserved.\nNormal midpoint rate: {normal.FrequencyAtAge(.5f)}; superluminous: {super.FrequencyAtAge(.5f)}.\n");
        Selection.activeGameObject = normal.gameObject;
        UnityEditorInternal.InternalEditorUtility.SetIsInspectorExpanded(super, true);
        Debug.Log("COSMOS superluminous layer installed; independent half-height curve and full mass nuggets.");
    }
}
#endif
