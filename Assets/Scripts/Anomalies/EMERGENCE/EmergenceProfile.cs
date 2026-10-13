using System;
using UnityEngine;

namespace Massive.Emergence
{
    [CreateAssetMenu(menuName = "MASSIVE/EMERGENCE/Particle Ecology")]
    public sealed class EmergenceProfile : ScriptableObject
    {
        public const int SpeciesCount = 5, NutrientSpecies = 3, InertSpecies = 4;
        [Serializable]
        public sealed class Species
        {
            public string name;
            [Min(0)] public int count;
            [ColorUsage(false)] public Color color;
            [Tooltip("World-space diameter. The repulsive core uses the same size.")]
            [Min(.005f)] public float diameter = .1f;
            [Tooltip("Stable random size variation as a fraction of diameter. Zero gives equal sizes.")]
            [Range(0, .75f)] public float sizeVariation = .12f;
            [Min(0)] public float forceMultiplier = 1;
            [Min(.01f)] public float dragMultiplier = 1;
            [Min(.01f)] public float speedMultiplier = 1;
            [Min(0)] public float agitationMultiplier = 1;
            public Species(string label, int population, Color tint) { name = label; count = population; color = tint; }
        }

        [Header("Species: structure, motor, catalyst, nutrient, inert")]
        public Species[] species = {
            new Species("Structure", 420, new Color(.25f, .9f, .68f)),
            new Species("Motor", 240, new Color(.85f, .3f, .78f)),
            new Species("Catalyst", 180, new Color(1, .8f, .25f)),
            new Species("Nutrient", 560, new Color(.25f, .6f, 1)),
            new Species("Inert", 0, new Color(.88f, .9f, .94f))
        };
        [Header("Dispersed starting conditions")]
        public int seed = 1729;
        [Tooltip("Fraction of the field initially occupied by the random mixture. No prebuilt organisms or seed clusters.")]
        [Range(.25f, 1)] public float initialCoverage = .95f;
        [Min(0)] public float initialSpeed = .15f;
        [Header("Local forces")]
        [Min(.005f)] public float interactionRadius = 1.6f;
        [Tooltip("General short-range repulsion distance, before the species attraction band. Size-based core repulsion is added independently.")]
        [Min(.001f)] public float separation = .26f;
        [Min(0)] public float force = 4;
        [Min(0)] public float repulsion = 24;
        [Min(.1f)] public float drag = 4.5f;
        [Min(.1f)] public float speedLimit = 2.8f;
        [Min(0)] public float agitation = .18f;
        [Min(.1f)] public float maximumForce = 45;
        [Tooltip("Rows respond to columns; diagonal entries control self-attraction. Positive attracts, negative repels. Ordered pairs may differ.")]
        public float[] attraction = {
             .18f, .5f, .65f, .03f, -.12f,
             -.25f, .12f, .75f, .08f, -.12f,
             .6f, -.35f, -.05f, .35f, -.12f,
             -.08f, .02f, .6f, -.035f, -.12f,
             0, 0, 0, 0, -.08f
        };
        [Header("Soft particle spacing")]
        [Tooltip("Preferred edge-to-edge gap. Used for initial placement and the repulsive core; motion can briefly compress this gap or overlap.")]
        [Min(0)] public float particleGap = .012f;
        [Tooltip("Core reach as a multiple of the two visible radii plus Particle Gap. Repulsion begins outside the preferred spacing to slow approaching particles.")]
        [Range(1, 4)] public float coreRangeMultiplier = 3;
        [Tooltip("Maximum extra repulsion at coincident centers. Rises quadratically inside the core, independently of the attraction matrix. Total acceleration still respects Maximum Force.")]
        [Min(0)] public float coreRepulsion = 80;
        [Tooltip("Damps closing motion inside the core. Separating particles are unaffected; this reduces compression and jitter without hard collisions.")]
        [Min(0)] public float coreDamping = 6;
        [Range(0, 1)] public float wallBounce = .35f;
        [Header("Simulation")]
        [Tooltip("GPU is the normal backend. CPU Reference is for comparison and supports up to 4,000 particles.")]
        public EmergenceBackend backend = EmergenceBackend.GPU;
        [Range(30, 120)] public int simulationHz = 60;
        [Range(1, 6)] public int maximumStepsPerFrame = 3;
        [Header("Recognition and metabolism")]
        [Min(.005f)] public float connectionRadius = .55f;
        [Tooltip("Caps each organism link at this multiple of the two visible radii. Connection Radius remains the absolute maximum.")]
        [Min(1)] public float connectionSizeMultiplier = 5;
        [Tooltip("Each body particle needs this many nearby body neighbours to join an organism. Rejects isolated members and sparse chains.")]
        [Range(0, 12)] public int minimumLocalNeighbours = 3;
        [Min(3)] public int minimumBody = 16;
        [Range(0, 1)] public float minimumStructureFraction = .25f;
        [Range(0, 1)] public float minimumMotorFraction = .08f;
        [Min(.02f)] public float recognitionInterval = .2f;
        [Min(.1f)] public float maturationSeconds = 1.5f;
        [Tooltip("Capture distance around a mature catalyst. Captured nutrients visibly travel to its surface before shrinking.")]
        [Min(.001f)] public float feedingRadius = .3f;
        [Min(.05f)] public float feedingApproachSeconds = .45f;
        [Tooltip("Duration of the eased scale-to-zero animation on consumption. Opacity stays at one.")]
        [Min(.05f)] public float particleShrinkSeconds = .25f;
        [Tooltip("Duration of the eased growth from zero on replenishment. Nutrients cannot be captured during growth.")]
        [Min(.05f)] public float particleGrowSeconds = .45f;
        [Min(.1f)] public float reservePerCatalyst = 6;
        [Min(0)] public float processingPerSecond = .18f;
        [Min(.1f)] public float materialPerNugget = 12;
        [Min(.1f)] public float secretionInterval = 3;
        [Min(.1f)] public float fieldSecretionInterval = .8f;
        [Min(1)] public float nutrientReturnSeconds = 22;
        [Range(0, 1)] public float unfedMotorResponse = .2f;
        [Min(.1f)] public float motorFuelPerCatalyst = 2;
        [Header("Organism swimming")]
        [Tooltip("Mature, viable organisms swim together even with no food reserves. Particle forces still shape and deform them.")]
        public bool enableSwimming = true;
        [Tooltip("Cruising speed as a fraction of Speed Limit. The final particle speed still obeys its normal limit.")]
        [Range(0, 1)] public float swimSpeedFraction = .65f;
        [Tooltip("Seconds for swimming to ease in or out. Also smooths speed changes after organisms split or merge.")]
        [Min(.05f)] public float swimResponseSeconds = .6f;
        [Tooltip("Maximum steering rate in degrees per second.")]
        [Min(0)] public float swimTurnSpeed = 90;
        [Tooltip("Wandering turn strength as a fraction of the steering rate. Independent of food and reserves.")]
        [Range(0, 1)] public float swimWanderStrength = .55f;
        [Tooltip("Seconds between smoothly changing random wandering turns.")]
        [Min(.1f)] public float swimWanderSeconds = 3.5f;
        [Tooltip("Fractional rhythmic variation in cruising speed. Zero gives steady swimming.")]
        [Range(0, .5f)] public float swimPulseAmount = .12f;
        [Min(.1f)] public float swimPulseSeconds = 2;
        [Tooltip("Food steering strength as a fraction of the steering rate. Zero disables food seeking without stopping swimming.")]
        [Range(0, 1)] public float swimFoodBias = .3f;
        [Tooltip("Food sensing distance from the organism center, as a multiple of Interaction Radius. Only free, fully grown nutrients count.")]
        [Min(.1f)] public float swimSenseRadiusMultiple = 2.5f;
        [Tooltip("Distance at which organisms begin turning from walls, as a multiple of Interaction Radius, plus estimated body radius.")]
        [Min(.1f)] public float swimWallRangeMultiple = 1.5f;
        [Header("Player disturbance")]
        [Min(0)] public float wakeStrength = 12;
        [Min(0)] public float attackImpulse = 3.5f;
        [Min(0)] public float repulsorImpulse = 7;
        [Min(0)] public float disruptedSeconds = 2.5f;
        [Header("Optional flat color feedback")]
        public bool showWasteColor;
        [ColorUsage(false)] public Color wasteColor = Color.white;
        [Min(.01f)] public float wasteColorAt = 1.4f;
        public bool showDisruptionColor;
        [ColorUsage(false)] public Color disruptionColor = new Color(1, .3f, .2f);
        [Header("Pickups")]
        [Range(4, 64)] public int nuggetCapacity = 24;
        [Min(1)] public float nuggetLifetime = 18;
        [Tooltip("Outward speed added when a completed bud detaches (also used for immediate secretion).")]
        [Min(0)] public float nuggetEjectionSpeed = .8f;
        [Tooltip("Relative chances after a conversion succeeds. Normalized automatically; zero excludes a type. Does not change conversion speed or material cost.")]
        public EmergenceDropWeights pickupChances = new EmergenceDropWeights();
        [Header("Reward budding telegraph")]
        public bool enableRewardTelegraph = true;
        [Tooltip("Time from the first local pulse to release. Material stays in the organism until release.")]
        [Min(.05f)] public float budWindupSeconds = .8f;
        [Tooltip("Maximum local visual size change. Does not modify particle forces, positions or collision radii.")]
        [Range(0, .5f)] public float budPulseStrength = .22f;
        [Min(.01f)] public float budPulseRadius = .5f;
        [Tooltip("Fraction of the windup at which the reward starts growing.")]
        [Range(0, .95f)] public float budGrowthStart = .2f;
        [Tooltip("Fraction of the windup at which the reward reaches full size.")]
        [Range(.05f, 1)] public float budGrowthEnd = .9f;
        [Tooltip("Distance outside the emitting catalyst's surface for a full-size nugget. Scales with reward size so nugglets stay attached.")]
        [Min(0)] public float budAttachmentDistance = .4f;
        [Tooltip("Time for an interrupted bud to shrink back. Its stored material remains available.")]
        [Min(.05f)] public float budRetractSeconds = .25f;
        public int BodyParticleCount => species[0].count + species[1].count + species[2].count;
        public int ParticleCount { get { int n = 0; foreach (var s in species) n += s.count; return n; } }
        public float MaximumRadius { get { float r = 0; foreach (var s in species) r = Mathf.Max(r, s.diameter * (1 + s.sizeVariation) * .5f); return r; } }

