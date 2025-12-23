using UnityEngine;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using UnityEngine.Rendering;

/// <summary>
/// MagnetosphereVisualizer
/// - Field lines integrated in XZ plane from a dipole-like field.
/// - Magnetopause ("envelope") remolded by wind + storm pressure.
/// - Storm has ease-in/hold/ease-out and a moving front (turbulence localized).
/// - Boundary approach is smoothed (tangent easing + intersection + boundary slide).
/// - UPDATED: Proper inverse-Jacobian (includes non-linear scale derivatives / shear term) so lines conform to warp without “flat sides”.
/// - UPDATED: Envelope normal uses derivative-aware gradient (better tangent easing + curvature-following slide).
/// - NEW: Option to hide visualization in Scene View during Play mode.
/// </summary>
public class MagnetosphereVisualizer : MonoBehaviour
{
    // ============================================================
    // Editor / Scene View visualization control
    // ============================================================

    [Header("Editor Visualization")]
    [Tooltip("If true, hides LineRenderers + debug envelopes (and particles drawn by this script) in the Scene View while in Play mode.")]
    [SerializeField] private bool hideVisualizationInSceneViewDuringPlay = true;

    // SRP vs Built-in camera hook
    private bool usesSRP;
    private int sceneHideDepth = 0;

    private readonly List<Renderer> sceneHideRenderers = new List<Renderer>(512);
    private bool rendererCacheDirty = true;

    // ============================================================
    // Compute / rendering toggles
    // ============================================================

    [Header("Compute Shader (optional particles)")]
    [SerializeField] private ComputeShader magnetosphereCompute;

    [Header("Render Toggles")]
    [SerializeField] private bool renderParticles = false;
    [SerializeField] private bool renderFieldLines = true;

    // ============================================================
    // Particle rendering (optional)
    // ============================================================

    [Header("Particle Settings")]
    [SerializeField] private int particleCount = 50000;
    [SerializeField] private float particleSize = 0.02f;
    [SerializeField] private float particleDamping = 0.5f;
    [SerializeField] private float velocityLimit = 5.0f;
    [SerializeField] private Material particleMaterial;

    // ============================================================
    // Core magnetosphere parameters
    // ============================================================

    [Header("Magnetosphere Settings")]
    [SerializeField] private float dipoleMoment = 170.0f;
    [SerializeField] private float magnetosphereRadius = 13.0f;
    [SerializeField] private float planetRadius = 0.03f;
    [SerializeField] private Vector3 dipolePosition = Vector3.zero;

    [Header("Orientation")]
    [Tooltip("Magnetic dipole axis in world space. Forward gives you side-on when viewing XZ.")]
    [SerializeField] private Vector3 dipoleAxis = Vector3.forward;

    // ============================================================
    // Wind + baseline shaping
    // ============================================================

    [Header("Wind / Global Shape (baseline)")]
    [Tooltip("DOWNSTREAM direction (tail direction). Clamped to XZ if enabled.")]
    [SerializeField] private Vector3 solarWindDirection = Vector3.right;

    [SerializeField, Range(0f, 6f)] private float solarWindStrength = 0.0f;
    [SerializeField, Range(0f, 2.5f)] private float daysideCompression = 0.0f;
    [SerializeField, Range(0f, 6.0f)] private float nightsideStretch = 0.0f;
    [SerializeField, Range(0f, 6.0f)] private float tailFieldStrength = 0.0f;

    [Header("Live Turbulence (baseline)")]
    [SerializeField, Range(0f, 3f)] private float turbulenceStrength = 0.06f;
    [SerializeField, Range(0.05f, 2f)] private float turbulenceScale = 1.09f;
    [SerializeField, Range(0.05f, 5f)] private float turbulenceSpeed = 1.10f;

    // ============================================================
    // Storm timing + front
    // ============================================================

    [Header("Storm Settings (moving front/wave)")]
    [SerializeField] private float stormInterval = 2.0f;

    [Tooltip("Storm peak hold duration. Total storm time = easeIn + duration + easeOut.")]
    [SerializeField] private float stormDuration = 2.0f;

    [Tooltip("Ease-in time (adds to total storm duration).")]
    [SerializeField] private float stormEaseInTime = 0.50f;

    [Tooltip("Ease-out time (adds to total storm duration).")]
    [SerializeField] private float stormEaseOutTime = 0.75f;

    [Tooltip("Storm intensity = extra global 'pressure drive' at peak.")]
    [SerializeField, Range(0f, 20f)] private float stormIntensity = 4.5f;

    [Tooltip("Scales stormIntensity into actual pressure drive.")]
    [SerializeField, Range(0f, 3f)] private float stormWarpScale = 0.25f;

    [Tooltip("Scales how much storm boosts turbulence (front+wake).")]
    [SerializeField, Range(0f, 2f)] private float stormTurbulenceScale = 0.65f;

    [Header("Storm Front Shape (turbulence localization)")]
    [SerializeField] private float stormFrontThickness = 5.5f;
    [SerializeField] private float stormWakeLength = 12.0f;
    [SerializeField, Range(0f, 1f)] private float stormFrontFeather = 0.65f;

    [Header("System-wide vs local storm influence")]
    [Tooltip("Local front warp amount (0 = purely global envelope, 1 = stronger local shove).")]
    [SerializeField, Range(0f, 1f)] private float stormFrontLocalWarpAmount = 0.15f;

    [Tooltip("Higher = more global / less local. At 1, local warp is essentially disabled.")]
    [SerializeField, Range(0f, 1f)] private float stormGlobalWarpShare = 0.35f;

    // ============================================================
    // Wind direction randomization / drift
    // ============================================================

    [Header("Wind Direction Randomization")]
    [SerializeField] private bool windXZOnly = true;
    [SerializeField] private bool randomizeWindOnStormStart = true;

    [SerializeField] private bool driftWindWhenCalm = false;
    [SerializeField, Range(0f, 1f)] private float calmWindDriftAmount = 0.15f;
    [SerializeField, Range(0.05f, 2f)] private float calmWindDriftSpeed = 0.20f;

    // ============================================================
    // Envelope/magnetopause model (shape + response)
    // ============================================================

    [Header("Safe Zone Envelope (concept model)")]
    [Tooltip("Blend width for day<->tail transition. 0 uses magnetosphereRadius * 0.35.")]
    [SerializeField] private float magnetopauseBlendWidth = 0f;

    [SerializeField, Range(0.05f, 1f)] private float dayPerpCompressionRatio = 0.35f;
    [SerializeField, Range(0.05f, 1f)] private float tailPerpFlareRatio = 0.35f;

    [Header("Envelope Response Curve")]
    [Tooltip("Higher = envelope responds sooner to increases in wind/storm.")]
    [SerializeField, Range(0.01f, 3f)] private float pressureGain = 0.35f;

    [Tooltip("If true, uses windStrength^2 before response curve (more 'pressure-like').")]
    [SerializeField] private bool useSquaredPressure = true;

    [SerializeField, Range(0.25f, 6f)] private float daysideResponseExp = 1.6f;
    [SerializeField, Range(0.25f, 6f)] private float tailResponseExp = 2.0f;
    [SerializeField, Range(0.25f, 6f)] private float tailPerpResponseExp = 1.4f;

    [Header("Stronger Storm Compression")]
    [Tooltip("Allows the nose/day-side to get closer than the default clamp. Lower = closer.")]
    [SerializeField, Range(0.03f, 0.50f)] private float minDaysideAlongScale = 0.10f;

    [Tooltip("Allows day-side perpendicular scale to get smaller than default clamp. Lower = tighter.")]
    [SerializeField, Range(0.03f, 0.80f)] private float minDaysidePerpScale = 0.18f;

    [Tooltip("Extra day-side compression that scales with storm level (0=no extra).")]
    [SerializeField, Range(0f, 8f)] private float stormDaysideCompressionBoost = 3.0f;

    [Header("Flank / Terminator Pinch")]
    [Tooltip("Extra pinch applied near the day↔night transition (along≈0).")]
    [SerializeField, Range(0f, 8f)] private float flankPinchStrength = 2.0f;

    [Tooltip("Minimum perpendicular scale at the flank under maximum pressure (lower = narrower).")]
    [SerializeField, Range(0.05f, 1f)] private float minFlankPerpScale = 0.55f;

    [Tooltip("How wide (world units) the flank pinch band is along the wind axis. 0 = auto.")]
    [SerializeField] private float flankPinchWidth = 0f;

    [Tooltip("How aggressively the flank pinch ramps with pressure.")]
    [SerializeField, Range(0.25f, 6f)] private float flankPinchResponseExp = 1.5f;

    // ============================================================
    // Edge stability + boundary interaction
    // ============================================================

    [Header("Edge Stability")]
    [Tooltip("0..1 multiplier applied to turbulence near the outer envelope (1 = unchanged, 0 = calm).")]
    [SerializeField, Range(0f, 1f)] private float outerTurbulenceMultiplier = 0.25f;

    [Tooltip("0..1 multiplier applied to post-warp visual warble near the envelope (1 = unchanged, 0 = calm).")]
    [SerializeField, Range(0f, 1f)] private float outerWarbleMultiplier = 0.15f;

    [Tooltip("Caps adaptive step size multiplier (prevents huge steps far out).")]
    [SerializeField, Range(1f, 10f)] private float maxStepMultiplier = 3.0f;

    [Header("Magnetopause Approach Smoothing")]
    [SerializeField, Range(0.5f, 0.999f)]
    private float boundaryEaseStart = 0.90f;

    [SerializeField, Range(0.5f, 8f)]
    private float boundaryEasePower = 2.0f;

    [SerializeField, Range(0f, 1f)]
    private float boundarySlide = 0.85f;

    [SerializeField, Range(0, 16)]
    private int boundaryIntersectionIterations = 8;

    [SerializeField, Range(0f, 1f)]
    private float boundaryNearDamping = 0.25f;

    [Tooltip("Curvature-following slide. More substeps = rounder drape.")]
    [SerializeField, Range(1, 16)]
    private int boundarySlideSubsteps = 6;

    // ============================================================
    // Field-line stretch conformance (updated Jacobian)
    // ============================================================

    [Header("Field-Line Stretch Conformance")]
    [Tooltip("If true, transform field direction out of warped space via inverse Jacobian.")]
    [SerializeField] private bool useWarpJacobianTransform = true;

