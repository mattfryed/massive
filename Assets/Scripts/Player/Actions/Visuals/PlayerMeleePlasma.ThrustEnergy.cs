using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.Player
{
    public sealed partial class PlayerMeleePlasma
    {
        [Header("Thrust — organic energy")]
        [Tooltip("Replaces the source's flat Glow layer with moving three-dimensional energy around emitted body particles. Off restores the authored glow.")]
        public bool thrustOrganicGlow = true;
        [Range(0, 2)] public float thrustGlowIntensity = .65f;
        [Range(0, 1)] public float thrustGlowBreakup = .7f;
        [Range(0, 1)] public float thrustGlowDepth = .6f;
        [Range(0, 4)] public float thrustGlowFlow = 1.3f;
        [Tooltip("Optional override. Uses the bundled MeleeThrustEnergy material when empty.")]
        public Material thrustEnergyMaterial;

        const int ThrustEnergyCapacity = 18;
        sealed class ThrustEnergy
        {
            public GameObject root;
            public MeshRenderer renderer;
            public MaterialPropertyBlock properties;
            public readonly Vector4[] starts = new Vector4[ThrustEnergyCapacity];
            public readonly Vector4[] ends = new Vector4[ThrustEnergyCapacity];
            public readonly Vector4[] frames = new Vector4[ThrustEnergyCapacity];
            public int count;
            public Vector3 minimum, maximum;
        }

        Material bundledThrustEnergyMaterial, ownedThrustEnergyMaterial;
        bool thrustEnergyMaterialResolved;
        Mesh thrustEnergyProxy;
        static readonly int EnergyAId = Shader.PropertyToID("_EnergyA");
        static readonly int EnergyBId = Shader.PropertyToID("_EnergyB");
        static readonly int EnergyCId = Shader.PropertyToID("_EnergyC");
        static readonly int EnergyCountId = Shader.PropertyToID("_EnergyCount");
        static readonly int EnergyBoundsCenterId = Shader.PropertyToID("_EnergyBoundsCenter");
        static readonly int EnergyBoundsExtentsId = Shader.PropertyToID("_EnergyBoundsExtents");
        static readonly int EnergyControlsId = Shader.PropertyToID("_EnergyControls");
        static readonly int EnergyOpacityId = Shader.PropertyToID("_EnergyOpacity");

        Material ResolveThrustEnergyMaterial()
        {
            if (thrustEnergyMaterial) return thrustEnergyMaterial;
            if (!thrustEnergyMaterialResolved)
            {
                thrustEnergyMaterialResolved = true;
                bundledThrustEnergyMaterial = Resources.Load<Material>("MeleeThrustEnergy");
                if (!bundledThrustEnergyMaterial)
                {
                    var shader = Shader.Find("MASSIVE/MeleeThrustEnergy");
                    if (shader) ownedThrustEnergyMaterial = new Material(shader)
                    { name = "Thrust energy (temporary)", hideFlags = HideFlags.HideAndDontSave };
                }
            }
            return bundledThrustEnergyMaterial ? bundledThrustEnergyMaterial : ownedThrustEnergyMaterial;
        }

        void BeginThrustEnergy(ThrustTrail trail)
        {
            if (trail.energy != null)
            {
                trail.energy.count = 0;
                if (!thrustOrganicGlow || thrustGlowIntensity <= .001f) HideThrustEnergy(trail);
            }
        }

        void AddThrustEnergy(ThrustTrail trail, ParticleSystem ps, ParticleSystemRenderer renderer,
            ParticleSystem.Particle particle, ThrustBirth birth, float passOpacity)
        {
            if (!thrustOrganicGlow || thrustGlowIntensity <= .001f) return;
            if (trail.energy == null) trail.energy = new ThrustEnergy();
            var energy = trail.energy;
            if (energy.count >= ThrustEnergyCapacity) return;
            float playerSize = trail.sizeScale;
            float weight = particle.GetCurrentColor(ps).a / 255f * passOpacity;
            if (weight <= .005f || particle.remainingLifetime <= 0) return;
            var frame = thrustWorldEmission ? birth.matrix : ps.transform.localToWorldMatrix;
            Vector3 size = particle.GetCurrentSize3D(ps);
            size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            var particleMatrix = frame * Matrix4x4.TRS(particle.position, Quaternion.Euler(particle.rotation3D), size);
            Vector3 center = frame.MultiplyPoint3x4(particle.position);
            Vector3 axis;
            float halfLength, radius;
            if (renderer.renderMode == ParticleSystemRenderMode.Mesh && renderer.mesh)
            {
                Bounds meshBounds = renderer.mesh.bounds;
                Vector3 extents = meshBounds.extents;
                Vector3 x = particleMatrix.MultiplyVector(Vector3.right * extents.x);
                Vector3 y = particleMatrix.MultiplyVector(Vector3.up * extents.y);
                Vector3 z = particleMatrix.MultiplyVector(Vector3.forward * extents.z);
                float lx = x.magnitude, ly = y.magnitude, lz = z.magnitude;
                if (lx >= ly && lx >= lz) { axis = x; halfLength = lx; radius = Mathf.Max(ly, lz); }
                else if (ly >= lz) { axis = y; halfLength = ly; radius = Mathf.Max(lx, lz); }
                else { axis = z; halfLength = lz; radius = Mathf.Max(lx, ly); }
                center = particleMatrix.MultiplyPoint3x4(meshBounds.center);
                halfLength *= .72f;
                radius = radius * .4f + .015f * playerSize;
            }
            else
            {
                axis = frame.MultiplyVector(particle.totalVelocity);
                if (axis.sqrMagnitude < .0001f) axis = frame.MultiplyVector(Vector3.forward);
                float scale = Mathf.Max(frame.MultiplyVector(Vector3.right).magnitude,
                    frame.MultiplyVector(Vector3.forward).magnitude);
                float diameter = Mathf.Max(size.x, Mathf.Max(size.y, size.z)) * scale;
                float stretch = renderer.renderMode == ParticleSystemRenderMode.Stretch ? Mathf.Abs(renderer.lengthScale) : 1;
                halfLength = diameter * Mathf.Max(1, stretch) * .28f;
                radius = diameter * .18f + .015f * playerSize;
            }
            if (axis.sqrMagnitude < .000001f) axis = Vector3.forward;
            axis.Normalize();
            // The pockets remain local to authored body particles, never a full-player halo.
            halfLength = Mathf.Clamp(halfLength, .025f * playerSize, Mathf.Max(.08f * playerSize, trail.physicalReach * .7f));
            radius = Mathf.Clamp(radius, .02f * playerSize, .16f * playerSize);
            Vector3 start = center - axis * halfLength;
            Vector3 end = center + axis * halfLength;
            Vector3 up = frame.MultiplyVector(Vector3.up).normalized;
            if (Mathf.Abs(Vector3.Dot(up, axis)) > .95f) up = Mathf.Abs(axis.y) < .9f ? Vector3.up : Vector3.right;
            int index = energy.count++;
            energy.starts[index] = new Vector4(start.x, start.y, start.z, radius);
            energy.ends[index] = new Vector4(end.x, end.y, end.z, Mathf.Clamp01(weight));
            energy.frames[index] = new Vector4(up.x, up.y, up.z, (particle.randomSeed & 65535u) * (6.2831853f / 65535f));
            float pad = radius * 1.75f * Mathf.Max(1, Mathf.Lerp(.5f, 1.4f, thrustGlowDepth)) + .035f * playerSize;
            Vector3 minimum = Vector3.Min(start, end) - Vector3.one * pad;
            Vector3 maximum = Vector3.Max(start, end) + Vector3.one * pad;
            energy.minimum = index == 0 ? minimum : Vector3.Min(energy.minimum, minimum);
            energy.maximum = index == 0 ? maximum : Vector3.Max(energy.maximum, maximum);
        }

        void RenderThrustEnergy(ThrustTrail trail, float elapsed, float opacity)
        {
            var energy = trail.energy;
            if (!thrustOrganicGlow || thrustGlowIntensity <= .001f || opacity <= .001f || energy == null || energy.count == 0)
            { HideThrustEnergy(trail); return; }
            Material material = ResolveThrustEnergyMaterial();
            if (!material) { HideThrustEnergy(trail); return; }
            EnsureThrustEnergyGraphics(energy);
            Vector3 center = (energy.minimum + energy.maximum) * .5f;
            Vector3 extents = Vector3.Max(Vector3.one * (.025f * trail.sizeScale), (energy.maximum - energy.minimum) * .5f);
            energy.root.transform.SetPositionAndRotation(center, Quaternion.identity);
            energy.root.transform.localScale = extents;
            energy.renderer.sharedMaterial = material;
            var properties = energy.properties;
            properties.SetVectorArray(EnergyAId, energy.starts);
            properties.SetVectorArray(EnergyBId, energy.ends);
            properties.SetVectorArray(EnergyCId, energy.frames);
            properties.SetInt(EnergyCountId, energy.count);
            properties.SetVector(EnergyBoundsCenterId, center);
            properties.SetVector(EnergyBoundsExtentsId, extents);
            properties.SetVector(EnergyControlsId, new Vector4(thrustGlowIntensity, thrustGlowBreakup, thrustGlowDepth, elapsed * thrustGlowFlow));
            properties.SetFloat(EnergyOpacityId, Mathf.Clamp01(opacity));
            energy.renderer.SetPropertyBlock(properties);
            energy.renderer.enabled = true;
            trail.instance.visible = true;
        }

        void EnsureThrustEnergyGraphics(ThrustEnergy energy)
        {
            if (energy.root) return;
            if (!thrustEnergyProxy)
            {
                thrustEnergyProxy = new Mesh { name = "Thrust energy bounds", hideFlags = HideFlags.HideAndDontSave };
                thrustEnergyProxy.vertices = new[] { new Vector3(-1,-1,-1), new Vector3(1,-1,-1), new Vector3(1,1,-1), new Vector3(-1,1,-1),
                    new Vector3(-1,-1,1), new Vector3(1,-1,1), new Vector3(1,1,1), new Vector3(-1,1,1) };
                thrustEnergyProxy.triangles = new[] { 0,2,1,0,3,2,4,5,6,4,6,7,0,4,7,0,7,3,1,2,6,1,6,5,3,7,6,3,6,2,0,1,5,0,5,4 };
                thrustEnergyProxy.RecalculateBounds();
            }
            energy.root = new GameObject("Thrust organic energy (temporary)")
            { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
            energy.root.AddComponent<MeshFilter>().sharedMesh = thrustEnergyProxy;
            energy.renderer = energy.root.AddComponent<MeshRenderer>();
            energy.renderer.enabled = false;
            energy.renderer.shadowCastingMode = ShadowCastingMode.Off;
            energy.renderer.receiveShadows = false;
            energy.renderer.lightProbeUsage = LightProbeUsage.Off;
            energy.renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            energy.properties = new MaterialPropertyBlock();
        }

        static void HideThrustEnergy(ThrustTrail trail)
        {
            if (trail != null && trail.energy != null && trail.energy.renderer) trail.energy.renderer.enabled = false;
        }

        static void ReleaseThrustEnergy(ThrustTrail trail)
        {
            if (trail.energy == null) return;
            HideThrustEnergy(trail);
            if (trail.energy.root)
            {
                if (Application.isPlaying) Destroy(trail.energy.root); else DestroyImmediate(trail.energy.root);
            }
            trail.energy = null;
        }

        void ReleaseThrustEnergyResources()
        {
            if (thrustEnergyProxy) { if (Application.isPlaying) Destroy(thrustEnergyProxy); else DestroyImmediate(thrustEnergyProxy); }
            if (ownedThrustEnergyMaterial) { if (Application.isPlaying) Destroy(ownedThrustEnergyMaterial); else DestroyImmediate(ownedThrustEnergyMaterial); }
            thrustEnergyProxy = null; ownedThrustEnergyMaterial = bundledThrustEnergyMaterial = null;
            thrustEnergyMaterialResolved = false;
        }
    }
}
