#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Massive.Demonstrations;
using Massive.Enemies;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class EnemyEncounterComposerValidation
{
    private const string Folder = "Library/EnemyEncounterComposerValidation";
    static EnemyEncounterComposerValidation()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Folder + "/run.request")) return;
            File.Delete(Folder + "/run.request"); Run();
        };
    }
    [MenuItem("MASSIVE/Enemies/Validate Encounter Composer")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        Directory.CreateDirectory(Folder);
        var results = new List<string>(); EnemyEncounterTimeline scratch = null; GameObject fixture = null;
        string scratchPath = null;
        var source = AssetDatabase.LoadAssetAtPath<EnemyEncounterTimeline>(EnemyEncounterLabSetup.TimelinePath);
        string original = EditorJsonUtility.ToJson(source);
        void Check(bool valid, string message) { if (!valid) throw new Exception(message); results.Add("PASS " + message); }
        try
        {
            scratch = Object.Instantiate(source);
            scratchPath = AssetDatabase.GenerateUniqueAssetPath("Assets/Editor/Encounter Composer Validation Scratch.asset");
            AssetDatabase.CreateAsset(scratch, scratchPath);
            int originalCount = scratch.cues.Count;
            var rows = scratch.cues[0].formation;
            Undo.IncrementCurrentGroup();
            int added = EnemyEncounterAuthoring.Add(scratch, rows, 8.13f, .25f);
            Check(added == originalCount && scratch.cues[added].arrivalSeconds == 8.25f, "Drop adds an independently timed cue and snaps to .25 seconds");
            Undo.PerformUndo(); Check(scratch.cues.Count == originalCount, "Undo removes the added cue");
            Undo.PerformRedo(); Check(scratch.cues.Count == originalCount + 1 && scratch.cues[added].formation == rows, "Redo restores the cue and formation reference");
            Undo.IncrementCurrentGroup(); EnemyEncounterAuthoring.Move(scratch, added, 12.34f, 0);
            Check(Mathf.Abs(scratch.cues[added].arrivalSeconds - 12.34f) < .001f, "Unsnapped movement keeps the exact time");
            Undo.PerformUndo(); Check(scratch.cues[added].arrivalSeconds == 8.25f, "Undo restores drag timing in one operation");
            Undo.IncrementCurrentGroup(); EnemyEncounterAuthoring.Move(scratch, added, -2, .25f);
            Check(scratch.cues[added].arrivalSeconds == 0, "Dragging before zero clamps to zero");
            var originalCue = scratch.cues[added]; originalCue.variants.Add(scratch.cues[1].formation); originalCue.fallback = scratch.cues[2].formation; originalCue.allowHorizontalMirror = true;
            Check(!originalCue.overrideSpawnPolicy && originalCue.IntegrityFor(rows) == EnemyFormation.Integrity.Flexible,
                "New encounters inherit the Flexible Drone formation policy");
            originalCue.overrideSpawnPolicy = true; originalCue.integrity = EnemyFormation.Integrity.Strict;
            originalCue.maxPositionAdjustment = .75f; originalCue.blockedSlotGrace = 2.5f;
            Undo.IncrementCurrentGroup(); int duplicate = EnemyEncounterAuthoring.Duplicate(scratch, added);
            Check(scratch.cues[duplicate].formation == originalCue.formation && scratch.cues[duplicate].fallback == originalCue.fallback && scratch.cues[duplicate].allowHorizontalMirror, "Duplicate preserves formation, fallback and mirror settings");
            Check(scratch.cues[duplicate].variants.Count == 1 && !ReferenceEquals(originalCue.variants, scratch.cues[duplicate].variants), "Duplicate owns its variants list");
            Check(scratch.cues[duplicate].overrideSpawnPolicy && scratch.cues[duplicate].IntegrityFor(rows) == EnemyFormation.Integrity.Strict &&
                scratch.cues[duplicate].AdjustmentFor(rows) == .75f && scratch.cues[duplicate].GraceFor(rows) == 2.5f,
                "Duplicate preserves spawn policy overrides");
            scratch.cues[duplicate].variants.Clear(); Check(originalCue.variants.Count == 1, "Editing duplicated variants leaves the original cue intact");
            Undo.IncrementCurrentGroup(); EnemyEncounterAuthoring.Remove(scratch, duplicate);
            Check(scratch.cues.Count == originalCount + 1, "Remove deletes exactly one cue");
            Undo.PerformUndo(); Check(scratch.cues.Count == originalCount + 2, "Undo restores a removed cue");
            int savedTotal = scratch.maxAliveTotal, savedType = scratch.populationLimits[0].maxAlive;
            Undo.IncrementCurrentGroup();
            var settings = new SerializedObject(scratch); settings.Update();
            settings.FindProperty("maxAliveTotal").intValue = savedTotal + 7;
            settings.FindProperty("populationLimits").GetArrayElementAtIndex(0).FindPropertyRelative("maxAlive").intValue = savedType + 1;
            settings.ApplyModifiedProperties();
            Check(scratch.maxAliveTotal == savedTotal + 7 && scratch.populationLimits[0].maxAlive == savedType + 1, "Composer population fields edit the timeline itself");
            Undo.PerformUndo(); Check(scratch.maxAliveTotal == savedTotal && scratch.populationLimits[0].maxAlive == savedType, "Population controls support Undo");
            Undo.PerformRedo(); Check(scratch.maxAliveTotal == savedTotal + 7 && scratch.populationLimits[0].maxAlive == savedType + 1, "Population controls support Redo");
            AssetDatabase.SaveAssetIfDirty(scratch); AssetDatabase.ImportAsset(scratchPath, ImportAssetOptions.ForceUpdate);
            var reloaded = AssetDatabase.LoadAssetAtPath<EnemyEncounterTimeline>(scratchPath);
            Check(reloaded.cues.Count == originalCount + 2 && File.ReadAllText(scratchPath).Contains("copy"), "Edited timeline saves and reloads with its cues");
            Check(reloaded.cues[duplicate].overrideSpawnPolicy && reloaded.cues[duplicate].AdjustmentFor(rows) == .75f &&
                reloaded.cues[duplicate].GraceFor(rows) == 2.5f, "Spawn policy overrides survive an asset round trip");
            Check(reloaded.maxAliveTotal == savedTotal + 7 && reloaded.populationLimits[0].maxAlive == savedType + 1 && reloaded.maxPressure == source.maxPressure,
                "Total/type/pressure settings survive asset round trip");
            fixture = new GameObject("Composer validation director"); fixture.SetActive(false);
            var director = fixture.AddComponent<EnemyDirector>(); director.encounterTimeline = scratch;
            for (int seed = 0; seed < 20; seed++)
            {
                director.encounterSeed = seed;
                var expected = director.ChooseFormation(added, out bool expectedMirror);
                var actual = EnemyEncounterAuthoring.Choose(scratch, added, seed, out bool actualMirror);
                if (expected != actual || expectedMirror != actualMirror) throw new Exception("Seed preview differs from Director");
            }
            Check(true, "Twenty variant/mirror seeds exactly match the Director");
            var lab = Object.FindObjectsByType<EnemyEncounterLab>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (lab && lab.layout.arena) lab.layout.arena.RefreshNow(false);
            scratch.cues.Clear(); EnemyEncounterAuthoring.Add(scratch, rows, 3, 0);
            var problems = EnemyEncounterAuthoring.Validate(scratch, lab ? lab.layout : null, lab ? lab.director : null, 1);
            Check(problems.Count == 0, "The authored paired rows validate in the open lab");
            EnemyEncounterAuthoring.Add(scratch, rows, 3, 0);
            problems = EnemyEncounterAuthoring.Validate(scratch, lab ? lab.layout : null, lab ? lab.director : null, 1);
            if (lab) Check(problems.Any(p => p.message.Contains("reservation conflict")), "Simultaneous overlapping formations are flagged");
            scratch.maxAliveTotal = 1;
            Check(EnemyEncounterAuthoring.Validate(scratch, null, null, 1).Any(p => p.message.Contains("Total limit 1:")), "Authoring checks use the timeline total limit without a Director");
            scratch.maxAliveTotal = 0; scratch.populationLimits[0].maxAlive = 1;
            Check(EnemyEncounterAuthoring.Validate(scratch, null, null, 1).Any(p => p.message.Contains("Drone limit 1:")), "Authoring checks use the timeline per-enemy limit");
            scratch.populationLimits[0].maxAlive = 0;
            scratch.maxPressure = 1; scratch.duration = 1;
            problems = EnemyEncounterAuthoring.Validate(scratch, null, null, 1);
            Check(problems.Any(p => p.message.Contains("Pressure limit")), "A batch exceeding pressure is flagged");
            Check(problems.Any(p => p.message.Contains("after the timeline")), "Spawns beyond timeline end are flagged");
            scratch.cues[0].formation = null;
            Check(EnemyEncounterAuthoring.Validate(scratch, null, null, 1).Any(p => p.message.Contains("Missing")), "Missing formation is reported without throwing");
            Check(EditorJsonUtility.ToJson(source) == original, "Validation leaves the authored timeline unchanged");
            File.WriteAllText(Folder + "/report.txt", "PASSED\n" + string.Join("\n", results));
            Debug.Log("[Encounter Composer validation] PASSED " + results.Count + " checks");
        }
        catch (Exception error)
        {
            File.WriteAllText(Folder + "/report.txt", "FAILED\n" + string.Join("\n", results) + "\n" + error);
            Debug.LogException(error);
        }
        finally
        {
            if (scratch) Undo.ClearUndo(scratch);
            if (fixture) Object.DestroyImmediate(fixture);
            if (!string.IsNullOrEmpty(scratchPath)) AssetDatabase.DeleteAsset(scratchPath);
            var window = EnemyEncounterComposer.Show(source);
            var host = EditorGUIUtility.GetMainWindowPosition(); window.position = new Rect(host.center.x - 730, host.center.y - 420, 1460, 840);
        }
    }
}
#endif
