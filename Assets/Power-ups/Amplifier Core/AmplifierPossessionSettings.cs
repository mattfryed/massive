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
        [Tooltip("Holding Attack automatically fires at this charge time. Release earlier for a shorter shot.")]
        [Min(.1f)] public float fullChargeSeconds = 2f;
        [Tooltip("Calculate shot drag from the charge distances and stopping settings. Coasting Drag does not limit these shots. Off keeps the original drag-driven tuning.")]
        public bool distanceDrivenShots;
        [Tooltip("Unobstructed travel at zero charge in distance-driven mode. Charge changes drag, not damage or initial speed. Collisions, fields and edits during flight can change the actual distance.")]
        [Min(.1f)] public float minimumRange = 5f;
        [Tooltip("Unobstructed travel at full charge in distance-driven mode. Intermediate charge interpolates between the two distances.")]
        [Min(.1f)] public float maximumRange = 26f;
        [Min(.1f)] public float shotSpeed = 16f;
        [Tooltip("After this time the shot smoothly brakes to rest. It never stops at a distance cutoff.")]
        [Min(1f)] public float maximumFlightSeconds = 15f;
        [Tooltip("Speed in world units per second below which the shot can settle. Raise this to remove the slow drifting tail sooner. Entry is allowed after Settling Seconds and Reentry Delay.")]
        [Min(.01f)] public float settledSpeed = .15f;
        [Tooltip("How long the shot must stay below Settled Speed before it rests. Zero settles on the first eligible physics step. Resonance-guided turns cannot settle midway.")]
        [Min(0f)] public float settlingSeconds = .3f;
        [Tooltip("Minimum drag once Maximum Flight Seconds has elapsed. Higher values brake faster; the Core still slows through physics before settling.")]
        [Min(0f)] public float finalBrakeDrag = 5f;
        [Tooltip("Rolling rotation from travel and collision direction. Zero leaves only the Core's idle shell motion.")]
        [Range(0f, 1f)] public float rollingSpin = .45f;
        [Header("Charge vibration — visual only")]
        public bool chargeVibration = true;
        [Tooltip("Maximum visual displacement as a fraction of the Core's radius. Does not move the collider or alter aim.")]
        [Range(0f, .2f)] public float chargeVibrationAmount = .055f;
        [Min(0f)] public float chargeVibrationFrequency = 24f;
        [Range(0f, .95f)] public float chargeVibrationStart = .1f;
        [Tooltip("Higher values concentrate the vibration near full charge.")]
        [Range(.25f, 4f)] public float chargeVibrationRamp = 1.7f;
        [Header("Ejection ripple — Core surface")]
        public bool ejectionRipple = true;
        [Min(.05f)] public float ejectionRippleSeconds = .85f;
        [Range(0f, .2f)] public float ejectionRippleAmplitude = .08f;
        [Tooltip("Width of the travelling ripple packet around the sphere, in radians.")]
        [Range(.1f, 1.5f)] public float ejectionRippleWidth = .55f;
        [Range(1f, 12f)] public float ejectionRippleFrequency = 5f;
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
        [Tooltip("Extra time after the loser's exit animation during which Attack is discarded. Aiming stays available; the winner's aiming countdown starts after this fixed protection.")]
        [Min(0f)] public float postContestGraceSeconds = .2f;
        [Tooltip("Both Attack and Shield must stay released this long before a fresh Attack press can charge. Release can overlap the fixed protection; presses during protection are never queued.")]
        [Min(0f)] public float postContestReleaseSeconds = .1f;
        public float PostContestLockSeconds => (animatePlayerTransit ? Mathf.Max(.05f, exitSeconds) : 0f) + Mathf.Max(0f, postContestGraceSeconds);
        [Tooltip("Additional wait before Thrust can enter an available Core, after a shot settles or players are ejected.")]
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
        [Header("Contest ring — Light clockwise / Dark counterclockwise")]
        public bool showContestRing = true;
        [Tooltip("Gap outside the Core, as a fraction of its radius.")]
        [Min(0f)] public float contestRingGap = .2f;
        [Tooltip("Ring width as a fraction of the Core's radius.")]
        [Range(.02f, .5f)] public float contestRingWidth = .14f;
        [Tooltip("White border around the black portion, as a fraction of the Core's radius.")]
        [Range(.005f, .08f)] public float contestRingOutline = .02f;
        public bool showRingHalfwayMark = true;
        [Header("Control prompts — gameplay and edit-mode preview")]
        public bool showControlText = true;
        public TMPro.TMP_FontAsset controlTextFont;
        [Tooltip("TextMeshPro point size at a Core radius of one world unit. Text scales with the Core.")]
        [Min(.1f)] public float controlTextSize = 2.4f;
        public Color controlTextColor = Color.white;
        [Tooltip("X moves right; Y sets the gap below the Core or contest ring. Both are fractions of the Core radius.")]
        public Vector2 controlTextOffset = new Vector2(0f, .55f);
        [Range(-50f, 100f)] public float controlTextLineSpacing;
        public bool showControlTimer = true;
        [Tooltip("Available tokens: {team}, {time}. Time includes the seconds suffix and can be hidden separately.")]
        [TextArea(2, 3)] public string aimingText = "{team}  ·  HOLD ATTACK\nRELEASE TO FIRE / FULL = AUTO  {time}";
        [TextArea(2, 3)] public string enteringText = "{team}\nENTERING CORE";
        [TextArea(2, 3)] public string contestedText = "ALTERNATE ATTACK / SHIELD\n{time}";
        [TextArea(2, 3)] public string winnerText = "{team} WINS  ·  HOLD ATTACK\nRELEASE TO FIRE / FULL = AUTO  {time}";
        public string lightTeamText = "LIGHT";
        public string darkTeamText = "DARK";
        // Keep old serialized values so existing profiles and prefab overrides migrate safely.
        [HideInInspector] public float meterWidth = 2.4f;
        [HideInInspector] public float meterHeight = .18f;
        [HideInInspector] public float meterOffset = .5f;
    }
}
