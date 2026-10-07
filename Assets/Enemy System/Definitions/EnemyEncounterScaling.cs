using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Massive.Enemies
{
    /// <summary>One deterministic expansion shared by the Composer and Director. Never edits authored slots.</summary>
    public static class EnemyEncounterScaling
    {
        public enum CuePolicy { Inherit, Disable, Override }
        [Serializable] public sealed class Profile
        {
            public bool enabled = true;
            [Range(0, 1)] public float droneExtraFraction = .5f;
            [Range(0, 1)] public float rangedExtraFraction = .25f;
            [Min(0)] public int seekerExtraPairs = 1, dysonExtraPairs = 1;
            public bool completeTurretQuadrants = true;
            [Range(0, 1), Tooltip("Inherited Seeker, Dyson and turret reinforcements start this far through the authored timeline.")]
            public float advancedFrom = .5f;
            [Min(1)] public float totalLimitMultiplier = 1.5f, typeLimitMultiplier = 1.5f, pressureLimitMultiplier = 1.5f;
            [Range(0, 3), Tooltip("Extra slots expire this long after their scheduled arrival. Full warnings are always required.")]
            public float reinforcementGrace = 1f;
        }

        public sealed class Limits
        {
            public int total;
            public float pressure;
            readonly Dictionary<EnemyDefinition, int> types = new();
            public int For(EnemyDefinition enemy) => enemy && types.TryGetValue(enemy, out int cap) ? cap : 0;
            public static Limits Capture(EnemyEncounterTimeline timeline, bool twoVTwo)
            {
                var profile = twoVTwo && timeline.twoVTwo != null && timeline.twoVTwo.enabled ? timeline.twoVTwo : null;
                var result = new Limits {
                    total = Scale(timeline.maxAliveTotal, profile?.totalLimitMultiplier ?? 1),
                    pressure = Mathf.Max(0, timeline.maxPressure) * Mathf.Max(1, profile?.pressureLimitMultiplier ?? 1)
                };
                foreach (var entry in timeline.populationLimits)
                    if (entry != null && entry.enemy) result.types[entry.enemy] = Scale(timeline.Limit(entry.enemy), profile?.typeLimitMultiplier ?? 1);
                return result;
            }
            static int Scale(int value, float multiplier) => value <= 0 ? 0 : Mathf.CeilToInt(value * Mathf.Max(1, multiplier));
        }

        public static List<EnemyFormation.Slot> Reinforcements(EnemyEncounterTimeline timeline,
            EnemyEncounterTimeline.Cue cue, EnemyFormation formation, bool twoVTwo)
        {
            var result = new List<EnemyFormation.Slot>();
            var profile = timeline ? timeline.twoVTwo : null;
            if (!twoVTwo || profile == null || !profile.enabled || cue.scaling == CuePolicy.Disable || !formation) return result;
            var original = Enumerable.Range(0, formation.slots.Count).Select(i => cue.GetSlot(formation, i)).ToList();
            if (original.Count < 2 || original.Any(s => s == null || !s.enemy || !s.enemy.prefab)) return result;
            var enemy = original[0].enemy;
            if (original.Any(s => s.enemy != enemy || s.region != original[0].region)) return result;
            var prefab = enemy.prefab;
            bool turret = prefab.GetComponent<ParticleBeamTurretController>();
            int pairs = 0;
            if (cue.scaling == CuePolicy.Override) pairs = Mathf.Clamp(cue.reinforcementPairs, 0, 32);
            else if (prefab.GetComponent<RangedDroneController>()) pairs = Mathf.FloorToInt(original.Count * Mathf.Clamp01(profile.rangedExtraFraction) / 2);
            else if (prefab.GetComponent<DroneController>()) pairs = Mathf.FloorToInt(original.Count * Mathf.Clamp01(profile.droneExtraFraction) / 2);
            else if (cue.arrivalSeconds / Mathf.Max(1, timeline.duration) >= profile.advancedFrom)
            {
                if (prefab.GetComponent<SeekerController>()) pairs = profile.seekerExtraPairs;
                else if (prefab.GetComponent<DysonSphereRepulsorController>()) pairs = profile.dysonExtraPairs;
                else if (turret && profile.completeTurretQuadrants) pairs = 1;
            }
            pairs = Mathf.Clamp(pairs, 0, 32);
            if (pairs == 0) return result;

            // Extras follow the original release sequence at its existing cadence; paired sides share a release.
            var delays = original.Select(s => s.releaseDelay).Distinct().OrderBy(x => x).ToArray();
            float step = delays.Length > 1 ? delays.Zip(delays.Skip(1), (a, b) => b - a).Where(x => x > .001f).DefaultIfEmpty(.25f).Min() : .25f;
            float last = Mathf.Max(0, delays.Last());
            void Pair(EnemyFormation.Slot a, EnemyFormation.Slot b)
            {
                int group = result.Count / 2 + 1;
                a.releaseGroup = b.releaseGroup = group;
                a.releaseDelay = b.releaseDelay = last + step * group;
                result.Add(a); result.Add(b);
            }
            if (turret)
            {
                // Only complete an authored diagonal. Four-quadrant and custom mounts are already explicit.
                if (original.Count != 2) return result;
                var a = original[0].Copy(); var b = original[1].Copy();
                a.socket = OppositeWall(a.socket); b.socket = OppositeWall(b.socket);
                if (a.socket == null || b.socket == null || a.socket == b.socket || original.Any(s => s.socket == a.socket || s.socket == b.socket)) return result;
                Pair(a, b); return result;
            }
            if (original.Any(s => !string.IsNullOrEmpty(s.socket))) return result;

            if (formation.adjustmentPattern == EnemyFormation.AdjustmentPattern.Rows)
            {
                var rows = original.Select((slot, i) => (slot, row: Mathf.RoundToInt(formation.slots[i].position.y * 1000)))
                    .GroupBy(s => s.row).OrderBy(g => g.Key).Select(g => g.Select(s => s.slot).ToArray()).ToArray();
                if (rows.Length != 2 || rows[0].Count() != rows[1].Count()) return result;
                var a = rows[0].OrderBy(s => s.position.x).ToArray(); var b = rows[1].OrderBy(s => s.position.x).ToArray();
                int count = Mathf.Min(pairs, a.Length - 1);
                // Evenly distribute insertions across the row, then announce them from the middle outward.
                var gaps = Enumerable.Range(0, count).Select(i => Mathf.FloorToInt((i + .5f) * (a.Length - 1) / count))
                    .OrderBy(i => Mathf.Abs(i - (a.Length - 2) * .5f)).ToArray();
                foreach (int i in gaps)
                { var x = a[i].Copy(); var y = b[i].Copy(); x.position = (a[i].position + a[i + 1].position) * .5f; y.position = (b[i].position + b[i + 1].position) * .5f; Pair(x, y); }
            }
            else if (formation.adjustmentPattern == EnemyFormation.AdjustmentPattern.Ring)
            {
                Vector2 center = Vector2.zero; foreach (var s in original) center += s.position; center /= original.Count;
                var ring = original.OrderBy(s => Mathf.Atan2(s.position.y - center.y, s.position.x - center.x)).ToArray();
                if (ring.Length % 2 != 0) return result;
                var used = new List<Vector2>();
                Vector2 Between(int i)
                {
                    var a = ring[i].position - center; var b = ring[(i + 1) % ring.Length].position - center;
                    // Project the chord midpoint back onto the ellipse; includes per-cue position edits.
                    float angle = Vector2.Angle(ring[i].facing, ring[(i + 1) % ring.Length].facing) * Mathf.Deg2Rad;
                    return center + (a + b) * .5f / Mathf.Max(.5f, Mathf.Cos(angle * .5f));
                }
                var candidates = Enumerable.Range(0, ring.Length / 2).ToList();
                while (candidates.Count > 0 && result.Count < pairs * 2)
                {
                    int i = candidates.OrderByDescending(c => used.Count == 0 ? 1 : used.Min(p => (p - Between(c)).sqrMagnitude)).First(); candidates.Remove(i);
                    int j = i + ring.Length / 2; var a = ring[i].Copy(); var b = ring[j].Copy();
                    a.position = Between(i); b.position = Between(j);
                    a.facing = (ring[i].facing + ring[(i + 1) % ring.Length].facing).normalized;
                    b.facing = (ring[j].facing + ring[(j + 1) % ring.Length].facing).normalized;
                    Pair(a, b); used.Add(a.position); used.Add(b.position);
                }
            }
            else if (formation.adjustmentPattern == EnemyFormation.AdjustmentPattern.Groups)
            {
                var groups = original.GroupBy(s => s.placementGroup).OrderBy(g => g.Key).ToArray();
                if (groups.Length != 2) return result;
                var rows = new EnemyFormation.Slot[2][];
                for (int side = 0; side < 2; side++)
                {
                    var group = groups[side].ToArray(); Vector2 forward = group[0].facing.normalized, across = new(-forward.y, forward.x);
                    var depths = group.Select(s => Vector2.Dot(s.position, forward)).OrderBy(x => x).ToArray();
                    float back = depths[0]; float depthStep = depths.Where(x => x > back + .001f).DefaultIfEmpty(back + .06f).First() - back;
                    var backRow = group.Where(s => Mathf.Abs(Vector2.Dot(s.position, forward) - back) < .001f).ToArray();
                    float lateral = group.Average(s => Vector2.Dot(s.position, across));
                    var widths = backRow.Select(s => Vector2.Dot(s.position, across)).OrderBy(x => x).ToArray();
                    float spacing = widths.Length > 1 ? widths[1] - widths[0] : .1f;
                    rows[side] = Enumerable.Range(0, pairs).Select(i => {
                        var s = group[0].Copy(); s.position = forward * (back - depthStep) + across * (lateral + (i - (pairs - 1) * .5f) * spacing); return s;
                    }).ToArray();
                }
                for (int i = 0; i < pairs; i++) Pair(rows[0][i], rows[1][i]);
            }
            else
            {
                // Local formations add opposing flanks relative to the authored pair, never on top of it.
                var ordered = original.OrderBy(s => s.position.x).ToArray();
                for (int i = 0; i < pairs; i++)
                {
                    var a = ordered[0].Copy(); var b = ordered[^1].Copy();
                    a.position += Vector2.up * (.12f * (i + 1)); b.position -= Vector2.up * (.12f * (i + 1)); Pair(a, b);
                }
            }
            return result;
        }
        static string OppositeWall(string socket) => socket switch {
            "TopLeft" => "TopRight", "TopRight" => "TopLeft", "BottomLeft" => "BottomRight", "BottomRight" => "BottomLeft",
            "SideLeftTop" => "SideRightTop", "SideRightTop" => "SideLeftTop", "SideLeftBottom" => "SideRightBottom", "SideRightBottom" => "SideLeftBottom", _ => null
        };
    }
}
