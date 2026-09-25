using UnityEngine;

namespace Massive.Singularity
{
    /// <summary>Opt-in brightness for ordinary surface-bound mesh renderers.
    /// The owner transform stays in the unrolled physics chart. Compatible shaders
    /// multiply RGB by _SingularityBrightness, leaving transparency/depth unchanged.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(600)]
    public sealed class SingularityRendererBrightness : MonoBehaviour
    {
        [SerializeField] private SingularitySurface surface;
        [SerializeField] private Renderer[] targets;
        private MaterialPropertyBlock properties;
        private static readonly int BrightnessId = Shader.PropertyToID("_SingularityBrightness");
        public float CurrentBrightness { get; private set; } = 1f;

        private void OnEnable() { Cache(); RefreshPresentation(); }
        private void OnDisable() { Apply(1f); }
        private void LateUpdate() { RefreshPresentation(); }
        private void Cache()
        {
            if (targets == null || targets.Length == 0) targets = GetComponentsInChildren<Renderer>(true);
            if (properties == null) properties = new MaterialPropertyBlock();
        }
        public void Configure(SingularitySurface value)
        {
            surface = value; Cache(); RefreshPresentation();
        }
        public void RefreshPresentation()
        {
            float brightness = 1f;
            if (isActiveAndEnabled && surface != null)
            {
                Vector3 local = surface.transform.InverseTransformPoint(transform.position);
                brightness = surface.EvaluateBrightness(local.z + surface.FrontHeight * .5f);
            }
            Apply(brightness);
        }
        private void Apply(float brightness)
        {
            CurrentBrightness = brightness;
            if (properties == null || targets == null) return;
            foreach (var target in targets)
            {
                if (target == null) continue;
                // Shell coverage and timeout warning also use this block. Always
                // merge with the live values rather than restoring an old snapshot.
                target.GetPropertyBlock(properties);
                properties.SetFloat(BrightnessId, brightness);
                target.SetPropertyBlock(properties);
            }
        }
    }
}
