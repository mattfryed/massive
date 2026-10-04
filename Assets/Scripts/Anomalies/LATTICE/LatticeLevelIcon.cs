using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.Lattice
{
    /// <summary>Presentation-only volumetric lattice. Owns no gameplay grid or physics.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed partial class LatticeLevelIcon : MonoBehaviour
    {
        [Header("Grid cube")]
        [Tooltip("Cells per axis, including the interior. Ten cells means eleven intersection points per axis.")]
        [Range(2, 14)] public int cellsPerAxis = 4;
        [Range(4, 20)] public int strandSegments = 12;
        [Header("Disruption volume")]
        public LatticeDisruptionField.FieldMode mode;
        [Range(.03f, 1f)] public float noiseFrequency = .19f;
        [Range(0f, 1f)] public float threshold = .51f;
        public Vector3 drift = new Vector3(.09f, .035f, .055f);
        [Min(0)] public float evolutionSpeed = .075f;
        public int noiseSeed = 173;
        [Header("Loose threads")]
        public bool holdTipsAtBoundary = true;
        [Min(.05f)] public float retractSeconds = .7f;
        [Min(.05f)] public float reconnectSeconds = .95f;
        [Min(0)] public float looseSeconds = .18f;
        [Range(0, .5f)] public float threadLooseness = .13f;
        [Min(0)] public float breezeSpeed = 17f;
        [Range(0, 1)] public float strandVariation = 1f;
        public int strandMotionSeed = 181262287;
        [Header("Appearance (pixels at 1080p)")]
        [Range(.3f, 3f)] public float lineWidthPixels = 1f;
        [Range(1, 8)] public float dotDiameterPixels = 3f;
        [Range(0, 1)] public float lineOpacity = .42f;
        [Range(0, 1)] public float dotOpacity = .9f;
        [Header("RGB dots and ghost trails")]
        public bool rgbDotJitter = true;
        [Range(0, 12)] public float dotJitterRadius = 3.2f;
        [Min(0)] public float dotJitterSpeed = 12f;
        [Range(.1f, 4f)] public float dotJitterDepth = 2f;
        public int dotJitterSeed = 9271;
        public bool dotGhostTrails = true;
        [Range(.02f, .6f)] public float dotTrailDuration = .6f;
        [Range(0, 1)] public float dotTrailOpacity = .75f;
        [Header("Slow tumble")]
        public bool rotateIcon = true;
        [Tooltip("Maximum angular speed in degrees per second, independent of menu time scale.")]
        [Range(0, 30)] public float rotationSpeed = 8f;
        [Tooltip("Approximate seconds between changes in the drifting rotation direction.")]
        [Min(1)] public float rotationDirectionSeconds = 8f;
        public int rotationSeed = 4127;
        [Header("Preview")]
        public bool animateInEditor = true;
        [Min(0)] public float previewTime = 12f;

        public int NodeCount => positions == null ? 0 : positions.Length;
        public int EdgeCount => edges == null ? 0 : edges.Length;
        public int DisconnectedEdgeCount { get; private set; }
        public int TransitioningEdgeCount { get; private set; }
        public float FieldTime => clock;
        public Quaternion VisualRotation => visualRotation;
        public bool IsReady => mesh != null && edgeBuffer != null && nodeBuffer != null;
        struct Edge { public int a, b, ends; public float split, delay; public bool cut; public Boundary boundary; }
        struct Boundary { public bool cut, a, b; public float fromA, fromB, split; }
        Edge[] edges;
        Vector3[] positions;
        Vector4[] states;
        Vector2[] nodes;
        int[] degrees;
        Mesh mesh, previousMesh;
        MeshFilter filter;
        MeshRenderer meshRenderer;
        MaterialPropertyBlock block, previousBlock;
        ComputeBuffer edgeBuffer, nodeBuffer;
        int builtCells, builtSegments;
        float clock, nextSample;
        double editorTick;
        bool dirty = true;
        Quaternion visualRotation = Quaternion.identity;
        static readonly int RotationId = Shader.PropertyToID("_LatticeIconRotation");
        static readonly int EdgesId = Shader.PropertyToID("_LatticeEdges"), NodesId = Shader.PropertyToID("_LatticeIconNodes"),
            LayoutId = Shader.PropertyToID("_LatticeLayout"), StyleId = Shader.PropertyToID("_LatticeStyle"),
            FlutterId = Shader.PropertyToID("_LatticeFlutter"), VariationId = Shader.PropertyToID("_LatticeStrandVariation"),
            MotionSeedId = Shader.PropertyToID("_LatticeMotionSeed"), JitterId = Shader.PropertyToID("_LatticeDotJitter"),
            DotSeedId = Shader.PropertyToID("_LatticeDotSeed"), TrailId = Shader.PropertyToID("_LatticeDotTrail");

        void OnEnable() { clock = previewTime; visualRotation = Quaternion.identity; editorTick = Time.realtimeSinceStartupAsDouble; dirty = true; }
        void OnValidate() { dirty = true; }
        void OnDisable() { Release(); }
        void OnDestroy() { Release(); }
        void Update()
        {
            float dt;
            if (Application.IsPlaying(gameObject)) dt = Time.unscaledDeltaTime;
            else
            {
                double now = Time.realtimeSinceStartupAsDouble;
                dt = animateInEditor ? Mathf.Clamp((float)(now - editorTick), 0, .05f) : 0;
                editorTick = now;
                if (!animateInEditor && clock != previewTime) { clock = previewTime; nextSample = -1; }
            }
            Advance(dt);
        }

        public void Rebuild()
        {
            Release();
            if (!isActiveAndEnabled || !gameObject.scene.IsValid()) return;
            builtCells = Mathf.Clamp(cellsPerAxis, 2, 14); builtSegments = Mathf.Clamp(strandSegments, 4, 20);
            filter = GetComponent<MeshFilter>(); meshRenderer = GetComponent<MeshRenderer>();
            previousMesh = filter.sharedMesh;
            previousBlock = new MaterialPropertyBlock(); meshRenderer.GetPropertyBlock(previousBlock);
            block = new MaterialPropertyBlock();
            int n = builtCells + 1;
            positions = new Vector3[n * n * n]; nodes = new Vector2[positions.Length]; degrees = new int[positions.Length];
            var list = new List<Edge>(3 * builtCells * n * n);
            for (int z = 0; z < n; z++) for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                int i = x + n * (y + n * z);
                positions[i] = new Vector3(x, y, z) - Vector3.one * (builtCells * .5f);
                if (x < builtCells) Add(i, i + 1);
                if (y < builtCells) Add(i, i + n);
                if (z < builtCells) Add(i, i + n * n);
            }
            edges = list.ToArray(); states = new Vector4[edges.Length * 2];
            for (int i = 0; i < states.Length; i++) states[i].x = .5f;
            edgeBuffer = new ComputeBuffer(states.Length, 16); nodeBuffer = new ComputeBuffer(nodes.Length, 8);
            BuildMesh(); filter.sharedMesh = mesh; nextSample = -1; dirty = false;
            void Add(int a, int b) { list.Add(new Edge { a = a, b = b, split = .5f }); degrees[a]++; degrees[b]++; }
        }

        public void Advance(float deltaTime)
        {
            if (!isActiveAndEnabled) return;
            if (!IsReady || builtCells != Mathf.Clamp(cellsPerAxis, 2, 14) || builtSegments != Mathf.Clamp(strandSegments, 4, 20)) Rebuild();
            if (!IsReady) return;
            float dt = Mathf.Clamp(deltaTime, 0, .1f); clock += dt;
            AdvanceRotation(dt);
            if (dirty || clock >= nextSample)
            {
                RefreshDepth();
                for (int i = 0; i < edges.Length; i++) edges[i].boundary = Connection(edges[i].a, edges[i].b);
                nextSample = clock + .05f; dirty = false;
            }
            DisconnectedEdgeCount = TransitioningEdgeCount = 0;
            for (int i = 0; i < nodes.Length; i++) nodes[i].x = 0;
            for (int i = 0; i < edges.Length; i++)
            {
                Edge e = edges[i]; Boundary boundary = e.boundary;
                int ends = (boundary.a ? 1 : 0) | (boundary.b ? 2 : 0);
                Vector4 a = states[i * 2], b = states[i * 2 + 1];
                if (boundary.cut && (!e.cut || ends != e.ends))
                {
                    float split = ends == 1 ? 0 : ends == 2 ? 1 : ends == 0 ? boundary.split : e.split;
                    if (a.x + b.x >= .999f) { a.x = split; b.x = 1 - split; }
                    else { if (ends == 1) a.x = 0; if (ends == 2) b.x = 0; }
                    e.split = split;
                }
                if (boundary.cut != e.cut) { e.cut = boundary.cut; e.delay = e.cut ? Mathf.Max(0, looseSeconds) : 0; }
                e.ends = ends; e.delay = Mathf.Max(0, e.delay - dt);
                float ta = e.cut ? (holdTipsAtBoundary ? boundary.fromA : 0) : e.split;
                float tb = e.cut ? (holdTipsAtBoundary ? boundary.fromB : 0) : 1 - e.split;
                if (holdTipsAtBoundary && e.cut) { a.x = ta; b.x = tb; }
                else if (e.delay <= 0)
                {
                    float step = dt / Mathf.Max(.05f, e.cut ? retractSeconds : reconnectSeconds);
                    a.x = Mathf.MoveTowards(a.x, ta, step); b.x = Mathf.MoveTowards(b.x, tb, step);
                }
                float extension = Mathf.Clamp01(a.x + b.x);
                float loose = e.cut && (e.delay > 0 || holdTipsAtBoundary) ? 1 : 4 * extension * (1 - extension);
                a.y = Mathf.MoveTowards(a.y, loose, dt * 8); b.y = a.y;
                a.w = b.w = e.cut ? 1 : 0;
                states[i * 2] = a; states[i * 2 + 1] = b; edges[i] = e;
                if (e.cut) DisconnectedEdgeCount++;
                if (Mathf.Abs(a.x - ta) + Mathf.Abs(b.x - tb) > .001f || e.delay > 0) TransitioningEdgeCount++;
                nodes[e.a].x += 1 - extension; nodes[e.b].x += 1 - extension;
            }
            for (int i = 0; i < nodes.Length; i++)
                nodes[i].x = inside[i] ? 1 : nodes[i].x / Mathf.Max(1, degrees[i]);
            edgeBuffer.SetData(states); nodeBuffer.SetData(nodes);
            ApplyProperties();
        }

        public void GetNode(int index, out Vector3 position, out float disruption, out float depth)
        { position = positions[index]; disruption = nodes[index].x; depth = nodes[index].y; }
        public void GetEdge(int index, out Vector3 a, out Vector3 b, out Vector4 fromA, out Vector4 fromB)
        { var e = edges[index]; a = positions[e.a]; b = positions[e.b]; fromA = states[index * 2]; fromB = states[index * 2 + 1]; }

        void ApplyProperties()
        {
            // Render-only rotation keeps authored prefab transforms and carousel placement intact.
            block.SetMatrix(RotationId, Matrix4x4.Rotate(visualRotation));
            block.SetBuffer(EdgesId, edgeBuffer); block.SetBuffer(NodesId, nodeBuffer);
            block.SetVector(LayoutId, new Vector4(0, 0, builtCells, clock));
            block.SetVector(StyleId, new Vector4(lineWidthPixels, dotDiameterPixels, lineOpacity, dotOpacity));
            block.SetVector(FlutterId, new Vector4(threadLooseness, breezeSpeed, holdTipsAtBoundary ? 1 : 0, 0));
            block.SetFloat(VariationId, strandVariation); block.SetInt(MotionSeedId, strandMotionSeed);
            block.SetVector(JitterId, new Vector4(rgbDotJitter ? dotJitterRadius : 0, dotJitterSpeed, dotJitterDepth, 0));
            block.SetInt(DotSeedId, dotJitterSeed);
            block.SetVector(TrailId, new Vector4(dotTrailDuration, dotGhostTrails && rgbDotJitter ? dotTrailOpacity : 0, 0, 0));
            meshRenderer.SetPropertyBlock(block);
        }

        void AdvanceRotation(float dt)
        {
            if (!rotateIcon || dt <= 0 || rotationSpeed <= 0) return;
            float t = clock / Mathf.Max(1, rotationDirectionSeconds);
            float seed = (rotationSeed & 65535) * .0137f;
            Vector3 velocity = new Vector3(
                Mathf.PerlinNoise(t, seed + 11.3f),
                Mathf.PerlinNoise(t + 37.1f, seed + 71.7f),
                Mathf.PerlinNoise(t + 93.5f, seed + 139.2f)) * 2 - Vector3.one;
            float strength = velocity.magnitude;
            if (strength > .0001f)
                visualRotation = (Quaternion.AngleAxis(rotationSpeed * Mathf.Min(1, strength) * dt,
                    velocity / strength) * visualRotation).normalized;
        }

        void BuildMesh()
        {
            var vertices = new List<Vector3>(); var directions = new List<Vector3>();
            var data = new List<Vector4>(); var indices = new List<int>();
            for (int i = 0; i < edges.Length; i++) for (int end = 0; end < 2; end++)
            {
                Edge e = edges[i]; Vector3 a = positions[end == 0 ? e.a : e.b], b = positions[end == 0 ? e.b : e.a];
                int start = vertices.Count;
                for (int j = 0; j <= builtSegments; j++) for (int side = 0; side < 2; side++)
                { vertices.Add(a); directions.Add(b - a); data.Add(new Vector4(j / (float)builtSegments, side * 2 - 1, i * 2 + end, 0)); }
                for (int j = 0; j < builtSegments; j++) Quad(start + j * 2, start + j * 2 + 2);
            }
            for (int i = 0; i < nodes.Length; i++)
            {
                int start = vertices.Count;
                for (int y = 0; y < 2; y++) for (int x = 0; x < 2; x++)
                { vertices.Add(positions[i]); directions.Add(Vector3.zero); data.Add(new Vector4(x * 2 - 1, y * 2 - 1, i, 1)); }
                Quad(start, start + 2);
            }
            mesh = new Mesh { name = "LATTICE icon volume (derived)", hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetUVs(0, directions); mesh.SetUVs(1, data); mesh.SetTriangles(indices, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * (builtCells * Mathf.Sqrt(3) + 4));
            void Quad(int a, int b) { indices.Add(a); indices.Add(b); indices.Add(a + 1); indices.Add(a + 1); indices.Add(b); indices.Add(b + 1); }
        }

        void Release()
        {
            if (filter && filter.sharedMesh == mesh) filter.sharedMesh = previousMesh;
            if (meshRenderer && previousBlock != null) meshRenderer.SetPropertyBlock(previousBlock);
            edgeBuffer?.Release(); nodeBuffer?.Release(); edgeBuffer = nodeBuffer = null;
            if (mesh) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
            mesh = previousMesh = null; previousBlock = null; positions = null; edges = null; states = null; nodes = null; inside = null;
            dirty = true;
        }
    }
}
