using System;
using System.Collections.Generic;
using UnityEngine;

namespace Massive.TextAnimation
{
    [CreateAssetMenu(
        fileName = "TextAnimationSequence",
        menuName = "MASSIVE/Text Animation/Sequence")]
    public sealed class TextAnimationSequence : ScriptableObject
    {
        [Serializable]
        public sealed class Track
        {
            [Tooltip("Resolved by TMPTextAnimationGroup. Examples: NUMBER, TITLE, ACTIVE_TIER.")]
            public string slotId = "PRIMARY";
            public TextAnimationPreset preset;
            [Min(0f)] public float startDelay;

            [Header("Context Overrides")]
            [Min(0f)] public float intensityMultiplier = 1f;
            public bool overrideDirection;
            public Vector2 direction = Vector2.right;
            public bool overrideAccentColor;
            public Color accentColor = Color.white;
        }

        [Tooltip("Used for track start delays. Each preset still owns its own time mode.")]
        public TextAnimationTimeMode delayTimeMode = TextAnimationTimeMode.Unscaled;

        [Tooltip("Useful for staggered IN sequences: cull every resolved target at sequence start, then let each preset reveal its own target when its track begins.")]
        public bool hideTargetsUntilTheirTrackStarts;

        public bool warnAboutMissingSlots = true;
        [Min(0f)] public float tailSeconds;
        public List<Track> tracks = new List<Track>();

        private void OnValidate()
        {
            tailSeconds = Mathf.Max(0f, tailSeconds);
            tracks ??= new List<Track>();

            for (int i = 0; i < tracks.Count; i++)
            {
                Track track = tracks[i];
                if (track == null) continue;
                track.startDelay = Mathf.Max(0f, track.startDelay);
                track.intensityMultiplier = Mathf.Max(0f, track.intensityMultiplier);
                if (track.direction.sqrMagnitude < 0.000001f)
                    track.direction = Vector2.right;
            }
        }
    }
}
