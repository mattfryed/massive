using UnityEngine;
using TMPro;
using Massive.Player;
using Massive.Multiplier;
using Massive.Resonance;
using Massive.Scoring;

namespace Massive.Scoring
{
    public sealed partial class TeamAmplifierToastPresenter : Massive.Settings.ISharedSettingsConsumer
    {
        [SerializeField, Tooltip("Use the shared project profile across scenes. Turn off to restore this component's local tuning.")]
        private bool useSharedSettings = true;
        public bool UseSharedSettings { get => useSharedSettings; set => useSharedSettings = value; }
        public Massive.Settings.SharedSettingsProfile SharedSettingsAsset => Massive.Settings.SharedSettingsRuntime.Load<Massive.Settings.AmplifierSharedProfile>();
        public string SharedSettingsGroup => "toast";
        private Massive.Settings.AmplifierSharedProfile SharedProfile => Massive.Settings.SharedSettingsRuntime.Resolve<Massive.Settings.AmplifierSharedProfile>(this, useSharedSettings);
        private Massive.Settings.TeamAmplifierToastPresenterSettings SharedTuning => SharedProfile != null ? SharedProfile.toast : null;
        public TMP_FontAsset Effective_font => SharedTuning != null && SharedTuning.font != null ? SharedTuning.font : font;
        public Vector2 Effective_lightAnchor => SharedTuning != null ? SharedTuning.lightAnchor : lightAnchor;
        public Vector2 Effective_darkAnchor => SharedTuning != null ? SharedTuning.darkAnchor : darkAnchor;
        public float Effective_overallScale => SharedTuning != null ? SharedTuning.overallScale : overallScale;
        public float Effective_teamLabelSize => SharedTuning != null ? SharedTuning.teamLabelSize : teamLabelSize;
        public float Effective_multiplierSize => SharedTuning != null ? SharedTuning.multiplierSize : multiplierSize;
        public string Effective_lightLabel => SharedTuning != null ? SharedTuning.lightLabel : lightLabel;
        public string Effective_darkLabel => SharedTuning != null ? SharedTuning.darkLabel : darkLabel;
        public float Effective_lifetime => SharedTuning != null ? SharedTuning.lifetime : lifetime;
        public float Effective_fadeInSeconds => SharedTuning != null ? SharedTuning.fadeInSeconds : fadeInSeconds;
        public float Effective_fadeOutSeconds => SharedTuning != null ? SharedTuning.fadeOutSeconds : fadeOutSeconds;
        public bool Effective_useUnscaledTime => SharedTuning != null ? SharedTuning.useUnscaledTime : useUnscaledTime;
        public bool Effective_showMaxAmplification => SharedTuning != null ? SharedTuning.showMaxAmplification : showMaxAmplification;
        public string Effective_maxAmplificationLabel => SharedTuning != null ? SharedTuning.maxAmplificationLabel : maxAmplificationLabel;
        public float Effective_maxNoticeFontSize => SharedTuning != null ? SharedTuning.maxNoticeFontSize : maxNoticeFontSize;
        public float Effective_maxNoticeScale => SharedTuning != null ? SharedTuning.maxNoticeScale : maxNoticeScale;
        public Vector2 Effective_maxNoticeOffset => SharedTuning != null ? SharedTuning.maxNoticeOffset : maxNoticeOffset;
        public float Effective_maxNoticeLifetime => SharedTuning != null ? SharedTuning.maxNoticeLifetime : maxNoticeLifetime;
        public float Effective_maxNoticeRepeatDelay => SharedTuning != null ? SharedTuning.maxNoticeRepeatDelay : maxNoticeRepeatDelay;
        public bool Effective_rainbowColors => SharedTuning != null ? SharedTuning.rainbowColors : rainbowColors;
        public float Effective_rainbowSpeed => SharedTuning != null ? SharedTuning.rainbowSpeed : rainbowSpeed;
        public bool Effective_vibration => SharedTuning != null ? SharedTuning.vibration : vibration;
        public float Effective_vibrationPixels => SharedTuning != null ? SharedTuning.vibrationPixels : vibrationPixels;
        public float Effective_vibrationFrequency => SharedTuning != null ? SharedTuning.vibrationFrequency : vibrationFrequency;
        public float Effective_scalePunch => SharedTuning != null ? SharedTuning.scalePunch : scalePunch;
        public bool Effective_ghostTrails => SharedTuning != null ? SharedTuning.ghostTrails : ghostTrails;
        public float Effective_ghostOpacity => SharedTuning != null ? SharedTuning.ghostOpacity : ghostOpacity;
        public float Effective_ghostLifetime => SharedTuning != null ? SharedTuning.ghostLifetime : ghostLifetime;
        public float Effective_ghostTravelPixels => SharedTuning != null ? SharedTuning.ghostTravelPixels : ghostTravelPixels;
        public float Effective_ghostsPerSecond => SharedTuning != null ? SharedTuning.ghostsPerSecond : ghostsPerSecond;
    }
}
