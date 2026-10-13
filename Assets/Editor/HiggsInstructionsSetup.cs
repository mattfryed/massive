#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Massive.Demonstrations;
using Massive.Scoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HiggsInstructionsSetup
{
    public const string PanelPath = "Assets/Scripts/Instructions/InstructionsPanel-HIGGS.prefab";
    public const string Folder = "Library/HiggsInstructionsValidation";
    public const string GameplayScene = "Assets/Scenes/S-3_HIGGS.unity";
    static HiggsInstructionsSetup() => EditorApplication.delayCall += () =>
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Folder+"/apply.request")) return;
        File.Delete(Folder+"/apply.request");
        try { Apply(); HiggsInstructionsValidation.Run(); }
        catch(Exception e) { File.WriteAllText(Folder+"/setup-error.txt", e.ToString()); Debug.LogException(e); }
    };

    [MenuItem("MASSIVE/HIGGS/Build Knot Instructions Close-up")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play Mode first.");
        var panel = PrefabUtility.LoadPrefabContents(PanelPath);
        var sourceScene = EditorSceneManager.OpenPreviewScene(GameplayScene);
        try
        {
            var sourceGrid = sourceScene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<VectorGridGPU>(true)).Single();
            var sourceField = sourceScene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<HiggsFieldGPU>(true)).Single();
            var sourceGoal = sourceScene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SymmetryKnotGoalMouth>(true)).Single(g => g.teamID == 1);
            var frame = panel.transform.Find("Mid panel");
            if(!frame) throw new InvalidOperationException("HIGGS middle instruction pane is missing.");
            var legacy = frame.Find("Rect Transform");
            if(legacy) Object.DestroyImmediate(legacy.gameObject);
            var previous = panel.transform.Find("Knot possession demonstration");
            if(previous) Object.DestroyImmediate(previous.gameObject);
            var stage = new GameObject("Knot possession demonstration");
            stage.SetActive(false);
            stage.transform.SetParent(panel.transform,false);
            // Preserve world X for the knot's left/right goal orientation rule.
            stage.transform.localPosition = new Vector3(0,0,1200);
            var demo = stage.AddComponent<HiggsInstructionsDemo>();
            demo.panelFrame = frame;
            demo.knotRules = AssetDatabase.LoadAssetAtPath<GameObject>(HiggsStageSetup.KnotPath).GetComponent<SymmetryKnotController>();
            demo.economy = AssetDatabase.LoadAssetAtPath<ScoreEconomyProfile>("Assets/Scripts/Scoring/ScoreEconomyProfile.asset");
            demo.playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerActor.prefab");

            var gridObject = CopyPose("Gameplay grid", sourceGrid.transform, stage.transform);
            demo.grid = gridObject.AddComponent<VectorGridGPU>();
            EditorUtility.CopySerialized(sourceGrid, demo.grid);
            demo.grid.registerAsDefault = false;

            var fieldObject = CopyPose("Gameplay Higgs field", sourceField.transform, stage.transform);
            fieldObject.AddComponent<MeshFilter>().sharedMesh = sourceField.GetComponent<MeshFilter>().sharedMesh;
            var renderer = fieldObject.AddComponent<MeshRenderer>();
            EditorUtility.CopySerialized(sourceField.GetComponent<MeshRenderer>(), renderer);
            demo.field = fieldObject.AddComponent<HiggsFieldGPU>();
            EditorUtility.CopySerialized(sourceField, demo.field);
            var fieldData = new SerializedObject(demo.field);
            fieldData.FindProperty("targetRenderer").objectReferenceValue = renderer;
            fieldData.ApplyModifiedPropertiesWithoutUndo();

            var cameraObject = new GameObject("Knot close-up camera");
            cameraObject.transform.SetParent(stage.transform,false);
            cameraObject.transform.localPosition = new Vector3(.4f,25,.35f);
            cameraObject.transform.localRotation = Quaternion.Euler(90,0,0);
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled=false; camera.orthographic=true; camera.orthographicSize=2.65f;
            camera.nearClipPlane=.1f; camera.farClipPlane=50;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black;
            camera.allowHDR=false; camera.allowMSAA=true;
            demo.viewport=cameraObject.AddComponent<PlayerDemoView>();

            var knotObject = Object.Instantiate(demo.knotRules.gameObject,stage.transform);
            knotObject.name="Knot visual template";
            knotObject.SetActive(false);
            knotObject.transform.localPosition=new Vector3(.65f,0,-.15f);
            Object.DestroyImmediate(knotObject.GetComponent<SymmetryKnotController>());
            foreach(var collider in knotObject.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            demo.knotTemplate=knotObject.GetComponent<SymmetryKnotVisual>();
            var goal = new GameObject("Light goal direction");
            goal.transform.SetParent(stage.transform,false);
            goal.transform.localPosition=sourceGoal.FlowAnchor.position;
            demo.goalAnchor=goal.transform;

            var toastAsset=(SymmetryKnotIncomeToast)new SerializedObject(demo.knotRules).FindProperty("incomeToastPrefab").objectReferenceValue;
            demo.incomeToast=Object.Instantiate(toastAsset,stage.transform);
            demo.incomeToast.name="Capture and income toast";
            stage.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(panel,PanelPath);
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Folder+"/setup.txt",
                "Copied HIGGS grid spacing: "+sourceGrid.gridScale+"; size: "+sourceGrid.size+
                "\nHiggs field world size: "+sourceField.WorldSizeXZ+
                "\nKnot radius: "+demo.knotRules.CaptureRadius+
                "\nCanonical PlayerActor and gameplay toast, no score footer.\n");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(sourceScene);
            PrefabUtility.UnloadPrefabContents(panel);
        }
    }

    static GameObject CopyPose(string name, Transform source, Transform parent)
    {
        var result = new GameObject(name);
        result.transform.SetParent(parent, false);
        result.transform.localPosition = source.position;
        result.transform.localRotation = source.rotation;
        result.transform.localScale = source.lossyScale;
        return result;
    }
}
#endif
