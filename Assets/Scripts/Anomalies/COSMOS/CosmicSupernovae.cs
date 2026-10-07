using System;
using Massive.Scoring;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Massive.Cosmos
{
    // Independent layers share this scheduler, warning lifecycle, rendering and pickup pooling.
    [RequireComponent(typeof(CosmicWebBackground)), DefaultExecutionOrder(650)]
    public sealed class CosmicSupernovae : MonoBehaviour
    {
        public enum SupernovaKind { Normal, Superluminous }
        public SupernovaKind kind;
        public CosmicWebBackground web;
        public MatterNuggetScript nuggletPrefab;
        public Shader flashShader;

        [Header("Supernova frequency over Age")]
        [Tooltip("X = simulation Age (0–1); Y = frequency multiplier. At Y = 1, the peak rate below applies. Negative values produce no explosions.")]
        public AnimationCurve frequencyOverAge = BellCurve();
        [Tooltip("Explosions per regulation second when the curve is at 1. Timing is random, not a regular pulse.")]
        [Min(0)] public float peakExplosionsPerSecond = 1;
        [Tooltip("0 = sample the whole web evenly; 1 = strongly favor its current clusters. The early soup remains dispersed.")]
        [Range(0, 1)] public float clusterBias = .92f;
        [Tooltip("World-space clearance from playable walls and goal separators. Events outside this area are rejected.")]
        [Min(0)] public float wallClearance = .35f;

        [Header("Released mass pickup")]
        [Tooltip("Random planar launch speed in world units per second.")]
        public Vector2 ejectionSpeed = new Vector2(2, 5);
        [Min(.5f)] public float nuggletLifetime = 20;
        [Tooltip("Pool capacity allocated when the level starts. If every slot is occupied, new events are skipped rather than replacing a pickup.")]
        [Range(1, 128)] public int nuggletCapacity = 32;

        [Header("Localized flash")]
        [ColorUsage(false, true)] public Color flashColor = new Color(1, .67f, .25f);
        [Tooltip("Choose one palette color per event, retained through buildup, flash and ring.")]
        public bool randomizeColor = true;
        [ColorUsage(false, true)] public Color cyanWhiteColor = new Color(.88f, .98f, 1);
        [ColorUsage(false, true)] public Color cyanBlueColor = new Color(.15f, .64f, 1);
        [ColorUsage(false, true)] public Color lightPurpleColor = new Color(.76f, .5f, 1);
        [Min(0)] public float flashBrightness = 4;
        [Min(.05f)] public float flashRadius = .75f;
        [Min(.05f)] public float flashSeconds = .4f;
        [Tooltip("Outer ejecta color for the superluminous shell. Normal explosions use their selected flash color.")]
        [ColorUsage(false, true)] public Color shellColor = new Color(1, .42f, .08f);
        [Tooltip("Irregular structure in the superluminous expanding shell.")]
        [Range(0, 1)] public float shellDetail = .7f;

        [Header("Superluminous cloud")]
        [Tooltip("Seconds for the ejecta cloud to expand to its full radius. Independent of the brief center flash.")]
        [Min(.05f)] public float cloudExpansionSeconds = 1.8f;
        [Tooltip("Seconds for the expanded cloud to dissolve after expansion finishes.")]
        [Min(.05f)] public float cloudFadeSeconds = 2.4f;
        [Tooltip("Large-scale billowing and asymmetric mixing in the expanding cloud.")]
        [Range(0, 1)] public float cloudDistortion = .85f;
        [Tooltip("Speed of internal cloud and telegraph motion. Zero freezes internal flow; expansion still runs.")]
        [Range(0, 2)] public float cloudMixingSpeed = .45f;
        [Tooltip("Uneven, moving gas around the source during buildup. Zero restores a round envelope.")]
        [Range(0, 1)] public float telegraphDistortion = .85f;

        [Header("Build up and die off glow")]
        [Tooltip("Seconds of subtle warning glow before the explosion. Zero restores an immediate burst. The mass pickup launches at the peak.")]
        [Min(0)] public float buildUpSeconds = 1.8f;
        [Tooltip("Maximum buildup glow relative to Flash Brightness, before flicker. The explosion still reaches full brightness.")]
        [Range(0, 1)] public float buildUpBrightness = .12f;
        [Tooltip("Strength of irregular, smoothly varying flicker during buildup. Zero gives a steady rise.")]
        [Range(0, 1)] public float flickerStrength = .35f;
        [Min(0)] public float flickerSpeed = 7;
        [Tooltip("Seconds for the glow to fade smoothly after the peak. The existing flash/ring keeps its own Flash Seconds duration.")]
        [Min(.01f)] public float dieOffSeconds = .3f;

        [Header("Randomness")]
        public bool fixedRandomSeed;
        public int randomSeed = 90211;

        struct Flash
        {
            public CosmicWebTopology.Particle source;
            public Vector3 position, pickupPoint;
            public Color color;
            public float elapsed, buildUp, duration, dieOff, radius, flickerSeed, cancelGlow, cloudExpansion, cloudFade;
            public int generation, nuggletSlot;
            public bool active, exploded, cancelled;
        }

        MatterNuggetScript[] nugglets;
        bool[] reservedNugglets;
        Flash[] flashes;
        GameObject poolRoot;
        Material flashMaterial;
        Mesh flashMesh;
        MaterialPropertyBlock flashProperties;
        System.Random random;
        float previousRemaining;
        double hazardRemaining;
        bool matchFinished;
        static readonly int PhaseID = Shader.PropertyToID("_Phase"), ColorID = Shader.PropertyToID("_Color"),
            BrightnessID = Shader.PropertyToID("_Brightness"), GlowID = Shader.PropertyToID("_Glow"),
            SuperluminousID = Shader.PropertyToID("_Superluminous"), ShellColorID = Shader.PropertyToID("_ShellColor"),
            ShellDetailID = Shader.PropertyToID("_ShellDetail"), BurstSeedID = Shader.PropertyToID("_BurstSeed"),
            CloudExpansionID = Shader.PropertyToID("_CloudExpansion"), CloudOpacityID = Shader.PropertyToID("_CloudOpacity"),
            CloudDistortionID = Shader.PropertyToID("_CloudDistortion"), FlowTimeID = Shader.PropertyToID("_FlowTime"),
            TelegraphDistortionID = Shader.PropertyToID("_TelegraphDistortion");
        public int ExplosionsReleased { get; private set; }
        public float CurrentFrequency => web ? FrequencyAtAge(web.Age) : 0;

        static AnimationCurve BellCurve()
        {
            const float sigma = .18f;
            float tail = Mathf.Exp(-.5f * .5f / (2 * sigma * sigma));
            var keys = new Keyframe[9];
            for (int i = 0; i < keys.Length; i++)
            {
                float age = i / 8f, x = age - .5f;
                float gaussian = Mathf.Exp(-x * x / (2 * sigma * sigma));
                float value = (gaussian - tail) / (1 - tail);
                float slope = i == 0 || i == 8 ? 0 : -x * gaussian / (sigma * sigma * (1 - tail));
                keys[i] = new Keyframe(age, value, slope, slope);
            }
            return new AnimationCurve(keys) { preWrapMode = WrapMode.ClampForever, postWrapMode = WrapMode.ClampForever };
        }

        void Reset() { web = GetComponent<CosmicWebBackground>(); }
        void Awake()
        {
            if (!web) web = GetComponent<CosmicWebBackground>();
            if (!web || !web.match || !nuggletPrefab || !flashShader || !flashShader.isSupported)
            {
                Debug.LogError("[COSMOS] Supernovae need the web, its match clock, a mass pickup prefab and a supported flash shader.", this);
                enabled = false; return;
            }
            nugglets = new MatterNuggetScript[Mathf.Clamp(nuggletCapacity, 1, 128)];
            reservedNugglets = new bool[nugglets.Length];
            flashes = new Flash[32];
            // A world-space root preserves the pickup prefab's scale and horizontal physics plane.
            poolRoot = new GameObject(kind == SupernovaKind.Superluminous ? "COSMOS Superluminous Nuggets" : "COSMOS Supernova Nugglets");
            SceneManager.MoveGameObjectToScene(poolRoot, gameObject.scene);
            flashMaterial = new Material(flashShader) { hideFlags = HideFlags.HideAndDontSave };
            flashProperties = new MaterialPropertyBlock();
            flashMesh = new Mesh { name = "COSMOS supernova flash", hideFlags = HideFlags.HideAndDontSave };
            flashMesh.vertices = new[] { new Vector3(-1, -1), new Vector3(-1, 1), new Vector3(1, 1), new Vector3(1, -1) };
            flashMesh.uv = new[] { new Vector2(-1, -1), new Vector2(-1, 1), new Vector2(1, 1), new Vector2(1, -1) };
            flashMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            flashMesh.bounds = new Bounds(Vector3.zero, new Vector3(2, 2, .1f));
            flashMesh.UploadMeshData(true);
        }
        void OnEnable()
        {
            random = new System.Random(fixedRandomSeed ? randomSeed : Guid.NewGuid().GetHashCode());
            ResetTimeline();
        }
        void ResetTimeline()
        {
            previousRemaining = web && web.match ? web.match.RegulationRemainingSeconds : 0;
            hazardRemaining = NextInterval();
            matchFinished = false; ExplosionsReleased = 0;
            ClearEffects();
        }
        double NextInterval() => -Math.Log(Math.Max(.000001, 1 - random.NextDouble()));
        public float FrequencyAtAge(float age)
        {
            float rate = frequencyOverAge == null ? 0 : frequencyOverAge.Evaluate(Mathf.Clamp01(age)) * Mathf.Max(0, peakExplosionsPerSecond);
            return float.IsNaN(rate) || float.IsInfinity(rate) ? 0 : Mathf.Max(0, rate);
        }
        Color NextColor()
        {
            if (!randomizeColor) return flashColor;
            switch (random.Next(4))
            {
                case 1: return cyanWhiteColor;
                case 2: return cyanBlueColor;
                case 3: return lightPurpleColor;
                default: return flashColor;
            }
        }
        void LateUpdate()
        {
            if (nugglets == null || !web || !web.match) return;
            var match = web.match;
            float remaining = match.RegulationRemainingSeconds;
            if (remaining > previousRemaining + .001f) ResetTimeline();
            float elapsed = Mathf.Max(0, previousRemaining - remaining);
            float priorAge = Mathf.Clamp01(1 - previousRemaining / match.RegulationDurationSeconds);
            previousRemaining = remaining;
            bool finished = match.Phase == MatchRuntimePhase.FinaleEntry || match.Phase == MatchRuntimePhase.Resolving || match.Phase == MatchRuntimePhase.Complete;
            if (finished)
            {
                if (!matchFinished) ClearEffects();
                matchFinished = true; return;
            }
            bool running = match.Phase == MatchRuntimePhase.Regulation || match.Phase == MatchRuntimePhase.Bonus;
            float timelineDelta = running && !match.IsStartupBlocked && web.isActiveAndEnabled && Time.timeScale > 0 ? elapsed : 0;
            if (timelineDelta > 0)
            {
                // Integrate the age-dependent rate against the actual match clock. A paused
                // regulation clock creates neither events nor a catch-up debt on resume.
                // Schedule the warning ahead of the curve so its eventual explosion still
                // follows the authored Age distribution rather than shifting it later.
                float lead = Mathf.Max(0, buildUpSeconds) / match.RegulationDurationSeconds;
                double exposure = .5 * (FrequencyAtAge(priorAge + lead) + FrequencyAtAge(web.Age + lead)) * timelineDelta;
                hazardRemaining -= exposure;
                int attempts = 0;
                while (hazardRemaining <= 0 && attempts++ < 4)
                {
                    TryBeginSupernova(); hazardRemaining += NextInterval();
                }
                // Bound catch-up work after a hitch or an extreme Inspector rate.
                if (hazardRemaining <= 0) hazardRemaining = NextInterval();
            }
            DrawFlashes(timelineDelta, Time.deltaTime);
        }
        void TryBeginSupernova()
        {
            float buildup = Mathf.Max(0, buildUpSeconds);
            if (web.match.RegulationRemainingSeconds <= buildup) return;
            int flashSlot = -1;
            for (int i = 0; i < flashes.Length; i++)
                if (!flashes[i].active) { flashSlot = i; break; }
            if (flashSlot < 0) return;
            int slot = -1;
            for (int i = 0; i < nugglets.Length; i++)
                if (!reservedNugglets[i] && (!nugglets[i] || !nugglets[i].gameObject.activeSelf)) { slot = i; break; }
            if (slot < 0 || !web.TrySampleMatterPoint(random, clusterBias, wallClearance, out var source, out var origin, out var pickupPoint)) return;
            // Reserve capacity now, but do not create/activate the pickup until the burst.
            reservedNugglets[slot] = true;
            flashes[flashSlot] = new Flash { source = source, generation = web.Generation, position = origin, pickupPoint = pickupPoint,
                nuggletSlot = slot, buildUp = buildup, duration = Mathf.Max(.05f, flashSeconds), dieOff = Mathf.Max(.01f, dieOffSeconds),
                cloudExpansion = kind == SupernovaKind.Superluminous ? Mathf.Max(.05f, cloudExpansionSeconds) : 0,
                cloudFade = kind == SupernovaKind.Superluminous ? Mathf.Max(.05f, cloudFadeSeconds) : 0,
                radius = Mathf.Max(.05f, flashRadius) * Mathf.Lerp(.8f, 1.2f, (float)random.NextDouble()),
                flickerSeed = (float)random.NextDouble() * 1000, color = NextColor(), active = true };
        }
        void Explode(ref Flash flash)
        {
            int slot = flash.nuggletSlot;
            if (!nugglets[slot]) nugglets[slot] = Instantiate(nuggletPrefab, poolRoot.transform);
            float angle = (float)random.NextDouble() * Mathf.PI * 2;
            float low = Mathf.Max(0, Mathf.Min(ejectionSpeed.x, ejectionSpeed.y));
            float high = Mathf.Max(low, Mathf.Max(ejectionSpeed.x, ejectionSpeed.y));
            float speed = Mathf.Lerp(low, high, (float)random.NextDouble());
            // All pickups use the game's XZ physics plane, directly above the source galaxy.
            Vector3 pickupPoint = flash.pickupPoint; pickupPoint.y = 0;
            nugglets[slot].Eject(pickupPoint, new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * speed, Mathf.Max(.5f, nuggletLifetime));
            reservedNugglets[slot] = false; flash.nuggletSlot = -1;
            flash.exploded = true; flash.elapsed = flash.buildUp;
            ExplosionsReleased++;
        }
        float BuildUpGlow(Flash flash)
        {
            float rise = Mathf.SmoothStep(0, 1, Mathf.Clamp01(flash.elapsed / Mathf.Max(.0001f, flash.buildUp)));
            float clock = flash.elapsed * Mathf.Max(0, flickerSpeed);
            float noise = .7f * Mathf.PerlinNoise(flash.flickerSeed, clock) +
                .3f * Mathf.PerlinNoise(flash.flickerSeed + 37.1f, clock * 2.13f);
            float strength = Mathf.Clamp01(flickerStrength);
            return Mathf.Clamp01(buildUpBrightness) * rise * rise * Mathf.Lerp(1 - strength, 1 + strength, noise);
        }
        void DrawFlashes(float timelineDelta, float visualDelta)
        {
            for (int i = 0; i < flashes.Length; i++)
            {
                if (!flashes[i].active) continue;
                var flash = flashes[i];
                if (!flash.exploded && !flash.cancelled)
                {
                    // A warning follows its galaxy through drift, turbulence and infall.
                    if (flash.generation != web.Generation ||
                        !web.TryGetMatterPoint(flash.source, wallClearance, out var origin, out var pickupPoint))
                    {
                        // If its galaxy leaves the playable area, smoothly retire the warning.
                        flash.cancelGlow = BuildUpGlow(flash); flash.cancelled = true;
                        reservedNugglets[flash.nuggletSlot] = false; flash.nuggletSlot = -1;
                        flash.elapsed = flash.buildUp;
                    }
                    else
                    {
                        flash.position = origin; flash.pickupPoint = pickupPoint;
                        flash.elapsed += timelineDelta;
                        if (flash.elapsed >= flash.buildUp && timelineDelta > 0) Explode(ref flash);
                    }
                }
                else flash.elapsed += visualDelta;
                bool pending = !flash.exploded && !flash.cancelled;
                float afterPeak = Mathf.Max(0, flash.elapsed - flash.buildUp);
                float lifetime = Mathf.Max(Mathf.Max(flash.duration, flash.dieOff), flash.cloudExpansion + flash.cloudFade);
                if (!pending && afterPeak >= (flash.cancelled ? flash.dieOff : lifetime))
                { flashes[i].active = false; continue; }
                flashes[i] = flash;
                float glow = pending ? BuildUpGlow(flash) :
                    (flash.cancelled ? flash.cancelGlow : 1) * (1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(afterPeak / flash.dieOff)));
                flashProperties.SetFloat(GlowID, glow);
                flashProperties.SetFloat(PhaseID, pending ? -1 : flash.cancelled ? 1 : Mathf.Clamp01(afterPeak / flash.duration));
                flashProperties.SetColor(ColorID, flash.color);
                flashProperties.SetFloat(SuperluminousID, kind == SupernovaKind.Superluminous ? 1 : 0);
                flashProperties.SetColor(ShellColorID, shellColor);
                flashProperties.SetFloat(ShellDetailID, shellDetail);
                flashProperties.SetFloat(BurstSeedID, flash.flickerSeed);
                flashProperties.SetFloat(CloudExpansionID, Mathf.Clamp01(afterPeak / Mathf.Max(.05f, flash.cloudExpansion)));
                flashProperties.SetFloat(CloudOpacityID, flash.exploded ?
                    1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((afterPeak - flash.cloudExpansion) / Mathf.Max(.05f, flash.cloudFade))) : 0);
                flashProperties.SetFloat(CloudDistortionID, cloudDistortion);
                flashProperties.SetFloat(TelegraphDistortionID, telegraphDistortion);
                flashProperties.SetFloat(FlowTimeID, flash.elapsed * Mathf.Max(0, cloudMixingSpeed));
                flashProperties.SetFloat(BrightnessID, Mathf.Max(0, flashBrightness));
                var matrix = Matrix4x4.TRS(flash.position, web.transform.rotation, Vector3.one * flash.radius);
                Graphics.DrawMesh(flashMesh, matrix, flashMaterial, gameObject.layer, null, 0,
                    flashProperties, ShadowCastingMode.Off, false, null, LightProbeUsage.Off);
            }
        }
        void ClearEffects()
        {
            if (nugglets != null) foreach (var pickup in nugglets) if (pickup) pickup.gameObject.SetActive(false);
            if (flashes != null) Array.Clear(flashes, 0, flashes.Length);
            if (reservedNugglets != null) Array.Clear(reservedNugglets, 0, reservedNugglets.Length);
        }
        void OnDisable() { ClearEffects(); }
        void OnDestroy()
        {
            if (poolRoot) Destroy(poolRoot);
            if (flashMaterial) Destroy(flashMaterial);
            if (flashMesh) Destroy(flashMesh);
        }
    }
}
