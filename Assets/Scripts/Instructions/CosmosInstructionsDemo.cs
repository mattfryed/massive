using Massive.Cosmos;
using Massive.Demonstrations;
using UnityEngine;

/// <summary>A camera close-up of the production web, supernova layers and pickup prefabs.</summary>
[DisallowMultipleComponent]
public sealed class CosmosInstructionsDemo : MonoBehaviour
{
    public CosmicWebBackground web;
    public CosmicSupernovae normal, superluminous;
    public PlayerDemoView viewport;
    public Transform panelFrame;
    public Vector2 viewportSize = new(7.37f, 3.9f);
    [Range(0, 1)] public float universeAge = .5f;
    [Min(.5f)] public float quietSeconds = 1f;
    public int CompletedLoops { get; private set; }
    public float LoopAge { get; private set; }
    public float LoopSeconds => 2 * (Mathf.Max(normal.EventSeconds, superluminous.EventSeconds) + quietSeconds);
    public int NormalWarnings { get; private set; }
    public int SuperluminousWarnings { get; private set; }

    Camera previewCamera;
    System.Random random;
    float elapsed, nextNormal, nextSuper;
    bool ready;

    void OnEnable()
    {
        previewCamera = viewport.GetComponent<Camera>();
        previewCamera.enabled = false;
        web.PresentationAge = universeAge;
        normal.enabled = superluminous.enabled = true;
        normal.presentationCamera = superluminous.presentationCamera = previewCamera;
        elapsed = LoopAge = 0;
        nextNormal = nextSuper = .65f;
        CompletedLoops = NormalWarnings = SuperluminousWarnings = 0;
        random = new System.Random(90212);
        ready = false;
    }

    void Update()
    {
        if (!web || web.RenderedParticleCount == 0) return;
        ready = true;
        web.PresentationAge = universeAge;
        elapsed += Time.deltaTime;
        CompletedLoops = Mathf.FloorToInt(elapsed / LoopSeconds);
        LoopAge = Mathf.Repeat(elapsed, LoopSeconds);
        if (elapsed >= nextNormal)
        {
            if (TryStart(normal, -3f)) { NormalWarnings++; nextNormal += LoopSeconds * .5f; }
            else nextNormal = elapsed + .25f;
        }
        if (elapsed >= nextSuper)
        {
            if (TryStart(superluminous, 3f)) { SuperluminousWarnings++; nextSuper += LoopSeconds; }
            else nextSuper = elapsed + .25f;
        }
        // Never clear or rewind visible effects at the cycle seam. The production
        // cloud dissolve and pickup lifetime complete naturally across repeated cycles.
    }

    bool TryStart(CosmicSupernovae layer, float horizontal)
    {
        Vector3 target = previewCamera.transform.position;
        target.x += horizontal;
        target.z -= .25f;
        target.y = 0;
        float best = float.PositiveInfinity;
        CosmicWebTopology.Particle chosen = default;
        for (int i = 0; i < 192; i++)
        {
            if (!web.TrySampleMatterPoint(random, layer.clusterBias, layer.wallClearance, out var source, out _, out var point)) continue;
            // Choose real source galaxies near the two preview centers; never relocate a flash
            // away from its matter particle just to fit the illustration.
            point.y = 0;
            float distance = (point - target).sqrMagnitude;
            if (distance >= best) continue;
            chosen = source; best = distance;
        }
        return best <= .85f * .85f && layer.TryBeginPresentation(chosen);
    }

    void LateUpdate()
    {
        if (!ready || !viewport || !panelFrame || !previewCamera) return;
        if (!viewport.screenCamera) viewport.screenCamera = Camera.main;
        if (!viewport.screenCamera) return;
        Vector3 half = new Vector3(viewportSize.x, viewportSize.y, 0) * .5f;
        viewport.lowerLeft = panelFrame.TransformPoint(-half);
        viewport.upperRight = panelFrame.TransformPoint(half);
        viewport.RefreshRect();
        previewCamera.depth = viewport.screenCamera.depth + 1;
        previewCamera.enabled = true;
    }

    void OnDisable()
    {
        if (previewCamera) previewCamera.enabled = false;
        if (normal) normal.enabled = false;
        if (superluminous) superluminous.enabled = false;
        if (web) web.PresentationAge = null;
        ready = false;
    }
}
