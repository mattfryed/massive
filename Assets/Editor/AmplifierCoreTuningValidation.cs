using System;
using Massive.Multiplier;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.Multiplier.Editor
{
    /// <summary>Isolated authoring regressions; actual low-speed contact is additionally checked in Play Mode.</summary>
    public static class AmplifierCoreTuningValidation
    {
        public static string RunChecks()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Run Core authoring checks outside Play Mode.");
            var scene = EditorSceneManager.NewPreviewScene();
            int passed = 0;
            try
            {
                var root = new GameObject("Core tuning fixture");
                SceneManager.MoveGameObjectToScene(root, scene);
                root.SetActive(false);
                root.transform.localScale = Vector3.one * 0.75f;
                var body = root.AddComponent<Rigidbody>();
                var collider = root.AddComponent<SphereCollider>(); collider.radius = 0.75f;
                var visuals = new GameObject("Visual Root"); visuals.transform.SetParent(root.transform, false);
                var core = root.AddComponent<AmplifierCoreGameplay>();
                var settings = new SerializedObject(core);
                settings.FindProperty("coreScale").floatValue = 2f;
                settings.FindProperty("mass").floatValue = 5f;
                settings.FindProperty("linearDrag").floatValue = 0.7f;
                settings.FindProperty("angularDrag").floatValue = 0.9f;
                settings.ApplyModifiedPropertiesWithoutUndo();
                float pendingClearance = AmplifierResonanceSpawner.EstimateCoreRadius(core);
                core.ApplyTuning();
                Check(Mathf.Abs(root.transform.localScale.x - 1.5f) < .0001f, "overall size multiplies original scene size", ref passed);
                Check(Mathf.Abs(visuals.transform.lossyScale.x - 1.5f) < .0001f, "visual shell follows physical size", ref passed);
                Check(Mathf.Abs(AmplifierResonanceSpawner.EstimateCoreRadius(core) - 1.125f) < .0001f, "physical clearance follows scale", ref passed);
                Check(Mathf.Abs(pendingClearance - 1.125f) < .0001f, "placement anticipates an unapplied prefab size", ref passed);
                Check(Mathf.Abs(body.mass - 5f) < .0001f, "mass dial applies independently", ref passed);
                Check(Mathf.Abs(body.linearDamping - .7f) < .0001f && Mathf.Abs(body.angularDamping - .9f) < .0001f, "drag dials reach physics body", ref passed);
                core.ApplyTuning(); core.ApplyTuning();
                Check(Mathf.Abs(root.transform.localScale.x - 1.5f) < .0001f, "repeated application never compounds size", ref passed);
                settings.Update(); settings.FindProperty("coreScale").floatValue = .5f; settings.ApplyModifiedPropertiesWithoutUndo();
                core.ApplyTuning();
                Check(Mathf.Abs(root.transform.localScale.x - .375f) < .0001f, "reducing dial preserves authored baseline", ref passed);
                Check(Mathf.Abs(body.mass - 5f) < .0001f, "size change does not silently change mass", ref passed);
                settings.Update(); settings.FindProperty("presentationOnly").boolValue = true; settings.FindProperty("coreScale").floatValue = 3f;
                settings.ApplyModifiedPropertiesWithoutUndo(); core.ApplyTuning();
                Check(Mathf.Abs(root.transform.localScale.x - .375f) < .0001f, "HUD and treatment copies retain presentation size", ref passed);
                return passed + " Amplifier Core tuning checks passed.";
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static void Check(bool condition, string message, ref int count)
        {
            if (!condition) throw new InvalidOperationException("Amplifier Core tuning: " + message);
            count++;
        }
    }
}
