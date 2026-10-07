#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Massive.Enemies;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
internal static class EnemyTimelineTimingValidation
{
    const string Folder = "Library/EnemyTimelineTiming";
    static EnemyTimelineTimingValidation() => EditorApplication.delayCall += () =>
    {
        if (!File.Exists(Folder + "/test.request") || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Folder + "/test.request"); Run();
    };
    [MenuItem("MASSIVE/Encounters/Validate regulation timeline scaling")]
    internal static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Directory.CreateDirectory(Folder);
        var results = new List<string>();
        var source = AssetDatabase.LoadAssetAtPath<EnemyEncounterTimeline>(EnemyEncounterLabSetup.TimelinePath);
        var copy = Object.Instantiate(source);
        EnemyEncounterTimeline reloaded = null;
        GameObject isolated = null;
        void Check(bool pass, string label) { if (!pass) throw new Exception(label); results.Add("PASS " + label); }
        string before = EditorJsonUtility.ToJson(source);
        try
        {
            copy.duration = 180; copy.fitRegulation = true;
            Check(Mathf.Approximately(90 * copy.TimeScaleFor(120), 60) && Mathf.Approximately(90 * copy.TimeScaleFor(240), 120),
                "Midpoint stays halfway through regulation when its duration doubles");
            Check(Mathf.Approximately(copy.TimeScaleFor(60), 1 / 3f) && copy.TimeScaleFor(0) == 1,
                "Shorter matches scale proportionally; unavailable match timing preserves authored seconds");
            copy.fitRegulation = false;
            Check(copy.TimeScaleFor(240) == 1, "Fixed timing remains available");
            Undo.IncrementCurrentGroup();
            var so = new SerializedObject(copy); so.FindProperty("fitRegulation").boolValue = true; so.ApplyModifiedProperties();
            Undo.PerformUndo(); Check(!copy.fitRegulation, "Fit regulation toggle supports Undo");
            Undo.PerformRedo(); Check(copy.fitRegulation, "Fit regulation toggle supports Redo");
            reloaded = ScriptableObject.CreateInstance<EnemyEncounterTimeline>();
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(copy), reloaded);
            Check(reloaded.fitRegulation && reloaded.duration == 180, "Scaling settings survive Unity serialization");
            isolated = new GameObject("Timing authoring fixture"); isolated.SetActive(false);
            var lab = isolated.AddComponent<Massive.Demonstrations.EnemyEncounterLab>(); lab.previewRegulationSeconds = 240;
            var director = isolated.AddComponent<EnemyDirector>(); director.encounterTimeline = copy;
            float scale = director.PreviewTimelineTimeScale(copy);
            Check(Mathf.Approximately(scale, 240 / 180f), "Lab preview uses its explicit regulation duration, independent of the test-scene clock");
            copy.cues.Clear(); copy.cues.Add(new EnemyEncounterTimeline.Cue { formation = source.cues[0].formation, arrivalSeconds = 90 });
            Undo.IncrementCurrentGroup(); EnemyEncounterAuthoring.Move(copy, 0, 100 / scale, 0);
            Check(Mathf.Approximately(copy.cues[0].arrivalSeconds * scale, 100), "Editing a displayed match time stores the corresponding proportional authored time");
            Undo.PerformUndo(); Check(copy.cues[0].arrivalSeconds == 90, "Scaled time editing supports Undo");
            copy.cues[0].arrivalSeconds = 179;
            Check(EnemyEncounterAuthoring.Validate(copy, null, director, 1).Any(i => i.message.Contains("after the timeline")),
                "Validation includes unchanged formation stagger at the scaled timeline end");
            Check(before == EditorJsonUtility.ToJson(source), "Timing tests preserve the user's authored timeline");
            File.WriteAllText(Folder + "/validation.txt", "PASSED\n" + string.Join("\n", results));
            Debug.Log("[Regulation timing] " + results.Count + " editor checks passed.");
        }
        catch (Exception e)
        { File.WriteAllText(Folder + "/validation.txt", "FAILED\n" + string.Join("\n", results) + "\n" + e); Debug.LogException(e); return; }
        finally
        {
            Undo.ClearUndo(copy); Object.DestroyImmediate(copy);
            if (reloaded) Object.DestroyImmediate(reloaded);
            if (isolated) Object.DestroyImmediate(isolated);
        }
        EnemyFlexiblePlacementValidation.Run();
    }
}
#endif
