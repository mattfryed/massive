using System;
using UnityEngine;

namespace Massive.TextAnimation
{
    [Serializable]
    public abstract class TextAnimationModule
    {
        public bool enabled = true;
        [Range(0f, 1f)] public float startNormalized;
        [Range(0f, 1f)] public float endNormalized = 1f;
        public AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        internal float Evaluate(float progress)
        {
            float a = Mathf.Clamp01(startNormalized);
            float b = Mathf.Clamp01(endNormalized);
            float p = Mathf.Clamp01(progress);

            float local;
            if (b - a <= 0.000001f)
                local = p >= b ? 1f : 0f;
            else
                local = Mathf.InverseLerp(a, b, p);

            return curve != null ? curve.Evaluate(local) : local;
        }

        internal virtual void ApplyGlyph(
            ref TextAnimationGlyphState state,
            in TextAnimationEvaluationContext context)
        {
        }

        internal virtual bool UsesPreviousGlyphs => false;

        internal virtual bool UsesSdfGlyphMorphs => false;

        internal virtual void ApplyPreviousGlyph(
            ref TextAnimationPreviousGlyphState state,
            in TextAnimationEvaluationContext context)
        {
        }

        internal virtual void ApplySdfGlyphMorph(
            ref TextAnimationSdfMorphState state,
            in TextAnimationEvaluationContext context)
        {
        }

        internal virtual void ApplyRoot(
            ref TextAnimationRootState state,
            in TextAnimationEvaluationContext context)
        {
        }

        internal virtual void ApplyMaterial(
            ref TMPMaterialTransientState state,
            in TextAnimationEvaluationContext context)
        {
        }

        internal virtual void ApplyEcho(
            ref TextAnimationEchoState state,
            in TextAnimationEvaluationContext context)
        {
        }

        internal virtual void Validate()
        {
            startNormalized = Mathf.Clamp01(startNormalized);
            endNormalized = Mathf.Clamp(endNormalized, startNormalized, 1f);
        }

        protected static float ScaleEffect(float value, float identity, float intensity)
        {
            return Mathf.LerpUnclamped(identity, value, Mathf.Max(0f, intensity));
        }

        protected static Vector2 ScaleEffect(Vector2 value, Vector2 identity, float intensity)
        {
            return Vector2.LerpUnclamped(identity, value, Mathf.Max(0f, intensity));
        }
    }

    [Serializable]
    public sealed class AlphaTextAnimationModule : TextAnimationModule
    {
        [Range(0f, 1f)] public float fromAlpha;
        [Range(0f, 1f)] public float toAlpha = 1f;

        [Tooltip("Usually disabled: reduced-motion intensity should not make a required reveal unreadable.")]
        public bool scaleByContextIntensity;

        internal override void ApplyGlyph(
            ref TextAnimationGlyphState state,
            in TextAnimationEvaluationContext context)
        {
            float t = Evaluate(context.characterProgress);
            float alpha = Mathf.LerpUnclamped(fromAlpha, toAlpha, t);
            if (scaleByContextIntensity)
                alpha = ScaleEffect(alpha, 1f, context.Intensity);
            state.alphaMultiplier *= Mathf.Clamp01(alpha);
        }
    }

    [Serializable]
    public sealed class GlyphTransformTextAnimationModule : TextAnimationModule
    {
        public Vector2 positionFrom;
        public Vector2 positionTo;
        public float rotationFromDegrees;
        public float rotationToDegrees;
        public Vector2 scaleFrom = Vector2.one;
        public Vector2 scaleTo = Vector2.one;
        public float shearXFrom;
        public float shearXTo;

        [Header("Runtime Direction")]
        [Tooltip("Treat authored +X as the runtime context direction.")]
        public bool rotateAuthoredPositionByContextDirection;

        [Tooltip("Adds a second displacement along the runtime context direction.")]
        public bool addContextDirection;
        public float directionDistanceFrom;
        public float directionDistanceTo;