    [Tooltip("0 = old behavior, 1 = fully Jacobian-corrected.")]
    [SerializeField, Range(0f, 1f)] private float warpFieldTransformBlend = 1.0f;

    [Tooltip(">1 exaggerates warp conformity; <1 softens it.")]
    [SerializeField, Range(0.25f, 4f)] private float warpFieldInversePower = 1.0f;

    [Header("Warp Jacobian Numerics")]
    [Tooltip("Finite-difference step (world units) for derivative-aware Jacobian/normal. ~1–2% of radius is a good start.")]
    [SerializeField, Range(0.001f, 2.0f)]
    private float warpJacobianDerivStep = 0.15f;

    [Header("Post-Warp Line Geometry (recommended for your look)")]
    [SerializeField] private bool postWarpLinePositions = true;

    [Tooltip("0 = no post-warp, 1 = fully warp line geometry to match envelope scaling.")]
    [SerializeField, Range(0f, 1f)] private float postWarpAmount = 1.0f;

    [Tooltip("Exponent applied to envelope scales when post-warping line geometry.")]
    [SerializeField, Range(0.25f, 4f)] private float postWarpPower = 1.0f;

    [Tooltip("Start post-warping as q approaches this value (q=1 at boundary). Higher = only near boundary.")]
    [SerializeField, Range(0f, 0.95f)] private float postWarpStartQ = 0.25f;

    [Tooltip("Higher = concentrate post-warp nearer to the boundary (reduces mid-line flattening).")]
    [SerializeField, Range(0.25f, 6f)] private float postWarpRampExp = 2.0f;


    // ============================================================
    // Field-line seeding (fill the envelope)
    // ============================================================

    [Header("Field Line Seeding")]
    [SerializeField] private bool constrainLinesToXZPlane = true;
    [SerializeField, Range(0.90f, 0.999f)] private float seedOuterFill = 0.985f;
    [SerializeField, Range(0.05f, 0.8f)] private float seedInnerFill = 0.18f;
    [SerializeField, Range(0f, 1f)] private float seedJitter = 0.15f;

    // ============================================================
    // Debug envelopes
    // ============================================================

    [Header("Debug Envelope Renderer")]
    [SerializeField] private bool showDebugEnvelope = true;

    [SerializeField] private Color debugCurrentColor = new Color(0.2f, 0.5f, 1f, 0.85f);
    [SerializeField] private Color debugMidColor = new Color(0.2f, 1.0f, 0.25f, 0.75f);
    [SerializeField] private Color debugMaxColor = new Color(1.0f, 0.9f, 0.2f, 0.75f);

    [SerializeField] private float debugEnvelopeWidth = 0.03f;
    [SerializeField] private int debugEnvelopeSamples = 256;
    [SerializeField] private float debugDashLength = 0.9f;
    [SerializeField] private float debugGapLength = 0.55f;

    [SerializeField] private bool debugEnvelopeInXZ = true;
    [SerializeField] private bool drawMidAndMaxEnvelopes = true;
    [SerializeField, Range(0f, 1f)] private float debugMidLevel = 0.5f;

    // ============================================================
    // Field lines (integration)
    // ============================================================

    [Header("Field Lines (Runtime)")]
    [SerializeField] private Material fieldLineMaterial;
    [SerializeField] private int fieldLineCount = 100;
    [SerializeField] private int fieldLineSegments = 201;

    [SerializeField] private float lineStep = 0.06f;
    [SerializeField] private float lineResponse = 10.0f;
    [SerializeField] private float seedDrift = 1.55f;
    [SerializeField] private float visualWarble = 0.08f;

    [Header("Integration Quality (curvature)")]
    [Tooltip("If enabled, uses smaller steps where the field is weak so far-field arcs don't flatten into long straight segments.")]
    [SerializeField] private bool useWeakFieldSmallerSteps = true;

    [Tooltip("Reference |B| value that marks the start of 'weak field' stepping.")]
    [SerializeField, Range(0.01f, 1.0f)] private float weakFieldReference = 0.12f;

    [Tooltip("Step factor applied at very weak field (0.5 halves step size).")]
    [SerializeField, Range(0.1f, 1.0f)] private float weakFieldMinStepFactor = 0.55f;

    [Header("Spin / Layer Motion")]
    [SerializeField, Range(0f, 2f)] private float spinStrength = 1.0f;
    [SerializeField, Range(0f, 2f)] private float spinSpeed = 0.25f;

    [Header("Pole Footpoints")]
    [SerializeField, Range(0f, 1f)] private float poleConvergence = 1.0f;
    [SerializeField] private float poleApproachDistance = 1.25f;
    [SerializeField, Range(0f, 1f)] private float poleApproachTangential = 0.25f;
    [SerializeField, Range(0f, 2f)] private float poleApproachStrength = 1.0f;
    [SerializeField] private float poleSnapRadiusMultiplier = 1.25f;

    // ============================================================
    // Line appearance
    // ============================================================

    [Header("Line Color Mapping")]
    [SerializeField, Range(0.25f, 4f)] private float innerOuterBlendPower = 1.0f;
    [SerializeField] private Color innerLineColor = new Color(1.0f, 0.0f, 1.0f, 0.22f);
    [SerializeField] private Color outerLineColor = new Color(1.0f, 1.0f, 1.0f, 0.18f);

    [Header("Core Color Blend (near planet/poles)")]
    [SerializeField] private Color coreLineColor = new Color(1.0f, 0.2f, 0.8f, 1.0f);
    [SerializeField, Range(0f, 0.5f)] private float coreBlendLength = 0.12f;
    [SerializeField, Range(0f, 1f)] private float coreBlendSharpness = 0.6f;

    [Header("Line Alpha Along Length")]
    [SerializeField, Range(0f, 2f)] private float endAlphaMultiplier = 1.0f;
    [SerializeField, Range(0f, 2f)] private float midAlphaMultiplier = 1.0f;
    [SerializeField, Range(0f, 0.5f)] private float endFadeLength = 0.0f;

    [Header("Width")]
    [SerializeField] private float fieldLineWidth = 0.02f;

    // ============================================================
    // GPU particle struct (optional)
    // ============================================================

    [StructLayout(LayoutKind.Sequential)]
    private struct Particle
    {
        public Vector3 position;
        public Vector3 velocity;
        public float charge;
        public float energy;
        public float lifetime;
        public float maxLifetime;
    }

    private ComputeBuffer particleBuffer;
    private ComputeBuffer argsBuffer;
    private Mesh particleMesh;
    private Bounds bounds;

    private int initKernel, updateKernel, applyStormKernel;

    // ============================================================
    // Runtime state
    // ============================================================

    private float simulationTime;
    private float lastStormTime;
    private float currentStormTime;
    private Vector3 currentStormDirection;
    private bool stormActive;

    private Vector3 baseWindDir;
    private Vector3 effectiveWindDir;

    private float stormEnvelope;      // 0..1
    private float stormFrontCenterS;  // along wind axis

    private float globalWindStrengthCached;

    private Vector3 axisY;
    private Quaternion dipoleRotation;

    private GameObject fieldLinesRoot;
    private LineRenderer[] fieldLines;
    private Vector3[] linePointsPrev;
    private Vector3[] linePointsNow;

    private float[] lineRingT;
    private float[] lineSpinDir;

    private bool appearanceDirty = true;

    // Debug envelope dashed sets
    private class DashSet
    {
        public GameObject root;
        public readonly List<LineRenderer> dashes = new List<LineRenderer>(128);
        public Color color;
    }

    private GameObject debugEnvelopeRoot;
    private Material debugEnvelopeMaterial;
    private DashSet dashCurrent, dashMid, dashMax;

    // ============================================================
    // Unity lifecycle
    // ============================================================

