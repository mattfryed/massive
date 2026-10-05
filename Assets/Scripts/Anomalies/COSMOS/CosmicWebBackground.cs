using Massive.Scoring;
using UnityEngine;
using UnityEngine.Rendering;

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
        [Tooltip("Gradual compression toward the oval edge and goal separators.")]
        [Range(0, 1)] public float edgeCondensation = .6f;
        [Header("Palette and light")]
        [ColorUsage(false, true)] public Color filamentColor = new Color(.035f, .32f, .85f);
        [ColorUsage(false, true)] public Color clusterColor = new Color(1f, .24f, .61f);
        [Range(.1f, 3)] public float brightness = 1;
        [Range(.008f, .065f)] public float galaxySize = .025f;
        [Header("Edit Mode preview")]
        [Tooltip("0 = near-uniform early matter, 0.5 = developed cosmic web, 1 = void-dominated late universe. Play Mode always follows regulation time.")]
        [Range(0, 1)] public float previewAge = .5f;

        ArenaBoundsFromVectorGrid arena;
        ComputeBuffer particles;
        Material material;
        MaterialPropertyBlock properties;
        int builtSeed, builtCount;
        float builtOrganicStrength;
        Shader builtShader;
        public int FilamentCount { get; private set; }
        public CosmicWebTopology.Statistics TopologyStatistics { get; private set; }
        public int RenderedParticleCount => particles != null ? particles.count : 0;
        public int Generation { get; private set; }
        public float Age => Application.isPlaying ? MatchAge(match) : previewAge;
        static readonly int ParticlesID = Shader.PropertyToID("_Particles"), AgeID = Shader.PropertyToID("_Age"),
            ProfileID = Shader.PropertyToID("_Oval"), ScaleID = Shader.PropertyToID("_WorldScale"),
            MatrixID = Shader.PropertyToID("_GridToWorld"), PaddingID = Shader.PropertyToID("_Padding"),
            DepthID = Shader.PropertyToID("_Depth"), BlueID = Shader.PropertyToID("_Blue"), PinkID = Shader.PropertyToID("_Pink"),
            BrightnessID = Shader.PropertyToID("_Brightness"), SizeID = Shader.PropertyToID("_GalaxySize"),
            VariationID = Shader.PropertyToID("_EvolutionVariation"), CondensationID = Shader.PropertyToID("_EdgeCondensation");

        void OnEnable() { arena = GetComponent<ArenaBoundsFromVectorGrid>(); properties = new MaterialPropertyBlock(); }
        void LateUpdate()
        {
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
            properties.SetFloat(SizeID, galaxySize);
            properties.SetFloat(VariationID, evolutionVariation);
            properties.SetFloat(CondensationID, edgeCondensation);
            var drawBounds = new Bounds(transform.position, Vector3.one * (arena.OvalHalfWidthLocal * 2 * transform.lossyScale.magnitude + 10));
            Graphics.DrawProcedural(material, drawBounds, MeshTopology.Triangles, 6, particles.count,
                null, properties, ShadowCastingMode.Off, false, gameObject.layer);
        }
        public void Rebuild()
        {
            Release();
            if (!particleShader || !SystemInfo.supportsComputeShaders) return;
            var data = CosmicWebTopology.Build(Mathf.Clamp(particleCount, 8000, 100000), seed, out var statistics, organicStrength);
            particles = new ComputeBuffer(data.Length, 80, ComputeBufferType.Structured);
            particles.SetData(data);
            material = new Material(particleShader) { name = "COSMOS cosmic web (generated)", hideFlags = HideFlags.HideAndDontSave };
            TopologyStatistics = statistics; FilamentCount = statistics.filaments;
            builtOrganicStrength = organicStrength; builtSeed = seed; builtCount = particleCount; builtShader = particleShader; Generation++;
        }
        public static float MatchAge(GameManagerScript manager)
        {
            if (!manager || manager.Phase == MatchRuntimePhase.Preparing || manager.Phase == MatchRuntimePhase.Countdown) return 0;
            return Mathf.Clamp01(1 - manager.RegulationRemainingSeconds / manager.RegulationDurationSeconds);
        }
        void OnDisable() { Release(); }
        void OnDestroy() { Release(); }
        void Release()
        {
            particles?.Release(); particles = null;
            if (material) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
            material = null;
        }
    }
}
