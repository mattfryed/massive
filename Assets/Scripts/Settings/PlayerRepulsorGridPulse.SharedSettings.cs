using UnityEngine;
using TMPro;
using Massive.Player;
using Massive.Multiplier;
using Massive.Resonance;
using Massive.Scoring;

namespace Massive.Player
{
    public sealed partial class PlayerRepulsorGridPulse : Massive.Settings.ISharedSettingsConsumer
    {
        [SerializeField, Tooltip("Use the shared project profile across scenes. Turn off to restore this component's local tuning.")]
        private bool useSharedSettings = true;
        public bool UseSharedSettings { get => useSharedSettings; set => useSharedSettings = value; }
        public Massive.Settings.SharedSettingsProfile SharedSettingsAsset => Massive.Settings.SharedSettingsRuntime.Load<Massive.Settings.MeleeVisualProfile>();
        public string SharedSettingsGroup => "grid";
        private Massive.Settings.MeleeVisualProfile SharedProfile => Massive.Settings.SharedSettingsRuntime.Resolve<Massive.Settings.MeleeVisualProfile>(this, useSharedSettings);
        private Massive.Settings.PlayerRepulsorGridPulseSettings SharedTuning => SharedProfile != null ? SharedProfile.grid : null;
        public bool Effective_pulseEnabled => SharedTuning != null ? SharedTuning.pulseEnabled : pulseEnabled;
        public float Effective_intensity => SharedTuning != null ? SharedTuning.intensity : intensity;
        public float Effective_travelRadius => SharedTuning != null ? SharedTuning.travelRadius : travelRadius;
        public float Effective_duration => SharedTuning != null ? SharedTuning.duration : duration;
        public float Effective_packetWidth => SharedTuning != null ? SharedTuning.packetWidth : packetWidth;
        public float Effective_curl => SharedTuning != null ? SharedTuning.curl : curl;
    }
}
