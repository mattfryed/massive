#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Massive.Enemies;
using UnityEditor;
using UnityEngine;

/// <summary>Editor operations on the existing encounter assets; no second encounter format.</summary>
internal static class EnemyEncounterAuthoring
{
    internal static float Snap(float seconds, float step) => Mathf.Max(0, step > 0 ? Mathf.Round(seconds / step) * step : seconds);
    internal static float LastDelay(EnemyFormation formation) => formation && formation.slots.Count > 0
        ? formation.slots.Max(s => s == null ? 0 : Mathf.Max(0, s.releaseDelay)) : 0;
    internal static EnemyDefinition Type(EnemyFormation formation)
    {
        if (!formation || formation.slots.Count == 0) return null;
        var first = formation.slots[0]?.enemy;
        return first && formation.slots.All(s => s != null && s.enemy == first) ? first : null;
    }
    internal static string Name(EnemyDefinition definition)
    {
        if (!definition) return "Mixed / Unassigned";
        string name = definition.name.StartsWith("ED_") ? definition.name.Substring(3) : definition.name;
        return ObjectNames.NicifyVariableName(name.Replace('_', ' '));
    }
    internal static EnemyFormation Choose(EnemyEncounterTimeline timeline, int index, int seed, out bool mirror)
    {
        var cue = timeline.cues[index];
        var options = new List<EnemyFormation>();
        if (cue.formation) options.Add(cue.formation);
        if (cue.variants != null) options.AddRange(cue.variants.Where(f => f));
        // Match EnemyDirector. Keep cue order stable: indices participate in variant selection.
        var random = new System.Random(unchecked(seed * 397 ^ index * 7919));
        var chosen = options.Count == 0 ? null : options[random.Next(options.Count)];
        mirror = cue.allowHorizontalMirror && random.Next(2) == 1;
        return chosen;
    }
    private static void Record(EnemyEncounterTimeline timeline, string action)
    {
        if (!timeline) throw new ArgumentNullException(nameof(timeline));
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit encounter assets outside Play Mode.");
        Undo.RegisterCompleteObjectUndo(timeline, action);
    }
    internal static int Add(EnemyEncounterTimeline timeline, EnemyFormation formation, float arrival, float snap)
    {
        if (!formation) throw new ArgumentNullException(nameof(formation));
        Record(timeline, "Add encounter");
        timeline.cues.Add(new EnemyEncounterTimeline.Cue { label = formation.name, formation = formation,
            arrivalSeconds = Snap(arrival, snap), allowedLateness = 3 });
        EditorUtility.SetDirty(timeline); return timeline.cues.Count - 1;
    }
    internal static void Move(EnemyEncounterTimeline timeline, int index, float arrival, float snap)
    {
        float value = Snap(arrival, snap);
        if (Mathf.Approximately(value, timeline.cues[index].arrivalSeconds)) return;
        Record(timeline, "Move encounter"); timeline.cues[index].arrivalSeconds = value; EditorUtility.SetDirty(timeline);
    }
    internal static int Duplicate(EnemyEncounterTimeline timeline, int index)
    {
        Record(timeline, "Duplicate encounter"); var cue = timeline.cues[index];
        // Append, rather than insert/sort, so existing seeded cues do not change identity.
        timeline.cues.Add(new EnemyEncounterTimeline.Cue { label = cue.label + " copy", formation = cue.formation,
            arrivalSeconds = cue.arrivalSeconds + 5, allowedLateness = cue.allowedLateness,
            allowHorizontalMirror = cue.allowHorizontalMirror, flipWallOrientation = cue.flipWallOrientation, fallback = cue.fallback,
            overrideSpawnPolicy = cue.overrideSpawnPolicy, integrity = cue.integrity,
            maxPositionAdjustment = cue.maxPositionAdjustment, blockedSlotGrace = cue.blockedSlotGrace,
            scaling = cue.scaling, reinforcementPairs = cue.reinforcementPairs,
            placementOverrides = cue.placementOverrides == null ? new() : cue.placementOverrides.Where(p => p != null).Select(p => p.Copy()).ToList(),
            variants = cue.variants == null ? new() : new(cue.variants) });
        EditorUtility.SetDirty(timeline); return timeline.cues.Count - 1;
    }
    internal static void Remove(EnemyEncounterTimeline timeline, int index)
    { Record(timeline, "Remove encounter"); timeline.cues.RemoveAt(index); EditorUtility.SetDirty(timeline); }

