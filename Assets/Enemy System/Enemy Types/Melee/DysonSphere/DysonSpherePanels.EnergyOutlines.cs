using Shapes;
using Unity.Mathematics;
using UnityEngine;

namespace Massive.Enemies
{
    public sealed partial class DysonSpherePanels
    {
        [Header("Amplifier energy outlines")]
        public bool energyOutlines;
        [Tooltip("Read-only flow, scale and emission reference from the team-goal Core. Outline hues use the full spectrum independently of its warm-biased palette.")]
        public Material outlineEnergySource;
        [Min(.01f), Tooltip("Multiplier of the source material's pattern scale, across the entire shell.")]
        public float outlineEnergyPatternScale = 1f;
        [Min(0f), Tooltip("Multiplier of the source material's flow speed. Zero freezes color flow.")]
        public float outlineEnergyFlowSpeed = 1f;
        [Range(0f, 1f), Tooltip("Color purity of the full-spectrum noise pattern.")]
        public float outlineEnergySaturation = .95f;
        [Range(0f, 2f)] public float outlineEnergyBrightness = 1f;
        [Range(1, 32), Tooltip("Minimum samples per edge. Finer noise automatically increases sampling up to 32, without changing line width.")]
        public int outlineEnergySamples = 6;

        private static readonly int EnergyScale = Shader.PropertyToID("_PatternScale");
        private static readonly int EnergyFlow = Shader.PropertyToID("_FlowSpeed");
        private static readonly int EnergyEmission = Shader.PropertyToID("_Emission");
        private float _energyScale, _energyTime, _energyEmission01;

        // Read material settings once per draw, never once per vertex. The source is not modified.
        private void PrepareEnergyOutlines(float clock)
        {
            _energyScale = EnergyFloat(EnergyScale, 4.4f) * Mathf.Max(.01f, outlineEnergyPatternScale) * .2f;
            _energyTime = clock * EnergyFloat(EnergyFlow, .62f) * Mathf.Max(0f, outlineEnergyFlowSpeed);
            _energyEmission01 = Mathf.Clamp01((EnergyFloat(EnergyEmission, 1.2f) - .5f) / 4.5f);
        }

        private float EnergyFloat(int id, float fallback) =>
            outlineEnergySource && outlineEnergySource.HasProperty(id) ? outlineEnergySource.GetFloat(id) : fallback;

        private Color SampleOutlineEnergy(Vector3 shellPosition, float alpha)
        {
            // Whole-shell coordinates, with no triangle index, edge UV, or per-panel random phase.
            Vector3 p = shellPosition.normalized;
            float3 position = new float3(p.x, p.y, p.z) * _energyScale
                + new float3(.17f, -.13f, .11f) * _energyTime;
            // The angle of two decorrelated, zero-centered noise channels distributes
            // hues around the full color wheel. Direct scalar noise-to-hue mapping
            // would cluster around its mean; weighted palette mixing favored red.
            float x = noise.snoise(position + new float3(17.13f, 3.71f, 31.41f));
            float y = noise.snoise(position + new float3(-11.7f, 47.23f, 8.19f));
            float hue = Mathf.Repeat(Mathf.Atan2(y, x) / (Mathf.PI * 2f) + .5f + _energyTime * .025f, 1f);
            float brightness = Mathf.Lerp(1f, 1.65f, _energyEmission01) * Mathf.Max(0f, outlineEnergyBrightness);
            Color result = Color.HSVToRGB(hue, Mathf.Clamp01(outlineEnergySaturation), brightness, true);
            result.a = alpha;
            return result;
        }

        private void DrawEnergyEdge(Vector3 start, Vector3 end, Vector3 fieldStart, Vector3 fieldEnd, float reveal, float alpha)
        {
            if (!energyOutlines) { DrawEdge(start, end, reveal); return; }
            if (reveal <= .001f) return;
            float visible = Mathf.Clamp01(reveal);
            float span = Vector3.Distance(fieldStart.normalized, fieldEnd.normalized) * _energyScale;
            int samples = Mathf.Clamp(Mathf.Max(outlineEnergySamples, Mathf.CeilToInt(span * 8f)), 1, 32);
            if (_gpuEnergyActive)
            {
                QueueGpuEnergyEdge(start, end, fieldStart, fieldEnd, visible, alpha, samples);
                return;
            }
            Vector3 previous = start;
            Color previousColor = SampleOutlineEnergy(fieldStart, alpha);
            // Fixed sample positions along the full edge keep the pattern stable during draw-in.
            for (int i = 1; i <= samples; i++)
            {
                float t = Mathf.Min((float)i / samples, visible);
                Vector3 next = Vector3.Lerp(start, end, t);
                Color nextColor = SampleOutlineEnergy(Vector3.Lerp(fieldStart, fieldEnd, t), alpha);
                Draw.Line(previous, next, previousColor, nextColor);
                if (t >= visible) break;
                previous = next;
                previousColor = nextColor;
            }
        }
    }
}
