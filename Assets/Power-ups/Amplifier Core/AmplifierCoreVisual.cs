using UnityEngine;

namespace Massive.Multiplier
{
    /// <summary>
    /// Presentation-only motion and lifecycle reveal for the Amplifier Core.
    /// Gameplay ownership and multiplier state remain in the scoring system.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AmplifierCoreVisual : MonoBehaviour
    {
        private static readonly int CellScaleId = Shader.PropertyToID("_CellScale");
        private static readonly int BandWidthId = Shader.PropertyToID("_BandWidth");

        [Header("Visual Parts")]
        [SerializeField] private Transform energyCore;
        [SerializeField] private Transform neutralShell;
        [SerializeField] private Renderer energyRenderer;
        [SerializeField] private Renderer shellRenderer;

        [Header("Idle Motion")]
        [SerializeField] private Vector3 energyRotationAxis = new Vector3(0.35f, 0.9f, 0.15f);
        [SerializeField] private float energyRotationSpeed = -17f;
        [SerializeField] private Vector3 shellRotationAxis = new Vector3(-0.2f, 0.45f, 1f);
        [SerializeField] private float shellRotationSpeed = 11f;
        [SerializeField, Range(0f, 0.1f)] private float breatheAmplitude = 0.025f;
        [SerializeField, Min(0f)] private float breatheFrequency = 0.85f;

        [Header("Lifecycle Reveal")]
        [SerializeField, Min(1.5f)] private float spawnOrganicRibbonScale = 1.5f;

        private Vector3 _energyBaseScale;
        private Vector3 _shellBaseScale;
        private float _restingRibbonScale = 5.75f;
        private float _restingShellCoverage = 0.93f;
        private float _phase;
        private float _excitement01;
        private float _coreReveal01 = 1f;
        private float _shellReveal01 = 1f;
        private bool _energyRendererWasEnabled = true;
        private bool _shellRendererWasEnabled = true;
        private bool _hasBaseline;
        private MaterialPropertyBlock _shellProperties;

        public float Excitement01 => _excitement01;
        public float CoreReveal01 => _coreReveal01;
        public float ShellReveal01 => _shellReveal01;
        public float RestingRibbonScale => _restingRibbonScale;
        public float RestingShellCoverage => _restingShellCoverage;

        private void Awake()
        {
            ResolveRenderers();
            CaptureBaseline();
            _phase = Mathf.Abs(GetInstanceID() * 0.00137f) % (Mathf.PI * 2f);
        }

        private void OnEnable()
        {
            ResolveRenderers();
            CaptureBaseline();
        }

        private void OnDisable()
        {
            RestorePresentation();
        }

        private void OnValidate()
        {
            breatheAmplitude = Mathf.Clamp(breatheAmplitude, 0f, 0.1f);
            breatheFrequency = Mathf.Max(0f, breatheFrequency);
            spawnOrganicRibbonScale = Mathf.Max(1.5f, spawnOrganicRibbonScale);
            ResolveRenderers();
        }

        private void Update()
        {
            if (!_hasBaseline)
                CaptureBaseline();

            float speedScale = Mathf.Lerp(1f, 2.4f, _excitement01);
            float deltaTime = Time.deltaTime;

            if (energyCore != null)
            {
                energyCore.Rotate(
                    energyRotationAxis.normalized,
                    energyRotationSpeed * speedScale * deltaTime,
                    Space.Self);
            }

            if (neutralShell != null)
            {
                neutralShell.Rotate(
                    shellRotationAxis.normalized,
                    shellRotationSpeed * speedScale * deltaTime,
                    Space.Self);
            }

            float pulseAmount = breatheAmplitude * Mathf.Lerp(1f, 2f, _excitement01);
            float pulse = 1f + Mathf.Sin(
                (Time.time * breatheFrequency * Mathf.PI * 2f) + _phase) * pulseAmount;

            if (energyCore != null)
                energyCore.localScale = _energyBaseScale * (pulse * _coreReveal01);

            if (neutralShell != null)
            {
                float shellPulse = 1f + ((pulse - 1f) * 0.22f);
                neutralShell.localScale = _shellBaseScale * shellPulse;
            }
        }

        public void SetExcitement(float normalizedExcitement)
        {
            _excitement01 = Mathf.Clamp01(normalizedExcitement);
        }

        /// <summary>
        /// Drives the staggered lifecycle reveal. At zero the shell has no
        /// coverage and broad organic cells; at one it exactly matches the
        /// material values authored in the Inspector.
        /// </summary>
        public void SetLifecycleReveal(float shellReveal01, float coreReveal01)
        {
            if (!_hasBaseline)
                CaptureBaseline();

            _shellReveal01 = Mathf.Clamp01(shellReveal01);
            _coreReveal01 = Mathf.Clamp01(coreReveal01);

            if (shellRenderer != null)
                shellRenderer.enabled = _shellRendererWasEnabled;
            if (energyRenderer != null)
                energyRenderer.enabled = _energyRendererWasEnabled && _coreReveal01 > 0.001f;

            ApplyShellRevealProperties();
        }

        public void PrepareSpawnPresentation()
        {
            SetLifecycleReveal(0f, 0f);
        }

        public void RestorePresentation()
        {
            if (!_hasBaseline)
                return;

            _shellReveal01 = 1f;
            _coreReveal01 = 1f;
            _excitement01 = 0f;

            if (energyCore != null)
                energyCore.localScale = _energyBaseScale;
            if (neutralShell != null)
                neutralShell.localScale = _shellBaseScale;
            if (energyRenderer != null)
                energyRenderer.enabled = _energyRendererWasEnabled;
            if (shellRenderer != null)
                shellRenderer.enabled = _shellRendererWasEnabled;

            ApplyShellRevealProperties();
        }

        private void ResolveRenderers()
        {
            if (energyRenderer == null && energyCore != null)
                energyRenderer = energyCore.GetComponent<Renderer>();
            if (shellRenderer == null && neutralShell != null)
                shellRenderer = neutralShell.GetComponent<Renderer>();
        }

        private void CaptureBaseline()
        {
            if (_hasBaseline)
                return;

            ResolveRenderers();
            _energyBaseScale = energyCore != null ? energyCore.localScale : Vector3.one;
            _shellBaseScale = neutralShell != null ? neutralShell.localScale : Vector3.one;
            _energyRendererWasEnabled = energyRenderer == null || energyRenderer.enabled;
            _shellRendererWasEnabled = shellRenderer == null || shellRenderer.enabled;

            Material shellMaterial = shellRenderer != null ? shellRenderer.sharedMaterial : null;
            if (shellMaterial != null)
            {
                if (shellMaterial.HasProperty(CellScaleId))
                    _restingRibbonScale = shellMaterial.GetFloat(CellScaleId);
                if (shellMaterial.HasProperty(BandWidthId))
                    _restingShellCoverage = shellMaterial.GetFloat(BandWidthId);
            }

            _shellProperties = new MaterialPropertyBlock();
            _hasBaseline = true;
        }

        private void ApplyShellRevealProperties()
        {
            if (shellRenderer == null || _shellProperties == null)
                return;

            shellRenderer.GetPropertyBlock(_shellProperties);
            Material material = shellRenderer.sharedMaterial;
            float eased = SmoothStep01(_shellReveal01);

            if (material != null && material.HasProperty(CellScaleId))
            {
                _shellProperties.SetFloat(
                    CellScaleId,
                    Mathf.Lerp(spawnOrganicRibbonScale, _restingRibbonScale, eased));
            }

            if (material != null && material.HasProperty(BandWidthId))
            {
                _shellProperties.SetFloat(
                    BandWidthId,
                    Mathf.Lerp(0f, _restingShellCoverage, eased));
            }

            shellRenderer.SetPropertyBlock(_shellProperties);
        }

        private static float SmoothStep01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - (2f * value));
        }
    }
}