    private void OnEnable()
    {
        usesSRP = (GraphicsSettings.currentRenderPipeline != null);

        if (usesSRP)
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRenderingSRP;
            RenderPipelineManager.endCameraRendering += OnEndCameraRenderingSRP;
        }
        else
        {
            Camera.onPreCull += OnCameraPreCullBuiltin;
            Camera.onPostRender += OnCameraPostRenderBuiltin;
        }
    }

    private void OnDisable()
    {
        if (usesSRP)
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRenderingSRP;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRenderingSRP;
        }
        else
        {
            Camera.onPreCull -= OnCameraPreCullBuiltin;
            Camera.onPostRender -= OnCameraPostRenderBuiltin;
        }

        // Ensure we never leave renderers forced-off.
        sceneHideDepth = 0;
        ApplySceneViewHide(false);

        ReleaseBuffers();
    }

    private void OnDestroy()
    {
        ReleaseBuffers();

        if (particleMesh != null)
        {
            if (Application.isPlaying) Destroy(particleMesh);
            else DestroyImmediate(particleMesh);
        }

        if (fieldLinesRoot != null)
        {
            if (Application.isPlaying) Destroy(fieldLinesRoot);
            else DestroyImmediate(fieldLinesRoot);
        }

        if (debugEnvelopeRoot != null)
        {
            if (Application.isPlaying) Destroy(debugEnvelopeRoot);
            else DestroyImmediate(debugEnvelopeRoot);
        }
    }

    private void OnValidate()
    {
        dipoleMoment = Mathf.Max(0.1f, dipoleMoment);
        magnetosphereRadius = Mathf.Max(0.5f, magnetosphereRadius);
        planetRadius = Mathf.Max(0.001f, Mathf.Abs(planetRadius));

        fieldLineCount = Mathf.Max(1, fieldLineCount);
        fieldLineSegments = Mathf.Max(8, fieldLineSegments);

        lineStep = Mathf.Max(0.005f, lineStep);
        lineResponse = Mathf.Max(0.01f, lineResponse);

        stormFrontThickness = Mathf.Max(0.2f, stormFrontThickness);
        stormWakeLength = Mathf.Max(0f, stormWakeLength);

        stormEaseInTime = Mathf.Max(0f, stormEaseInTime);
        stormEaseOutTime = Mathf.Max(0f, stormEaseOutTime);
        stormDuration = Mathf.Max(0.01f, stormDuration);

        boundaryIntersectionIterations = Mathf.Clamp(boundaryIntersectionIterations, 0, 16);
        boundarySlideSubsteps = Mathf.Clamp(boundarySlideSubsteps, 1, 16);

        if (dipoleAxis == Vector3.zero) dipoleAxis = Vector3.forward;
        if (solarWindDirection == Vector3.zero) solarWindDirection = Vector3.right;

        appearanceDirty = true;
        rendererCacheDirty = true;
    }

    private void Start()
    {
        axisY = dipoleAxis.normalized;
        dipoleRotation = Quaternion.FromToRotation(Vector3.up, axisY);

        baseWindDir = NormalizeWind(solarWindDirection);
        currentStormDirection = baseWindDir;

        bounds = new Bounds(dipolePosition, Vector3.one * magnetosphereRadius * 10f);

        if (renderParticles)
        {
            InitializeCompute();
            CreateParticleMesh();
            InitializeParticles();
        }

        if (renderFieldLines)
            BuildOrRebuildFieldLinesObjects();

        lastStormTime = -Mathf.Max(0.01f, stormInterval);
        stormFrontCenterS = -999999f;

        EnsureDebugEnvelopeObjects();
        ForceRecomputeFieldLinesImmediate();
    }

    private void Update()
    {
        simulationTime += Time.deltaTime;

        // Wind drift when calm (optional).
        if (!driftWindWhenCalm && !stormActive)
            baseWindDir = NormalizeWind(solarWindDirection);

        if (driftWindWhenCalm && !stormActive)
        {
            float n = Mathf.PerlinNoise(10.1f, simulationTime * calmWindDriftSpeed);
            float a = (n - 0.5f) * 2f * calmWindDriftAmount * Mathf.PI;
            Vector3 drift = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            baseWindDir = Vector3.Slerp(baseWindDir, drift.normalized, 1f - Mathf.Exp(-1.5f * Time.deltaTime));
        }

        // Storm scheduling.
        bool stormsEnabled = stormInterval > 0.01f && stormIntensity > 0.001f && stormWarpScale > 0.0001f;
        if (stormsEnabled && !stormActive && (simulationTime - lastStormTime > stormInterval))
            StartNewStorm();

        UpdateStormEnvelopeAndFront();

        effectiveWindDir = stormActive ? currentStormDirection : baseWindDir;
        effectiveWindDir = NormalizeWind(effectiveWindDir);

        // System-wide envelope "pressure drive"
        globalWindStrengthCached = solarWindStrength + (stormIntensity * stormWarpScale * stormEnvelope);

        // Optional particles simulation (rendering happens per-camera in OnRenderObject)
        if (renderParticles && particleBuffer != null && particleMaterial != null)
        {
            UpdateCompute();
        }

        // Field lines
        if (renderFieldLines && fieldLines != null)
        {
            if (appearanceDirty) UpdateAllLineGradients();
            UpdateLiveFieldLines();
        }

        // Debug envelopes
        if (showDebugEnvelope)
        {
            EnsureDebugEnvelopeObjects();
            UpdateDebugEnvelopes();
        }
        else if (debugEnvelopeRoot != null)
        {
            debugEnvelopeRoot.SetActive(false);
        }
    }

    /// <summary>
    /// Per-camera render hook. This lets us skip particles in Scene View when hiding is enabled.
    /// (LineRenderers are handled via forceRenderingOff in the camera callbacks.)
    /// </summary>
    private void OnRenderObject()
    {
        if (!renderParticles || particleBuffer == null || particleMaterial == null) return;

        Camera cam = Camera.current;
        if (hideVisualizationInSceneViewDuringPlay && Application.isPlaying && cam != null && cam.cameraType == CameraType.SceneView)
            return;

        RenderParticles();
    }

    // ============================================================
    // Scene View hide implementation
    // ============================================================

    private void OnBeginCameraRenderingSRP(ScriptableRenderContext context, Camera cam)
    {
        if (!ShouldHideForCamera(cam)) return;
        EnterSceneViewHide();
    }

    private void OnEndCameraRenderingSRP(ScriptableRenderContext context, Camera cam)
    {
        if (!ShouldHideForCamera(cam)) return;
        ExitSceneViewHide();
    }

    private void OnCameraPreCullBuiltin(Camera cam)
    {
        if (!ShouldHideForCamera(cam)) return;
        EnterSceneViewHide();
    }

    private void OnCameraPostRenderBuiltin(Camera cam)
    {
        if (!ShouldHideForCamera(cam)) return;
        ExitSceneViewHide();
    }

    private bool ShouldHideForCamera(Camera cam)
    {
        if (!hideVisualizationInSceneViewDuringPlay) return false;
        if (!Application.isPlaying) return false;
        if (cam == null) return false;
        return cam.cameraType == CameraType.SceneView;
    }

    private void EnterSceneViewHide()
    {
        sceneHideDepth++;
        if (sceneHideDepth == 1) ApplySceneViewHide(true);
    }

    private void ExitSceneViewHide()
    {
        sceneHideDepth = Mathf.Max(0, sceneHideDepth - 1);
        if (sceneHideDepth == 0) ApplySceneViewHide(false);
    }

    private void ApplySceneViewHide(bool hide)
    {
        // Only LineRenderers/debug envelope renderers use forceRenderingOff.
        EnsureSceneHideRendererCache();

        for (int i = 0; i < sceneHideRenderers.Count; i++)
        {
            var r = sceneHideRenderers[i];
            if (!r) continue;
            r.forceRenderingOff = hide;
        }
    }

    private void EnsureSceneHideRendererCache()
    {
        if (!rendererCacheDirty) return;
        rendererCacheDirty = false;

        sceneHideRenderers.Clear();

        if (fieldLines != null)
        {
            for (int i = 0; i < fieldLines.Length; i++)
            {
                if (fieldLines[i]) sceneHideRenderers.Add(fieldLines[i]);
            }
        }

        void AddDashSet(DashSet set)
        {
            if (set == null) return;
            for (int i = 0; i < set.dashes.Count; i++)
            {
                if (set.dashes[i]) sceneHideRenderers.Add(set.dashes[i]);
            }
        }

        AddDashSet(dashCurrent);
        AddDashSet(dashMid);
        AddDashSet(dashMax);
    }

    // ============================================================
    // Storm envelope + easing (ease-in / hold / ease-out)
    // ============================================================

    private void UpdateStormEnvelopeAndFront()
    {
        stormEnvelope = 0f;
        if (!stormActive) return;

        currentStormTime += Time.deltaTime;

        float total = stormEaseInTime + stormDuration + stormEaseOutTime;
        total = Mathf.Max(0.0001f, total);

        float t = currentStormTime;

        // Envelope
        if (stormEaseInTime > 0.0001f && t < stormEaseInTime)
        {
            float u = t / stormEaseInTime;
            stormEnvelope = Smooth01(u);
        }
        else if (t < stormEaseInTime + stormDuration)
        {
            stormEnvelope = 1f;
        }
        else if (stormEaseOutTime > 0.0001f && t < stormEaseInTime + stormDuration + stormEaseOutTime)
        {
            float u = (t - stormEaseInTime - stormDuration) / stormEaseOutTime;
            stormEnvelope = 1f - Smooth01(u);
        }
        else
        {
            stormEnvelope = 0f;
        }

        // Front travels over entire total duration
        float t01 = Mathf.Clamp01(currentStormTime / total);
        float startS = -magnetosphereRadius * 1.25f;
        float endS = magnetosphereRadius * 2.25f;
        stormFrontCenterS = Mathf.Lerp(startS, endS, t01);

        if (currentStormTime >= total)
        {
            stormActive = false;
            stormFrontCenterS = -999999f;
        }
    }

    // ============================================================
    // Wind / storm utilities
    // ============================================================

    private Vector3 NormalizeWind(Vector3 w)
    {
        if (windXZOnly) w.y = 0f;
        if (w.sqrMagnitude < 1e-6f) w = Vector3.right;
        return w.normalized;
    }

    private Vector3 RandomWindXZ()
    {
        float a = Random.Range(0f, Mathf.PI * 2f);
        return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)).normalized;
    }

    private void StartNewStorm()
    {
        currentStormDirection = randomizeWindOnStormStart ? RandomWindXZ() : baseWindDir;

        stormActive = true;
        currentStormTime = 0f;
        lastStormTime = simulationTime;

        stormFrontCenterS = -magnetosphereRadius * 1.25f;
    }

    public void TriggerStorm(Vector3 directionXZ)
    {
        currentStormDirection = NormalizeWind(directionXZ);
        stormActive = true;
        currentStormTime = 0f;
        lastStormTime = simulationTime;

        stormFrontCenterS = -magnetosphereRadius * 1.25f;
    }

    /// <summary>
    /// Returns local "front core" (slab) and "wake" influence at position.
    /// Used for localized turbulence and optional local warp.
    /// </summary>
    private void StormMaskAt(Vector3 pos, out float frontCore, out float wake)
    {
        frontCore = 0f;
        wake = 0f;

        if (!stormActive) return;

        Vector3 r = pos - dipolePosition;
        float along = Vector3.Dot(r, effectiveWindDir);
        float dist = along - stormFrontCenterS;

        float half = stormFrontThickness * 0.5f;
        float feather = Mathf.Lerp(0.05f, 2.0f, stormFrontFeather);

        float ad = Mathf.Abs(dist);
        float core = 1f - Smoothstep01(half, half * feather, ad);

        float wk = 0f;
        if (stormWakeLength > 0.001f && dist > 0f)
        {
            float u = Mathf.Clamp01(dist / stormWakeLength);
            wk = 1f - Smooth01(u);
        }

        frontCore = core * stormEnvelope;
        wake = wk * stormEnvelope;
    }

    // ============================================================
    // Envelope / magnetopause model
    // ============================================================

    /// <summary>
    /// Computes the local envelope semi-axes (a along wind, b perpendicular) and local scale factors.
    /// This is the single "source of truth" for boundary shape and for warp scaling.
    /// </summary>
    private void GetLocalEnvelope(float windStrength, float along, out float a, out float b, out float alongScale, out float perpScale)
    {
        float bw = (magnetopauseBlendWidth > 0.001f) ? magnetopauseBlendWidth : magnetosphereRadius * 0.35f;
        float tTail = Smoothstep01(-bw, bw, along); // 0=day, 1=tail

        float w = Mathf.Max(0f, windStrength);
        float x = useSquaredPressure ? (w * w) : w;
        float pressure01 = 1f - Mathf.Exp(-x * Mathf.Max(0.0001f, pressureGain));

        float pDay = Mathf.Pow(pressure01, Mathf.Max(0.01f, daysideResponseExp));
        float pTail = Mathf.Pow(pressure01, Mathf.Max(0.01f, tailResponseExp));
        float pPerp = Mathf.Pow(pressure01, Mathf.Max(0.01f, tailPerpResponseExp));

        float comp = daysideCompression * pDay;
        float stretch = nightsideStretch * pTail;
        float flare = tailFieldStrength * pPerp;

        // Storm-only extra dayside compression (scales with storm level)
        float stormMaxDrive = Mathf.Max(0.0001f, stormIntensity * stormWarpScale);
        float stormContribution = Mathf.Max(0f, windStrength - solarWindStrength);
        float stormLevel01 = Mathf.Clamp01(stormContribution / stormMaxDrive);
        comp *= (1f + stormDaysideCompressionBoost * stormLevel01);

        // Base day/tail factors
        float dayAlong = 1f / (1f + comp);
        float dayPerp = 1f / (1f + comp * dayPerpCompressionRatio);

        float tailAlong = 1f + stretch;
        float tailPerp = 1f + flare * tailPerpFlareRatio;

        // Adjustable clamps (lets nose get closer under strong storms)
        dayAlong = Mathf.Clamp(dayAlong, minDaysideAlongScale, 1f);
        dayPerp = Mathf.Clamp(dayPerp, minDaysidePerpScale, 1.25f);
        tailAlong = Mathf.Clamp(tailAlong, 1f, 18f);
        tailPerp = Mathf.Clamp(tailPerp, 1f, 6.0f);

        // Blend day<->tail continuously
        alongScale = Mathf.Lerp(dayAlong, tailAlong, tTail);
        perpScale = Mathf.Lerp(dayPerp, tailPerp, tTail);

        // Flank pinch (terminator tightening)
        float fw = (flankPinchWidth > 0.001f)
            ? flankPinchWidth
            : ((magnetopauseBlendWidth > 0.001f) ? magnetopauseBlendWidth : magnetosphereRadius * 0.35f);
        fw = Mathf.Max(0.001f, fw);

        float flankW = Mathf.Exp(-(along * along) / (fw * fw)); // 1 at along=0, ->0 away
        float pFlank = Mathf.Pow(pressure01, flankPinchResponseExp);
        float pinch01 = Mathf.Clamp01(flankPinchStrength * pFlank);

        float targetPerp = Mathf.Lerp(perpScale, Mathf.Max(0.001f, perpScale * minFlankPerpScale), pinch01);
        perpScale = Mathf.Lerp(perpScale, targetPerp, flankW);

        a = magnetosphereRadius * alongScale;
        b = magnetosphereRadius * perpScale;
    }

    /// <summary>
    /// Derivative-aware envelope scale sampling (finite difference in along).
    /// </summary>
    private void GetEnvelopeScalesAndDerivs(
        float windStrength, float along,
        out float alongScale, out float perpScale,
        out float dAlongScale_dAlong, out float dPerpScale_dAlong)
    {
        GetLocalEnvelope(windStrength, along, out _, out _, out alongScale, out perpScale);

        float h = Mathf.Max(1e-3f, warpJacobianDerivStep > 0f ? warpJacobianDerivStep : magnetosphereRadius * 0.02f);

        GetLocalEnvelope(windStrength, along + h, out _, out _, out float aP, out float pP);
        GetLocalEnvelope(windStrength, along - h, out _, out _, out float aM, out float pM);

        dAlongScale_dAlong = (aP - aM) / (2f * h);
        dPerpScale_dAlong = (pP - pM) / (2f * h);
    }

    /// <summary>
    /// Implicit ellipse "q" in wind frame: q==1 on boundary, q<1 inside, q>1 outside.
    /// Returns q and also outputs along/perp/a/b for reuse.
    /// </summary>
    private float EnvelopeQ(Vector3 pos, float envStrength, out float along, out Vector3 perp, out float a, out float b)
    {
        Vector3 w = effectiveWindDir.sqrMagnitude < 1e-6f ? Vector3.right : effectiveWindDir;

        Vector3 r = pos - dipolePosition;
        along = Vector3.Dot(r, w);
        perp = r - w * along;
        float perpMag = perp.magnitude;

        GetLocalEnvelope(envStrength, along, out a, out b, out _, out _);

        float q = Mathf.Sqrt((along * along) / (a * a) + (perpMag * perpMag) / (b * b));
        return q;
    }

    /// <summary>
    /// UPDATED: Derivative-aware outward normal of the envelope surface at a point.
    /// This improves tangent easing + boundary slide curvature (reduces “flat” draping).
    /// </summary>
    private Vector3 EnvelopeNormal(Vector3 pos, float envStrength)
    {
        Vector3 w = (effectiveWindDir.sqrMagnitude < 1e-6f) ? Vector3.right : effectiveWindDir.normalized;

        Vector3 r = pos - dipolePosition;
        float along = Vector3.Dot(r, w);
        Vector3 perp = r - w * along;
        float p2 = perp.sqrMagnitude;

        GetEnvelopeScalesAndDerivs(
            envStrength, along,
            out float sa, out float sp,
            out float dsa, out float dsp);

        float R = magnetosphereRadius;
        float a = Mathf.Max(1e-4f, R * sa);
        float b = Mathf.Max(1e-4f, R * sp);

        float invA2 = 1f / (a * a);
        float invB2 = 1f / (b * b);

        float safeSa = Mathf.Max(1e-4f, sa);
        float safeSp = Mathf.Max(1e-4f, sp);

        // d(1/a^2)/dAlong and d(1/b^2)/dAlong
        float dInvA2 = (-2f * dsa / safeSa) * invA2;
        float dInvB2 = (-2f * dsp / safeSp) * invB2;

        // f = along^2/a^2 + |perp|^2/b^2
        float df_dAlong = (2f * along * invA2) + (along * along) * dInvA2 + p2 * dInvB2;

        Vector3 grad = w * df_dAlong + perp * (2f * invB2);

        if (constrainLinesToXZPlane) grad.y = 0f;
        if (grad.sqrMagnitude < 1e-10f) grad = (pos - dipolePosition);

        return grad.normalized;
    }

    /// <summary>
    /// Smooth weight used to begin tangential steering before reaching the boundary.
    /// </summary>
    private float BoundaryEaseWeight(float q)
    {
        float w = Smoothstep01(boundaryEaseStart, 1f, q);
        return Mathf.Pow(w, boundaryEasePower);
    }

    /// <summary>
    /// Project any point back inside the envelope (radial in wind-frame ellipse metric).
    /// </summary>
    private Vector3 ApplyMagnetopauseClamp(Vector3 next, float envStrength)
    {
        Vector3 w = effectiveWindDir;
        Vector3 r = next - dipolePosition;

        float along = Vector3.Dot(r, w);
        Vector3 perp = r - w * along;
        float perpMag = perp.magnitude;

        GetLocalEnvelope(envStrength, along, out float a, out float b, out _, out _);

        float q = Mathf.Sqrt((along * along) / (a * a) + (perpMag * perpMag) / (b * b));
        if (q > 1f)
        {
            float inv = 1f / q;
            next = dipolePosition + w * (along * inv) + perp * inv;
            if (constrainLinesToXZPlane) next.y = dipolePosition.y;
        }

        return next;
    }

    /// <summary>
    /// Solve boundary radius along an XZ direction by finding r where q(r)=1.
    /// Used for envelope-aware seeding and debug rendering.
    /// </summary>
    private float SolveEnvelopeRadiusAlongDirection(Vector3 dirXZ, float windStrength)
    {
        Vector3 w = effectiveWindDir.sqrMagnitude < 1e-6f ? Vector3.right : effectiveWindDir;
        float dot = Mathf.Clamp(Vector3.Dot(dirXZ, w), -1f, 1f);
        float perpFactor = Mathf.Sqrt(Mathf.Max(0f, 1f - dot * dot));

        float Q(float r)
        {
            float along = r * dot;
            float perpMag = r * perpFactor;
            GetLocalEnvelope(windStrength, along, out float a, out float b, out _, out _);
            return Mathf.Sqrt((along * along) / (a * a) + (perpMag * perpMag) / (b * b));
        }

        float low = 0f;
        float high = magnetosphereRadius * 2f;
        float qh = Q(high);

        int expand = 0;
        while (qh < 1f && expand < 12)
        {
            high *= 1.5f;
            qh = Q(high);
            expand++;
        }

        for (int it = 0; it < 26; it++)
        {
            float mid = 0.5f * (low + high);
            float qm = Q(mid);
            if (qm < 1f) low = mid;
            else high = mid;
        }

        return high;
    }

    // ============================================================
    // UPDATED: Jacobian-corrected field direction for warp conformity
    // ============================================================

    /// <summary>
    /// UPDATED: Proper inverse Jacobian for r' = w*(along*sa(along)) + perp*sp(along).
    /// Includes the missing coupling (shear) term from d(sp)/d(along).
    /// </summary>
    private Vector3 UnwarpFieldDirection(Vector3 Bwarp, Vector3 pos, float windStrengthForWarp)
    {
        Vector3 w = (effectiveWindDir.sqrMagnitude < 1e-6f) ? Vector3.right : effectiveWindDir.normalized;

        Vector3 r = pos - dipolePosition;
        float along = Vector3.Dot(r, w);
        Vector3 perp = r - w * along;

        GetEnvelopeScalesAndDerivs(
            windStrengthForWarp, along,
            out float sa, out float sp,
            out float dsa, out float dsp);

        // For r' = w*(along*sa(along)) + perp*sp(along)
        // dr'/dAlong = w*(sa + along*dsa) + perp*(dsp)
        float denomAlong = Mathf.Max(1e-4f, sa + along * dsa);
        float denomPerp = Mathf.Max(1e-4f, sp);

        float Balong = Vector3.Dot(Bwarp, w);
        Vector3 Bperp = Bwarp - w * Balong;

        // Artistic control: exponentiate inverse magnitudes
        float invAlong = Mathf.Pow(1f / denomAlong, warpFieldInversePower);
        float invPerp = Mathf.Pow(1f / denomPerp, warpFieldInversePower);

        // Solve J * v = Bwarp (block triangular in {along,perp})
        float vAlong = Balong * invAlong;

        // Missing term in the old implementation: coupling from dsp
        Vector3 vPerp = (Bperp - perp * (dsp * vAlong)) * invPerp;

        Vector3 B = w * vAlong + vPerp;

        if (constrainLinesToXZPlane) B.y = 0f;
        return B;
    }

    // ============================================================
    // Field lines: build, style, integrate
    // ============================================================

    /// <summary>
    /// Build LineRenderer objects and allocate buffers for line points.
    /// </summary>
    private void BuildOrRebuildFieldLinesObjects()
    {
        if (fieldLineMaterial == null)
            fieldLineMaterial = new Material(Shader.Find("Sprites/Default"));

        if (fieldLinesRoot != null) Destroy(fieldLinesRoot);
        fieldLinesRoot = new GameObject("MagnetosphereFieldLines");
        fieldLinesRoot.transform.SetParent(transform, false);

        fieldLines = new LineRenderer[fieldLineCount];
        linePointsPrev = new Vector3[fieldLineCount * fieldLineSegments];
        linePointsNow = new Vector3[fieldLineCount * fieldLineSegments];

        lineRingT = new float[fieldLineCount];
        lineSpinDir = new float[fieldLineCount];

        int rings = Mathf.Max(3, Mathf.RoundToInt(Mathf.Sqrt(fieldLineCount)));

        for (int i = 0; i < fieldLineCount; i++)
        {
            int ring = i % rings;
            float ringT = (rings == 1) ? 0f : ring / (float)(rings - 1);
            lineRingT[i] = ringT;

            float h = Hash01(i);
            lineSpinDir[i] = (h > 0.5f) ? 1f : -1f;

            var go = new GameObject($"FieldLine_{i:000}");
            go.transform.SetParent(fieldLinesRoot.transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.material = fieldLineMaterial;
            lr.widthMultiplier = fieldLineWidth;
            lr.numCornerVertices = 2;
            lr.numCapVertices = 2;
            lr.positionCount = fieldLineSegments;

            fieldLines[i] = lr;
        }

        appearanceDirty = true;
        rendererCacheDirty = true;
    }

    /// <summary>
    /// Update gradients/width for all lines (cheap, only when settings change).
    /// </summary>
    private void UpdateAllLineGradients()
    {
        appearanceDirty = false;

        for (int i = 0; i < fieldLineCount; i++)
        {
            var lr = fieldLines[i];
            if (!lr) continue;

            Color baseC = ComputeBaseLineColor(lineRingT[i]);
            lr.colorGradient = BuildLineGradient(baseC);
            lr.widthMultiplier = fieldLineWidth;
        }
    }

    private Color ComputeBaseLineColor(float ringT)
    {
        float t = Mathf.Clamp01(ringT);
        t = Mathf.Pow(t, innerOuterBlendPower);
        return Color.Lerp(innerLineColor, outerLineColor, t);
    }

    /// <summary>
    /// Gradient that keeps ends visible (poles) and supports a "core blend" region near ends.
    /// </summary>
    private Gradient BuildLineGradient(Color baseColor)
    {
        float L = Mathf.Clamp(coreBlendLength, 0f, 0.5f);
        float midMix = Mathf.Lerp(0.25f, 0.85f, coreBlendSharpness);
        Color mixC = Color.Lerp(coreLineColor, baseColor, midMix);

        float eps = 0.0001f;
        float k1 = Mathf.Max(eps, L * 0.5f);
        float k2 = Mathf.Max(k1 + eps, L);

        float aBase = baseColor.a;
        float endAlpha = Mathf.Clamp01(aBase * endAlphaMultiplier);
        float midAlpha = Mathf.Clamp01(aBase * midAlphaMultiplier);

        float fade = Mathf.Clamp(endFadeLength, 0f, 0.5f);
        float f1 = Mathf.Max(0f, fade);
        float f2 = Mathf.Max(f1 + eps, 1f - fade);

        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(coreLineColor, 0f),
                new GradientColorKey(mixC, k1),
                new GradientColorKey(baseColor, k2),
                new GradientColorKey(baseColor, 1f - k2),
                new GradientColorKey(mixC, 1f - k1),
                new GradientColorKey(coreLineColor, 1f),
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(endAlpha, 0f),
                new GradientAlphaKey(midAlpha, f1),
                new GradientAlphaKey(midAlpha, 0.5f),
                new GradientAlphaKey(midAlpha, f2),
                new GradientAlphaKey(endAlpha, 1f),
            }
        );
        return g;
    }

    private void ForceRecomputeFieldLinesImmediate()
    {
        if (!renderFieldLines || fieldLines == null) return;
        RecomputeFieldLinesPositions(simulationTime, writePrev: true);
        ApplyFieldLinesToRenderers(1f);
    }

    private void UpdateLiveFieldLines()
    {
        float a = 1f - Mathf.Exp(-lineResponse * Time.deltaTime);
        RecomputeFieldLinesPositions(simulationTime, writePrev: false);
        ApplyFieldLinesToRenderers(a);
    }

    /// <summary>
    /// Main field-line integrator.
    /// </summary>
    private void RecomputeFieldLinesPositions(float t, bool writePrev)
    {
        int rings = Mathf.Max(3, Mathf.RoundToInt(Mathf.Sqrt(fieldLineCount)));
        int sectors = Mathf.CeilToInt(fieldLineCount / (float)rings);

        int half = fieldLineSegments / 2;
        float eps = 1e-4f;

        float envStrength = globalWindStrengthCached;

        for (int i = 0; i < fieldLineCount; i++)
        {
            int ring = i % rings;
            int sector = i / rings;

            float ringT = (rings == 1) ? 0f : ring / (float)(rings - 1);
            lineRingT[i] = ringT;

            float h = Hash01(i);
            float azNoise = (Mathf.PerlinNoise(h * 11.1f, t * 0.15f) - 0.5f) * 2f * seedDrift;
            float jitterR = (Mathf.PerlinNoise(h * 17.3f, t * 0.18f + 8.1f) - 0.5f) * 2f * seedJitter;

            float spin = lineSpinDir[i] * spinSpeed * (0.35f + 1.25f * ringT) * spinStrength;
            float baseAz = (sector / (float)Mathf.Max(1, sectors)) * Mathf.PI * 2f;

            float az = baseAz + azNoise + t * spin;

            Vector3 dirXZ = new Vector3(Mathf.Cos(az), 0f, Mathf.Sin(az)).normalized;
            Vector3 tangentWorld = new Vector3(-dirXZ.z, 0f, dirXZ.x).normalized;

            float boundaryR = SolveEnvelopeRadiusAlongDirection(dirXZ, envStrength);
            float rInner = magnetosphereRadius * Mathf.Clamp(seedInnerFill, 0.05f, 0.8f);
            float rOuter = boundaryR * seedOuterFill;

            float ringCurve = Mathf.Pow(Mathf.Clamp01(ringT), 0.9f);
            float r = Mathf.Lerp(rInner, rOuter, ringCurve) * (1f + jitterR * 0.06f);

            Vector3 seedWorld = dipolePosition + dirXZ * r;
            if (constrainLinesToXZPlane) seedWorld.y = dipolePosition.y;

            int baseIndex = i * fieldLineSegments;

            // integrate backward
            Vector3 p = seedWorld;
            bool reachedPlanet = false;

            for (int s = half - 1; s >= 0; s--)
            {
                Vector3 B = ComputeField(p, t, out _);
                float Bm = Mathf.Max(B.magnitude, eps);

                Vector3 dir = (B / Bm);
                if (constrainLinesToXZPlane) dir.y = 0f;

                // In post-warp mode we want natural dipole-ish arcs; boundary should not hijack the direction.
                // We confine positions later with a cheap clamp + visual post-warp.
                if (!(postWarpLinePositions && postWarpAmount > 0.001f))
                    dir = AdjustDirForBoundaries(p, dir, envStrength);
                dir = AdjustDirForPoleApproach(p, dir, tangentWorld);

                // Keep curvature in the weak-field far region: don't increase step when |B| is small.
                float step = lineStep;
                if (useWeakFieldSmallerSteps)
                {
                    float refB = Mathf.Max(1e-4f, weakFieldReference);
                    float tWeak = Mathf.Clamp01(refB / (Bm + refB));
                    step *= Mathf.Lerp(1f, weakFieldMinStepFactor, Smooth01(tWeak));
                }

                float qHere = EnvelopeQ(p, envStrength, out _, out _, out _, out _);
                float wHere = BoundaryEaseWeight(qHere);
                step *= Mathf.Lerp(1f, 1f - boundaryNearDamping, wHere);

                step = Mathf.Min(step, lineStep * maxStepMultiplier);

                Vector3 next = p - dir.normalized * step;
                if (constrainLinesToXZPlane) next.y = dipolePosition.y;

next = ApplyPlanetFootpoint(next, ref reachedPlanet);

bool postWarpActive = postWarpLinePositions && postWarpAmount > 0.001f;

if (!postWarpActive)
{
    next = ConstrainStepToEnvelope(p, next, dir, envStrength);
}

// Always safe to clamp (cheap + prevents tiny overshoots)
next = ApplyMagnetopauseClamp(next, envStrength);


                linePointsNow[baseIndex + s] = next;
                p = next;

                if (reachedPlanet)
                {
                    for (int k = s - 1; k >= 0; k--) linePointsNow[baseIndex + k] = next;
                    break;
                }
            }

            // Middle
            linePointsNow[baseIndex + half] = seedWorld;

            // integrate forward
            p = seedWorld;
            reachedPlanet = false;

            for (int s = half + 1; s < fieldLineSegments; s++)
            {
                Vector3 B = ComputeField(p, t, out _);
                float Bm = Mathf.Max(B.magnitude, eps);

                Vector3 dir = (B / Bm);
                if (constrainLinesToXZPlane) dir.y = 0f;

                if (!(postWarpLinePositions && postWarpAmount > 0.001f))
                    dir = AdjustDirForBoundaries(p, dir, envStrength);
                dir = AdjustDirForPoleApproach(p, dir, tangentWorld);

                float step = lineStep;
                if (useWeakFieldSmallerSteps)
                {
                    float refB = Mathf.Max(1e-4f, weakFieldReference);
                    float tWeak = Mathf.Clamp01(refB / (Bm + refB));
                    step *= Mathf.Lerp(1f, weakFieldMinStepFactor, Smooth01(tWeak));
                }

                float qHere = EnvelopeQ(p, envStrength, out _, out _, out _, out _);
                float wHere = BoundaryEaseWeight(qHere);
                step *= Mathf.Lerp(1f, 1f - boundaryNearDamping, wHere);

                step = Mathf.Min(step, lineStep * maxStepMultiplier);

                Vector3 next = p + dir.normalized * step;
                if (constrainLinesToXZPlane) next.y = dipolePosition.y;

next = ApplyPlanetFootpoint(next, ref reachedPlanet);

bool postWarpActive = postWarpLinePositions && postWarpAmount > 0.001f;

if (!postWarpActive)
{
    next = ConstrainStepToEnvelope(p, next, dir, envStrength);
}

// Always safe to clamp (cheap + prevents tiny overshoots)
next = ApplyMagnetopauseClamp(next, envStrength);


                linePointsNow[baseIndex + s] = next;
                p = next;

                if (reachedPlanet)
                {
                    for (int k = s + 1; k < fieldLineSegments; k++) linePointsNow[baseIndex + k] = next;
                    break;
                }
            }

// -------- visual warble (damped at boundary) + post-warp geometry + re-clamp --------
for (int s = 0; s < fieldLineSegments; s++)
{
    float u = (fieldLineSegments == 1) ? 0f : s / (float)(fieldLineSegments - 1);
    Vector3 pos = linePointsNow[baseIndex + s];

    float rMag = (pos - dipolePosition).magnitude;
    float boundaryW = Smooth01((rMag - magnetosphereRadius * 0.55f) / (magnetosphereRadius * 0.55f));
    float poleW = 1f - Mathf.Abs(Vector3.Dot((pos - dipolePosition).normalized, axisY));

    float warbleEdge = Mathf.Lerp(1f, outerWarbleMultiplier, boundaryW);

    Vector3 wob = WarbleOffset(pos, t, i, u);
    pos = pos + wob * visualWarble * warbleEdge * poleW;

    if (constrainLinesToXZPlane) pos.y = dipolePosition.y;

    // --- NEW: warp the LINE GEOMETRY toward the envelope shape (this is the “stretch with boundary” look) ---
    if (postWarpLinePositions)
    {
        float q = EnvelopeQ(pos, envStrength, out _, out _, out _, out _);

        // start warping from postWarpStartQ and ramp up to the boundary
        // (pow ramp concentrates the effect near q≈1 so mid-line curvature stays smooth)
        float wq = Smoothstep01(postWarpStartQ, 1f, q);
        wq = Mathf.Pow(wq, Mathf.Max(0.25f, postWarpRampExp));

        // also weight by radius so near-planet geometry stays cleaner
        float wr = Smooth01((rMag - planetRadius) / Mathf.Max(1e-4f, magnetosphereRadius - planetRadius));

        float amt = postWarpAmount * wq * wr;

        pos = WarpPositionForWindVisual(pos, effectiveWindDir, envStrength, amt, postWarpPower);
        if (constrainLinesToXZPlane) pos.y = dipolePosition.y;
    }

    // Keep final points inside the envelope
    pos = ApplyMagnetopauseClamp(pos, envStrength);

    linePointsNow[baseIndex + s] = pos;
}


            if (writePrev)
            {
                for (int s = 0; s < fieldLineSegments; s++)
                    linePointsPrev[baseIndex + s] = linePointsNow[baseIndex + s];
            }
        }
    }

    private void ApplyFieldLinesToRenderers(float lerpAlpha)
    {
        for (int i = 0; i < fieldLineCount; i++)
        {
            int baseIndex = i * fieldLineSegments;
            var lr = fieldLines[i];

            for (int s = 0; s < fieldLineSegments; s++)
            {
                int idx = baseIndex + s;
                Vector3 prev = linePointsPrev[idx];
                Vector3 now = linePointsNow[idx];
                Vector3 smoothed = Vector3.Lerp(prev, now, lerpAlpha);

                lr.SetPosition(s, smoothed);
                linePointsPrev[idx] = smoothed;
            }
        }
    }

    // ============================================================
    // Field model (CPU)
    // ============================================================

    
    /// <summary>
    /// Compute the vector field used to integrate lines.
    /// 
    /// IMPORTANT (for your desired "nested arc / gradient-step" look):
    /// - When Post-Warp Line Positions is enabled, we integrate streamlines in an UNWARPED dipole field
    ///   (plus turbulence), then warp the *geometry* afterward to match the envelope. This preserves the
    ///   clean dipole arc shapes and avoids the mid-body "depressed / flattened" look that comes from
    ///   sampling the field in warped space.
    /// - When Post-Warp is disabled, we fall back to the original behavior: sample dipole in warped space
    ///   (and optionally Jacobian-correct direction).
    /// </summary>
    private Vector3 ComputeField(Vector3 pos, float t, out float localWarpStrength)
    {
        if (constrainLinesToXZPlane) pos.y = dipolePosition.y;

        StormMaskAt(pos, out float frontCore, out float wake);

        // System-wide envelope strength
        float globalStrength = globalWindStrengthCached;

        // Optional small local warp so the front feels like it "hits"
        float localExtra = stormIntensity * stormWarpScale * stormFrontLocalWarpAmount * frontCore * (1f - stormGlobalWarpShare);
        localWarpStrength = globalStrength + localExtra;

        bool postWarpMode = postWarpLinePositions && postWarpAmount > 0.001f;

        Vector3 B;

        if (postWarpMode)
        {
            // Integrate in unwarped dipole field for clean nested arcs.
            B = DipoleField(pos);
        }
        else
        {
            // Sample dipole in warped coordinates (envelope remolding).
            Vector3 warpedPos = WarpPositionForWind(pos, effectiveWindDir, localWarpStrength);
            if (constrainLinesToXZPlane) warpedPos.y = dipolePosition.y;

            Vector3 Bwarp = DipoleField(warpedPos);
            B = Bwarp;

            // Optional: Transform direction back out of the warp (prevents "flat" night-side arcs).
            bool doJacobian = useWarpJacobianTransform;
            if (doJacobian)
            {
                Vector3 Bunwarp = UnwarpFieldDirection(Bwarp, pos, localWarpStrength);

                // Make blend respond to inverse power (keeps “proportional” feel as you dial power)
                float blend = warpFieldTransformBlend;
                blend = 1f - Mathf.Pow(1f - blend, Mathf.Max(0.25f, warpFieldInversePower));

                B = Vector3.Lerp(Bwarp, Bunwarp, blend);
            }
        }

        // Turbulence (world space) with edge damping
        Vector3 r = pos - dipolePosition;
        float rMag = Mathf.Max(1e-4f, r.magnitude);
        float boundaryW = Smooth01((rMag - magnetosphereRadius * 0.55f) / (magnetosphereRadius * 0.55f));

        float stormTurb = stormIntensity * (frontCore * 1.0f + wake * 0.6f) * stormTurbulenceScale;

        float turbAmp = turbulenceStrength * (0.15f + 0.85f * boundaryW) * (1f + 0.25f * stormTurb);
        turbAmp *= Mathf.Lerp(1f, outerTurbulenceMultiplier, boundaryW);

        if (turbAmp > 0f)
        {
            Vector3 n = CurlNoise(pos / Mathf.Max(1e-4f, turbulenceScale), t * turbulenceSpeed);
            if (constrainLinesToXZPlane) n.y = 0f;
            B += n * turbAmp;
        }

        if (constrainLinesToXZPlane) B.y = 0f;
        return B;
    }