        internal override void ApplyGlyph(
            ref TextAnimationGlyphState state,
            in TextAnimationEvaluationContext context)
        {
            float t = Evaluate(context.characterProgress);
            float intensity = context.Intensity;

            Vector2 position = Vector2.LerpUnclamped(positionFrom, positionTo, t);
            if (rotateAuthoredPositionByContextDirection)
                position = TextAnimationMath.RotateFromPositiveX(position, context.Direction);

            if (addContextDirection)
            {
                float distance = Mathf.LerpUnclamped(directionDistanceFrom, directionDistanceTo, t);
                position += context.Direction * distance;
            }

            float rotation = Mathf.LerpUnclamped(rotationFromDegrees, rotationToDegrees, t);
            Vector2 scale = Vector2.LerpUnclamped(scaleFrom, scaleTo, t);
            float shear = Mathf.LerpUnclamped(shearXFrom, shearXTo, t);

            state.positionOffset += (Vector3)(position * intensity);
            state.rotationDegrees += rotation * intensity;
            state.scale = Vector2.Scale(state.scale, ScaleEffect(scale, Vector2.one, intensity));
            state.shearX += shear * intensity;
        }
    }

    /// <summary>
    /// Blends the signed-distance fields of an outgoing glyph and its replacement
    /// inside one fixed character cell. Use with ChangedCharacters or ChangedDigits
    /// so stable text remains untouched.
    /// </summary>
    [Serializable]
    public sealed class GlyphMorphTextAnimationModule : TextAnimationModule
    {
        [Header("SDF Contour Blend")]
        [Tooltip("Multiplier for antialiasing around the interpolated contour.")]
        [Range(0.25f, 3f)] public float edgeSoftness = 1f;
        [Tooltip("Moves the interpolated contour inward or outward without moving the glyph.")]
        [Range(-0.2f, 0.2f)] public float contourBias;

        internal override bool UsesPreviousGlyphs => true;
        internal override bool UsesSdfGlyphMorphs => true;

        internal override void ApplySdfGlyphMorph(
            ref TextAnimationSdfMorphState state,
            in TextAnimationEvaluationContext context)
        {
            float t = Mathf.Clamp01(Evaluate(context.characterProgress));
            state.enabled = true;
            state.progress = t;
            state.edgeSoftness = edgeSoftness;
            state.contourBias = contourBias * context.Intensity;
        }

        internal override void Validate()
        {
            base.Validate();
            edgeSoftness = Mathf.Clamp(edgeSoftness, 0.25f, 3f);
            contourBias = Mathf.Clamp(contourBias, -0.2f, 0.2f);
        }
    }

    [Serializable]
    public sealed class TrackingTextAnimationModule : TextAnimationModule
    {
        [Tooltip("Additional local-space distance between adjacent visible glyph centers.")]
        public float fromTracking = 12f;
        public float toTracking;

        internal override void ApplyGlyph(
            ref TextAnimationGlyphState state,
            in TextAnimationEvaluationContext context)
        {
            float t = Evaluate(context.characterProgress);
            float value = Mathf.LerpUnclamped(fromTracking, toTracking, t);
            state.tracking += value * context.Intensity;
        }
    }

    [Serializable]
    public sealed class ColorTextAnimationModule : TextAnimationModule
    {
        public Color fromMultiplier = new Color(1f, 1f, 1f, 0f);
        public Color toMultiplier = Color.white;
        public bool useContextAccentAsDestination;

        internal override void ApplyGlyph(
            ref TextAnimationGlyphState state,
            in TextAnimationEvaluationContext context)
        {
            float t = Evaluate(context.characterProgress);
            Color destination = useContextAccentAsDestination && context.runtime.useAccentColor
                ? context.runtime.accentColor
                : toMultiplier;
            Color value = Color.LerpUnclamped(fromMultiplier, destination, t);
            value = Color.LerpUnclamped(Color.white, value, context.Intensity);
            state.colorMultiplier *= value;
        }
    }

