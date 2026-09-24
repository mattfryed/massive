using UnityEngine;
using TMPro;
using Massive.Player;
using Massive.Multiplier;
using Massive.Resonance;
using Massive.Scoring;

namespace Massive.Settings
{
    [CreateAssetMenu(menuName = "MASSIVE/Shared Settings/Text Animation")]
    public sealed class TextAnimationSharedProfile : SharedSettingsProfile
    {
        [Tooltip("Shared score digit animation. A blank assignment preserves each scoreboard's local preset.")]
        public Massive.TextAnimation.TextAnimationPreset scoreDigitMorphPreset;
        [Range(0f, 5f)] public float scoreDigitMorphIntensity = 1f;
        [Tooltip("Tier colors and promotion/loop animation presets shared by every scoreboard.")]
        public EnergyTierVisualProfile energyTierProfile;
    }
}
