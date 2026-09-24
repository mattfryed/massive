using UnityEngine;
using TMPro;
using Massive.Player;
using Massive.Multiplier;
using Massive.Resonance;
using Massive.Scoring;

namespace Massive.Player
{
    public sealed partial class PlayerRepulsorFeedback : Massive.Settings.ISharedSettingsConsumer
    {
        [SerializeField, Tooltip("Use the shared project profile across scenes. Turn off to restore this component's local tuning.")]
        private bool useSharedSettings = true;
        public bool UseSharedSettings { get => useSharedSettings; set => useSharedSettings = value; }
        public Massive.Settings.SharedSettingsProfile SharedSettingsAsset => Massive.Settings.SharedSettingsRuntime.Load<Massive.Settings.MeleeVisualProfile>();
        public string SharedSettingsGroup => "body";
        private Massive.Settings.MeleeVisualProfile SharedProfile => Massive.Settings.SharedSettingsRuntime.Resolve<Massive.Settings.MeleeVisualProfile>(this, useSharedSettings);
        private Massive.Settings.PlayerRepulsorFeedbackSettings SharedTuning => SharedProfile != null ? SharedProfile.body : null;
        public bool Effective_bodyPulseEnabled => SharedTuning != null ? SharedTuning.bodyPulseEnabled : bodyPulseEnabled;
        public float Effective_contraction => SharedTuning != null ? SharedTuning.contraction : contraction;
        public float Effective_expansion => SharedTuning != null ? SharedTuning.expansion : expansion;
        public float Effective_reboundRiseTime => SharedTuning != null ? SharedTuning.reboundRiseTime : reboundRiseTime;
        public float Effective_settleTime => SharedTuning != null ? SharedTuning.settleTime : settleTime;
        public float Effective_vibrationAmount => SharedTuning != null ? SharedTuning.vibrationAmount : vibrationAmount;
        public float Effective_vibrationFrequency => SharedTuning != null ? SharedTuning.vibrationFrequency : vibrationFrequency;
        public bool Effective_recoveryEnabled => SharedTuning != null ? SharedTuning.recoveryEnabled : recoveryEnabled;
        public float Effective_recoveryMovement => SharedTuning != null ? SharedTuning.recoveryMovement : recoveryMovement;
        public float Effective_recoveryDuration => SharedTuning != null ? SharedTuning.recoveryDuration : recoveryDuration;
    }
}
