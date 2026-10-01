using Shapes;
using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.Enemies
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed partial class SeekerVisuals : ImmediateModeShapeDrawer
    {
        public MeshFilter panels;
        public Renderer core;
        [Header("Hexagonal pyramid nose")]
        [Min(0f), Tooltip("Moves the entire nose forward along the attack axis, without changing its shape.")]
        public float noseOffset = .18f;
        [Min(.01f)] public float noseRadius = .27f;
        [Min(.01f)] public float noseLength = .48f;
        public float noseSpinSpeed = 24f;
        [Min(1f)] public float chargeNoseSpinMultiplier = 8f;
        [Min(.01f)] public float noseSpinEaseOutSeconds = .65f;
        [Header("Thrust nose elasticity")]
        [Range(0f, .5f)] public float thrustNoseStretch = .18f;
        [Min(.01f)] public float noseStretchRiseSeconds = .045f;
        [Min(.05f)] public float noseBounceSeconds = .45f;
        [Range(.5f, 4f)] public float noseBounceCycles = 1.5f;
        [Range(0f, 10f)] public float noseBounceDamping = 3.5f;
        [Header("Large forward-facing rear ring")]
        [Min(0f), Tooltip("Distance behind the core along the attack axis; independent of radius.")]
        public float largeRingOffset = .28f;
        [Min(.01f)] public float largeRingRadius = .34f;
        [Min(.01f)] public float largePanelWidth = .28f;
        [Min(.01f)] public float largePanelLength = .36f;
        [Range(0f, 80f)] public float largePanelOutwardTilt = 30f;
        public float largeRingSpinSpeed = -32f;
        [Header("Small backward-facing rear ring")]
        [Min(0f)] public float smallRingOffset = .48f;
        [Min(.01f)] public float smallRingRadius = .21f;
        [Min(.01f)] public float smallPanelWidth = .18f;
        [Min(.01f)] public float smallPanelLength = .25f;
        [Range(0f, 80f)] public float smallPanelOutwardTilt = 18f;
        public float smallRingSpinSpeed = 40f;
        [Header("Charge stretch and compression")]
        [Min(0f)] public float chargeStretch = .65f;
        [Range(0f, 85f)] public float chargeInwardTilt = 48f;
        [Min(0f)] public float chargeVibrationAmplitude = .012f;
        [Min(0f)] public float chargeVibrationFrequency = 32f;
        [Tooltip("Brief rear-ring overshoot toward the nose at full compression.")]
        [Min(0f)] public float compressionTravel = .11f;
        [Min(.01f)] public float releaseSeconds = .18f;
        [Header("Energy core and outline")]
        [Min(.01f)] public float coreRadius = .15f;
        [Range(0f, 1f)] public float coreChargeGrowth = .2f;
        [Min(.001f)] public float outlineWidth = .01f;
        [Range(0f, .6f)] public float facetTimingScatter = .35f;
        [Min(0f)] public float shatterDistance = 1.2f;
        public const int FaceCount = 18;
        public float AnimationTime { get; private set; }
        public float DeathProgress { get; private set; }
        public float RearDisplacement { get; private set; }
        public float CoreLength { get; private set; }
        public float NoseAngle { get; private set; }
        public float NoseSpinMultiplier { get; private set; } = 1f;
        public float NoseStretch { get; private set; } = 1f;
        public float FacetProgress(int i) => progress[Mathf.Clamp(i, 0, FaceCount - 1)];
        private SeekerController seeker;
        private EnemyBase enemy;
        private Mesh mesh;
        private MaterialPropertyBlock properties;
        private float deathAge = -1f;
        private float noseBounceAge = -1f, noseReleaseStretch;
        private bool wasThrusting;
        private readonly Vector3[] corners = new Vector3[FaceCount * 3], vertices = new Vector3[FaceCount * 6];
        private readonly Vector3[] directions = new Vector3[FaceCount], axes = new Vector3[FaceCount];
        private readonly float[] start = new float[FaceCount], progress = new float[FaceCount];
        private readonly int[] origin = new int[FaceCount];
        private readonly Vector4[] balls = new Vector4[48];
        public override void OnEnable()
        {
            seeker = GetComponent<SeekerController>(); enemy = GetComponent<EnemyBase>();
            if (enemy) enemy.Died += OnDeath;
            deathAge = -1f; AnimationTime = DeathProgress = RearDisplacement = 0f;
            NoseAngle = 0f; NoseSpinMultiplier = NoseStretch = 1f; noseBounceAge = -1f; wasThrusting = false; ClearGhosts();
            properties = new MaterialPropertyBlock(); var random = new System.Random(GetInstanceID());
            float Sample(float a, float b) => Mathf.Lerp(a, b, (float)random.NextDouble());
            for (int i = 0; i < FaceCount; i++)
            {
                start[i] = Sample(0f, 1f); origin[i] = random.Next(3);
                directions[i] = new Vector3(Sample(-1f, 1f), Sample(-1f, 1f), Sample(-1f, 1f)).normalized;
                axes[i] = new Vector3(Sample(-1f, 1f), Sample(-1f, 1f), Sample(-1f, 1f)).normalized;
            }
            Rebuild(); useCullingMasks = true; base.OnEnable();
        }
        public override void OnDisable()
        {
            base.OnDisable(); if (enemy) enemy.Died -= OnDeath; ClearGhosts();
            if (mesh)
            {
                if (panels && panels.sharedMesh == mesh) panels.sharedMesh = null;
                if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); mesh = null;
            }
        }
        private void OnDeath(EnemyBase e, EnemyDamageSource cause) { deathAge = 0f; ClearGhosts(); }
        public void Rebuild()
        {
            if (!panels) return;
            if (!mesh) { mesh = new Mesh { name = "Seeker shell (generated)", hideFlags = HideFlags.DontSave }; mesh.MarkDynamic(); }
            mesh.vertices = vertices; var indices = new int[vertices.Length]; for (int i = 0; i < indices.Length; i++) indices[i] = i;
            mesh.triangles = indices; panels.sharedMesh = mesh; UpdateGeometry(1f);
        }
        private void LateUpdate()
        {
            if (!panels || !core || (Application.isPlaying && enemy && enemy.IsPaused && !enemy.IsDead)) return;
            if (!mesh) Rebuild();
            float dt = Application.isPlaying ? Time.deltaTime : 0f; AnimationTime += dt;
            if (deathAge >= 0f) deathAge += dt;
            AnimateNose(dt);
            DeathProgress = deathAge < 0f ? 0f : Mathf.Clamp01(deathAge / Mathf.Max(.01f, enemy ? enemy.despawnDelaySeconds : .75f));
            float compression = seeker ? Mathf.SmoothStep(0f, 1f, seeker.Compression01) : 0f;
            if (seeker && seeker.Phase == SeekerController.AttackPhase.Firing)
                compression = 1f - Mathf.SmoothStep(0f, 1f, seeker.PhaseAge / Mathf.Max(.01f, releaseSeconds));
            RearDisplacement = (seeker ? seeker.Extension01 : 0f) * chargeStretch - compression * compressionTravel;
            float reveal = Application.isPlaying && seeker ? seeker.RevealProgress : 1f;
            UpdateGeometry(reveal); UpdateCore(reveal); AdvanceGhosts(dt);
        }
        private void AnimateNose(float dt)
        {
            bool charging = seeker && seeker.Phase == SeekerController.AttackPhase.Charging;
            float charge = charging ? seeker.Charge01 : 0f;
            if (charging) NoseSpinMultiplier = Mathf.Lerp(1f, Mathf.Max(1f, chargeNoseSpinMultiplier), charge * charge);
            else NoseSpinMultiplier = Mathf.Lerp(NoseSpinMultiplier, 1f, 1f - Mathf.Exp(-5f * dt / Mathf.Max(.01f, noseSpinEaseOutSeconds)));
            NoseAngle = Mathf.Repeat(NoseAngle + noseSpinSpeed * NoseSpinMultiplier * dt, 360f);
            bool thrusting = seeker && seeker.Phase == SeekerController.AttackPhase.Firing && deathAge < 0f;
            if (thrusting)
            {
                NoseStretch = Mathf.Lerp(NoseStretch, 1f + thrustNoseStretch, 1f - Mathf.Exp(-5f * dt / Mathf.Max(.01f, noseStretchRiseSeconds)));
                noseBounceAge = -1f;
            }
            else
            {
                if (wasThrusting) { noseBounceAge = 0f; noseReleaseStretch = NoseStretch - 1f; }
                if (noseBounceAge >= 0f)
                {
                    noseBounceAge += dt;
                    float u = Mathf.Clamp01(noseBounceAge / Mathf.Max(.05f, noseBounceSeconds));
                    NoseStretch = 1f + noseReleaseStretch * Mathf.Cos(u * noseBounceCycles * Mathf.PI * 2f)
                        * Mathf.Exp(-noseBounceDamping * u) * (1f - u);
                    if (u >= 1f) { noseBounceAge = -1f; NoseStretch = 1f; }
                }
            }
            wasThrusting = thrusting;
        }
        public void CopyPoseCorners(Vector3[] destination, bool animated = false)
        {
            float time = animated ? AnimationTime : 0f;
            Quaternion noseRoll = Quaternion.AngleAxis(animated ? NoseAngle : 0f, Vector3.forward);
            for (int i = 0; i < 6; i++)
            {
                int n = i * 3;
                destination[n] = noseRoll * Radial(i * Mathf.PI / 3f) * noseRadius + Vector3.forward * noseOffset;
                destination[n + 1] = noseRoll * Radial((i + 1) * Mathf.PI / 3f) * noseRadius + Vector3.forward * noseOffset;
                destination[n + 2] = Vector3.forward * (noseOffset + noseLength * (animated ? NoseStretch : 1f));
            }
            Ring(destination, 6, largeRingOffset, largeRingRadius, largePanelWidth, largePanelLength, largePanelOutwardTilt,
                largeRingSpinSpeed * time, true, animated);
            Ring(destination, 12, smallRingOffset, smallRingRadius, smallPanelWidth, smallPanelLength, smallPanelOutwardTilt,
                smallRingSpinSpeed * time, false, animated);
        }
        private static Vector3 Radial(float angle) => new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
        private void Ring(Vector3[] points, int first, float offset, float radius, float width, float length, float tilt, float spin, bool forward, bool animated)
        {
            float charge = animated && seeker ? seeker.Charge01 : 0f;
            float rear = animated ? RearDisplacement : 0f;
            Quaternion roll = Quaternion.AngleAxis(spin, Vector3.forward);
            for (int i = 0; i < 6; i++)
            {
                Vector3 radial = Radial((i + .5f) * Mathf.PI / 3f), tangent = new Vector3(-radial.y, radial.x, 0f);
                float wave = Mathf.Sin(AnimationTime * chargeVibrationFrequency * Mathf.PI * 2f + i * 2.4f)
                    + .3f * Mathf.Sin(AnimationTime * chargeVibrationFrequency * 9.31f + i);
                float jitter = forward ? chargeVibrationAmplitude * charge * wave / 1.3f : 0f;
                Vector3 hinge = radial * radius + Vector3.back * (offset + rear) + radial * jitter;
                float angle = (tilt - (forward ? chargeInwardTilt * charge : 0f)) * Mathf.Deg2Rad;
                Vector3 tip = hinge + (Vector3.forward * (forward ? 1f : -1f) * Mathf.Cos(angle) + radial * Mathf.Sin(angle)) * length;
                int n = (first + i) * 3;
                points[n] = roll * (hinge - tangent * width * .5f); points[n + 1] = roll * (hinge + tangent * width * .5f); points[n + 2] = roll * tip;
            }
        }
        public Mesh CreateSpawnOutline()
        {
            var pose = new Vector3[FaceCount * 3]; CopyPoseCorners(pose);
            var outline = new Mesh { name = "Seeker arrival outline (generated)", hideFlags = HideFlags.DontSave };
            ParticleBeamTurretGeometry.BuildOutline(outline, pose); return outline;
        }
        private void UpdateGeometry(float reveal)
        {
            CopyPoseCorners(corners, true);
            if (deathAge < 0f && seeker && seeker.damageTrigger is CapsuleCollider hurt)
            {
                float low = -coreRadius, high = coreRadius, radius = coreRadius;
                for (int i = 0; i < corners.Length; i++)
                {
                    low = Mathf.Min(low, corners[i].z); high = Mathf.Max(high, corners[i].z);
                    radius = Mathf.Max(radius, new Vector2(corners[i].x, corners[i].y).magnitude);
                }
                // Exposed rear panels remain sword-hittable as they stretch away during charge.
                Vector3 center = Vector3.forward * ((low + high) * .5f);
                float height = Mathf.Max(radius * 2f, high - low);
                if (hurt.center != center) hurt.center = center;
                if (hurt.radius != radius) hurt.radius = radius;
                if (hurt.height != height) hurt.height = height;
            }
            for (int f = 0; f < FaceCount; f++)
            {
                if (deathAge < 0f) progress[f] = Mathf.Clamp01((reveal - start[f] * facetTimingScatter) / Mathf.Max(.01f, 1f - start[f] * facetTimingScatter));
                Vector3 center = (corners[f * 3] + corners[f * 3 + 1] + corners[f * 3 + 2]) / 3f;
                float ease = 1f - (1f - DeathProgress) * (1f - DeathProgress);
                for (int j = 0; j < 3; j++) corners[f * 3 + j] = center + directions[f] * shatterDistance * ease
                    + Quaternion.AngleAxis(ease * 260f, axes[f]) * (corners[f * 3 + j] - center) * (1f - DeathProgress);
                Vector3 anchor = corners[f * 3 + origin[f]];
                float fill = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.3f, 1f, progress[f]));
                for (int j = 0; j < 3; j++) { vertices[f * 6 + j] = Vector3.Lerp(anchor, corners[f * 3 + j], fill); vertices[f * 6 + 5 - j] = vertices[f * 6 + j]; }
            }
            mesh.vertices = vertices; mesh.RecalculateBounds();
        }
        private void UpdateCore(float reveal)
        {
            float charge = seeker ? seeker.Charge01 : 0f;
            float radius = coreRadius * (1f + charge * coreChargeGrowth);
            float extension = Mathf.Max(0f, RearDisplacement); CoreLength = radius * 2f + extension;
            float scale = Mathf.SmoothStep(0f, 1f, reveal) * (1f - DeathProgress);
            // Uniform volume scaling keeps the metaball radius meaningful while accommodating the stretched core.
            float volume = Mathf.Max(1f, (extension + radius * 3f) * 1.3f);
            core.transform.localPosition = Vector3.back * extension * .5f; core.transform.localScale = Vector3.one * volume;
            int count = 0;
            int bridgeCount = Mathf.Clamp(Mathf.CeilToInt(extension / Mathf.Max(.01f, radius * .7f)) + 1, 1, 20);
            for (int i = 0; i < bridgeCount; i++)
            {
                float u = bridgeCount == 1 ? .5f : i / (float)(bridgeCount - 1);
                float r = radius * .67f * scale;
                balls[count++] = new Vector4(0f, 0f, Mathf.Lerp(-extension * .5f, extension * .5f, u) / volume, r / volume);
            }
            for (int i = 0; i < 6; i++)
            {
                float phase = AnimationTime * (3f + i * .21f + charge * 35f) + i * 2.39996f;
                Vector3 p = new Vector3(Mathf.Cos(phase), Mathf.Sin(phase), Mathf.Sin(phase * 1.27f) * .6f) * radius * .63f;
                p.z += (i % 2 == 0 ? -.5f : .5f) * extension;
                balls[count++] = new Vector4(p.x / volume, p.y / volume, p.z / volume, radius * .42f * scale / volume);
            }
            core.enabled = scale > .001f;
            properties.SetInt("_BallCount", count); properties.SetVectorArray("_Balls", balls);
            properties.SetFloat("_SmoothK", .014f * scale / volume); properties.SetFloat("_BeamContinuous", 0f);
            core.SetPropertyBlock(properties);
        }
        public override void DrawShapes(Camera camera)
        {
            if (!mesh || !isActiveAndEnabled) return;
            using (Draw.Command(camera))
            {
                DrawGhosts();
                Draw.Matrix = transform.localToWorldMatrix; Draw.ZTest = CompareFunction.LessEqual;
                Draw.LineGeometry = LineGeometry.Volumetric3D; Draw.ThicknessSpace = ThicknessSpace.Meters;
                Draw.Thickness = outlineWidth * (1f - DeathProgress); Draw.Color = Color.white; Draw.ZOffsetFactor = -1f;
                for (int f = 0; f < FaceCount; f++) for (int j = 0; j < 3; j++)
                {
                    float part = Mathf.Clamp01(progress[f] * 3f - j); if (part <= .0001f) continue;
                    Vector3 a = corners[f * 3 + (origin[f] + j) % 3], b = corners[f * 3 + (origin[f] + j + 1) % 3];
                    if ((b - a).sqrMagnitude > .000001f) Draw.Line(a, Vector3.Lerp(a, b, part));
                }
            }
        }
    }
}
