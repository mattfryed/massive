using System.Collections.Generic;
using Shapes;
using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.Enemies
{
    /// <summary>Face tracing, rigid-panel breakup and bounded core/fueling metaballs.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class CarrierVisuals : ImmediateModeShapeDrawer
    {
        public Transform assembly;
        public MeshFilter shell;
        public Renderer core;
        [Min(.001f)] public float outlineWidth = .012f;
        [Header("Arrival and motion")]
        [Min(.05f)] public float spawnSeconds = 1.2f;
        [Range(0f, .6f)] public float facetTimingScatter = .35f;
        public float spinDegreesPerSecond = 8f;
        [Header("Dyson-style face explosion")]
        [Min(0f)] public float shatterDistance = 1.5f;
        [Min(0f)] public float shatterSpinDegrees = 320f;
        [Range(0f, 1f)] public float shatterRandomness = .35f;
        [Header("Core and launch fuel")]
        public Vector3 coreCenter = new Vector3(0f, .55f, 0f);
        [Min(.01f)] public float coreRadius = .08f;
        [Min(0f)] public float coreOrbit = .09f;
        [Range(1, 5)] public int fuelBallsPerLaunch = 3;
        [Min(.05f)] public float fuelTravelSeconds = .38f;
        [Min(0f)] public float fuelStaggerSeconds = .055f;
        [Min(.005f)] public float fuelRadius = .05f;

        public float SpinAngle { get; private set; }
        public float DeathProgress { get; private set; }
        public int ActiveFuelCount { get; private set; }
        public int TotalFuelBursts { get; private set; }
        public float FacetProgress(int index) => faceProgress[Mathf.Clamp(index, 0, 25)];
        public Vector3 FuelPosition(int index) => Frame.TransformPoint(fuelPositions[Mathf.Clamp(index, 0, FuelCapacity - 1)]);
        public float FaceDisplacement(int index) => faceOffsets[Mathf.Clamp(index, 0, 25)].magnitude;

        private const int FaceCount = 26, FuelCapacity = 36;
        private Mesh mesh;
        private Vector3[] hullVertices;
        private int[][] faces;
        private readonly Vector3[] corners = new Vector3[FaceCount * 4], fillVertices = new Vector3[129];
        private readonly Vector3[] centers = new Vector3[FaceCount], directions = new Vector3[FaceCount], axes = new Vector3[FaceCount], faceOffsets = new Vector3[FaceCount];
        private readonly float[] faceStart = new float[FaceCount], faceDuration = new float[FaceCount], faceProgress = new float[FaceCount], deathStartProgress = new float[FaceCount];
        private readonly int[] faceCorner = new int[FaceCount];
        private readonly List<Vector2Int> edges = new(48);
        private readonly Vector4[] balls = new Vector4[48];
        private readonly float[] fuelAges = new float[FuelCapacity], fuelSizes = new float[FuelCapacity];
        private readonly Vector3[] fuelSources = new Vector3[FuelCapacity], fuelPositions = new Vector3[FuelCapacity];
        private readonly EnemyBase[] fuelTargets = new EnemyBase[FuelCapacity];
        private MaterialPropertyBlock properties;
        private EnemyBase enemy;
        private CarrierController carrier;
        private System.Random random;
        private Quaternion baseRotation;
        private float age, deathAge = -1f, corePulse;
        private int fuelIndex;
        private Transform Frame => assembly ? assembly : transform;
        private static readonly int BallCountId = Shader.PropertyToID("_BallCount"), BallsId = Shader.PropertyToID("_Balls"), SmoothKId = Shader.PropertyToID("_SmoothK");

        public override void OnEnable()
        {
            enemy = GetComponent<EnemyBase>(); carrier = GetComponent<CarrierController>();
            if (enemy) enemy.Died += OnDeath;
            if (carrier) carrier.DroneLaunched += OnLaunch;
            properties = new MaterialPropertyBlock(); random = new System.Random(GetInstanceID());
            age = SpinAngle = corePulse = DeathProgress = 0f; deathAge = -1f;
            fuelIndex = ActiveFuelCount = TotalFuelBursts = 0;
            baseRotation = assembly ? assembly.localRotation : Quaternion.identity;
            for (int i = 0; i < FuelCapacity; i++) { fuelAges[i] = float.PositiveInfinity; fuelTargets[i] = null; }
            if (core) core.enabled = !Application.isPlaying;
            RebuildGeometry();
            useCullingMasks = true; base.OnEnable();
        }
        public override void OnDisable()
        {
            base.OnDisable();
            if (enemy) enemy.Died -= OnDeath;
            if (carrier) carrier.DroneLaunched -= OnLaunch;
            if (Application.isPlaying && assembly) assembly.localRotation = baseRotation;
            if (mesh)
            {
                if (shell && shell.sharedMesh == mesh) shell.sharedMesh = null;
                if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
                mesh = null;
            }
        }
        private float Sample(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
        private Vector3 RandomDirection() => new Vector3(Sample(-1f, 1f), Sample(-1f, 1f), Sample(-1f, 1f)).normalized;

        public void RebuildGeometry()
        {
            if (!shell) return;
            if (random == null) random = new System.Random(GetInstanceID());
            if (!mesh)
            {
                mesh = new Mesh { name = "Carrier rhombicuboctahedron (generated)", hideFlags = HideFlags.DontSave };
                mesh.MarkDynamic();
            }
            CarrierHullGeometry.Create(out hullVertices, out faces);
            var uniqueEdges = new HashSet<Vector2Int>(); edges.Clear();
            for (int f = 0; f < faces.Length; f++)
            {
                var face = faces[f]; Vector3 center = Vector3.zero;
                foreach (int vertex in face) center += hullVertices[vertex];
                centers[f] = center / face.Length;
                directions[f] = Vector3.Lerp(centers[f].normalized, RandomDirection(), shatterRandomness).normalized * Sample(.85f, 1.15f);
                axes[f] = RandomDirection();
                faceStart[f] = Sample(0f, 1f); faceDuration[f] = Sample(.72f, 1f); faceCorner[f] = random.Next(face.Length);
                for (int i = 0; i < face.Length; i++)
                {
                    int a = face[i], b = face[(i + 1) % face.Length];
                    var edge = new Vector2Int(Mathf.Min(a, b), Mathf.Max(a, b));
                    if (uniqueEdges.Add(edge)) edges.Add(edge);
                }
            }
            mesh.Clear(); mesh.vertices = fillVertices;
            var indices = new int[fillVertices.Length]; for (int i = 0; i < indices.Length; i++) indices[i] = i;
            mesh.triangles = indices; shell.sharedMesh = mesh;
            UpdatePanels(Application.isPlaying ? 0f : 1f);
        }
        private float PartTime(float progress, int face)
        {
            float start = faceStart[face] * facetTimingScatter;
            return Mathf.Clamp01((progress - start) / Mathf.Max(.01f, (1f - start) * faceDuration[face]));
        }
        private void OnDeath(EnemyBase source, EnemyDamageSource cause)
        {
            deathAge = 0f;
            for (int i = 0; i < FaceCount; i++) deathStartProgress[i] = faceProgress[i];
            for (int i = 0; i < FuelCapacity; i++) { fuelTargets[i] = null; fuelAges[i] = float.PositiveInfinity; }
            ActiveFuelCount = 0;
        }
        private void OnLaunch(int slot, EnemyBase drone)
        {
            TotalFuelBursts++; corePulse = 1f;
            for (int n = 0; n < Mathf.Clamp(fuelBallsPerLaunch, 1, 5); n++)
            {
                int i = fuelIndex++ % FuelCapacity;
                fuelSources[i] = Frame.TransformPoint(coreCenter);
                fuelTargets[i] = drone; fuelAges[i] = -n * Mathf.Max(0f, fuelStaggerSeconds);
                fuelSizes[i] = fuelRadius * Sample(.85f, 1.15f);
            }
        }
        private void LateUpdate()
        {
            if (!mesh) RebuildGeometry();
            if (!mesh || !core) return;
            if (Application.isPlaying && enemy && enemy.IsPaused && !enemy.IsDead) return;
            float dt = Application.isPlaying ? Time.deltaTime : 0f; age += dt;
            if (deathAge >= 0f) deathAge += dt;
            else if (Application.isPlaying && assembly)
            {
                SpinAngle = Mathf.Repeat(SpinAngle + spinDegreesPerSecond * dt, 360f);
                assembly.localRotation = baseRotation * Quaternion.AngleAxis(SpinAngle, Vector3.up);
            }
            DeathProgress = deathAge < 0f ? 0f : Mathf.Clamp01(deathAge / Mathf.Max(.01f, enemy ? enemy.despawnDelaySeconds : .8f));
            float spawn = Application.isPlaying && carrier ? carrier.RevealProgress : 1f;
            UpdatePanels(spawn); UpdateCore(dt, spawn);
        }
        private void UpdatePanels(float spawn)
        {
            if (faces == null) return;
            int vertex = 0;
            for (int f = 0; f < faces.Length; f++)
            {
                var face = faces[f]; float u = DeathProgress;
                float ease = 1f - (1f - u) * (1f - u);
                faceOffsets[f] = directions[f] * (shatterDistance * ease);
                Quaternion tumble = Quaternion.AngleAxis(shatterSpinDegrees * ease, axes[f]);
                float scale = deathAge < 0f ? 1f : Mathf.Lerp(1f, .05f, u);
                faceProgress[f] = deathAge < 0f ? PartTime(spawn, f) : deathStartProgress[f];
                for (int j = 0; j < face.Length; j++)
                    corners[f * 4 + j] = centers[f] + faceOffsets[f] + tumble * (hullVertices[face[j]] - centers[f]) * scale;
                if (CarrierHullGeometry.IsCrown(hullVertices, face)) continue;
                float fill = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.35f, 1f, faceProgress[f]));
                Vector3 anchor = corners[f * 4 + faceCorner[f]];
                for (int j = 1; j < face.Length - 1; j++)
                {
                    fillVertices[vertex++] = Vector3.Lerp(anchor, corners[f * 4], fill);
                    fillVertices[vertex++] = Vector3.Lerp(anchor, corners[f * 4 + j], fill);
                    fillVertices[vertex++] = Vector3.Lerp(anchor, corners[f * 4 + j + 1], fill);
                }
            }
            mesh.vertices = fillVertices; mesh.RecalculateBounds();
        }
        private void UpdateCore(float dt, float spawn)
        {
            float intensity = Mathf.SmoothStep(0f, 1f, spawn) * (1f - DeathProgress);
            corePulse = Mathf.MoveTowards(corePulse, 0f, dt * 4f);
            int count = 0; Bounds volume = new Bounds(coreCenter, Vector3.one * .5f);
            for (int i = 0; i < DroneVisuals.CoreBallCount; i++)
            {
                float phase = age * (2.2f + i * .19f) + i * 2.39996f;
                Vector3 p = coreCenter + (i == 0 ? Vector3.zero : new Vector3(Mathf.Cos(phase), Mathf.Sin(phase * 1.13f), Mathf.Sin(phase * .83f) * .85f) * coreOrbit);
                float r = coreRadius * (i == 0 ? .85f : .72f + .18f * Mathf.Sin(phase * 1.37f)) * intensity * (1f + corePulse * .2f);
                AddBall(p, r, ref count, ref volume);
            }
            ActiveFuelCount = 0;
            for (int i = 0; i < FuelCapacity; i++)
            {
                fuelAges[i] += dt;
                if (fuelAges[i] < 0f) continue;
                float t = fuelAges[i] / Mathf.Max(.05f, fuelTravelSeconds);
                var target = fuelTargets[i];
                if (t >= 1f || !target || target.IsDead) { fuelTargets[i] = null; continue; }
                Vector3 start = fuelSources[i], end = target.transform.position;
                // Fuel crosses the interior directly; opaque shell faces naturally occlude it.
                Vector3 world = Vector3.Lerp(start, end, t);
                fuelPositions[i] = Frame.InverseTransformPoint(world);
                float envelope = Mathf.SmoothStep(0f, 1f, t / .12f) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.82f, 1f, t)));
                AddBall(fuelPositions[i], fuelSizes[i] * envelope, ref count, ref volume);
                ActiveFuelCount++;
            }
            // Fit one shared SDF volume around the core and current packets; no per-packet objects/materials.
            float size = Mathf.Max(.8f, Mathf.Max(volume.size.x, Mathf.Max(volume.size.y, volume.size.z)) + .12f);
            core.transform.localPosition = volume.center; core.transform.localScale = Vector3.one * size;
            for (int i = 0; i < count; i++)
            {
                Vector4 ball = balls[i]; Vector3 p = (new Vector3(ball.x, ball.y, ball.z) - volume.center) / size;
                balls[i] = new Vector4(p.x, p.y, p.z, ball.w / size);
            }
            core.enabled = count > 0;
            properties.SetInt(BallCountId, count); properties.SetVectorArray(BallsId, balls);
            properties.SetFloat(SmoothKId, .0064f / size); core.SetPropertyBlock(properties);
        }
        private void AddBall(Vector3 position, float radius, ref int count, ref Bounds bounds)
        {
            if (radius < .0005f || count >= balls.Length) return;
            balls[count++] = new Vector4(position.x, position.y, position.z, radius);
            bounds.Encapsulate(position - Vector3.one * radius); bounds.Encapsulate(position + Vector3.one * radius);
        }
        public override void DrawShapes(Camera camera)
        {
            if (!mesh || !shell) return;
            using (Draw.Command(camera))
            {
                Draw.Matrix = shell.transform.localToWorldMatrix; Draw.ZTest = CompareFunction.LessEqual;
                Draw.LineGeometry = LineGeometry.Volumetric3D; Draw.ThicknessSpace = ThicknessSpace.Meters;
                Draw.Thickness = outlineWidth; Draw.Color = new Color(1f, 1f, 1f, 1f - DeathProgress); Draw.ZOffsetFactor = -1f;
                if (deathAge < 0f && (!Application.isPlaying || !carrier || carrier.IsSpawnReady))
                { foreach (var edge in edges) Draw.Line(hullVertices[edge.x], hullVertices[edge.y]); return; }
                for (int f = 0; f < faces.Length; f++)
                {
                    int length = faces[f].Length; float remaining = length * faceProgress[f];
                    for (int j = 0; j < length; j++)
                    {
                        Vector3 a = corners[f * 4 + (faceCorner[f] + j) % length], b = corners[f * 4 + (faceCorner[f] + j + 1) % length];
                        if (remaining > .0001f && (b - a).sqrMagnitude > .000001f) Draw.Line(a, Vector3.Lerp(a, b, Mathf.Clamp01(remaining)));
                        remaining -= 1f;
                    }
                }
            }
        }
    }
}
