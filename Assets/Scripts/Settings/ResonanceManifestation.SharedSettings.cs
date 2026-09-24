using UnityEngine;
using TMPro;
using Massive.Player;
using Massive.Multiplier;
using Massive.Resonance;
using Massive.Scoring;

namespace Massive.Resonance
{
    public sealed partial class ResonanceManifestation : Massive.Settings.ISharedSettingsConsumer
    {
        [SerializeField, Tooltip("Use the shared project profile across scenes. Turn off to restore this component's local tuning.")]
        private bool useSharedSettings = true;
        public bool UseSharedSettings { get => useSharedSettings; set => useSharedSettings = value; }
        public Massive.Settings.SharedSettingsProfile SharedSettingsAsset => Massive.Settings.SharedSettingsRuntime.Load<Massive.Settings.ResonanceSharedProfile>();
        public string SharedSettingsGroup => "formation";
        private Massive.Settings.ResonanceSharedProfile SharedProfile => Massive.Settings.SharedSettingsRuntime.Resolve<Massive.Settings.ResonanceSharedProfile>(this, useSharedSettings);
        private Massive.Settings.ResonanceManifestationSettings SharedTuning => SharedProfile != null ? SharedProfile.formation : null;
        public float Effective_spawnSeconds => SharedTuning != null ? SharedTuning.spawnSeconds : spawnSeconds;
        public float Effective_despawnSeconds => SharedTuning != null ? SharedTuning.despawnSeconds : despawnSeconds;
        public float Effective_sizeGrowthPower => SharedTuning != null ? SharedTuning.sizeGrowthPower : sizeGrowthPower;
        public float Effective_condensationPower => SharedTuning != null ? SharedTuning.condensationPower : condensationPower;
        public ResonanceBirthDistribution Effective_birthDistribution => SharedTuning != null ? SharedTuning.birthDistribution : birthDistribution;
        public float Effective_localSpawnSpread => SharedTuning != null ? SharedTuning.localSpawnSpread : localSpawnSpread;
        public float Effective_alongArcSpread => SharedTuning != null ? SharedTuning.alongArcSpread : alongArcSpread;
        public float Effective_fieldCoherence => SharedTuning != null ? SharedTuning.fieldCoherence : fieldCoherence;
        public float Effective_fieldWavelength => SharedTuning != null ? SharedTuning.fieldWavelength : fieldWavelength;
        public float Effective_birthPulse => SharedTuning != null ? SharedTuning.birthPulse : birthPulse;
        public float Effective_vibrationStrength => SharedTuning != null ? SharedTuning.vibrationStrength : vibrationStrength;
        public float Effective_vibrationFrequency => SharedTuning != null ? SharedTuning.vibrationFrequency : vibrationFrequency;
    }
}
