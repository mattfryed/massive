using System.Collections.Generic;
using UnityEngine;

namespace Massive.Emergence
{
    /// <summary>A bounded, force-shaped 3D colony with cached formation and inexpensive menu motion.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class EmergenceLevelIcon : MonoBehaviour
    {
        [Header("Organism formation")]
        [Tooltip("Source of the attraction matrix, spacing, species proportions, relative sizes and colors. Never modified by the icon.")]
        public EmergenceProfile palette;
        [Tooltip("Distribute the combined structure/motor/catalyst count using the ecology's body-species proportions. Disable to use the individual counts below.")]
        public bool matchEcologyComposition = true;
        [Range(32, 480)] public int structureParticles = 160;
        [Range(12, 200)] public int motorParticles = 54;
        [Range(4, 80)] public int catalystParticles = 19;
        [Range(0, 48)] public int nutrientParticles = 9;
        [Tooltip("One-time 3D settling steps using the ecology's forces. Results are cached between menu and instructions instances.")]
        [Range(30, 360)] public int settlingSteps = 240;
        [Range(.2f, .8f)] public float formationRadius = .62f;
        [Tooltip("Individual particle radius in local units. Zero hides the particles without changing the formation.")]
        [Range(0f, .06f)] public float particleRadius = .026f;
        [Range(0, .4f)] public float sizeVariation = .18f;
        public Vector3 volumeTilt = new Vector3(16, 0, -12);
        [Header("Bounded living motion")]
        public bool animate = true;
        [Range(0, .08f)] public float breathAmount = .035f;
        [Range(2, 12)] public float breathSeconds = 4.8f;
        [Range(0, .025f)] public float internalMotion = .008f;
        [Range(0, 15)] public float rockDegrees = 8;
        [Range(6, 30)] public float rockSeconds = 17;
        [Header("Local particle flow")]
        public bool enableLocalFlow = true;
        [Tooltip("Internal circulation speed in degrees per second. Zero pauses local flow; Animate pauses all motion.")]
        [Range(0, 90)] public float flowSpeed = 30;
        [Tooltip("Difference in circulation between layers. At one, opposite sides can flow in opposite directions.")]
        [Range(0, 1)] public float flowShear = .85f;
        [Tooltip("Local eddy twist in degrees. Nearby particles share a smooth current instead of independently jittering.")]
        [Range(0, 45)] public float eddyDegrees = 20;
        [Tooltip("Spatial frequency of local eddies. Higher values create smaller swirls.")]
        [Range(1, 6)] public float eddyScale = 3;
        [Tooltip("Circulation near the organism's surface relative to its interior.")]
        [Range(0, 1)] public float surfaceFlow = .35f;
        [Header("Pivot, recoil and settling")]
        public bool enablePivots = true;
        [Range(0, 45)] public float pivotDegrees = 24;
        [Range(1.5f, 8)] public float pivotInterval = 3.8f;
        [Range(.08f, .6f)] public float pivotSeconds = .2f;
        [Range(.2f, 1.5f)] public float settleSeconds = .8f;
        [Tooltip("Maximum displacement from the icon's anchor, in local units.")]
        [Range(0, .08f)] public float pivotRecoil = .028f;
        [Range(0, .08f)] public float pivotSquash = .035f;
        [Header("Editor preview")]
        public bool animateInEditor = true;

        [SerializeField, HideInInspector] int bakedFormationKey;
        [SerializeField, HideInInspector] Vector3[] bakedCenters;
        [SerializeField, HideInInspector] float[] bakedSizes;
        [SerializeField, HideInInspector] float bakedMaximumSize;

        Mesh mesh, previousMesh;
        MeshFilter filter;
        MeshRenderer meshRenderer;
        MaterialPropertyBlock properties, previousProperties;
        bool dirty = true;
        double editorTime;
        float seconds, flowPhase;
        float maximumSize = 1.5f;
        int formationKey;
        readonly int[] counts = new int[4];
        public int ParticleCount { get; private set; }
        public float MotionSeconds => seconds;
        // Local circulation preserves each center's radius, as does the colony rotation.
        // Include the maximum per-particle size, ripple, strain and recoil.
        public float EnvelopeRadius => (Mathf.Clamp(formationRadius, .2f, .8f) * (1 + Mathf.Clamp(breathAmount, 0, .08f))
            + Mathf.Clamp(internalMotion, 0, .025f)) * (1 + (enablePivots ? Mathf.Clamp(pivotSquash, 0, .08f) : 0))
            + Mathf.Clamp(particleRadius, 0f, .06f) * (1 + Mathf.Clamp(sizeVariation, 0, .4f)) * maximumSize
            + (enablePivots ? Mathf.Clamp(pivotRecoil, 0, .08f) : 0);

        static readonly int MotionId = Shader.PropertyToID("_Motion"), ShapeId = Shader.PropertyToID("_Shape"),
            RotationId = Shader.PropertyToID("_ColonyRotation"), PivotId = Shader.PropertyToID("_Pivot"),
            FlowId = Shader.PropertyToID("_Flow"), FlowShapeId = Shader.PropertyToID("_FlowShape");
        static readonly int[] ColorIds = { Shader.PropertyToID("_Structure"), Shader.PropertyToID("_Motor"),
            Shader.PropertyToID("_Catalyst"), Shader.PropertyToID("_Nutrient") };
        static readonly Color[] DefaultColors = { new Color(.25f, .9f, .68f), new Color(.85f, .3f, .78f),
            new Color(1, .8f, .25f), new Color(.25f, .6f, 1) };

        void OnEnable() { dirty = true; editorTime = Time.realtimeSinceStartupAsDouble; }
        void OnValidate() { dirty = true; }
        void OnDisable() { Release(); }
        void OnDestroy() { Release(); }
        void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            float dt = Application.IsPlaying(gameObject) ? Time.unscaledDeltaTime :
                animateInEditor ? Mathf.Clamp((float)(now - editorTime), 0, .05f) : 0;
            editorTime = now;
            Advance(dt);
        }