    [Serializable]
    public sealed class WaveTextAnimationModule : TextAnimationModule
    {
        public Vector2 positionAmplitude = new Vector2(0f, 8f);
        public float rotationAmplitudeDegrees = 3f;
        public Vector2 scaleAmplitude = new Vector2(0.05f, 0.05f);
        public float shearAmplitude;
        [Min(0f)] public float temporalCycles = 1f;
        public float characterPhaseDegrees = 35f;
        public bool rotatePositionByContextDirection;
        public bool useCharacterProgressForEnvelope;

        internal override void ApplyGlyph(
            ref TextAnimationGlyphState state,
            in TextAnimationEvaluationContext context)
        {
            float progress = useCharacterProgressForEnvelope
                ? context.characterProgress
                : context.globalProgress;
            float envelope = Evaluate(progress) * context.Intensity;
            float phase = context.globalProgress * temporalCycles * Mathf.PI * 2f +
                          context.orderRank * characterPhaseDegrees * Mathf.Deg2Rad;
            float wave = Mathf.Sin(phase) * envelope;

            Vector2 position = rotatePositionByContextDirection
                ? TextAnimationMath.RotateFromPositiveX(positionAmplitude, context.Direction)
                : positionAmplitude;

            state.positionOffset += (Vector3)(position * wave);
            state.rotationDegrees += rotationAmplitudeDegrees * wave;
            state.scale = Vector2.Scale(state.scale, Vector2.one + scaleAmplitude * wave);
            state.shearX += shearAmplitude * wave;
        }
    }

    [Serializable]
    public sealed class NoiseTextAnimationModule : TextAnimationModule
    {
        public Vector2 positionAmplitude = new Vector2(6f, 4f);
        public float rotationAmplitudeDegrees = 2f;
        public Vector2 scaleAmplitude = new Vector2(0.04f, 0.04f);
        public float shearAmplitude;
        [Min(0.01f)] public float frequency = 18f;
        public bool rotatePositionByContextDirection;
        public bool useCharacterProgressForEnvelope;

        internal override void ApplyGlyph(
            ref TextAnimationGlyphState state,
            in TextAnimationEvaluationContext context)
        {
            float progress = useCharacterProgressForEnvelope
                ? context.characterProgress
                : context.globalProgress;
            float envelope = Evaluate(progress) * context.Intensity;
            float time = context.elapsedSeconds * Mathf.Max(0.01f, frequency);
            float seed = context.seed * 0.001731f + context.characterIndex * 0.137f;

            float nx = SignedPerlin(seed + 11.1f, time);
            float ny = SignedPerlin(seed + 37.7f, time * 1.173f);
            float nr = SignedPerlin(seed + 91.3f, time * 0.913f);
            float ns = SignedPerlin(seed + 123.9f, time * 1.311f);
            float nsh = SignedPerlin(seed + 173.3f, time * 0.731f);

            Vector2 position = new Vector2(
                nx * positionAmplitude.x,
                ny * positionAmplitude.y);
            if (rotatePositionByContextDirection)
                position = TextAnimationMath.RotateFromPositiveX(position, context.Direction);

            state.positionOffset += (Vector3)(position * envelope);
            state.rotationDegrees += nr * rotationAmplitudeDegrees * envelope;
            state.scale = Vector2.Scale(
                state.scale,
                Vector2.one + scaleAmplitude * (ns * envelope));
            state.shearX += nsh * shearAmplitude * envelope;
        }

        internal override void Validate()
        {
            base.Validate();
            frequency = Mathf.Max(0.01f, frequency);
        }

        private static float SignedPerlin(float x, float y)
        {
            return Mathf.PerlinNoise(x, y) * 2f - 1f;
        }
    }

