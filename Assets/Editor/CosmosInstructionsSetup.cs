#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Massive.Cosmos;
using Massive.Demonstrations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class CosmosInstructionsSetup
{
    public const string PanelPath = "Assets/Scripts/Anomalies/COSMOS/InstructionsPanel-COSMOS.prefab";
    public const string GameplayScene = "Assets/Scenes/S-9_COSMOS.unity";
    public const string Folder = "Library/CosmosInstructionsValidation";
    static CosmosInstructionsSetup() => EditorApplication.delayCall += () =>
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Folder + "/apply.request")) return;
        File.Delete(Folder + "/apply.request");
        try { Apply(); CosmosInstructionsValidation.Run(); }
        catch (Exception e) { File.WriteAllText(Folder + "/setup-error.txt", e.ToString()); Debug.LogException(e); }
    };

    [MenuItem("MASSIVE/COSMOS/Instructions/Build Supernova Close-up")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play Mode first.");
        Directory.CreateDirectory(Folder);
        if (!File.Exists(Folder + "/Original.prefab")) File.Copy(PanelPath, Folder + "/Original.prefab");
        var panel = PrefabUtility.LoadPrefabContents(PanelPath);
        var sourceScene = EditorSceneManager.OpenPreviewScene(GameplayScene);
        try
        {
            var roots = sourceScene.GetRootGameObjects();
            var sourceWeb = roots.SelectMany(r => r.GetComponentsInChildren<CosmicWebBackground>(true)).Single();
            var sourceGrid = sourceWeb.GetComponent<VectorGridGPU>();
            var sourceBounds = sourceWeb.GetComponent<ArenaBoundsFromVectorGrid>();
            var sourceCamera = roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)).Single(c => c.CompareTag("MainCamera"));
            var frame = panel.transform.Find("Mid panel");
            if (!frame) throw new InvalidOperationException("COSMOS middle instruction pane is missing.");
            var legacy = frame.Find("Rect Transform");
            if (legacy) Object.DestroyImmediate(legacy.gameObject);
            var previous = panel.transform.Find("Supernova demonstration");
            if (previous) Object.DestroyImmediate(previous.gameObject);
            var stage = new GameObject("Supernova demonstration");
            stage.SetActive(false); stage.transform.SetParent(panel.transform, false);
            stage.transform.localPosition = new Vector3(0, 0, 1800);
            var demo = stage.AddComponent<CosmosInstructionsDemo>();
            demo.panelFrame = frame;

            var gridObject = new GameObject("Gameplay COSMOS grid and web");
            gridObject.transform.SetParent(stage.transform, false);
            gridObject.transform.localPosition = sourceGrid.transform.position;
            gridObject.transform.localRotation = sourceGrid.transform.rotation;
            gridObject.transform.localScale = sourceGrid.transform.lossyScale;
            var grid = gridObject.AddComponent<VectorGridGPU>();
            EditorUtility.CopySerialized(sourceGrid, grid); grid.registerAsDefault = false;
            var bounds = gridObject.AddComponent<ArenaBoundsFromVectorGrid>();
            EditorUtility.CopySerialized(sourceBounds, bounds);
            var boundsData = new SerializedObject(bounds);
            boundsData.FindProperty("grid").objectReferenceValue = grid;
            boundsData.ApplyModifiedPropertiesWithoutUndo();
            demo.web = gridObject.AddComponent<CosmicWebBackground>();
            EditorUtility.CopySerialized(sourceWeb, demo.web);
            demo.web.match = null;

            var cameraObject = new GameObject("Supernova close-up camera");
            cameraObject.transform.SetParent(stage.transform, false);
            cameraObject.transform.localPosition = new Vector3(0, 25, 0);
            cameraObject.transform.localRotation = sourceCamera.transform.rotation;
            var camera = cameraObject.AddComponent<Camera>();
            EditorUtility.CopySerialized(sourceCamera, camera);
            camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 3.8f;
            camera.nearClipPlane = .1f; camera.farClipPlane = 50;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.targetTexture = null; camera.rect = new Rect(0, 0, 1, 1);
            demo.viewport = cameraObject.AddComponent<PlayerDemoView>();

            foreach (var source in sourceWeb.GetComponents<CosmicSupernovae>())
            {
                var layer = gridObject.AddComponent<CosmicSupernovae>();
                EditorUtility.CopySerialized(source, layer);
                layer.web = demo.web; layer.presentationCamera = camera;
                var data = new SerializedObject(layer);
                data.FindProperty("presentationOnly").boolValue = true;
                data.ApplyModifiedPropertiesWithoutUndo();
                if (layer.kind == CosmicSupernovae.SupernovaKind.Normal) demo.normal = layer;
                else demo.superluminous = layer;
            }
            if (!demo.normal || !demo.superluminous) throw new InvalidOperationException("Gameplay must supply both supernova layers.");
            stage.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(panel, PanelPath);
            File.WriteAllText(Folder + "/setup.txt", "Production web, grid, cameras' render settings and both supernova components copied from saved COSMOS.\n" +
                "Only the middle pane's legacy demonstration is replaced. Normal and superluminous use their actual pickup prefabs.\n" +
                "Representative age 0.5; normal repeats twice per superluminous cycle; all visual durations and sizes are gameplay values.\n");
        }
        finally { EditorSceneManager.ClosePreviewScene(sourceScene); PrefabUtility.UnloadPrefabContents(panel); }
    }
}
#endif