private Vector3 DipoleField(Vector3 pos)
    {
        Vector3 r = pos - dipolePosition;
        float rMag = r.magnitude;
        if (rMag < 1e-4f) return Vector3.zero;

        Vector3 rHat = r / rMag;
        Vector3 m = axisY * dipoleMoment;

        float mDotR = Vector3.Dot(m, rHat);
        return (3f * mDotR * rHat - m) / (rMag * rMag * rMag);
    }

    private Vector3 WarpPositionForWind(Vector3 pos, Vector3 windDirDownstream, float windStrengthNow)
    {
        if (windStrengthNow <= 1e-6f) return pos;

        Vector3 w = (windDirDownstream.sqrMagnitude < 1e-6f) ? Vector3.right : windDirDownstream.normalized;
        Vector3 r = pos - dipolePosition;

        float along = Vector3.Dot(r, w);
        Vector3 perp = r - w * along;

        GetLocalEnvelope(windStrengthNow, along, out _, out _, out float alongScale, out float perpScale);

        Vector3 warped = dipolePosition + w * (along * alongScale) + perp * perpScale;
        if (constrainLinesToXZPlane) warped.y = dipolePosition.y;

        return warped;
    }

    private Vector3 WarpPositionForWindVisual(Vector3 pos, Vector3 windDirDownstream, float windStrengthNow, float amount, float power)
{
    // Visual (post) warp of the already-integrated line geometry.
    // IMPORTANT: We preserve the original envelope "q" value so points do NOT drift away from (or into) the boundary.
    if (windStrengthNow <= 1e-6f || amount <= 1e-6f) return pos;

    Vector3 w = (windDirDownstream.sqrMagnitude < 1e-6f) ? Vector3.right : windDirDownstream.normalized;

    Vector3 r0 = pos - dipolePosition;
    float along0 = Vector3.Dot(r0, w);
    Vector3 perp0 = r0 - w * along0;

    // Original q (distance in ellipse metric)
    GetLocalEnvelope(windStrengthNow, along0, out float a0, out float b0, out _, out _);
    float perpMag0 = perp0.magnitude;
    float q0 = Mathf.Sqrt((along0 * along0) / (a0 * a0) + (perpMag0 * perpMag0) / (b0 * b0));

    // Local warp scales at the original along coordinate
    GetLocalEnvelope(windStrengthNow, along0, out _, out _, out float alongScale, out float perpScale);
    alongScale = Mathf.Max(1e-4f, alongScale);
    perpScale = Mathf.Max(1e-4f, perpScale);

    float sa = Mathf.Lerp(1f, Mathf.Pow(alongScale, power), amount);
    float sp = Mathf.Lerp(1f, Mathf.Pow(perpScale, power), amount);

    float along1 = along0 * sa;
    Vector3 perp1 = perp0 * sp;

    Vector3 p1 = dipolePosition + w * along1 + perp1;
    if (constrainLinesToXZPlane) p1.y = dipolePosition.y;

    // Compute q after warp, then re-scale in wind-basis so q stays q0 (prevents boundary drift / inversion artifacts).
    Vector3 r1 = p1 - dipolePosition;
    float along1b = Vector3.Dot(r1, w);
    Vector3 perp1b = r1 - w * along1b;

    GetLocalEnvelope(windStrengthNow, along1b, out float a1, out float b1, out _, out _);
    float perpMag1 = perp1b.magnitude;
    float q1 = Mathf.Sqrt((along1b * along1b) / (a1 * a1) + (perpMag1 * perpMag1) / (b1 * b1));

    if (q1 > 1e-4f)
    {
        float s = q0 / q1;
        along1b *= s;
        perp1b *= s;
        p1 = dipolePosition + w * along1b + perp1b;
        if (constrainLinesToXZPlane) p1.y = dipolePosition.y;
    }

    return p1;
}


    // ============================================================
    // Boundary approach: tangent easing + intersection + curvature-following slide
    // ============================================================

    private Vector3 AdjustDirForBoundaries(Vector3 pos, Vector3 dir, float envStrength)
    {
        float q = EnvelopeQ(pos, envStrength, out _, out _, out _, out _);
        float w = BoundaryEaseWeight(q);
        if (w <= 0f) return dir;

        Vector3 n = EnvelopeNormal(pos, envStrength);

        Vector3 tangent = dir - n * Vector3.Dot(dir, n);
        if (constrainLinesToXZPlane) tangent.y = 0f;

        if (tangent.sqrMagnitude < 1e-8f)
            tangent = new Vector3(-n.z, 0f, n.x);

        tangent.Normalize();

        // Ensure tangent points the same general way as the incoming direction (prevents “reversal”)
if (Vector3.Dot(tangent, dir) < 0f) tangent = -tangent;


        Vector3 outDir = Vector3.Slerp(dir.normalized, tangent, w).normalized;
        if (constrainLinesToXZPlane) outDir.y = 0f;
        return outDir.normalized;
    }

    private Vector3 ConstrainStepToEnvelope(Vector3 p, Vector3 next, Vector3 dir, float envStrength)
    {
        float q1 = EnvelopeQ(next, envStrength, out _, out _, out _, out _);
        if (q1 <= 1f) return next;

        float lo = 0f, hi = 1f;
        for (int i = 0; i < boundaryIntersectionIterations; i++)
        {
            float mid = 0.5f * (lo + hi);
            Vector3 m = Vector3.Lerp(p, next, mid);
            float qm = EnvelopeQ(m, envStrength, out _, out _, out _, out _);
            if (qm > 1f) hi = mid; else lo = mid;
        }

        float tHit = hi;
        Vector3 hit = Vector3.Lerp(p, next, tHit);
        if (constrainLinesToXZPlane) hit.y = dipolePosition.y;

        float totalDist = Vector3.Distance(p, next);
        float remain = Mathf.Max(0f, totalDist * (1f - tHit));

        if (remain <= 1e-5f || boundarySlide <= 1e-5f)
            return ApplyMagnetopauseClamp(hit, envStrength);

        Vector3 n = EnvelopeNormal(hit, envStrength);
        Vector3 tangent = dir - n * Vector3.Dot(dir, n);
        if (constrainLinesToXZPlane) tangent.y = 0f;

        if (tangent.sqrMagnitude < 1e-8f)
            tangent = new Vector3(-n.z, 0f, n.x);

        tangent.Normalize();

        // Ensure tangent points the same general way as the incoming direction (prevents “reversal”)
if (Vector3.Dot(tangent, dir) < 0f) tangent = -tangent;


        float slideDist = remain * boundarySlide;
        int sub = Mathf.Max(1, boundarySlideSubsteps);
        float ds = slideDist / sub;

        Vector3 cur = ApplyMagnetopauseClamp(hit, envStrength);
        Vector3 tdir = tangent;

        for (int i = 0; i < sub; i++)
        {
            Vector3 ncur = EnvelopeNormal(cur, envStrength);

            Vector3 tcur = tdir - ncur * Vector3.Dot(tdir, ncur);
            if (constrainLinesToXZPlane) tcur.y = 0f;

            if (tcur.sqrMagnitude < 1e-8f)
                tcur = new Vector3(-ncur.z, 0f, ncur.x);

            tcur.Normalize();
            // Preserve tangent direction continuity over substeps
if (Vector3.Dot(tcur, tdir) < 0f) tcur = -tcur;

            tdir = tcur;

            cur += tcur * ds;
            if (constrainLinesToXZPlane) cur.y = dipolePosition.y;

            cur = ApplyMagnetopauseClamp(cur, envStrength);
        }

        return cur;
    }

    // ============================================================
    // Pole steering (stylized)
    // ============================================================

    private Vector3 AdjustDirForPoleApproach(Vector3 pos, Vector3 dir, Vector3 lineTangent)
    {
        Vector3 r = pos - dipolePosition;
        float rMag = Mathf.Max(1e-4f, r.magnitude);
        float distFromSurface = rMag - planetRadius;

        if (distFromSurface > poleApproachDistance) return dir;

        float w = 1f - Mathf.Clamp01(distFromSurface / Mathf.Max(1e-4f, poleApproachDistance));
        w = Smooth01(w) * poleApproachStrength;

        float sign = Mathf.Sign(Vector3.Dot(r, axisY));
        if (Mathf.Abs(sign) < 1e-3f) sign = 1f;

        Vector3 poleNormal = axisY * sign;
        Vector3 polePoint = dipolePosition + poleNormal * planetRadius;
        Vector3 towardPole = (polePoint - pos).normalized;

        Vector3 tang = lineTangent - poleNormal * Vector3.Dot(lineTangent, poleNormal);
        if (constrainLinesToXZPlane) tang.y = 0f;
        if (tang.sqrMagnitude < 1e-6f) tang = new Vector3(-poleNormal.z, 0f, poleNormal.x);
        tang.Normalize();

        Vector3 desired = Vector3.Slerp(towardPole, tang, poleApproachTangential).normalized;
        Vector3 outDir = Vector3.Slerp(dir, desired, Mathf.Clamp01(w)).normalized;

        if (constrainLinesToXZPlane) outDir.y = 0f;
        if (outDir.sqrMagnitude < 1e-8f) outDir = dir;

        return outDir.normalized;
    }

    private Vector3 ApplyPlanetFootpoint(Vector3 next, ref bool reachedPlanet)
    {
        Vector3 r = next - dipolePosition;
        float rMag = r.magnitude;

        float snapRadius = planetRadius * poleSnapRadiusMultiplier;
        if (rMag <= snapRadius)
        {
            Vector3 rDir = (rMag > 1e-6f) ? (r / rMag) : axisY;

            float sign = Mathf.Sign(Vector3.Dot(rDir, axisY));
            if (Mathf.Abs(sign) < 1e-3f) sign = 1f;

            Vector3 poleDir = (axisY * sign).normalized;
            Vector3 footDir = Vector3.Slerp(rDir, poleDir, poleConvergence).normalized;

            next = dipolePosition + footDir * planetRadius;
            if (constrainLinesToXZPlane) next.y = dipolePosition.y;

            reachedPlanet = true;
        }
        return next;
    }

    // ============================================================
    // Curl noise + warble
    // ============================================================

    private Vector3 CurlNoise(Vector3 p, float t)
    {
        Vector3 f1 = new Vector3(1.31f, 2.17f, 0.91f);
        Vector3 f2 = new Vector3(-1.73f, 0.63f, 2.41f);
        Vector3 f3 = new Vector3(2.27f, -1.11f, 1.53f);

        float p1 = Vector3.Dot(p, f1) + t * 1.1f;
        float p2 = Vector3.Dot(p, f2) + t * 0.9f;
        float p3 = Vector3.Dot(p, f3) + t * 1.3f;

        Vector3 da = Mathf.Cos(p1) * f1;
        Vector3 db = Mathf.Cos(p2) * f2;
        Vector3 dc = Mathf.Cos(p3) * f3;

        return new Vector3(dc.y - db.z, da.z - dc.x, db.x - da.y);
    }

    private Vector3 WarbleOffset(Vector3 pos, float t, int lineId, float u)
    {
        float h = Hash01(lineId);
        float phase = t * 0.9f + h * 12.7f;

        Vector3 r = pos - dipolePosition;
        Vector3 rN = r.sqrMagnitude < 1e-6f ? Vector3.forward : r.normalized;

        Vector3 tangent = Vector3.Cross(axisY, rN);
        if (tangent.sqrMagnitude < 1e-6f) tangent = Vector3.right;
        tangent.Normalize();

        Vector3 bitan = Vector3.Cross(rN, tangent).normalized;

        float w1 = Mathf.Sin(phase + u * 6.283f);
        float w2 = Mathf.Cos(phase * 1.17f + u * 9.111f);

        Vector3 wob = (tangent * w1 + bitan * w2) * 0.5f;
        if (constrainLinesToXZPlane) wob.y = 0f;
        return wob;
    }

    // ============================================================
    // Debug envelope rendering (dashed)
    // ============================================================

    private void EnsureDebugEnvelopeObjects()
    {
        if (!showDebugEnvelope) return;

        if (debugEnvelopeRoot == null)
        {
            debugEnvelopeRoot = new GameObject("DebugMagnetopauseEnvelopes");
            debugEnvelopeRoot.transform.SetParent(transform, false);
            rendererCacheDirty = true;
        }
        debugEnvelopeRoot.SetActive(true);

        if (debugEnvelopeMaterial == null)
            debugEnvelopeMaterial = new Material(Shader.Find("Sprites/Default"));

        if (dashCurrent == null) { dashCurrent = CreateDashSet("Current", debugCurrentColor); rendererCacheDirty = true; }
        if (dashMid == null) { dashMid = CreateDashSet("Mid", debugMidColor); rendererCacheDirty = true; }
        if (dashMax == null) { dashMax = CreateDashSet("Max", debugMaxColor); rendererCacheDirty = true; }
    }

    private DashSet CreateDashSet(string name, Color color)
    {
        DashSet set = new DashSet();
        set.root = new GameObject($"Envelope_{name}");
        set.root.transform.SetParent(debugEnvelopeRoot.transform, false);
        set.color = color;
        return set;
    }

    private void UpdateDebugEnvelopes()
    {
        if (debugEnvelopeRoot == null) return;

        float y = debugEnvelopeInXZ ? dipolePosition.y : dipolePosition.y;

        DrawDashedEnvelope(dashCurrent, globalWindStrengthCached, y);

        if (drawMidAndMaxEnvelopes)
        {
            float midStrength = solarWindStrength + (stormIntensity * stormWarpScale * Mathf.Clamp01(debugMidLevel));
            float maxStrength = solarWindStrength + (stormIntensity * stormWarpScale);

            DrawDashedEnvelope(dashMid, midStrength, y);
            DrawDashedEnvelope(dashMax, maxStrength, y);

            dashMid.root.SetActive(true);
            dashMax.root.SetActive(true);
        }
        else
        {
            dashMid.root.SetActive(false);
            dashMax.root.SetActive(false);
        }
    }

    private void DrawDashedEnvelope(DashSet set, float windStrength, float y)
    {
        int N = Mathf.Clamp(debugEnvelopeSamples, 32, 2048);
        Vector3[] pts = new Vector3[N + 1];

        for (int i = 0; i <= N; i++)
        {
            float a = (i / (float)N) * Mathf.PI * 2f;
            Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)).normalized;

            float r = SolveEnvelopeRadiusAlongDirection(d, windStrength);
            Vector3 p = dipolePosition + d * r;
            p.y = y;

            pts[i] = p;
        }

        float dash = Mathf.Max(0.01f, debugDashLength);
        float gap = Mathf.Max(0.01f, debugGapLength);

        List<List<Vector3>> segments = BuildDashedSegments(pts, dash, gap);
        EnsureDashRendererCount(set, segments.Count);

        for (int i = 0; i < set.dashes.Count; i++)
        {
            var lr = set.dashes[i];
            if (i >= segments.Count)
            {
                lr.gameObject.SetActive(false);
                continue;
            }

            lr.gameObject.SetActive(true);
            var seg = segments[i];

            lr.positionCount = seg.Count;
            for (int p = 0; p < seg.Count; p++)
                lr.SetPosition(p, seg[p]);

            lr.widthMultiplier = debugEnvelopeWidth;
            lr.material = debugEnvelopeMaterial;

            Gradient g = new Gradient();
            g.SetKeys(
                new GradientColorKey[] { new GradientColorKey(set.color, 0f), new GradientColorKey(set.color, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(set.color.a, 0f), new GradientAlphaKey(set.color.a, 1f) }
            );
            lr.colorGradient = g;
        }
    }

    private void EnsureDashRendererCount(DashSet set, int needed)
    {
        int before = set.dashes.Count;

        while (set.dashes.Count < needed)
        {
            var go = new GameObject($"Dash_{set.dashes.Count:000}");
            go.transform.SetParent(set.root.transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.numCornerVertices = 0;
            lr.numCapVertices = 0;
            lr.alignment = LineAlignment.View;

            set.dashes.Add(lr);
        }

        if (set.dashes.Count != before)
            rendererCacheDirty = true;
    }

    private List<List<Vector3>> BuildDashedSegments(Vector3[] polylineClosed, float dashLen, float gapLen)
    {
        var segments = new List<List<Vector3>>(128);

        float target = dashLen;
        bool drawing = true;

        List<Vector3> current = null;
        float acc = 0f;

        for (int i = 0; i < polylineClosed.Length - 1; i++)
        {
            Vector3 a = polylineClosed[i];
            Vector3 b = polylineClosed[i + 1];
            float segLen = Vector3.Distance(a, b);
            if (segLen < 1e-6f) continue;

            float t = 0f;
            while (t < 1f)
            {
                float remain = target - acc;
                float stepT = Mathf.Min(1f - t, remain / segLen);

                Vector3 p0 = Vector3.Lerp(a, b, t);
                Vector3 p1 = Vector3.Lerp(a, b, t + stepT);

                if (drawing)
                {
                    if (current == null)
                    {
                        current = new List<Vector3>(16);
                        current.Add(p0);
                    }
                    current.Add(p1);
                }

                acc += stepT * segLen;
                t += stepT;

                if (acc >= target - 1e-5f)
                {
                    if (drawing && current != null && current.Count >= 2)
                        segments.Add(current);

                    current = null;
                    drawing = !drawing;
                    target = drawing ? dashLen : gapLen;
                    acc = 0f;
                }
            }
        }

        if (drawing && current != null && current.Count >= 2)
            segments.Add(current);

        return segments;
    }

    // ============================================================
    // Optional particles (kept compatible)
    // ============================================================

    private void InitializeCompute()
    {
        if (magnetosphereCompute == null)
        {
            Debug.LogError("Magnetosphere compute shader not assigned.");
            enabled = false;
            return;
        }

        initKernel = magnetosphereCompute.FindKernel("InitParticles");
        updateKernel = magnetosphereCompute.FindKernel("UpdateParticles");
        applyStormKernel = magnetosphereCompute.FindKernel("ApplyStorm");

        int stride = Marshal.SizeOf(typeof(Particle));
        particleBuffer = new ComputeBuffer(particleCount, stride);

        magnetosphereCompute.SetBuffer(initKernel, "particles", particleBuffer);
        magnetosphereCompute.SetBuffer(updateKernel, "particles", particleBuffer);
        magnetosphereCompute.SetBuffer(applyStormKernel, "particles", particleBuffer);

        magnetosphereCompute.SetInt("particleCount", particleCount);
        magnetosphereCompute.SetFloat("particleDamping", particleDamping);
        magnetosphereCompute.SetFloat("velocityLimit", velocityLimit);

        magnetosphereCompute.SetFloat("dipoleMoment", dipoleMoment);
        magnetosphereCompute.SetFloat("magnetosphereRadius", magnetosphereRadius);
        magnetosphereCompute.SetFloat("planetRadius", planetRadius);
        magnetosphereCompute.SetVector("dipolePosition", dipolePosition);
        magnetosphereCompute.SetVector("dipoleAxis", axisY);

        uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
    }

    private void CreateParticleMesh()
    {
        particleMesh = new Mesh { name = "ParticleQuad" };

        Vector3[] vertices = new Vector3[4]
        {
            new Vector3(-0.5f, -0.5f, 0),
            new Vector3( 0.5f, -0.5f, 0),
            new Vector3(-0.5f,  0.5f, 0),
            new Vector3( 0.5f,  0.5f, 0),
        };

        int[] triangles = new int[6] { 0, 2, 1, 2, 3, 1 };

        Vector2[] uv = new Vector2[4]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(0, 1),
            new Vector2(1, 1),
        };

        particleMesh.vertices = vertices;
        particleMesh.triangles = triangles;
        particleMesh.uv = uv;
        particleMesh.RecalculateNormals();
        particleMesh.RecalculateBounds();
    }

    private void InitializeParticles()
    {
        magnetosphereCompute.SetFloat("time", Random.value * 1000f);
        int groups = Mathf.CeilToInt(particleCount / 256f);
        magnetosphereCompute.Dispatch(initKernel, groups, 1, 1);
    }

    private void UpdateCompute()
    {
        magnetosphereCompute.SetFloat("time", simulationTime);
        magnetosphereCompute.SetFloat("deltaTime", Time.deltaTime);

        magnetosphereCompute.SetVector("solarWindDirection", effectiveWindDir);
        magnetosphereCompute.SetFloat("solarWindStrength", solarWindStrength);

        magnetosphereCompute.SetFloat("daysideCompression", daysideCompression);
        magnetosphereCompute.SetFloat("nightsideStretch", nightsideStretch);
        magnetosphereCompute.SetFloat("tailFieldStrength", tailFieldStrength);

        magnetosphereCompute.SetFloat("turbulenceStrength", turbulenceStrength);
        magnetosphereCompute.SetFloat("turbulenceScale", turbulenceScale);
        magnetosphereCompute.SetFloat("turbulenceSpeed", turbulenceSpeed);

        magnetosphereCompute.SetFloat("stormIntensity", stormIntensity);
        magnetosphereCompute.SetFloat("stormActive", stormActive ? 1f : 0f);
        magnetosphereCompute.SetFloat("stormEnvelope", stormEnvelope);
        magnetosphereCompute.SetFloat("stormWarpScale", stormWarpScale);
        magnetosphereCompute.SetFloat("stormTurbulenceScale", stormTurbulenceScale);

        magnetosphereCompute.SetFloat("stormFrontCenterS", stormFrontCenterS);
        magnetosphereCompute.SetFloat("stormFrontThickness", stormFrontThickness);
        magnetosphereCompute.SetFloat("stormWakeLength", stormWakeLength);
        magnetosphereCompute.SetFloat("stormFrontFeather", stormFrontFeather);

        int groups = Mathf.CeilToInt(particleCount / 256f);

        if (stormActive)
            magnetosphereCompute.Dispatch(applyStormKernel, groups, 1, 1);

        magnetosphereCompute.Dispatch(updateKernel, groups, 1, 1);
    }

    private void RenderParticles()
    {
        if (particleMesh == null || argsBuffer == null) return;

        particleMaterial.SetBuffer("particles", particleBuffer);
        particleMaterial.SetFloat("_ParticleSize", particleSize);
        particleMaterial.SetVector("_DipolePosition", dipolePosition);

        uint[] args = new uint[5]
        {
            particleMesh.GetIndexCount(0),
            (uint)particleCount,
            particleMesh.GetIndexStart(0),
            particleMesh.GetBaseVertex(0),
            0
        };
        argsBuffer.SetData(args);

        Graphics.DrawMeshInstancedIndirect(
            particleMesh,
            0,
            particleMaterial,
            bounds,
            argsBuffer,
            castShadows: UnityEngine.Rendering.ShadowCastingMode.Off,
            receiveShadows: false,
            layer: gameObject.layer
        );
    }

    private void ReleaseBuffers()
    {
        if (particleBuffer != null) { particleBuffer.Release(); particleBuffer = null; }
        if (argsBuffer != null) { argsBuffer.Release(); argsBuffer = null; }
    }

    // ============================================================
    // Utility helpers
    // ============================================================

    private static float Hash01(int i)
    {
        uint x = (uint)i;
        x ^= x >> 16;
        x *= 0x7feb352du;
        x ^= x >> 15;
        x *= 0x846ca68bu;
        x ^= x >> 16;
        return (x & 0x00FFFFFF) / 16777216f;
    }

    private static float Smooth01(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    private static float Smoothstep01(float edge0, float edge1, float x)
    {
        if (edge1 <= edge0) return (x < edge0) ? 0f : 1f;
        float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }
}
