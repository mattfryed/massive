using System.Collections.Generic;
using UnityEngine;

namespace Massive.Enemies
{
    public partial class EnemyDirector
    {
        private void BuildFlexibleUnits(CueStatus cue, List<FormationMember> proposed, EnemyFormation formation)
        {
            var links = new Dictionary<int, FormationUnit>();
            foreach (var m in proposed)
            {
                FormationUnit unit;
                if (m.slot.releaseGroup <= 0 || !links.TryGetValue(m.slot.releaseGroup, out unit))
                {
                    unit = new FormationUnit { arrival = m.arrival, warningAt = m.warningAt, deadline = m.deadline };
                    cue.units.Add(unit); if (m.slot.releaseGroup > 0) links.Add(m.slot.releaseGroup, unit);
                }
                unit.members.Add(m);
                unit.releaseDelay = Mathf.Max(unit.releaseDelay, Mathf.Max(0, m.slot.releaseDelay));
                unit.arrival = Mathf.Max(unit.arrival, m.arrival);
                unit.warningAt = unit.arrival - Mathf.Max(.1f, formation.warningSeconds);
                unit.deadline = Mathf.Min(unit.deadline, m.deadline);
            }
        }
        private bool UnitClear(FormationUnit unit, List<FormationMember> batch, out string reason)
        {
            reason = null; bool clear = true;
            foreach (var m in unit.members)
            {
                bool valid = ClearInBatch(m, batch, out string problem);
                m.status.reason = problem;
                if (!valid) { m.status.state = "Blocked"; reason ??= problem; clear = false; }
                else m.status.state = unit.announced ? "Warning" : "Reserved";
            }
            return clear;
        }
        private void SkipUnit(CueStatus cue, FormationUnit unit, string reason)
        {
            foreach (var m in unit.members)
            {
                if (m.warning) m.warning.Cancel(); members.Remove(m);
                m.status.state = "Skipped"; m.status.reason = reason; cue.skipped++;
            }
            unit.finished = true;
        }
        private void PreserveFollowingCadence(CueStatus cue, FormationUnit released, float arrival)
        {
            // Obstructed slots have independent retry schedules and cannot hold the clear sequence.
            if (released.wasBlocked) return;
            foreach (var later in cue.units)
            {
                float gap = later.releaseDelay - released.releaseDelay;
                if (later.finished || later.wasBlocked || gap <= .001f) continue;
                float next = Mathf.Max(later.arrival, arrival + gap);
                if (!later.announced) later.warningAt += next - later.arrival;
                later.arrival = next;
                foreach (var m in later.members) m.arrival = next;
            }
        }
        private void TickFlexibleFormation(CueStatus cue)
        {
            var policy = encounterTimeline.cues[cue.index];
            float warning = Mathf.Max(.1f, cue.formation.warningSeconds);
            float earliest = float.PositiveInfinity;
            string blockedReason = null;
            var batch = members.FindAll(m => m.cue == cue);
            // A blocked slot owns its deadline. It never holds unrelated release times hostage.
            foreach (var unit in cue.units)
            {
                if (unit.finished || GameplayAge < unit.warningAt) continue;
                if (GameplayAge > unit.deadline + .02f)
                { SkipUnit(cue, unit, unit.reason ?? "Slot deadline passed"); continue; }
                if (GameplayAge < unit.nextAttempt) { if (unit.blocked) blockedReason ??= unit.reason; continue; }
                if (!unit.announced)
                {
                    if (GameplayAge + warning > unit.deadline + .02f)
                    { SkipUnit(cue, unit, unit.reason ?? "Insufficient warning lead"); continue; }
                    if (!UnitClear(unit, batch, out _))
                    {
                        if (unit.members.Count > 1) TryRigidPlacement(unit.members, batch, cue.formation, policy.AdjustmentFor(cue.formation));
                        foreach (var m in unit.members) FindLocalPlacement(m, batch, cue.formation, policy.AdjustmentFor(cue.formation));
                    }
                    if (!UnitClear(unit, batch, out string reason))
                    {
                        unit.blocked = unit.wasBlocked = true; unit.reason = reason; blockedReason ??= reason; unit.nextAttempt = GameplayAge + .25f;
                        foreach (var m in unit.members) m.placed = false;
                        continue;
                    }
                    unit.arrival = Mathf.Max(unit.arrival, GameplayAge + warning); unit.announced = true; unit.blocked = false; unit.reason = null;
                    PreserveFollowingCadence(cue, unit, unit.arrival);
                    foreach (var m in unit.members)
                    {
                        m.placed = true; m.arrival = unit.arrival; UpdateSlotPose(m);
                        m.warning = Instantiate(m.slot.telegraph, m.pose.position, m.pose.rotation, transform);
                        m.warning.Begin(warning, SpawnWorldScale(m.slot.enemy), SpawnOutline(m.slot.enemy)); _telegraphs.Add(m.warning);
                        m.status.announced = true; m.status.state = "Warning"; m.status.reason = null;
                    }
                }
                if (GameplayAge + .00001f < unit.arrival) continue;
                // Once announced, these exact positions are immutable, even if actors move into them.
                if (!UnitClear(unit, batch, out string obstruction))
                { unit.blocked = unit.wasBlocked = true; unit.reason = obstruction; blockedReason ??= obstruction; unit.nextAttempt = GameplayAge + .1f; continue; }
                if (!TimelineBudgetAllows(null, null, out string budget))
                {
                    unit.blocked = unit.wasBlocked = true; unit.reason = budget; blockedReason ??= budget; unit.nextAttempt = GameplayAge + .1f;
                    foreach (var m in unit.members) { m.status.state = "Blocked"; m.status.reason = budget; }
                    continue;
                }
                // Time spent obstructed belongs to this unit, not to the remaining sequence.
                // Only frame drift on an otherwise-ready release may shift later staggering.
                if (unit.blocked) unit.arrival = Mathf.Max(unit.arrival, GameplayAge);
                unit.blocked = false; unit.reason = null; earliest = Mathf.Min(earliest, unit.arrival);
            }
            if (!float.IsPositiveInfinity(earliest))
            {
                foreach (var unit in cue.units)
                {
                    if (unit.finished || unit.blocked) continue;
                    if (unit.arrival > earliest + .001f) continue;
                    if (!unit.announced || GameplayAge < unit.arrival) continue;
                    PreserveFollowingCadence(cue, unit, GameplayAge);
                    foreach (var m in unit.members)
                    {
                        members.Remove(m); SpawnFormationMember(m); cue.spawned++;
                        m.status.state = "Spawned"; m.status.reason = null;
                        if (m.warning) m.warning.Complete();
                    }
                    unit.finished = true; cue.lastRelease = GameplayAge;
                }
            }
            bool pending = cue.units.Exists(u => !u.finished);
            cue.reason = blockedReason;
            if (!pending)
                FinishCue(cue, cue.skipped > 0 ? cue.spawned > 0 ? "Partial" : "Skipped" : "Complete",
                    cue.skipped > 0 ? $"{cue.skipped} slots skipped; inspect slot reasons" : null);
            else cue.state = blockedReason != null ? "Holding" : cue.spawned > 0 ? "Releasing" : "Reserved";
        }
    }
}