        public void Advance(float deltaTime)
        {
            if (!isActiveAndEnabled || !gameObject.scene.IsValid()) return;
            ResolveCounts();
            // Small parameter hash also picks up edits to the referenced ecology profile.
            if (formationKey != EmergenceIconFormation.Key(palette, counts, Mathf.Clamp(settlingSteps, 30, 360))) dirty = true;
            if (dirty || !mesh) Rebuild();
            if (!mesh) return;
            if (animate)
            {
                float dt = Mathf.Max(0, deltaTime);
                seconds += dt;
                // Accumulate travel separately so adjusting speed does not teleport particles.
                if (enableLocalFlow) flowPhase += dt * Mathf.Clamp(flowSpeed, 0, 90) * Mathf.Deg2Rad;
            }
            float breath = seconds * (2 * Mathf.PI / Mathf.Clamp(breathSeconds, 2, 12));
            float rock = seconds * (2 * Mathf.PI / Mathf.Clamp(rockSeconds, 6, 30));
            float degrees = Mathf.Clamp(rockDegrees, 0, 15);
            var rotation = Quaternion.Euler(volumeTilt) * Quaternion.Euler(
                Mathf.Sin(rock) * degrees, Mathf.Sin(rock * .73f) * degrees, Mathf.Sin(rock * .57f) * degrees * .45f);
            Vector4 pivot = Vector4.zero;
            if (enablePivots) rotation *= PivotMotion(out pivot);
            properties.SetMatrix(RotationId, Matrix4x4.Rotate(rotation));
            properties.SetVector(PivotId, pivot);
            properties.SetVector(FlowId, new Vector4(flowPhase, Mathf.Clamp01(flowShear),
                Mathf.Clamp(eddyDegrees, 0, 45) * Mathf.Deg2Rad, Mathf.Clamp(eddyScale, 1, 6)));
            properties.SetVector(FlowShapeId, new Vector4(Mathf.Clamp01(surfaceFlow), enableLocalFlow ? 1 : 0, 0, 0));
            properties.SetVector(MotionId, new Vector4(breath, Mathf.Clamp(breathAmount, 0, .08f),
                Mathf.Clamp(internalMotion, 0, .025f), seconds * .8f));
            properties.SetVector(ShapeId, new Vector4(Mathf.Clamp(formationRadius, .2f, .8f),
                Mathf.Clamp(particleRadius, 0f, .06f), Mathf.Clamp(sizeVariation, 0, .4f), 0));
            for (int i = 0; i < ColorIds.Length; i++)
            {
                Color c = EmergenceIconFormation.Species(palette, i)?.color ?? DefaultColors[i];
                c.a = 1; properties.SetColor(ColorIds[i], c);
            }
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * (EnvelopeRadius * 2));
            meshRenderer.SetPropertyBlock(properties);
        }

        public void Rebuild()
        {
            Release();
            if (!isActiveAndEnabled || !gameObject.scene.IsValid()) return;
            filter = GetComponent<MeshFilter>(); meshRenderer = GetComponent<MeshRenderer>();
            previousMesh = filter.sharedMesh;
            previousProperties = new MaterialPropertyBlock(); meshRenderer.GetPropertyBlock(previousProperties);
            properties = new MaterialPropertyBlock();
            ResolveCounts();
            int steps = Mathf.Clamp(settlingSteps, 30, 360);
            formationKey = EmergenceIconFormation.Key(palette, counts, steps);
            int total = counts[0] + counts[1] + counts[2] + counts[3];
            var formation = bakedFormationKey == formationKey && bakedCenters != null && bakedCenters.Length == total
                && bakedSizes != null && bakedSizes.Length == total
                ? new EmergenceIconFormation.Shape { centers = bakedCenters, sizes = bakedSizes, maximumSize = bakedMaximumSize }
                : EmergenceIconFormation.Build(palette, counts, steps);
            maximumSize = formation.maximumSize;
            var vertices = new List<Vector3>(); var centers = new List<Vector4>();
            var details = new List<Vector4>(); var triangles = new List<int>();
            const int latitude = 6, longitude = 10;
            ParticleCount = 0;
            for (int species = 0; species < counts.Length; species++)
            for (int i = 0; i < counts[species]; i++)
            {
                Vector3 center = formation.centers[ParticleCount];
                float phase = i * 1.618f + species * 2.1f;
                int start = vertices.Count;
                for (int y = 0; y <= latitude; y++) for (int x = 0; x <= longitude; x++)
                {
                    float polar = y * Mathf.PI / latitude, azimuth = x * (2 * Mathf.PI / longitude);
                    vertices.Add(new Vector3(Mathf.Sin(polar) * Mathf.Cos(azimuth), Mathf.Cos(polar), Mathf.Sin(polar) * Mathf.Sin(azimuth)));
                    centers.Add(new Vector4(center.x, center.y, center.z, species));
                    details.Add(new Vector4(phase, Mathf.Sin(i * 17.13f + species * 3.7f), formation.sizes[ParticleCount], 0));
                    if (y == latitude || x == longitude) continue;
                    int a = start + y * (longitude + 1) + x, b = a + longitude + 1;
                    triangles.Add(a); triangles.Add(a + 1); triangles.Add(b);
                    triangles.Add(a + 1); triangles.Add(b + 1); triangles.Add(b);
                }
                ParticleCount++;
            }
            mesh = new Mesh { name = "EMERGENCE organism spheres", hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(vertices); mesh.SetUVs(0, centers); mesh.SetUVs(1, details); mesh.SetTriangles(triangles, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * (EnvelopeRadius * 2));
            mesh.UploadMeshData(true); filter.sharedMesh = mesh; dirty = false;
        }

        // Called by the authoring tools; the saved mesh-free snapshot avoids runtime relaxation.
        public void BakeFormation()
        {
            ResolveCounts();
            int steps = Mathf.Clamp(settlingSteps, 30, 360);
            var formation = EmergenceIconFormation.Build(palette, counts, steps);
            bakedFormationKey = EmergenceIconFormation.Key(palette, counts, steps);
            bakedCenters = formation.centers; bakedSizes = formation.sizes; bakedMaximumSize = formation.maximumSize;
            dirty = true;
        }

        void ResolveCounts()
        {
            counts[0] = Mathf.Clamp(structureParticles, 32, 480); counts[1] = Mathf.Clamp(motorParticles, 12, 200);
            counts[2] = Mathf.Clamp(catalystParticles, 4, 80); counts[3] = Mathf.Clamp(nutrientParticles, 0, 48);
            if (!matchEcologyComposition || !palette) return;
            int total = counts[0] + counts[1] + counts[2];
            float structure = Mathf.Max(0, EmergenceIconFormation.Species(palette, 0)?.count ?? 0);
            float motor = Mathf.Max(0, EmergenceIconFormation.Species(palette, 1)?.count ?? 0);
            float catalyst = Mathf.Max(0, EmergenceIconFormation.Species(palette, 2)?.count ?? 0);
            float sum = structure + motor + catalyst;
            if (sum <= 0) return;
            counts[0] = Mathf.RoundToInt(total * structure / sum);
            counts[1] = Mathf.Clamp(Mathf.RoundToInt(total * motor / sum), 0, total - counts[0]);
            counts[2] = total - counts[0] - counts[1];
        }

        Quaternion PivotMotion(out Vector4 motion)
        {
            // Each bounded pose is held between short turns. Timing and axes vary deterministically,
            // so the movement reads as intermittent steering rather than continuous wobbling.
            float interval = Mathf.Clamp(pivotInterval, 1.5f, 8);
            int cycle = Mathf.FloorToInt(seconds / interval);
            float phase = seconds - cycle * interval;
            float onset = interval * Mathf.Lerp(.15f, .4f, Noise(cycle + 3.4f));
            phase -= onset;
            Quaternion from = cycle == 0 ? Quaternion.identity : PivotPose(cycle - 1);
            Quaternion to = PivotPose(cycle);
            if (phase < 0) { motion = Vector4.zero; return from; }
            float turn = Mathf.Clamp(pivotSeconds, .08f, .6f);
            // Finish within this cycle even at the shortest interval / longest authoring settings.
            float settle = Mathf.Min(Mathf.Clamp(settleSeconds, .2f, 1.5f), interval - onset - turn);
            float u = Mathf.Clamp01(phase / turn);
            float blend = u * u * (3 - 2 * u);
            float recovery = Mathf.Clamp01((phase - turn) / Mathf.Max(.05f, settle));
            float pulse = phase < turn ? blend : (1 - recovery) * (1 - recovery) * Mathf.Cos(recovery * Mathf.PI);
            Vector3 direction = to * Vector3.right;
            Vector3 recoil = direction * (Mathf.Clamp(pivotRecoil, 0, .08f) * pulse);
            motion = new Vector4(recoil.x, recoil.y, recoil.z, pulse * Mathf.Clamp(pivotSquash, 0, .08f));
            float overshoot = .06f * Mathf.Sin(recovery * Mathf.PI * 2) * (1 - recovery);
            return Quaternion.SlerpUnclamped(from, to, blend + overshoot);
        }

        Quaternion PivotPose(int cycle)
        {
            Vector3 axis = new Vector3(Noise(cycle + 11.7f) * 2 - 1, Noise(cycle + 29.1f) * 2 - 1, Noise(cycle + 51.3f) * 2 - 1).normalized;
            return Quaternion.AngleAxis(Mathf.Clamp(pivotDegrees, 0, 45) * Mathf.Lerp(.55f, 1, Noise(cycle + 7.2f)), axis);
        }

        static float Noise(float n) { float value = Mathf.Sin(n * 127.1f + 311.7f) * 43758.5453f; return value - Mathf.Floor(value); }

        void Release()
        {
            if (meshRenderer && properties != null) meshRenderer.SetPropertyBlock(previousProperties);
            if (filter && filter.sharedMesh == mesh) filter.sharedMesh = previousMesh;
            if (mesh) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
            mesh = null; previousMesh = null; properties = null; previousProperties = null; ParticleCount = 0;
        }
    }
}
