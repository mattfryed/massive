using Massive.Scoring;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace Massive.Cosmos
{
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(600)]
    [RequireComponent(typeof(ArenaBoundsFromVectorGrid))]
    public sealed class CosmicWebBackground : MonoBehaviour
    {
        public GameManagerScript match;
        public Shader particleShader;
        [Header("Oval fit")]
        [Min(0)] public float padding = .25f;
        [Tooltip("Distance behind the grid; kept above the existing ground plane.")]
        [Range(.5f, 2.5f)] public float backgroundDepth = 1.7f;
        [Header("Structure")]
        public int seed = 90210;
        [Range(8000, 100000)] public int particleCount = 56000;
        [Tooltip("Multiscale filament curvature and the spread of fine branches. Rebuilds the structure.")]
        [Range(0, 2)] public float organicStrength = 1;
        [Tooltip("Regional differences in collapse timing and curved infall. Zero synchronizes the regions.")]
        [Range(0, 2)] public float evolutionVariation = 1;
        [Tooltip("Gradual compression toward the full oval edge, continuing behind both goals.")]
        [Range(0, 1)] public float edgeCondensation = .6f;
        [Tooltip("How the lensing blends into the middle. 0 = tight compression near the rim, 0.5 = original profile, 1 = broader, smoother transition inward. Changes particle placement, not brightness.")]
        [Range(0, 1)] public float lensingTransitionSmoothness = .5f;
        [Tooltip("Darkening distance inward from the padded oval edge, in world units. Larger values give a wider vignette; zero gives a crisp edge. Does not change the lensing or particle positions.")]
        [FormerlySerializedAs("edgeCondensationFeathering"), Range(0, 3)] public float edgeVignette = .05f;
        [Tooltip("Continuous shared flow carries filaments and merging clusters. Zero disables this drift only.")]
        [Range(0, 2)] public float driftStrength = 1;
        [Header("Cluster turbulence")]
        [Tooltip("Irregular local streams, eddies and attraction around moving clusters. Disable to bypass the turbulence calculations and compare the original flow.")]
        public bool clusterTurbulence = true;
        [Tooltip("Strength of uneven local flow and gentle attraction. The effect fades away from clusters and intensifies during mergers; zero disables it.")]
        [Range(0, 2)] public float turbulenceStrength = .7f;
        [Header("Palette and light")]
        [ColorUsage(false, true)] public Color filamentColor = new Color(.035f, .32f, .85f);
        [ColorUsage(false, true)] public Color clusterColor = new Color(1f, .24f, .61f);
        [Range(.1f, 3)] public float brightness = 1;
        [Tooltip("Variation in base brightness between tracers. 0 = uniform, 1 = original contrast, 2 = stronger contrast. Haze and age-related dimming still apply.")]
        [Range(0, 2)] public float brightnessVariance = 1;
        [Range(.008f, .065f)] public float galaxySize = .025f;
        [Header("Early universe heat")]
        [Tooltip("Mottled heat palette for the initial soup. Fades out at Early Heat Transition Point without changing particle distribution or motion. Zero restores the original early palette.")]
        [Range(0, 1)] public float earlyHeat = 1;
        [Tooltip("Early-only brightness multiplier, blended out with Early Heat. Overall Brightness still applies.")]
        [Range(1, 3)] public float earlyHeatBrightness = 1.55f;
        [Tooltip("Age at which Early Heat finishes fading into the normal web palette. 0 = immediate, 0.45 = original timing, 1 = end of the timeline.")]
        [Range(0, 1)] public float earlyHeatTransitionPoint = .45f;
        [Tooltip("Fade duration in Age units, ending at Transition Point. The start is clamped to Age 0. Zero gives an immediate switch.")]
        [Range(0, 1)] public float earlyHeatTransitionDuration = .43f;
        [Header("Early Heat colors and distribution")]
        [Tooltip("Base color for the coolest patches, before cyan, gold and red are blended over it.")]
        [ColorUsage(false, true)] public Color earlyHeatBlueColor = new Color(.015f, .12f, .65f);
        [ColorUsage(false, true)] public Color earlyHeatCyanColor = new Color(.025f, .66f, 1);
        [Tooltip("How far cyan extends into the blue patches. 0 = no cyan, 1 = replace all blue. Gold and red can still cover it; this is a relative color range, not a particle percentage.")]
        [Range(0, 1)] public float earlyHeatCyanCoverage = .78f;
        [ColorUsage(false, true)] public Color earlyHeatGoldColor = new Color(1, .86f, .12f);
        [Tooltip("How far gold extends into the cooler colors. 0 = no gold, 1 = replace all cool colors before red. Higher values make more of the heat map warm.")]
        [Range(0, 1)] public float earlyHeatGoldCoverage = .525f;
        [ColorUsage(false, true)] public Color earlyHeatRedColor = new Color(1, .12f, .012f);
        [Tooltip("How far red extends into the other colors. 0 = no red, 1 = all red. Higher values create more hot-colored patches.")]
        [Range(0, 1)] public float earlyHeatRedCoverage = .23f;
        [Header("Edit Mode preview")]
        [Tooltip("0 = near-uniform early matter, 0.5 = developed cosmic web, 1 = void-dominated late universe. Play Mode always follows regulation time.")]
        [Range(0, 1)] public float previewAge = .5f;
        [Tooltip("Keep the web flowing at the selected age, including ages 0 and 1. Disable for a still preview.")]
        public bool animatePreview = true;

        ArenaBoundsFromVectorGrid arena;
        ComputeBuffer particles;
        // Small CPU sample of the exact rendered topology for occasional gameplay events.
        CosmicWebTopology.Particle[] interactionSamples;
        Material material;
        MaterialPropertyBlock properties;
        int builtSeed, builtCount;
        float builtOrganicStrength;
        float motionSeconds, previousRemaining;
#if UNITY_EDITOR
        double lastPreviewTick;
        float previewSeconds, lastPreviewAge;
#endif
        Shader builtShader;
        public int FilamentCount { get; private set; }
        public CosmicWebTopology.Statistics TopologyStatistics { get; private set; }
        public int RenderedParticleCount => particles != null ? particles.count : 0;
        public int Generation { get; private set; }
        public float Age => Application.isPlaying ? MatchAge(match) : previewAge;
        public float MotionTime
        {
            get
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) return previewAge * 120 + previewSeconds;
#endif
                return motionSeconds;
            }
        }
        static readonly int ParticlesID = Shader.PropertyToID("_Particles"), AgeID = Shader.PropertyToID("_Age"),
            ProfileID = Shader.PropertyToID("_Oval"), ScaleID = Shader.PropertyToID("_WorldScale"),
            MatrixID = Shader.PropertyToID("_GridToWorld"), PaddingID = Shader.PropertyToID("_Padding"),
            DepthID = Shader.PropertyToID("_Depth"), BlueID = Shader.PropertyToID("_Blue"), PinkID = Shader.PropertyToID("_Pink"),
            BrightnessID = Shader.PropertyToID("_Brightness"), SizeID = Shader.PropertyToID("_GalaxySize"),
            VariationID = Shader.PropertyToID("_EvolutionVariation"), CondensationID = Shader.PropertyToID("_EdgeCondensation"),
            MotionTimeID = Shader.PropertyToID("_MotionTime"), DriftID = Shader.PropertyToID("_DriftStrength"),
            TurbulenceID = Shader.PropertyToID("_ClusterTurbulence"), HeatID = Shader.PropertyToID("_EarlyHeat"),
            BrightnessVarianceID = Shader.PropertyToID("_BrightnessVariance"), EdgeVignetteID = Shader.PropertyToID("_EdgeVignette"),
            LensingSmoothnessID = Shader.PropertyToID("_LensingTransitionSmoothness"),
            HeatTransitionID = Shader.PropertyToID("_HeatTransition"), HeatCoverageID = Shader.PropertyToID("_HeatCoverage"),
            HeatBlueID = Shader.PropertyToID("_HeatBlue"), HeatCyanID = Shader.PropertyToID("_HeatCyan"),
            HeatGoldID = Shader.PropertyToID("_HeatGold"), HeatRedID = Shader.PropertyToID("_HeatRed");

        void OnEnable()
        {
            arena = GetComponent<ArenaBoundsFromVectorGrid>(); properties = new MaterialPropertyBlock();
            previousRemaining = match ? match.RegulationRemainingSeconds : 0;
#if UNITY_EDITOR
            lastPreviewTick = UnityEditor.EditorApplication.timeSinceStartup; lastPreviewAge = previewAge;
            UnityEditor.EditorApplication.update += TickPreview;
#endif
        }
