using System;
using Massive.TextAnimation;
using UnityEngine;

namespace Massive.Scoring
{
    /// <summary>
    /// Pure presentation data for the six MASSIVE energy classes.
    ///
    /// Keep this asset visual-only. Score values and gameplay balance remain in
    /// ScoreEconomyProfile, while this profile controls TMP/SDF styling and the
    /// generic promotion response for each destination tier.
    /// </summary>
    [CreateAssetMenu(
        fileName = "EnergyTierVisualProfile",
        menuName = "MASSIVE/Scoring/Energy Tier Visual Profile")]
    public sealed class EnergyTierVisualProfile : ScriptableObject
    {
        [Header("Tier Indicator State")]
        [Tooltip("Color used by every tier indicator except the currently active tier.")]
        [SerializeField] private Color inactiveTierIndicatorColor =
            new Color(0.30f, 0.32f, 0.36f, 1f);

        [Serializable]
        public sealed class TierStyle
        {
            [Header("Identity")]
            public EnergyUnit unit;

            [Header("Persistent Text Color")]
            public Color scoreColor = Color.white;
            public Color activeUnitColor = Color.white;
            public Color activeIndicatorColor = Color.white;

            [Header("Tier Indicator Font Size")]
            [Tooltip("Multiplier applied to the active tier label's authored TMP font size. Every inactive tier returns to its authored size and inactive gray.")]
            [Min(0.01f)] public float activeIndicatorFontSizeMultiplier = 1f;

            [Header("TMP SDF")]
            public Color outlineColor = Color.black;
            [Range(0f, 1f)] public float outlineWidth;
            [Range(-1f, 1f)] public float faceDilate;
            [Range(0f, 1f)] public float softness;

            public bool enableGlow;
            public Color glowColor = Color.white;
            [Range(-1f, 1f)] public float glowOffset;
            [Range(0f, 1f)] public float glowInner;
            [Range(0f, 1f)] public float glowOuter;
            [Range(0f, 1f)] public float glowPower = 0.5f;

            [Header("Reusable Text Animation")]
            [Tooltip("Optional preset played on the destination tier label. When assigned, this replaces generic tier-root motion unless the controller explicitly enables both.")]
            public TextAnimationPreset promotionTextPreset;

            [Min(0f)] public float promotionTextIntensity = 1f;
            public Vector2 promotionTextDirection = Vector2.right;

            [Tooltip("When enabled, the text preset owns this tier's motion root. Generic tier-root punch and shake remain the fallback when no usable preset is available.")]
            public bool promotionTextPresetReplacesGenericMotion = true;

            [Tooltip("Pass this tier's score color into the preset as its runtime accent color.")]
            public bool useTierColorAsAnimationAccent = true;

            [Header("Active Tier Baseline Animation")]
            [Tooltip("Optional low-intensity looping preset played after this tier's activation transition completes. It stops as soon as the tier becomes inactive.")]
            public TextAnimationPreset activeLoopTextPreset;

            [Min(0f)] public float activeLoopTextIntensity = 0.25f;
            public Vector2 activeLoopTextDirection = Vector2.right;

            [Tooltip("Pass this tier's score color into the active-loop preset as its runtime accent color.")]
            public bool useTierColorAsActiveLoopAccent = true;

            [Header("Generic Promotion Motion")]
            [Min(0f)] public float promotionSeconds = 0.20f;
            [Min(1f)] public float punchScale = 1.04f;
            [Min(0f)] public float shakePosition;
            [Min(0f)] public float shakeRotationDegrees;
            [Min(0.01f)] public float shakeFrequency = 24f;
            public AnimationCurve promotionEnvelope = EnergyTierVisualProfile.CreatePulseEnvelope();

            [Header("Optional Promotion Assets")]
            [Tooltip("Trigger sent to the controller's shared Animator for this tier.")]
            public string animatorTrigger;

            [Tooltip("One-shot prefab spawned at the controller's VFX anchor.")]
            public GameObject promotionVfxPrefab;

            [Min(0f)] public float promotionVfxLifetime = 2f;
            public AudioClip promotionAudio;

            [Header("Optional Persistent Tier Asset")]
            [Tooltip("Prefab kept alive while this is the active tier, then replaced on the next promotion.")]
            public GameObject activeLoopVfxPrefab;
        }

        [SerializeField] private TierStyle[] tiers = new TierStyle[0];

        public Color InactiveTierIndicatorColor => inactiveTierIndicatorColor;

        public TierStyle GetStyle(EnergyUnit unit)
        {
            if (tiers != null)
            {
                for (int i = 0; i < tiers.Length; i++)
                {
                    TierStyle style = tiers[i];
                    if (style != null && style.unit == unit)
                        return style;
                }
            }

            return null;
        }

