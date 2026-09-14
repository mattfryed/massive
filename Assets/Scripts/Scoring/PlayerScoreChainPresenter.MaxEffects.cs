using UnityEngine;
using UnityEngine.Rendering;

namespace Massive.Scoring
{
    public sealed partial class PlayerScoreChainPresenter
    {
        [Header("Maximum Multiplier Effects")]
        [SerializeField] private bool maximumEffectsEnabled = true;
        [SerializeField] private Material maximumGlowMaterial;
        [SerializeField] private Color maximumEffectColor = Color.white;
        [SerializeField, Min(0.05f)] private float maximumPulseFrequency = 0.8f;
        [SerializeField, Range(0f, 1f)] private float maximumGlowOpacity = 0.32f;
        [Tooltip("Glow reach as a fraction of the bar's height.")]
        [SerializeField, Range(0f, 1f)] private float maximumGlowSpread = 0.45f;
        [SerializeField, Min(0.1f)] private float maximumSweepSeconds = 1.7f;

        private static readonly int GlowTintId = Shader.PropertyToID("_Tint");
        private static readonly int BarSizeId = Shader.PropertyToID("_BarSize");
        private static readonly int GlowStrengthId = Shader.PropertyToID("_GlowStrength");
        private static readonly int SweepPhaseId = Shader.PropertyToID("_SweepPhase");
        private GameObject _maximumEffectsRoot;
        private MeshRenderer _maximumGlowRenderer;
        private Mesh _maximumGlowMesh;
        private MaterialPropertyBlock _maximumGlowProperties;
        private float _maximumEffectsStartedAt;

        public bool MaximumEffectsActive => _maximumEffectsRoot != null && _maximumEffectsRoot.activeInHierarchy;

        private void UpdateMaximumEffects()
        {
            if (!maximumEffectsEnabled || maximumGlowMaterial == null || progressRectangle == null ||
                chain == null || !chain.IsAtMaxMultiplier || _displayedProgress < 0.999f)
            {
                HideMaximumEffects();
                return;
            }

            EnsureMaximumEffects();
            if (!_maximumEffectsRoot.activeSelf)
            {
                _maximumEffectsStartedAt = Time.unscaledTime;
                _maximumEffectsRoot.SetActive(true);
            }

            float elapsed = Time.unscaledTime - _maximumEffectsStartedAt;
            float pulse = 0.7f + 0.3f * Mathf.Sin(elapsed * Mathf.PI * 2f * maximumPulseFrequency);
            float height = progressRectangle.Height;
            float width = progressRectangle.Width;
            float spread = Mathf.Max(0.001f, height * maximumGlowSpread);
            Vector3 center = progressRectangle.Pivot == Shapes.RectPivot.Corner
                ? new Vector3(width * 0.5f, height * 0.5f, 0f)
                : Vector3.zero;

            // One soft glow behind the opaque bar keeps its black digits crisp.
            _maximumEffectsRoot.transform.localPosition = center + Vector3.forward * 0.02f;
            _maximumEffectsRoot.transform.localScale = new Vector3(width + spread * 2f, height + spread * 2f, 1f);
            _maximumGlowProperties.SetVector(BarSizeId, new Vector4(width, height, spread, 0f));
            _maximumGlowProperties.SetColor(GlowTintId, maximumEffectColor);
            _maximumGlowProperties.SetVector(GlowStrengthId, new Vector4(maximumGlowOpacity, pulse, 0f, 0f));
            _maximumGlowProperties.SetFloat(SweepPhaseId, Mathf.Repeat(elapsed / Mathf.Max(0.1f, maximumSweepSeconds), 1f));
            _maximumGlowRenderer.SetPropertyBlock(_maximumGlowProperties);
        }

        private void EnsureMaximumEffects()
        {
            if (_maximumEffectsRoot != null) return;
            _maximumEffectsRoot = new GameObject("Maximum multiplier glow");
            _maximumEffectsRoot.layer = progressRectangle.gameObject.layer;
            _maximumEffectsRoot.transform.SetParent(progressRectangle.transform, false);
            _maximumEffectsRoot.SetActive(false);
            _maximumGlowMesh = new Mesh { name = "Multiplier glow quad" };
            _maximumGlowMesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f)
            };
            _maximumGlowMesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            _maximumGlowMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            _maximumGlowMesh.RecalculateBounds();
            _maximumEffectsRoot.AddComponent<MeshFilter>().sharedMesh = _maximumGlowMesh;
            _maximumGlowRenderer = _maximumEffectsRoot.AddComponent<MeshRenderer>();
            _maximumGlowRenderer.sharedMaterial = maximumGlowMaterial;
            _maximumGlowRenderer.sortingLayerID = progressRectangle.SortingLayerID;
            _maximumGlowRenderer.sortingOrder = progressRectangle.SortingOrder;
            _maximumGlowRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _maximumGlowRenderer.receiveShadows = false;
            _maximumGlowRenderer.lightProbeUsage = LightProbeUsage.Off;
            _maximumGlowRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _maximumGlowProperties = new MaterialPropertyBlock();
        }

        private void HideMaximumEffects()
        {
            if (_maximumEffectsRoot != null && _maximumEffectsRoot.activeSelf)
                _maximumEffectsRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_maximumEffectsRoot != null) Destroy(_maximumEffectsRoot);
            if (_maximumGlowMesh != null) Destroy(_maximumGlowMesh);
        }
    }
}
