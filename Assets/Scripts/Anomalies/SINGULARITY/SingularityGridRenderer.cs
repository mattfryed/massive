using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.Singularity
{
    /// <summary>Scene-local strip renderer. All geometry and materials are transient;
    /// the existing VectorGridGPU and its simulation are not modified or duplicated.</summary>
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(500)]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed partial class SingularityGridRenderer : MonoBehaviour
    {
        public SingularitySurface surface;
        public SingularityPlayerMotor player;
        public Shader gridShader;
        public Material materialTemplate;
        [Header("Continuous surface lattice")]
        [Min(.1f)] public float cellSpacing = 1f;
        [Range(2, 32)] public int samplesPerCell = 12;
        [Range(128, 4096)] public int surfaceSamples = 1024;
        [Range(.5f, 5f)] public float lineWidthPixels = 1.35f;
        [ColorUsage(true, true)] public Color frontColor = new Color(.88f, .96f, 1f, .8f);
        [ColorUsage(true, true)] public Color rearColor = new Color(.34f, .65f, .85f, .4f);
        [Header("Hard side borders")]
        [Tooltip("Independent left/right boundary weight, including each bend's front-facing arc up to its crest.")]
        [Range(.5f, 10f)] public float sideBorderWidthPixels = 3f;
        [ColorUsage(true, true)] public Color sideBorderColor = Color.white;
        [Tooltip("Also apply the hard-edge color and weight to the rear face and rear-facing arcs of both bends. They remain dashed either way.")]
        public bool applySideBorderStyleToRear;
        [Header("Rear-face distinction")]
        [Min(.01f)] public float rearDashLength = .25f;
        [Min(0f)] public float rearGapLength = .25f;
        [Tooltip("Keep the rear stroke and empty interval equal when adjusting dash length.")]
        public bool equalRearDashesAndGaps = true;
        public bool showPlayerAttraction = true;

        public int VertexCount => mesh != null ? mesh.vertexCount : 0;
        public int ResolvedSamplesPerCell { get; private set; }
        public bool SamplingLimited { get; private set; }
        public Texture2D SurfaceLookup => lookup;
        public int ActiveAttractorCount { get; private set; }
        public float EffectiveRearGap => equalRearDashesAndGaps ? Mathf.Max(.01f, rearDashLength) : Mathf.Max(0f, rearGapLength);

        // This fixed upper bound also appears in the shader. Registration allocates
        // only when adding an actor; presentation reuses one stable upload array.
        public const int MaximumAttractors = 16;
        private readonly List<ISingularityAttractor> attractors = new List<ISingularityAttractor>(MaximumAttractors);
        private readonly Vector4[] attractionValues = new Vector4[MaximumAttractors];

        private const int VertexBudget = 200000;
        private MeshFilter filter;
        private MeshRenderer rendererComponent;
        private Mesh mesh, priorMesh;
        private Material material, priorMaterial, builtTemplate;
        private Texture2D lookup;
        private MaterialPropertyBlock properties, priorProperties;
        private Bounds surfaceBounds;
        private bool rebuildRequested = true, ownsBindings, priorRendererEnabled, priorReceiveShadows;
        private ShadowCastingMode priorShadowCasting;
        private int seenRevision = int.MinValue, builtSamples, builtLookupSamples;
        private float builtCellSpacing;
        private SingularitySurface builtSurface;
        private Shader builtShader;

        private static readonly int LookupId = Shader.PropertyToID("_SurfaceLookup");
        private static readonly int ShapeId = Shader.PropertyToID("_SurfaceShape");
        private static readonly int MatrixId = Shader.PropertyToID("_SurfaceLocalToWorld");
        private static readonly int WidthId = Shader.PropertyToID("_LineWidthPixels");
        private static readonly int FrontId = Shader.PropertyToID("_FrontColor");
        private static readonly int RearId = Shader.PropertyToID("_RearColor");
        private static readonly int DashesId = Shader.PropertyToID("_RearDashes");
        private static readonly int BorderColorId = Shader.PropertyToID("_SideBorderColor");
        private static readonly int BorderStyleId = Shader.PropertyToID("_SideBorderStyle");
        private static readonly int CrestsId = Shader.PropertyToID("_SurfaceCrests");
        private static readonly int AttractorsId = Shader.PropertyToID("_SurfaceAttractors");
        private static readonly int AttractorCountId = Shader.PropertyToID("_SurfaceAttractorCount");

        public bool RegisterAttractor(ISingularityAttractor source)
        {
            if (source == null) return false;
            if (attractors.Contains(source)) return true;
            if (attractors.Count >= MaximumAttractors) return false;
            attractors.Add(source); return true;
        }

        public void UnregisterAttractor(ISingularityAttractor source)
        { if (source != null) attractors.Remove(source); }

        private void OnEnable() { rebuildRequested = true; }
        // RefreshPresentation compares geometry inputs. Inspector color/width/dash
        // changes only update the property block, not the lattice or lookup texture.
        private void OnDisable() { ClearRipples(); Release(); }
        private void OnDestroy() { Release(); }
        private void LateUpdate() { RefreshPresentation(); }

        /// <summary>Recreate only this renderer's derived data; never saves a scene or asset.</summary>
        public void Rebuild()
        {
            Release();
            if (!isActiveAndEnabled || !gameObject.scene.IsValid()) return;
            if (surface == null) surface = GetComponentInParent<SingularitySurface>();
            if (surface == null || surface.Width <= 0f || surface.LoopLength <= 0f) return;
            Shader shader = gridShader != null ? gridShader : materialTemplate != null
                ? materialTemplate.shader : Shader.Find("MASSIVE/Singularity/Grid");
            if (shader == null) return;
            filter = GetComponent<MeshFilter>(); rendererComponent = GetComponent<MeshRenderer>();
            properties = properties ?? new MaterialPropertyBlock();
            priorProperties = priorProperties ?? new MaterialPropertyBlock();
            rendererComponent.GetPropertyBlock(priorProperties);
            priorMesh = filter.sharedMesh; priorMaterial = rendererComponent.sharedMaterial;
            priorRendererEnabled = rendererComponent.enabled; ownsBindings = true;
            priorShadowCasting = rendererComponent.shadowCastingMode;
            priorReceiveShadows = rendererComponent.receiveShadows;

            material = materialTemplate != null ? new Material(materialTemplate) : new Material(shader);
            material.shader = shader; material.name = "Singularity grid (runtime)";
            material.hideFlags = HideFlags.HideAndDontSave;
            BuildLookup(); BuildMesh();
            filter.sharedMesh = mesh; rendererComponent.sharedMaterial = material;
            rendererComponent.shadowCastingMode = ShadowCastingMode.Off;
            rendererComponent.receiveShadows = false;
            rendererComponent.enabled = true;
            seenRevision = surface.Revision; builtSurface = surface; builtTemplate = materialTemplate;
            builtShader = gridShader; builtCellSpacing = cellSpacing; builtSamples = samplesPerCell;
            builtLookupSamples = surfaceSamples; rebuildRequested = false;
            BindPresentation();
        }

        /// <summary>Update tint, attraction, surface transform and culling without mesh churn.</summary>
        public void RefreshPresentation()
        {
            if (!isActiveAndEnabled || !gameObject.scene.IsValid()) return;
            if (rebuildRequested || mesh == null || material == null || surface != builtSurface
                || (surface != null && surface.Revision != seenRevision) || gridShader != builtShader
                || materialTemplate != builtTemplate || cellSpacing != builtCellSpacing
                || samplesPerCell != builtSamples || surfaceSamples != builtLookupSamples)
            { Rebuild(); return; }
            BindPresentation();
        }

        private void BuildLookup()
        {
            int count = Mathf.Clamp(surfaceSamples, 128, 4096);
            lookup = new Texture2D(count, 1, TextureFormat.RGBAFloat, false, true) {
                name = "Singularity periodic surface lookup", hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, anisoLevel = 0
            };
            var values = new Color[count];
            surfaceBounds = new Bounds(surface.Evaluate(0f, 0f), Vector3.zero);
            for (int i = 0; i < count; i++)
            {
                float s = i * surface.LoopLength / count;
                Vector4 frame = surface.SamplePacked(s);
                values[i] = new Color(frame.x, frame.y, frame.z, frame.w);
                surfaceBounds.Encapsulate(new Vector3(-surface.Width * .5f * frame.x, frame.y, frame.z));
                surfaceBounds.Encapsulate(new Vector3(surface.Width * .5f * frame.x, frame.y, frame.z));
            }
            // Pixel-width extrusion and floating-point interpolation need a small culling margin.
            surfaceBounds.Expand(Mathf.Max(.25f, surface.Width * .02f));
            lookup.SetPixels(values); lookup.Apply(false, true);
        }

        private void BuildMesh()
        {
            float spacing = Mathf.Max(.1f, cellSpacing);
            // Even across-cell count preserves the central longitudinal grid line.
            int acrossCells = Mathf.Clamp(Mathf.RoundToInt(surface.Width / (spacing * 2f)) * 2, 2, 128);
            List<float> rows = BuildHorizontalRows(spacing, out bool rowDensityLimited);
            int loopCells = rows.Count;
            int subdivisions = Mathf.Clamp(samplesPerCell, 2, 32);
            while (subdivisions > 1 && EstimateVertices(acrossCells, loopCells, subdivisions) > VertexBudget) subdivisions--;
            ResolvedSamplesPerCell = subdivisions;
            SamplingLimited = subdivisions != samplesPerCell || surface.Width / spacing > 128f || rowDensityLimited;
            int acrossSteps = acrossCells * subdivisions, loopSteps = loopCells * subdivisions;
            int vertexCount = EstimateVertices(acrossCells, loopCells, subdivisions);
            var vertices = new List<Vector3>(vertexCount);
            var logical = new List<Vector2>(vertexCount);
            var tangent = new List<Vector2>(vertexCount);
            var stroke = new List<Vector4>(vertexCount);
            var triangles = new List<int>(vertexCount * 3);
            float halfWidth = surface.Width * .5f;

            for (int line = 0; line <= acrossCells; line++)
            {
                float x = Mathf.Lerp(-halfWidth, halfWidth, line / (float)acrossCells);
                int start = vertices.Count;
                for (int sample = 0; sample <= loopSteps; sample++)
                {
                    // Sample the same sections as the horizontal lattice so both
                    // crests are explicit vertices, not straddled by a long quad.
                    int row = Mathf.Min(sample / subdivisions, loopCells - 1);
                    float nextRow = row + 1 < loopCells ? rows[row + 1] : surface.LoopLength;
                    float s = sample == loopSteps ? surface.LoopLength
                        : Mathf.Lerp(rows[row], nextRow, (sample % subdivisions) / (float)subdivisions);
                    AddPair(new Vector2(x, s), new Vector2(0f, surface.LoopLength / loopSteps * .5f),
                        s, 0f, line == 0 || line == acrossCells, vertices, logical, tangent, stroke);
                    if (sample > 0) AddQuad(start + (sample - 1) * 2, triangles);
                }
            }
            // Each regional list includes its first seam and excludes its last;
            // shared joins and the periodic seam therefore appear exactly once.
            for (int line = 0; line < loopCells; line++)
            {
                float s = rows[line];
                bool crest = Mathf.Abs(s - surface.TopCrestDistance) < .00001f
                    || Mathf.Abs(s - surface.BottomCrestDistance) < .00001f;
                int start = vertices.Count;
                for (int sample = 0; sample <= acrossSteps; sample++)
                {
                    float x = Mathf.Lerp(-halfWidth, halfWidth, sample / (float)acrossSteps);
                    AddPair(new Vector2(x, s), new Vector2(surface.Width / acrossSteps * .5f, 0f),
                        x + halfWidth, crest ? 2f : 1f, false, vertices, logical, tangent, stroke);
                    if (sample > 0) AddQuad(start + (sample - 1) * 2, triangles);
                }
            }
            mesh = new Mesh { name = "Singularity closed lattice (runtime)",
                hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetUVs(0, logical); mesh.SetUVs(1, tangent); mesh.SetUVs(2, stroke);
            mesh.SetTriangles(triangles, 0, false);
            UpdateBounds();
        }

        private List<float> BuildHorizontalRows(float spacing, out bool limited)
        {
            // Matching even face counts keep an exact center row on both faces.
            // The rear's shorter height then shrinks its cell spacing just as it
            // shrinks the column spacing, instead of superimposing every rear row
            // over a front row. Split the bends at their actual projected extrema
            // to guarantee one solid crest row, even off the nominal cell lattice.
            int faceCells = Mathf.Max(2, Mathf.RoundToInt(surface.FrontHeight / (spacing * 2f)) * 2);
            float turnLength = surface.RearStart - surface.TopStart;
            int turnCells = Mathf.Max(2, Mathf.RoundToInt(turnLength / spacing));
            limited = 2 * (faceCells + turnCells) > 256;
            if (limited)
            {
                float reduction = 128f / (faceCells + turnCells);
                faceCells = Mathf.Clamp(Mathf.RoundToInt(faceCells * reduction * .5f) * 2, 2, 126);
                turnCells = 128 - faceCells;
            }
            float frontTurn = surface.TopCrestDistance - surface.TopStart;
            float rearTurn = surface.RearStart - surface.TopCrestDistance;
            int frontTurnCells = Mathf.Clamp(Mathf.RoundToInt(turnCells * frontTurn / turnLength), 1, turnCells - 1);
            int rearTurnCells = turnCells - frontTurnCells;
            var rows = new List<float>(2 * (faceCells + turnCells));
            AddRows(rows, 0f, surface.FrontHeight, faceCells);
            AddRows(rows, surface.TopStart, frontTurn, frontTurnCells);
            AddRows(rows, surface.TopCrestDistance, rearTurn, rearTurnCells);
            AddRows(rows, surface.RearStart, surface.FrontHeight * surface.RearScale, faceCells);
            AddRows(rows, surface.BottomStart, rearTurn, rearTurnCells);
            AddRows(rows, surface.BottomCrestDistance, frontTurn, frontTurnCells);
            return rows;
        }

        private static void AddRows(List<float> rows, float start, float length, int count)
        {
            for (int i = 0; i < count; i++) rows.Add(start + length * (i / (float)count));
        }

        private static int EstimateVertices(int across, int loop, int subdivisions)
        { return 2 * ((across + 1) * (loop * subdivisions + 1) + loop * (across * subdivisions + 1)); }
        private static void AddPair(Vector2 point, Vector2 step, float along, float across, bool sideBorder,
            List<Vector3> vertices, List<Vector2> logical, List<Vector2> tangent, List<Vector4> stroke)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                vertices.Add(Vector3.zero); logical.Add(point); tangent.Add(step);
                stroke.Add(new Vector4(side, along, across, sideBorder ? 1f : 0f));
            }
        }
        private static void AddQuad(int a, List<int> triangles)
        {
            triangles.Add(a); triangles.Add(a + 2); triangles.Add(a + 1);
            triangles.Add(a + 1); triangles.Add(a + 2); triangles.Add(a + 3);
        }

        private void BindPresentation()
        {
            if (surface == null || material == null || lookup == null || rendererComponent == null) return;
            properties.SetTexture(LookupId, lookup);
            properties.SetVector(ShapeId, new Vector4(surface.Width, surface.LoopLength, lookup.width, 0f));
            properties.SetMatrix(MatrixId, surface.transform.localToWorldMatrix);
            properties.SetFloat(WidthId, Mathf.Clamp(lineWidthPixels, .5f, 5f));
            properties.SetColor(FrontId, frontColor); properties.SetColor(RearId, rearColor);
            properties.SetVector(DashesId, new Vector4(Mathf.Max(.01f, rearDashLength), EffectiveRearGap, 0f, 0f));
            properties.SetColor(BorderColorId, sideBorderColor);
            properties.SetVector(BorderStyleId, new Vector4(Mathf.Clamp(sideBorderWidthPixels, .5f, 10f),
                applySideBorderStyleToRear ? 1f : 0f, 0f, 0f));
            properties.SetVector(CrestsId, new Vector4(surface.TopCrestDistance, surface.BottomCrestDistance, 0f, 0f));
            int count = 0;
            for (int i = attractors.Count - 1; i >= 0; i--)
            {
                ISingularityAttractor source = attractors[i];
                // A Unity object held through an interface does not use Unity's
                // destroyed-object null operator until explicitly cast back.
                if (source == null || (source is Object && (Object)source == null))
                { attractors.RemoveAt(i); continue; }
                if (!showPlayerAttraction || !source.IsAttractionActive || source.Surface != surface) continue;
                Vector2 center = source.SmoothedAttractionPosition;
                if (!Finite(center.x) || !Finite(center.y) || !Finite(source.AttractionRadius) || !Finite(source.AttractionPull)) continue;
                if (source.AttractionRadius <= 0f || source.AttractionPull <= 0f) continue;
                attractionValues[count++] = new Vector4(center.x, surface.Wrap(center.y), source.AttractionRadius, source.AttractionPull);
            }
            if (showPlayerAttraction && player != null && player.Surface == surface && player.isActiveAndEnabled)
            {
                Vector2 center = player.SmoothedAttractionPosition;
                if (count < MaximumAttractors)
                    attractionValues[count++] = new Vector4(center.x, center.y, Mathf.Max(0f, player.AttractionRadius), Mathf.Max(0f, player.AttractionPull));
            }
            ActiveAttractorCount = count;
            properties.SetVectorArray(AttractorsId, attractionValues);
            properties.SetInt(AttractorCountId, count);
            BindRipples();
            BindAmplifierTreatment();
            BindResonanceAttraction();
            BindBlackHoleAttraction();
            rendererComponent.SetPropertyBlock(properties);
            UpdateBounds();
        }

        private void UpdateBounds()
        {
            if (mesh == null || surface == null) return;
            Matrix4x4 relative = transform.worldToLocalMatrix * surface.transform.localToWorldMatrix;
            Vector3 center = relative.MultiplyPoint3x4(surfaceBounds.center), e = surfaceBounds.extents;
            e.x += AmplifierBoundsPadding;
            Vector3 x = relative.MultiplyVector(new Vector3(e.x, 0, 0));
            Vector3 y = relative.MultiplyVector(new Vector3(0, e.y, 0));
            Vector3 z = relative.MultiplyVector(new Vector3(0, 0, e.z));
            Vector3 extent = new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y), Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
            mesh.bounds = new Bounds(center, extent * 2f);
        }

        /// <summary>CPU reference of the periodic compact shader field. Radius uses
        /// centerline arc-length and query-local width metric; this approximates surface
        /// distance away from the centerline while the rear width is tapering.</summary>
        public static Vector2 EvaluateAttraction(Vector2 logical, Vector2 center, float radius, float pull,
            float widthScale, float loopLength, float width)
        {
            if (radius <= 0f || pull <= 0f || loopLength <= 0f) return logical;
            float metric = Mathf.Max(.0001f, widthScale);
            float edgeDistance = (width * .5f - Mathf.Abs(logical.x)) * metric;
            if (edgeDistance <= 0f) return logical;
            Vector2 toward = new Vector2((center.x - logical.x) * metric,
                Mathf.Repeat(center.y - logical.y + loopLength * .5f, loopLength) - loopLength * .5f);
            float t2 = toward.sqrMagnitude / Mathf.Max(.00000001f, radius * radius);
            if (t2 >= 1f) return logical;
            float bell = 1f - t2; bell = bell * bell * bell;
            float edge = Mathf.Clamp01(edgeDistance / Mathf.Max(.0001f, Mathf.Min(radius * .25f, 1f)));
            edge = edge * edge * edge * (edge * (edge * 6f - 15f) + 10f);
            float weight = pull * bell * edge;
            toward *= weight * .85f / (.85f + weight);
            return logical + new Vector2(toward.x / metric, toward.y);
        }

        /// <summary>Order-independent multi-source reference, matching the shader.
        /// A shared soft cap prevents overlapping players multiplying displacement without bound.</summary>
        public static Vector2 EvaluateCombinedAttraction(Vector2 logical, Vector4[] sources, int count,
            float widthScale, float loopLength, float width)
        {
            if (sources == null || loopLength <= 0f) return logical;
            float metric = Mathf.Max(.0001f, widthScale);
            float edgeDistance = (width * .5f - Mathf.Abs(logical.x)) * metric;
            if (edgeDistance <= 0f) return logical;
            Vector2 weighted = Vector2.zero;
            float totalWeight = 0f;
            for (int i = 0; i < Mathf.Min(Mathf.Min(count, sources.Length), MaximumAttractors); i++)
            {
                Vector4 source = sources[i];
                if (!Finite(source.x) || !Finite(source.y) || !Finite(source.z) || !Finite(source.w) || source.z <= 0f || source.w <= 0f) continue;
                Vector2 toward = new Vector2((source.x - logical.x) * metric,
                    Mathf.Repeat(source.y - logical.y + loopLength * .5f, loopLength) - loopLength * .5f);
                float t2 = toward.sqrMagnitude / Mathf.Max(.00000001f, source.z * source.z);
                if (t2 >= 1f) continue;
                float bell = 1f - t2; bell = bell * bell * bell;
                float edge = Mathf.Clamp01(edgeDistance / Mathf.Max(.0001f, Mathf.Min(source.z * .25f, 1f)));
                edge = edge * edge * edge * (edge * (edge * 6f - 15f) + 10f);
                float weight = source.w * bell * edge;
                weighted += toward * weight; totalWeight += weight;
            }
            weighted *= .85f / (.85f + totalWeight);
            return logical + new Vector2(weighted.x / metric, weighted.y);
        }

        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }

        private void Release()
        {
            if (ownsBindings)
            {
                if (filter != null && filter.sharedMesh == mesh) filter.sharedMesh = priorMesh;
                if (rendererComponent != null && rendererComponent.sharedMaterial == material)
                {
                    rendererComponent.sharedMaterial = priorMaterial;
                    rendererComponent.enabled = priorRendererEnabled;
                    rendererComponent.shadowCastingMode = priorShadowCasting;
                    rendererComponent.receiveShadows = priorReceiveShadows;
                    rendererComponent.SetPropertyBlock(priorProperties);
                }
            }
            ownsBindings = false;
            DestroyDerived(mesh); DestroyDerived(material); DestroyDerived(lookup);
            mesh = null; material = null; lookup = null;
        }
        private static void DestroyDerived(Object value)
        { if (value == null) return; if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value); }
    }
}
