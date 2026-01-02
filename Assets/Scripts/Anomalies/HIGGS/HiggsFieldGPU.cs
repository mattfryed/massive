using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// GPU-driven Higgs underlay field:
/// - Updates drifting metaball "bubbles" on GPU
/// - Rasterizes: height (with excite), base height (no excite), excite mask
/// - Feeds textures into a URP topo shader
///
/// NEW:
/// - Supports per-bubble "ExciteHoldUntil" (absolute world time) so gameplay
///   systems can keep a given excitation alive long enough for knot lifetimes.
/// - Supports a gameplay-controlled excitation spawn RATE (excitations/sec),
///   converted internally into per-bubble chance so knot pacing is decoupled
///   from bubble soup visuals.
/// </summary>
[DisallowMultipleComponent]
public class HiggsFieldGPU : MonoBehaviour
{
    // ============================================================
    // Inspector: Assets / Target
    // ============================================================

    [Header("Assets")]
    [SerializeField] private ComputeShader higgsCompute;
    [SerializeField] private Material higgsUnderlayMaterial;

    [Tooltip("Optional. If null, tries to grab MeshRenderer on this GameObject.")]
    [SerializeField] private MeshRenderer targetRenderer;

    // ============================================================
    // Inspector: Plane placement
    // ============================================================

    [Header("Underlay Plane")]
    [Tooltip("World-space size (X,Z) of the Higgs underlay plane.")]
    [SerializeField] private Vector2 worldSizeXZ = new Vector2(40f, 24f);

    [Tooltip("World-space Y position of the underlay plane.")]
    [SerializeField] private float underlayY = -0.35f;

    // ============================================================
    // Inspector: Simulation output resolution
    // ============================================================

    [Header("Heightmap")]
    [SerializeField, Range(128, 1024)]
    private int textureSize = 512;

    // ============================================================
    // Inspector: Metaballs
    // ============================================================

    [Header("Bubbles / Metaballs")]
    [SerializeField, Range(4, 256)]
    private int maxBubbles = 64;

    [SerializeField, Range(1, 256)]
    private int minActiveBubbles = 12;

    [Tooltip("Bubble radii in world units.")]
    [SerializeField] private Vector2 radiusRange = new Vector2(0.25f, 1.8f);

    [Tooltip("Bubble amplitude contribution range (can be negative).")]
    [SerializeField] private Vector2 ampRange = new Vector2(-0.18f, 0.18f);

    [Tooltip("Base offset added to heightmap before bubbles (negative pushes field 'below').")]
    [SerializeField] private float baseOffset = -0.12f;

    // ============================================================
    // Inspector: Motion field (positions)
    // ============================================================

    [Header("Motion")]
    [SerializeField] private float driftSpeedUV = 0.06f;
    [SerializeField] private float noiseScale = 3.5f;
    [SerializeField, Range(0f, 1f)] private float velocityDamping = 0.92f;

    // ============================================================
    // Inspector: Amplitude animation (per-bubble)
    // ============================================================

    [Header("Amplitude Animation")]
    [Tooltip("Phase speed range. Each bubble picks a stable random speed within this range.")]
    [SerializeField] private Vector2 phaseSpeedRange = new Vector2(0.6f, 1.2f);

    [Header("Amplitude Noise Wobble")]
    [Tooltip("Adds slow smooth noise to the amplitude (breaks up periodic sine pulsing).")]
    [SerializeField] private float ampNoiseStrength = 0.08f;

    [Tooltip("Time speed for amplitude wobble.")]
    [SerializeField] private float ampNoiseSpeed = 0.18f;

    [Tooltip("Spatial scale for amplitude wobble sampling in UV space.")]
    [SerializeField] private float ampNoiseSpatialScale = 2.0f;

    // ============================================================
    // Inspector: Excitations
    // ============================================================

    [Header("Excitations (visual + gameplay trigger source)")]
    [Tooltip("Per-bubble probability per second to become a high peak (scaled by ramp). Used when gameplay override is NOT set.")]
    [SerializeField] private Vector2 exciteChancePerSecond = new Vector2(0.02f, 0.16f);

