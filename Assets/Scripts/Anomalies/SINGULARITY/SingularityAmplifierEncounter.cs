using Massive.Multiplier;
using Massive.Resonance;
using UnityEngine;

namespace Massive.Singularity
{
    /// <summary>Level-local adaptation of the existing encounter owner. Patterns stay
    /// on the front face; Core physics shares the players' periodic chart.</summary>
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(-150)]
    public sealed class SingularityAmplifierEncounter : MonoBehaviour
    {
        public AmplifierResonanceSpawner spawner;
        public SingularitySurface surface;
        public SingularityGridRenderer grid;
        public SingularityBlackHolePortal portal;
        public Transform frontPlacementPlane;
        public SphereCollider blackHoleExclusion;
        public ResonancePatternController editPreview;
        [Tooltip("Full planar arena size for which the ordered pattern prefabs were authored. Their layout is fitted to this level's flat front face, not its folded projected bounds.")]
        public Vector2 patternReferenceSize = new Vector2(28f, 12f);
        [Tooltip("Spawn-only exclusion around the black hole's full attraction field, so a new Core does not immediately fall in. Does not add a physical obstacle or affect Core motion.")]
        [Min(0f)] public float blackHoleSpawnPadding = .2f;

        private AmplifierResonanceSpawner subscribed;
        private ResonancePatternController boundPattern;
        private Vector2 previewScale;
        private bool hasPreviewScale;

        private void OnEnable()
        {
            RefreshPlacement();
            Subscribe();
        }
        private void OnDisable()
        {
            Unsubscribe();
            if (grid != null && grid.ActiveResonancePattern == boundPattern) grid.SetResonancePattern(null);
            boundPattern = null;
        }
        private void Update()
        {
            RefreshPlacement();
            Subscribe();
            if (Application.isPlaying)
            {
                var active = spawner != null ? spawner.ActivePattern : null;
                if (active != boundPattern) BindPattern(active);
            }
            else RefreshEditPreview();
        }
        private void Subscribe()
        {
            if (!Application.isPlaying || subscribed == spawner) return;
            Unsubscribe();
            subscribed = spawner;
            if (subscribed == null) return;
            subscribed.PatternPreparing += PreparePattern;
            subscribed.CoreSpawned += PrepareCore;
        }
        private void Unsubscribe()
        {
            if (subscribed != null)
            {
                subscribed.PatternPreparing -= PreparePattern;
                subscribed.CoreSpawned -= PrepareCore;
            }
            subscribed = null;
        }

        public void RefreshPlacement()
        {
            if (surface == null || spawner == null || spawner.spawnRegion == null || frontPlacementPlane == null) return;
            frontPlacementPlane.SetPositionAndRotation(surface.transform.position,
                surface.transform.rotation * Quaternion.Euler(90f, 0f, 0f));
            var region = spawner.spawnRegion;
            region.explicitPlane = frontPlacementPlane;
            region.explicitPlaneSize = new Vector2(surface.Width, surface.FrontHeight);
            region.spawnHeightWorld = surface.transform.position.y;
            if (blackHoleExclusion != null && portal != null)
            {
                blackHoleExclusion.transform.position = portal.transform.position;
                blackHoleExclusion.radius = Mathf.Max(portal.eventHorizonRadius, portal.EffectiveAttractionRadius)
                    + Mathf.Max(0f, blackHoleSpawnPadding);
            }
        }

        public Vector2 FitPatternScale(Vector2 authored)
        {
            if (surface == null) return authored;
            return Vector2.Scale(authored, new Vector2(surface.Width / Mathf.Max(.01f, patternReferenceSize.x),
                surface.FrontHeight / Mathf.Max(.01f, patternReferenceSize.y)));
        }
        private void PreparePattern(ResonancePatternController pattern)
        {
            if (pattern == null) return;
            pattern.patternScale = FitPatternScale(pattern.patternScale);
            pattern.arenaBounds = null;
            pattern.grid = null;
            pattern.energyOrigin = surface != null ? surface.transform : null;
            var manifestation = pattern.GetComponent<ResonanceManifestation>();
            if (manifestation != null && surface != null)
                manifestation.SetArea(Vector2.zero, new Vector2(surface.Width, surface.FrontHeight) * .5f);
            BindPattern(pattern);
        }
        private void PrepareCore(AmplifierCoreGameplay core)
        {
            if (core == null || surface == null) return;
            // The folded renderer is the only grid authority in this level.
            foreach (var interactor in core.GetComponentsInChildren<GridInteractor>(true)) interactor.enabled = false;
            var adapter = core.GetComponent<SingularityAmplifierAdapter>();
            if (adapter == null) adapter = core.gameObject.AddComponent<SingularityAmplifierAdapter>();
            adapter.Configure(surface, grid);
            adapter.ConfigurePortal(portal);
            var brightness = core.GetComponent<SingularityRendererBrightness>();
            if (brightness == null) brightness = core.gameObject.AddComponent<SingularityRendererBrightness>();
            brightness.Configure(surface);
        }
        private void BindPattern(ResonancePatternController pattern)
        {
            boundPattern = pattern;
            if (grid != null) grid.SetResonancePattern(pattern);
        }
        public void RefreshEditPreview()
        {
            if (Application.isPlaying || editPreview == null || surface == null) return;
            if (!hasPreviewScale)
            {
                // Read the authored prefab, never repeatedly scale the already-fitted scene copy.
                var entries = spawner != null ? spawner.patternOrder : null;
                var source = entries != null && entries.Count > 0
                    ? entries[Mathf.Clamp(spawner.startingPattern, 0, entries.Count - 1)]?.patternPrefab : null;
                previewScale = source != null ? source.patternScale : editPreview.patternScale;
                hasPreviewScale = true;
            }
            Vector2 fitted = FitPatternScale(previewScale);
            if (editPreview.patternScale != fitted)
            {
                editPreview.patternScale = fitted;
                editPreview.RequestRebuild();
            }
            BindPattern(editPreview);
        }
    }
}
