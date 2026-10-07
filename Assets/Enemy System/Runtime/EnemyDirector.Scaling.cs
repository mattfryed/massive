using System.Collections.Generic;
using UnityEngine;

namespace Massive.Enemies
{
    public partial class EnemyDirector
    {
        public bool TimelineIsTwoVTwo { get; private set; }
        public EnemyEncounterScaling.Limits TimelineLimits { get; private set; }
        readonly List<CueStatus> reinforcementQueue = new();

        private void ReserveReinforcements(CueStatus parent)
        {
            var policy = encounterTimeline.cues[parent.index];
            var slots = EnemyEncounterScaling.Reinforcements(encounterTimeline, policy, parent.formation, TimelineIsTwoVTwo);
            if (slots.Count == 0) return;
            var cue = parent.reinforcements = new CueStatus { index = parent.index, label = parent.label + " reinforcements",
                formation = parent.formation, arrival = parent.arrival, mirror = parent.mirror, optional = true, state = "Reserved" };
            float warning = Mathf.Max(.1f, cue.formation.warningSeconds);
            float grace = Mathf.Clamp(encounterTimeline.twoVTwo.reinforcementGrace, 0, 3);
            for (int i = 0; i < slots.Count; i += 2)
            {
                var pair = new List<FormationMember>(); string failure = null;
                for (int j = i; j < i + 2; j++)
                {
                    var slot = slots[j];
                    bool resolved = arenaLayout.Resolve(slot, cue.mirror, out var pose, out string reason, policy.flipWallOrientation);
                    if (!resolved) failure ??= reason;
                    var status = new SlotStatus { index = j, authored = pose.position }; cue.slots.Add(status);
                    var member = new FormationMember { cue = cue, slot = slot, pose = pose, authored = pose, status = status,
                        arrival = Mathf.Max(cue.arrival + slot.releaseDelay, GameplayAge + warning),
                        deadline = cue.arrival + slot.releaseDelay + grace };
                    member.warningAt = member.arrival - warning; UpdateSlotPose(member); pair.Add(member);
                }
                BuildFlexibleUnits(cue, pair, cue.formation);
                var unit = cue.units[^1];
                if (failure == null) TimelineBudgetAllows(pair, null, out failure);
                if (failure == null && unit.arrival > unit.deadline + .02f) failure = "Extra arrived too late for a full warning";
                if (failure == null)
                {
                    ResolvePlacement(pair, cue.formation, policy.AdjustmentFor(cue.formation), true);
                    foreach (var member in pair)
                    {
                        if (!member.placed) failure ??= member.status.reason;
                        // Even a temporarily blocked original slot keeps priority over an extra.
                        foreach (var original in members)
                            if (!original.cue.optional && Overlaps(member, original)) failure ??= "Original formation has priority";
                    }
                }
                if (failure != null) SkipUnit(cue, unit, "Optional pair skipped: " + failure);
                else members.AddRange(pair);
            }
        }
    }
}