    [Tooltip("Amplitude used while excited (strong positive peak).")]
    [SerializeField] private float exciteAmp = 0.85f;

    [Tooltip("Excited bubbles tighten up a bit.")]
    [SerializeField] private float exciteRadiusMultiplier = 0.55f;

    [SerializeField] private float exciteDuration = 3.0f;

    [Tooltip("Seconds for excitation to ease in/out (prevents popping).")]
    [SerializeField] private float exciteFadeTime = 0.35f;

    [Tooltip("World-units border padding where excitations cannot trigger.")]
    [SerializeField] private float exciteBorderWorld = 0.75f;

    [Header("Excitations - Extra Smoothing")]
[Tooltip("Shapes the excitation fade-in. 1 = normal. >1 = gentler/slower merge-in.")]
[SerializeField] private float exciteInEasePower = 2.0f;


    // ============================================================
    // NEW: Gameplay pacing override
    // ============================================================

    [Header("Gameplay Excitation Pacing Override")]
    [Tooltip("If >= 0, overrides excitation spawning with a target NEW excitations per second.\n" +
             "This is converted into per-bubble chance internally, decoupling gameplay pacing from bubble count.\n" +
             "Set from HiggsExcitationKnotManager.")]
    [SerializeField] private float gameplayExcitationsPerSecondOverride = -1f;

    [Tooltip("If true, compensates for excite border padding so the global rate stays closer to target.")]
    [SerializeField] private bool compensateForBorderArea = true;

    /// <summary>Last computed active bubble count (debug + for external systems).</summary>
    public int ActiveBubblesLastFrame { get; private set; }

    /// <summary>
    /// Set target NEW excitations per second. (0 pauses new excitations; negative clears override)
    /// </summary>
    public void SetGameplayExcitationsPerSecond(float perSecond)
    {
        gameplayExcitationsPerSecondOverride = perSecond < 0f ? -1f : Mathf.Max(0f, perSecond);
    }

    /// <summary>Convenience helper: set target NEW excitations per minute.</summary>
    public void SetGameplayExcitationsPerMinute(float perMinute)
    {
        if (perMinute < 0f) { gameplayExcitationsPerSecondOverride = -1f; return; }
        gameplayExcitationsPerSecondOverride = Mathf.Max(0f, perMinute) / 60f;
    }

    /// <summary>Revert to inspector-driven exciteChancePerSecond (ramped) behavior.</summary>
    public void ClearGameplayExcitationRateOverride()
    {
        gameplayExcitationsPerSecondOverride = -1f;
    }

    // ============================================================
    // Inspector: Session ramp
    // ============================================================

    [Header("Session Ramp")]
    [Tooltip("Seconds to ramp from early game (fewer) to late game (many).")]
    [SerializeField] private float rampSeconds = 600f;

    [Tooltip("If >= 0, overrides ramp (0..1). Otherwise uses elapsed / rampSeconds.")]
    [SerializeField, Range(-1f, 1f)]
    private float rampOverride01 = -1f;

    // ============================================================
    // GPU Data + Internals
    // ============================================================

    [StructLayout(LayoutKind.Sequential)]
    private struct Bubble
    {
        public Vector2 posUV;
        public Vector2 velUV;
        public float amp;
        public float radius;
        public float phase;
        public float exciteT;

        // Must match compute shader layout
        public float exciteAge;
        public float pad0;

        // Used for baseAmp/exciteW storage in compute
        public Vector2 pad;
    }

    private ComputeBuffer bubbleBuffer;

    // Per-bubble absolute "hold until" times (world seconds)
    private ComputeBuffer exciteHoldUntilBuffer;
    private float[] exciteHoldUntilCPU;
    private bool exciteHoldDirty;

    // Raster outputs
    private RenderTexture heightRT;      // full (with excitations)
    private RenderTexture heightBaseRT;  // base (no excitations)
    private RenderTexture exciteRT;      // excitation mask

    public RenderTexture HeightTexture => heightRT;
    public RenderTexture HeightBaseTexture => heightBaseRT;
    public RenderTexture ExciteTexture => exciteRT;

