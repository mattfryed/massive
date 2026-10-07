using System.Collections.Generic;
using UnityEngine;

namespace Massive.Enemies
{
    public partial class EnemyDirector
    {
        // Bounded, deterministic search. Equal fractions on center-to-corner ranges
        // give mirrored wall positions, including the oval arena's curved boundary.
        private const int WallSearchSteps = 64;
        private void ResolveWallPlacement(List<FormationMember> batch)
        {
            var movable = new List<FormationMember>();
            FormationMember anchor = null;
            bool allClear = true;
            foreach (var m in batch)
            {
                if (m.pose.socket == null || !m.pose.socket.useRange) continue;
                if (m.status.announced) { anchor ??= m; continue; }
                movable.Add(m); allClear &= ClearInBatch(m, batch, out _);
            }
            if (movable.Count == 0 || allClear) return;
            var previous = new EnemyArenaLayout.Pose[movable.Count];
            for (int i = 0; i < movable.Count; i++) previous[i] = movable[i].pose;

            bool Together(float fraction)
            {
                foreach (var m in movable) m.pose = arenaLayout.WallPose(m.pose.socket, fraction);
                foreach (var m in movable) if (!ClearInBatch(m, batch, out _)) return false;
                return true;
            }
            float preferred = anchor != null ? anchor.pose.wallFraction : movable[0].authored.wallFraction;
            bool found = Together(preferred);
            if (!found && anchor == null)
                foreach (float fraction in WallCandidates(preferred))
                    if (Together(fraction)) { found = true; break; }
            if (!found)
            {
                for (int i = 0; i < movable.Count; i++) movable[i].pose = previous[i];
                // Symmetry is a preference: one obstructed quadrant must not cancel
                // a clear quadrant, or prevent a useful asymmetric pair.
                foreach (var m in movable)
                {
                    if (ClearInBatch(m, batch, out _)) continue;
                    var original = m.pose;
                    foreach (float fraction in WallCandidates(m.authored.wallFraction))
                    {
                        m.pose = arenaLayout.WallPose(original.socket, fraction);
                        if (ClearInBatch(m, batch, out _)) break;
                        m.pose = original;
                    }
                }
            }
            foreach (var m in movable)
            {
                m.placed = ClearInBatch(m, batch, out m.status.reason);
                UpdateSlotPose(m);
            }
        }

        private static IEnumerable<float> WallCandidates(float preferred)
        {
            yield return Mathf.Clamp01(preferred);
            // Start nearest the preferred point, then expand in both directions.
            int center = Mathf.RoundToInt(preferred * WallSearchSteps);
            for (int offset = 0; offset <= WallSearchSteps; offset++)
            {
                int lower = center - offset, upper = center + offset;
                if (lower >= 0) yield return lower / (float)WallSearchSteps;
                if (offset > 0 && upper <= WallSearchSteps) yield return upper / (float)WallSearchSteps;
            }
        }
    }
}
