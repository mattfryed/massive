using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.Orbital
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class OrbitalCloudVisual : MonoBehaviour
    {
        [Min(.1f)] public float radius = 4.9f;
        [Tooltip("Extra sparse outer reach, relative to radius. Dense inner lobes stay in place.")]
        [Range(0f, .5f)] public float outerTailExtension;
        public float OuterRadius => radius * OrbitalDensity.SupportRadius(outerTailExtension);
        [Range(256, 40000)] public int particleCount = 20000;
        public Material particleMaterial;
        [Tooltip("Full 3D cloud for object displays such as the level-select diorama.")]
        public bool fullVolume;
        public Vector3 volumeTilt = new Vector3(24f, 18f, -14f);
        [Tooltip("Presentation turn of the entire volume, not motion of individual electrons.")]
        public float volumeTurnDegreesPerSecond = 12f;
        [Tooltip("The cut face lies in local XZ; only the far (negative Y) hemisphere remains.")]
        [Range(0f, .35f)] public float hemisphereFraction = .12f;
        public Vector2 lifetime = new Vector2(.09f, .32f);
        public Vector2 pointSize = new Vector2(.035f, .07f);
        [Range(0f, 1f)] public float opacity = .78f;
        public bool useUnscaledTime;
        [Min(0f)] public float sceneIntroSeconds;

        [Header("Formation cycle")]
        public OrbitalFormation initialFormation;
        public bool cycleFormations;
        [Tooltip("Gameplay seconds between transition starts. Pauses with the match.")]
        [Min(.1f)] public float formationIntervalSeconds = 15f;
        [Tooltip("Smooth density redistribution time, included in the interval.")]
        [Min(.01f)] public float formationTransitionSeconds = 2f;

        private GameObject generated;
        private ParticleSystem system;
        private ParticleSystem.Particle[] particles;
        private float[] ages, durations, baseSizes;
        private Vector2[] densityPairs;
        private System.Random random;
        private OrbitalDensity.FaceSampler[] samplers;
        private OrbitalDensity.VolumeSampler[] volumeSamplers;
        private readonly OrbitalFormationCycle cycle = new OrbitalFormationCycle();
        private OrbitalFormation cachedFrom, cachedTo;
        private OrbitalProbabilityCloud gameplay;
        private float volumeTurn, introAge;
        public int VisibleParticleCount => system != null ? system.particleCount : 0;
        public OrbitalFormation CurrentFormation => cycle.From;
        public OrbitalFormation NextFormation => cycle.To;
        public float FormationBlend => cycle.Blend;
        public int TransitionsStarted => cycle.TransitionsStarted;

        private void OnEnable() { Build(); }
        private void OnDisable() { Release(); }
        private void OnDestroy() { Release(); }

        [ContextMenu("Rebuild cloud and restart cycle")]
        public void Rebuild() { Release(); Build(); }

        public float DensityAtNormalized(Vector3 position)
        {
            float from = OrbitalDensity.Evaluate(position, outerTailExtension, cycle.From);
            return cycle.From == cycle.To ? from : Mathf.Lerp(from,
                OrbitalDensity.Evaluate(position, outerTailExtension, cycle.To), cycle.Blend);
        }

        internal void AdvanceFormation(float seconds)
        {
            if (cycleFormations && system != null) cycle.Advance(seconds, formationIntervalSeconds, formationTransitionSeconds);
        }

        private void Build()
        {
            if (generated != null || particleMaterial == null) return;
            random = new System.Random(74113);
            cycle.Reset(initialFormation); cachedFrom = cycle.From; cachedTo = cycle.To;
            gameplay = GetComponent<OrbitalProbabilityCloud>();
            samplers = new OrbitalDensity.FaceSampler[OrbitalFormationCycle.Count];
            volumeSamplers = new OrbitalDensity.VolumeSampler[OrbitalFormationCycle.Count];
            for (int i = 0; i < OrbitalFormationCycle.Count; i++)
            {
                if (!cycleFormations && i != (int)cycle.From) continue;
                if (fullVolume) volumeSamplers[i] = new OrbitalDensity.VolumeSampler(outerTailExtension, (OrbitalFormation)i);
                else samplers[i] = new OrbitalDensity.FaceSampler(false, outerTailExtension, (OrbitalFormation)i);
            }
            volumeTurn = 0f;
            introAge = Application.isPlaying ? 0f : sceneIntroSeconds;
            generated = new GameObject("Electron probability samples (transient)") { hideFlags = HideFlags.HideAndDontSave };
            generated.layer = gameObject.layer;
            generated.transform.SetParent(transform, false);
            system = generated.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false; main.loop = false; main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.maxParticles = Mathf.Clamp(particleCount, 256, 40000); main.startSpeed = 0f;
            var emission = system.emission; emission.enabled = false;
            var shape = system.shape; shape.enabled = false;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = particleMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.minParticleSize = 0f; renderer.maxParticleSize = .1f;
            renderer.sortMode = fullVolume ? ParticleSystemSortMode.Distance : ParticleSystemSortMode.None;
            // Paused systems do not reliably rebuild native bounds after manual particle uploads.
            // Explicit bounds keep the density visible in the actual Game view as well as Camera.Render captures.
            renderer.localBounds = new Bounds(Vector3.zero, Vector3.one * (OuterRadius * 2.1f));
            int count = main.maxParticles;
            particles = new ParticleSystem.Particle[count];
            ages = new float[count]; durations = new float[count]; densityPairs = new Vector2[count]; baseSizes = new float[count];
            for (int i = 0; i < count; i++) { Resample(i); ages[i] = durations[i] * (float)random.NextDouble(); }
            system.Play(true);
            system.Pause(true);
            system.Simulate(0f, false, false, false);
            Tick(0f);
            system.Pause(false);
        }

        private void Update()
        {
            if (system == null) return;
            if (Application.isPlaying)
            {
                float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                // In gameplay, physics advances the formation before evaluating collisions.
                if (gameplay == null) AdvanceFormation(dt);
                Tick(dt);
            }
        }

        private float SamplerWeight(int index)
        {
            // Supports enabling the cycle from the Inspector during a live preview.
            if (fullVolume)
            {
                if (volumeSamplers[index] == null) volumeSamplers[index] = new OrbitalDensity.VolumeSampler(outerTailExtension, (OrbitalFormation)index);
                return volumeSamplers[index].TotalWeight;
            }
            if (samplers[index] == null) samplers[index] = new OrbitalDensity.FaceSampler(false, outerTailExtension, (OrbitalFormation)index);
            return samplers[index].TotalWeight;
        }

        private void CacheDensity(int i, Vector3 point)
        {
            float from = OrbitalDensity.Evaluate(point, outerTailExtension, cycle.From);
            densityPairs[i] = new Vector2(from, cycle.From == cycle.To ? from : OrbitalDensity.Evaluate(point, outerTailExtension, cycle.To));
        }

        private void Resample(int i)
        {
            int selected = (int)cycle.From;
            if (cycle.From != cycle.To)
            {
                // Account for each formation's integrated density, so sampling follows the same mixed field as hits.
                float nextWeight = cycle.Blend * SamplerWeight((int)cycle.To);
                float totalWeight = (1f - cycle.Blend) * SamplerWeight(selected) + nextWeight;
                if (random.NextDouble() * totalWeight < nextWeight) selected = (int)cycle.To;
            }
            SamplerWeight(selected);
            Vector3 p = fullVolume ? volumeSamplers[selected].Sample(random) : samplers[selected].Sample(random);
            bool backing = !fullVolume && i < particles.Length * hemisphereFraction;
            if (backing)
            {
                // Far hemisphere provides depth in Scene View; the face remains the primary density display.
                float extent = OrbitalDensity.SupportRadius(outerTailExtension);
                float depth = Mathf.Sqrt(Mathf.Max(0f, extent * extent - p.sqrMagnitude));
                p.y = -depth * (float)random.NextDouble();
            }
            CacheDensity(i, p);
            particles[i].position = p * radius;
            particles[i].velocity = Vector3.zero;
            baseSizes[i] = Mathf.Lerp(pointSize.x, pointSize.y, (float)random.NextDouble());
            particles[i].startLifetime = particles[i].remainingLifetime = 1000f;
            ages[i] = 0f;
            durations[i] = Mathf.Lerp(Mathf.Max(.03f, lifetime.x), Mathf.Max(.04f, lifetime.y), (float)random.NextDouble());
        }

        private void Tick(float dt)
        {
            introAge += dt;
            float intro = sceneIntroSeconds <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(introAge / sceneIntroSeconds));
            if (fullVolume)
            {
                volumeTurn = Mathf.Repeat(volumeTurn + volumeTurnDegreesPerSecond * dt, 360f);
                generated.transform.localRotation = Quaternion.Euler(volumeTilt) *
                    Quaternion.AngleAxis(volumeTurn, new Vector3(.35f, .8f, .2f).normalized);
            }
            bool changed = cachedFrom != cycle.From || cachedTo != cycle.To;
            for (int i = 0; i < particles.Length; i++)
            {
                if (changed) CacheDensity(i, particles[i].position / radius);
                ages[i] += dt;
                if (ages[i] >= durations[i])
                {
                    // Carry elapsed time into the new sample. Resetting every point to zero alpha
                    // makes the whole cloud vanish when a slow frame exceeds the blink lifetime.
                    float overshoot = ages[i] - durations[i];
                    Resample(i);
                    ages[i] = Mathf.Repeat(overshoot, durations[i]);
                }
                float phase = ages[i] / durations[i];
                Color c = Color.Lerp(DensityColor(0f), DensityColor(Mathf.Lerp(densityPairs[i].x, densityPairs[i].y, cycle.Blend)), intro);
                c.a = opacity * (!fullVolume && i < particles.Length * hemisphereFraction ? .13f : 1f);
                c.a *= Mathf.Clamp01(phase * 12f) * Mathf.Clamp01((1f - phase) * 8f);
                particles[i].startColor = c;
                particles[i].startSize = baseSizes[i] * intro;
            }
            cachedFrom = cycle.From; cachedTo = cycle.To;
            system.SetParticles(particles, particles.Length);
        }

        public static Color DensityColor(float density)
        {
            float t = Mathf.Pow(Mathf.Clamp01(density), .32f);
            if (t < .32f) return Color.Lerp(new Color(.16f, .025f, .42f), new Color(.52f, .075f, .7f), t / .32f);
            if (t < .67f) return Color.Lerp(new Color(.52f, .075f, .7f), new Color(1f, .29f, .055f), (t - .32f) / .35f);
            return Color.Lerp(new Color(1f, .29f, .055f), new Color(1f, .97f, .58f), (t - .67f) / .33f);
        }

        private void Release()
        {
            if (generated != null)
            {
                if (Application.isPlaying) Destroy(generated); else DestroyImmediate(generated);
            }
            generated = null; system = null; particles = null;
            ages = durations = baseSizes = null; densityPairs = null; samplers = null; volumeSamplers = null;
        }
    }
}
