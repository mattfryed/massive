using System;
using TMPro;
using UnityEngine;

namespace Massive.Scoring
{
    [Serializable]
    public sealed class TeamAmplifierGoalNumberSettings : ISerializationCallbackReceiver
    {
        public bool enabled = true;
        [Tooltip("Empty uses the existing score HUD font.")]
        public TMP_FontAsset font;
        [Tooltip("Optional prefix on the small number inside the white disc.")]
        public string prefix = "";
        public Color lightColor = Color.black;
        public Color darkColor = Color.black;
        public Color outlineColor = Color.black;
        [Range(0f, .35f)] public float outlineWidth;
        // Retained to migrate existing scene/prefab and shared-profile proportions.
        [HideInInspector] public float whiteCircleScale = .53f;
        public Color whiteCircleColor = Color.white;
        [HideInInspector] public float numberScale = .17f;
        [HideInInspector] public float lightNumberHorizontalOffset = -.12f;
        [HideInInspector] public float darkNumberHorizontalOffset = .12f;
        [SerializeField, HideInInspector] private bool levelOffsetsInitialized;
        [Tooltip("Master scale applied to every multiplier level and all three layers.")]
        [Min(.01f)] public float overallScale = 1f;
        [Tooltip("Independent whole-badge, white-circle and text scales, plus mirrored text offset, for each multiplier. Circle and text sizes are relative to that level's whole badge.")]
        public LevelScale[] levelScales = {
            new LevelScale(2, .28f), new LevelScale(4, .44f), new LevelScale(8, .64f)
        };
        [Tooltip("Moves the complete badge toward the goal exterior as a fraction of its depth. Zero cuts the circle and core in half at the arena border.")]
        [Range(-1f, 1f)] public float outwardOffset;
        [Tooltip("Moves the complete badge vertically as a fraction of the goal half-height. Zero vertically centers it.")]
        [Range(-1f, 1f)] public float verticalOffset;
        [Tooltip("Extra space inside the thick goal ring, in world units. The number cannot draw over the ring.")]
        [Min(0f)] public float ringInset = .025f;

        [Header("Multiplier Transitions")]
        public bool animateChanges = true;
        [Tooltip("Time for the core, circle and text sizes to reach the next level. First capture grows the core and circle from zero.")]
        [Min(0f)] public float growthSeconds = .45f;
        public AnimationCurve growthEasing = Massive.TextAnimation.TextAnimationMath.EaseOut01();
        [Tooltip("First capture only: text strokes grow from invisible to normal after the core and circle finish scaling. Later digits use the shared team-score morph preset.")]
        [Min(0f)] public float firstTextGrowSeconds = .35f;
        public AnimationCurve firstTextGrowEasing = Massive.TextAnimation.TextAnimationMath.EaseOut01();

        [Serializable]
        public struct LevelScale
        {
            [Min(2)] public int multiplier;
            [InspectorName("Whole Scale"), Min(.01f)]
            [Tooltip("Complete badge diameter as a fraction of the goal diameter; Overall Scale is also applied.")]
            public float scale;
            [Range(.1f, 1f)]
            [Tooltip("White circle diameter relative to this level's whole badge diameter.")]
            public float whiteCircleScale;
            [InspectorName("Text Scale"), Range(.01f, 1f)]
            [Tooltip("Text height relative to this level's whole badge diameter.")]
            public float numberScale;
            [InspectorName("Text Horizontal Offset"), Range(-.5f, .5f)]
            [Tooltip("Moves only the text, as a fraction of this level's badge diameter. Negative moves Light left and Dark right (into their goals); positive reverses both. Zero centers text on the badge.")]
            public float horizontalOffset;
            public LevelScale(int multiplier, float scale)
            { this.multiplier = multiplier; this.scale = scale; whiteCircleScale = 0; numberScale = 0; horizontalOffset = -.12f; }
        }

        public float ScaleFor(int multiplier)
        {
            if (multiplier <= 1) return 0;
            return Mathf.Max(.01f, LevelFor(multiplier).scale) * Mathf.Max(.01f, overallScale);
        }

        public float WhiteCircleScaleFor(int multiplier)
        {
            float value = LevelFor(multiplier).whiteCircleScale;
            return Mathf.Max(.01f, value > 0 ? value : whiteCircleScale);
        }

        public float NumberScaleFor(int multiplier)
        {
            float value = LevelFor(multiplier).numberScale;
            return Mathf.Max(.01f, value > 0 ? value : numberScale);
        }

        public float NumberHorizontalOffsetFor(int multiplier, int teamID)
        {
            float value = LevelFor(multiplier).horizontalOffset;
            return teamID == 1 ? value : -value;
        }

        LevelScale LevelFor(int multiplier)
        {
            var result = new LevelScale(multiplier, .28f) {
                whiteCircleScale = whiteCircleScale, numberScale = numberScale, horizontalOffset = lightNumberHorizontalOffset
            };
            int nearest = int.MinValue;
            if (levelScales != null) foreach (var level in levelScales)
            {
                if (level.multiplier == multiplier) return level;
                if (level.multiplier <= multiplier && level.multiplier > nearest)
                { nearest = level.multiplier; result = level; }
            }
            return result;
        }

        public void OnBeforeSerialize() => InitializeLayerScales();
        public void OnAfterDeserialize() => InitializeLayerScales();
        void InitializeLayerScales()
        {
            // Zero is the uninitialized sentinel, including constructor defaults:
            // Unity can keep constructor values when a serialized field is missing.
            // Seed from the previous global tuning once, preserving authored scenes.
            if (levelScales == null) return;
            for (int i = 0; i < levelScales.Length; i++)
            {
                if (levelScales[i].whiteCircleScale <= 0)
                    levelScales[i].whiteCircleScale = whiteCircleScale > 0 ? whiteCircleScale : .53f;
                if (levelScales[i].numberScale <= 0)
                    levelScales[i].numberScale = numberScale > 0 ? numberScale : .17f;
                // Preserve Light's existing offset; Dark now mirrors it by design.
                // Track migration separately because zero is a valid authored offset.
                if (!levelOffsetsInitialized)
                    levelScales[i].horizontalOffset = lightNumberHorizontalOffset;
            }
            levelOffsetsInitialized = true;
        }
    }
}