    internal readonly struct Issue
    {
        public readonly int cue;
        public readonly string message;
        public Issue(int cue, string message) { this.cue = cue; this.message = message; }
    }
    internal static List<Issue> Validate(EnemyEncounterTimeline timeline, EnemyArenaLayout layout, EnemyDirector director, int seed, bool twoVTwo = false)
    {
        var issues = new List<Issue>(); if (!timeline) return issues;
        var limits = EnemyEncounterScaling.Limits.Capture(timeline, twoVTwo);
        float scale = director ? (Application.isPlaying && director.encounterTimeline == timeline
            ? director.TimelineTimeScale : director.PreviewTimelineTimeScale(timeline)) : 1f;
        float end = Mathf.Max(1f, timeline.duration) * scale;
        var chosen = new List<(int index, EnemyFormation formation, bool mirror, float arrival)>();
        for (int i = 0; i < timeline.cues.Count; i++)
        {
            var cue = timeline.cues[i];
            var formation = Choose(timeline, i, seed, out bool mirror);
            void Add(string message) => issues.Add(new Issue(i, message));
            if (!formation || formation.slots.Count == 0) { Add("Missing or empty formation."); continue; }
            foreach (var group in formation.slots.Where(s => s != null && s.releaseGroup > 0).GroupBy(s => s.releaseGroup))
                if (group.Any(s => !Mathf.Approximately(s.releaseDelay, group.First().releaseDelay)))
                    Add("Linked release group " + group.Key + " should use equal release delays.");
            float warning = Mathf.Max(.1f, formation.warningSeconds);
            float arrival = cue.arrivalSeconds * scale;
            if (arrival < warning) Add("Warning begins before 0s; the Director will delay or skip this cue.");
            if (arrival + LastDelay(formation) > end) Add("Last spawn is after the timeline duration.");
            var budget = new EnemyPopulationBudget();
            var poses = new List<(EnemyArenaLayout.Pose pose, float radius)>();
            for (int slotIndex = 0; slotIndex < formation.slots.Count; slotIndex++)
            {
                var slot = cue.GetSlot(formation, slotIndex);
                if (slot == null || !slot.enemy || !slot.enemy.prefab || !slot.telegraph) { Add("Slot is missing an enemy, prefab or telegraph."); continue; }
                budget.Add(timeline, slot.enemy, EnemyPopulationBudget.Kind.Requested);
                if (!layout) continue;
                float radius = Mathf.Max(director ? director.spawnCheckRadiusWorld : 0, slot.enemy.GetSpawnRadiusWorld());
                if (!layout.Resolve(slot, mirror, out var pose, out var reason, cue.flipWallOrientation)) { Add(reason); continue; }
                if (!layout.Clear(pose, radius, director ? director.borderBufferWorld : 0, out reason))
                {
                    bool rangeFits = false;
                    if (pose.socket != null && pose.socket.useRange)
                        for (int sample = 0; sample <= 64 && !rangeFits; sample++)
                            rangeFits = layout.Clear(layout.WallPose(pose.socket, sample / 64f), radius, 0, out _);
                    if (!rangeFits) Add(reason);
                }
                if (poses.Any(p => (p.pose.clearance - pose.clearance).sqrMagnitude < Mathf.Pow(p.radius + radius, 2)))
                    Add(pose.socket != null && pose.socket.useRange ? "Preferred wall footprints overlap; runtime will search their ranges." : "Formation footprints overlap.");
                poses.Add((pose, radius));
            }
            if (!budget.Allows(timeline, out var budgetReason, limits)) Add("Batch cannot fit: " + budgetReason);
            var extras = EnemyEncounterScaling.Reinforcements(timeline, cue, formation, twoVTwo);
            foreach (var extra in extras) budget.Add(timeline, extra.enemy, EnemyPopulationBudget.Kind.Requested);
            if (extras.Count > 0 && !budget.Allows(timeline, out var extraReason, limits)) Add("Some optional 2v2 pairs will be skipped: " + extraReason);
            if (extras.Count > 0 && arrival + extras.Max(s => s.releaseDelay) > end) Add("Optional 2v2 arrivals extend beyond the timeline duration.");
            chosen.Add((i, formation, mirror, arrival));
        }
        // Predict only authored reservation conflicts; surviving enemies require the live simulation.
        for (int a = 0; a < chosen.Count; a++) for (int b = a + 1; b < chosen.Count; b++)
        {
            var x = chosen[a]; var y = chosen[b];
            if (Mathf.Max(x.arrival - x.formation.warningSeconds, y.arrival - y.formation.warningSeconds) >
                Mathf.Min(x.arrival + LastDelay(x.formation), y.arrival + LastDelay(y.formation))) continue;
            bool overlap = false;
            if (layout) for (int pIndex = 0; pIndex < x.formation.slots.Count; pIndex++) for (int qIndex = 0; qIndex < y.formation.slots.Count; qIndex++)
            {
                var p = timeline.cues[x.index].GetSlot(x.formation, pIndex);
                var q = timeline.cues[y.index].GetSlot(y.formation, qIndex);
                if (p == null || q == null || !p.enemy || !q.enemy ||
                    !layout.Resolve(p, x.mirror, out var pp, out _, timeline.cues[x.index].flipWallOrientation) ||
                    !layout.Resolve(q, y.mirror, out var qq, out _, timeline.cues[y.index].flipWallOrientation)) continue;
                float minimum = director ? director.spawnCheckRadiusWorld : 0;
                float radius = Mathf.Max(minimum, p.enemy.GetSpawnRadiusWorld()) + Mathf.Max(minimum, q.enemy.GetSpawnRadiusWorld());
                if ((pp.clearance - qq.clearance).sqrMagnitude < radius * radius) overlap = true;
            }
            if (overlap)
            {
                issues.Add(new Issue(x.index, "Possible reservation conflict with " + timeline.cues[y.index].label));
                issues.Add(new Issue(y.index, "Possible reservation conflict with " + timeline.cues[x.index].label));
            }
        }
        return issues.GroupBy(i => (i.cue, i.message)).Select(g => g.First()).ToList();
    }
}
#endif
