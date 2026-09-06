using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.Multiplier
{
    /// <summary>
    /// Goal-deposit VFX built around one ray-marched three-dimensional cloud.
    /// A small bounded particle layer adds breakup without defining the shape.
    /// Runtime objects are created once and reused by each goal.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AmplifierAuroraBlast : MonoBehaviour
    {
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int BlastProgressId = Shader.PropertyToID("_BlastProgress");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");

        [SerializeField] private Transform origin;
        [SerializeField] private Material particleMaterial;
        [SerializeField] private Material volumeMaterial;

        [Header("Volumetric Energy Cloud")]
        [SerializeField, Min(0.1f)] private float volumeLifetime = 1.55f;
        [SerializeField, Min(0.01f)] private float volumeStartDiameter = 0.34f;
        [SerializeField, Min(0.1f)] private float volumeMaximumDiameter = 5.2f;
        [SerializeField, Min(0f)] private float volumeInwardDrift = 0.72f;

        [Header("Sparse Cloud Breakup")]
        [SerializeField, Range(0, 32)] private int gasParticleCount = 14;
        [SerializeField, Min(0.1f)] private float gasLifetime = 1.18f;
        [SerializeField, Min(0f)] private float gasSpeed = 1.65f;
        [SerializeField, Min(0.01f)] private float gasSize = 0.46f;

        [Header("Sparse Aurora Filaments")]
        [SerializeField, Range(0, 24)] private int ribbonParticleCount = 8;
        [SerializeField, Min(0.1f)] private float ribbonLifetime = 0.92f;
        [SerializeField, Min(0f)] private float ribbonSpeed = 2.65f;
        [SerializeField, Min(0.01f)] private float ribbonSize = 0.12f;

        [Header("Palette")]
        [SerializeField] private Color[] palette =
        {
            new Color(1f, 0.12f, 0.72f, 0.8f),
            new Color(0.05f, 0.85f, 1f, 0.78f),
            new Color(0.66f, 1f, 0.18f, 0.76f),
            new Color(1f, 0.38f, 0.08f, 0.76f),
            new Color(0.64f, 0.2f, 1f, 0.8f)
        };

        private ParticleSystem _gasSystem;
        private ParticleSystem _ribbonSystem;
        private Transform _effectRoot;
        private Transform _volumeTransform;
        private MeshRenderer _volumeRenderer;
        private MaterialPropertyBlock _volumeProperties;
        private Material _runtimeFallbackParticleMaterial;
        private Material _runtimeFallbackVolumeMaterial;
        private Vector3 _volumeOrigin;
        private Vector3 _volumeInwardDirection;
        private float _volumeStartTime;
        private float _volumeSeed;
        private float _volumeProgress;
        private int _playSequence;
        private bool _volumeActive;

        public ParticleSystem GasSystem => _gasSystem;
        public ParticleSystem RibbonSystem => _ribbonSystem;
        public MeshRenderer VolumeRenderer => _volumeRenderer;
        public bool IsVolumeActive => _volumeActive;
        public float VolumeProgress => _volumeProgress;

        private void OnValidate()
        {
            volumeLifetime = Mathf.Max(0.1f, volumeLifetime);
            volumeStartDiameter = Mathf.Max(0.01f, volumeStartDiameter);
            volumeMaximumDiameter = Mathf.Max(volumeStartDiameter, volumeMaximumDiameter);
            volumeInwardDrift = Mathf.Max(0f, volumeInwardDrift);
            gasParticleCount = Mathf.Clamp(gasParticleCount, 0, 32);
            ribbonParticleCount = Mathf.Clamp(ribbonParticleCount, 0, 24);
            gasLifetime = Mathf.Max(0.1f, gasLifetime);
            ribbonLifetime = Mathf.Max(0.1f, ribbonLifetime);
            gasSpeed = Mathf.Max(0f, gasSpeed);
            ribbonSpeed = Mathf.Max(0f, ribbonSpeed);
            gasSize = Mathf.Max(0.01f, gasSize);
            ribbonSize = Mathf.Max(0.01f, ribbonSize);
        }

        private void Update()
        {
            if (!_volumeActive)
                return;

            float elapsed = Time.time - _volumeStartTime;
            _volumeProgress = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, volumeLifetime));
            UpdateVolumePresentation(_volumeProgress);

            if (_volumeProgress >= 1f)
            {
                _volumeActive = false;
                if (_volumeRenderer != null)
                    _volumeRenderer.enabled = false;
            }
        }

        private void OnDisable()
        {
            if (_gasSystem != null)
                _gasSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_ribbonSystem != null)
                _ribbonSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_volumeRenderer != null)
                _volumeRenderer.enabled = false;
            _volumeActive = false;
        }

        private void OnDestroy()
        {
            if (_runtimeFallbackParticleMaterial != null)
                Destroy(_runtimeFallbackParticleMaterial);
            if (_runtimeFallbackVolumeMaterial != null)
                Destroy(_runtimeFallbackVolumeMaterial);
        }

        public void Play()
        {
            EnsureBuilt();
            PositionAtOrigin();

            _gasSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _ribbonSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _gasSystem.Play(true);
            _ribbonSystem.Play(true);

            var random = new System.Random(
                unchecked((GetInstanceID() * 397) ^ (++_playSequence * 7919)));
            EmitCloud(_gasSystem, gasParticleCount, gasLifetime, gasSpeed, gasSize, random, false);
            EmitCloud(
                _ribbonSystem,
                ribbonParticleCount,
                ribbonLifetime,
                ribbonSpeed,
                ribbonSize,
                random,
                true);

            _volumeStartTime = Time.time;
            _volumeSeed = Mathf.Lerp(-20f, 20f, Next01(random));
            _volumeProgress = 0f;
            _volumeActive = _volumeRenderer != null && _volumeRenderer.sharedMaterial != null;
            if (_volumeRenderer != null)
                _volumeRenderer.enabled = _volumeActive;
            if (_volumeTransform != null)
            {
                _volumeTransform.localRotation = Quaternion.Euler(
                    Next01(random) * 180f,
                    Next01(random) * 360f,
                    Next01(random) * 180f);
            }

            UpdateVolumePresentation(0f);
        }

        /// <summary>
        /// Editor and showcase hook for inspecting an exact lifecycle moment
        /// without depending on frame timing.
        /// </summary>
        public void PreviewVolumeProgress(float progress01)
        {
            EnsureBuilt();
            PositionAtOrigin();
            _volumeSeed = GetInstanceID() * 0.0137f;
            _volumeProgress = Mathf.Clamp01(progress01);
            _volumeActive = _volumeProgress < 1f &&
                            _volumeRenderer != null &&
                            _volumeRenderer.sharedMaterial != null;
            if (_volumeRenderer != null)
                _volumeRenderer.enabled = _volumeActive;
            UpdateVolumePresentation(_volumeProgress);
        }

        private void EnsureBuilt()
        {
            if (_effectRoot == null)
            {
                GameObject rootObject = new GameObject("Amplifier Aurora Blast");
                rootObject.layer = gameObject.layer;
                _effectRoot = rootObject.transform;
                _effectRoot.SetParent(transform, false);
            }

            if (_volumeRenderer == null)
                CreateVolumeCloud();
            if (_gasSystem == null)
                _gasSystem = CreateParticleSystem("Cloud Breakup", false);
            if (_ribbonSystem == null)
                _ribbonSystem = CreateParticleSystem("Aurora Filaments", true);
        }

        private void CreateVolumeCloud()
        {
            GameObject volumeObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            volumeObject.name = "Volumetric Energy Cloud";
            volumeObject.layer = gameObject.layer;
            volumeObject.transform.SetParent(_effectRoot, false);
            _volumeTransform = volumeObject.transform;

            Collider generatedCollider = volumeObject.GetComponent<Collider>();
            if (generatedCollider != null)
            {
                generatedCollider.enabled = false;
                Destroy(generatedCollider);
            }

            _volumeRenderer = volumeObject.GetComponent<MeshRenderer>();
            _volumeRenderer.sharedMaterial = ResolveVolumeMaterial();
            _volumeRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _volumeRenderer.receiveShadows = false;
            _volumeRenderer.lightProbeUsage = LightProbeUsage.Off;
            _volumeRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _volumeRenderer.enabled = false;
            _volumeProperties = new MaterialPropertyBlock();
        }

        private ParticleSystem CreateParticleSystem(string objectName, bool ribbons)
        {
            GameObject child = new GameObject(objectName);
            child.layer = gameObject.layer;
            child.transform.SetParent(_effectRoot, false);

            ParticleSystem system = child.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = ribbons ? 32 : 48;
            main.startSpeed = 0f;
            main.startSize = 1f;
            main.startLifetime = 1f;
            main.gravityModifier = 0f;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.High;
            noise.frequency = ribbons ? 0.37f : 0.28f;
            noise.strength = ribbons ? 0.56f : 0.82f;
            noise.scrollSpeed = ribbons ? 0.54f : 0.33f;
            noise.damping = true;
            noise.separateAxes = true;
            noise.strengthX = ribbons ? 0.42f : 0.76f;
            noise.strengthY = ribbons ? 0.3f : 0.48f;
            noise.strengthZ = ribbons ? 0.62f : 0.76f;

            Gradient alphaGradient = new Gradient();
            alphaGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.7f, 0.12f),
                    new GradientAlphaKey(0.42f, ribbons ? 0.58f : 0.5f),
                    new GradientAlphaKey(0f, 1f)
                });
            ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
                system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(alphaGradient);

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f,
                ribbons
                    ? new AnimationCurve(
                        new Keyframe(0f, 0.08f),
                        new Keyframe(0.24f, 1f),
                        new Keyframe(1f, 0f))
                    : new AnimationCurve(
                        new Keyframe(0f, 0.18f),
                        new Keyframe(0.34f, 1f),
                        new Keyframe(1f, 0.18f)));

            ParticleSystem.TrailModule trails = system.trails;
            trails.enabled = ribbons;
            if (ribbons)
            {
                trails.mode = ParticleSystemTrailMode.PerParticle;
                trails.ratio = 0.72f;
                trails.lifetime = 0.2f;
                trails.dieWithParticles = true;
                trails.sizeAffectsWidth = true;
                trails.inheritParticleColor = true;
            }

            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ribbons
                ? ParticleSystemRenderMode.Stretch
                : ParticleSystemRenderMode.Billboard;
            renderer.velocityScale = ribbons ? 0.06f : 0f;
            renderer.lengthScale = ribbons ? 0.9f : 1f;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.sharedMaterial = ResolveParticleMaterial();
            if (ribbons)
                renderer.trailMaterial = ResolveParticleMaterial();

            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return system;
        }

        private void EmitCloud(
            ParticleSystem system,
            int count,
            float lifetime,
            float speed,
            float size,
            System.Random random,
            bool ribbons)
        {
            int colorCount = palette != null ? palette.Length : 0;
            for (int i = 0; i < count; i++)
            {
                float angle = ((i + Next01(random)) / Mathf.Max(1, count)) * Mathf.PI * 2f;
                float vertical = Mathf.Lerp(
                    ribbons ? -0.28f : -0.54f,
                    ribbons ? 0.4f : 0.62f,
                    Next01(random));
                Vector3 direction = new Vector3(
                    Mathf.Cos(angle),
                    vertical,
                    Mathf.Sin(angle)).normalized;

                float speedVariation = Mathf.Lerp(0.55f, 1.14f, Next01(random));
                float sizeVariation = Mathf.Lerp(0.62f, 1.32f, Next01(random));
                float lifeVariation = Mathf.Lerp(0.8f, 1.2f, Next01(random));

                var emit = new ParticleSystem.EmitParams
                {
                    position = direction * Mathf.Lerp(0.02f, 0.16f, Next01(random)),
                    velocity = direction * (speed * speedVariation),
                    startLifetime = lifetime * lifeVariation,
                    startSize = size * sizeVariation,
                    startColor = colorCount > 0
                        ? palette[(i + _playSequence) % colorCount]
                        : Color.white
                };
                system.Emit(emit, 1);
            }
        }

        private void PositionAtOrigin()
        {
            if (_effectRoot == null)
                return;

            Transform source = origin != null ? origin : transform;
            _effectRoot.position = source.position;
            _effectRoot.rotation = source.rotation;
            _volumeOrigin = source.position;

            Vector3 towardArena = -source.position;
            towardArena.y = 0f;
            _volumeInwardDirection = towardArena.sqrMagnitude > 0.0001f
                ? towardArena.normalized
                : Vector3.zero;

            // Goal hitboxes are flattened. Counter-scale the hierarchy so the
            // ray-marched volume and its noise remain truly three-dimensional.
            Vector3 parentScale = transform.lossyScale;
            _effectRoot.localScale = new Vector3(
                ReciprocalScale(parentScale.x),
                ReciprocalScale(parentScale.y),
                ReciprocalScale(parentScale.z));
        }

        private void UpdateVolumePresentation(float progress01)
        {
            if (_volumeRenderer == null || _volumeTransform == null)
                return;

            float progress = Mathf.Clamp01(progress01);
            float expansion01 = EaseOutCubic(Mathf.Clamp01(progress / 0.7f));
            float dissipation01 = SmoothStep01(Mathf.InverseLerp(0.62f, 1f, progress));
            float diameter = Mathf.Lerp(
                volumeStartDiameter,
                volumeMaximumDiameter,
                expansion01);
            diameter *= Mathf.Lerp(1f, 0.82f, dissipation01);
            _volumeTransform.localScale = Vector3.one * diameter;
            _volumeTransform.position = _volumeOrigin +
                                        _volumeInwardDirection *
                                        (volumeInwardDrift * expansion01);

            float appear = SmoothStep01(Mathf.InverseLerp(0f, 0.1f, progress));
            float disappear = 1f - SmoothStep01(Mathf.InverseLerp(0.56f, 1f, progress));
            float opacity = appear * disappear;

            _volumeRenderer.GetPropertyBlock(_volumeProperties);
            _volumeProperties.SetFloat(OpacityId, opacity);
            _volumeProperties.SetFloat(BlastProgressId, progress);
            _volumeProperties.SetFloat(SeedId, _volumeSeed);
            _volumeRenderer.SetPropertyBlock(_volumeProperties);
        }

        private Material ResolveParticleMaterial()
        {
            if (particleMaterial != null)
                return particleMaterial;
            if (_runtimeFallbackParticleMaterial != null)
                return _runtimeFallbackParticleMaterial;

            Shader shader = Shader.Find("MASSIVE/Amplifier Core/Aurora Particle");
            if (shader == null)
                shader = Shader.Find("Particles/Additive");
            if (shader != null)
            {
                _runtimeFallbackParticleMaterial = new Material(shader)
                {
                    name = "Amplifier Aurora Runtime Particle Material"
                };
            }

            return _runtimeFallbackParticleMaterial;
        }

        private Material ResolveVolumeMaterial()
        {
            if (volumeMaterial != null)
                return volumeMaterial;
            if (_runtimeFallbackVolumeMaterial != null)
                return _runtimeFallbackVolumeMaterial;

            Shader shader = Shader.Find("MASSIVE/Amplifier Core/Aurora Volume");
            if (shader != null)
            {
                _runtimeFallbackVolumeMaterial = new Material(shader)
                {
                    name = "Amplifier Aurora Runtime Volume Material"
                };
            }

            return _runtimeFallbackVolumeMaterial;
        }

        private static float Next01(System.Random random)
        {
            return (float)random.NextDouble();
        }

        private static float ReciprocalScale(float value)
        {
            return Mathf.Abs(value) > 0.0001f ? 1f / Mathf.Abs(value) : 1f;
        }

        private static float SmoothStep01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private static float EaseOutCubic(float value)
        {
            value = Mathf.Clamp01(value);
            float inverse = 1f - value;
            return 1f - inverse * inverse * inverse;
        }
    }
}