    [Serializable]
    public sealed class SdfTextAnimationModule : TextAnimationModule
    {
        [Header("Additive Offsets From Persistent Style")]
        public float faceDilateFromOffset = -0.15f;
        public float faceDilateToOffset;
        public float outlineWidthFromOffset;
        public float outlineWidthToOffset;
        public float softnessFromOffset;
        public float softnessToOffset;
        public float glowOffsetFromOffset;
        public float glowOffsetToOffset;
        public float glowInnerFromOffset;
        public float glowInnerToOffset;
        public float glowOuterFromOffset;
        public float glowOuterToOffset;
        public float glowPowerFromOffset;
        public float glowPowerToOffset;

        [Header("Optional Color Override")]
        public bool forceGlowWhilePlaying;
        public bool useContextAccentForOutline;
        [Range(0f, 1f)] public float outlineAccentWeight;
        public bool useContextAccentForGlow;
        [Range(0f, 1f)] public float glowAccentWeight;

        internal override void ApplyMaterial(
            ref TMPMaterialTransientState state,
            in TextAnimationEvaluationContext context)
        {
            float t = Evaluate(context.globalProgress);
            float intensity = context.Intensity;

            state.faceDilateOffset += Mathf.LerpUnclamped(faceDilateFromOffset, faceDilateToOffset, t) * intensity;
            state.outlineWidthOffset += Mathf.LerpUnclamped(outlineWidthFromOffset, outlineWidthToOffset, t) * intensity;
            state.softnessOffset += Mathf.LerpUnclamped(softnessFromOffset, softnessToOffset, t) * intensity;
            state.glowOffsetOffset += Mathf.LerpUnclamped(glowOffsetFromOffset, glowOffsetToOffset, t) * intensity;
            state.glowInnerOffset += Mathf.LerpUnclamped(glowInnerFromOffset, glowInnerToOffset, t) * intensity;
            state.glowOuterOffset += Mathf.LerpUnclamped(glowOuterFromOffset, glowOuterToOffset, t) * intensity;
            state.glowPowerOffset += Mathf.LerpUnclamped(glowPowerFromOffset, glowPowerToOffset, t) * intensity;
            state.forceGlow |= forceGlowWhilePlaying;

            if (context.runtime.useAccentColor && useContextAccentForOutline && outlineAccentWeight > 0f)
            {
                state.overrideOutlineColor = true;
                state.outlineColor = context.runtime.accentColor;
                state.outlineColorWeight = Mathf.Max(state.outlineColorWeight, outlineAccentWeight * intensity);
            }

            if (context.runtime.useAccentColor && useContextAccentForGlow && glowAccentWeight > 0f)
            {
                state.overrideGlowColor = true;
                state.glowColor = context.runtime.accentColor;
                state.glowColorWeight = Mathf.Max(state.glowColorWeight, glowAccentWeight * intensity);
            }
        }
    }

    /// <summary>
    /// Draws progressively fainter full-text copies behind the primary TMP
    /// target. This produces a directional typographic echo without sacrificing
    /// the readability of the main text.
    /// </summary>
    [Serializable]
    public sealed class GhostTrailTextAnimationModule : TextAnimationModule
    {
        [Range(1, 12)] public int copyCount = 3;
        [Min(0f)] public float spacing = 6f;
        [Range(0f, 1f)] public float maximumOpacity = 0.38f;
        [Range(0f, 1f)] public float opacityFalloff = 0.62f;

        [Header("Directional Spread")]
        [Tooltip("Use the runtime direction supplied by the trigger or owning controller.")]
        public bool useContextDirection = true;
        public Vector2 authoredDirection = Vector2.right;
        [Min(0f)] public float spreadFrom = 0.25f;
        [Min(0f)] public float spreadTo = 1f;

        [Header("Appearance")]
        public bool useContextAccentColor = true;
        public Color tint = Color.white;
        [Range(-0.20f, 0.20f)] public float scaleStepPerCopy;

        public GhostTrailTextAnimationModule()
        {
            curve = TextAnimationMath.Pulse01();
        }

