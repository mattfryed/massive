using System.Collections.Generic;
using UnityEngine;

namespace Massive.Enemies
{
    public partial class EnemyDirector
    {
        private readonly Collider[] placementHits = new Collider[128];
        private static readonly Vector2[] offsetDirections = {
            Vector2.right, Vector2.left, Vector2.up, Vector2.down,
            new Vector2(1, 1).normalized, new Vector2(-1, 1).normalized,
            new Vector2(1, -1).normalized, new Vector2(-1, -1).normalized };

        private void UpdateSlotPose(FormationMember m)
        {
            m.status.position = m.pose.position; m.status.clearance = m.pose.clearance; m.status.rotation = m.pose.rotation;
            m.status.radius = Mathf.Max(spawnCheckRadiusWorld, m.slot.enemy.GetSpawnRadiusWorld());
            m.status.adjusted = (m.pose.position - m.authored.position).sqrMagnitude > .00001f;
        }
        private bool ClearInBatch(FormationMember m, List<FormationMember> batch, out string reason)
        {
            if (!MemberClear(m, m.cue, out reason)) return false;
            // Protect even the unresolved slots' intended space: a correction must not steal a later slot.
            foreach (var other in batch)
                if (other != m && Overlaps(m, other)) { reason = "Formation footprints overlap"; return false; }
            return true;
        }
        private void ResolvePlacement(List<FormationMember> batch, EnemyFormation formation, float maximum, bool flexible)
        {
            ResolveWallPlacement(batch);
            bool allClear = true;
            foreach (var m in batch)
            { m.placed = ClearInBatch(m, batch, out m.status.reason); allClear &= m.placed; }
            if (!allClear && maximum > 0)
            {
                if (!flexible) TryRigidPlacement(batch, batch, formation, maximum);
                else
                {
                    var groups = new Dictionary<int, List<FormationMember>>();
                    foreach (var m in batch)
                    {
                        int key = m.slot.placementGroup;
                        if (key == 0 && formation.adjustmentPattern == EnemyFormation.AdjustmentPattern.Rows)
                            key = Mathf.RoundToInt(m.slot.position.y * 10000);
                        if (!groups.TryGetValue(key, out var group)) groups.Add(key, group = new());
                        group.Add(m);
                    }
                    foreach (var group in groups.Values)
                        if (group.Exists(m => !m.placed)) TryRigidPlacement(group, batch, formation, maximum);
                    foreach (var m in batch) if (!m.placed) FindLocalPlacement(m, batch, formation, maximum);
                }
            }
            foreach (var m in batch)
            {
                m.placed = ClearInBatch(m, batch, out m.status.reason);
                m.status.state = m.placed ? "Reserved" : "Blocked"; UpdateSlotPose(m);
            }
        }
        private Vector3 Offset(Vector2 direction, float distance) => arenaLayout.Direction(direction) * distance;

        private bool TryRigidPlacement(List<FormationMember> group, List<FormationMember> batch, EnemyFormation formation, float maximum)
        {
            if (group.Exists(m => m.pose.socket != null || m.status.announced)) return false;
            var previous = new EnemyArenaLayout.Pose[group.Count];
            Vector3 center = Vector3.zero;
            for (int i = 0; i < group.Count; i++) { previous[i] = group[i].pose; center += group[i].authored.position; }
            center /= group.Count;
            bool Test(Vector3 offset, float angle)
            {
                var rotation = Quaternion.AngleAxis(angle, Vector3.up);
                foreach (var m in group)
                {
                    m.pose = m.authored;
                    m.pose.position = center + rotation * (m.authored.position - center) + offset;
                    m.pose.clearance = center + rotation * (m.authored.clearance - center) + offset;
                    m.pose.rotation = rotation * m.authored.rotation;
                }
                foreach (var m in group)
                    if ((m.pose.position - m.authored.position).sqrMagnitude > maximum * maximum + .0001f || !ClearInBatch(m, batch, out _)) return false;
                foreach (var m in group) m.placed = true;
                return true;
            }
            if (formation.adjustmentPattern == EnemyFormation.AdjustmentPattern.Ring)
            {
                float radius = 0;
                foreach (var m in group) radius = Mathf.Max(radius, (m.authored.position - center).magnitude);
                float angle = 2 * Mathf.Asin(Mathf.Min(1, maximum / Mathf.Max(.001f, 2 * radius))) * Mathf.Rad2Deg;
                for (int step = 1; step <= 4; step++)
                    if (Test(Vector3.zero, angle * step / 4) || Test(Vector3.zero, -angle * step / 4)) return true;
            }
            for (int step = 1; step <= 4; step++) foreach (var direction in offsetDirections)
                if (Test(Offset(direction, maximum * step / 4), 0)) return true;
            for (int i = 0; i < group.Count; i++) group[i].pose = previous[i];
            return false;
        }
        private bool FindLocalPlacement(FormationMember m, List<FormationMember> batch, EnemyFormation formation, float maximum)
        {
            if (ClearInBatch(m, batch, out m.status.reason)) { m.placed = true; return true; }
            if (maximum <= 0 || m.pose.socket != null || m.status.announced) { m.placed = false; return false; }
            var previous = m.pose;
            Vector3 center = Vector3.zero;
            var policy = encounterTimeline.cues[m.cue.index];
            for (int i = 0; i < formation.slots.Count; i++)
                if (arenaLayout.Resolve(policy.GetSlot(formation, i), m.cue.mirror, out var pose, out _, policy.flipWallOrientation)) center += pose.position;
            center /= formation.slots.Count;
            // Ring corrections prefer the tangent/radius; rows prefer the arena's horizontal axis.
            Vector3 radial = m.authored.position - center; radial.y = 0;
            Vector3 tangent = Vector3.Cross(Vector3.up, radial.normalized);
            for (int step = 1; step <= 4; step++) foreach (var direction in offsetDirections)
            {
                Vector3 offset = formation.adjustmentPattern == EnemyFormation.AdjustmentPattern.Ring && radial.sqrMagnitude > .001f
                    ? (tangent * direction.x + radial.normalized * direction.y) * (maximum * step / 4)
                    : Offset(direction, maximum * step / 4);
                m.pose = m.authored; m.pose.position += offset; m.pose.clearance += offset;
                if (ClearInBatch(m, batch, out _)) { m.placed = true; m.status.reason = null; UpdateSlotPose(m); return true; }
            }
            m.pose = previous; m.placed = false; UpdateSlotPose(m); return false;
        }
    }
}
