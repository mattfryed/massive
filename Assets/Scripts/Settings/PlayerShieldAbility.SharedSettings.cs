using Massive.Settings;
using UnityEngine;

namespace Massive.Player
{
    public sealed partial class PlayerShieldAbility : ISharedSettingsConsumer
    {
        [SerializeField, Tooltip("Use project-wide Shield response settings from Player Tuning.")]
        private bool useSharedSettings = true;
        public bool UseSharedSettings { get => useSharedSettings; set => useSharedSettings = value; }
        public SharedSettingsProfile SharedSettingsAsset => SharedSettingsRuntime.Load<PlayerTuningProfile>();
        public string SharedSettingsGroup => "shield";
        private PlayerShieldResponseSettings SharedShieldSettings
        {
            get
            {
                if (!owner) owner = GetComponentInParent<PlayerControllerScript>();
                if (owner && !owner.UsesGameplayTuning) return null;
                return SharedSettingsRuntime.Resolve<PlayerTuningProfile>(this, useSharedSettings)?.shield;
            }
        }
        public bool ParryCounterEnabled => SharedShieldSettings?.parryCounterThrust ?? parryCounterThrust;
        public float TapActivationCooldownSeconds => Mathf.Max(0f,
            SharedShieldSettings?.tapActivationCooldownSeconds ?? tapActivationCooldownSeconds);
        public float HoldActivationCooldownSeconds => Mathf.Max(0f,
            SharedShieldSettings?.holdActivationCooldownSeconds ?? activationCooldownSeconds);
    }
}
