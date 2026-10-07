using System;
using System.Collections.Generic;
using UnityEngine;

namespace Massive.PowerUps
{
    /// <summary>One project-wide source, loaded from Resources in every scene and player build.</summary>
    public sealed class PowerUpSettings : ScriptableObject
    {
        public PowerUpVisualSettings visuals = new();
        public PowerUpLifecycleSettings lifecycle = new();
        public PowerUpSpawnSettings spawning = new();
        public PowerUpToastSettings toasts = new();
        public List<PowerUpDefinition> definitions = new();
        private static PowerUpSettings current;
        private static bool loaded;
        [NonSerialized] public int Revision;

        public static PowerUpSettings Current
        {
            get
            {
                if (!loaded) { current = Resources.Load<PowerUpSettings>("PowerUpSettings"); loaded = true; }
                return current;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reload() { current = null; loaded = false; }
        public void NotifyChanged() { ValidateValues(); unchecked { Revision++; } }
        private void OnValidate() => NotifyChanged();
        public void ValidateValues()
        {
            visuals.shellSeconds = Mathf.Max(.01f, visuals.shellSeconds);
            visuals.shellScale = Mathf.Max(.05f, visuals.shellScale);
            visuals.innerScale = Mathf.Max(.05f, visuals.innerScale);
            visuals.lineThickness = Mathf.Max(.001f, visuals.lineThickness);
            visuals.innerStartFraction = Mathf.Clamp01(visuals.innerStartFraction);
            visuals.innerSpawnSeconds = Mathf.Max(.01f, visuals.innerSpawnSeconds);
            visuals.innerAcquireSeconds = Mathf.Max(.01f, visuals.innerAcquireSeconds);
            visuals.particleSpawnSeconds = Mathf.Max(.01f, visuals.particleSpawnSeconds);
            visuals.particleAcquireSeconds = Mathf.Max(.01f, visuals.particleAcquireSeconds);
            visuals.faceSeparateSeconds = Mathf.Max(.01f, visuals.faceSeparateSeconds);
            visuals.faceUndrawSeconds = Mathf.Max(.01f, visuals.faceUndrawSeconds);
            visuals.faceUndrawDelay = Mathf.Max(0, visuals.faceUndrawDelay);
            visuals.faceDistance = Mathf.Max(0, visuals.faceDistance);
            lifecycle.worldLifetimeSeconds = Mathf.Max(0, lifecycle.worldLifetimeSeconds);
            lifecycle.cleanupDelaySeconds = Mathf.Max(0, lifecycle.cleanupDelaySeconds);
            lifecycle.effectDurationMultiplier = Mathf.Max(.01f, lifecycle.effectDurationMultiplier);
            spawning.minDelay = Mathf.Max(.05f, spawning.minDelay);
            spawning.maxDelay = Mathf.Max(spawning.minDelay, spawning.maxDelay);
            spawning.maxConcurrent = Mathf.Max(0, spawning.maxConcurrent);
            spawning.placementAttempts = Mathf.Clamp(spawning.placementAttempts, 1, 200);
            spawning.borderMargin = Mathf.Max(0, spawning.borderMargin);
            spawning.checkRadius = Mathf.Max(0, spawning.checkRadius);
            spawning.minPlayerDistance = Mathf.Max(0, spawning.minPlayerDistance);
            spawning.minPickupDistance = Mathf.Max(0, spawning.minPickupDistance);
            toasts.introSeconds = Mathf.Max(0, toasts.introSeconds);
            toasts.outroSeconds = Mathf.Max(0, toasts.outroSeconds);
            toasts.totalSeconds = Mathf.Max(toasts.introSeconds + toasts.outroSeconds, toasts.totalSeconds);
            toasts.worldScale = Mathf.Max(.0001f, toasts.worldScale);
        }
        public float WorldLifetime(PowerUpDefinition definition) =>
            lifecycle.useDefinitionLifetimes && definition ? Mathf.Max(0, definition.worldLifetimeSeconds) : lifecycle.worldLifetimeSeconds;
        public float EffectDuration(PowerUpDefinition definition) =>
            Mathf.Max(.01f, definition.effectDurationSeconds * lifecycle.effectDurationMultiplier);

        public float TotalWeight()
        {
            float total = 0;
            foreach (var def in definitions) if (def && def.pickupPrefab) total += Mathf.Max(0, def.spawnWeight);
            return total;
        }
        public PowerUpDefinition Select(float normalizedRandom)
        {
            float total = TotalWeight();
            if (total <= 0) return null; // Zero percent means never, including when the entire mixer is zero.
            float sample = Mathf.Clamp(normalizedRandom, 0, .99999994f) * total;
            PowerUpDefinition last = null;
            foreach (var def in definitions)
            {
                if (!def || !def.pickupPrefab || def.spawnWeight <= 0) continue;
                last = def; sample -= def.spawnWeight;
                if (sample < 0) return def;
            }
            return last;
        }
    }

    [Serializable]
    public sealed class PowerUpVisualSettings
    {
        [Header("Shell / inner icon")]
        [Tooltip("Scales the shell and both pickup colliders together, relative to the authored shell radius.")]
        [Min(.05f)] public float shellScale = 1;
        [Min(.05f)] public float innerScale = 1;
        [Min(.001f)] public float lineThickness = .02f;
        public Color shellColor = Color.white;
        [Header("Spawn / reversed natural despawn")]
        [Min(.01f)] public float shellSeconds = 1;
        [Range(0,1)] public float innerStartFraction = .5f;
        public AnimationCurve drawEase = AnimationCurve.EaseInOut(0,0,1,1);
        [Range(0,.4f)] public float edgeTimingVariation = .16f;
        [Range(0,.4f)] public float edgeSpeedVariation = .18f;
        public bool useUnscaledAnimationTime = true;
        [Header("Inner metaball icon")]
        [Min(.01f)] public float innerSpawnSeconds = .3f;
        [Min(.01f)] public float innerAcquireSeconds = .22f;
        [Min(0)] public float innerStagger = .03f;
        [Min(0)] public float innerJitter = .015f;
        [Range(0,2)] public float innerOvershoot = 1.15f;
        [Header("Inner particle icon (Mass Node)")]
        [Min(.01f)] public float particleSpawnSeconds = .25f;
        [Min(.01f)] public float particleAcquireSeconds = .18f;
        [Header("Acquire / separated face outlines")]
        [Min(.01f)] public float faceSeparateSeconds = .4f;
        [Min(.01f)] public float faceUndrawSeconds = .45f;
        [Min(0)] public float faceUndrawDelay = .06f;
        [Min(0)] public float faceDistance = .55f;
        public float faceSpinDegrees = 220;
        [Range(0,1)] public float faceDirectionRandomness = .35f;
        [Range(0,.4f)] public float faceTimingVariation = .12f;
        [Range(0,.4f)] public float faceSpeedVariation = .16f;
        public AnimationCurve separateEase = AnimationCurve.EaseInOut(0,0,1,1);
        public AnimationCurve undrawEase = AnimationCurve.EaseInOut(0,0,1,1);
    }
    [Serializable]
    public sealed class PowerUpLifecycleSettings
    {
        [Tooltip("Zero keeps a pickup alive until acquired. Tutorial never-expire flags remain respected.")]
        [Min(0)] public float worldLifetimeSeconds = 12;
        [Tooltip("Delay after the pickup is fully invisible before its GameObject is removed. Uses gameplay time.")]
        [Min(0)] public float cleanupDelaySeconds = 1;
        [Tooltip("Use each shared definition's world lifetime instead of the uniform lifetime above.")]
        public bool useDefinitionLifetimes;
        [Tooltip("Scales each shared definition's equipped duration. Does not affect instant Mass Node grants or explicit demo overrides.")]
        [Min(.01f)] public float effectDurationMultiplier = 1;
    }
    [Serializable]
    public sealed class PowerUpSpawnSettings
    {
        public bool enabled = true;
        [Min(.05f)] public float minDelay = 6;
        [Min(.05f)] public float maxDelay = 9;
        [Min(0)] public int maxConcurrent = 2;
        [Min(0)] public float borderMargin = .75f;
        [Min(0)] public float checkRadius = .6f;
        public LayerMask noSpawnMask = 512;
        [Min(0)] public float minPlayerDistance = 1.75f;
        [Min(0)] public float minPickupDistance = 1.5f;
        [Range(1,200)] public int placementAttempts = 30;
    }
    [Serializable]
    public sealed class PowerUpToastSettings
    {
        public bool enabled = true;
        public PowerUpPickupToast prefab;
        public bool showDescription = true;
        [Range(10,100)] public float descriptionPercent = 70;
        [Min(0)] public float introSeconds = .3f;
        [Min(0)] public float totalSeconds = 1.6f;
        [Min(0)] public float outroSeconds = .2f;
        public float worldYLift = .25f;
        public float screenUpOffset = 1;
        [Min(.0001f)] public float worldScale = .012f;
        public Vector2 panelPadding = new(.5f,.5f);
        [Min(0)] public float maxWidth;
        public bool billboard = true;
        public int sortingOrder = 5000;
        public AnimationCurve introEase = AnimationCurve.EaseInOut(0,0,1,1);
        public AnimationCurve outroEase = AnimationCurve.EaseInOut(0,0,1,1);
    }
}
