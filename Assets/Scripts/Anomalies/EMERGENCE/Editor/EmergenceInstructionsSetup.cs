using System;
using System.Linq;
using Massive.Demonstrations;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.Emergence.Editor
{
    public static class EmergenceInstructionsSetup
    {
        public const string PanelPath = EmergenceLevelSetup.Folder + "/InstructionsPanel-EMERGENCE.prefab";
        const string DemoName = "Organism reward demonstration";

        [MenuItem("MASSIVE/EMERGENCE/Refresh Instructions Demonstration")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Refresh instructions in Edit Mode after compilation.");
            var reference = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Scripts/Anomalies/ORBITAL/InstructionsPanel-ORBITAL.prefab");
            var referenceIcon = reference.GetComponentInChildren<LevelIcon>(true).transform;
            var panel = PrefabUtility.LoadPrefabContents(PanelPath);
            try
            {
                var icon = panel.GetComponentsInChildren<LevelIcon>(true).Single().transform;
                icon.localPosition = referenceIcon.localPosition; icon.localRotation = referenceIcon.localRotation; icon.localScale = referenceIcon.localScale;
                PrefabUtility.RecordPrefabInstancePropertyModifications(icon);
                var mid = panel.transform.Find("Mid panel");
                if (!mid) throw new InvalidOperationException("Missing middle-right preview frame.");
                var old = panel.transform.Find(DemoName);
                if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
                var legacy = mid.Find("Rect Transform");
                if (legacy) UnityEngine.Object.DestroyImmediate(legacy.gameObject);
                var stage = Child(panel.transform, DemoName); stage.SetActive(false);
                stage.transform.localPosition = new Vector3(1000, 0, 1000);
                var demo = stage.AddComponent<EmergenceInstructionsDemo>();
                demo.ecology = AssetDatabase.LoadAssetAtPath<EmergenceProfile>(EmergenceLevelSetup.ProfilePath);
                demo.particleMaterial = AssetDatabase.LoadAssetAtPath<Material>(EmergenceLevelSetup.Folder + "/Ecology Particles.mat");
                demo.rewardPrefabs = new[] { "Metabolic Matter Nugglet", "Metabolic Mass Nugget", "Metabolic Amplifier Nugglet", "Metabolic Amplifier Nugget" }
                    .Select(name => AssetDatabase.LoadAssetAtPath<GameObject>(EmergenceLevelSetup.Folder + "/" + name + ".prefab").GetComponent<EmergenceMassNugget>()).ToArray();
                var cameraObject = Child(stage.transform, "Cropped gameplay camera");
                cameraObject.transform.localPosition = new Vector3(0, 25, 0);
                cameraObject.transform.localRotation = Quaternion.Euler(90, 0, 0);
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 1.25f;
                camera.nearClipPlane = .1f; camera.farClipPlane = 50;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.allowHDR = false; camera.allowMSAA = true;
                demo.viewport = cameraObject.AddComponent<PlayerDemoView>(); demo.panelFrame = mid;
                var frame = new SerializedObject(mid.GetComponent<Shapes.Rectangle>());
                demo.viewportSize = new Vector2(frame.FindProperty("width").floatValue - .1f, frame.FindProperty("height").floatValue - .1f);
                foreach (var text in panel.GetComponentsInChildren<TMP_Text>(true))
                    if (text.name == "Instructions body") text.text = "Follow feeding organisms. Their pulse and growing bud signal a NUGGET or NUGGLET is about to be released.\n\nCollect MATTER for mass and AMPLIFIERS for mass + personal multiplier.\n\nTHRUST, SWEEP and REPULSOR disrupt organisms. Keep them intact for a steady supply.";
                stage.SetActive(true);
                PrefabUtility.SaveAsPrefabAsset(panel, PanelPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(panel); }
        }
        static GameObject Child(Transform parent, string name)
        {
            var child = new GameObject(name); SceneManager.MoveGameObjectToScene(child, parent.gameObject.scene);
            child.transform.SetParent(parent, false); return child;
        }
    }
}
