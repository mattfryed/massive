using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Massive.Lattice.Editor
{
    public static class LatticeLevelIconAssets
    {
        public const string Folder = "Assets/Scripts/Anomalies/LATTICE";
        public const string PrefabPath = Folder + "/LS_LATTICE Icon.prefab";

        [MenuItem("MASSIVE/LATTICE/Create and Preview Level Icon %#&i")]
        public static void CreateAndPreview() { CreateMissing(); LatticeLevelIconPreview.Open(); }

        public static void CreateMissing()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)) return;
            var shader = Shader.Find("MASSIVE/Lattice/Level Icon");
            if (!shader) throw new InvalidOperationException("LATTICE icon shader has not imported.");
            string materialPath = Folder + "/Lattice Icon.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!material) { material = new Material(shader) { name = "LATTICE Icon" }; AssetDatabase.CreateAsset(material, materialPath); }
            Scene scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("LS_LATTICE Icon"); SceneManager.MoveGameObjectToScene(root, scene);
                var selection = root.AddComponent<LevelIcon>();
                var visual = new GameObject("Lattice Volume"); SceneManager.MoveGameObjectToScene(visual, scene);
                visual.transform.SetParent(root.transform, false);
                visual.transform.localScale = Vector3.one * .16f;
                visual.transform.localRotation = Quaternion.Euler(26, 24, -24);
                var icon = visual.AddComponent<LatticeLevelIcon>();
                var renderer = visual.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                var so = new SerializedObject(selection); so.FindProperty("visualsRoot").objectReferenceValue = visual.transform;
                so.ApplyModifiedPropertiesWithoutUndo();
                // Generated meshes and state buffers are instance-owned, never prefab assets.
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            Debug.Log("LATTICE level icon created.");
        }
    }

    [CustomEditor(typeof(LatticeLevelIcon)), CanEditMultipleObjects]
    public sealed class LatticeLevelIconEditor : UnityEditor.Editor
    {
        double nextFrame;
        void OnEnable() { EditorApplication.update += Preview; }
        void OnDisable() { EditorApplication.update -= Preview; }
        void Preview()
        {
            if (Application.isPlaying || EditorApplication.timeSinceStartup < nextFrame) return;
            var icon = target as LatticeLevelIcon;
            if (!icon || !icon.isActiveAndEnabled || !icon.animateInEditor) return;
            nextFrame = EditorApplication.timeSinceStartup + 1.0 / 30;
            EditorApplication.QueuePlayerLoopUpdate(); SceneView.RepaintAll(); Repaint();
        }
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var icon = (LatticeLevelIcon)target;
            EditorGUILayout.HelpBox($"{icon.NodeCount:N0} nodes / {icon.EdgeCount:N0} connections. {icon.DisconnectedEdgeCount:N0} disrupted. Presentation only; menu animation uses unscaled time.", MessageType.Info);
            if (GUILayout.Button("Open Animated Icon Preview")) LatticeLevelIconPreview.Open();
        }
    }

    public sealed class LatticeLevelIconPreview : EditorWindow
    {
        Scene scene;
        Camera camera;
        LatticeLevelIcon icon;
        RenderTexture texture;
        double previousTime;
        bool playing = true;
        public static void Open()
        {
            var window = GetWindow<LatticeLevelIconPreview>("LATTICE Icon");
            window.minSize = new Vector2(640, 520); window.Show();
        }
        void OnEnable() { EditorApplication.update += Tick; previousTime = EditorApplication.timeSinceStartup; }
        void OnDisable()
        {
            EditorApplication.update -= Tick;
            ReleasePreview();
        }
        void OnProjectChange() { ReleasePreview(); }
        void ReleasePreview()
        {
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            if (texture) { texture.Release(); DestroyImmediate(texture); }
            scene = default; icon = null; camera = null; texture = null;
        }
        void EnsurePreview()
        {
            if (icon) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LatticeLevelIconAssets.PrefabPath);
            if (!prefab) return;
            scene = EditorSceneManager.NewPreviewScene();
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            icon = root.GetComponentInChildren<LatticeLevelIcon>(); icon.animateInEditor = false; icon.Rebuild(); icon.Advance(0);
            var go = new GameObject("LATTICE Preview Camera"); SceneManager.MoveGameObjectToScene(go, scene);
            camera = go.AddComponent<Camera>(); camera.scene = scene; camera.enabled = false;
            camera.transform.position = new Vector3(0, 5, 0); camera.transform.rotation = Quaternion.Euler(90, 0, 0);
            camera.orthographic = true; camera.orthographicSize = .82f; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black; camera.allowHDR = false;
            texture = new RenderTexture(900, 900, 24) { antiAliasing = 4, hideFlags = HideFlags.HideAndDontSave };
            camera.targetTexture = texture;
        }
        void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - previousTime < 1.0 / 30 || EditorApplication.isCompiling) return;
            EnsurePreview();
            if (icon) { if (playing) { icon.previewTime = icon.FieldTime; icon.Advance(Mathf.Min(.05f, (float)(now - previousTime))); icon.previewTime = icon.FieldTime; } camera.Render(); Repaint(); }
            previousTime = now;
        }
        void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                playing = GUILayout.Toggle(playing, "Animate", EditorStyles.toolbarButton);
                if (GUILayout.Button("Select Prefab", EditorStyles.toolbarButton)) Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(LatticeLevelIconAssets.PrefabPath);
                if (GUILayout.Button("Validate", EditorStyles.toolbarButton)) LatticeLevelIconValidation.RunMenu();
            }
            float size = Mathf.Min(position.width, position.height - 70);
            Rect rect = GUILayoutUtility.GetRect(size, size, GUILayout.ExpandWidth(true));
            if (texture) GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, false);
            if (icon) EditorGUILayout.LabelField($"{icon.cellsPerAxis} × {icon.cellsPerAxis} × {icon.cellsPerAxis} cells  ·  {icon.NodeCount:N0} nodes  ·  {icon.EdgeCount:N0} connections", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.LabelField("Edit the prefab to keep tuning. This preview leaves open scenes untouched.", EditorStyles.centeredGreyMiniLabel);
        }
    }
}
