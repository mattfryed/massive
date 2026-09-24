using UnityEngine;
using TMPro;
using Massive.Player;
using Massive.Multiplier;
using Massive.Resonance;
using Massive.Scoring;

namespace Massive.Settings
{
    [CreateAssetMenu(menuName = "MASSIVE/Shared Settings/MeleeVisualProfile")]
    public sealed class MeleeVisualProfile : SharedSettingsProfile
    {
        [Header("Treatment")]
        public MeleeVisualStyle visualStyle = MeleeVisualStyle.Plasma;
        [Tooltip("Shared asset. Per-player settings use a property block, not material instances.")]
        public Material plasmaMaterial;
        [Header("Layers — independent toggles")]
        public bool solidBody = true;
        public bool brightRim = true;
        public bool flowingFilaments = true;
        public bool softHalo = true;
        [Range(0, 1)] public float bodyOpacity = 1f;
        [Range(0, 1)] public float rimIntensity = .95f;
        [Range(0, 1)] public float filamentIntensity = .8f;
        [Range(0, 1)] public float haloIntensity = .08f;
        [Range(.01f, .18f)] public float rimWidth = .045f;
        [Header("Plasma motion")]
        [Range(0, 1)] public float edgeWarble = .3f;
        [Range(.25f, 12)] public float noiseScale = 3f;
        [Range(0, 5)] public float flowSpeed = 1f;
        [Header("Shape — visual tuning only")]
        [Tooltip("1 matches the sword capsule's outer reach. Does not change attack range.")]
        [Range(.5f, 1.5f)] public float reachScale = 1f;
        [Range(.3f, 2)] public float widthScale = 1f;
        [Tooltip("Curvature behind the live sword direction during a swipe; degrees.")]
        [Range(0, 65)] public float sweepBend = 32f;
        [Range(.02f, .5f)] public float rootOffset = .22f;
        [Range(0, .3f)] public float surfaceHeight = .06f;
        [Range(.025f, .4f)] public float repulsorBandWidth = .14f;
        [Header("Attack phases")]
        [Tooltip("Dim forming blade before the existing damage window opens.")]
        [Range(0, .6f)] public float windupOpacity = .22f;
        [Tooltip("Faint receding energy after the damage window closes.")]
        [Range(0, .6f)] public float recoveryOpacity = .16f;
        [Header("Asset slashes — thrust / Prick 5")]
        public MeleePrefabEffectSettings thrustPrefabEffect = new MeleePrefabEffectSettings
        {
            referenceReach = 5f, positionOffset = new Vector3(0, 0, .25f),
            sourceStartTime = .32f, sourceActiveEndTime = .65f, sourceRecoveryEndTime = .95f
        };
        [Header("Asset slashes — second arc / Sword Slash 5")]
        public MeleePrefabEffectSettings swipePrefabEffect = new MeleePrefabEffectSettings
        {
            rotationOffset = new Vector3(-90, -90, 0), referenceReach = 2.5f,
            sourceActiveEndTime = .18f, sourceRecoveryEndTime = .65f
        };
        [Header("Second arc — comparison")]
        public ArcSweepTreatment arcSweepTreatment = ArcSweepTreatment.Volumetric;
        public Material volumetricArcMaterial;
        [Range(.5f, 1.5f)] public float volumeReachScale = 1f;
        [Range(60, 300)] public float volumeArcDegrees = 235f;
        [Range(.05f, .8f)] public float volumeRadialWidth = .28f;
        [Range(.03f, 1)] public float volumeThickness = .3f;
        [Range(.1f, 6)] public float volumeDensity = 2.8f;
        [Range(.3f, 3)] public float volumeTaper = .8f;
        [Range(0, .3f)] public float volumeTurbulence = .075f;
        [Range(1, 12)] public float volumeNoiseScale = 4.5f;
        [Range(0, 5)] public float volumeFlowSpeed = 1.15f;
        public bool volumeBlackBody = true;
        public bool volumeWhiteEdge = true;
        public bool volumeFilaments = true;
        public bool volumeSatelliteWisp = true;
        [Range(0, 3)] public float volumeEdgeIntensity = .9f;
        [Range(0, 2)] public float volumeFilamentIntensity = .6f;
        [Header("Travelling tendrils and world-space aftermath")]
        [Range(2, 10)] public int volumeTendrilCount = 4;
        [Range(0, 6)] public int volumeExtraTendrils = 3;
        [Range(.12f, 1)] public float volumeTravelDuration = .30f;
        [Range(0, .12f)] public float volumeEmissionSpacing = .028f;
        [Range(.08f, .8f)] public float volumeTailLength = .40f;
        [Range(0, 1.5f)] public float volumeBranching = .8f;
        [Range(.25f, 6)] public float volumeConvergenceRate = 2.2f;
        [Range(0, 1.5f), Tooltip("Visual lifetime AFTER the collider window. Does not extend damage.")]
        public float volumeLingerSeconds = .55f;
        [Range(.5f, 4)] public float volumeDissolve = 1.5f;
        public bool volumeSheath = true;
        public bool volumeCounterflow = true;
        public bool volumeWake = true;
        [Range(0, 1.5f)] public float volumeSheathIntensity = .55f;
        [Range(0, 1.5f)] public float volumeCounterflowIntensity = .45f;
        [Range(0, 1.5f)] public float volumeWakeIntensity = .35f;
        [Header("Organic volume and local energy crackle")]
        public bool volumeOrganicGlow = true;
        [Range(0, 1)] public float volumeGlowBreakup = .7f;
        [Range(0, 1)] public float volumeGlowDepth = .65f;
        [Range(0, 4)] public float volumeGlowFlow = 1.3f;
        public bool volumeCrackle = true;
        [Range(0, 1)] public float volumeCrackleAmount = .35f;
        [Range(2, 30)] public float volumeCrackleScale = 12f;
        [Range(0, 25)] public float volumeCrackleRate = 9f;
        [Header("Thrust — emission and aftermath")]
        [Range(0, 1.5f), Tooltip("Visual fade after the attack stage. Does not extend damage or block the next attack.")]
        public float thrustLingerSeconds = .4f;
        [Range(.5f, 4f)] public float thrustFadeCurve = 1.3f;
        [Tooltip("Particles retain their birth position and aim. Only newly emitted particles follow the player.")]
        public bool thrustWorldEmission = true;
        [Range(1, 5), Tooltip("Body emission passes during one thrust. New passes follow current aim; previous particles stay in their birth frame. Applied on the next attack or restarted preview.")]
        public int thrustEmissionPasses = 3;
        [Header("Thrust — organic energy")]
        [Tooltip("Replaces the source's flat Glow layer with moving three-dimensional energy around emitted body particles. Off restores the authored glow.")]
        public bool thrustOrganicGlow = true;
        [Range(0, 2)] public float thrustGlowIntensity = .65f;
        [Range(0, 1)] public float thrustGlowBreakup = .7f;
        [Range(0, 1)] public float thrustGlowDepth = .6f;
        [Range(0, 4)] public float thrustGlowFlow = 1.3f;
        [Tooltip("Optional override. Uses the bundled MeleeThrustEnergy material when empty.")]
        public Material thrustEnergyMaterial;
        [Header("Third stage — outward repulsor pulse")]
        public RepulsorVisualTreatment repulsorTreatment = RepulsorVisualTreatment.SeismicDetonation;
        [Tooltip("Optional. An included shader supplies the pulse when this is empty.")]
        public Material repulsorPulseMaterial;
        [Range(.035f, .5f)] public float repulsorPulseWidth = .32f;
        [Range(.025f, .6f)] public float repulsorPulseThickness = .28f;
        [Range(.1f, 6)] public float repulsorPulseDensity = 2.8f;
        [Range(0, .4f)] public float repulsorPulseTurbulence = .30f;
        [Range(0, 1)] public float repulsorPulseBreakup = .7f;
        [Range(0, 5)] public float repulsorPulseFlow = 1.3f;
        [Range(0, 1)] public float repulsorPulseLinger = .42f;
        [Range(.5f, 4)] public float repulsorPulseFade = 1.25f;
        public bool repulsorPulseBlackBody = true;
        public bool repulsorPulseWhiteEdge = true;
        public bool repulsorPulseFilaments = true;
        public bool repulsorPulseWisps = true;
        [Range(0, 2)] public float repulsorPulseEdgeIntensity = .9f;
        [Range(0, 2)] public float repulsorPulseFilamentIntensity = 1f;
        [Range(0, 1)] public float repulsorPulseWispIntensity = .3f;
        [Header("Shockwave burst")]
        public bool repulsorReleaseFlash = true;
        public bool repulsorTurbulentWake = true;
        [Range(0f, 1f)] public float repulsorDissolveBreakup = .92f;
        [Header("Seismic detonation")]
        public bool repulsorImplosion = true;
        public bool repulsorBlastVolume = true;
        public bool repulsorSeismicStreak = true;
        [Range(0f, 3f)] public float repulsorRadiance = 1.35f;
        [Range(0f, 1f)] public float repulsorBlastOpacity = .38f;
        [Range(.1f, 1f)] public float repulsorBlastDepth = .55f;
        [Range(0f, 2f)] public float repulsorChargeIntensity = .8f;
        [Header("Repulsor — launch tears and broken crests")]
        [Tooltip("Uneven clusters of needle-ended pressure streaks, released with the main pulse.")]
        public bool repulsorBurstRays = true;
        [Tooltip("Short, curling pieces of the pressure crest leave a slower, fragmented wake.")]
        public bool repulsorBurstCrescents = true;
        [Range(0, 2)] public float repulsorBurstIntensity = .85f;
        [Tooltip("Length of the trailing detail. The leading edge still follows the pulse radius.")]
        [Range(.25f, 1.5f)] public float repulsorBurstSpread = 1f;
        [Header("Repulsor — seismic ejecta")]
        [Tooltip("Short, independently moving streaks accelerate away from the discharge and gather inward during its windup.")]
        public bool repulsorSparkEjecta = true;
        [Tooltip("Brief, hairline discharges fork through the expanding pulse. They do not form an orbiting ring.")]
        public bool repulsorArcDischarge = true;
        [Range(0, 1)] public float repulsorEjectaDensity = .65f;
        public PlayerRepulsorFeedbackSettings body = new PlayerRepulsorFeedbackSettings();
        public PlayerRepulsorGridPulseSettings grid = new PlayerRepulsorGridPulseSettings();
    }

