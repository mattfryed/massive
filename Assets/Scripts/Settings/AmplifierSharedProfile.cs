using UnityEngine;
using TMPro;
using Massive.Player;
using Massive.Multiplier;
using Massive.Resonance;
using Massive.Scoring;

namespace Massive.Settings
{
    [CreateAssetMenu(menuName = "MASSIVE/Shared Settings/AmplifierSharedProfile")]
    public sealed class AmplifierSharedProfile : SharedSettingsProfile
    {
        public AmplifierTreatmentSettings treatment = new AmplifierTreatmentSettings();
        public AmplifierCoreGameplaySettings core = new AmplifierCoreGameplaySettings();
        public AmplifierCoreVisualSettings coreSurface = new AmplifierCoreVisualSettings();
        public AmplifierGoalCaptureSettings goal = new AmplifierGoalCaptureSettings();
        public TeamAmplifierToastPresenterSettings toast = new TeamAmplifierToastPresenterSettings();
    }

    [System.Serializable]
    public sealed class AmplifierCoreGameplaySettings
    {
        [Header("Size & Motion")]
        [Tooltip("Scales the shell, energy core, and physical collider together. 1 preserves the authored size. Mass remains an independent setting.")]
        [SerializeField, Min(0.05f)] public float coreScale = 1f;
        [SerializeField, Min(0.01f)] public float mass = 3f;
        [Tooltip("Slows linear motion over time. Zero keeps momentum until an impact or goal force changes it.")]
        [SerializeField, Min(0f)] public float linearDrag = 0.42f;
        [SerializeField, Min(0f)] public float angularDrag = 0.6f;
        [Tooltip("Maximum speed across the playing field, in world units per second. Zero removes the limit.")]
        [SerializeField, Min(0f)] public float maximumPlanarSpeed = 25f;
        [Tooltip("Energy retained in the direction perpendicular to a wall. Wall friction is removed so glancing hits keep sliding. Also applies to solid Resonance surfaces.")]
        [SerializeField, Range(0f, 1f)] public float wallRestitution = 0.85f;
        [SerializeField, Min(0.05f)] public float spawnScaleSeconds = 0.38f;
        [SerializeField, Min(0.05f)] public float spawnShellMorphSeconds = 0.62f;
        [SerializeField, Min(0f)] public float spawnCoreDelaySeconds = 0.13f;
        [SerializeField, Min(0.05f)] public float spawnCoreRevealSeconds = 0.34f;
        [Header("Capture / Despawn")]
        [SerializeField, Min(0.05f)] public float captureTravelSeconds = 0.32f;
        [SerializeField] public AnimationCurve captureEase =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField, Min(0.05f)] public float despawnScaleSeconds = 0.4f;
        [SerializeField, Min(0.05f)] public float despawnShellMorphSeconds = 0.56f;
        [SerializeField, Min(0.05f)] public float despawnCoreHideSeconds = 0.2f;
        [Header("Attack Impact")]
        [SerializeField, Min(0f)] public float attackImpulse = 20f;
        [SerializeField, Range(0f, 1f)] public float playerPlanarVelocityRetention = 0.03f;
        [SerializeField, Min(0f)] public float playerImpactStopSeconds = 0.12f;
        [SerializeField, Min(0f)] public float repeatImpactLockoutSeconds = 0.12f;
    }

    [System.Serializable]
    public sealed class AmplifierCoreVisualSettings
    {
        [Header("Idle Motion")]
        [SerializeField] public Vector3 energyRotationAxis = new Vector3(0.35f, 0.9f, 0.15f);
        [SerializeField] public float energyRotationSpeed = -17f;
        [SerializeField] public Vector3 shellRotationAxis = new Vector3(-0.2f, 0.45f, 1f);
        [SerializeField] public float shellRotationSpeed = 11f;
        [SerializeField, Range(0f, 0.1f)] public float breatheAmplitude = 0.025f;
        [SerializeField, Min(0f)] public float breatheFrequency = 0.85f;
        [Header("Lifecycle Reveal")]
        [SerializeField, Min(1.5f)] public float spawnOrganicRibbonScale = 1.5f;
        [Header("Time-out Warning")]
        [Tooltip("Erratic surface deformation during the encounter's final warning seconds. This affects the shell pattern only, not the Core's position or collision.")]
        [SerializeField, Range(0f, 2f)] public float timeoutInstability = 1f;
    }

    [System.Serializable]
    public sealed class AmplifierGoalCaptureSettings
    {
        [SerializeField, Min(0f)] public float pullAcceleration = 24f;
        [SerializeField] public AnimationCurve pullByProximity =
            AnimationCurve.EaseInOut(0f, 0.15f, 1f, 1f);
        [Header("Goal at maximum multiplier")]
        [SerializeField] public bool repelWhenMaxed = true;
        [SerializeField, Min(0f)] public float repulsionAcceleration = 32f;
        [Tooltip("Minimum outward speed when the Core touches a full goal. Prevents it sitting inside the capture aperture.")]
        [SerializeField, Min(0f)] public float repulsionMinimumSpeed = 3f;
    }

    [System.Serializable]
    public sealed class TeamAmplifierToastPresenterSettings
    {
        [SerializeField] public TMP_FontAsset font;
        [Header("Placement and type")]
        [Tooltip("Screen position: X 0 = left, 1 = right; Y 0 = bottom, 1 = top.")]
        [SerializeField] public Vector2 lightAnchor = new Vector2(.18f, .63f);
        [Tooltip("Screen position: X 0 = left, 1 = right; Y 0 = bottom, 1 = top.")]
        [SerializeField] public Vector2 darkAnchor = new Vector2(.82f, .63f);
        [SerializeField, Min(.1f)] public float overallScale = 1f;
        [SerializeField, Min(8f)] public float teamLabelSize = 24f;
        [SerializeField, Min(12f)] public float multiplierSize = 88f;
        [SerializeField] public string lightLabel = "LIGHT TEAM AMPLIFIED";
        [SerializeField] public string darkLabel = "DARK TEAM AMPLIFIED";
        [Header("Timing")]
        [SerializeField, Min(.05f)] public float lifetime = 2.2f;
        [SerializeField, Min(0f)] public float fadeInSeconds = .12f;
        [SerializeField, Min(0f)] public float fadeOutSeconds = .45f;
        [SerializeField] public bool useUnscaledTime = true;
        [Header("Max amplification notice")]
        [SerializeField] public bool showMaxAmplification = true;
        [SerializeField] public string maxAmplificationLabel = "MAX AMPLIFICATION";
        [SerializeField, Min(8f)] public float maxNoticeFontSize = 28f;
        [SerializeField, Min(.1f)] public float maxNoticeScale = 1f;
        [Tooltip("Offset from each team's anchor in reference-resolution pixels. Negative Y places it below the capture toast.")]
        [SerializeField] public Vector2 maxNoticeOffset = new Vector2(0f, -85f);
        [SerializeField, Min(.05f)] public float maxNoticeLifetime = 1.35f;
        [Tooltip("Minimum time between notices for the same team, even if several Cores arrive together.")]
        [SerializeField, Min(0f)] public float maxNoticeRepeatDelay = 1.75f;
        [Header("Tier effects — ×2 always stays plain white")]
        [SerializeField] public bool rainbowColors = true;
        [SerializeField, Min(0f)] public float rainbowSpeed = .32f;
        [SerializeField] public bool vibration = true;
        [SerializeField, Min(0f)] public float vibrationPixels = 3f;
        [SerializeField, Min(0f)] public float vibrationFrequency = 24f;
        [SerializeField, Range(0f, .5f)] public float scalePunch = .16f;
        [SerializeField] public bool ghostTrails = true;
        [SerializeField, Range(0f, 1f)] public float ghostOpacity = .34f;
        [SerializeField, Min(.02f)] public float ghostLifetime = .3f;
        [SerializeField, Min(0f)] public float ghostTravelPixels = 34f;
        [SerializeField, Range(1f, 30f)] public float ghostsPerSecond = 16f;
    }
}
