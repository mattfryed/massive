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
internal static class EnemySpawnPlacementValidation
{
    const string Folder = "Library/EnemySpawnPlacement";
    static EnemySpawnPlacementValidation() => EditorApplication.delayCall += () =>
    {
        if (!File.Exists(Folder + "/test.request") || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Folder + "/test.request"); Run();
    };
    [MenuItem("MASSIVE/Encounters/Validate spawn placement editing")]
    internal static void Run()
    {
        var checks = new List<string>();
        var objects = new List<Object>();
        EnemyArenaLayout layout = null; bool wasOval = false;
        void Check(bool pass, string label) { if (!pass) throw new Exception(label); checks.Add("PASS " + label); }
        Directory.CreateDirectory(Folder);
        try
        {
            layout = Object.FindObjectsByType<EnemyDirector>(FindObjectsSortMode.None).First(d => d.encounterTimeline && d.arenaLayout).arenaLayout;
            wasOval = layout.arena.ovalOutline;
            var source = AssetDatabase.LoadAssetAtPath<EnemyFormation>(EnemyEncounterLabSetup.Folder + "/03 Seeker Stagger.asset");
            var turret = AssetDatabase.LoadAssetAtPath<EnemyFormation>(EnemyEncounterLabSetup.Folder + "/05 Turret Top Bottom.asset");
            string before = EditorJsonUtility.ToJson(source);
            var timeline = ScriptableObject.CreateInstance<EnemyEncounterTimeline>(); objects.Add(timeline);
            timeline.cues.Add(new EnemyEncounterTimeline.Cue { formation = source });
            timeline.cues.Add(new EnemyEncounterTimeline.Cue { formation = source });
            var desired = source.slots[0].Copy(); desired.position = new Vector2(.34f, .63f);
            foreach (bool oval in new[] { false, true }) foreach (bool mirror in new[] { false, true })
            {
                layout.arena.ovalOutline = oval;
                layout.Resolve(desired, mirror, out var pose, out _);
                Check(EnemySpawnPlacementAuthoring.Project(layout, source.slots[0], mirror, false, pose.position, out var result) &&
                    Vector2.Distance(result.position, desired.position) < .0001f, $"{(oval ? "Oval" : "Rectangle")} projection round trip (mirror={mirror})");
            }
            layout.arena.ovalOutline = wasOval;
            var wallSlot = turret.slots[0].Copy(); wallSlot.overrideWallPosition = true; wallSlot.wallPosition = .31f;
            layout.Resolve(wallSlot, false, out var wall, out _, true);
            Check(EnemySpawnPlacementAuthoring.Project(layout, turret.slots[0], false, true, wall.clearance, out var projected) &&
                Mathf.Abs(projected.wallPosition - .31f) < .0001f && projected.socket == turret.slots[0].socket,
                "Wall drag follows the flipped curved quadrant without editing the shared mount");
            Undo.IncrementCurrentGroup();
            Check(EnemySpawnPlacementAuthoring.Commit(timeline, 0, source, 0, desired) &&
                timeline.cues[0].GetSlot(source, 0).position == desired.position && timeline.cues[1].placementOverrides.Count == 0 &&
                before == EditorJsonUtility.ToJson(source), "Cue placement is isolated from shared template and other encounters");
            Undo.PerformUndo(); Check(timeline.cues[0].placementOverrides.Count == 0, "Placement supports Undo");
            Undo.PerformRedo(); Check(timeline.cues[0].GetSlot(source, 0).position == desired.position, "Placement supports Redo");
            Check(timeline.cues[0].GetSlot(turret, 0) == turret.slots[0], "Overrides cannot leak into different variants or fallbacks");
            Undo.IncrementCurrentGroup(); int duplicate = EnemyEncounterAuthoring.Duplicate(timeline, 0);
            timeline.cues[duplicate].placementOverrides[0].position = Vector2.zero;
            Check(timeline.cues[0].GetSlot(source, 0).position == desired.position, "Duplicated encounter owns independent placement entries");
            var loaded = ScriptableObject.CreateInstance<EnemyEncounterTimeline>(); objects.Add(loaded);
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(timeline), loaded);
            Check(loaded.cues[0].GetSlot(source, 0).position == desired.position, "Placement survives Unity serialization");
            Undo.IncrementCurrentGroup(); EnemySpawnPlacementAuthoring.Reset(timeline, 0, source);
            Check(timeline.cues[0].placementOverrides.Count == 0, "Reset restores the shared formation's positions");
            Undo.PerformUndo(); Check(timeline.cues[0].GetSlot(source, 0).position == desired.position, "Reset supports Undo");
            var shared = Object.Instantiate(source); objects.Add(shared);
            Undo.IncrementCurrentGroup(); EnemySpawnPlacementAuthoring.Commit(null, -1, shared, 0, desired);
            Check(shared.slots[0].position == desired.position && shared.slots[0].releaseDelay == source.slots[0].releaseDelay,
                "Library drag changes placement while preserving spawn timing");
            File.WriteAllText(Folder + "/validation.txt", "PASSED\n" + string.Join("\n", checks));
            Debug.Log("[Spawn placement editing] " + checks.Count + " checks passed.");
        }
        catch (Exception e)
        { File.WriteAllText(Folder + "/validation.txt", "FAILED\n" + string.Join("\n", checks) + "\n" + e); Debug.LogException(e); return; }
        finally
        {
            if (layout) layout.arena.ovalOutline = wasOval;
            foreach (var obj in objects) if (obj) { Undo.ClearUndo(obj); Object.DestroyImmediate(obj); }
        }
        EnemyFlexiblePlacementValidation.Run();
    }
}
#endif
