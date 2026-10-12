using UnityEngine;

namespace Massive.Multiplier
{
    public enum AmplifierInteractionMode { Physical = 0, PossessionShot = 1 }
    public enum AmplifierControlState { Available, Aiming, Contested, Launched }

    [System.Serializable]
    public sealed class AmplifierPossessionSettings
    {
        [Tooltip("Physical preserves attack/beam pushing. Possession Shot uses Thrust to enter, aim, hold and release Attack.")]
        public AmplifierInteractionMode mode;
        [Header("Aim and charge")]
        [Min(.25f)] public float possessionSeconds = 5f;
        [Min(.1f)] public float fullChargeSeconds = 2f;
        [Tooltip("Approximate unobstructed coast distance at minimum charge. Charge changes drag, not damage or initial speed; walls and fields can change the actual distance.")]
        [Min(.1f)] public float minimumRange = 5f;
        [Min(.1f)] public float maximumRange = 26f;
        [Min(.1f)] public float shotSpeed = 16f;
        [Tooltip("After this time the shot smoothly brakes to rest. It never stops at a distance cutoff.")]
        [Min(1f)] public float maximumFlightSeconds = 15f;
        [Tooltip("Below this speed, after a short settling period, the Core becomes enterable again.")]
        [Min(.01f)] public float settledSpeed = .15f;
        [Tooltip("Rolling rotation from travel and collision direction. Zero leaves only the Core's idle shell motion.")]
        [Range(0f, 1f)] public float rollingSpin = .45f;
        [Tooltip("NPC damage relative to that NPC's normal sword-hit damage. Independent of charge and range; never damages players.")]
        [Min(0f)] public float npcDamageMultiplier = 1f;
        [Min(0f)] public float npcPushSpeed = 8f;
        [Header("Contest")]
        [Min(.25f)] public float contestSeconds = 3f;
        [Range(.01f, .5f)] public float progressPerPress = .085f;
        [Tooltip("Maximum credited alternating presses per second, per player. Holding or pressing both together earns nothing.")]
        [Range(1f, 12f)] public float pressesPerSecond = 6f;
        [Min(0f)] public float loserStunSeconds = .8f;
        [Min(.25f)] public float winnerAimSeconds = 2.5f;
        [Min(0f)] public float reentryDelay = .8f;
        [Header("Release and defence")]
        [Min(0f)] public float ejectionRecoil = 2f;
        [Range(0f, 1f)] public float parryRangeRetention = .8f;
        [Range(0f, 1f)] public float blockRangeRetention = .35f;
        [Range(0f, 1f)] public float blockSpeedRetention = .5f;
        [Header("Possession marker")]
        [Range(.1f, .6f)] public float playerMarkerScale = .33f;
        [Tooltip("Outline thickness as a fraction of the player marker's radius.")]
        [Range(.01f, .2f)] public float playerMarkerOutline = .075f;
        [Tooltip("Clear space around the possession circle, as a fraction of its radius. All shell layers flow around this opening.")]
        [Range(.03f, .5f)] public float markerClearance = .16f;
        [InspectorName("Shell Gap Blending")]
        [Tooltip("How broadly each indicator opening merges into the shell's moving noise gaps. Zero gives a tight circular opening; higher values form broader organic joins, without pushing the noise away.")]
        [Range(0f, 2f)] public float markerFlowDeflection = 1f;
        [Header("Entry / exit — SINGULARITY tidal stretch")]
        public bool animatePlayerTransit = true;
        [Min(.05f)] public float entrySeconds = .38f;
        [Min(.05f)] public float exitSeconds = .32f;
        [Range(.1f, 8f)] public float transitExponent = 3.5f;
        [Range(.005f, .3f)] public float transitMinimumScale = .045f;
        [Range(1f, 8f)] public float transitMaximumStretch = 3f;
        [Header("Aim guide — dots grow outward with shot charge")]
        public bool showTrajectory = true;
        [UnityEngine.Serialization.FormerlySerializedAs("showChargeChevrons")]
        [Tooltip("Grow the aim dots from nearest to farthest as the Core charges. Turn off to keep a plain dotted aim line.")]
        public bool showDotCharge = true;
        [Min(.5f)] public float previewLength = 14f;
        [HideInInspector] public int previewDeflections = 2; // Retained for existing serialized settings.
        [Min(.08f)] public float dotSpacing = .45f;
        [Min(.01f)] public float dotRadius = .045f;
        [InspectorName("Final Dot Scale")]
        [Tooltip("Last aim dot's size relative to the first. Dots progressively shrink along the guide. 0 tapers to zero; 1 keeps every dot the same size.")]
        [Range(0f, 1f)] public float dotEndScale = .25f;
        [Tooltip("Each dot's fully charged size relative to its resting size. The distance taper is preserved.")]
        [Range(1f, 4f)] public float chargedDotScale = 2.5f;
        [Tooltip("Fraction of the charge over which each dot grows. Small values give a sharper outward progression; larger values blend neighboring dots together.")]
        [Range(.02f, 1f)] public float dotChargeSpread = .12f;
        [Header("Contest bar — only while two opposing players are inside")]
        [Min(.3f)] public float meterWidth = 2.4f;
        [Min(.05f)] public float meterHeight = .18f;
        [Min(0f)] public float meterOffset = .5f;
    }
}
