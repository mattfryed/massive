using System;
using UnityEngine;

namespace Massive.Multiplier
{
    public enum AmplifierSeamMode { Off, [InspectorName("Clustered color fringe")] NodalSeams, [InspectorName("Drifting color interference")] ModeBeating }
    [Serializable]
    public sealed class AmplifierShimmerSettings
    {
        public bool enabled = true;
        [Tooltip("Larger values make the pattern larger and more widely spaced.")]
        [Range(0.2f, 5)] public float scale = 1;
        [Tooltip("Zero removes the pattern modulation while retaining the layer.")]
        [Range(0, 2)] public float contrast = 1;
        [Range(0, 3)] public float curl = 1;
        [Tooltip("Zero freezes pattern drift; negative values reverse it. Other goal motion continues.")]
        [Range(-3, 3)] public float driftSpeed = 1;
        public Vector4 ShaderValues => new Vector4(Mathf.Max(0.2f,scale),enabled?contrast:0,curl,driftSpeed);
    }
    [Serializable]
    public sealed class AmplifierTreatmentSettings
    {
        [Header("Effect containment")]
        [Tooltip("Off clips all goal-surface effects at the moving grid boundary and suppresses field discharge. Boundary-line treatments remain independent.")]
        public bool allowEffectsOverGrid = true;
        [Header("Color interference — fine spectral strands at the edge")]
        public AmplifierSeamMode seamMode = AmplifierSeamMode.Off;
        public bool seamCapture = true;
        public bool seamHeld = true;
        [Range(0, 3)] public float seamCaptureIntensity = 1.3f;
        [Range(0, 1)] public float seamHeldIntensity = 0.35f;
        [Range(0.01f, 0.15f)] public float seamWidth = 0.055f;
        [Range(0, 0.3f)] public float seamReach = 0.10f;
        [Range(2, 12)] public int seamModeCount = 5;
        [Range(0, 1)] public float seamModeMix = 0.6f;
        [Range(0, 2)] public float seamBeatFrequency = 0.18f;
        [Range(0, 1)] public float seamDrift = 0.12f;
        [Range(0, 1)] public float seamSpectralSplit = 0.55f;
        public AmplifierShimmerSettings interferenceShimmer = new AmplifierShimmerSettings();
        [Header("Corona layers")]
        public bool corona = true;
        [InspectorName("Cloud Corona")]
        public bool particleCloud;
        public bool flareCorona = true;
        public bool atmosphereWisps;
        [Header("Cloud corona — fine granular falloff")]
        [Range(0, 3)] public float cloudIntensity = 1.6f;
        [Range(0.02f, 0.4f)] public float cloudExtent = 0.16f;
        [Range(0, 1)] public float cloudDensity = 0.75f;
        public AmplifierShimmerSettings cloudShimmer = new AmplifierShimmerSettings();
        [Header("Flare corona — localized curling filaments")]
        [Range(0, 3)] public float flareIntensity = 1.5f;
        [Range(0.01f, 0.3f)] public float flareExtent = 0.085f;
        public AmplifierShimmerSettings flareShimmer = new AmplifierShimmerSettings();
        [Header("Atmosphere wisps — optional wider smoke-like curls")]
        [Range(0, 2)] public float atmosphereIntensity = 0.7f;
        [Range(0.05f, 0.6f)] public float atmosphereExtent = 0.3f;
        public AmplifierShimmerSettings wispShimmer = new AmplifierShimmerSettings();
        [Header("Corona palette")]
        public Color coronaColorA = new Color(0.08f,0.9f,1);
        public Color coronaColorB = new Color(0.9f,0.05f,0.75f);
        public Color coronaColorC = new Color(1,0.65f,0.04f);
        [Header("Shared corona shape")]
        [Range(0.005f, 0.3f)] public float radialExtent = 0.045f;
        [Range(0, 1)] public float density = 0.55f;
        [Range(0, 3)] public float intensity = 1;
        [Range(0, 1)] public float nodalClustering = 0.65f;
        [Range(0.001f, 0.015f)] public float grainSize = 0.0025f;
        [Range(0, 1)] public float chromaticSeparation = 0.35f;
        [Range(0, 1)] public float tangleDensity = 0.65f;
        [Range(0, 0.3f)] public float solarWispReach = 0.11f;
        [Header("Whole goal surface pulse")]
        public bool wholeGoalPulse = true;
        [Range(0, 0.4f)] public float goalPulseAmplitude = 0.12f;
        [Range(0, 0.04f)] public float goalHeldMotion = 0.006f;
        [Tooltip("Scales capture packets, held waves and the optional discharge on the grid only.")]
        [Range(0, 1)] public float gridCoupling = 0.25f;
        [Range(0.1f, 5)] public float falloff = 2.2f;
        [Header("Superposed perimeter waves: amplitude / cycles / phase drift")]
        public Vector3 waveA = new Vector3(0.48f, 3, 0.17f);
        public Vector3 waveB = new Vector3(0.3f, 7, -0.11f);
        public Vector3 waveC = new Vector3(0.22f, 17, 0.07f);
        [Header("Capture burst")]
        public bool captureBurst = true;
        [Range(0, 3)] public float burstIntensity = 1;
        [Range(0.1f, 5)] public float burstDuration = 1.8f;
        [Range(0, 1)] public float absorptionSeconds = 0.32f;
        [Header("Boundary wave packets")]
        public bool capturePackets = true;
        [Range(0, 1.5f)] public float carrierAmplitude = 0.32f;
        [Range(0.25f, 5)] public float wavelength = 1.1f;
        [Range(0, 6)] public float carrierFrequency = 1.8f;
        [Range(0.1f, 3)] public float envelopeWidth = 0.65f;
        [Range(0, 12)] public float propagationSpeed = 3.4f;
        [Range(0, 3)] public float dispersion = 0.65f;
        [Range(-2, 2)] public float alongAcrossRatio = 0.8f;
        [Range(-180, 180)] public float quadraturePhase = 90;
        [Range(1, 5)] public int emissions = 2;
        [Range(0.08f, 2)] public float packetSpacing = 0.4f;
        [Header("Held charge (independent of capture)")]
        public bool heldCharge = true;
        [Range(0, 1)] public float heldIntensity = 0.24f;
        public bool coloredBoundary = true;
        public bool travellingWave = true;
        [Range(0, 0.2f)] public float heldAmplitude = 0.025f;
        public bool occasionalPackets;
        [Range(0.5f, 12)] public float heldPacketInterval = 4;
        [Header("Polar regions")]
        public bool polar;
        public bool alternatePoles;
        public bool addPolesWithMultiplier;
        [Range(2, 8)] public int poleCount = 2;
        [Range(0, 360)] public float poleSeparation = 180;
        [Range(-180, 180)] public float polePhase = 90;
        [Range(0.05f, 1)] public float poleWidth = 0.28f;
        [Header("Living filaments")]
        public bool branching;
        [Range(0, 1)] public float branchDensity = 0.35f;
        [Range(0.01f, 0.5f)] public float branchReach = 0.08f;
        [Range(0.2f, 6)] public float branchLifetime = 1.7f;
        [Range(0, 1)] public float reconnect = 0.15f;
        [Range(0, 1)] public float multiplierDensity = 0.25f;
        [Range(0, 1)] public float multiplierPersistence = 0.2f;
        [Range(0, 1)] public float multiplierReach = 0.12f;
        [Header("Optional capture discharge through existing grid")]
        public bool gridDischarge;
        [Range(0, 2)] public float gridAmplitude = 0.65f;
        [Range(0, 14)] public float gridSpeed = 5;
        [Range(0.2f, 5)] public float gridWidth = 1.4f;
        [Range(0.3f, 8)] public float gridWavelength = 3;
        [Range(-2, 2)] public float gridCurl = 1.1f;

        public static readonly string[] Names = { "Restrained / dispersive", "Cloud / nodal packets", "Polar / living filaments", "Curling field discharge" };
        public static readonly string[] SeamNames = { "Off", "Clustered fringe", "Drifting interference" };
        public static AmplifierTreatmentSettings Preset(int index)
        {
            var s = new AmplifierTreatmentSettings();
            if (index == 1) { s.particleCloud = true; s.flareCorona=false; s.radialExtent = 0.06f; s.density = 0.8f; s.nodalClustering = 0.82f; s.occasionalPackets = true; }
            if (index == 2) { s.polar = true; s.alternatePoles = true; s.branching = true; s.occasionalPackets = true; s.density = 0.42f; }
            if (index == 3) { s.gridDischarge = true; s.branching = true; s.branchDensity = 0.2f; s.particleCloud = true; s.atmosphereWisps=true; s.radialExtent = 0.05f; }
            return s;
        }
    }
}
