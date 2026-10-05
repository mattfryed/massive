#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.Lattice.EditorTools
{
    [InitializeOnLoad]
    public static partial class LatticeSceneLayout
    {
        const string Output = "Library/LatticeSceneLayout";
        static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();
        static string PathOf(Transform t) => AnimationUtility.CalculateTransformPath(t, null);

        static LatticeSceneLayout()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool("LATTICE.LayoutFlow", false)) return;
                SessionState.SetBool("LATTICE.LayoutFlow", false);
                EditorApplication.delayCall += () => EditorSceneManager.OpenScene(LatticeSetup.ScenePath);
            };
        }

        [MenuItem("MASSIVE/LATTICE/Validate standard level flow %#&n")]
        public static void ValidateFlow()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty || scene.path != LatticeSetup.ScenePath)
                throw new InvalidOperationException("Open saved LATTICE in Edit Mode before validating its full launch flow.");
            ValidateSaved();
            EditorSceneManager.OpenScene("Assets/Scenes/" + SceneFlow.LevelSelectScene + ".unity");
            SessionState.SetBool("LATTICE.LayoutFlow", true);
            Massive.Lattice.Editor.LatticePlayableValidation.Run();
        }

        [MenuItem("MASSIVE/LATTICE/Inspect standard scene layout %#&h")]
        public static void Inspect()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
            Directory.CreateDirectory(Output);
            foreach (string path in new[] { LatticeSetup.ScenePath, NovaLevelSetup.NovaPath })
            {
                var scene = EditorSceneManager.OpenPreviewScene(path);
                try
                {
                    var text = new StringBuilder();
                    foreach (var t in All<Transform>(scene))
                    {
                        text.AppendLine(PathOf(t) + (t.gameObject.activeSelf ? "" : " [inactive]") + " [" +
                            string.Join(", ", t.GetComponents<Component>().Where(c => c && !(c is Transform)).Select(c => c.GetType().Name)) + "]");
                    }
                    text.AppendLine("\nSYSTEM CONFIGURATION\n");
                    foreach (var c in All<MonoBehaviour>(scene).Where(c => c && (
                        c.GetType().Name.Contains("Manager") || c.GetType().Name.Contains("Roster") ||
                        c.GetType().Name.Contains("Spawner") || c.GetType().Name.Contains("SceneContext") ||
                        c.GetType().Name.Contains("ScoreService") || c.GetType().Name.Contains("TimerPresenter") ||
                        c.GetType().Name.Contains("TeamAmplifierToast"))))
                    {
                        text.AppendLine(PathOf(c.transform) + " : " + c.GetType().FullName);
                        text.AppendLine(EditorJsonUtility.ToJson(c, true));
                        var p = new SerializedObject(c).GetIterator();
                        while (p.Next(true))
                            if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue)
                            {
                                var value = p.objectReferenceValue;
                                var owner = value is GameObject go ? go.transform : (value as Component)?.transform;
                                text.AppendLine("REF " + p.propertyPath + " = " + (owner && owner.gameObject.scene.IsValid() ? PathOf(owner) : AssetDatabase.GetAssetPath(value)));
                            }
                    }
                    File.WriteAllText(Output + "/" + scene.name + ".txt", text.ToString());
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            Debug.Log("LATTICE/NOVA layout inventory saved to " + Output);
        }
    }
}
#endif