    // Optional useful access for manager/readback
    public ComputeBuffer BubbleBuffer => bubbleBuffer;
    public Vector2 WorldSizeXZ => worldSizeXZ;
    public int MaxBubbles => maxBubbles;

    private int kInit, kUpdate, kRaster;
    private float elapsed;

    // ---- Compute property IDs (match HiggsFieldSim.compute) ----
    private static readonly int PID_Bubbles = Shader.PropertyToID("_Bubbles");
    private static readonly int PID_MaxBubbles = Shader.PropertyToID("_MaxBubbles");
    private static readonly int PID_ActiveBubbles = Shader.PropertyToID("_ActiveBubbles");

    private static readonly int PID_DT = Shader.PropertyToID("_DT");
    private static readonly int PID_Time = Shader.PropertyToID("_Time");

    private static readonly int PID_NoiseScale = Shader.PropertyToID("_NoiseScale");
    private static readonly int PID_DriftSpeedUV = Shader.PropertyToID("_DriftSpeedUV");
    private static readonly int PID_VelDamping = Shader.PropertyToID("_VelDamping");

    private static readonly int PID_AmpMinMax = Shader.PropertyToID("_AmpMinMax");
    private static readonly int PID_RadiusMinMax = Shader.PropertyToID("_RadiusMinMax");
    private static readonly int PID_BaseOffset = Shader.PropertyToID("_BaseOffset");

    private static readonly int PID_ExciteChance = Shader.PropertyToID("_ExciteChancePerSecond");
    private static readonly int PID_ExciteAmp = Shader.PropertyToID("_ExciteAmp");
    private static readonly int PID_ExciteRadiusMul = Shader.PropertyToID("_ExciteRadiusMul");
    private static readonly int PID_ExciteDuration = Shader.PropertyToID("_ExciteDuration");
    private static readonly int PID_ExciteFadeTime = Shader.PropertyToID("_ExciteFadeTime");
    private static readonly int PID_ExciteBorderWorld = Shader.PropertyToID("_ExciteBorderWorld");
    private static readonly int PID_ExciteInEasePower = Shader.PropertyToID("_ExciteInEasePower");


    private static readonly int PID_PhaseSpeedMinMax = Shader.PropertyToID("_PhaseSpeedMinMax");
    private static readonly int PID_AmpNoiseStrength = Shader.PropertyToID("_AmpNoiseStrength");
    private static readonly int PID_AmpNoiseSpeed = Shader.PropertyToID("_AmpNoiseSpeed");
    private static readonly int PID_AmpNoiseSpatialScale = Shader.PropertyToID("_AmpNoiseSpatialScale");

    private static readonly int PID_WorldSizeXZ = Shader.PropertyToID("_WorldSizeXZ");
    private static readonly int PID_TexSize = Shader.PropertyToID("_TexSize");

    private static readonly int PID_HeightTex = Shader.PropertyToID("_HiggsHeight");
    private static readonly int PID_HeightBaseTex = Shader.PropertyToID("_HiggsHeightBase");
    private static readonly int PID_ExciteTex = Shader.PropertyToID("_HiggsExcite");

    // Hold buffer binding
    private static readonly int PID_ExciteHoldUntil = Shader.PropertyToID("_ExciteHoldUntil");

    // ---- Material property IDs (match HiggsUnderlayTopo_URP.shader) ----
    private static readonly int MID_HeightTex = Shader.PropertyToID("_HiggsHeight");
    private static readonly int MID_ExciteTex = Shader.PropertyToID("_HiggsExcite");

    private int _lastActiveBubbles;
public int ActiveBubbles => _lastActiveBubbles;


    // ============================================================
    // Public API for gameplay systems
    // ============================================================

    /// <summary>
    /// Ensures a given bubble's excitation stays alive until an absolute world time.
    /// If multiple callers set it, the latest (max) wins.
    /// Time base must match compute (_Time), which we set to Time.time.
    /// </summary>
    public void SetExcitationHoldUntil(int bubbleIndex, float holdUntilWorldTime)
    {
        if (bubbleIndex < 0 || bubbleIndex >= maxBubbles)
            return;

        EnsureHoldBuffer();

        // Only extend (never shrink)
        if (holdUntilWorldTime > exciteHoldUntilCPU[bubbleIndex] + 0.0001f)
        {
            exciteHoldUntilCPU[bubbleIndex] = holdUntilWorldTime;
            exciteHoldDirty = true;
        }
    }

