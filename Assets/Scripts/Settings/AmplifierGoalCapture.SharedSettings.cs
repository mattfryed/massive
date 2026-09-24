using UnityEngine;
using TMPro;
using Massive.Player;
using Massive.Multiplier;
using Massive.Resonance;
using Massive.Scoring;

namespace Massive.Multiplier
{
    public sealed partial class AmplifierGoalCapture : Massive.Settings.ISharedSettingsConsumer
    {
        [SerializeField, Tooltip("Use the shared project profile across scenes. Turn off to restore this component's local tuning.")]
        private bool useSharedSettings = true;
        public bool UseSharedSettings { get => useSharedSettings; set => useSharedSettings = value; }
        public Massive.Settings.SharedSettingsProfile SharedSettingsAsset => Massive.Settings.SharedSettingsRuntime.Load<Massive.Settings.AmplifierSharedProfile>();
        public string SharedSettingsGroup => "goal";
        private Massive.Settings.AmplifierSharedProfile SharedProfile => Massive.Settings.SharedSettingsRuntime.Resolve<Massive.Settings.AmplifierSharedProfile>(this, useSharedSettings);
        private Massive.Settings.AmplifierGoalCaptureSettings SharedTuning => SharedProfile != null ? SharedProfile.goal : null;
        public float Effective_pullAcceleration => SharedTuning != null ? SharedTuning.pullAcceleration : pullAcceleration;
        public AnimationCurve Effective_pullByProximity => SharedTuning != null && SharedTuning.pullByProximity != null ? SharedTuning.pullByProximity : pullByProximity;
        public bool Effective_repelWhenMaxed => SharedTuning != null ? SharedTuning.repelWhenMaxed : repelWhenMaxed;
        public float Effective_repulsionAcceleration => SharedTuning != null ? SharedTuning.repulsionAcceleration : repulsionAcceleration;
        public float Effective_repulsionMinimumSpeed => SharedTuning != null ? SharedTuning.repulsionMinimumSpeed : repulsionMinimumSpeed;
    }
}
