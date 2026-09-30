using UnityEngine;

namespace Massive.Enemies
{
    /// <summary>A bounded, reusable metaball pool. The turret advances it with its paused gameplay clock.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(MetaballSDFInstance))]
    public sealed class ParticleBeamPlasmaContact : MonoBehaviour
    {
        [Header("Sustained contact plasma")]
        [Min(0f)] public float dropsPerSecond = 22f;
        [Min(.1f)] public float dropLifetime = .65f;
        [Min(.01f)] public float dropRadius = .065f;
        [Min(0f)] public float splashSpeed = .65f;
        [Min(.01f)] public float contactRadius = .17f;
        [Tooltip("World-space drip acceleration. Negative Z drips down the arena in the gameplay view.")]
        public Vector3 dripAcceleration = new Vector3(0f, -.15f, -.9f);
        [Range(0f, 1f)] public float whiteFraction = .75f;

        private struct Drop { public Vector3 position, velocity; public float age, life, radius, ink; }
        private readonly Drop[] drops = new Drop[20];
        private readonly Vector4[] balls = new Vector4[MetaballSDFInstance.MaxBalls];
        private readonly float[] ink = new float[MetaballSDFInstance.MaxBalls];
        private MetaballSDFInstance sdf;
        private Renderer target;
        private MaterialPropertyBlock properties;
        private MeshFilter filter;
        private Mesh volumeMesh, sourceMesh;
        private readonly Vector3[] volumeVertices = new Vector3[8];
        private Vector3 point, normal = Vector3.back;
        private float clock, budget, coating, strength;
        private int nextDrop, serial, ballCount;
        public bool IsEmitting { get; private set; }
        public int LiveDropCount { get; private set; }
        public int TotalEmitted { get; private set; }
        public float AnimationTime => clock;
        public Vector3 ContactPoint => point;