        [ContextMenu("Reset To Suggested Visual Defaults")]
        public void ResetToSuggestedDefaults()
        {
            tiers = new[]
            {
                CreateStyle(
                    EnergyUnit.MilliElectronVolt,
                    new Color(0.96f, 0.96f, 0.96f, 1f),
                    outlineWidth: 0f,
                    glow: 0f,
                    seconds: 0.12f,
                    punch: 1.025f,
                    shake: 0f,
                    rotation: 0f),

                CreateStyle(
                    EnergyUnit.ElectronVolt,
                    new Color(0.72f, 0.94f, 1f, 1f),
                    outlineWidth: 0.04f,
                    glow: 0.18f,
                    seconds: 0.18f,
                    punch: 1.055f,
                    shake: 1.0f,
                    rotation: 0.2f),

                CreateStyle(
                    EnergyUnit.KiloElectronVolt,
                    new Color(0.38f, 0.72f, 1f, 1f),
                    outlineWidth: 0.08f,
                    glow: 0.28f,
                    seconds: 0.22f,
                    punch: 1.08f,
                    shake: 1.8f,
                    rotation: 0.5f),

                CreateStyle(
                    EnergyUnit.MegaElectronVolt,
                    new Color(0.70f, 0.46f, 1f, 1f),
                    outlineWidth: 0.12f,
                    glow: 0.38f,
                    seconds: 0.27f,
                    punch: 1.11f,
                    shake: 2.8f,
                    rotation: 0.9f),

                CreateStyle(
                    EnergyUnit.GigaElectronVolt,
                    new Color(1f, 0.48f, 0.16f, 1f),
                    outlineWidth: 0.16f,
                    glow: 0.50f,
                    seconds: 0.34f,
                    punch: 1.15f,
                    shake: 4.2f,
                    rotation: 1.5f),

                CreateStyle(
                    EnergyUnit.TeraElectronVolt,
                    new Color(1f, 0.78f, 0.18f, 1f),
                    outlineWidth: 0.22f,
                    glow: 0.68f,
                    seconds: 0.48f,
                    punch: 1.22f,
                    shake: 6.5f,
                    rotation: 2.4f)
            };
        }

        private void OnEnable()
        {
            if (tiers == null || tiers.Length == 0)
                ResetToSuggestedDefaults();
        }

        private void Reset()
        {
            ResetToSuggestedDefaults();
        }

        private void OnValidate()
        {
            if (tiers == null || tiers.Length == 0)
                return;

            for (int i = 0; i < tiers.Length; i++)
            {
                TierStyle style = tiers[i];
                if (style == null) continue;

                style.promotionTextIntensity = Mathf.Max(0f, style.promotionTextIntensity);
                if (style.promotionTextDirection.sqrMagnitude < 0.000001f)
                    style.promotionTextDirection = Vector2.right;

                style.activeIndicatorFontSizeMultiplier =
                    Mathf.Max(0.01f, style.activeIndicatorFontSizeMultiplier);

                style.activeLoopTextIntensity = Mathf.Max(0f, style.activeLoopTextIntensity);
                if (style.activeLoopTextDirection.sqrMagnitude < 0.000001f)
                    style.activeLoopTextDirection = Vector2.right;

                style.promotionSeconds = Mathf.Max(0f, style.promotionSeconds);
                style.punchScale = Mathf.Max(1f, style.punchScale);
                style.shakePosition = Mathf.Max(0f, style.shakePosition);
                style.shakeRotationDegrees = Mathf.Max(0f, style.shakeRotationDegrees);
                style.shakeFrequency = Mathf.Max(0.01f, style.shakeFrequency);
                style.promotionVfxLifetime = Mathf.Max(0f, style.promotionVfxLifetime);
            }
        }

        private static TierStyle CreateStyle(
            EnergyUnit unit,
            Color color,
            float outlineWidth,
            float glow,
            float seconds,
            float punch,
            float shake,
            float rotation)
        {
            return new TierStyle
            {
                unit = unit,
                scoreColor = color,
                activeUnitColor = color,
                activeIndicatorColor = color,
                outlineColor = color,
                outlineWidth = outlineWidth,
                faceDilate = Mathf.Lerp(0f, 0.15f, glow),
                softness = Mathf.Lerp(0f, 0.08f, glow),
                enableGlow = glow > 0f,
                glowColor = color,
                glowOffset = 0f,
                glowInner = Mathf.Clamp01(glow * 0.35f),
                glowOuter = Mathf.Clamp01(glow),
                glowPower = Mathf.Clamp01(0.40f + glow * 0.45f),
                promotionSeconds = seconds,
                punchScale = punch,
                shakePosition = shake,
                shakeRotationDegrees = rotation,
                shakeFrequency = Mathf.Lerp(22f, 38f, glow),
                promotionEnvelope = EnergyTierVisualProfile.CreatePulseEnvelope(),
                promotionVfxLifetime = 2f
            };
        }
        internal static AnimationCurve CreatePulseEnvelope()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 4f),
                new Keyframe(0.45f, 1f, 0f, 0f),
                new Keyframe(1f, 0f, -3f, 0f));
        }

    }
}
