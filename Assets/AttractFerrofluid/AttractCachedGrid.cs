using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.AttractStudy
{
    /// <summary>A moving presentation grid sampling a cached, stationary attraction field.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class AttractCachedGrid : MonoBehaviour
    {
        [Header("Grid")]
        [Tooltip("Total local XY coverage, including the area revealed by rotation and tilt.")]
        public Vector2 gridSize = new Vector2(80f, 80f);
        [Min(.25f), Tooltip("Side length of each square. Layout edits automatically rebuild the mesh.")]
        public float gridSpacing = 1f;
        [Range(2, 16)] public int segmentsPerCell = 8;

        [Header("Appearance (no rebake needed)")]
        [Range(.5f, 8f), Tooltip("Screen-space width in pixels, independent of distance and grid scale.")]
        public float lineWidth = 1.3f;
        [ColorUsage(true, true)] public Color baseColor = new Color(1, 1, 1, .12941177f);
        [ColorUsage(true, true)] public Color attractionColor = new Color(4.71f, 4.71f, 4.71f, .12941177f);

        [Header("Cached Attraction")]
        public Transform attractor;
        [Min(.01f)] public float attractionRadius = 20f;
        [Min(0f)] public float attractionStrength = 103.9f;
        [Min(.01f)] public float springStrength = 12f;
        [Tooltip("Distance pulled, in world units, at which the attraction color is fully applied.")]
        [Min(.01f)] public float colorDisplacement = 1f;
        [Range(0f, .9f), Tooltip("Combined inner plateau from the former grid and radial force.")]
        public float innerFraction = .2f;
        [Range(.01f, 1f)] public float influenceCap = .8f;
        [Min(0f)] public float crowdStiffness = .6f;

        [SerializeField] Shader lineShader;

        const int FieldSamples = 1024;
        const int VertexBudget = 500000;
        const HideFlags Transient = HideFlags.HideAndDontSave;
        static readonly int FieldId = Shader.PropertyToID("_AttractionField");
        static readonly int FieldUvId = Shader.PropertyToID("_FieldUv");
        static readonly int AttractorId = Shader.PropertyToID("_Attractor");
        static readonly int WidthId = Shader.PropertyToID("_LineWidth");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int AttractionColorId = Shader.PropertyToID("_AttractionColor");
        static readonly int ColorDistanceId = Shader.PropertyToID("_ColorDisplacement");

        MeshFilter meshFilter;
        MeshRenderer meshRenderer;
        Mesh mesh;
        Material material, originalMaterial;
        Mesh originalMesh;
        Texture2D field;
        MaterialPropertyBlock properties, originalProperties;
        Vector4 layoutKey, shapeKey, falloffKey;
        Vector3 lastAttractor;
        Vector3 lastScale;
        Color lastBase, lastAttraction;
        float lastWidth, lastColorDistance, maximumPull;
        Bounds flatBounds;
        bool forceRebake = true, appearanceDirty = true, lastHasAttractor;
        public int MeshBakeCount { get; private set; }
        public int FieldBakeCount { get; private set; }
        public int VertexCount => mesh != null ? mesh.vertexCount : 0;
        public bool SamplingLimited { get; private set; }

        void OnEnable()
        {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            originalMesh = meshFilter.sharedMesh;
            originalMaterial = meshRenderer.sharedMaterial;
            originalProperties = new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(originalProperties);
            properties = new MaterialPropertyBlock();
            forceRebake = appearanceDirty = true;
            RefreshCache();
        }

        void OnValidate()
        {
            // OnValidate may run off the main thread. Resource work stays in Update.
            gridSize = new Vector2(Mathf.Clamp(gridSize.x, 2, 256), Mathf.Clamp(gridSize.y, 2, 256));
            gridSpacing = Mathf.Clamp(gridSpacing, .25f, 16f);
            segmentsPerCell = Mathf.Clamp(segmentsPerCell, 2, 16);
            attractionRadius = Mathf.Max(.01f, attractionRadius);
            attractionStrength = Mathf.Max(0, attractionStrength);
            springStrength = Mathf.Max(.01f, springStrength);
            innerFraction = Mathf.Clamp(innerFraction, 0, .9f);
            influenceCap = Mathf.Clamp(influenceCap, .01f, 1);
            crowdStiffness = Mathf.Max(0, crowdStiffness);
            colorDisplacement = Mathf.Max(.01f, colorDisplacement);
            appearanceDirty = true;
        }

        void Update() => RefreshCache();

        [ContextMenu("Rebake Grid")]
        public void Rebake() { forceRebake = true; }

        /// <summary>Refresh on the main thread; unchanged frames do no baking or property uploads.</summary>
        public void RefreshCache()
        {
            if (!isActiveAndEnabled || meshRenderer == null || lineShader == null) return;
            if (material == null || material.shader != lineShader)
            {
                Release(material);
                material = new Material(lineShader) { name = "Attract cached grid (generated)", hideFlags = Transient };
                meshRenderer.sharedMaterial = material;
                appearanceDirty = true;
            }
            var layout = new Vector4(Mathf.Clamp(gridSize.x, 2, 256), Mathf.Clamp(gridSize.y, 2, 256),
                Mathf.Clamp(gridSpacing, .25f, 16), Mathf.Clamp(segmentsPerCell, 2, 16));
            var shape = new Vector4(Mathf.Max(.01f, attractionRadius), Mathf.Max(0, attractionStrength),
                Mathf.Max(.01f, springStrength), Mathf.Clamp(innerFraction, 0, .9f));
            var falloff = new Vector4(Mathf.Clamp(influenceCap, .01f, 1), Mathf.Max(0, crowdStiffness), 0, 0);
            if (mesh == null || forceRebake || layout != layoutKey)
            {
                BuildMesh(layout);
                layoutKey = layout;
            }
            if (field == null || forceRebake || shape != shapeKey || falloff != falloffKey)
            {
                BakeField(shape, falloff);
                shapeKey = shape;
                falloffKey = falloff;
                appearanceDirty = true;
            }
            forceRebake = false;
            if (lastScale != transform.lossyScale || appearanceDirty)
            {
                lastScale = transform.lossyScale;
                float scale = Mathf.Max(.0001f, Mathf.Min(Mathf.Abs(lastScale.x), Mathf.Abs(lastScale.y), Mathf.Abs(lastScale.z)));
                var bounds = flatBounds;
                bounds.Expand(2f * maximumPull / scale + 1f);
                mesh.bounds = bounds;
            }
            Vector3 center = attractor != null ? attractor.position : Vector3.zero;
            // Only uniforms change if the author moves the attractor. Rotation/tilt use the
            // ordinary object matrix; they never invalidate the mesh or radial lookup.
            if (appearanceDirty || (attractor != null) != lastHasAttractor || center != lastAttractor || lastBase != baseColor ||
                lastAttraction != attractionColor || lastWidth != lineWidth || lastColorDistance != colorDisplacement)
            {
                properties.SetTexture(FieldId, field);
                properties.SetVector(FieldUvId, new Vector4((FieldSamples - 1f) / FieldSamples, .5f / FieldSamples, 0, 0));
                properties.SetVector(AttractorId, new Vector4(center.x, center.y, center.z,
                    attractor != null ? 1f / shape.x : 0f));
                properties.SetFloat(WidthId, Mathf.Clamp(lineWidth, .5f, 8));
                properties.SetColor(BaseColorId, baseColor);
                properties.SetColor(AttractionColorId, attractionColor);
                properties.SetFloat(ColorDistanceId, Mathf.Max(.01f, colorDisplacement));
                meshRenderer.SetPropertyBlock(properties);
                lastAttractor = center; lastHasAttractor = attractor != null; lastBase = baseColor; lastAttraction = attractionColor;
                lastWidth = lineWidth; lastColorDistance = colorDisplacement;
                appearanceDirty = false;
            }
        }

        void BakeField(Vector4 shape, Vector4 falloff)
        {
            if (field == null)
                field = new Texture2D(FieldSamples, 1, TextureFormat.RFloat, false, true) {
                    name = "Attract settled radial pull", hideFlags = Transient,
                    wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
                };
            var pulls = new float[FieldSamples];
            maximumPull = 0;
            for (int i = 0; i < pulls.Length; i++)
            {
                pulls[i] = SettledPull(shape.x * i / (FieldSamples - 1f), shape.x, shape.y,
                    shape.z, shape.w, falloff.x, falloff.y);
                maximumPull = Mathf.Max(maximumPull, pulls[i]);
            }
            field.SetPixelData(pulls, 0);
            field.Apply(false, false);
            FieldBakeCount++;
        }

        /// <summary>Static equilibrium of the previous smooth radial force and spring.</summary>
        public static float SettledPull(float distance, float radius, float strength,
            float spring, float inner, float cap, float crowd)
        {
            if (distance <= 0 || radius <= 0 || distance >= radius || strength <= 0) return 0;
            float low = 0, high = distance * .95f;
            // Solve spring * (1 + crowd * weight) * pull = strength * weight.
            // The center clamp avoids a singularity if a future layout intersects the sphere center.
            for (int i = 0; i < 24; i++)
            {
                float pull = (low + high) * .5f;
                float t = Mathf.Clamp01(((distance - pull) / radius - inner) / Mathf.Max(.0001f, 1f - inner));
                float weight = Mathf.Min(cap, 1f - t * t * (3f - 2f * t));
                float residual = Mathf.Max(.01f, spring) * (1f + Mathf.Max(0, crowd) * weight) * pull - strength * weight;
                if (residual > 0) high = pull; else low = pull;
            }
            return (low + high) * .5f;
        }

        void BuildMesh(Vector4 layout)
        {
            int halfX = Mathf.CeilToInt(layout.x * .5f / layout.z);
            int halfY = Mathf.CeilToInt(layout.y * .5f / layout.z);
            int linesX = halfX * 2 + 1, linesY = halfY * 2 + 1;
            int stepsX = Mathf.Max(2, Mathf.CeilToInt(layout.x / layout.z * layout.w));
            int stepsY = Mathf.Max(2, Mathf.CeilToInt(layout.y / layout.z * layout.w));
            double wanted = 2.0 * (linesX * (stepsY + 1.0) + linesY * (stepsX + 1.0));
            SamplingLimited = wanted > VertexBudget;
            if (SamplingLimited)
            {
                double factor = (VertexBudget - 2.0 * (linesX + linesY)) / (wanted - 2.0 * (linesX + linesY));
                stepsX = Mathf.Max(2, (int)(stepsX * factor));
                stepsY = Mathf.Max(2, (int)(stepsY * factor));
            }
            int capacity = 2 * (linesX * (stepsY + 1) + linesY * (stepsX + 1));
            var vertices = new List<Vector3>(capacity);
            var sides = new List<Vector2>(capacity);
            var steps = new List<Vector2>(capacity);
            var triangles = new List<int>(capacity * 3);
            for (int x = -halfX; x <= halfX; x++)
                AddLine(new Vector2(x * layout.z, -layout.y * .5f), new Vector2(0, layout.y / stepsY), stepsY,
                    vertices, sides, steps, triangles);
            for (int y = -halfY; y <= halfY; y++)
                AddLine(new Vector2(-layout.x * .5f, y * layout.z), new Vector2(layout.x / stepsX, 0), stepsX,
                    vertices, sides, steps, triangles);
            if (mesh == null) mesh = new Mesh { name = "Attract cached grid", hideFlags = Transient, indexFormat = IndexFormat.UInt32 };
            mesh.Clear();
            mesh.SetVertices(vertices); mesh.SetUVs(0, sides); mesh.SetUVs(1, steps);
            mesh.SetTriangles(triangles, 0);
            flatBounds = mesh.bounds;
            meshFilter.sharedMesh = mesh;
            appearanceDirty = true;
            MeshBakeCount++;
        }

        static void AddLine(Vector2 start, Vector2 step, int count, List<Vector3> vertices,
            List<Vector2> sides, List<Vector2> steps, List<int> triangles)
        {
            int first = vertices.Count;
            for (int i = 0; i <= count; i++)
            {
                Vector3 p = start + step * i;
                vertices.Add(p); vertices.Add(p);
                sides.Add(new Vector2(-1, 0)); sides.Add(new Vector2(1, 0));
                steps.Add(step); steps.Add(step);
                if (i == 0) continue;
                int v = first + i * 2;
                triangles.Add(v - 2); triangles.Add(v); triangles.Add(v - 1);
                triangles.Add(v - 1); triangles.Add(v); triangles.Add(v + 1);
            }
        }

        void OnDisable()
        {
            if (meshFilter != null && meshFilter.sharedMesh == mesh) meshFilter.sharedMesh = originalMesh;
            if (meshRenderer != null && meshRenderer.sharedMaterial == material)
            {
                meshRenderer.sharedMaterial = originalMaterial;
                meshRenderer.SetPropertyBlock(originalProperties);
            }
            Release(mesh); Release(material); Release(field);
            mesh = null; material = null; field = null;
        }

        static void Release(Object resource)
        {
            if (resource == null) return;
            if (Application.isPlaying) Destroy(resource); else DestroyImmediate(resource);
        }
    }
}
