using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Massive.Emergence.Editor
{
    public static class EmergenceLevelIconAssets
    {
        public const string PrefabPath = EmergenceLevelSetup.Folder + "/LS_EMERGENCE Icon.prefab";

        [MenuItem("MASSIVE/EMERGENCE/Enable Level and Update Organism Icon")]
        public static void EnableLevel()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Update the icon in Edit Mode.");
            var definition = AssetDatabase.LoadAssetAtPath<LevelDefinition>(EmergenceLevelSetup.DefinitionPath);
            if (!definition) throw new InvalidOperationException("Create the EMERGENCE level first.");
            definition.iconPrefab = CreateOrUpdate();
            EditorUtility.SetDirty(definition); AssetDatabase.SaveAssetIfDirty(definition);
            Register(definition);
        }

        public static void Register(LevelDefinition definition)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/Scripts/CORE/LevelCatalog.asset");
            if (!catalog || !definition) throw new InvalidOperationException("Missing level catalog or definition.");
            // Place this level by characteristic scale, also repairing an existing registration.
            // Keep every other level in its authored order and collapse duplicate registrations.
            var levels = catalog.Levels.Where(level => level != definition).ToList();
            int index = levels.FindIndex(level => level && level.scaleExponent > definition.scaleExponent);
            levels.Insert(index < 0 ? levels.Count : index, definition);
            if (catalog.Levels.SequenceEqual(levels)) return;
            var serialized = new SerializedObject(catalog); var list = serialized.FindProperty("levels");
            list.arraySize = levels.Count;
            for (int i = 0; i < levels.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = levels[i];
            serialized.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(catalog);
        }

        public static GameObject CreateOrUpdate()
        {
            var shader = Shader.Find("MASSIVE/Emergence/Level Icon");
            if (!shader || !shader.isSupported) throw new InvalidOperationException("Import the organism icon shader first.");
            string materialPath = EmergenceLevelSetup.Folder + "/Emergence Icon.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath); }
            bool existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Scene preview = existing ? default : EditorSceneManager.NewPreviewScene();
            var root = existing ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("LS_EMERGENCE Icon");
            try
            {
                if (!existing) SceneManager.MoveGameObjectToScene(root, preview);
                var selection = root.GetComponent<LevelIcon>() ?? root.AddComponent<LevelIcon>();
                var visual = root.transform.Find("Living Colony");
                if (!visual)
                {
                    var go = new GameObject("Living Colony"); SceneManager.MoveGameObjectToScene(go, root.scene);
                    visual = go.transform; visual.SetParent(root.transform, false);
                }
                var oldField = visual.GetComponent<EmergenceField>();
                if (oldField) UnityEngine.Object.DestroyImmediate(oldField);
                var icon = visual.GetComponent<EmergenceLevelIcon>();
                if (!icon)
                {
                    visual.localPosition = Vector3.zero; visual.localRotation = Quaternion.identity; visual.localScale = Vector3.one;
                    icon = visual.gameObject.AddComponent<EmergenceLevelIcon>();
                    icon.palette = AssetDatabase.LoadAssetAtPath<EmergenceProfile>(EmergenceLevelSetup.ProfilePath);
                }
                var renderer = visual.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                var serialized = new SerializedObject(selection);
                serialized.FindProperty("visualsRoot").objectReferenceValue = visual;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                icon.BakeFormation();
                // The generated mesh belongs to each live instance, never to the saved prefab.
                icon.enabled = false; icon.enabled = true;
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                if (existing) PrefabUtility.UnloadPrefabContents(root);
                else EditorSceneManager.ClosePreviewScene(preview);
            }
        }
    }

    [CustomEditor(typeof(EmergenceLevelIcon)), CanEditMultipleObjects]
    public sealed class EmergenceLevelIconEditor : UnityEditor.Editor
    {
        double nextFrame;
        void OnEnable() { EditorApplication.update += Preview; }
        void OnDisable() { EditorApplication.update -= Preview; }
        void Preview()
        {
            if (Application.isPlaying || EditorApplication.timeSinceStartup < nextFrame) return;
            var icon = target as EmergenceLevelIcon;
            if (!icon || !icon.isActiveAndEnabled || !icon.animateInEditor) return;
            nextFrame = EditorApplication.timeSinceStartup + 1.0 / 30;
            EditorApplication.QueuePlayerLoopUpdate(); SceneView.RepaintAll();
        }
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("The ecology's pair forces settle a complete 3D colony once. Match Ecology Composition redistributes the combined body-particle budget using the profile's proportions. Local Particle Flow adds differential circulation and eddies inside the body; Flow Speed pauses or speeds up those currents. All motion uses unscaled time. Flow changes need no rebake. Profile force edits regenerate the preview; bake the result into the prefab to avoid settling on the first runtime load.", MessageType.Info);
            if (GUILayout.Button("Bake Formation From Ecology"))
            {
                foreach (UnityEngine.Object item in targets)
                {
                    var icon = (EmergenceLevelIcon)item;
                    Undo.RecordObject(icon, "Bake organism icon formation");
                    icon.BakeFormation(); EditorUtility.SetDirty(icon);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(icon);
                }
            }
        }
    }
}
