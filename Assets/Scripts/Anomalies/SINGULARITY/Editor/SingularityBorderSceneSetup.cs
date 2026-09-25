using System;
using System.Linq;
using Massive.Multiplier;
using Massive.Singularity;
using Shapes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SingularityBorderSceneSetup
{
    [MenuItem("MASSIVE/SINGULARITY/Connect Goal Borders and Amplifier Effects")]
    public static void ApplyMenu()
    {
        Scene scene = SceneManager.GetActiveScene();
        Debug.Log(Apply(scene));
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save SINGULARITY.");
    }

    public static string Apply(Scene scene)
    {
        if (Application.isPlaying || EditorApplication.isCompiling || scene.path != SingularitySceneSetup.ScenePath)
            throw new InvalidOperationException("Open SINGULARITY in Edit Mode after compilation.");
        var grid = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SingularityGridRenderer>(true)).Single();
        var gameplay = scene.GetRootGameObjects().Single(g => g.name == "SINGULARITY - Gameplay");
        var goals = gameplay.GetComponentsInChildren<AmplifierGoalCapture>(true).OrderBy(g => g.TeamID).ToArray();
        if (goals.Length != 2 || goals[0].TeamID != 1 || goals[1].TeamID != 2)
            throw new InvalidOperationException("Both team goals must be present.");
        var left = gameplay.transform.Find("TEAM 1 goal/MAX RING LEFT").GetComponent<Disc>();
        var right = gameplay.transform.Find("TEAM 2 goal/MAX RING RIGHT").GetComponent<Disc>();
        var treatment = grid.GetComponent<AmplifierGoalTreatments>();
        if (!treatment) treatment = Undo.AddComponent<AmplifierGoalTreatments>(grid.gameObject);
        Undo.RecordObject(treatment, "Connect folded Amplifier effects");
        foreach (var goal in goals) Undo.RecordObject(goal, "Connect folded Amplifier effects");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Power-ups/Amplifier Core/Amplifier Core.prefab");
        treatment.Configure(null, goals, Shader.Find("MASSIVE/AmplifierTreatment"),
            prefab ? prefab.GetComponent<AmplifierCoreGameplay>() : null);
        treatment.ConfigurePresentationGrid(grid.surface.transform, new Vector2(grid.surface.Width, grid.surface.ProjectedHeight), true);
        treatment.ShowControls = false;
        Undo.RecordObject(grid, "Connect folded Amplifier effects");
        grid.amplifierTreatments = treatment;
        var extensions = grid.GetComponent<SingularityGoalBorderExtensions>();
        if (!extensions) extensions = Undo.AddComponent<SingularityGoalBorderExtensions>(grid.gameObject);
        Undo.RecordObject(extensions, "Connect goal border extensions");
        extensions.grid = grid; extensions.leftGoalOutline = left; extensions.rightGoalOutline = right;
        grid.RefreshPresentation();
        foreach (var value in goals) EditorUtility.SetDirty(value);
        EditorUtility.SetDirty(treatment); EditorUtility.SetDirty(grid); EditorUtility.SetDirty(extensions);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorApplication.QueuePlayerLoopUpdate(); SceneView.RepaintAll();
        return "SINGULARITY: both goals connected to folded Amplifier border effects and four straight outline extensions.";
    }
}