        private void Awake()
        {
            sdf = GetComponent<MetaballSDFInstance>(); target = sdf.TargetRenderer;
            filter = GetComponent<MeshFilter>();
            if (filter)
            {
                sourceMesh = filter.sharedMesh;
                volumeMesh = new Mesh { name = "Fitted plasma contact volume", hideFlags = HideFlags.DontSave };
                volumeMesh.MarkDynamic(); volumeMesh.vertices = volumeVertices;
                volumeMesh.triangles = new[] { 0,2,1,1,2,3, 4,5,6,5,7,6, 0,1,4,1,5,4, 2,6,3,3,6,7, 0,4,2,2,4,6, 1,3,5,3,7,5 };
                filter.sharedMesh = volumeMesh;
            }
            properties = new MaterialPropertyBlock(); Clear();
        }
        private void OnDestroy()
        {
            if (filter && filter.sharedMesh == volumeMesh) filter.sharedMesh = sourceMesh;
            if (volumeMesh) Destroy(volumeMesh);
        }
        private void OnDisable() { if (sdf) Clear(); }
        public void SetContact(Vector3 position, Vector3 outwardNormal, float intensity)
        {
            point = position;
            normal = outwardNormal.sqrMagnitude > .001f ? outwardNormal.normalized : Vector3.back;
            strength = Mathf.Clamp01(intensity); IsEmitting = strength > .001f;
        }
        public void StopEmission() { IsEmitting = false; budget = 0f; }
        public void Clear()
        {
            StopEmission(); coating = clock = 0f; nextDrop = serial = LiveDropCount = TotalEmitted = 0;
            for (int i = 0; i < drops.Length; i++) drops[i] = default;
            if (sdf) { sdf.Clear(); sdf.Apply(); }
            if (target) target.enabled = false;
        }
        private static float Hash(int n) => Mathf.Repeat(Mathf.Sin(n * 12.9898f + 3.71f) * 43758.5453f, 1f);
        public void Advance(float dt)
        {
            if (!isActiveAndEnabled || !sdf || dt <= 0f) return;
            clock += dt;
            coating = Mathf.MoveTowards(coating, IsEmitting ? strength : 0f, dt / .18f);
            for (int i = 0; i < drops.Length; i++)
            {
                ref Drop d = ref drops[i]; if (d.life <= 0f) continue;
                d.age += dt; if (d.age >= d.life) { d.life = 0f; continue; }
                d.velocity += dripAcceleration * dt; d.position += d.velocity * dt;
            }
            if (IsEmitting)
            {
                budget += dt * Mathf.Clamp(dropsPerSecond, 0f, 120f);
                while (budget >= 1f)
                {
                    budget -= 1f; int seed = ++serial;
                    Vector3 side = Vector3.Cross(Vector3.up, normal).normalized;
                    if (side.sqrMagnitude < .01f) side = Vector3.right;
                    float lateral = Hash(seed * 3) * 2f - 1f;
                    drops[nextDrop] = new Drop
                    {
                        position = point + side * lateral * contactRadius * .55f,
                        velocity = (side * lateral + normal * Mathf.Lerp(.2f, .6f, Hash(seed * 5))
                            + Vector3.up * Mathf.Lerp(.05f, .3f, Hash(seed * 7))) * splashSpeed,
                        life = dropLifetime * Mathf.Lerp(.7f, 1.2f, Hash(seed * 11)),
                        radius = dropRadius * Mathf.Lerp(.7f, 1.4f, Hash(seed * 13)),
                        ink = Mathf.Repeat(seed * .618034f, 1f) < 1f - whiteFraction ? 1f : 0f
                    };
                    nextDrop = (nextDrop + 1) % drops.Length; TotalEmitted++;
                }
            }
            Rebuild();
        }
        private void Add(Vector3 p, float radius, float black)
        {
            if (radius <= .0001f || ballCount >= balls.Length) return;
            balls[ballCount] = new Vector4(p.x, p.y, p.z, radius); ink[ballCount++] = black;
        }
        private void Rebuild()
        {
            ballCount = LiveDropCount = 0;
            Vector3 side = Vector3.Cross(Vector3.up, normal).normalized;
            if (coating > .001f)
            {
                float r = contactRadius * coating;
                Add(point, r * (1f + .08f * Mathf.Sin(clock * 17f)), whiteFraction <= 0f ? 1f : 0f);
                for (int i = 0; i < 4; i++)
                {
                    float phase = clock * 9f + i * Mathf.PI * .5f;
                    Add(point + side * Mathf.Cos(phase) * r * .65f + Vector3.up * Mathf.Sin(phase) * r * .5f
                        + normal * r * .1f, r * .6f, (i + .5f) / 4f >= whiteFraction ? 1f : 0f);
                }
            }
            for (int i = 0; i < drops.Length; i++)
            {
                ref Drop d = ref drops[i]; if (d.life <= 0f) continue; LiveDropCount++;
                float u = d.age / d.life;
                float radius = d.radius * (1f - Mathf.SmoothStep(0f, 1f, u));
                Add(d.position, radius, d.ink);
                // Attached satellite stretches into a short viscous neck before the drop contracts.
                Add(d.position - d.velocity * .09f * (1f - u), radius * .72f, d.ink);
            }
            sdf.Clear();
            if (ballCount == 0) { sdf.Apply(); target.enabled = false; return; }
            Vector3 low = Vector3.one * float.MaxValue, high = Vector3.one * float.MinValue;
            for (int i = 0; i < ballCount; i++)
            { Vector3 p = balls[i]; Vector3 pad = Vector3.one * (balls[i].w + .08f); low = Vector3.Min(low, p - pad); high = Vector3.Max(high, p + pad); }
            Vector3 size = high - low, center = (high + low) * .5f;
            float scale = Mathf.Max(.1f, Mathf.Max(size.x, Mathf.Max(size.y, size.z)));
            transform.SetPositionAndRotation(center, Quaternion.identity);
            Vector3 parent = transform.parent ? transform.parent.lossyScale : Vector3.one;
            transform.localScale = new Vector3(scale / Mathf.Max(.001f, Mathf.Abs(parent.x)), scale / Mathf.Max(.001f, Mathf.Abs(parent.y)), scale / Mathf.Max(.001f, Mathf.Abs(parent.z)));
            for (int i = 0; i < ballCount; i++) sdf.AddBall(((Vector3)balls[i] - center) / scale, balls[i].w / scale);
            sdf.Apply(); target.GetPropertyBlock(properties);
            properties.SetFloat("_BeamContinuous", 0f); properties.SetFloat("_PlasmaMode", 2f);
            properties.SetFloat("_SmoothK", .075f / scale); properties.SetFloat("_SmoothComp", .15f);
            properties.SetFloat("_SurfaceEps", .001f); properties.SetFloatArray("_PlasmaInk", ink);
            Vector3 extent = size * (.5f / scale);
            properties.SetVector("_VolumeHalfExtents", extent);
            if (volumeMesh)
            {
                // Separated old/new hit points must not turn the volume into a huge rasterized cube.
                for (int i = 0; i < 8; i++) volumeVertices[i] = new Vector3((i & 1) == 0 ? -extent.x : extent.x,
                    (i & 2) == 0 ? -extent.y : extent.y, (i & 4) == 0 ? -extent.z : extent.z);
                volumeMesh.vertices = volumeVertices; volumeMesh.RecalculateBounds();
            }
            target.SetPropertyBlock(properties); target.enabled = true;
        }
    }
}
