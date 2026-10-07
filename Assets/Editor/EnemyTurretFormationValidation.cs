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
public static class EnemyTurretFormationValidation
{
    const string Folder = "Library/EnemyTurretFormations";
    static EnemyTurretFormationValidation() => EditorApplication.delayCall += () =>
    {
        if (!File.Exists(Folder + "/test.request") || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Folder + "/test.request"); Run();
    };
    [MenuItem("MASSIVE/Encounters/Validate turret formation controls")]
    public static void Run()
    {
        Directory.CreateDirectory(Folder);
        var checks = new List<string>();
        EnemyFormation copy = null;
        EnemyEncounterTimeline timeline = null, reloaded = null;
        void Check(bool passed, string label)
        { if (!passed) throw new Exception(label); checks.Add("PASS " + label); }
        try
        {
            var four = EnemyTurretFormationAuthoring.CreateFourQuadrants();
            var source = AssetDatabase.LoadAssetAtPath<EnemyFormation>(EnemyEncounterLabSetup.Folder + "/05 Turret Top Bottom.asset");
            string before = EditorJsonUtility.ToJson(source);
            copy = Object.Instantiate(source);
            Check(EnemyTurretFormationAuthoring.CanFlip(copy) && EnemyTurretFormationAuthoring.CanFlip(four), "Inspector flip is available for paired and four-quadrant turret formations");
            Undo.IncrementCurrentGroup(); EnemyTurretFormationAuthoring.Flip(copy);
            Check(copy.slots[0].socket == "TopRight" && copy.slots[1].socket == "BottomLeft", "Shared-formation button swaps the intended diagonal");
            Undo.PerformUndo();
            Check(copy.slots[0].socket == "TopLeft" && copy.slots[1].socket == "BottomRight", "Formation flip supports Undo");
            Undo.PerformRedo();
            Check(copy.slots[0].socket == "TopRight" && copy.slots[1].socket == "BottomLeft", "Formation flip supports Redo");
            Undo.IncrementCurrentGroup(); EnemyTurretFormationAuthoring.Flip(copy);
            Check(copy.slots[0].socket == "TopLeft" && copy.slots[1].socket == "BottomRight", "Second flip restores the original orientation");

            timeline = ScriptableObject.CreateInstance<EnemyEncounterTimeline>();
            timeline.cues.Add(new EnemyEncounterTimeline.Cue { formation = source });
            timeline.cues.Add(new EnemyEncounterTimeline.Cue { formation = source });
            Undo.IncrementCurrentGroup();
            var serialized = new SerializedObject(timeline); serialized.Update();
            serialized.FindProperty("cues").GetArrayElementAtIndex(1).FindPropertyRelative("flipWallOrientation").boolValue = true;
            serialized.ApplyModifiedProperties(); Undo.FlushUndoRecordObjects();
            Check(!timeline.cues[0].flipWallOrientation && timeline.cues[1].flipWallOrientation, "Composer flip changes only the selected encounter");
            Undo.PerformUndo(); Check(!timeline.cues[1].flipWallOrientation, "Encounter flip supports Undo");
            Undo.PerformRedo(); Check(timeline.cues[1].flipWallOrientation, "Encounter flip supports Redo");
            Undo.IncrementCurrentGroup(); int duplicate = EnemyEncounterAuthoring.Duplicate(timeline, 1);
            Check(timeline.cues[duplicate].flipWallOrientation, "Duplicated encounter retains its orientation");
            reloaded = ScriptableObject.CreateInstance<EnemyEncounterTimeline>();
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(timeline), reloaded);
            Check(reloaded.cues[1].flipWallOrientation && !reloaded.cues[0].flipWallOrientation, "Orientation survives Unity serialization");
            Check(before == EditorJsonUtility.ToJson(source), "Control validation preserves the shared paired formation");
            Check(four.slots.Select(s => s.socket).OrderBy(s => s).SequenceEqual(new[] { "BottomLeft", "BottomRight", "TopLeft", "TopRight" }), "New formation contains exactly one turret per top/bottom quadrant");
            File.WriteAllText(Folder + "/validation.txt", "PASSED\n" + string.Join("\n", checks));
            Debug.Log("[Turret formation controls] " + checks.Count + " checks passed.");
        }
        catch (Exception e)
        { File.WriteAllText(Folder + "/validation.txt", "FAILED\n" + string.Join("\n", checks) + "\n" + e); Debug.LogException(e); return; }
        finally
        {
            foreach (var obj in new Object[] { copy, timeline, reloaded })
                if (obj) { Undo.ClearUndo(obj); Object.DestroyImmediate(obj); }
        }
        EnemyFlexiblePlacementValidation.Run();
    }
}
#endif
