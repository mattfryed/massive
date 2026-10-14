using Massive.Scoring;
using UnityEngine;

namespace Massive.Cosmos
{
    /// <summary>A presentation-only finale over the existing web. No topology or pickup simulation is rebuilt.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(CosmicWebBackground))]
    [DefaultExecutionOrder(550)]
    public sealed class CosmicCrunchFinale : MonoBehaviour, IMatchFinale
    {
        public CosmicWebBackground web;
        [Header("Timing")]
        [Min(.25f)] public float duration = 6;
        [Min(0)] public float blackoutHold = .35f;
        [Tooltip("X = finale time; Y = contraction. Endpoints are forced to 0 and 1. A slow start and accelerating middle retain the gathering phase.")]
        public AnimationCurve contraction = new AnimationCurve(
            new Keyframe(0, 0, 0, 0), new Keyframe(.18f, .035f, .4f, .4f),
            new Keyframe(.58f, .6f, 1.7f, 1.7f), new Keyframe(.86f, .96f, .5f, .5f),
            new Keyframe(1, 1, 0, 0));
        [Header("Collapse")]
        [Tooltip("Coherent cluster-to-cluster delay, as a fraction of the contraction. Zero synchronizes the regions.")]
        [Range(0, .4f)] public float regionalDelay = .16f;
        [Range(0, 3)] public float turbulence = 1;
        [Tooltip("Final particle diameter relative to its existing size. All matter disappears at the end, regardless of this setting.")]
        [Range(.01f, 1)] public float finalParticleScale = .08f;
        [Tooltip("Radius in world units of the final solid pinpoint. One existing tracer becomes this core so it remains visible after the cloud contracts below a pixel.")]
        [Min(.001f)] public float coreRadius = .05f;
        [Header("Compression heat")]
        [Range(0, 1)] public float heatBlend = 1;
        [ColorUsage(false, true)] public Color warmColor = new Color(1, .18f, .035f);
        [ColorUsage(false, true)] public Color hotColor = new Color(1, .78f, .2f);
        [ColorUsage(false, true)] public Color coreColor = Color.white;
        [Tooltip("Brief size/brightness accent at the final pinpoint. Zero removes the pulse.")]
        [Range(0, 1)] public float finalPulse = .35f;
        [Header("Runtime presentation")]
        [Tooltip("Goals and HUD to withdraw at the buzzer. Never include this object, its ancestors, the camera or match manager. GameplayObjects is withdrawn by the match manager.")]
        public GameObject[] withdrawAtStart = new GameObject[0];

        static readonly int CrunchID = Shader.PropertyToID("_Crunch"), StyleID = Shader.PropertyToID("_CrunchStyle"),
            WarmID = Shader.PropertyToID("_CrunchWarm"), HotID = Shader.PropertyToID("_CrunchHot"), CoreID = Shader.PropertyToID("_CrunchCore"),
            CoreRadiusID = Shader.PropertyToID("_CrunchCoreRadius");
        bool running, finished, previewActive;
        float elapsed, previewProgress, startMotion;
        GameManagerScript activeMatch;

        public bool VisualActive => isActiveAndEnabled && (running || finished || (!Application.isPlaying && previewActive));
        public bool IsRunning => running;
        public bool PreviewActive => !Application.isPlaying && previewActive;
        public float Progress => running || finished ? Mathf.Clamp01(elapsed / Mathf.Max(.25f, duration)) : previewProgress;
        public float VisualMotionTime => startMotion + (running || finished ? elapsed : previewProgress * Mathf.Max(.25f, duration));

        void OnEnable()
        {
            if (!web) web = GetComponent<CosmicWebBackground>();
            if (web) web.CrunchFinale = this;
        }

        public void BeginMatchFinale()
        {
            if (!Application.isPlaying || running || finished || !web || !web.match) return;
            activeMatch = web.match;
            if (!activeMatch.BeginPresentationFinale(this)) return;
            if (!web.isActiveAndEnabled || !web.particleShader || !web.particleShader.isSupported)
            {
                Debug.LogWarning("[COSMOS] Crunch web is unavailable; completing the match without the presentation.", this);
                activeMatch.CompletePresentationFinale(this);
                return;
            }
            startMotion = web.MotionTime;
            elapsed = 0;
            previewActive = false;
            running = true;
            // Supernova OnDisable clears warnings and pooled pickups before the first crunch frame.
            foreach (var layer in GetComponents<CosmicSupernovae>()) layer.enabled = false;
            var grid = GetComponent<VectorGridGPU>();
            if (grid) grid.enabled = false;
            var gridRenderer = GetComponent<MeshRenderer>();
            if (gridRenderer) gridRenderer.enabled = false;
            var layout = GetComponent<CosmosArenaLayout>();
            if (layout) layout.PresentationHidden = true;
            foreach (var root in withdrawAtStart)
            {
                if (!root || transform.IsChildOf(root.transform) || activeMatch.transform.IsChildOf(root.transform) ||
                    root.GetComponentInChildren<Camera>(true)) continue;
                root.SetActive(false);
            }
        }

        void Update()
        {
            if (!Application.isPlaying || !running) return;
            // Match finale presentation continues even if a preceding bonus paused world time.
            elapsed += Time.unscaledDeltaTime;
            if (elapsed < Mathf.Max(.25f, duration) + Mathf.Max(0, blackoutHold)) return;
            running = false;
            finished = true; // Keep particles extinguished through the PostGame handoff.
            if (activeMatch) activeMatch.CompletePresentationFinale(this);
        }

        public void SetPreview(float progress)
        {
            if (Application.isPlaying) return;
            if (!previewActive && web) startMotion = web.MotionTime;
            previewActive = true;
            previewProgress = Mathf.Clamp01(progress);
        }

        public void StopPreview() { previewActive = false; previewProgress = 0; }

        internal static void SetInactive(MaterialPropertyBlock properties)
        {
            properties.SetVector(CrunchID, Vector4.zero);
            properties.SetVector(StyleID, Vector4.zero);
        }

        internal void ApplyVisuals(MaterialPropertyBlock properties)
        {
            float p = Progress;
            float amount = p <= 0 ? 0 : p >= 1 ? 1 : Mathf.Clamp01(contraction == null ? p : contraction.Evaluate(p));
            properties.SetVector(CrunchID, new Vector4(p, amount, Mathf.Clamp(regionalDelay, 0, .4f), Mathf.Clamp(turbulence, 0, 3)));
            properties.SetVector(StyleID, new Vector4(Mathf.Clamp(finalParticleScale, .01f, 1), Mathf.Clamp01(heatBlend), Mathf.Clamp01(finalPulse), 1));
            properties.SetColor(WarmID, warmColor);
            properties.SetColor(HotID, hotColor);
            properties.SetColor(CoreID, coreColor);
            properties.SetFloat(CoreRadiusID, Mathf.Max(.001f, coreRadius));
        }

        void OnDisable()
        {
            StopPreview();
            running = false;
            finished = false;
            if (web && web.CrunchFinale == this) web.CrunchFinale = null;
            // At runtime the match manager completes a disabled selected finale on its next update.
            // Do not reactivate gameplay or request scene changes during scene unload.
        }
    }
}
