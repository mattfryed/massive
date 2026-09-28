using System;
using System.Linq;
using System.Text;
using Massive.Demonstrations;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static partial class MassiveDemoMigration
{
    [MenuItem("MASSIVE/Demonstrations/4 - Add Grid Strips and Captions")]
    public static void RefineMenu() { Debug.Log(RefineHowTo()); }
    public static string RefineHowTo()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Use Edit Mode.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != HowToPath) throw new Exception("Open How To Play.");
        System.IO.Directory.CreateDirectory("Library/DemoMigrationBackup/Revision2");
        EditorSceneManager.SaveScene(scene, "Library/DemoMigrationBackup/Revision2/HowToBefore.unity", true);
        var roots = scene.GetRootGameObjects();
        var template = roots.Single(g => g.name == "PLAYING FIELD").GetComponentInChildren<VectorGridGPU>(true);
        var parent = roots.Single(g => g.name == "Player demonstrations");
        if (!parent.GetComponent<GridInteractionSystem>()) parent.AddComponent<GridInteractionSystem>();
        var toast = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Power-ups/PU_PickupToast.prefab");
        foreach (var director in parent.GetComponentsInChildren<PlayerDemoDirector>(true))
        {
            var view = director.GetComponentInChildren<PlayerDemoView>(true);
            float left = .05f, right = 15.6f, z;
            if (director.Scenario.kind == PlayerDemoKind.MovementAndCombo) z = 3.72f;
            else if (director.Scenario.kind == PlayerDemoKind.Block) z = -.04f;
            else
            {
                z = -3.82f;
                const float gap = .35f;
                float unit = (right-left-2f*gap)/4f;
                string title = director.Scenario.powerUp.displayName;
                if (director.Scenario.powerUp is Massive.PowerUps.TimeDilationPowerUpDefinition) right = left + unit;
                else if (director.Scenario.powerUp is Massive.PowerUps.ParticleAcceleratorPowerUpDefinition) { left += unit+gap; right = left+unit*2f; }
                else left += unit*3f+gap*2f;
                view.captionPrefab = toast; view.caption = title;
            }
            view.lowerLeft = new Vector3(left,0,z-1.7f); view.upperRight = new Vector3(right,0,z+1.7f);
            view.RefreshRect(); EditorUtility.SetDirty(view);
            var grid = director.GetComponentInChildren<VectorGridGPU>(true);
            if (!grid)
            {
                var go = new GameObject("Demonstration grid"); go.SetActive(false);
                go.transform.SetParent(director.transform,false);
                grid = go.AddComponent<VectorGridGPU>();
                EditorUtility.CopySerialized(template, grid);
                grid.registerAsDefault = false;
                grid.ResetPresentation();
                grid.lineColor = new Color(.42f,.42f,.42f,1f);
                grid.boundary.borderOverlay = true;
                grid.boundary.borderColor = new Color(.55f,.55f,.55f,1f);
                grid.boundary.borderWidthWorld = .012f;
                grid.transform.localPosition = Vector3.down*.08f;
                grid.transform.localRotation = Quaternion.Euler(90f,0,0);
                grid.size = new Vector2(12f,5f);
                grid.gridScale = .4f;
                go.SetActive(true);
            }
            Set(director,"demoGrid",grid);
            director.Scenario.viewPadding = .35f;
            director.Scenario.actionTimeout = 12f;
            EditorUtility.SetDirty(director.Scenario);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        return "Five explicitly bound grid strips; rows match the 3.4-unit instruction boxes, Accelerator receives double width, and three permanent toast captions are assigned.";
    }
    public static string InspectRefinement()
    {
        var sb = new StringBuilder();
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name.Contains("sequence") || root.name == "PU panel")
            {
                var border = root.transform.Find("Border");
                if (border)
                {
                    sb.AppendLine(PathOf(border) + " pos=" + border.position + " scale=" + border.lossyScale);
                    foreach(var c in border.GetComponents<Component>())
                        if(c && !(c is Transform) && !(c is MeshFilter) && !(c is MeshRenderer)) sb.AppendLine(EditorJsonUtility.ToJson(c));
                }
            }
        }
        return sb.ToString();
    }
}
