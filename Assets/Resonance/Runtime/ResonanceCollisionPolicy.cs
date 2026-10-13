using System.Collections.Generic;
using UnityEngine;

namespace Massive.Resonance
{
    /// <summary>Physics-only contact layers; never changes the global collision matrix.</summary>
    public static class ResonanceCollisionPolicy
    {
        public const string LayerName = "ResonanceEnemyPassThrough";
        public const string ActorLayerName = "ResonanceActorPassThrough";
        public static int Layer => LayerMask.NameToLayer(LayerName);
        public static int ActorLayer => LayerMask.NameToLayer(ActorLayerName);
        public static int ActorMask { get { int layer = ActorLayer; return layer < 0 ? 0 : 1 << layer; } }
        public static int Mask { get { int layer = Layer; return (layer < 0 ? 0 : 1 << layer) | ActorMask; } }
        public static int EnemyQueryMask(int authoredMask) => authoredMask & ~Mask;

        // Custom layers and matrix differences retain native pair filtering.
        internal static int ResolveLayer(int obstacleLayer, bool enemiesUseSludge) =>
            enemiesUseSludge ? ResolveEquivalentLayer(obstacleLayer, Layer) : -1;
        internal static int ResolveActorLayer(int obstacleLayer) => ResolveEquivalentLayer(obstacleLayer, ActorLayer);
        private static int ResolveEquivalentLayer(int obstacleLayer, int layer)
        {
            if (obstacleLayer != 0 || layer < 0) return -1;
            for (int other = 0; other < 32; other++)
                if (Physics.GetIgnoreLayerCollision(obstacleLayer, other) != Physics.GetIgnoreLayerCollision(layer, other))
                    return -1;
            return layer;
        }

        private sealed class Exclusion { public int users, ownedMask; }
        private static readonly Dictionary<Collider, Exclusion> exclusions = new Dictionary<Collider, Exclusion>();
        internal static void Acquire(Collider collider)
        {
            if (!exclusions.TryGetValue(collider, out var entry))
            {
                entry = new Exclusion { ownedMask = ActorMask & ~collider.excludeLayers.value };
                exclusions.Add(collider, entry);
                collider.excludeLayers = collider.excludeLayers.value | entry.ownedMask;
            }
            entry.users++;
        }
        internal static void Release(Collider collider)
        {
            // A destroyed Unity object remains the dictionary key until its last owner releases it.
            if (ReferenceEquals(collider, null) || !exclusions.TryGetValue(collider, out var entry)) return;
            if (--entry.users != 0) return;
            if (collider != null) collider.excludeLayers = collider.excludeLayers.value & ~entry.ownedMask;
            exclusions.Remove(collider);
        }
    }
}