        internal override void ApplyEcho(
            ref TextAnimationEchoState state,
            in TextAnimationEvaluationContext context)
        {
            float intensity = context.Intensity;
            float envelope = Mathf.Max(0f, Evaluate(context.globalProgress));
            if (copyCount <= 0 || maximumOpacity <= 0f || envelope <= 0.0001f)
                return;

            Vector2 direction = useContextDirection
                ? context.Direction
                : authoredDirection;
            if (direction.sqrMagnitude < 0.000001f)
                direction = Vector2.right;
            else
                direction.Normalize();

            state.visible = true;
            state.copyCount = Mathf.Max(1, copyCount);
            state.direction = direction;
            state.spacing = Mathf.Max(0f, spacing) * intensity;
            state.spread = Mathf.LerpUnclamped(
                Mathf.Max(0f, spreadFrom),
                Mathf.Max(0f, spreadTo),
                Mathf.Clamp01(context.globalProgress));
            state.opacity = Mathf.Clamp01(maximumOpacity * envelope * Mathf.Min(1f, intensity));
            state.opacityFalloff = Mathf.Clamp01(opacityFalloff);
            state.tint = useContextAccentColor && context.runtime.useAccentColor
                ? context.runtime.accentColor
                : tint;
            state.scaleStepPerCopy = scaleStepPerCopy * intensity;
        }

        internal override void Validate()
        {
            base.Validate();
            copyCount = Mathf.Clamp(copyCount, 1, 12);
            spacing = Mathf.Max(0f, spacing);
            maximumOpacity = Mathf.Clamp01(maximumOpacity);
            opacityFalloff = Mathf.Clamp01(opacityFalloff);
            spreadFrom = Mathf.Max(0f, spreadFrom);
            spreadTo = Mathf.Max(0f, spreadTo);
            scaleStepPerCopy = Mathf.Clamp(scaleStepPerCopy, -0.20f, 0.20f);
        }
    }

    [Serializable]
    public sealed class RootMotionTextAnimationModule : TextAnimationModule
    {
        [Tooltip("Peak local offset. The module curve acts as the envelope.")]
        public Vector2 positionAtPeak;
        public float rotationAtPeakDegrees;
        public Vector2 scaleAtPeak = Vector2.one;
        public bool rotatePositionByContextDirection;

        [Header("Procedural Shake")]
        public Vector2 shakePositionAmplitude;
        public float shakeRotationAmplitudeDegrees;
        [Min(0.01f)] public float shakeFrequency = 24f;

        internal override void ApplyRoot(
            ref TextAnimationRootState state,
            in TextAnimationEvaluationContext context)
        {
            float envelope = Evaluate(context.globalProgress) * context.Intensity;
            float phase = context.elapsedSeconds * Mathf.Max(0.01f, shakeFrequency) * Mathf.PI * 2f;

            float sx = Mathf.Sin(phase + context.seed * 0.013f);
            float sy = Mathf.Sin(phase * 1.371f + 1.7f + context.seed * 0.007f);
            float sr = Mathf.Sin(phase * 1.117f + 0.8f + context.seed * 0.011f);

            Vector2 basePosition = rotatePositionByContextDirection
                ? TextAnimationMath.RotateFromPositiveX(positionAtPeak, context.Direction)
                : positionAtPeak;
            Vector2 shakePosition = new Vector2(
                sx * shakePositionAmplitude.x,
                sy * shakePositionAmplitude.y);
            if (rotatePositionByContextDirection)
                shakePosition = TextAnimationMath.RotateFromPositiveX(shakePosition, context.Direction);

            state.positionOffset += (Vector3)((basePosition + shakePosition) * envelope);
            state.eulerOffset += new Vector3(
                0f,
                0f,
                (rotationAtPeakDegrees + sr * shakeRotationAmplitudeDegrees) * envelope);

            Vector2 peak = Vector2.LerpUnclamped(Vector2.one, scaleAtPeak, envelope);
            state.scaleMultiplier = Vector3.Scale(
                state.scaleMultiplier,
                new Vector3(peak.x, peak.y, 1f));
        }

        internal override void Validate()
        {
            base.Validate();
            shakeFrequency = Mathf.Max(0.01f, shakeFrequency);
        }
    }
}
