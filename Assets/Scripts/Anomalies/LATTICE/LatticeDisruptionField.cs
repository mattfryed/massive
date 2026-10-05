using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.Lattice
{
    /// <summary>Scene-owned connection state shared by rendering and locomotion.
    /// The existing grid still owns its simulation, forces and arena boundary.</summary>
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(100)]
    [RequireComponent(typeof(VectorGridGPU))]
    public sealed partial class LatticeDisruptionField : MonoBehaviour
    {
        public enum FieldMode { DriftingNoise, Connected, Disconnected }
        [Header("Disruption")]
        public FieldMode mode;
        [Range(.03f, 1f)] public float noiseFrequency = .19f;
        [Range(0f, 1f)] public float threshold = .51f;
        public Vector2 drift = new Vector2(.09f, .035f);
        [Min(0f)] public float evolutionSpeed = .075f;
        public int seed = 173;
        [Range(0f, .1f)] public float noiseHysteresis = .018f;
        [Header("Strand motion")]
        [InspectorName("Hold Tips At Boundary")]
        [Tooltip("Keep free strands at the actual noise contour instead of retracting them completely. Applies in Play Mode too.")]
        public bool keepStrandsAtNoiseBoundary;
        [Min(.05f)] public float retractSeconds = .7f;
        [Min(.05f)] public float reconnectSeconds = .95f;
        [Min(0f)] public float looseSeconds = .18f;
        [Tooltip("Amount of slack and drifting bend in each loose thread.")]
        [Range(0f, .5f)] public float flutterAmplitude = .16f;
        [Tooltip("Relative breeze speed. Zero freezes the thread shapes; the existing value 7 is a gentle drift.")]
        [Min(0f)] public float flutterFrequency = 7f;
        [Tooltip("Differences in each strand's shape, slack, response speed and independent drifting bends.")]
        [Range(0f, 1f)] public float strandVariation = 1f;
        [Tooltip("Stable random identity for thread motion only. Does not change the disruption noise or movement.")]
        public int strandMotionSeed = 4817;
        [Range(4, 24)] public int strandSegments = 12;
        [Header("Appearance")]
        public Shader strandShader;
        [Tooltip("Pixels at 1080p. Scales with taller render targets to preserve readability at 4K.")]
        [Range(.5f, 5f)] public float lineWidthPixels = 1.25f;
        [Range(1f, 12f)] public float dotDiameterPixels = 3.5f;
        [Range(0f, 1f)] public float lineOpacity = .42f;
        [Range(0f, 1f)] public float dotOpacity = .72f;
        [Header("Dot color jitter")]
        [InspectorName("RGB Dot Jitter")]
        public bool rgbDotJitter = true;
        [Tooltip("Maximum offset of each color layer in pixels at 1080p. Zero restores white dots.")]
        [Range(0f, 12f)] public float dotJitterRadius = 3f;
        [Tooltip("Random position changes per second. Zero freezes the color offsets.")]
        [Min(0f)] public float dotJitterSpeed = 12f;
        [Tooltip("Distance inside the disruption, in grid cells, where the jitter reaches full strength.")]
        [Min(.1f)] public float dotJitterDepth = 3f;
        [Tooltip("Independent random motion for every dot and RGB layer. Does not change the disruption or threads.")]
        public int dotJitterSeed = 9271;
        [Header("Dot ghost trails")]
        public bool dotGhostTrails = true;
        [Tooltip("Seconds of each RGB layer's recent jitter path retained as fading afterimages.")]
        [Range(.02f, .6f)] public float dotTrailDuration = .18f;
        [Tooltip("Visibility of the fading afterimages, relative to the current dot. Zero hides the trails.")]
        [Range(0f, 1f)] public float dotTrailOpacity = .5f;
        [Header("Traversal")]
        public bool quantizeMovement = true;
        [Range(.1f, 1f)] public float enterDisruption = .72f;
        [Range(0f, .9f)] public float exitDisruption = .42f;
        [Min(.1f)] public float stepSpeedMultiplier = 1f;
        [Header("Editor preview")]
        [Tooltip("Faint dashed noise contour in the Editor's Scene/Game views, independent of Gizmos. Never included in a player build.")]
        public bool showNoiseBoundary = true;
        public bool animateInEditor = true;
        [Min(0f)] public float previewTime;

        public VectorGridGPU Grid { get; private set; }
        public float Spacing => Grid ? Grid.ResolvedGridScale : 1f;
        public int EdgeCount => edges == null ? 0 : edges.Length;
        public int NodeCount => nodes == null ? 0 : nodes.Length;
        public int DisconnectedEdgeCount { get; private set; }
        public int TransitioningEdgeCount { get; private set; }
        public float FieldTime => clock;
        public bool IsReady => mesh && material && stateBuffer != null && Grid && Grid.isActiveAndEnabled;

        struct Edge { public Vector2 a, b; public int ia, ib, ends; public bool disconnected; public float delay, split; }
        Edge[] edges;
        Vector4[] states;
        Color[] nodes;
        int[] nodeDegrees;
        int nx, ny, builtSegments;
        Vector2 origin, builtSize;
        float builtSpacing, clock;
        double editorTick;
        Mesh mesh;
        Material material;
        Texture2D nodeTexture;
        ComputeBuffer stateBuffer;
        MeshRenderer originalRenderer;
        MaterialPropertyBlock block;
        bool ownsRenderer, previousForceOff;

        void OnEnable() { Grid = GetComponent<VectorGridGPU>(); clock = Application.isPlaying ? 0 : previewTime; editorTick = Time.realtimeSinceStartupAsDouble; }
        void OnDisable() { Release(); }
        void OnDestroy() { Release(); }
        void OnValidate()
        {
            retractSeconds = Mathf.Max(.05f, retractSeconds); reconnectSeconds = Mathf.Max(.05f, reconnectSeconds);
            strandSegments = Mathf.Clamp(strandSegments, 4, 24); noiseFrequency = Mathf.Max(.03f, noiseFrequency);
            exitDisruption = Mathf.Min(exitDisruption, enterDisruption - .05f);
            dotJitterDepth = Mathf.Max(.1f, dotJitterDepth);
            dotJitterSpeed = Mathf.Max(0, dotJitterSpeed);
        }
        void Update()
        {
            if (!EnsureResources()) return;
            float dt = 0;
            if (Application.IsPlaying(gameObject)) dt = Time.deltaTime;
            else
            {
                double now = Time.realtimeSinceStartupAsDouble;
                if (animateInEditor) dt = Mathf.Clamp((float)(now - editorTick), 0, .05f);
                else clock = previewTime;
                editorTick = now;
            }
            Advance(dt);
        }
        void LateUpdate()
        {
            if (!EnsureResources()) { RestoreRenderer(); return; }
            // Copy the grid's current GPU simulation and response layers after its LateUpdate.
            originalRenderer.GetPropertyBlock(block);
            BindResonanceAttraction();
            block.SetBuffer("_LatticeEdges", stateBuffer);
            block.SetTexture("_LatticeNodes", nodeTexture);
            block.SetVector("_LatticeLayout", new Vector4(origin.x, origin.y, Spacing, clock));
            block.SetVector("_LatticeCounts", new Vector4(nx, ny, 0, 0));
            block.SetVector("_LatticeStyle", new Vector4(lineWidthPixels, dotDiameterPixels, lineOpacity, dotOpacity));
            block.SetVector("_LatticeFlutter", new Vector4(flutterAmplitude, flutterFrequency, keepStrandsAtNoiseBoundary ? 1 : 0, 0));
            block.SetFloat("_LatticeStrandVariation", strandVariation);
            block.SetInt("_LatticeMotionSeed", strandMotionSeed);
            block.SetVector("_LatticeDotJitter", new Vector4(rgbDotJitter ? dotJitterRadius : 0, dotJitterSpeed, dotJitterDepth, 0));
            block.SetInt("_LatticeDotSeed", dotJitterSeed);
            block.SetVector("_LatticeDotTrail", new Vector4(dotTrailDuration, dotGhostTrails && rgbDotJitter ? dotTrailOpacity : 0, 0, 0));
            Graphics.DrawMesh(mesh, transform.localToWorldMatrix, material, gameObject.layer, null, 0, block,
                ShadowCastingMode.Off, false, null, LightProbeUsage.Off);
            if (!ownsRenderer) { previousForceOff = originalRenderer.forceRenderingOff; ownsRenderer = true; }
            originalRenderer.forceRenderingOff = true;
#if UNITY_EDITOR
            DrawEditorBoundary();
#endif
        }

        public float NoiseAt(Vector2 local, float time)
        {
            if (mode == FieldMode.Connected) return 0;
            if (mode == FieldMode.Disconnected) return 1;
            float z = time * evolutionSpeed;
            Vector2 p = local * noiseFrequency + drift * time * noiseFrequency + new Vector2(seed * .173f, seed * .317f);
            Vector2 warp = new Vector2(Mathf.PerlinNoise(p.x * .63f + z, p.y * .63f + 8.7f),
                Mathf.PerlinNoise(p.x * .63f + 29.3f, p.y * .63f - z)) - Vector2.one * .5f;
            p += warp * 1.1f;
            return .72f * Mathf.PerlinNoise(p.x + .23f * Mathf.Sin(z), p.y + .31f * Mathf.Cos(z * .83f))
                + .28f * Mathf.PerlinNoise(p.y * 1.83f + z + 41f, -p.x * 1.83f - z * .7f);
        }

        public void Advance(float deltaTime)
        {
            if (edges == null) return;
            float dt = Mathf.Clamp(deltaTime, 0, .1f); clock += dt;
            DisconnectedEdgeCount = TransitioningEdgeCount = 0;
            for (int i = 0; i < nodes.Length; i++) nodes[i] = Color.clear;
            for (int i = 0; i < edges.Length; i++)
            {
                Edge e = edges[i];
                var boundary = ConnectionAt(e.a, e.b, clock, keepStrandsAtNoiseBoundary ? 0 : (e.disconnected ? -noiseHysteresis : noiseHysteresis));
                bool cut = boundary.cut;
                int ends = (boundary.disruptedA ? 1 : 0) | (boundary.disruptedB ? 2 : 0);
                Vector4 a = states[i * 2], b = states[i * 2 + 1];
                if (cut && (!e.disconnected || ends != e.ends))
                {
                    // A severed node releases one whole tether from the intact end.
                    // Only a cut between two intact nodes produces two free strands.
                    float split = ends == 1 ? 0 : ends == 2 ? 1 : ends == 0 ? boundary.split : e.split;
                    if (a.x + b.x >= .999f) { a.x = split; b.x = 1 - split; }
                    else { if (ends == 1) a.x = 0; if (ends == 2) b.x = 0; }
                    e.split = split;
                }
                if (cut != e.disconnected) { e.disconnected = cut; e.delay = cut ? looseSeconds : 0; }
                e.ends = ends;
                e.delay = Mathf.Max(0, e.delay - dt);
                float targetA = cut ? (keepStrandsAtNoiseBoundary ? boundary.fromA : 0) : e.split;
                float targetB = cut ? (keepStrandsAtNoiseBoundary ? boundary.fromB : 0) : 1 - e.split;
                if (keepStrandsAtNoiseBoundary && cut) { a.x = targetA; b.x = targetB; }
                else if (e.delay <= 0)
                {
                    a.x = Mathf.MoveTowards(a.x, targetA, dt / (cut ? retractSeconds : reconnectSeconds));
                    b.x = Mathf.MoveTowards(b.x, targetB, dt / (cut ? retractSeconds : reconnectSeconds));
                }
                float extension = Mathf.Clamp01(a.x + b.x);
                float targetLoose = cut && (e.delay > 0 || keepStrandsAtNoiseBoundary) ? 1f : 4f * extension * (1f - extension);
                a.y = Mathf.MoveTowards(a.y, targetLoose, dt * 8f); b.y = a.y;
                a.z = b.z = i * 2.399963f; a.w = b.w = cut ? 1 : 0;
                states[i * 2] = a; states[i * 2 + 1] = b;
                edges[i] = e;
                bool changing = Mathf.Abs(a.x - targetA) + Mathf.Abs(b.x - targetB) > .001f || e.delay > 0;
                if (cut && !changing) DisconnectedEdgeCount++;
                if (changing) TransitioningEdgeCount++;
                float missing = 1 - extension;
                nodes[e.ia].r += missing; nodes[e.ib].r += missing;
            }
            if (rgbDotJitter) RefreshDotDepths();
            for (int i = 0; i < nodes.Length; i++)
            {
                nodes[i].r /= Mathf.Max(1, nodeDegrees[i]);
                // Green stores geometric depth for rendering only; red still owns locomotion.
                nodes[i].g = rgbDotJitter ? dotDepths[i] : 0;
                // Holding visible tethers at the contour must not restore locomotion
                // to the disrupted nodes on the other side of that contour.
                if (keepStrandsAtNoiseBoundary && NoiseAt(origin + new Vector2(i % nx, i / nx) * Spacing, clock) > threshold)
                    nodes[i].r = 1;
            }
            stateBuffer.SetData(states);
            nodeTexture.SetPixels(nodes); nodeTexture.Apply(false, false);
        }

        public void GetNode(int index, out Vector2 position, out float disruption, out float depthInCells)
        {
            position = origin + new Vector2(index % nx, index / nx) * Spacing;
            disruption = nodes[index].r; depthInCells = nodes[index].g;
        }

        public Vector2 ToLocal(Vector3 world) { Vector3 p = transform.InverseTransformPoint(world); return new Vector2(p.x, p.y); }
        public Vector3 ToWorld(Vector2 local, float worldY)
        { Vector3 p = transform.TransformPoint(new Vector3(local.x, local.y, 0)); p.y = worldY; return p; }
        public float DisruptionAt(Vector3 world)
        {
            if (nodes == null || !Grid) return 0;
            Vector2 p = ToLocal(world);
            if (Mathf.Abs(p.x) > Grid.size.x * .5f || Mathf.Abs(p.y) > Grid.size.y * .5f) return 0;
            Vector2 uv = (p - origin) / Spacing;
            int x = Mathf.Clamp(Mathf.FloorToInt(uv.x), 0, nx - 2), y = Mathf.Clamp(Mathf.FloorToInt(uv.y), 0, ny - 2);
            return Mathf.Lerp(Mathf.Lerp(nodes[x+y*nx].r, nodes[x+1+y*nx].r, Mathf.Clamp01(uv.x-x)),
                Mathf.Lerp(nodes[x+(y+1)*nx].r, nodes[x+1+(y+1)*nx].r, Mathf.Clamp01(uv.x-x)), Mathf.Clamp01(uv.y-y));
        }
        public Vector2Int NearestNode(Vector3 world)
        { Vector2 p = ToLocal(world) / Spacing; return new Vector2Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y)); }
        public Vector2Int NearestValidNode(Vector3 world, float radius)
        {
            Vector2Int node = NearestNode(world); Vector3 scale = transform.lossyScale;
            int x = Mathf.Max(0, Mathf.FloorToInt((Grid.size.x * .5f - radius / Mathf.Max(.001f, Mathf.Abs(scale.x))) / Spacing));
            int y = Mathf.Max(0, Mathf.FloorToInt((Grid.size.y * .5f - radius / Mathf.Max(.001f, Mathf.Abs(scale.y))) / Spacing));
            return new Vector2Int(Mathf.Clamp(node.x,-x,x), Mathf.Clamp(node.y,-y,y));
        }
        public Vector3 NodeWorld(Vector2Int node, float y) => ToWorld((Vector2)node * Spacing, y);
        public bool NodeFits(Vector2Int node, float radius)
        {
            Vector2 p = (Vector2)node * Spacing;
            Vector3 scale = transform.lossyScale;
            return Mathf.Abs(p.x) <= Grid.size.x * .5f - radius / Mathf.Max(.001f, Mathf.Abs(scale.x)) &&
                Mathf.Abs(p.y) <= Grid.size.y * .5f - radius / Mathf.Max(.001f, Mathf.Abs(scale.y));
        }

        bool EnsureResources()
        {
            if (!Grid) Grid = GetComponent<VectorGridGPU>();
            if (!Grid || !Grid.isActiveAndEnabled || !gameObject.scene.IsValid() || Spacing <= 0) return false;
            if (!strandShader) strandShader = Shader.Find("MASSIVE/Lattice/Strands");
            if (!strandShader || !strandShader.isSupported) return false;
            if (mesh && builtSpacing == Spacing && builtSize == Grid.size && builtSegments == strandSegments && material.shader == strandShader) return true;
            Release();
            originalRenderer = GetComponent<MeshRenderer>(); block = new MaterialPropertyBlock();
            builtSpacing = Spacing; builtSize = Grid.size; builtSegments = strandSegments;
            int hx = Mathf.FloorToInt(Grid.size.x * .5f / Spacing + .0001f), hy = Mathf.FloorToInt(Grid.size.y * .5f / Spacing + .0001f);
            nx = hx * 2 + 1; ny = hy * 2 + 1; origin = new Vector2(-hx * Spacing, -hy * Spacing);
            if (nx < 2 || ny < 2 || nx * ny > 20000) return false;
            nodes = new Color[nx * ny]; nodeDegrees = new int[nodes.Length];
            var edgeList = new List<Edge>();
            for (int y = 0; y < ny; y++) for (int x = 0; x < nx; x++)
            {
                int a = x + y * nx;
                if (x + 1 < nx) AddEdge(a, a + 1, edgeList);
                if (y + 1 < ny) AddEdge(a, a + nx, edgeList);
            }
            edges = edgeList.ToArray(); states = new Vector4[edges.Length * 2];
            for (int i = 0; i < edges.Length; i++)
            {
                Edge e = edges[i]; e.split = .5f; edges[i] = e;
                states[i * 2].x = states[i * 2 + 1].x = .5f;
            }
            stateBuffer = new ComputeBuffer(states.Length, 16);
            nodeTexture = new Texture2D(nx, ny, TextureFormat.RGBAFloat, false, true) {
                name = "LATTICE node connection state", hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            material = new Material(strandShader) { name = "LATTICE strands (transient)", hideFlags = HideFlags.HideAndDontSave };
            BuildMesh(); Advance(0); return true;
        }
        void AddEdge(int a, int b, List<Edge> list)
        {
            list.Add(new Edge { ia = a, ib = b, a = origin + new Vector2(a % nx, a / nx) * Spacing,
                b = origin + new Vector2(b % nx, b / nx) * Spacing });
            nodeDegrees[a]++; nodeDegrees[b]++;
        }
        void BuildMesh()
        {
            var positions = new List<Vector3>(); var directions = new List<Vector2>();
            var data = new List<Vector4>(); var triangles = new List<int>();
            for (int i = 0; i < edges.Length; i++) for (int end = 0; end < 2; end++)
            {
                Edge e = edges[i]; Vector2 a = end == 0 ? e.a : e.b, b = end == 0 ? e.b : e.a;
                int start = positions.Count;
                for (int j = 0; j <= strandSegments; j++) for (int side = 0; side < 2; side++)
                { positions.Add(a); directions.Add(b-a); data.Add(new Vector4(j / (float)strandSegments, side * 2 - 1, i * 2 + end, 0)); }
                for (int j = 0; j < strandSegments; j++) Quad(start + j * 2, start + j * 2 + 2, triangles);
            }
            for (int i = 0; i < nodes.Length; i++)
            {
                int start = positions.Count; Vector2 p = origin + new Vector2(i % nx, i / nx) * Spacing;
                for (int y = 0; y < 2; y++) for (int x = 0; x < 2; x++)
                { positions.Add(p); directions.Add(Vector2.zero); data.Add(new Vector4(x*2-1,y*2-1,i,1)); }
                Quad(start, start + 2, triangles);
            }
            mesh = new Mesh { name = "LATTICE strand and node mesh (transient)", hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(positions); mesh.SetUVs(0, directions); mesh.SetUVs(1, data); mesh.SetTriangles(triangles, 0);
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(Grid.size.x + 8, Grid.size.y + 8, 8));
        }
        static void Quad(int a, int b, List<int> indices)
        { indices.Add(a); indices.Add(b); indices.Add(a+1); indices.Add(a+1); indices.Add(b); indices.Add(b+1); }
        void RestoreRenderer()
        { if (ownsRenderer && originalRenderer) originalRenderer.forceRenderingOff = previousForceOff; ownsRenderer = false; }
        void Release()
        {
            ResetResonanceAttraction();
#if UNITY_EDITOR
            Dispose(boundaryMesh); Dispose(boundaryMaterial); boundaryMesh = null; boundaryMaterial = null;
#endif
            boundaryClock = float.NaN; dotDepths = null; boundaryLines.Clear();
            RestoreRenderer(); stateBuffer?.Release(); stateBuffer = null;
            Dispose(mesh); Dispose(material); Dispose(nodeTexture); mesh = null; material = null; nodeTexture = null;
            edges = null; states = null; nodes = null;
        }
        static void Dispose(Object value) { if (!value) return; if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
    }
}
