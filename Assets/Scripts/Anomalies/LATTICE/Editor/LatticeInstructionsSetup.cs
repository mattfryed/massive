#if UNITY_EDITOR
using System;
using System.IO;
using Massive.Demonstrations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Massive.Lattice.Editor
{
    public static class LatticeInstructionsSetup
    {
        public const string PanelPath = "Assets/Scripts/Anomalies/LATTICE/InstructionsPanel-LATTICE.prefab";
        [MenuItem("MASSIVE/LATTICE/Install instructions close-up")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play Mode before editing instructions.");
            Directory.CreateDirectory("Library/LatticeInstructions");
            if (!File.Exists("Library/LatticeInstructions/InstructionsPanel-before.prefab"))
                File.Copy(PanelPath, "Library/LatticeInstructions/InstructionsPanel-before.prefab");
            var panel = PrefabUtility.LoadPrefabContents(PanelPath);
            var sourceScene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/S-1_LATTICE.unity");
            try
            {
                var old = panel.transform.Find("Disruption movement demonstration");
                if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
                // Remove the inherited, inactive ORBITAL/MAGNETOSPHERE preview rig.
                var legacy = panel.transform.Find("Mid panel/Rect Transform");
                if (legacy) UnityEngine.Object.DestroyImmediate(legacy.gameObject);
                LatticeDisruptionField source = null;
                foreach (var root in sourceScene.GetRootGameObjects())
                    if (!source) source = root.GetComponentInChildren<LatticeDisruptionField>(true);
                if (!source) throw new InvalidOperationException("LATTICE gameplay field is missing.");
                var stage = new GameObject("Disruption movement demonstration");
                stage.SetActive(false);
                stage.transform.SetParent(panel.transform, false);
                stage.transform.localPosition = new Vector3(1000f, 0f, 1000f);
                var demo = stage.AddComponent<LatticeInstructionsDemo>();
                var gridObject = new GameObject("Gameplay grid and disruption");
                gridObject.transform.SetParent(stage.transform, false);
                gridObject.transform.localPosition = source.transform.position;
                gridObject.transform.localRotation = source.transform.rotation;
                gridObject.transform.localScale = source.transform.lossyScale;
                var grid = gridObject.AddComponent<VectorGridGPU>();
                EditorUtility.CopySerialized(source.GetComponent<VectorGridGPU>(), grid);
                grid.registerAsDefault = false;
                var field = gridObject.AddComponent<LatticeDisruptionField>();
                EditorUtility.CopySerialized(source, field);
                field.showNoiseBoundary = false;
                demo.field = field;
                demo.playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerActor.prefab");
                var cameraObject = new GameObject("Cropped gameplay camera");
                cameraObject.transform.SetParent(stage.transform, false);
                cameraObject.transform.localPosition = Vector3.up * 25f;
                cameraObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 2.65f;
                camera.nearClipPlane = .1f; camera.farClipPlane = 50f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.allowHDR = false; camera.allowMSAA = true;
                demo.viewport = cameraObject.AddComponent<PlayerDemoView>();
                demo.panelFrame = panel.transform.Find("Mid panel");
                var frame = new SerializedObject(demo.panelFrame.GetComponent<Shapes.Rectangle>());
                demo.viewportSize = new Vector2(frame.FindProperty("width").floatValue - .1f, frame.FindProperty("height").floatValue - .1f);
                stage.SetActive(true);
                PrefabUtility.SaveAsPrefabAsset(panel, PanelPath);
                Debug.Log("[LATTICE instructions] Installed full-scale grid, canonical player and cropped looping demonstration.");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(sourceScene);
                PrefabUtility.UnloadPrefabContents(panel);
            }
        }
    }
}
#endif
