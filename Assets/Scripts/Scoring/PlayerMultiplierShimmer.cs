using Massive.Settings;
using UnityEngine;

namespace Massive.Scoring
{
    [System.Serializable]
    public sealed class PlayerMultiplierShimmerSettings
    {
        public bool enabled = true;
        [Min(1f)] public float startsAtMultiplier = 8f;
        [Min(1f)] public float fullIntensityMultiplier = 16f;
        [Tooltip("Visible strength when the threshold is first reached.")]
        [Range(0f, 1f)] public float startingIntensity = .25f;
        [Range(0f, 1f)] public float outlineOpacity = .85f;
        [Tooltip("Shimmer reach relative to the player's body radius.")]
        [Range(.02f, 1f)] public float outlineReach = .32f;
        [Range(0f, 3f)] public float barIntensity = 1.6f;
        [Range(.2f, 5f)] public float patternScale = 2f;
        [Range(0f, 2f)] public float contrast = .65f;
        [Range(0f, 3f)] public float curl = 1f;
        [Range(-3f, 3f)] public float driftSpeed = 1f;

        public float Strength(double multiplier)
        {
            if (!enabled || double.IsNaN(multiplier) || multiplier < startsAtMultiplier) return 0f;
            float progress = Mathf.InverseLerp(startsAtMultiplier,
                Mathf.Max(startsAtMultiplier + .001f, fullIntensityMultiplier), (float)multiplier);
            return Mathf.Lerp(Mathf.Clamp01(startingIntensity), 1f, progress);
        }
    }

    // Presentation only: the personal chain remains the sole multiplier authority.
    public static class PlayerMultiplierShimmer
    {
        private static readonly PlayerMultiplierShimmerSettings Defaults = new();
        private static readonly int ShimmerId = Shader.PropertyToID("_MultiplierShimmer");
        private static readonly int DetailId = Shader.PropertyToID("_MultiplierShimmerDetail");
        private static readonly int PaletteAId = Shader.PropertyToID("_MultiplierPaletteA");
        private static readonly int PaletteBId = Shader.PropertyToID("_MultiplierPaletteB");
        private static readonly int PaletteCId = Shader.PropertyToID("_MultiplierPaletteC");
        public static PlayerMultiplierShimmerSettings Settings =>
            SharedSettingsRuntime.Load<PlayerTuningProfile>()?.multiplierShimmer ?? Defaults;

        public static float Strength(PlayerScoreChain chain)
        {
            if (!chain || !chain.isActiveAndEnabled || !chain.Player ||
                (!chain.Player.ParticipatesInMatch && !chain.Player.SimulationRoot) || chain.Player.IsSpawning) return 0f;
            return Settings.Strength(chain.CurrentMultiplier);
        }

        public static void Apply(MaterialPropertyBlock properties, PlayerScoreChain chain, float strength, float reach)
        {
            float phase = chain && chain.Player ? chain.Player.playerID * .73f : 0f;
            Apply(properties, Settings, strength, reach, Time.unscaledTime, phase);
        }

        // Explicit clock lets editor previews use the identical shader inputs without a live score chain.
        public static void Apply(MaterialPropertyBlock properties, PlayerMultiplierShimmerSettings settings,
            float strength, float reach, float seconds, float phase = 0f)
        {
            properties.SetVector(ShimmerId, new Vector4(strength, Mathf.Max(.001f, reach),
                seconds * settings.driftSpeed + phase, Mathf.Max(.2f, settings.patternScale)));
            properties.SetVector(DetailId, new Vector4(settings.contrast, settings.curl, 0f, 0f));
            // Share the goal corona's cyan/magenta/gold palette, including live tuning.
            var goal = SharedSettingsRuntime.Load<AmplifierSharedProfile>()?.treatment;
            properties.SetColor(PaletteAId, goal != null ? goal.coronaColorA : new Color(.08f, .9f, 1f));
            properties.SetColor(PaletteBId, goal != null ? goal.coronaColorB : new Color(.9f, .05f, .75f));
            properties.SetColor(PaletteCId, goal != null ? goal.coronaColorC : new Color(1f, .65f, .04f));
        }
    }
}
