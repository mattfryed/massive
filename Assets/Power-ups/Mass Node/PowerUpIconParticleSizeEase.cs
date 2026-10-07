using System.Collections;
using UnityEngine;

namespace Massive.PowerUps
{
    /// <summary>
    /// Scales the inner icon, including already-live particles, on the pickup's timeline.
    /// </summary>
    [DisallowMultipleComponent]
    public class PowerUpIconParticleSizeEase : MonoBehaviour
    {
        [Header("Particle Systems to Animate (all inner icon layers)")]
        [SerializeField] private ParticleSystem[] systems;

        [Header("Timing")]
        [SerializeField, Min(0.01f)] private float spawnSeconds = 0.25f;
        [SerializeField, Min(0.01f)] private float despawnSeconds = 0.18f;

        [Header("Ease")]
        [SerializeField] private AnimationCurve ease01 = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Options")]
        [Tooltip("If true, PlaySpawn() runs automatically on OnEnable (works with Instantiate).")]
        [SerializeField] private bool autoPlayOnEnable = true;

        [Tooltip("Clears existing particles at spawn so it ramps in cleanly.")]
        [SerializeField] private bool clearOnSpawn = true;

        [Tooltip("Stops emission after shrinking to 0 on despawn.")]
        [SerializeField] private bool stopAfterDespawn = true;

        private Vector3 _authoredScale;
        private bool _cached;
        private float _size01 = 1f;
        private float _acquireSize;
        private float _globalScale = 1f;
        private Coroutine _co;
        public float SpawnDuration => spawnSeconds;
        public float DespawnDuration => despawnSeconds;

        private void Reset()
        {
            // Only the icon's systems belong here; the shell has its own animation.
            systems = GetComponentsInChildren<ParticleSystem>(true);
        }

        private void Awake()
        {
            CacheDefaultsIfNeeded();
        }

        private void OnEnable()
        {
            if (autoPlayOnEnable && !GetComponentInParent<PowerUpIconManifestAnimator>())
                PlaySpawn();
        }

        private void CacheDefaultsIfNeeded()
        {
            if (_cached) return;
            _cached = true;
            _authoredScale = transform.localScale;
            if (systems == null || systems.Length == 0)
                systems = GetComponentsInChildren<ParticleSystem>(true);

            for (int i = 0; i < systems.Length; i++)
            {
                var ps = systems[i];
                if (!ps) continue;
                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
        }

        private void ApplySize01(float s01)
        {
            CacheDefaultsIfNeeded();
            _size01 = Mathf.Clamp01(s01);
            transform.localScale = _authoredScale * (_size01 * _globalScale);
        }

        public void ApplyPickupSettings(PowerUpVisualSettings settings)
        {
            CacheDefaultsIfNeeded(); _globalScale = settings.innerScale;
            spawnSeconds = settings.particleSpawnSeconds; despawnSeconds = settings.particleAcquireSeconds;
            ApplySize01(_size01);
        }

        public void SetHiddenInstant() => ApplySize01(0f);

        public void SampleSpawnTime(float seconds)
        {
            float u = Mathf.Clamp01(seconds / Mathf.Max(0.0001f, spawnSeconds));
            ApplySize01(u <= 0f ? 0f : u >= 1f ? 1f : ease01.Evaluate(u));
        }

        public void BeginAcquireDespawn() => _acquireSize = _size01;

        public void SampleAcquireTime(float seconds)
        {
            ApplySize01(_acquireSize * (1f - Mathf.SmoothStep(0f, 1f, seconds / Mathf.Max(0.0001f, despawnSeconds))));
        }

        public void PrepareSpawn()
        {
            CacheDefaultsIfNeeded();
            if (_co != null) StopCoroutine(_co);
            ApplySize01(0f);
            foreach (var ps in systems)
            {
                if (!ps) continue;
                if (clearOnSpawn) ps.Clear(true);
                ps.Play(true);
            }
        }

        public void PlaySpawn()
        {
            PrepareSpawn();
            _co = StartCoroutine(AnimateSizeRoutine(target01: 1f, seconds: spawnSeconds, stopAtEnd: false));
        }

        public void PlayDespawn()
        {
            CacheDefaultsIfNeeded();

            if (_co != null) StopCoroutine(_co);
            _co = StartCoroutine(AnimateSizeRoutine(target01: 0f, seconds: despawnSeconds, stopAtEnd: stopAfterDespawn));
        }

        private IEnumerator AnimateSizeRoutine(float target01, float seconds, bool stopAtEnd)
        {
            float start01 = _size01;
            float t = 0f;
            seconds = Mathf.Max(0.0001f, seconds);

            while (t < seconds)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / seconds);
                float eased = (ease01 != null) ? ease01.Evaluate(u) : Mathf.SmoothStep(0f, 1f, u);

                ApplySize01(Mathf.Lerp(start01, target01, eased));
                yield return null;
            }

            ApplySize01(target01);

            if (stopAtEnd && target01 <= 0.001f)
            {
                for (int i = 0; i < systems.Length; i++)
                {
                    var ps = systems[i];
                    if (!ps) continue;
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }

            _co = null;
        }
    }
}
