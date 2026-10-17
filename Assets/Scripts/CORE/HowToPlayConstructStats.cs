using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Author all construct stat fills on the pane root, in percent of their track width.</summary>
[ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(-10600)]
[AddComponentMenu("MASSIVE/How To Play/Construct Stat Bars")]
public sealed class HowToPlayConstructStats : MonoBehaviour
{
    [Serializable]
    public sealed class Construct
    {
        public string label;
        [Range(0f, 100f)] public float health, attack, speed, range, score;
        [HideInInspector] public HowToPlayStatBarIdle[] bars = new HowToPlayStatBarIdle[5];

        public void Apply()
        {
            ApplyBar(0, health); ApplyBar(1, attack); ApplyBar(2, speed); ApplyBar(3, range); ApplyBar(4, score);
        }

        private void ApplyBar(int index, float percent)
        {
            if (bars == null || index >= bars.Length || !bars[index]) return;
            var bar = bars[index];
            float amount = Mathf.Clamp01(percent / 100f);
            bar.SetAuthoredWidth(bar.MaximumWidth * amount);
            // Hide zero fills completely; enabling a former zero also starts its native idle animation.
            if (bar.gameObject.activeSelf != (amount > 0f)) bar.gameObject.SetActive(amount > 0f);
        }
    }

    [SerializeField] private Construct[] constructs = Array.Empty<Construct>();
    private bool applyPending;

    private void OnEnable() => ApplyValues();
    private void OnValidate() => applyPending = true;
    private void Update() { if (applyPending) ApplyValues(); }

    public void ApplyValues(bool includePersistentAssets = false)
    {
        applyPending = false;
        // Persistent prefab assets are edited by the inspector, not by ExecuteAlways callbacks.
        if (!includePersistentAssets && !gameObject.scene.IsValid()) return;
        foreach (var construct in constructs) construct?.Apply();
    }

    public IEnumerable<HowToPlayStatBarIdle> BoundBars()
    {
        foreach (var construct in constructs)
            if (construct?.bars != null)
                foreach (var bar in construct.bars) if (bar) yield return bar;
    }
}
