using UnityEngine;

namespace Massive.Enemies
{
    /// <summary>Outline warning; the owning director advances it on the batch gameplay clock.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed partial class EnemySpawnTelegraph : MonoBehaviour
    {
        [Min(.01f)] public float fadeInSeconds = .35f;
        [Min(.01f)] public float fadeOutSeconds = .9f;
        [Min(0f)] public float wireframeDelay = .28f;
        [Tooltip("Uniform scale relative to the spawned enemy. Set both values to 1 for an exact size match.")]
        public Vector2 sizeMultiplierRange = Vector2.one;
        [Range(0f, .6f)] public float defocusWidth = .25f;
        [Header("Colors")]
        public Color wireframeColor = new Color(1f, .025f, .012f, 1f);
        public Color glowColor = new Color(1f, .19f, .025f, 1f);
        public Color ghostColor = new Color(1f, .12f, .018f, 1f);
        public float rollDegreesPerSecond = 32f;
        [Range(0f, .15f)] public float breathScale = .035f;
        [Min(0f)] public float breathFrequency = .7f;
        public float Age { get; private set; }
        public float Opacity { get; private set; }
        public float Defocus { get; private set; }
        public bool IsCompleting => _finishAge >= 0f;

        private MeshRenderer _renderer;
        private MaterialPropertyBlock _properties;
        private Vector3 _baseScale, _authoredScale;
        private Quaternion _baseRotation;
        private float _finishAge = -1f, _finishOpacity, _warningSeconds;
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
        private static readonly int DefocusId = Shader.PropertyToID("_Defocus");
        private static readonly int DefocusWidthId = Shader.PropertyToID("_DefocusWidth");

        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            _properties = new MaterialPropertyBlock();
            _authoredScale = _baseScale = transform.localScale;
            _baseRotation = transform.localRotation;
            InitializeOutlineJoins();
            InitializeGhosts();
            InitializeDiffuseGlow();
        }

        public void Begin(float warningSeconds = 3f, Vector3? enemyWorldScale = null)
        {
            Age = 0f; _finishAge = -1f; Defocus = 0f; _warningSeconds = Mathf.Max(.01f, warningSeconds);
            Vector3 parent = transform.parent ? transform.parent.lossyScale : Vector3.one;
            Vector3 world = enemyWorldScale ?? Vector3.Scale(_authoredScale, parent);
            float min = Mathf.Max(.1f, sizeMultiplierRange.x), max = Mathf.Max(min, sizeMultiplierRange.y);
            _baseScale = new Vector3(world.x / Mathf.Max(.0001f, Mathf.Abs(parent.x)),
                world.y / Mathf.Max(.0001f, Mathf.Abs(parent.y)), world.z / Mathf.Max(.0001f, Mathf.Abs(parent.z))) * GhostSample(min, max);
            transform.localScale = _baseScale;
            var bounds = GetComponent<MeshFilter>().sharedMesh.bounds; bounds.Expand(defocusWidth * 6f);
            _renderer.localBounds = bounds;
            _renderer.enabled = true; SetOpacity(0f);
            SetDiffuseGlow(0f);
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
                Defocus = Mathf.SmoothStep(0f, 1f, fade);
                float envelope = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.25f, 1f, fade));
                SetOpacity(_finishOpacity * envelope);
                SetDiffuseGlow(_finishGlowOpacity * envelope);
                if (fade >= 1f) { Cancel(); return false; }
            }
            else
            {
                SetOpacity(Mathf.SmoothStep(0f, 1f, (Age - wireframeDelay) / Mathf.Max(.01f, fadeInSeconds)));
                float reveal = Mathf.SmoothStep(0f, 1f, Age / .18f);
                SetDiffuseGlow(diffuseGlowStrength * reveal * Mathf.Lerp(.15f, 1f, Mathf.Clamp01(Age / _warningSeconds)));
            }
            float breath = Mathf.Sin(Age * Mathf.PI * 2f * breathFrequency);
            transform.localScale = _baseScale * (1f + breathScale * breath);
            transform.localRotation = _baseRotation * Quaternion.AngleAxis(Age * rollDegreesPerSecond, Vector3.forward);
            AdvanceGhosts(delta);
            return true;
        }

        public void Complete()
        {
            if (IsCompleting) return;
            _finishOpacity = Opacity; _finishGlowOpacity = DiffuseGlowOpacity; _finishAge = 0f;
        }

        public void Cancel()
        {
            if (_renderer) _renderer.enabled = false;
            HideGhosts();
            SetDiffuseGlow(0f);
            Destroy(gameObject);
        }

        private void SetOpacity(float value)
        {
            Opacity = value;
            _properties.SetFloat(OpacityId, value);
            _properties.SetColor(ColorId, wireframeColor);
            _properties.SetColor(GlowColorId, glowColor);
            _properties.SetFloat(DefocusId, Defocus);
            _properties.SetFloat(DefocusWidthId, defocusWidth);
            _renderer.SetPropertyBlock(_properties);
        }
    }
}
