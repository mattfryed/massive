using System.Collections.Generic;
using UnityEngine;

namespace Massive.TextAnimation
{
    [CreateAssetMenu(
        fileName = "TextAnimationPreset",
        menuName = "MASSIVE/Text Animation/Preset")]
    public sealed class TextAnimationPreset : ScriptableObject
    {
        [Header("Timing")]
        [Min(0f)] public float delaySeconds;
        [Min(0f)] public float durationSeconds = 0.30f;
        [Min(0f)] public float characterStaggerSeconds = 0.015f;
        public TextAnimationTimeMode timeMode = TextAnimationTimeMode.Unscaled;

        [Header("Character Routing")]
        public TextAnimationCharacterSelection characterSelection = TextAnimationCharacterSelection.AllVisible;
        public TextAnimationOrder characterOrder = TextAnimationOrder.Forward;
        public int deterministicSeed = 1337;

        [Header("Playback")]
        [Tooltip("Looping presets repeat until their playback handle is cancelled or replaced.")]
        public TextAnimationPlaybackMode playbackMode = TextAnimationPlaybackMode.OneShot;
        public TextAnimationInterruption interruption = TextAnimationInterruption.Replace;
        public TextAnimationCompletionMode completion = TextAnimationCompletionMode.RestoreBaseline;

        [Tooltip("When enabled, a preset with no selected characters still runs root and SDF modules.")]
        public bool playObjectModulesWhenNoCharacters = true;

        [Header("Timeline Markers")]
        [Tooltip("Optional normalized markers for sound, particles, or other event hooks.")]
        public List<TextAnimationMarker> markers = new List<TextAnimationMarker>();

        [SerializeReference]
        [Tooltip("Use the custom Inspector's Add Module menu. Modules are evaluated in list order.")]
        private List<TextAnimationModule> modules = new List<TextAnimationModule>();

        public List<TextAnimationModule> Modules => modules;

        public float EstimateTotalSeconds(int selectedCharacterCount)
        {
            int count = Mathf.Max(0, selectedCharacterCount);
            float staggerTail = Mathf.Max(0, count - 1) * Mathf.Max(0f, characterStaggerSeconds);
            return Mathf.Max(0f, delaySeconds) + Mathf.Max(0f, durationSeconds) + staggerTail;
        }

        private void OnValidate()
        {
            delaySeconds = Mathf.Max(0f, delaySeconds);
            durationSeconds = Mathf.Max(0f, durationSeconds);
            characterStaggerSeconds = Mathf.Max(0f, characterStaggerSeconds);
            modules ??= new List<TextAnimationModule>();
            markers ??= new List<TextAnimationMarker>();

            for (int i = 0; i < modules.Count; i++)
                modules[i]?.Validate();

            for (int i = 0; i < markers.Count; i++)
            {
                TextAnimationMarker marker = markers[i];
                marker.normalizedTime = Mathf.Clamp01(marker.normalizedTime);
                markers[i] = marker;
            }

            markers.Sort((a, b) => a.normalizedTime.CompareTo(b.normalizedTime));
        }
    }
}
