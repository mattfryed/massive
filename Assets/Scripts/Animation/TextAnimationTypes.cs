using System;
using TMPro;
using UnityEngine;

namespace Massive.TextAnimation
{
    public enum TextAnimationTimeMode
    {
        Scaled,
        Unscaled
    }

    public enum TextAnimationOrder
    {
        Forward,
        Reverse,
        CenterOut,
        EdgesIn,
        Alternating,
        DeterministicRandom
    }

    public enum TextAnimationCharacterSelection
    {
        AllVisible,
        DigitsOnly,
        LettersOnly,
        ChangedCharacters,
        ChangedDigits
    }

    public enum TextAnimationInterruption
    {
        Replace,
        Queue,
        IgnoreWhilePlaying,
        CompleteAndReplace
    }

    public enum TextAnimationCompletionMode
    {
        RestoreBaseline,
        HoldFinalFrame,
        HideTarget
    }

    public enum TextAnimationPlaybackMode
    {
        OneShot,
        Loop
    }

    /// <summary>
    /// Runtime parameters that let one reusable preset adapt to the event that
    /// triggered it. Use TextAnimationContext.Default instead of default(...) if
    /// the standard intensity of one is desired.
    /// </summary>
    [Serializable]
    public struct TextAnimationContext
    {
        [Min(0f)] public float intensity;
        public Vector2 direction;
        public bool useAccentColor;
        public Color accentColor;
        public int seedOffset;

        [NonSerialized] public string previousText;
        [NonSerialized] public string newText;

        public static TextAnimationContext Default => new TextAnimationContext
        {
            intensity = 1f,
            direction = Vector2.right,
            useAccentColor = false,
            accentColor = Color.white,
            seedOffset = 0,
            previousText = null,
            newText = null
        };

        public TextAnimationContext Sanitized()
        {
            TextAnimationContext value = this;
            value.intensity = Mathf.Max(0f, value.intensity);

            if (value.direction.sqrMagnitude < 0.000001f)
                value.direction = Vector2.right;
            else
                value.direction.Normalize();

            return value;
        }

        public TextAnimationContext WithIntensity(float value)
        {
            TextAnimationContext copy = this;
            copy.intensity = Mathf.Max(0f, value);
            return copy;
        }

        public TextAnimationContext MultiplyIntensity(float multiplier)
        {
            TextAnimationContext copy = this;
            copy.intensity = Mathf.Max(0f, copy.intensity * Mathf.Max(0f, multiplier));
            return copy;
        }

        public TextAnimationContext WithDirection(Vector2 value)
        {
            TextAnimationContext copy = this;
            copy.direction = value.sqrMagnitude > 0.000001f
                ? value.normalized
                : Vector2.right;
            return copy;
        }

        public TextAnimationContext WithAccentColor(Color value)
        {
            TextAnimationContext copy = this;
            copy.useAccentColor = true;
            copy.accentColor = value;
            return copy;
        }

        public TextAnimationContext WithSeedOffset(int value)
        {
            TextAnimationContext copy = this;
            copy.seedOffset = value;
            return copy;
        }

        public TextAnimationContext WithTextChange(string previous, string current)
        {
            TextAnimationContext copy = this;
            copy.previousText = previous ?? string.Empty;
            copy.newText = current ?? string.Empty;
            return copy;
        }
    }

    [Serializable]
    public struct TextAnimationMarker
    {
        [Range(0f, 1f)] public float normalizedTime;
        public string id;
    }

    /// <summary>
    /// Lightweight handle returned by TMPTextAnimator.Play(). It remains active
    /// while the request is queued or playing.
    /// </summary>
    public readonly struct TextAnimationPlaybackHandle
    {
        private readonly TMPTextAnimator _owner;
        private readonly int _requestId;

        internal TextAnimationPlaybackHandle(TMPTextAnimator owner, int requestId)
        {
            _owner = owner;
            _requestId = requestId;
        }

        public bool IsValid => _owner != null && _requestId > 0;
        public bool IsActive => IsValid && _owner.IsPlaybackActive(_requestId);
        public int RequestId => _requestId;

        public void Cancel(bool restoreBaseline = true)
        {
            if (IsValid)
                _owner.Cancel(_requestId, restoreBaseline);
        }

        public void CompleteImmediately()
        {
            if (IsValid)
                _owner.Complete(_requestId);
        }
    }

    /// <summary>
    /// Complete persistent SDF state for one TMP material. Animation presets add
    /// transient offsets on top, then return to this state.
    /// </summary>
    [Serializable]
    public struct TMPMaterialPersistentStyle
    {
        public Color faceColor;
        public Color outlineColor;
        public float outlineWidth;
        public float faceDilate;
        public float softness;
        public bool glowEnabled;
        public Color glowColor;
        public float glowOffset;
        public float glowInner;
        public float glowOuter;
        public float glowPower;
    }

    internal struct TMPMaterialTransientState
    {
        public float faceDilateOffset;
        public float outlineWidthOffset;
        public float softnessOffset;
        public float glowOffsetOffset;
        public float glowInnerOffset;
        public float glowOuterOffset;
        public float glowPowerOffset;

        public bool forceGlow;
        public bool overrideOutlineColor;
        public Color outlineColor;
        public float outlineColorWeight;
        public bool overrideGlowColor;
        public Color glowColor;
        public float glowColorWeight;

        public static TMPMaterialTransientState Identity => new TMPMaterialTransientState
        {
            outlineColor = Color.white,
            glowColor = Color.white
        };
    }

    internal struct TextAnimationGlyphState
    {
        public Vector3 positionOffset;
        public float rotationDegrees;
        public Vector2 scale;
        public float shearX;
        public float alphaMultiplier;
        public Color colorMultiplier;
        public float tracking;

