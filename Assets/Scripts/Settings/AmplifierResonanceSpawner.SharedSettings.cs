using UnityEngine;
using TMPro;
using Massive.Player;
using Massive.Multiplier;
using Massive.Resonance;
using Massive.Scoring;

namespace Massive.Multiplier
{
    public sealed partial class AmplifierResonanceSpawner : Massive.Settings.ISharedSettingsConsumer
    {
        [SerializeField, Tooltip("Use the shared project profile across scenes. Turn off to restore this component's local tuning.")]
        private bool useSharedSettings = true;
        public bool UseSharedSettings { get => useSharedSettings; set => useSharedSettings = value; }
        public Massive.Settings.SharedSettingsProfile SharedSettingsAsset => Massive.Settings.SharedSettingsRuntime.Load<Massive.Settings.ResonanceSharedProfile>();
        public string SharedSettingsGroup => "cycle";
        private Massive.Settings.ResonanceSharedProfile SharedProfile => Massive.Settings.SharedSettingsRuntime.Resolve<Massive.Settings.ResonanceSharedProfile>(this, useSharedSettings);
        private Massive.Settings.AmplifierResonanceSpawnerSettings SharedTuning => SharedProfile != null ? SharedProfile.cycle : null;
        public float Effective_initialSpawnDelay => SharedTuning != null ? SharedTuning.initialSpawnDelay : initialSpawnDelay;
        public float Effective_respawnDelay => SharedTuning != null ? SharedTuning.respawnDelay : respawnDelay;
        public Vector2 Effective_respawnDelayVariation => SharedTuning != null ? SharedTuning.respawnDelayVariation : respawnDelayVariation;
        public float Effective_placementRetryDelay => SharedTuning != null ? SharedTuning.placementRetryDelay : placementRetryDelay;
        public float Effective_maximumActiveSeconds => SharedTuning != null ? SharedTuning.maximumActiveSeconds : maximumActiveSeconds;
        public float Effective_timeoutWarningSeconds => SharedTuning != null ? SharedTuning.timeoutWarningSeconds : timeoutWarningSeconds;
    }
}
