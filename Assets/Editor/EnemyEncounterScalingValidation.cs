#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Massive.Enemies;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Runs pure authoring checks before the scoped COSMOS Play Mode fixtures.
[InitializeOnLoad]
internal static class EnemyEncounterScalingValidation
{
    const string Folder = "Library/EnemyEncounterScaling";
    static EnemyEncounterScalingValidation() => EditorApplication.delayCall += () =>
    {
        if (!File.Exists(Folder + "/test.request") || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Folder + "/test.request"); Run();
    };
    [MenuItem("MASSIVE/Encounters/Validate 2v2 encounter scaling")]
    internal static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Directory.CreateDirectory(Folder); var checks = new List<string>();
        var original = AssetDatabase.LoadAssetAtPath<EnemyEncounterTimeline>(EnemyEncounterLabSetup.TimelinePath);
        var fixture = Object.Instantiate(original); fixture.twoVTwo = new(); fixture.duration = 180;
        string before = EditorJsonUtility.ToJson(original);
        var forms = AssetDatabase.FindAssets("t:EnemyFormation", new[] { EnemyEncounterLabSetup.Folder })
            .Select(g => AssetDatabase.LoadAssetAtPath<EnemyFormation>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
        var unchanged = forms.ToDictionary(f => f, EditorJsonUtility.ToJson);
        void Check(bool valid, string label) { if (!valid) throw new Exception(label); checks.Add("PASS " + label); }
        EnemyFormation Form(string name) => forms.Single(f => f.name == name);
        List<EnemyFormation.Slot> Extras(string name, bool mode = true, float arrival = 100)
        { var f = Form(name); return EnemyEncounterScaling.Reinforcements(fixture, new EnemyEncounterTimeline.Cue { formation = f, arrivalSeconds = arrival }, f, mode); }
        try
        {
            Check(Extras("01 Drone Paired Rows").Count == 10, "Drone rows expand to 15 per side");
            var rowExtras = Extras("01 Drone Paired Rows");
            Check(rowExtras.All(s => rowExtras.Any(p => Mathf.Abs(p.position.x + s.position.x - 1) < .001f && Mathf.Abs(p.position.y - s.position.y) < .001f)) && rowExtras.Min(s => s.position.x) < .15f,
                "Row reinforcements spread across the full row with left/right symmetry");
            Check(Extras("01b Drone Center Ring").Count == 4, "Drone ring rounds additional count down to complete pairs");
            Check(Extras("01c Drone Goal Phalanxes").Count == 10, "Drone phalanxes each gain a fifth rank of five");
            foreach (var form in forms.Where(f => f.name.StartsWith("02") && f.slots.Count >= 10))
            {
                var extras = EnemyEncounterScaling.Reinforcements(fixture, new() { formation = form }, form, true);
                Check(extras.Count == Mathf.FloorToInt(form.slots.Count * .25f / 2) * 2, form.name + " adds at most 25% in paired slots");
            }
            Check(Extras("03 Seeker Stagger", arrival: 30).Count == 0 && Extras("03 Seeker Stagger").Count == 2, "Inherited Seeker pair only arrives in the second half");
            Check(Extras("04 Dyson Flanks").Count == 2, "Late Dyson encounter gains a balanced pair");
            var turrets = Extras("05 Turret Top Bottom");
            Check(turrets.Count == 2 && turrets.Select(s => s.socket).SequenceEqual(new[] { "TopRight", "BottomLeft" }), "Paired turrets complete the opposite diagonal");
            Check(Extras("05c Turret Four Quadrants").Count == 0, "Four Quadrants is not doubled");
            Check(Extras("06 Carrier Upper Left").Count == 0, "Carrier count stays unchanged");
            Check(Extras("01 Drone Paired Rows", false).Count == 0, "1v1 adds no reinforcements");
            var rows = Form("01 Drone Paired Rows");
            var cue = new EnemyEncounterTimeline.Cue { formation = rows, scaling = EnemyEncounterScaling.CuePolicy.Disable };
            Check(EnemyEncounterScaling.Reinforcements(fixture, cue, rows, true).Count == 0, "Per-cue Disable preserves the base formation in 2v2");
            cue.scaling = EnemyEncounterScaling.CuePolicy.Override; cue.reinforcementPairs = 2;
            var plan = EnemyEncounterScaling.Reinforcements(fixture, cue, rows, true);
            Check(plan.Count == 4 && plan.GroupBy(s => s.releaseGroup).All(g => g.Count() == 2 && g.Select(s => s.releaseDelay).Distinct().Count() == 1), "Override uses complete synchronized pairs");
            Check(plan[0].releaseDelay == rows.slots.Max(s => s.releaseDelay) + .25f && plan[2].releaseDelay - plan[0].releaseDelay == .25f,
                "Reinforcements extend the release sequence at its original real-time cadence");
            string deterministic = JsonUtility.ToJson(plan[0]);
            Check(deterministic == JsonUtility.ToJson(EnemyEncounterScaling.Reinforcements(fixture, cue, rows, true)[0]), "Expansion is deterministic");
            cue.placementOverrides.Add(new() { formation = rows, slotIndex = 0, position = rows.slots[0].position + new Vector2(.01f, .015f) });
            Check(EnemyEncounterScaling.Reinforcements(fixture, cue, rows, true).Count == 4 && cue.GetSlot(rows, 0).position == cue.placementOverrides[0].position,
                "Dragged original slots retain their exact position and formation membership");
            fixture.cues.Clear(); fixture.cues.Add(cue);
            Undo.IncrementCurrentGroup(); int copy = EnemyEncounterAuthoring.Duplicate(fixture, 0);
            Check(fixture.cues[copy].scaling == cue.scaling && fixture.cues[copy].reinforcementPairs == 2 && fixture.cues[copy].placementOverrides[0] != cue.placementOverrides[0],
                "Duplicate preserves scaling controls and independently copies placement edits");
            Undo.PerformUndo(); Check(fixture.cues.Count == 1, "Scaling-aware cue duplication supports Undo");
            var saved = ScriptableObject.CreateInstance<EnemyEncounterTimeline>();
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(fixture), saved);
            Check(saved.twoVTwo.enabled && saved.twoVTwo.droneExtraFraction == .5f && saved.cues[0].reinforcementPairs == 2, "Profile and per-cue overrides survive Unity serialization");
            Object.DestroyImmediate(saved);

            fixture.maxAliveTotal = 20; fixture.maxPressure = 20;
            fixture.populationLimits.Clear(); fixture.populationLimits.Add(new() { enemy = rows.slots[0].enemy, maxAlive = 20 });
            var limits = EnemyEncounterScaling.Limits.Capture(fixture, true);
            Check(limits.total == 30 && limits.pressure == 30 && limits.For(rows.slots[0].enemy) == 30, "2v2 scales total, pressure and per-type limits consistently");
            var budget = new EnemyPopulationBudget();
            for (int i = 0; i < 26; i++) budget.Add(fixture, rows.slots[0].enemy, i < 10 ? EnemyPopulationBudget.Kind.Alive : EnemyPopulationBudget.Kind.Reserved);
            Check(!budget.Allows(fixture, out _) && budget.Allows(fixture, out _, limits), "Shared admission accounting includes living enemies and reservations under effective mode limits");
            fixture.maxAliveTotal = 0; fixture.maxPressure = 0; fixture.populationLimits[0].maxAlive = 0;
            limits = EnemyEncounterScaling.Limits.Capture(fixture, true);
            Check(limits.total == 0 && limits.pressure == 0 && limits.For(rows.slots[0].enemy) == 0, "Unlimited caps remain unlimited in both modes");
            fixture.twoVTwo.enabled = false;
            Check(EnemyEncounterScaling.Reinforcements(fixture, cue, rows, true).Count == 0, "Profile master switch disables all reinforcements");
            Check(before == EditorJsonUtility.ToJson(original) && unchanged.All(p => p.Value == EditorJsonUtility.ToJson(p.Key)), "Authoring tests preserve all user timelines and shared formations");
            File.WriteAllText(Folder + "/validation.txt", "PASSED\n" + string.Join("\n", checks));
            Debug.Log("[2v2 encounter scaling] " + checks.Count + " editor checks passed.");
        }
        catch (Exception e)
        { File.WriteAllText(Folder + "/validation.txt", "FAILED\n" + string.Join("\n", checks) + "\n" + e); Debug.LogException(e); return; }
        finally { Undo.ClearUndo(fixture); Object.DestroyImmediate(fixture); }
        EnemyFlexiblePlacementValidation.Run();
    }
}
#endif