        public string ConfigurationError()
        {
            if (pickupChances == null || !pickupChances.IsValid) return "Pickup chances must be finite and nonnegative, with at least one greater than zero.";
            foreach (float value in new[] { budWindupSeconds, budPulseStrength, budPulseRadius, budGrowthStart, budGrowthEnd, budAttachmentDistance, budRetractSeconds })
                if (!Finite(value) || value < 0) return "Budding settings must be finite and nonnegative.";
            if (budWindupSeconds < .05f || budPulseStrength > .5f || budPulseRadius < .01f || budRetractSeconds < .05f ||
                budGrowthStart >= budGrowthEnd || budGrowthEnd > 1) return "Check bud timing: windup/retract >= 0.05s, growth start < end <= 1, and pulse strength <= 0.5.";
            if (species == null || species.Length != SpeciesCount) return "Five species are required: structure, motor, catalyst, nutrient, inert.";
            foreach (var s in species)
                if (s == null || s.count < 0 || !Finite(s.diameter) || s.diameter < .005f || !Finite(s.sizeVariation) || s.sizeVariation < 0 || s.sizeVariation > .75f ||
                    !Finite(s.forceMultiplier) || s.forceMultiplier < 0 || !Finite(s.dragMultiplier) || s.dragMultiplier <= 0 ||
                    !Finite(s.speedMultiplier) || s.speedMultiplier <= 0 || !Finite(s.agitationMultiplier) || s.agitationMultiplier < 0)
                    return "Species need valid counts, finite positive sizes and motion settings.";
            int limit = backend == EmergenceBackend.GPU ? 100000 : 4000;
            if (ParticleCount < 1 || ParticleCount > limit) return "Use between 1 and " + limit.ToString("N0") + " particles for this backend.";
            if (attraction == null || attraction.Length != SpeciesCount * SpeciesCount) return "The attraction matrix must have twenty-five entries.";
            foreach (float value in attraction) if (!Finite(value)) return "Attraction values must be finite.";
            foreach (float value in new[] { interactionRadius, separation, force, repulsion, drag, speedLimit, agitation, maximumForce, particleGap, coreRangeMultiplier, coreRepulsion, coreDamping, connectionRadius, feedingRadius, reservePerCatalyst, materialPerNugget, nutrientReturnSeconds, maturationSeconds, processingPerSecond, secretionInterval, fieldSecretionInterval, recognitionInterval, motorFuelPerCatalyst, initialSpeed, initialCoverage, wasteColorAt })
                if (!Finite(value) || value < 0) return "Distances, forces, capacities and times must be finite and nonnegative.";
            if (separation <= 0 || interactionRadius <= separation || drag <= 0 || speedLimit <= 0 || maximumForce <= 0 || connectionRadius <= 0 ||
                feedingRadius <= 0 || reservePerCatalyst <= 0 || materialPerNugget <= 0 || nutrientReturnSeconds <= 0 || maturationSeconds <= 0 ||
                recognitionInterval <= 0 || motorFuelPerCatalyst <= 0 || initialCoverage < .25f || initialCoverage > 1 || wasteColorAt <= 0 || simulationHz < 30 || simulationHz > 120)
                return "Check positive simulation distances, capacities, times and update rate; interaction radius must exceed separation.";
            if (coreRangeMultiplier < 1 || coreRangeMultiplier > 4)
                return "Core range multiplier must be between 1 and 4.";
            foreach (float value in new[] { swimSpeedFraction, swimResponseSeconds, swimTurnSpeed, swimWanderStrength, swimWanderSeconds,
                swimPulseAmount, swimPulseSeconds, swimFoodBias, swimSenseRadiusMultiple, swimWallRangeMultiple })
                if (!Finite(value) || value < 0) return "Swimming settings must be finite and nonnegative.";
            if (swimSpeedFraction > 1 || swimResponseSeconds < .05f || swimWanderStrength > 1 || swimWanderSeconds < .1f ||
                swimPulseAmount > .5f || swimPulseSeconds < .1f || swimFoodBias > 1 || swimSenseRadiusMultiple < .1f || swimWallRangeMultiple < .1f)
                return "Check swimming fractions (0–1), pulse amount (0–0.5), response (at least 0.05s), periods and range multiples (at least 0.1).";
            if (!Finite(connectionSizeMultiplier) || connectionSizeMultiplier < 1 || minimumLocalNeighbours < 0 || minimumLocalNeighbours > 12 ||
                !Finite(feedingApproachSeconds) || feedingApproachSeconds < .05f || !Finite(particleShrinkSeconds) || particleShrinkSeconds < .05f ||
                !Finite(particleGrowSeconds) || particleGrowSeconds < .05f)
                return "Check organism link size, local neighbours (0–12) and particle transition times (at least 0.05 seconds).";
            return null;
        }
        static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        void OnEnable() => UpgradeLegacySpecies();
        void OnValidate() => UpgradeLegacySpecies();

