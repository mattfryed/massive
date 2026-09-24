using UnityEngine;
using TMPro;
using Massive.Player;
using Massive.Multiplier;
using Massive.Resonance;
using Massive.Scoring;

namespace Massive.Multiplier
{
    public sealed partial class AmplifierCoreVisual : Massive.Settings.ISharedSettingsConsumer
    {
        [SerializeField, Tooltip("Use the shared project profile across scenes. Turn off to restore this component's local tuning.")]
        private bool useSharedSettings = true;
        public bool UseSharedSettings { get => useSharedSettings; set => useSharedSettings = value; }
        public Massive.Settings.SharedSettingsProfile SharedSettingsAsset => Massive.Settings.SharedSettingsRuntime.Load<Massive.Settings.AmplifierSharedProfile>();
        public string SharedSettingsGroup => "coreSurface";
        private Massive.Settings.AmplifierSharedProfile SharedProfile => Massive.Settings.SharedSettingsRuntime.Resolve<Massive.Settings.AmplifierSharedProfile>(this, useSharedSettings);
        private Massive.Settings.AmplifierCoreVisualSettings SharedTuning => SharedProfile != null ? SharedProfile.coreSurface : null;
        public Vector3 Effective_energyRotationAxis => SharedTuning != null ? SharedTuning.energyRotationAxis : energyRotationAxis;
        public float Effective_energyRotationSpeed => SharedTuning != null ? SharedTuning.energyRotationSpeed : energyRotationSpeed;
        public Vector3 Effective_shellRotationAxis => SharedTuning != null ? SharedTuning.shellRotationAxis : shellRotationAxis;
        public float Effective_shellRotationSpeed => SharedTuning != null ? SharedTuning.shellRotationSpeed : shellRotationSpeed;
        public float Effective_breatheAmplitude => SharedTuning != null ? SharedTuning.breatheAmplitude : breatheAmplitude;
        public float Effective_breatheFrequency => SharedTuning != null ? SharedTuning.breatheFrequency : breatheFrequency;
        public float Effective_spawnOrganicRibbonScale => SharedTuning != null ? SharedTuning.spawnOrganicRibbonScale : spawnOrganicRibbonScale;
        public float Effective_timeoutInstability => SharedTuning != null ? SharedTuning.timeoutInstability : timeoutInstability;
    }
}