    [System.Serializable]
    public sealed class PlayerRepulsorFeedbackSettings
    {
        [Header("Body Pulse")]
        public bool bodyPulseEnabled = true;
        [Range(0f, .3f)] public float contraction = .10f;
        [Tooltip("Extra visual size at the peak. .15 means 115% of this player's normal size.")]
        [Range(0f, .3f)] public float expansion = .15f;
        [Min(.01f)] public float reboundRiseTime = .055f;
        [Min(.02f)] public float settleTime = .30f;
        [Range(0f, .06f)] public float vibrationAmount = .018f;
        [Range(1f, 60f)] public float vibrationFrequency = 28f;
        [Header("Movement Recovery")]
        public bool recoveryEnabled = true;
        [Tooltip("Movement available immediately after Repulsor finishes; eases back to normal.")]
        [Range(.05f, 1f)] public float recoveryMovement = .55f;
        [Min(0f)] public float recoveryDuration = .35f;
    }

    [System.Serializable]
    public sealed class PlayerRepulsorGridPulseSettings
    {
        [Header("Grid Pulse")]
        public bool pulseEnabled = true;
        [Tooltip("Maximum grid displacement at player size 1. Does not affect gameplay forces.")]
        [Range(0f, 0.5f)] public float intensity = 0.12f;
        [Tooltip("Short outward travel from the activation point, at player size 1.")]
        [Min(0.1f)] public float travelRadius = 2.5f;
        [Min(0.05f)] public float duration = 0.45f;
        [Tooltip("Width of the localized crest and trough, at player size 1.")]
        [Min(0.05f)] public float packetWidth = 0.55f;
        [Tooltip("Small tangential curl alongside the radial wave. Zero produces a radial pulse.")]
        [Range(0f, 1f)] public float curl = 0.2f;
    }
}
