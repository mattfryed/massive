#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Massive.Singularity.Editor
{
    /// <summary>Targeted instructions migration. Reads saved gameplay visuals; never saves a gameplay scene.</summary>
    public static class SingularityInstructionsSetup
    {
        public const string PanelPath = "Assets/Scripts/Anomalies/SINGULARITY/InstructionsPanel-SINGULARITY.prefab";
        private const string DefinitionPath = "Assets/Scripts/Level Select/LevelDefinition-SINGULARITY.asset";
        private const string DemoName = "Black hole gameplay demonstration";

        [MenuItem("MASSIVE/SINGULARITY/Refresh Instructions Demonstration")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Refresh instructions in Edit Mode after compilation.");
            var definition = AssetDatabase.LoadAssetAtPath<LevelDefinition>(DefinitionPath);
            if (!definition || !definition.iconPrefab) throw new InvalidOperationException("Missing SINGULARITY definition/icon.");
            string scenePath = AssetDatabase.FindAssets("t:Scene " + definition.SceneName)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Single(p => System.IO.Path.GetFileNameWithoutExtension(p) == definition.SceneName);
            var panel = PrefabUtility.LoadPrefabContents(PanelPath);
            Scene source = default;
            try
            {
                source = EditorSceneManager.OpenPreviewScene(scenePath);
                var sourceSurface = All<SingularitySurface>(source).Single(s => s.gameObject.activeInHierarchy);
                var sourceGrid = All<SingularityGridRenderer>(source).Single(g => g.gameObject.activeInHierarchy);
                var sourcePortal = All<SingularityBlackHolePortal>(source).Single(p => p.gameObject.activeInHierarchy);
                var sourceCamera = All<Camera>(source).Single(c => c.CompareTag("MainCamera"));
                var mid = panel.transform.Find("Mid panel");
                if (!mid) throw new InvalidOperationException("Missing middle preview frame.");

                var icon = panel.GetComponentsInChildren<LevelIcon>(true).SingleOrDefault();
                if (!icon) icon = ((GameObject)PrefabUtility.InstantiatePrefab(definition.iconPrefab, panel.transform)).GetComponent<LevelIcon>();
                icon.name = "SINGULARITY Carousel Icon";
                icon.transform.localPosition = new Vector3(0f, 5f, 0f);
                icon.transform.localRotation = Quaternion.identity;
                icon.transform.localScale = Vector3.one * 5.8f;
                PrefabUtility.RecordPrefabInstancePropertyModifications(icon.gameObject);
                PrefabUtility.RecordPrefabInstancePropertyModifications(icon.transform);
                // Local scaling deliberately ignores parent scale. Preserve the authored
                // orbital motion, but scale its particles with this larger instructions icon.
                // Read the source each time so refreshing cannot compound the adjustment.
                float iconScale = icon.transform.localScale.x / definition.iconPrefab.transform.localScale.x;
                foreach (var particles in icon.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (particles.main.scalingMode != ParticleSystemScalingMode.Local) continue;
                    var authored = PrefabUtility.GetCorrespondingObjectFromSource(particles.transform);
                    if (!authored) continue;
                    particles.transform.localScale = authored.localScale * iconScale;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(particles.transform);
                }

                var old = panel.transform.Find(DemoName);
                if (old) Object.DestroyImmediate(old.gameObject);
                var stage = Child(panel.transform, DemoName);
                stage.transform.localPosition = new Vector3(1000f, 0f, 1000f);
                var demo = stage.AddComponent<SingularityInstructionsDemo>();
                var surfaceObject = Child(stage.transform, "Gameplay folded surface");
                CopyPose(sourceSurface.transform, surfaceObject.transform);
                demo.surface = surfaceObject.AddComponent<SingularitySurface>();
                EditorUtility.CopySerialized(sourceSurface, demo.surface);
                var gridObject = Child(surfaceObject.transform, "Gameplay grid close-up");
                demo.grid = gridObject.AddComponent<SingularityGridRenderer>();
                EditorUtility.CopySerialized(sourceGrid, demo.grid);
                demo.grid.surface = demo.surface; demo.grid.player = null;
                demo.grid.amplifierTreatments = null; demo.grid.enabled = false;

                var hole = Child(stage.transform, "Black hole face passage");
                CopyPose(sourcePortal.transform, hole.transform);
                demo.portal = hole.AddComponent<SingularityBlackHolePortal>();
                EditorUtility.CopySerialized(sourcePortal, demo.portal);
                demo.portal.surface = demo.surface;
                demo.portal.players = Array.Empty<SingularityPlayerAdapter>();
                demo.portal.enabled = false;
                demo.grid.blackHole = demo.portal;
                var originalVisual = sourcePortal.transform.Find("Black Hole - Visual");
                if (!originalVisual) throw new InvalidOperationException("Missing gameplay black-hole visual.");
                var visual = Object.Instantiate(originalVisual.gameObject, hole.transform, false);
                visual.name = "Gameplay black-hole visuals";
                foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                foreach (var behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true))
                    if (!(behaviour is MirzaBeig.ParticleSystems.Rotator)) Object.DestroyImmediate(behaviour);
                demo.feedback = hole.AddComponent<SingularityBlackHoleFeedback>();
                var sourceFeedback = sourcePortal.GetComponent<SingularityBlackHoleFeedback>();
                if (sourceFeedback) EditorUtility.CopySerialized(sourceFeedback, demo.feedback);
                demo.feedback.portal = demo.portal; demo.feedback.grid = demo.grid;
                demo.feedback.enableHaptics = false; demo.feedback.enabled = false;

                var cameraObject = Child(stage.transform, "Cropped gameplay camera");
                cameraObject.transform.localPosition = sourceCamera.transform.position;
                cameraObject.transform.localRotation = sourceCamera.transform.rotation;
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true; camera.orthographicSize = 3.1f;
                camera.nearClipPlane = .1f; camera.farClipPlane = 70f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.allowHDR = sourceCamera.allowHDR; camera.allowMSAA = true;
                camera.enabled = false;
                demo.previewCamera = camera;
                demo.previewShader = AssetDatabase.LoadAssetAtPath<Shader>(
                    "Assets/Scripts/Anomalies/SINGULARITY/SingularityInstructionsPreview.shader");
                if (!demo.previewShader) throw new InvalidOperationException("Missing instructions preview shader.");
                demo.feedback.renderCamera = camera;
                demo.panelFrame = mid;
                var frame = new SerializedObject(mid.GetComponent<Shapes.Rectangle>());
                demo.viewportSize = new Vector2(frame.FindProperty("width").floatValue - .1f,
                    frame.FindProperty("height").floatValue - .1f);
                demo.playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerActor.prefab");
                if (!demo.playerPrefab) throw new InvalidOperationException("Missing canonical player prefab.");
                demo.playerPlaneHeight = sourcePortal.players.Where(p => p).First().transform.position.y;
                PrefabUtility.SaveAsPrefabAsset(panel, PanelPath);
                Debug.Log("SINGULARITY instructions: centered icon and cropped, isolated gameplay portal loop installed. Gameplay scenes unchanged.");
            }
            finally
            {
                if (source.IsValid()) EditorSceneManager.ClosePreviewScene(source);
                PrefabUtility.UnloadPrefabContents(panel);
            }
        }

        private static GameObject Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, parent.gameObject.scene);
            go.transform.SetParent(parent, false);
            return go;
        }
        private static void CopyPose(Transform source, Transform destination)
        {
            destination.localPosition = source.position;
            destination.localRotation = source.rotation;
            destination.localScale = source.lossyScale;
        }
        private static T[] All<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();
    }
}
#endif
