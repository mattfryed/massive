using UnityEngine;

namespace Massive.Resonance
{
    /// <summary>Explicit A/B switch. Deactivate the previous simulation before activating the other.</summary>
    [ExecuteAlways,DisallowMultipleComponent]
    public sealed class ResonanceRenderComparison : MonoBehaviour
    {
        public ResonancePatternController optionA;
        public ResonancePatternController optionB;
        public ResonanceRendering selected = ResonanceRendering.OptionAContinuous;
        private void OnEnable() { Apply(); }
        private void Update() { Apply(); }
        public void Select(ResonanceRendering choice) { selected=choice; Apply(); }
        private void Apply()
        {
            if (optionA==null || optionB==null || optionA==optionB) return;
            var active=selected==ResonanceRendering.OptionAContinuous ? optionA : optionB;
            var inactive=selected==ResonanceRendering.OptionAContinuous ? optionB : optionA;
            if (inactive.gameObject.activeSelf) inactive.gameObject.SetActive(false);
            if (!active.gameObject.activeSelf) active.gameObject.SetActive(true);
        }
    }
}
