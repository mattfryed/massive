using UnityEngine;
using TMPro;
using Massive.Player;
using Massive.Multiplier;
using Massive.Resonance;
using Massive.Scoring;

namespace Massive.Settings
{
    [CreateAssetMenu(menuName = "MASSIVE/Shared Settings/ResonanceSharedProfile")]
    public sealed class ResonanceSharedProfile : SharedSettingsProfile
    {
        public ResonanceManifestationSettings formation = new ResonanceManifestationSettings();
        public AmplifierResonanceSpawnerSettings cycle = new AmplifierResonanceSpawnerSettings();
    }

    [System.Serializable]
    public sealed class ResonanceManifestationSettings
    {
        [Header("Sand-pattern formation")]
        [Min(0f)] public float spawnSeconds = 2.4f;
        [Min(0f)] public float despawnSeconds = 1.2f;
        [Tooltip("Lower values grow the grains earlier in the formation. Idle size is unchanged.")]
        [Range(.1f, 4f)] public float sizeGrowthPower = .65f;
        [Tooltip("Higher values keep the grains dispersed longer before they condense into the pattern.")]
        [Range(.1f, 4f)] public float condensationPower = 1.5f;
        [Header("Particle birth location")]
        [Tooltip("Local Band births each grain near its resting arc; Full Field retains the original rectangular scatter.")]
        public ResonanceBirthDistribution birthDistribution = ResonanceBirthDistribution.FullField;
        [Tooltip("Maximum initial offset from each grain's idle position, in pattern-local units. Vibration is a separate small additional motion.")]
        [Min(0f)] public float localSpawnSpread = .55f;
        [Tooltip("Fraction of Spawn Spread along the arc. Lower values favor a narrow band across its surface.")]
        [Range(0f, 1f)] public float alongArcSpread = .25f;
        [Tooltip("Blend from individual grain jitter to a shared standing-wave vibration around the pattern origin. Visual only; does not simulate or deform the grid.")]
        [Range(0f, 1f)] public float fieldCoherence = .9f;
        [Tooltip("Distance between repeating standing-wave bands, in pattern-local units.")]
        [Min(.05f)] public float fieldWavelength = 2f;
        [Tooltip("Gentle grain-size pulsing with the local standing wave during formation. Zero disables it; idle size is never changed.")]
        [Range(0f, 1f)] public float birthPulse = .15f;
        [Tooltip("Local-unit vibration while condensing. Settles to zero at the exact idle position.")]
        [Min(0f)] public float vibrationStrength = .16f;
        [Min(0f)] public float vibrationFrequency = 10f;
    }

    [System.Serializable]
    public sealed class AmplifierResonanceSpawnerSettings
    {
        [Min(0f)] public float initialSpawnDelay = 1f;
        [Min(0f)] public float respawnDelay = 8f;
        [Tooltip("Uniform additive variation: (-2,2) with delay 8 produces waits between 6 and 10 seconds. Final delay is never negative.")]
        public Vector2 respawnDelayVariation = new Vector2(-2f, 2f);
        [Min(.05f)] public float placementRetryDelay = .5f;
        [Tooltip("Time available after the Core has fully appeared. At expiry the Core and pattern dissolve without scoring. Zero disables the time-out.")]
        [InspectorName("Sequence Time-out (seconds)"), Min(0f)] public float maximumActiveSeconds = 30f;
        [Tooltip("During the final seconds the Core's surface becomes increasingly unstable. Zero disables this warning; it never changes physical motion.")]
        [Min(0f)] public float timeoutWarningSeconds = 5f;
    }
}
