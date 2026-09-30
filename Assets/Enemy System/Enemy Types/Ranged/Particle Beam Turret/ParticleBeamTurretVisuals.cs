using Shapes;
using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.Enemies
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class ParticleBeamTurretVisuals : ImmediateModeShapeDrawer
    {
        public MeshFilter panels;
        public Renderer core;
        public Transform firingPivot;
        [Header("Base")]
        [Min(.05f)] public float baseRadius = .68f;
        [Tooltip("Depth from the wall-mounted octagon to the forward octagon, along local Z.")]
        [Min(.01f)] public float baseHeight = ParticleBeamTurretGeometry.DefaultBaseHeight;
        [Tooltip("Pivot offset from the base's forward rim. Negative values inset the apparatus.")]
        public float firingPivotOffset = -.01774037f;
        [Min(.005f)] public float outlineWidth = .012f;
        [Header("Core backing dome")]
        [Tooltip("Radius of the open-front, frequency-two geodesic hemisphere. Rotates with the firing pivot.")]
        [Min(.01f)] public float domeRadius = .3f;
        [Header("Floating ring")]
        [Min(.05f)] public float ringRadius = .43f;
        [Min(.01f)] public float mouthRadius = .15f;
        [Min(.01f)] public float panelLength = .4f;
        [Range(.1f, 1f)] public float panelWidth = .72f;
        public float ringSpinSpeed = 15f;
        [Min(0f)] public float idleAmplitude = .012f;
        [Min(0f)] public float idleSpeed = 3f;
        [Header("Charging")]
        [Min(0f)] public float chargeAmplitude = .005f;
        [Min(0f)] public float chargeSpeed = 100f;
        [Range(0f, 1f)] public float coreChargeGrowth = .35f;
        [Header("Firing")]
        [Range(0f, 70f)] public float panelOpenAngle = 25f;
        [Header("Lifecycle")]
        [Range(0f, .6f)] public float facetTimingScatter = .3f;
        [Min(0f)] public float shatterDistance = 1.1f;
        public float RingAngle { get; private set; }
        public float DeathProgress { get; private set; }
        public float MouthOpening { get; private set; }
        private const int FaceCount = ParticleBeamTurretGeometry.FaceCount;
        public float FacetProgress(int f) => progress[Mathf.Clamp(f, 0, FaceCount - 1)];
        public Vector3 FacetCornerWorld(int face, int corner) => transform.TransformPoint(corners[Mathf.Clamp(face, 0, FaceCount - 1) * 3 + Mathf.Clamp(corner, 0, 2)]);
        private EnemyBase enemy;
        private ParticleBeamTurretController turret;
        private Mesh mesh;
        private float age, deathAge = -1f, chargeMix, jitterClock;
        private readonly Vector3[] corners = new Vector3[FaceCount * 3], vertices = new Vector3[FaceCount * 6];
        private readonly Vector3[] directions = new Vector3[FaceCount], axes = new Vector3[FaceCount];
        private readonly float[] start = new float[FaceCount], duration = new float[FaceCount], progress = new float[FaceCount];
        private readonly int[] origin = new int[FaceCount];
        private readonly Vector4[] balls = new Vector4[48];
        private MaterialPropertyBlock properties;
        public override void OnEnable()
        {
            enemy = GetComponent<EnemyBase>(); turret = GetComponent<ParticleBeamTurretController>();
            if (enemy) enemy.Died += OnDeath;
            age = RingAngle = chargeMix = jitterClock = MouthOpening = DeathProgress = 0f; deathAge = -1f;
            properties = new MaterialPropertyBlock();
            var random = new System.Random(GetInstanceID());
            float Sample(float a, float b) => Mathf.Lerp(a, b, (float)random.NextDouble());
            for (int i = 0; i < FaceCount; i++)
            {
                start[i] = Sample(0f, 1f); duration[i] = Sample(.75f, 1f); origin[i] = random.Next(3);
                directions[i] = new Vector3(Sample(-1f, 1f), Sample(-1f, 1f), Sample(.1f, 1f)).normalized;
                axes[i] = new Vector3(Sample(-1f, 1f), Sample(-1f, 1f), Sample(-1f, 1f)).normalized;
            }
            Rebuild(); useCullingMasks = true; base.OnEnable();
        }
        public override void OnDisable()
        {
            base.OnDisable(); if (enemy) enemy.Died -= OnDeath;
            if (mesh) { if (panels && panels.sharedMesh == mesh) panels.sharedMesh = null; if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); mesh = null; }
        }
        private void OnDeath(EnemyBase e, EnemyDamageSource source) { deathAge = 0f; }
        public void Rebuild()
        {
            if (!panels) return;
            ApplyDimensions();
            if (!mesh) { mesh = new Mesh { name = "Turret triangles (generated)", hideFlags = HideFlags.DontSave }; mesh.MarkDynamic(); }
            mesh.vertices = vertices;
            var indices = new int[vertices.Length]; for (int i = 0; i < indices.Length; i++) indices[i] = i;
            mesh.triangles = indices; panels.sharedMesh = mesh; UpdateGeometry(1f);
        }
        public void ApplyDimensions()
        {
            if (!turret) turret = GetComponent<ParticleBeamTurretController>();
            float height = Mathf.Max(.01f, baseHeight), radius = Mathf.Max(.05f, baseRadius);
            if (firingPivot)
            {
                Vector3 position = firingPivot.localPosition; position.z = height + firingPivotOffset;
                if (firingPivot.localPosition != position) firingPivot.localPosition = position;
            }
            if (turret && turret.shellCollider is MeshCollider hull && hull.sharedMesh)
            {
                Vector3 size = hull.sharedMesh.bounds.size;
                Vector3 scale = new Vector3(radius * 2f / Mathf.Max(.001f, size.x), radius * 2f / Mathf.Max(.001f, size.y), height / Mathf.Max(.001f, size.z));
                if (hull.transform.localScale != scale) hull.transform.localScale = scale;
            }
            if (turret && turret.damageTrigger is CapsuleCollider hurt)
            {
                float reach = Mathf.Max(height, height + firingPivotOffset + panelLength);
                // Retain the established hurtbox at the original dimensions; only the
                // extra reach/width from authoring controls expands or contracts it.
                float reachDelta = reach - .83f;
                float hurtRadius = Mathf.Max(radius, Mathf.Max(ringRadius, domeRadius)) * (.59f / ParticleBeamTurretGeometry.DefaultBaseRadius);
                float hurtHeight = Mathf.Max(hurtRadius * 2f, 1.65f + reachDelta);
                Vector3 center = Vector3.forward * (.3f + reachDelta * .5f);
                if (hurt.radius != hurtRadius) hurt.radius = hurtRadius;
                if (hurt.center != center) hurt.center = center;
                if (hurt.height != hurtHeight) hurt.height = hurtHeight;
            }
        }
        private void LateUpdate()
        {
            if (!panels || !firingPivot || !core) return;
            if (!mesh) Rebuild();
            if (Application.isPlaying && enemy && enemy.IsPaused && !enemy.IsDead) return;
            ApplyDimensions();
            float dt = Application.isPlaying ? Time.deltaTime : 0f; age += dt;
            if (deathAge >= 0f) deathAge += dt;
            DeathProgress = deathAge < 0f ? 0f : Mathf.Clamp01(deathAge / Mathf.Max(.01f, enemy ? enemy.despawnDelaySeconds : .8f));
            bool charging = turret && turret.Phase == ParticleBeamTurretController.AttackPhase.Charging;
            bool firing = turret && turret.Phase == ParticleBeamTurretController.AttackPhase.Firing;
            chargeMix = Mathf.MoveTowards(chargeMix, charging ? 1f : 0f, dt / .15f);
            jitterClock += dt * Mathf.Lerp(idleSpeed, chargeSpeed, chargeMix);
            RingAngle = Mathf.Repeat(RingAngle + ringSpinSpeed * dt, 360f);
            MouthOpening = Mathf.MoveTowards(MouthOpening, firing ? 1f : 0f, dt / .15f);
            float reveal = Application.isPlaying && turret ? turret.RevealProgress : 1f;
            UpdateGeometry(reveal); UpdateCore(reveal);
        }
        private void UpdateGeometry(float reveal)
        {
            if (!firingPivot) return;
            for (int i = 0; i < ParticleBeamTurretGeometry.BaseFaces; i++) ParticleBeamTurretGeometry.BaseTriangle(i, baseRadius, baseHeight, corners, i * 3);
            Matrix4x4 pivotToRoot = transform.worldToLocalMatrix * firingPivot.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4f;
                Vector3 radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                Vector3 tangent = new Vector3(-radial.y, radial.x, 0f);
                float amplitude = Mathf.Lerp(idleAmplitude, chargeAmplitude, chargeMix);
                float jitter = (Mathf.Sin(jitterClock + i * 2.4f) + .3f * Mathf.Sin(jitterClock * 2.31f + i)) / 1.3f;
                Vector3 hinge = radial * (ringRadius + amplitude * jitter);
                Quaternion open = Quaternion.AngleAxis(panelOpenAngle * MouthOpening, tangent);
                Quaternion roll = Quaternion.AngleAxis(RingAngle, Vector3.forward);
                Vector3 tip = radial * mouthRadius + Vector3.forward * panelLength;
                int n = (i + ParticleBeamTurretGeometry.BaseFaces) * 3;
                corners[n] = pivotToRoot.MultiplyPoint3x4(roll * (hinge - tangent * ringRadius * .32f * panelWidth));
                corners[n + 1] = pivotToRoot.MultiplyPoint3x4(roll * (hinge + tangent * ringRadius * .32f * panelWidth));
                corners[n + 2] = pivotToRoot.MultiplyPoint3x4(roll * (hinge + open * (tip - radial * ringRadius)));
            }
            for (int i = 0; i < ParticleBeamTurretGeometry.DomeFaces; i++)
            {
                int n = (i + ParticleBeamTurretGeometry.BaseFaces + ParticleBeamTurretGeometry.RingFaces) * 3;
                ParticleBeamTurretGeometry.DomeTriangle(i, domeRadius, corners, n);
                for (int j = 0; j < 3; j++) corners[n + j] = pivotToRoot.MultiplyPoint3x4(corners[n + j]);
            }
            for (int f = 0; f < FaceCount; f++)
            {
                if (deathAge < 0f) progress[f] = Mathf.Clamp01((reveal - start[f] * facetTimingScatter) / Mathf.Max(.01f, (1f - start[f] * facetTimingScatter) * duration[f]));
                Vector3 center = (corners[f * 3] + corners[f * 3 + 1] + corners[f * 3 + 2]) / 3f;
                float ease = 1f - (1f - DeathProgress) * (1f - DeathProgress);
                for (int j = 0; j < 3; j++) corners[f * 3 + j] = center + directions[f] * shatterDistance * ease
                    + Quaternion.AngleAxis(260f * ease, axes[f]) * (corners[f * 3 + j] - center) * (1f - DeathProgress * .95f);
                Vector3 anchor = corners[f * 3 + origin[f]];
                float fill = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.3f, 1f, progress[f]));
                for (int j = 0; j < 3; j++)
                { vertices[f * 6 + j] = Vector3.Lerp(anchor, corners[f * 3 + j], fill); vertices[f * 6 + 5 - j] = vertices[f * 6 + j]; }
            }
            mesh.vertices = vertices; mesh.RecalculateBounds();
        }
        private void UpdateCore(float reveal)
        {
            float charge = turret ? turret.Charge01 : 0f;
            float scale = Mathf.SmoothStep(0f, 1f, reveal) * (1f - DeathProgress) * (1f + charge * coreChargeGrowth);
            for (int i = 0; i < 7; i++)
            {
                float phase = age * (3f + i * .21f) + jitterClock * chargeMix * .15f + i * 2.39996f;
                Vector3 p = i == 0 ? Vector3.zero : new Vector3(Mathf.Cos(phase), Mathf.Sin(phase), Mathf.Sin(phase * 1.27f)) * .095f;
                p *= 1f + charge * .18f;
                balls[i] = new Vector4(p.x, p.y, p.z, (i == 0 ? .1f : .062f) * scale);
            }
            core.enabled = scale > .001f;
            properties.SetInt("_BallCount", 7); properties.SetVectorArray("_Balls", balls);
            properties.SetFloat("_SmoothK", .008f * scale); core.SetPropertyBlock(properties);
        }
        public override void DrawShapes(Camera camera)
        {
            if (!mesh || !isActiveAndEnabled) return;
            using (Draw.Command(camera))
            {
                Draw.Matrix = transform.localToWorldMatrix; Draw.ZTest = CompareFunction.LessEqual;
                Draw.LineGeometry = LineGeometry.Volumetric3D; Draw.ThicknessSpace = ThicknessSpace.Meters;
                Draw.Thickness = outlineWidth; Draw.Color = new Color(1f, 1f, 1f, 1f - DeathProgress); Draw.ZOffsetFactor = -1f;
                for (int f = 0; f < FaceCount; f++)
                    for (int j = 0; j < 3; j++)
                    {
                        float part = Mathf.Clamp01(progress[f] * 3f - j); if (part <= .0001f) continue;
                        Vector3 a = corners[f * 3 + (origin[f] + j) % 3], b = corners[f * 3 + (origin[f] + j + 1) % 3];
                        if ((b - a).sqrMagnitude > .000001f) Draw.Line(a, Vector3.Lerp(a, b, part));
                    }
                if (Application.isPlaying && turret && turret.Phase == ParticleBeamTurretController.AttackPhase.Charging && DeathProgress == 0f)
                {
                    Draw.Matrix = Matrix4x4.identity; Draw.Color = Color.white;
                    Draw.Thickness = Mathf.Lerp(.004f, .009f, turret.Charge01);
                    // Telegraph brightness stays binary too; charge reveals longer dashes instead of alpha.
                    for (float distance = 0f; distance < turret.BeamLength; distance += .25f)
                        Draw.Line(turret.BeamOrigin + turret.BeamDirection * distance,
                            turret.BeamOrigin + turret.BeamDirection * Mathf.Min(turret.BeamLength, distance + Mathf.Lerp(.045f, .14f, turret.Charge01)));
                }
            }
        }
    }
}
