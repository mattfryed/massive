using System.Collections.Generic;
using UnityEngine;

namespace Massive.Emergence
{
    /// <summary>A bounded, presentation-only colony. No ecology solver, pickups or physics.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class EmergenceLevelIcon : MonoBehaviour
    {
        [Header("Organism formation")]
        [Tooltip("Uses the ecology's species colors without running its simulation.")]
        public EmergenceProfile palette;
        [Range(32, 240)] public int structureParticles = 160;
        [Range(12, 100)] public int motorParticles = 54;
        [Range(4, 40)] public int catalystParticles = 19;
        [Range(0, 24)] public int nutrientParticles = 9;
        [Range(.2f, .8f)] public float formationRadius = .62f;
        [Range(.008f, .06f)] public float particleRadius = .026f;
        [Range(0, .4f)] public float sizeVariation = .18f;
        public Vector3 volumeTilt = new Vector3(16, 0, -12);
        [Header("Bounded living motion")]
        public bool animate = true;
        [Range(0, .08f)] public float breathAmount = .035f;
        [Range(2, 12)] public float breathSeconds = 4.8f;
        [Range(0, .025f)] public float internalMotion = .008f;
        [Range(0, 15)] public float rockDegrees = 8;
        [Range(6, 30)] public float rockSeconds = 17;
        [Header("Editor preview")]
        public bool animateInEditor = true;

        Mesh mesh, previousMesh;
        MeshFilter filter;
        MeshRenderer meshRenderer;
        MaterialPropertyBlock properties, previousProperties;
        bool dirty = true;
        double editorTime;
        float seconds;
        public int ParticleCount { get; private set; }
        public float MotionSeconds => seconds;
        // Rotation preserves this sphere. Include the maximum per-particle size and ripple.
        public float EnvelopeRadius => Mathf.Clamp(formationRadius, .2f, .8f) * (1 + Mathf.Clamp(breathAmount, 0, .08f))
            + Mathf.Clamp(internalMotion, 0, .025f) + Mathf.Clamp(particleRadius, .008f, .06f) * 1.4f * 1.12f;

        static readonly int MotionId = Shader.PropertyToID("_Motion"), ShapeId = Shader.PropertyToID("_Shape"),
            RotationId = Shader.PropertyToID("_ColonyRotation");
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
            if (dirty || !mesh) Rebuild();
            if (!mesh) return;
            if (animate) seconds += Mathf.Max(0, deltaTime);
            float breath = seconds * (2 * Mathf.PI / Mathf.Clamp(breathSeconds, 2, 12));
            float rock = seconds * (2 * Mathf.PI / Mathf.Clamp(rockSeconds, 6, 30));
            float degrees = Mathf.Clamp(rockDegrees, 0, 15);
            var rotation = Quaternion.Euler(volumeTilt) * Quaternion.Euler(
                Mathf.Sin(rock) * degrees, Mathf.Sin(rock * .73f) * degrees, Mathf.Sin(rock * .57f) * degrees * .45f);
            properties.SetMatrix(RotationId, Matrix4x4.Rotate(rotation));
            properties.SetVector(MotionId, new Vector4(breath, Mathf.Clamp(breathAmount, 0, .08f),
                Mathf.Clamp(internalMotion, 0, .025f), seconds * .8f));
            properties.SetVector(ShapeId, new Vector4(Mathf.Clamp(formationRadius, .2f, .8f),
                Mathf.Clamp(particleRadius, .008f, .06f), Mathf.Clamp(sizeVariation, 0, .4f), 0));
            for (int i = 0; i < ColorIds.Length; i++)
            {
                Color c = palette && palette.species != null && palette.species.Length > i ? palette.species[i].color : DefaultColors[i];
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
            int[] counts = { Mathf.Clamp(structureParticles, 32, 240), Mathf.Clamp(motorParticles, 12, 100),
                Mathf.Clamp(catalystParticles, 4, 40), Mathf.Clamp(nutrientParticles, 0, 24) };
            var vertices = new List<Vector3>(); var centers = new List<Vector4>();
            var details = new List<Vector4>(); var triangles = new List<int>();
            const int latitude = 6, longitude = 10;
            ParticleCount = 0;
            for (int species = 0; species < counts.Length; species++)
            for (int i = 0; i < counts[species]; i++)
            {
                float angle = i * 2.39996323f;
                float fraction = (i + .5f) / counts[species];
                Vector3 center;
                if (species == 0)
                {
                    // An irregular open membrane exposes the colored interior to the menu camera.
                    float y = Mathf.Lerp(-1, .62f, fraction);
                    float r = Mathf.Sqrt(1 - y * y);
                    float lobe = .93f + .06f * Mathf.Sin(angle * 3 + y * 2);
                    center = new Vector3(Mathf.Cos(angle) * r, y * .68f, Mathf.Sin(angle) * r * .82f) * lobe;
                }
                else if (species == 1)
                {
                    // Motors gather in an internal folded belt, with a distinct hollow core.
                    float ring = .51f + .07f * Mathf.Sin(angle * 3);
                    center = new Vector3(Mathf.Cos(angle) * ring, .13f + .16f * Mathf.Sin(angle * 2 + i * .3f),
                        Mathf.Sin(angle) * ring * .82f);
                }
                else
                {
                    float r = Mathf.Sqrt(fraction) * (species == 2 ? .29f : .36f);
                    center = new Vector3(Mathf.Cos(angle) * r + (species == 2 ? -.08f : .18f),
                        .1f + .14f * Mathf.Sin(i * 1.7f), Mathf.Sin(angle) * r * .85f);
                }
                float phase = i * 1.618f + species * 2.1f;
                int start = vertices.Count;
                for (int y = 0; y <= latitude; y++) for (int x = 0; x <= longitude; x++)
                {
                    float polar = y * Mathf.PI / latitude, azimuth = x * (2 * Mathf.PI / longitude);
                    vertices.Add(new Vector3(Mathf.Sin(polar) * Mathf.Cos(azimuth), Mathf.Cos(polar), Mathf.Sin(polar) * Mathf.Sin(azimuth)));
                    centers.Add(new Vector4(center.x, center.y, center.z, species));
                    details.Add(new Vector4(phase, Mathf.Sin(i * 17.13f + species * 3.7f), species == 1 ? 1.12f : 1, 0));
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

        void Release()
        {
            if (meshRenderer && properties != null) meshRenderer.SetPropertyBlock(previousProperties);
            if (filter && filter.sharedMesh == mesh) filter.sharedMesh = previousMesh;
            if (mesh) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
            mesh = null; previousMesh = null; properties = null; previousProperties = null; ParticleCount = 0;
        }
    }
}
