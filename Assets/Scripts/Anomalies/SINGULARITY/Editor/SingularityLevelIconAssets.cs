using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Massive.Singularity.Editor
{
    /// <summary>Creates only missing, standalone icon assets. Does not register a
    /// level, open/save gameplay scenes or modify the existing carousel.</summary>
    public static class SingularityLevelIconAssets
    {
        public const string Folder = "Assets/Scripts/Anomalies/SINGULARITY";
        public const string PrefabPath = Folder + "/LS_SINGULARITY Icon.prefab";

        [MenuItem("MASSIVE/SINGULARITY/Create Missing Level Icon")]
        public static void CreateMissing()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) return;
            Shader shader = Shader.Find("MASSIVE/Singularity/Level Icon");
            Shader opaque = Shader.Find("Unlit/Color");
            if (shader == null || opaque == null) throw new InvalidOperationException("Icon shaders are not imported yet.");
            Material grid = LoadOrCreate("Icon Lattice.mat", shader);
            string horizonPath = Folder + "/Icon Horizon.mat";
            Material horizon = AssetDatabase.LoadAssetAtPath<Material>(horizonPath);
            if (horizon == null)
            {
                horizon = new Material(opaque) { name = "SINGULARITY Icon Horizon", color = Color.black };
                AssetDatabase.CreateAsset(horizon, horizonPath);
            }
            var preview = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                root = new GameObject("LS_SINGULARITY Icon");
                SceneManager.MoveGameObjectToScene(root, preview);
                var levelIcon = root.AddComponent<LevelIcon>();
                var visual = new GameObject("Folded Grid and Central Well");
                SceneManager.MoveGameObjectToScene(visual, preview);
                visual.transform.SetParent(root.transform, false);
                visual.transform.localScale = Vector3.one * .25f;
                visual.transform.localRotation = Quaternion.Euler(58f, -18f, -8f);
                var icon = visual.AddComponent<SingularityLevelIcon>();
                var renderer = visual.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = grid;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                SceneManager.MoveGameObjectToScene(sphere, preview);
                sphere.name = "Black Hole - Event Horizon";
                sphere.transform.SetParent(visual.transform, false);
                sphere.transform.localScale = Vector3.one * .84f;
                UnityEngine.Object.DestroyImmediate(sphere.GetComponent<Collider>());
                var sphereRenderer = sphere.GetComponent<MeshRenderer>();
                sphereRenderer.sharedMaterial = horizon;
                sphereRenderer.shadowCastingMode = ShadowCastingMode.Off;
                sphereRenderer.receiveShadows = false;
                var serialized = new SerializedObject(levelIcon);
                serialized.FindProperty("visualsRoot").objectReferenceValue = visual.transform;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                // Derivation occurs on enable in instances; do not persist a
                // transient mesh into the prefab or overwrite a user's existing icon.
                icon.enabled = false;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
                try
                {
                    contents.GetComponentInChildren<SingularityLevelIcon>(true).enabled = true;
                    PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(preview);
            }
            Debug.Log("Created standalone SINGULARITY icon. Carousel/catalog and gameplay scenes unchanged.");
        }

        private static Material LoadOrCreate(string name, Shader shader)
        {
            string path = Folder + "/" + name;
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(shader) { name = "SINGULARITY " + name.Replace(".mat", "") };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>Native isolated preview; never changes the user's scene or camera.</summary>
        public static string Capture(string absolutePath, bool carouselOrientation = false)
        {
            var preview = EditorSceneManager.NewPreviewScene();
            RenderTexture rt = null;
            Texture2D image = null;
            RenderTexture oldActive = RenderTexture.active;
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (prefab == null) throw new InvalidOperationException("Create the icon first.");
                var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                if (carouselOrientation) root.transform.rotation = Quaternion.Euler(0, 0, 180);
                root.GetComponentInChildren<SingularityLevelIcon>().Rebuild();
                // Prewarm only this isolated preview copy. Never modify or
                // simulate the user's live scene or the authored particle asset.
                foreach (var particles in root.GetComponentsInChildren<ParticleSystem>(true))
                    particles.Simulate(2f, false, true, true);
                var cameraObject = new GameObject("Icon Preview Camera");
                SceneManager.MoveGameObjectToScene(cameraObject, preview);
                var camera = cameraObject.AddComponent<Camera>();
                camera.scene = preview;
                camera.transform.position = new Vector3(0, 5, 0);
                camera.transform.rotation = Quaternion.Euler(90, 0, 0);
                camera.orthographic = true; camera.orthographicSize = .56f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.allowHDR = false;
                rt = new RenderTexture(1200, 900, 24) { antiAliasing = 4 };
                camera.targetTexture = rt;
                camera.Render(); RenderTexture.active = rt;
                image = new Texture2D(1200, 900, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1200, 900), 0, 0); image.Apply();
                System.IO.File.WriteAllBytes(absolutePath, image.EncodeToPNG());
                return absolutePath;
            }
            finally
            {
                RenderTexture.active = oldActive;
                EditorSceneManager.ClosePreviewScene(preview);
                if (rt != null) UnityEngine.Object.DestroyImmediate(rt);
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
            }
        }
    }
}
