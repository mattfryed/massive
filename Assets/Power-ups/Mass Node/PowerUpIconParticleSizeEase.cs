using System.Collections;
using UnityEngine;

namespace Massive.PowerUps
{
    /// <summary>
    /// Scales particle Start Size Multiplier from 0 -> default on spawn, and default -> 0 on despawn.
    /// Intended for power-up icon VFX (eg two particle systems inside the cage).
    /// </summary>
    [DisallowMultipleComponent]
    public class PowerUpIconParticleSizeEase : MonoBehaviour
    {
        [Header("Particle Systems to Animate (assign the 2 icon systems)")]
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

        private float[] _defaultStartSizeMul;
        private float _size01 = 1f;
        private Coroutine _co;

        private void Reset()
        {
            // Auto-grab, but I strongly recommend explicitly assigning ONLY the 2 icon systems.
            systems = GetComponentsInChildren<ParticleSystem>(true);
        }

        private void Awake()
        {
            CacheDefaultsIfNeeded();
        }

        private void OnEnable()
        {
            if (autoPlayOnEnable)
                PlaySpawn();
        }

        private void CacheDefaultsIfNeeded()
        {
            if (systems == null || systems.Length == 0)
                systems = GetComponentsInChildren<ParticleSystem>(true);

            _defaultStartSizeMul = new float[systems.Length];
            for (int i = 0; i < systems.Length; i++)
            {
                var ps = systems[i];
                if (!ps) { _defaultStartSizeMul[i] = 1f; continue; }

                var main = ps.main;
                _defaultStartSizeMul[i] = main.startSizeMultiplier;
            }
        }

        private void ApplySize01(float s01)
        {
            _size01 = Mathf.Clamp01(s01);

            if (_defaultStartSizeMul == null || _defaultStartSizeMul.Length != systems.Length)
                CacheDefaultsIfNeeded();

            for (int i = 0; i < systems.Length; i++)
            {
                var ps = systems[i];
                if (!ps) continue;

                var main = ps.main;
                main.startSizeMultiplier = _defaultStartSizeMul[i] * _size01;
            }
        }

        public void PlaySpawn()
        {
            CacheDefaultsIfNeeded();

            if (_co != null) StopCoroutine(_co);

            // Snap to 0 first
            ApplySize01(0f);

            // Clear + play so we grow in from nothing
            for (int i = 0; i < systems.Length; i++)
            {
                var ps = systems[i];
                if (!ps) continue;

                if (clearOnSpawn) ps.Clear(true);
                ps.Play(true);
            }

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
