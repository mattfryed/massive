using UnityEngine;

namespace Massive.Cosmos
{
    // Bake once in the Editor so opening Level Select never generates the Voronoi web.
    [PreferBinarySerialization]
    public sealed class CosmicWebIconData : ScriptableObject
    {
        public int seed;
        public float organicStrength;
        [HideInInspector] public CosmicWebTopology.Particle[] particles;
        public int Count => particles != null ? particles.Length : 0;
    }
}