    /// <summary>Clear hold for a single bubble.</summary>
    public void ClearExciteHold(int bubbleIndex)
    {
        if (bubbleIndex < 0 || bubbleIndex >= maxBubbles)
            return;

        EnsureHoldBuffer();

        if (exciteHoldUntilCPU[bubbleIndex] != 0f)
        {
            exciteHoldUntilCPU[bubbleIndex] = 0f;
            exciteHoldDirty = true;
        }
    }

    /// <summary>Clear all holds.</summary>
    public void ClearAllExciteHolds()
    {
        EnsureHoldBuffer();
        Array.Clear(exciteHoldUntilCPU, 0, exciteHoldUntilCPU.Length);
        exciteHoldDirty = true;
    }

    // ============================================================

    private void Reset()
    {
        targetRenderer = GetComponent<MeshRenderer>();
    }

    private void OnEnable()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<MeshRenderer>();

        EnsurePlaneTransform();
        EnsureResources();

        elapsed = 0f;

        if (targetRenderer != null && higgsUnderlayMaterial != null)
            targetRenderer.sharedMaterial = higgsUnderlayMaterial;
    }

    private void OnDisable()
    {
        ReleaseResources();
    }

    private void Update()
    {
        if (higgsCompute == null || higgsUnderlayMaterial == null)
            return;

        // Defensive: recreate resources if domain reload / disable-enable broke them
        if (bubbleBuffer == null ||
            heightRT == null || !heightRT.IsCreated() ||
            heightBaseRT == null || !heightBaseRT.IsCreated() ||
            exciteRT == null || !exciteRT.IsCreated() ||
            exciteHoldUntilBuffer == null)
        {
            EnsureResources();

            if (bubbleBuffer == null || heightRT == null || heightBaseRT == null || exciteRT == null || exciteHoldUntilBuffer == null)
                return;
        }

        // Upload hold times if needed (max 256 floats -> cheap)
        EnsureHoldBuffer();
        if (exciteHoldDirty)
        {
            exciteHoldUntilBuffer.SetData(exciteHoldUntilCPU);
            exciteHoldDirty = false;
        }

        float dt = Application.isPlaying ? Time.deltaTime : (1f / 60f);
        elapsed += Application.isPlaying ? dt : 0f;

        float ramp01 = (rampOverride01 >= 0f)
            ? Mathf.Clamp01(rampOverride01)
            : (rampSeconds <= 0.001f ? 1f : Mathf.Clamp01(elapsed / rampSeconds));

        int activeBubbles = Mathf.Clamp(
            Mathf.RoundToInt(Mathf.Lerp(minActiveBubbles, maxBubbles, ramp01)),
            1, maxBubbles
        );

        _lastActiveBubbles = activeBubbles;

        // ------------------------------------------------------------
        // Excitation spawn chance:
        // - Default: inspector vector (min->max) ramped by session.
        // - Override: gameplay provides "excitations per second" -> convert to chance/bubble/sec.
        // ------------------------------------------------------------
        float exciteChancePerBubblePerSecond;
        if (gameplayExcitationsPerSecondOverride >= 0f)
        {
            float targetPerSecond = gameplayExcitationsPerSecondOverride;

            // Convert global target into per-bubble chance
            exciteChancePerBubblePerSecond = targetPerSecond / Mathf.Max(1, activeBubbles);

            // Optional border compensation so global rate stays closer to target even with large margins.
            if (compensateForBorderArea)
            {
                float insideFrac = ComputeInsideAreaFraction();
                if (insideFrac > 0.0001f)
                    exciteChancePerBubblePerSecond /= insideFrac;
            }
        }
        else
        {
            // Old behavior: chance itself ramps with session (and is implicitly multiplied by bubble count).
            exciteChancePerBubblePerSecond = Mathf.Lerp(exciteChancePerSecond.x, exciteChancePerSecond.y, ramp01);
        }

        // ---- Push params to compute ----
        higgsCompute.SetInt(PID_MaxBubbles, maxBubbles);
        higgsCompute.SetInt(PID_ActiveBubbles, activeBubbles);
        higgsCompute.SetFloat(PID_DT, dt);
        higgsCompute.SetFloat(PID_Time, Time.time);

        higgsCompute.SetFloat(PID_NoiseScale, noiseScale);
        higgsCompute.SetFloat(PID_DriftSpeedUV, driftSpeedUV);
        higgsCompute.SetFloat(PID_VelDamping, velocityDamping);

        higgsCompute.SetVector(PID_AmpMinMax, ampRange);
        higgsCompute.SetVector(PID_RadiusMinMax, radiusRange);
        higgsCompute.SetFloat(PID_BaseOffset, baseOffset);

        higgsCompute.SetFloat(PID_ExciteChance, exciteChancePerBubblePerSecond);
        higgsCompute.SetFloat(PID_ExciteAmp, exciteAmp);
        higgsCompute.SetFloat(PID_ExciteRadiusMul, exciteRadiusMultiplier);
        higgsCompute.SetFloat(PID_ExciteDuration, exciteDuration);
        higgsCompute.SetFloat(PID_ExciteFadeTime, exciteFadeTime);
        higgsCompute.SetFloat(PID_ExciteBorderWorld, exciteBorderWorld);
        higgsCompute.SetFloat(PID_ExciteInEasePower, exciteInEasePower);


        higgsCompute.SetVector(PID_PhaseSpeedMinMax, phaseSpeedRange);
        higgsCompute.SetFloat(PID_AmpNoiseStrength, ampNoiseStrength);
        higgsCompute.SetFloat(PID_AmpNoiseSpeed, ampNoiseSpeed);
        higgsCompute.SetFloat(PID_AmpNoiseSpatialScale, ampNoiseSpatialScale);

        higgsCompute.SetVector(PID_WorldSizeXZ, worldSizeXZ);
        higgsCompute.SetInt(PID_TexSize, textureSize);

        // ---- Bind resources ----
        higgsCompute.SetBuffer(kUpdate, PID_Bubbles, bubbleBuffer);
        higgsCompute.SetBuffer(kUpdate, PID_ExciteHoldUntil, exciteHoldUntilBuffer);

        higgsCompute.SetBuffer(kRaster, PID_Bubbles, bubbleBuffer);
        higgsCompute.SetTexture(kRaster, PID_HeightTex, heightRT);
        higgsCompute.SetTexture(kRaster, PID_HeightBaseTex, heightBaseRT);
        higgsCompute.SetTexture(kRaster, PID_ExciteTex, exciteRT);

        // ---- Dispatch ----
        int tgBubbles = Mathf.CeilToInt(maxBubbles / 64f);
        higgsCompute.Dispatch(kUpdate, tgBubbles, 1, 1);

        int tgTex = Mathf.CeilToInt(textureSize / 8f);
        higgsCompute.Dispatch(kRaster, tgTex, tgTex, 1);

        // ---- Feed textures into underlay material ----
        higgsUnderlayMaterial.SetTexture(MID_HeightTex, heightRT);
        higgsUnderlayMaterial.SetTexture(MID_ExciteTex, exciteRT);
    }

    /// <summary>
    /// Computes what fraction of UV space is eligible for excitation triggering,
    /// given the exciteBorderWorld margin and the current worldSizeXZ.
    /// </summary>
    private float ComputeInsideAreaFraction()
    {
        float mu = (Mathf.Abs(worldSizeXZ.x) <= 0.0001f) ? 0f : (exciteBorderWorld / worldSizeXZ.x);
        float mv = (Mathf.Abs(worldSizeXZ.y) <= 0.0001f) ? 0f : (exciteBorderWorld / worldSizeXZ.y);

        float wu = Mathf.Clamp01(1f - 2f * Mathf.Abs(mu));
        float wv = Mathf.Clamp01(1f - 2f * Mathf.Abs(mv));
        return wu * wv;
    }

    private void EnsureHoldBuffer()
    {
        if (exciteHoldUntilCPU == null || exciteHoldUntilCPU.Length != maxBubbles)
        {
            exciteHoldUntilCPU = new float[maxBubbles];
            exciteHoldDirty = true;
        }

        if (exciteHoldUntilBuffer == null || exciteHoldUntilBuffer.count != maxBubbles)
        {
            exciteHoldUntilBuffer?.Release();
            exciteHoldUntilBuffer = new ComputeBuffer(maxBubbles, sizeof(float), ComputeBufferType.Structured);
            exciteHoldDirty = true;
        }
    }

    /// <summary>
    /// Allocates GPU resources and initializes bubble state.
    /// Safe to call multiple times (recreates resources).
    /// </summary>
    private void EnsureResources()
    {
        if (higgsCompute == null)
            return;

        // Kernel IDs
        kInit = higgsCompute.FindKernel("InitBubbles");
        kUpdate = higgsCompute.FindKernel("UpdateBubbles");
        kRaster = higgsCompute.FindKernel("RasterizeHeight");

        // Bubble buffer
        int stride = Marshal.SizeOf(typeof(Bubble));
        bubbleBuffer?.Release();
        bubbleBuffer = new ComputeBuffer(maxBubbles, stride, ComputeBufferType.Structured);

        // Hold buffer (float per bubble)
        EnsureHoldBuffer();

        // Height RT (full)
        if (heightRT != null) heightRT.Release();
        heightRT = CreateRFloatRT("HIGGS_HeightRT");

        // Height RT (base/no-excite)
        if (heightBaseRT != null) heightBaseRT.Release();
        heightBaseRT = CreateRFloatRT("HIGGS_HeightBaseRT");

        // Excitation RT
        if (exciteRT != null) exciteRT.Release();
        exciteRT = CreateRFloatRT("HIGGS_ExciteRT");

        // Init bubbles
        higgsCompute.SetInt(PID_MaxBubbles, maxBubbles);
        higgsCompute.SetVector(PID_AmpMinMax, ampRange);
        higgsCompute.SetVector(PID_RadiusMinMax, radiusRange);
        higgsCompute.SetVector(PID_WorldSizeXZ, worldSizeXZ);

        higgsCompute.SetBuffer(kInit, PID_Bubbles, bubbleBuffer);

        int tgBubbles = Mathf.CeilToInt(maxBubbles / 64f);
        higgsCompute.Dispatch(kInit, tgBubbles, 1, 1);
    }

    private RenderTexture CreateRFloatRT(string name)
    {
        var rt = new RenderTexture(textureSize, textureSize, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear)
        {
            enableRandomWrite = true,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            useMipMap = false,
            autoGenerateMips = false,
            name = name
        };
        rt.Create();
        return rt;
    }

    /// <summary>
    /// Releases GPU resources.
    /// </summary>
    private void ReleaseResources()
    {
        bubbleBuffer?.Release();
        bubbleBuffer = null;

        exciteHoldUntilBuffer?.Release();
        exciteHoldUntilBuffer = null;
        exciteHoldUntilCPU = null;
        exciteHoldDirty = false;

        if (heightRT != null) { heightRT.Release(); heightRT = null; }
        if (heightBaseRT != null) { heightBaseRT.Release(); heightBaseRT = null; }
        if (exciteRT != null) { exciteRT.Release(); exciteRT = null; }
    }

    /// <summary>
    /// Positions/scales this object as a flat XZ plane under the grid.
    /// Assumes you're using a Unity Quad (1x1 in XY) and rotates it onto XZ.
    /// </summary>
    private void EnsurePlaneTransform()
    {
        transform.position = new Vector3(transform.position.x, underlayY, transform.position.z);
        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        transform.localScale = new Vector3(worldSizeXZ.x, worldSizeXZ.y, 1f);

        if (targetRenderer == null)
            targetRenderer = GetComponent<MeshRenderer>();
    }

    // Optional external control (later: session/anomaly manager)
    public void SetRampOverride01(float ramp01) => rampOverride01 = Mathf.Clamp01(ramp01);
    public void ClearRampOverride() => rampOverride01 = -1f;
}
