using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Massive.Enemies
{
    /// <summary>Shared timeline accounting for runtime admission and Composer's batch checks.</summary>
    public sealed class EnemyPopulationBudget
    {
        public enum Kind { Alive, Reserved, Requested }
        public struct Count
        {
            public int alive, reserved, requested;
            public int Total => alive + reserved + requested;
            public override string ToString() => $"{alive} alive + {reserved} reserved + {requested} requested";
        }
        private readonly Dictionary<EnemyDefinition, Count> types = new();
        public Count Total { get; private set; }
        public float AlivePressure { get; private set; }
        public float ReservedPressure { get; private set; }
        public float RequestedPressure { get; private set; }
        public float Pressure => AlivePressure + ReservedPressure + RequestedPressure;
        public Count For(EnemyDefinition enemy) => enemy && types.TryGetValue(enemy, out var value) ? value : default;
        public void Add(EnemyEncounterTimeline timeline, EnemyDefinition enemy, Kind kind)
        {
            if (!enemy) return;
            var count = For(enemy); var total = Total; float cost = timeline.Cost(enemy);
            switch (kind)
            {
                case Kind.Alive: count.alive++; total.alive++; AlivePressure += cost; break;
                case Kind.Reserved: count.reserved++; total.reserved++; ReservedPressure += cost; break;
                case Kind.Requested: count.requested++; total.requested++; RequestedPressure += cost; break;
            }
            types[enemy] = count; Total = total;
        }
        public bool Allows(EnemyEncounterTimeline timeline, out string reason)
        {
            reason = null;
            if (timeline.maxAliveTotal > 0 && Total.Total > timeline.maxAliveTotal)
            { reason = $"Total limit {timeline.maxAliveTotal}: {Total}"; return false; }
            foreach (var pair in types)
            {
                int limit = timeline.Limit(pair.Key);
                if (limit > 0 && pair.Value.Total > limit)
                { reason = $"{Name(pair.Key)} limit {limit}: {pair.Value}"; return false; }
            }
            if (timeline.maxPressure > 0 && Pressure > timeline.maxPressure + .001f)
            {
                reason = $"Pressure limit {timeline.maxPressure:0.##}: {AlivePressure:0.##} alive + {ReservedPressure:0.##} reserved + {RequestedPressure:0.##} requested";
                return false;
            }
            return true;
        }
        public static string Name(EnemyDefinition enemy) => Regex.Replace(
            (enemy.name.StartsWith("ED_") ? enemy.name.Substring(3) : enemy.name).Replace('_', ' '), "(?<=[a-z])(?=[A-Z])", " ");
    }
}