        // Extend old assets without changing any authored species or reinterpreting the 4x4 matrix.
        // Population stays zero until explicitly authored; loading an old profile cannot add particles.
        public bool UpgradeLegacySpecies()
        {
            bool changed = false;
            if (species != null && species.Length == 4)
            {
                var old = species; species = new Species[SpeciesCount]; Array.Copy(old, species, 4);
                var nutrient = old[NutrientSpecies];
                species[InertSpecies] = new Species("Inert", 0, new Color(.88f, .9f, .94f))
                { diameter = nutrient != null ? nutrient.diameter : .1f, sizeVariation = nutrient != null ? nutrient.sizeVariation : .12f };
                changed = true;
            }
            if (attraction != null && attraction.Length == 16)
            {
                var old = attraction; attraction = new float[SpeciesCount * SpeciesCount];
                for (int row = 0; row < 4; row++)
                {
                    for (int column = 0; column < 4; column++) attraction[row * SpeciesCount + column] = old[row * 4 + column];
                    attraction[row * SpeciesCount + InertSpecies] = -.12f;
                }
                attraction[InertSpecies * SpeciesCount + InertSpecies] = -.08f;
                changed = true;
            }
            return changed;
        }
    }
    public enum EmergenceBackend { GPU, CPUReference }
}