#if UNITY_EDITOR
        void TickPreview()
        {
            double now = UnityEditor.EditorApplication.timeSinceStartup;
            if (previewAge != lastPreviewAge) { previewSeconds = 0; lastPreviewAge = previewAge; }
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode || !animatePreview)
            { lastPreviewTick = now; return; }
            if (now - lastPreviewTick < 1.0 / 30) return;
            previewSeconds += (float)System.Math.Min(.1, now - lastPreviewTick); lastPreviewTick = now;
            // Edit Mode can repaint cameras without running another MonoBehaviour update.
            // This material is owned by the web, so its clock stays live on those repaints.
            if (material) material.SetFloat(MotionTimeID, MotionTime);
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
#endif
        void LateUpdate()
        {
            if (Application.isPlaying)
            {
                float remaining = match ? match.RegulationRemainingSeconds : 0;
                bool pausedBonus = match && match.Phase == MatchRuntimePhase.Bonus && remaining >= previousRemaining;
                if (!pausedBonus && (!match || !match.IsStartupBlocked)) motionSeconds += Time.deltaTime;
                previousRemaining = remaining;
            }
            if (!arena || !arena.Grid || !arena.ovalOutline || !particleShader) return;
            if (particles == null || builtSeed != seed || builtCount != particleCount || builtShader != particleShader || builtOrganicStrength != organicStrength) Rebuild();
            if (particles == null || !material) return;
            properties.SetBuffer(ParticlesID, particles);
            properties.SetFloat(AgeID, Age);
            properties.SetVector(ProfileID, arena.OutlineShaderParameters);
            properties.SetVector(ScaleID, new Vector4(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y), 0, 0));
            properties.SetMatrix(MatrixID, transform.localToWorldMatrix);
            properties.SetFloat(PaddingID, padding);
            properties.SetFloat(DepthID, backgroundDepth);
            properties.SetColor(BlueID, filamentColor);
            properties.SetColor(PinkID, clusterColor);
            properties.SetFloat(BrightnessID, brightness);
            properties.SetFloat(BrightnessVarianceID, Mathf.Clamp(brightnessVariance, 0, 2));
            properties.SetFloat(SizeID, galaxySize);
            properties.SetFloat(VariationID, evolutionVariation);
            properties.SetFloat(CondensationID, edgeCondensation);
            properties.SetFloat(LensingSmoothnessID, Mathf.Clamp01(lensingTransitionSmoothness));
            properties.SetFloat(EdgeVignetteID, Mathf.Max(0, edgeVignette));
            material.SetFloat(MotionTimeID, MotionTime);
            properties.SetFloat(DriftID, driftStrength);
            properties.SetFloat(TurbulenceID, clusterTurbulence ? Mathf.Clamp(turbulenceStrength, 0, 2) : 0);
            properties.SetVector(HeatID, new Vector4(Mathf.Clamp01(earlyHeat), Mathf.Clamp(earlyHeatBrightness, 1, 3), seed & 0xffff, 0));
            float heatEnd = Mathf.Clamp01(earlyHeatTransitionPoint);
            properties.SetVector(HeatTransitionID, new Vector4(Mathf.Max(0, heatEnd - Mathf.Clamp01(earlyHeatTransitionDuration)), heatEnd, 0, 0));
            properties.SetVector(HeatCoverageID, new Vector4(Mathf.Clamp01(earlyHeatCyanCoverage), Mathf.Clamp01(earlyHeatGoldCoverage), Mathf.Clamp01(earlyHeatRedCoverage), 0));
            // Raw vectors retain the exact shader-space values of the original heat palette.
            properties.SetVector(HeatBlueID, (Vector4)earlyHeatBlueColor);
            properties.SetVector(HeatCyanID, (Vector4)earlyHeatCyanColor);
            properties.SetVector(HeatGoldID, (Vector4)earlyHeatGoldColor);
            properties.SetVector(HeatRedID, (Vector4)earlyHeatRedColor);
            var drawBounds = new Bounds(transform.position, Vector3.one * (arena.OvalHalfWidthLocal * 2 * transform.lossyScale.magnitude + 10));
            Graphics.DrawProcedural(material, drawBounds, MeshTopology.Triangles, 6, particles.count,
                null, properties, ShadowCastingMode.Off, false, gameObject.layer);
        }
        public void Rebuild()
        {
            Release();
            if (!particleShader || !SystemInfo.supportsComputeShaders) return;
            var data = CosmicWebTopology.Build(Mathf.Clamp(particleCount, 8000, 100000), seed, out var statistics, organicStrength);
            particles = new ComputeBuffer(data.Length, CosmicWebTopology.ParticleStride, ComputeBufferType.Structured);
            particles.SetData(data);
            interactionSamples = new CosmicWebTopology.Particle[Mathf.Min(2048, data.Length)];
            for (int i = 0; i < interactionSamples.Length; i++)
                interactionSamples[i] = data[i * data.Length / interactionSamples.Length];
            material = new Material(particleShader) { name = "COSMOS cosmic web (generated)", hideFlags = HideFlags.HideAndDontSave };
            TopologyStatistics = statistics; FilamentCount = statistics.filaments;
            builtOrganicStrength = organicStrength; builtSeed = seed; builtCount = particleCount; builtShader = particleShader; Generation++;
        }
        public static float MatchAge(GameManagerScript manager)
        {
            if (!manager || manager.Phase == MatchRuntimePhase.Preparing || manager.Phase == MatchRuntimePhase.Countdown) return 0;
            return Mathf.Clamp01(1 - manager.RegulationRemainingSeconds / manager.RegulationDurationSeconds);
        }
        public bool TrySampleMatterPoint(System.Random random, float clusterBias, float clearance,
            out Vector3 webPosition, out Vector3 fieldPosition)
            => TrySampleMatterPoint(random, clusterBias, clearance, out _, out webPosition, out fieldPosition);

        public bool TrySampleMatterPoint(System.Random random, float clusterBias, float clearance,
            out CosmicWebTopology.Particle source, out Vector3 webPosition, out Vector3 fieldPosition)
        {
            source = default;
            webPosition = fieldPosition = Vector3.zero;
            if (random == null || !isActiveAndEnabled || !arena || !arena.IsValid || !arena.ovalOutline ||
                interactionSamples == null || interactionSamples.Length == 0) return false;
            float age = Age;
            for (int attempt = 0; attempt < 64; attempt++)
            {
                var p = interactionSamples[random.Next(interactionSamples.Length)];
                float formation = CosmicWebTopology.Phase(age / Mathf.Clamp(.5f + p.flow.w * evolutionVariation * .035f, .40f, .62f), p.flow.w, evolutionVariation);
                float evacuation = CosmicWebTopology.Accretion(age, p.bend.w, evolutionVariation);
                float knot = Mathf.Clamp01(Mathf.Lerp(p.filament.w, p.cluster.w, evacuation) * formation);
                // A small floor retains occasional filament events; the early soup remains dispersed.
                float weight = Mathf.Lerp(1, .02f + .98f * knot * knot * knot, Mathf.Clamp01(clusterBias));
                if (random.NextDouble() > weight) continue;
                // Reject goal caps and wall margins instead of moving the explosion off its galaxy.
                if (!TryGetMatterPoint(p, clearance, out webPosition, out fieldPosition)) continue;
                source = p;
                return true;
            }
            return false;
        }
        public bool TryGetMatterPoint(CosmicWebTopology.Particle source, float clearance,
            out Vector3 webPosition, out Vector3 fieldPosition)
        {
            webPosition = fieldPosition = Vector3.zero;
            if (!isActiveAndEnabled || !arena || !arena.IsValid || !arena.ovalOutline) return false;
            Vector2 scale = new Vector2(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
            if (scale.x < .00001f || scale.y < .00001f) return false;
            Vector3 raw = CosmicWebTopology.Position(source, Age, evolutionVariation, MotionTime, driftStrength,
                clusterTurbulence ? Mathf.Clamp(turbulenceStrength, 0, 2) : 0);
            Vector2 projected = CosmicWebTopology.Project(raw, arena.OutlineShaderParameters, scale,
                padding + .06f, edgeCondensation, lensingTransitionSmoothness);
            fieldPosition = transform.TransformPoint(new Vector3(projected.x, projected.y, 0));
            if (!arena.ContainsWorldPoint(fieldPosition, Mathf.Max(0, clearance))) return false;
            webPosition = transform.TransformPoint(new Vector3(projected.x, projected.y,
                -backgroundDepth + Mathf.Clamp(raw.z, -1, 1) * .45f));
            return true;
        }
        void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= TickPreview;
#endif
            Release();
        }
        void OnDestroy() { Release(); }
        void Release()
        {
            particles?.Release(); particles = null;
            interactionSamples = null;
            if (material) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
            material = null;
        }
    }
}
