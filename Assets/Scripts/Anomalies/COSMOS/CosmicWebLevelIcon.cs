using UnityEngine;
using UnityEngine.Serialization;

namespace Massive.Cosmos
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class CosmicWebLevelIcon : MonoBehaviour
    {
        [Header("Midpoint cloud — Age is always 0.5")]
        public CosmicWebIconData webData;
        [Tooltip("Maximum 8,000 baked tracers. Lower this to reduce the icon's particle budget.")]
        [Range(256, 8000)] public int particleCount = 8000;
        [Range(.1f, .8f)] public float cloudScale = .55f;
        [Tooltip("Ovoid length divided by its height, in the cloud's own frame. The complete volume tilts and turns in 3D.")]
        [Range(1, 4)] public float ovalAspect = 1.8f;
        [Tooltip("Ovoid depth relative to its length. Set to 1 / Oval Aspect for a round cross-section; the default matches its height.")]
        [Range(.1f, 1)] public float depthRatio = 1f / 1.8f;
        public Vector3 volumeTilt = new Vector3(18, 10, -12);
        [Header("Internal motion")]
        public bool animate = true;
        [Tooltip("Independent motion clock. Does not advance Age or trigger further collapse.")]
        [Range(0, 3)] public float motionSpeed = 1;
        [Range(0, 2)] public float driftStrength = 1;
        public bool clusterTurbulence = true;
        [Range(0, 2)] public float turbulenceStrength = 2;
        [Range(0, 2)] public float evolutionVariation = 1;
        [Tooltip("Slow 3D tumble of the complete ovoid, revealing its sides and ends. Set to zero to stop the tumble; internal flow continues.")]
        [Range(-10, 10)] public float turnDegreesPerSecond = 3;
        [Header("Palette and light")]
        [ColorUsage(false, true)] public Color filamentColor = new Color(.035f, .32f, .85f);
        [ColorUsage(false, true)] public Color clusterColor = new Color(1, .24f, .61f);
        [Tooltip("Unlit emission strength. Each solid particle uses its full color, without soft glow or depth dimming.")]
        [Range(.1f, 4)] public float brightness = 1.5f;
        [Tooltip("Variation in solid particle size. Zero gives equal sizes; higher values produce finer dust and larger knots. Replaces brightness variation.")]
        [FormerlySerializedAs("brightnessVariance"), Range(0, 2)] public float sizeVariance = 1.449f;
        [Tooltip("Overall size of the crisp, opaque particles.")]
        [Range(.001f, .012f)] public float pointSize = .0035f;
        [Header("Preview")]
        public bool animateInEditor = true;

        MeshFilter filter;
        MeshRenderer meshRenderer;
        Mesh mesh;
        ComputeBuffer particles;
        MaterialPropertyBlock properties;
        CosmicWebIconData builtData;
        float seconds;
#if UNITY_EDITOR
        double previousEditorTime;
#endif
        public int RenderedParticleCount => particles != null ? particles.count : 0;
        public float MotionSeconds => seconds;
        static readonly int ParticlesID = Shader.PropertyToID("_Particles"), ClockID = Shader.PropertyToID("_MotionTime"),
            ScaleID = Shader.PropertyToID("_CloudScale"), RotationID = Shader.PropertyToID("_VolumeRotation"),
            ShapeID = Shader.PropertyToID("_VolumeShape"),
            BlueID = Shader.PropertyToID("_Blue"), PinkID = Shader.PropertyToID("_Pink"),
            BrightnessID = Shader.PropertyToID("_Brightness"), SizeVarianceID = Shader.PropertyToID("_SizeVariance"),
            SizeID = Shader.PropertyToID("_PointSize"), DriftID = Shader.PropertyToID("_DriftStrength"),
            TurbulenceID = Shader.PropertyToID("_ClusterTurbulence"), EvolutionID = Shader.PropertyToID("_EvolutionVariation");

        void OnEnable()
        {
            filter = GetComponent<MeshFilter>(); meshRenderer = GetComponent<MeshRenderer>();
            properties = new MaterialPropertyBlock();
#if UNITY_EDITOR
            previousEditorTime = UnityEditor.EditorApplication.timeSinceStartup;
#endif
        }
        void LateUpdate()
        {
            float delta = 0;
            if (Application.IsPlaying(gameObject)) delta = Time.unscaledDeltaTime;
#if UNITY_EDITOR
            else
            {
                double now = UnityEditor.EditorApplication.timeSinceStartup;
                if (animateInEditor) delta = Mathf.Clamp((float)(now - previousEditorTime), 0, .1f);
                previousEditorTime = now;
            }
#endif
            Advance(delta);
        }
        public void Advance(float deltaTime)
        {
            if (!isActiveAndEnabled) return;
            if (animate) seconds += Mathf.Max(0, deltaTime) * motionSpeed;
            int count = webData ? Mathf.Clamp(particleCount, 0, webData.Count) : 0;
            if (builtData != webData || RenderedParticleCount != count || !mesh) Rebuild(count);
            if (particles == null) return;
            properties.SetBuffer(ParticlesID, particles);
            properties.SetFloat(ClockID, 60 + seconds);
            properties.SetFloat(ScaleID, cloudScale);
            properties.SetVector(ShapeID, new Vector4(1, Mathf.Clamp(depthRatio, .1f, 1), 1 / Mathf.Clamp(ovalAspect, 1, 4), 0));
            // An oblique axis exposes the ends as well as the sides to the menu camera.
            properties.SetMatrix(RotationID, Matrix4x4.Rotate(Quaternion.Euler(volumeTilt) * Quaternion.AngleAxis(seconds * turnDegreesPerSecond, new Vector3(.25f, .65f, 1))));
            properties.SetColor(BlueID, filamentColor); properties.SetColor(PinkID, clusterColor);
            properties.SetFloat(BrightnessID, brightness); properties.SetFloat(SizeVarianceID, Mathf.Clamp(sizeVariance, 0, 2));
            properties.SetFloat(SizeID, pointSize); properties.SetFloat(DriftID, driftStrength);
            properties.SetFloat(TurbulenceID, clusterTurbulence ? turbulenceStrength : 0);
            properties.SetFloat(EvolutionID, evolutionVariation);
            meshRenderer.SetPropertyBlock(properties);
        }
        void Rebuild(int count)
        {
            Release(); builtData = webData;
            if (count <= 0 || !meshRenderer || !filter) return;
            particles = new ComputeBuffer(count, CosmicWebTopology.ParticleStride);
            particles.SetData(webData.particles, 0, 0, count);
            // One indexed quad per tracer; positions and all animation stay on the GPU.
            var vertices = new Vector3[count * 4]; var indices = new int[count * 6];
            var ids = new Vector2[count * 4];
            for (int i = 0; i < count; i++)
            {
                int v = i * 4, t = i * 6;
                vertices[v] = new Vector3(-1, -1, 0); vertices[v + 1] = new Vector3(-1, 1, 0);
                vertices[v + 2] = new Vector3(1, 1, 0); vertices[v + 3] = new Vector3(1, -1, 0);
                for (int j = 0; j < 4; j++) ids[v + j] = new Vector2(i, 0);
                indices[t] = v; indices[t + 1] = v + 1; indices[t + 2] = v + 2;
                indices[t + 3] = v; indices[t + 4] = v + 2; indices[t + 5] = v + 3;
            }
            mesh = new Mesh { name = "COSMOS Icon Quads", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = vertices; mesh.uv = ids; mesh.triangles = indices;
            // Includes the full 3D cloud, tilt/turn, shader motion and the largest Inspector scale.
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 6);
            mesh.UploadMeshData(true);
            filter.sharedMesh = mesh; meshRenderer.forceRenderingOff = false;
        }
        void Release()
        {
            if (meshRenderer) { meshRenderer.SetPropertyBlock(null); meshRenderer.forceRenderingOff = true; }
            if (filter && filter.sharedMesh == mesh) filter.sharedMesh = null;
            particles?.Release(); particles = null;
            if (mesh) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
            mesh = null; builtData = null;
        }
        void OnDisable() { Release(); }
        void OnDestroy() { Release(); }
    }
}
