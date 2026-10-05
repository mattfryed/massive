using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Massive.Cosmos.Editor
{
    public static class CosmicWebLevelIconAssets
    {
        public const string Folder = "Assets/Scripts/Anomalies/COSMOS";
        public const string PrefabPath = Folder + "/LS_COSMOS Icon.prefab";
        const string DataPath = Folder + "/Cosmic Web Icon Data.asset";

        [MenuItem("MASSIVE/COSMOS/Create and Preview Level Icon")]
        public static void CreateAndPreview() { CreateMissing(); CosmicWebLevelIconPreview.Open(); }

        public static void CreateMissing()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (!prefab)
            {
                var shader = Shader.Find("MASSIVE/Cosmos/Level Icon");
                if (!shader) throw new InvalidOperationException("COSMOS icon shader has not imported.");
                var source = UnityEngine.Object.FindFirstObjectByType<CosmicWebBackground>();
                var data = AssetDatabase.LoadAssetAtPath<CosmicWebIconData>(DataPath);
                if (!data)
                {
                    data = ScriptableObject.CreateInstance<CosmicWebIconData>();
                    data.seed = source ? source.seed : 90210;
                    data.organicStrength = source ? source.organicStrength : 1;
                    data.particles = CosmicWebTopology.Build(8000, data.seed, out _, data.organicStrength);
                    AssetDatabase.CreateAsset(data, DataPath);
                }
                string materialPath = Folder + "/Cosmic Web Icon.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (!material)
                {
                    material = new Material(shader) { name = "COSMOS Icon" };
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                Scene scene = EditorSceneManager.NewPreviewScene();
                try
                {
                    var root = new GameObject("LS_COSMOS Icon"); SceneManager.MoveGameObjectToScene(root, scene);
                    var selection = root.AddComponent<LevelIcon>();
                    var visual = new GameObject("Cosmic Web Cloud"); SceneManager.MoveGameObjectToScene(visual, scene);
                    visual.transform.SetParent(root.transform, false);
                    var icon = visual.AddComponent<CosmicWebLevelIcon>(); icon.webData = data;
                    if (source)
                    {
                        icon.filamentColor = source.filamentColor; icon.clusterColor = source.clusterColor;
                        icon.brightness = source.brightness; icon.sizeVariance = source.brightnessVariance;
                        icon.evolutionVariation = source.evolutionVariation; icon.driftStrength = source.driftStrength;
                        icon.clusterTurbulence = source.clusterTurbulence; icon.turbulenceStrength = source.turbulenceStrength;
                    }
                    var renderer = visual.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                    renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    var so = new SerializedObject(selection);
                    so.FindProperty("visualsRoot").objectReferenceValue = visual.transform; so.ApplyModifiedPropertiesWithoutUndo();
                    prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            var definition = AssetDatabase.LoadAssetAtPath<LevelDefinition>(Folder + "/LevelDefinition-COSMOS.asset");
            if (definition && definition.iconPrefab != prefab)
            {
                Undo.RecordObject(definition, "Assign COSMOS level icon"); definition.iconPrefab = prefab;
                EditorUtility.SetDirty(definition); AssetDatabase.SaveAssetIfDirty(definition);
            }
        }
    }

    [CustomEditor(typeof(CosmicWebLevelIcon)), CanEditMultipleObjects]
    public sealed class CosmicWebLevelIconEditor : UnityEditor.Editor
    {
        double nextFrame;
        void OnEnable() { EditorApplication.update += Tick; }
        void OnDisable() { EditorApplication.update -= Tick; }
        void Tick()
        {
            if (Application.isPlaying || EditorApplication.timeSinceStartup < nextFrame) return;
            var icon = target as CosmicWebLevelIcon;
            if (!icon || !icon.isActiveAndEnabled || !icon.animateInEditor || !icon.animate) return;
            nextFrame = EditorApplication.timeSinceStartup + 1.0 / 30;
            EditorApplication.QueuePlayerLoopUpdate(); SceneView.RepaintAll(); Repaint();
        }
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Age stays at 0.5. Motion Speed controls the independent flow clock. Turn tumbles the complete 3D ovoid and can be disabled independently of internal motion. Solid unlit dots use Size Variance for texture and Brightness for emission. The icon uses one draw, baked topology and no per-frame particle uploads.", MessageType.Info);
            if (GUILayout.Button("Open Animated Icon Preview")) CosmicWebLevelIconPreview.Open();
        }
    }

    public sealed class CosmicWebLevelIconPreview : EditorWindow
    {
        Scene scene;
        Camera previewCamera;
        CosmicWebLevelIcon icon;
        RenderTexture texture;
        double previousTime;
        bool playing = true;
        public static void Open()
        {
            var window = GetWindow<CosmicWebLevelIconPreview>("COSMOS Icon");
            window.minSize = new Vector2(520, 540); window.Show();
        }
        void OnEnable() { EditorApplication.update += Tick; previousTime = EditorApplication.timeSinceStartup; }
        void OnDisable() { EditorApplication.update -= Tick; ReleasePreview(); }
        void OnProjectChange() { ReleasePreview(); }
        void ReleasePreview()
        {
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            if (texture) { texture.Release(); DestroyImmediate(texture); }
            scene = default; previewCamera = null; icon = null; texture = null;
        }
        void EnsurePreview()
        {
            if (icon) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CosmicWebLevelIconAssets.PrefabPath);
            if (!prefab) return;
            scene = EditorSceneManager.NewPreviewScene();
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            icon = root.GetComponentInChildren<CosmicWebLevelIcon>(); icon.animateInEditor = false; icon.Advance(0);
            var go = new GameObject("COSMOS Preview Camera"); SceneManager.MoveGameObjectToScene(go, scene);
            previewCamera = go.AddComponent<Camera>(); previewCamera.scene = scene; previewCamera.enabled = false;
            previewCamera.transform.position = new Vector3(0, 5, 0); previewCamera.transform.rotation = Quaternion.Euler(90, 0, 0);
            previewCamera.orthographic = true; previewCamera.orthographicSize = .64f;
            previewCamera.clearFlags = CameraClearFlags.SolidColor; previewCamera.backgroundColor = Color.black;
            previewCamera.allowHDR = false;
            texture = new RenderTexture(900, 900, 24) { antiAliasing = 4, hideFlags = HideFlags.HideAndDontSave };
            previewCamera.targetTexture = texture;
        }
        void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - previousTime < 1.0 / 30 || EditorApplication.isCompiling) return;
            EnsurePreview();
            if (icon) { icon.Advance(playing ? Mathf.Min(.1f, (float)(now - previousTime)) : 0); previewCamera.Render(); Repaint(); }
            previousTime = now;
        }
        void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                playing = GUILayout.Toggle(playing, "Animate", EditorStyles.toolbarButton);
                if (GUILayout.Button("Select Prefab", EditorStyles.toolbarButton)) Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(CosmicWebLevelIconAssets.PrefabPath);
            }
            float size = Mathf.Min(position.width, position.height - 68);
            Rect rect = GUILayoutUtility.GetRect(size, size, GUILayout.ExpandWidth(true));
            if (texture) GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, false);
            if (icon) EditorGUILayout.LabelField($"Age 0.5 (fixed)  ·  {icon.RenderedParticleCount:N0} particles  ·  3D ovoid", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.LabelField("Edit the prefab to keep tuning. Open scenes are left untouched.", EditorStyles.centeredGreyMiniLabel);
        }
        public static void Capture(string absolutePath, float motionSeconds = 0)
        {
            // A temporary preview instance also supports stills without disturbing a live preview.
            var preview = CreateInstance<CosmicWebLevelIconPreview>();
            Texture2D image = null; var previous = RenderTexture.active;
            try
            {
                preview.EnsurePreview();
                if (!preview.icon) throw new InvalidOperationException("Create the COSMOS icon first.");
                preview.icon.Advance(motionSeconds); preview.previewCamera.Render(); RenderTexture.active = preview.texture;
                image = new Texture2D(preview.texture.width, preview.texture.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, image.width, image.height), 0, 0); image.Apply();
                System.IO.File.WriteAllBytes(absolutePath, image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; if (image) DestroyImmediate(image); DestroyImmediate(preview); }
        }
    }
}
