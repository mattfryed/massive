using UnityEngine;

namespace Massive.Enemies
{
    /// <summary>Outline warning; the owning director advances it on the batch gameplay clock.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed partial class EnemySpawnTelegraph : MonoBehaviour
    {
        [Min(.01f)] public float fadeInSeconds = .35f;
        [Min(.01f)] public float fadeOutSeconds = .45f;
        public float rollDegreesPerSecond = 32f;
        [Range(0f, .15f)] public float breathScale = .035f;
        [Min(0f)] public float breathFrequency = .7f;
        public float Age { get; private set; }
        public float Opacity { get; private set; }
        public bool IsCompleting => _finishAge >= 0f;

        private MeshRenderer _renderer;
        private MaterialPropertyBlock _properties;
        private Vector3 _baseScale;
        private Quaternion _baseRotation;
        private float _finishAge = -1f, _finishOpacity;
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");

        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            _properties = new MaterialPropertyBlock();
            _baseScale = transform.localScale;
            _baseRotation = transform.localRotation;
            InitializeGhosts();
        }

        public void Begin()
        {
            Age = 0f; _finishAge = -1f;
            _renderer.enabled = true; SetOpacity(0f);
            ResetGhosts();
        }

        public bool Advance(float delta)
        {
            delta = Mathf.Max(0f, delta); Age += delta;
            float fade = 0f;
            if (IsCompleting)
            {
                _finishAge += delta;
                fade = Mathf.Clamp01(_finishAge / Mathf.Max(.01f, fadeOutSeconds));
                SetOpacity(_finishOpacity * (1f - Mathf.SmoothStep(0f, 1f, fade)));
                if (fade >= 1f) { Cancel(); return false; }
            }
            else SetOpacity(Mathf.SmoothStep(0f, 1f, Age / Mathf.Max(.01f, fadeInSeconds)));
            float breath = Mathf.Sin(Age * Mathf.PI * 2f * breathFrequency);
            transform.localScale = _baseScale * (1f + breathScale * breath + .1f * fade);
            transform.localRotation = _baseRotation * Quaternion.AngleAxis(Age * rollDegreesPerSecond, Vector3.forward);
            AdvanceGhosts(delta);
            return true;
        }

        public void Complete()
        {
            if (IsCompleting) return;
            _finishOpacity = Opacity; _finishAge = 0f;
        }

        public void Cancel()
        {
            if (_renderer) _renderer.enabled = false;
            HideGhosts();
            Destroy(gameObject);
        }

        private void SetOpacity(float value)
        {
            Opacity = value;
            _properties.SetFloat(OpacityId, value);
            _renderer.SetPropertyBlock(_properties);
        }
    }
}
