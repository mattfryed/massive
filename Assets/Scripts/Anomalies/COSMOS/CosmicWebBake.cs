using UnityEngine;

namespace Massive.Cosmos
{
    /// <summary>Exact, serialized topology. Motion and presentation remain live in CosmicWebBackground.</summary>
    [PreferBinarySerialization]
    public sealed class CosmicWebBake : ScriptableObject
    {
        public const int FormatVersion = 1;
        [SerializeField] int formatVersion, count, seed;
        [SerializeField] float organicStrength;
        [SerializeField] string unityVersion, generatorFingerprint;
        [SerializeField] CosmicWebTopology.Statistics statistics;
        [SerializeField, HideInInspector] CosmicWebTopology.Particle[] particles;

        public bool Matches(int requestedCount, int requestedSeed, float requestedOrganicStrength)
            => formatVersion == FormatVersion && unityVersion == Application.unityVersion
                && count == Mathf.Clamp(requestedCount, 8000, 100000) && seed == requestedSeed
                && organicStrength.Equals(requestedOrganicStrength) && particles != null && particles.Length == count;

        public bool TryGet(int requestedCount, int requestedSeed, float requestedOrganicStrength,
            out CosmicWebTopology.Particle[] data, out CosmicWebTopology.Statistics stats)
        {
            bool valid = Matches(requestedCount, requestedSeed, requestedOrganicStrength);
            data = valid ? particles : null; stats = valid ? statistics : default;
            return valid;
        }

#if UNITY_EDITOR
        public bool MatchesGenerator(string fingerprint) => generatorFingerprint == fingerprint;
        public void SetData(int requestedCount, int requestedSeed, float requestedOrganicStrength,
            string fingerprint, CosmicWebTopology.Particle[] data, CosmicWebTopology.Statistics stats)
        {
            if (data == null || data.Length != Mathf.Clamp(requestedCount, 8000, 100000))
                throw new System.ArgumentException("Topology data does not match its bake settings.");
            formatVersion = FormatVersion; count = data.Length; seed = requestedSeed;
            organicStrength = requestedOrganicStrength; unityVersion = Application.unityVersion;
            generatorFingerprint = fingerprint; particles = data; statistics = stats;
        }
#endif
    }
}