        public static TextAnimationGlyphState Identity => new TextAnimationGlyphState
        {
            positionOffset = Vector3.zero,
            rotationDegrees = 0f,
            scale = Vector2.one,
            shearX = 0f,
            alphaMultiplier = 1f,
            colorMultiplier = Color.white,
            tracking = 0f
        };
    }

    /// <summary>
    /// Per-glyph state for a temporary rendering of the text that existed before
    /// SetTextAndPlay changed the target. Modules opt into this layer when an
    /// effect needs both outgoing and incoming letterforms on screen.
    /// </summary>
    internal struct TextAnimationPreviousGlyphState
    {
        public bool visible;
        public TextAnimationGlyphState glyph;

        public static TextAnimationPreviousGlyphState Identity =>
            new TextAnimationPreviousGlyphState
            {
                visible = false,
                glyph = TextAnimationGlyphState.Identity
            };
    }

    internal struct TextAnimationSdfMorphState
    {
        public bool enabled;
        public float progress;
        public float edgeSoftness;
        public float contourBias;

        public static TextAnimationSdfMorphState Identity =>
            new TextAnimationSdfMorphState
            {
                enabled = false,
                progress = 0f,
                edgeSoftness = 1f,
                contourBias = 0f
            };
    }

    internal struct TextAnimationRootState
    {
        public Vector3 positionOffset;
        public Vector3 eulerOffset;
        public Vector3 scaleMultiplier;

        public static TextAnimationRootState Identity => new TextAnimationRootState
        {
            positionOffset = Vector3.zero,
            eulerOffset = Vector3.zero,
            scaleMultiplier = Vector3.one
        };
    }

    /// <summary>
    /// Runtime state for full-text afterimages. Unlike glyph modules, this is
    /// rendered by lightweight TMP duplicates so the primary text stays crisp.
    /// </summary>
    internal struct TextAnimationEchoState
    {
        public bool visible;
        public int copyCount;
        public Vector2 direction;
        public float spacing;
        public float spread;
        public float opacity;
        public float opacityFalloff;
        public Color tint;
        public float scaleStepPerCopy;

        public static TextAnimationEchoState Identity => new TextAnimationEchoState
        {
            visible = false,
            copyCount = 0,
            direction = Vector2.right,
            spacing = 0f,
            spread = 1f,
            opacity = 0f,
            opacityFalloff = 1f,
            tint = Color.white,
            scaleStepPerCopy = 0f
        };
    }

    internal readonly struct TextAnimationEvaluationContext
    {
        public readonly TextAnimationContext runtime;
        public readonly float globalProgress;
        public readonly float characterProgress;
        public readonly float elapsedSeconds;
        public readonly float totalSeconds;
        public readonly int characterIndex;
        public readonly int visibleOrdinal;
        public readonly int orderRank;
        public readonly int selectedCount;
        public readonly int lineNumber;
        public readonly int lineVisibleOrdinal;
        public readonly int lineVisibleCount;
        public readonly char character;
        public readonly int seed;

        public TextAnimationEvaluationContext(
            TextAnimationContext runtime,
            float globalProgress,
            float characterProgress,
            float elapsedSeconds,
            float totalSeconds,
            int characterIndex,
            int visibleOrdinal,
            int orderRank,
            int selectedCount,
            int lineNumber,
            int lineVisibleOrdinal,
            int lineVisibleCount,
            char character,
            int seed)
        {
            this.runtime = runtime;
            this.globalProgress = globalProgress;
            this.characterProgress = characterProgress;
            this.elapsedSeconds = elapsedSeconds;
            this.totalSeconds = totalSeconds;
            this.characterIndex = characterIndex;
            this.visibleOrdinal = visibleOrdinal;
            this.orderRank = orderRank;
            this.selectedCount = selectedCount;
            this.lineNumber = lineNumber;
            this.lineVisibleOrdinal = lineVisibleOrdinal;
            this.lineVisibleCount = lineVisibleCount;
            this.character = character;
            this.seed = seed;
        }

        public float Intensity => Mathf.Max(0f, runtime.intensity);
        public Vector2 Direction => runtime.direction.sqrMagnitude > 0.000001f
            ? runtime.direction.normalized
            : Vector2.right;
        public float NormalizedOrder => selectedCount <= 1
            ? 0f
            : orderRank / (float)(selectedCount - 1);
    }

    public static class TextAnimationMath
    {
        public static Vector2 RotateFromPositiveX(Vector2 authoredVector, Vector2 direction)
        {
            Vector2 dir = direction.sqrMagnitude > 0.000001f
                ? direction.normalized
                : Vector2.right;

            float angle = Mathf.Atan2(dir.y, dir.x);
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            return new Vector2(
                authoredVector.x * cos - authoredVector.y * sin,
                authoredVector.x * sin + authoredVector.y * cos);
        }

        public static AnimationCurve Linear01()
        {
            return AnimationCurve.Linear(0f, 0f, 1f, 1f);
        }

        public static AnimationCurve EaseOut01()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 3.5f),
                new Keyframe(1f, 1f, 0f, 0f));
        }

        public static AnimationCurve Decay01()
        {
            return new AnimationCurve(
                new Keyframe(0f, 1f, 0f, -3.2f),
                new Keyframe(1f, 0f, -0.2f, 0f));
        }

        public static AnimationCurve Pulse01()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 6f),
                new Keyframe(0.24f, 1f, 0f, 0f),
                new Keyframe(1f, 0f, -2.2f, 0f));
        }

        public static AnimationCurve Overshoot01()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 4f),
                new Keyframe(0.68f, 1.12f, 0f, 0f),
                new Keyframe(1f, 1f, -0.2f, 0f));
        }
    }
}
