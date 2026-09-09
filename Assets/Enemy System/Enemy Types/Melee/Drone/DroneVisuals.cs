using Shapes;
using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.Enemies
{
    /// <summary>Point-traced facets and a bounded, irregular metaball engine.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class DroneVisuals : ImmediateModeShapeDrawer
    {
        [Header("Geometry — local +Z is the long forward nose")]
        public Transform shell;
        public MeshFilter noseMesh;
        public Renderer engineRenderer;
        [Min(.02f)] public float radius = .22f;
        [Min(.04f)] public float noseLength = .55f;
        [Range(.1f, 1f)] public float tailLengthRatio = .5f;
        [Min(0f)] public float rollDegreesPerSecond = 110f;
        [Range(.001f, .05f)] public float outlineWidth = .012f;
        public Color outlineColor = Color.white;
        [Header("Lifecycle")]
        [Min(.01f)] public float spawnSeconds = .45f;
        [Tooltip("Fraction of the animation available for different facet start times.")]
        [Range(0f, .6f)] public float facetTimingScatter = .28f;
        [Min(0f)] public float breakupDistance = .22f;
        [Header("Engine — shared MetaballSDF material")]
        [Tooltip("Typical radius of each small core lobe, not the whole core.")]
        [Min(.01f)] public float coreRadius = .042f;
        [Range(0f, .1f)] public float coreOrbit = .052f;
        [Range(8f, 60f)] public float exhaustPerSecond = 42f;
        [Range(.05f, .8f)] public float exhaustLifetime = .58f;
        [Min(0f)] public float exhaustSpeed = 1.1f;
        [Range(.01f, .1f)] public float exhaustRadius = .06f;
        [Range(0f, .6f)] public float exhaustVariation = .28f;
        [Range(0f, 30f)] public float exhaustSpreadDegrees = 12f;

        public float RollAngle => roll;
        public int ActiveExhaustCount { get; private set; }
        public const int CoreBallCount = 7;
        private const int ExhaustCapacity = 28;
        private readonly Vector3[] vertices = new Vector3[18], facets = new Vector3[36], ring = new Vector3[6];
        private readonly float[] faceStart = new float[12], faceDuration = new float[12], faceProgress = new float[12], deathStartProgress = new float[12];
        private readonly int[] faceCorner = new int[12];
        private readonly Vector4[] balls = new Vector4[48];
        private readonly Vector3[] exhaustPositions = new Vector3[ExhaustCapacity], exhaustVelocities = new Vector3[ExhaustCapacity], exhaustAccelerations = new Vector3[ExhaustCapacity];
        private readonly float[] exhaustAges = new float[ExhaustCapacity], exhaustSizes = new float[ExhaustCapacity], exhaustLives = new float[ExhaustCapacity];
        private Mesh mesh;
        private EnemyBase enemy;
        private Rigidbody body;
        private MaterialPropertyBlock properties;
        private System.Random random;
        private float age, deathAge = -1f, roll, emission, engineIntensity, phaseOffset, thrust;
        private int exhaustIndex;
        private static readonly int BallCountId = Shader.PropertyToID("_BallCount"), BallsId = Shader.PropertyToID("_Balls");

        public float FacetProgress(int index) => faceProgress[Mathf.Clamp(index, 0, 11)];
        private float Sample(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
        public override void OnEnable()
        {
            enemy = GetComponent<EnemyBase>(); body = GetComponent<Rigidbody>();
            if (enemy != null) enemy.Died += OnDeath;
            properties = new MaterialPropertyBlock();
            // Visual randomness never consumes the gameplay/spawn random sequence.
            random = new System.Random(GetInstanceID()); phaseOffset = Sample(0f, 20f);
            age = 0f; roll = 0f; deathAge = -1f; emission = 0f; thrust = 0f; exhaustIndex = 0;
            for (int i = 0; i < 12; i++)
            {
                faceStart[i] = Sample(0f, 1f); faceDuration[i] = Sample(.72f, 1f); faceCorner[i] = random.Next(3);
                faceProgress[i] = 0f;
            }
            for (int i = 0; i < ExhaustCapacity; i++) exhaustAges[i] = float.PositiveInfinity;
            useCullingMasks = true; base.OnEnable();
        }
        public override void OnDisable()
        {
            base.OnDisable();
            if (enemy != null) enemy.Died -= OnDeath;
            if (mesh != null)
            {
                if (noseMesh != null && noseMesh.sharedMesh == mesh) noseMesh.sharedMesh = null;
                if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
                mesh = null;
            }
        }
        private void OnDeath(EnemyBase source, EnemyDamageSource cause)
        {
            deathAge = 0f;
            for (int i = 0; i < 12; i++) deathStartProgress[i] = faceProgress[i];
        }
        private float PartTime(float time, int i)
        {
            float delay = faceStart[i] * facetTimingScatter;
            return Mathf.Clamp01((time - delay) / Mathf.Max(.01f, (1f - delay) * faceDuration[i]));
        }
        private void LateUpdate()
        {
            if (shell == null || noseMesh == null) return;
            if (Application.isPlaying && enemy != null && enemy.IsPaused && !enemy.IsDead) return;
            float dt = Application.isPlaying ? Time.deltaTime : 0f;
            age += dt;
            if (deathAge >= 0f) deathAge += dt;
            roll = Mathf.Repeat(roll + rollDegreesPerSecond * dt, 360f);
            float spawn = Application.isPlaying ? Mathf.Clamp01(age / Mathf.Max(.01f, spawnSeconds)) : 1f;
            float death = deathAge < 0f ? 0f : Mathf.Clamp01(deathAge / Mathf.Max(.01f, enemy != null ? enemy.despawnDelaySeconds : .3f));
            engineIntensity = Mathf.SmoothStep(0f, 1f, spawn) * (1f - death);
            shell.localRotation = Quaternion.AngleAxis(roll, Vector3.forward); shell.localScale = Vector3.one;
            if (mesh == null)
            {
                mesh = new Mesh { name = "Drone nose (generated)", hideFlags = HideFlags.DontSave };
                mesh.MarkDynamic(); mesh.vertices = vertices;
                int[] indices = new int[18]; for (int i = 0; i < indices.Length; i++) indices[i] = i;
                mesh.triangles = indices; noseMesh.sharedMesh = mesh;
            }
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI / 3f;
                ring[i] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            }
            for (int i = 0; i < 12; i++)
            {
                float removal = PartTime(death, i);
                float progress = deathAge < 0f ? PartTime(spawn, i) : deathStartProgress[i] * (1f - removal);
                faceProgress[i] = progress;
                int side = i % 6;
                Vector3 a = ring[side], b = ring[(side + 1) % 6];
                Vector3 c = i < 6 ? Vector3.forward * noseLength : Vector3.back * noseLength * tailLengthRatio;
                Vector3 center = (a + b + c) / 3f;
                Vector3 offset = new Vector3(center.x, center.y, i < 6 ? .08f : -.08f).normalized * (removal * breakupDistance);
                facets[i * 3] = a + offset; facets[i * 3 + 1] = b + offset; facets[i * 3 + 2] = c + offset;
                if (i >= 6) continue;
                // Fill grows from the same starting vertex after the outline begins tracing.
                float fill = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.45f, 1f, progress));
                Vector3 anchor = facets[i * 3 + faceCorner[i]];
                vertices[i * 3] = Vector3.Lerp(anchor, a + offset, fill);
                vertices[i * 3 + 1] = Vector3.Lerp(anchor, b + offset, fill);
                vertices[i * 3 + 2] = Vector3.Lerp(anchor, c + offset, fill);
            }
            mesh.vertices = vertices; mesh.RecalculateBounds(); UpdateEngine(dt, spawn);
        }
        private void AddBall(Transform volume, Vector3 worldPosition, float worldRadius, ref int count)
        {
            if (worldRadius <= .0005f || count >= balls.Length) return;
            Vector3 p = volume.InverseTransformPoint(worldPosition);
            float r = worldRadius / Mathf.Max(.0001f, Mathf.Abs(volume.lossyScale.x));
            if (Mathf.Abs(p.x) + r > .49f || Mathf.Abs(p.y) + r > .49f || Mathf.Abs(p.z) + r > .49f) return;
            balls[count++] = new Vector4(p.x, p.y, p.z, r);
        }
        private void UpdateEngine(float dt, float spawn)
        {
            if (engineRenderer == null) return;
            Transform volume = engineRenderer.transform;
            float rootScale = Mathf.Max(.0001f, Mathf.Abs(transform.lossyScale.x));
            int count = 0;
            for (int i = 0; i < CoreBallCount; i++)
            {
                float phase = age * (2.2f + i * .19f) + phaseOffset + i * 2.39996f;
                Vector3 local = i == 0 ? Vector3.zero : new Vector3(Mathf.Cos(phase), Mathf.Sin(phase * 1.13f), Mathf.Sin(phase * .83f) * .85f) * coreOrbit;
                float r = coreRadius * (i == 0 ? .85f : .72f + .18f * Mathf.Sin(phase * 1.37f));
                AddBall(volume, transform.TransformPoint(local), r * engineIntensity * rootScale, ref count);
            }
            bool moving = Application.isPlaying && deathAge < 0f && body != null && body.linearVelocity.sqrMagnitude > .02f && spawn > .9f;
            thrust = Mathf.MoveTowards(thrust, moving ? 1f : 0f, dt * 8f);
            for (int i = 0; i < 4; i++)
            {
                float t = (i + 1f) / 4f, phase = age * 12f + phaseOffset - i * .85f;
                Vector3 local = new Vector3(Mathf.Sin(phase) * .009f, Mathf.Cos(phase * 1.2f) * .009f, -noseLength * tailLengthRatio * .72f * t);
                float r = Mathf.Lerp(coreRadius * 1.3f, exhaustRadius, t) * (1f + .1f * Mathf.Sin(phase));
                AddBall(volume, transform.TransformPoint(local), r * thrust * engineIntensity * rootScale, ref count);
            }
            // Fixed arrays; particles age without per-frame allocations or particle GameObjects.
            for (int i = 0; i < ExhaustCapacity; i++)
            {
                exhaustAges[i] += dt;
                if (exhaustAges[i] >= exhaustLives[i]) continue;
                exhaustVelocities[i] += exhaustAccelerations[i] * dt;
                exhaustPositions[i] += exhaustVelocities[i] * dt;
            }
            if (moving)
            {
                emission += dt * exhaustPerSecond;
                int emitted = Mathf.Min(ExhaustCapacity, Mathf.FloorToInt(emission)); emission -= Mathf.Floor(emission);
                for (int n = 0; n < emitted; n++)
                {
                    int i = exhaustIndex++ % ExhaustCapacity;
                    float v = Mathf.Clamp(exhaustVariation, 0f, .6f), angle = Sample(0f, Mathf.PI * 2f);
                    float spread = Mathf.Tan(exhaustSpreadDegrees * Mathf.Deg2Rad) * Sample(.2f, 1f);
                    Vector3 direction = transform.TransformDirection(new Vector3(Mathf.Cos(angle) * spread, Mathf.Sin(angle) * spread, -1f)).normalized;
                    Vector3 nozzle = new Vector3(Mathf.Cos(angle) * .012f, Mathf.Sin(angle) * .012f, -noseLength * tailLengthRatio * .72f);
                    exhaustSizes[i] = exhaustRadius * Sample(1f - v, 1f + v) * rootScale;
                    exhaustLives[i] = Mathf.Max(.01f, exhaustLifetime * Sample(1f - v * .5f, 1f + v * .5f));
                    exhaustVelocities[i] = direction * exhaustSpeed * Sample(1f - v, 1f + v) * rootScale + body.linearVelocity * .65f;
                    exhaustAccelerations[i] = direction * exhaustSpeed * 2.5f * rootScale;
                    // Backdate catch-up emission to avoid stacked droplets on slower frames.
                    float initialAge = (emitted - 1f - n + emission) / Mathf.Max(1f, exhaustPerSecond);
                    exhaustAges[i] = initialAge;
                    exhaustPositions[i] = transform.TransformPoint(nozzle) + (exhaustVelocities[i] - body.linearVelocity) * initialAge;
                }
            }
            else emission = 0f;
            ActiveExhaustCount = 0;
            for (int i = 0; i < ExhaustCapacity; i++)
            {
                if (exhaustAges[i] >= exhaustLives[i]) continue;
                float t = exhaustAges[i] / exhaustLives[i], r = exhaustSizes[i] * Mathf.Pow(1f - t, 1.15f) * engineIntensity;
                int before = count; AddBall(volume, exhaustPositions[i], r, ref count);
                if (count > before) ActiveExhaustCount++;
            }
            properties.SetInt(BallCountId, count); properties.SetVectorArray(BallsId, balls); engineRenderer.SetPropertyBlock(properties);
        }
        public override void DrawShapes(Camera camera)
        {
            if (shell == null || mesh == null) return;
            using (Draw.Command(camera))
            {
                Draw.Matrix = shell.localToWorldMatrix; Draw.ZTest = CompareFunction.LessEqual;
                Draw.LineGeometry = LineGeometry.Volumetric3D; Draw.ThicknessSpace = ThicknessSpace.Meters;
                Draw.Thickness = outlineWidth; Draw.Color = outlineColor; Draw.ZOffsetFactor = -1f;
                for (int i = 0; i < 12; i++)
                {
                    int corner = faceCorner[i], start = i * 3;
                    Vector3 a = facets[start + corner], b = facets[start + (corner + 1) % 3], c = facets[start + (corner + 2) % 3];
                    float ab = Vector3.Distance(a, b), bc = Vector3.Distance(b, c), ca = Vector3.Distance(c, a);
                    float remaining = (ab + bc + ca) * faceProgress[i];
                    Trace(a, b, ab, ref remaining); Trace(b, c, bc, ref remaining); Trace(c, a, ca, ref remaining);
                }
            }
        }
        private static void Trace(Vector3 a, Vector3 b, float length, ref float remaining)
        {
            if (remaining > .0001f && length > .0001f) Draw.Line(a, Vector3.Lerp(a, b, Mathf.Clamp01(remaining / length)));
            remaining -= length;
        }
    }
}
