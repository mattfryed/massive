using UnityEngine;
using TMPro;
using Massive.Player;
using Massive.Multiplier;
using Massive.Resonance;
using Massive.Scoring;

namespace Massive.Multiplier
{
    public sealed partial class AmplifierCoreGameplay : Massive.Settings.ISharedSettingsConsumer
    {
        [SerializeField, Tooltip("Use the shared project profile across scenes. Turn off to restore this component's local tuning.")]
        private bool useSharedSettings = true;
        public bool UseSharedSettings { get => useSharedSettings; set => useSharedSettings = value; }
        public Massive.Settings.SharedSettingsProfile SharedSettingsAsset => Massive.Settings.SharedSettingsRuntime.Load<Massive.Settings.AmplifierSharedProfile>();
        public string SharedSettingsGroup => "core";
        private Massive.Settings.AmplifierSharedProfile SharedProfile => Massive.Settings.SharedSettingsRuntime.Resolve<Massive.Settings.AmplifierSharedProfile>(this, useSharedSettings && !presentationOnly);
        private Massive.Settings.AmplifierCoreGameplaySettings SharedTuning => SharedProfile != null ? SharedProfile.core : null;
        public float Effective_coreScale => SharedTuning != null ? SharedTuning.coreScale : coreScale;
        public float Effective_mass => SharedTuning != null ? SharedTuning.mass : mass;
        public float Effective_linearDrag => SharedTuning != null ? SharedTuning.linearDrag : linearDrag;
        public float Effective_angularDrag => SharedTuning != null ? SharedTuning.angularDrag : angularDrag;
        public float Effective_maximumPlanarSpeed => SharedTuning != null ? SharedTuning.maximumPlanarSpeed : maximumPlanarSpeed;
        public float Effective_wallRestitution => SharedTuning != null ? SharedTuning.wallRestitution : wallRestitution;
        public float Effective_spawnScaleSeconds => SharedTuning != null ? SharedTuning.spawnScaleSeconds : spawnScaleSeconds;
        public float Effective_spawnShellMorphSeconds => SharedTuning != null ? SharedTuning.spawnShellMorphSeconds : spawnShellMorphSeconds;
        public float Effective_spawnCoreDelaySeconds => SharedTuning != null ? SharedTuning.spawnCoreDelaySeconds : spawnCoreDelaySeconds;
        public float Effective_spawnCoreRevealSeconds => SharedTuning != null ? SharedTuning.spawnCoreRevealSeconds : spawnCoreRevealSeconds;
        public float Effective_captureTravelSeconds => SharedTuning != null ? SharedTuning.captureTravelSeconds : captureTravelSeconds;
        public AnimationCurve Effective_captureEase => SharedTuning != null && SharedTuning.captureEase != null ? SharedTuning.captureEase : captureEase;
        public float Effective_despawnScaleSeconds => SharedTuning != null ? SharedTuning.despawnScaleSeconds : despawnScaleSeconds;
        public float Effective_despawnShellMorphSeconds => SharedTuning != null ? SharedTuning.despawnShellMorphSeconds : despawnShellMorphSeconds;
        public float Effective_despawnCoreHideSeconds => SharedTuning != null ? SharedTuning.despawnCoreHideSeconds : despawnCoreHideSeconds;
        public float Effective_attackImpulse => SharedTuning != null ? SharedTuning.attackImpulse : attackImpulse;
        public float Effective_playerPlanarVelocityRetention => SharedTuning != null ? SharedTuning.playerPlanarVelocityRetention : playerPlanarVelocityRetention;
        public float Effective_playerImpactStopSeconds => SharedTuning != null ? SharedTuning.playerImpactStopSeconds : playerImpactStopSeconds;
        public float Effective_repeatImpactLockoutSeconds => SharedTuning != null ? SharedTuning.repeatImpactLockoutSeconds : repeatImpactLockoutSeconds;
    }
}
