using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class MagnetosphereFieldLinesGPU2D : MonoBehaviour
{
    // ============================================================
    // Compute + Material
    // ============================================================
    [Header("Compute + Material")]
    public ComputeShader fieldCS;
    public Material lineMaterial;
    private MaterialPropertyBlock _mpb;

    [Header("Rendering")]
    [Range(0f, 1f)] public float alpha = 1.0f;

    // Legacy (your current shader uses these)
    public Color lineColorNear = new Color(1f, 0.75f, 0.70f, 1f);
    public Color lineColorFar  = new Color(1f, 0.00f, 0.60f, 1f);
    [Min(0.0001f)] public float colorByMagScale = 1.0f;

    // ============================================================
    // Trace
    // ============================================================
    [Header("Trace")]
    [Min(16)] public int seedCount = 256;
    [Min(16)] public int maxSteps = 160;
    [Min(0.001f)] public float step = 0.08f;

    [Tooltip("Stops tracing when |B| < minFieldMag. If far-tail lines die early, lower this.")]
    [Min(0f)] public float minFieldMag = 0.01f;

    [Header("Pole Return Assist")]
    [Tooltip("Helps far-tail lines reach poles by lowering minFieldMag and increasing maxSteps automatically.")]
    public bool forceReturnToPoles = false;

    [Min(32)] public int forceMinSteps = 320;
    [Min(0f)] public float forceMinFieldMag = 0.0f;

    // ============================================================
    // Seeding (in deformed envelope)
    // ============================================================
    [Header("Seeding (fills deformed envelope)")]
    [Range(0.01f, 0.95f)] public float seedInnerFill = 0.12f;
    [Range(0.05f, 1.0f)] public float seedOuterFill = 0.98f;
    [Range(0.25f, 4f)] public float seedRadialPower = 1.0f;
    [Range(0f, 1f)] public float seedJitter = 0.10f;
    public uint randomSeed = 12345;

    // ============================================================
    // Dipole
    // ============================================================
    [Header("Dipole")]
    [Min(0.0001f)] public float dipoleMoment = 170f;
    [Min(0.0001f)] public float planetRadius = 0.03f;
    [Min(0.1f)] public float magnetosphereRadius = 13f;

    public Vector3 dipolePosition = Vector3.zero;

    [Tooltip("Mode A (up/down on screen): (0,0,1). XZ-only.")]
    public Vector3 dipoleAxis = new Vector3(0f, 0f, 1f);

    // ============================================================
    // Wind / envelope shaping (same knobs as CPU)
    // ============================================================
    [Header("Wind / Global Shape")]
    [Tooltip("DOWNSTREAM direction (tail direction). XZ only.")]
    public Vector3 solarWindDirection = Vector3.right;

    [Range(0f, 6f)] public float solarWindStrength = 0.0f;
    [Range(0f, 2.5f)] public float daysideCompression = 0.0f;
    [Range(0f, 6.0f)] public float nightsideStretch = 0.0f;
    [Range(0f, 6.0f)] public float tailFieldStrength = 0.0f;

    [Header("Envelope Response Curve")]
    [Range(0.01f, 3f)] public float pressureGain = 0.35f;
    public bool useSquaredPressure = true;

    [Range(0.25f, 6f)] public float daysideResponseExp = 1.6f;
    [Range(0.25f, 6f)] public float tailResponseExp = 2.0f;
    [Range(0.25f, 6f)] public float tailPerpResponseExp = 1.4f;

    [Header("Safe Zone Envelope (concept model)")]
    public float magnetopauseBlendWidth = 0f;
    [Range(0.05f, 1f)] public float dayPerpCompressionRatio = 0.35f;
    [Range(0.05f, 1f)] public float tailPerpFlareRatio = 0.35f;

    [Header("Stronger Storm Compression")]
    [Range(0.03f, 0.50f)] public float minDaysideAlongScale = 0.10f;
    [Range(0.03f, 0.80f)] public float minDaysidePerpScale = 0.18f;
    [Range(0f, 8f)] public float stormDaysideCompressionBoost = 3.0f;

    [Header("Flank / Terminator Pinch")]
    [Range(0f, 8f)] public float flankPinchStrength = 2.0f;
    [Range(0.05f, 1f)] public float minFlankPerpScale = 0.55f;
    public float flankPinchWidth = 0f;
    [Range(0.25f, 6f)] public float flankPinchResponseExp = 1.5f;

    // ============================================================
    // Storm scheduling
    // ============================================================
    [Header("Storm Settings")]
    public float stormInterval = 2.0f;
    public float stormDuration = 2.0f;
    public float stormEaseInTime = 0.50f;
    public float stormEaseOutTime = 0.75f;

    [Range(0f, 20f)] public float stormIntensity = 4.5f;
    [Range(0f, 3f)] public float stormWarpScale = 0.25f;

    [Header("Storm Front Shape")]
    public float stormFrontThickness = 5.5f;
    public float stormWakeLength = 12.0f;
    [Range(0f, 1f)] public float stormFrontFeather = 0.65f;

    [Header("System-wide vs local storm influence")]
    [Range(0f, 1f)] public float stormFrontLocalWarpAmount = 0.15f;
    [Range(0f, 1f)] public float stormGlobalWarpShare = 0.35f;

    [Header("Wind Randomization")]
    public bool randomizeWindOnStormStart = true;
    public bool driftWindWhenCalm = false;
    [Range(0f, 1f)] public float calmWindDriftAmount = 0.15f;
    [Range(0.05f, 2f)] public float calmWindDriftSpeed = 0.20f;

    // ============================================================
    // Motion (life)
    // ============================================================
    [Header("GPU Motion (life)")]
    public float spinStrength = 0.5f;
    public float spinSpeed = 0.25f;
    public float warbleStrength = 0.0f;
    public float warbleSpeed = 1.1f;

    [Header("Path Warble (requires compute support)")]
    [Tooltip("Visual bend/warble along arc path (requires compute to apply it to segment endpoints).")]
    public float pathWarbleStrength = 0.0f;
    public float pathWarbleSpeed = 0.7f;
    public float pathWarbleFrequency = 2.0f;

    // ============================================================
    // 3D illusion (requires compute+shader support)
    // ============================================================
    [Header("Pseudo 3D (requires compute+shader support)")]
    public bool pseudo3DEnabled = false;

    [Tooltip("Amplitude of pseudo depth (Y offset) used to fake loops going behind/in front.")]
    public float pseudo3DAmplitude = 0.35f;

    public float pseudo3DSpeed = 0.35f;
    public float pseudo3DTwist = 1.0f;

    [Tooltip("If enabled, dim arcs when they are on the 'back' side.")]
    public bool pseudo3DDimBackSide = true;

    [Range(0f, 1f)] public float backSideBrightness = 0.45f;
    [Range(0f, 1f)] public float backSideAlpha = 0.55f;

    // ============================================================
    // Color controls (requires FieldLines.shader update)
    // ============================================================
    [Header("Color (requires FieldLines.shader update)")]
    public bool useEnhancedColor = false;

    [Tooltip("Color at arcs closest to center (inner loops).")]
    public Color arcInnerColor = new Color(1f, 0.75f, 0.70f, 1f);

    [Tooltip("Color at arcs farthest out (outer loops).")]
    public Color arcOuterColor = new Color(1f, 0.0f, 0.60f, 1f);

    [Tooltip("Overlay color near pole footpoints.")]
    public Color poleOverlayColor = new Color(1f, 0.2f, 0.9f, 1f);

    [Tooltip("Pole overlay radius in world units.")]
    public float poleOverlayRadius = 0.45f;

    [Range(0f, 2f)] public float poleOverlayStrength = 1.0f;
    [Range(0.25f, 8f)] public float poleOverlayPower = 2.0f;

    [Tooltip("Scale used for radial gradient normalization (bigger = slower transition).")]
    public float radialColorRadiusScale = 1.0f;

    // ============================================================
    // Warp controls (if compute supports)
    // ============================================================
    [Header("Warp (if compute supports it)")]
    [Range(0f, 1f)] public float warpAmount = 0.0f;
    [Range(0f, 1f)] public float warpAlongAmount = 1.0f;
    [Range(0f, 1f)] public float warpPerpAmount = 0.0f;
    [Range(0f, 0.95f)] public float warpStartQ = 0.60f;
    [Range(0.25f, 6f)] public float warpRampExp = 2.5f;

    // ============================================================
    // Boundary drape (if compute supports)
    // ============================================================
    [Header("Boundary Approach (drape)")]
    [Range(0.5f, 0.99f)] public float boundaryEaseStart = 0.90f;
    [Range(0.25f, 8f)] public float boundaryEasePower = 2.0f;

    // ============================================================
    // Global Conformity (pre-drape) – makes arcs gradually align to the envelope before reaching the boundary.
    // ============================================================
    [Header("Field-Line Conformity (pre-drape)")]
    [Range(0f, 1f)] public float conformAmount = 0.55f;
    [Range(0f, 0.95f)] public float conformStartQ = 0.15f;
    [Range(0.25f, 6f)] public float conformExp = 1.2f;

    // ============================================================
    // Debug envelope viz (CPU dashed)
    // ============================================================
    [Header("Debug Envelope Renderer")]
    public bool showDebugEnvelope = true;
    public bool drawMidAndMaxEnvelopes = true;
    [Range(0f, 1f)] public float debugMidLevel = 0.5f;

    public Color debugCurrentColor = new Color(0.2f, 0.5f, 1f, 0.85f);
    public Color debugMidColor = new Color(0.2f, 1.0f, 0.25f, 0.75f);
    public Color debugMaxColor = new Color(1.0f, 0.9f, 0.2f, 0.75f);

    [Min(64)] public int debugEnvelopeSamples = 256;
    [Min(0.001f)] public float debugEnvelopeWidth = 0.03f;
    [Min(0.01f)] public float debugDashLength = 0.9f;
    [Min(0.01f)] public float debugGapLength = 0.55f;

    [Header("Debug Logging")]
    public bool debugLogSegmentCount = false;
    public int debugLogEveryNFrames = 60;

    // ============================================================
    // GPU buffers
    // ============================================================
    [StructLayout(LayoutKind.Sequential)]
    private struct Segment { public Vector3 a; public Vector3 b; public float mag; }

    private ComputeBuffer _segBuf;
    private ComputeBuffer _segCountBuf;
    private ComputeBuffer _argsBuf;

    // NEW: dummy seeds buffer required because compute declares _Seeds
private ComputeBuffer _seedsBuf;

// NEW: compute property IDs
static readonly int _SeedsID = Shader.PropertyToID("_Seeds");
static readonly int _UseProceduralSeedsID = Shader.PropertyToID("_UseProceduralSeeds");

// NEW: toggle (default true)
[Header("Seeding Mode")]
public bool useProceduralSeeds = true;
    private readonly uint[] _segCountReadback = new uint[1];

    private int _kIntegrate;
    private bool _inited;
    private int _frame;

    // ============================================================
    // Storm runtime
    // ============================================================
    private float _t;
    private float _lastStormTime;
    private float _currentStormTime;
    private bool _stormActive;
    private float _stormEnvelope;
    private float _stormFrontCenterS;

    private Vector3 _baseWindDir;
    private Vector3 _currentStormDir;
    private Vector3 _effectiveWindDir;

    // ============================================================
    // Debug envelope renderer state
    // ============================================================
    private class DashSet
    {
        public GameObject root;
        public readonly List<LineRenderer> dashes = new List<LineRenderer>(128);
        public Color color;
    }

    private GameObject _debugRoot;
    private Material _debugMat;
    private DashSet _dashCurrent, _dashMid, _dashMax;

    // ============================================================
    // IDs (compute)
    // ============================================================
    static readonly int _SegmentsID = Shader.PropertyToID("_Segments");
    static readonly int _SeedCountID = Shader.PropertyToID("_SeedCount");
    static readonly int _MaxStepsID  = Shader.PropertyToID("_MaxSteps");
    static readonly int _StepID      = Shader.PropertyToID("_Step");
    static readonly int _MinMagID    = Shader.PropertyToID("_MinMag");

    static readonly int _SeedInnerFillID   = Shader.PropertyToID("_SeedInnerFill");
    static readonly int _SeedOuterFillID   = Shader.PropertyToID("_SeedOuterFill");
    static readonly int _SeedRadialPowerID = Shader.PropertyToID("_SeedRadialPower");
    static readonly int _SeedJitterID      = Shader.PropertyToID("_SeedJitter");
    static readonly int _RandomSeedID      = Shader.PropertyToID("_RandomSeed");

    static readonly int _TimeNowID = Shader.PropertyToID("_TimeNow");
    static readonly int _SpinStrengthID = Shader.PropertyToID("_SpinStrength");
    static readonly int _SpinSpeedID = Shader.PropertyToID("_SpinSpeed");
    static readonly int _WarbleStrengthID = Shader.PropertyToID("_WarbleStrength");
    static readonly int _WarbleSpeedID = Shader.PropertyToID("_WarbleSpeed");

    // Optional additions (compute can ignore if not present)
    static readonly int _PathWarbleStrengthID = Shader.PropertyToID("_PathWarbleStrength");
    static readonly int _PathWarbleSpeedID = Shader.PropertyToID("_PathWarbleSpeed");
    static readonly int _PathWarbleFrequencyID = Shader.PropertyToID("_PathWarbleFrequency");

    static readonly int _Pseudo3DEnabledID = Shader.PropertyToID("_Pseudo3DEnabled");
    static readonly int _Pseudo3DAmplitudeID = Shader.PropertyToID("_Pseudo3DAmplitude");
    static readonly int _Pseudo3DSpeedID = Shader.PropertyToID("_Pseudo3DSpeed");
    static readonly int _Pseudo3DTwistID = Shader.PropertyToID("_Pseudo3DTwist");

    static readonly int _WarpAmountID      = Shader.PropertyToID("_WarpAmount");
    static readonly int _WarpAlongAmountID = Shader.PropertyToID("_WarpAlongAmount");
    static readonly int _WarpPerpAmountID  = Shader.PropertyToID("_WarpPerpAmount");
    static readonly int _WarpStartQID      = Shader.PropertyToID("_WarpStartQ");
    static readonly int _WarpRampExpID     = Shader.PropertyToID("_WarpRampExp");

    static readonly int _BoundaryEaseStartID = Shader.PropertyToID("_BoundaryEaseStart");
    static readonly int _BoundaryEasePowerID = Shader.PropertyToID("_BoundaryEasePower");
    // Global conformity (pre-drape)
    static readonly int _ConformAmountID = Shader.PropertyToID("_ConformAmount");
    static readonly int _ConformStartQID = Shader.PropertyToID("_ConformStartQ");
    static readonly int _ConformExpID = Shader.PropertyToID("_ConformExp");

    static readonly int _CenterID = Shader.PropertyToID("_Center");
    static readonly int _PlanetRadiusID = Shader.PropertyToID("_PlanetRadius");
    static readonly int _MagnetosphereRadiusID = Shader.PropertyToID("_MagnetosphereRadius");
    static readonly int _DipoleAxisID = Shader.PropertyToID("_DipoleAxis");
    static readonly int _DipoleMomentID = Shader.PropertyToID("_DipoleMoment");

    static readonly int _WindDirID = Shader.PropertyToID("_WindDir");
    static readonly int _SolarWindStrengthID = Shader.PropertyToID("_SolarWindStrength");
    static readonly int _GlobalWindStrengthID = Shader.PropertyToID("_GlobalWindStrength");

    static readonly int _DaysideCompressionID = Shader.PropertyToID("_DaysideCompression");
    static readonly int _NightsideStretchID = Shader.PropertyToID("_NightsideStretch");
    static readonly int _TailFieldStrengthID = Shader.PropertyToID("_TailFieldStrength");

    static readonly int _PressureGainID = Shader.PropertyToID("_PressureGain");
    static readonly int _UseSquaredPressureID = Shader.PropertyToID("_UseSquaredPressure");

    static readonly int _DaysideResponseExpID = Shader.PropertyToID("_DaysideResponseExp");
    static readonly int _TailResponseExpID = Shader.PropertyToID("_TailResponseExp");
    static readonly int _TailPerpResponseExpID = Shader.PropertyToID("_TailPerpResponseExp");

    static readonly int _MagnetopauseBlendWidthID = Shader.PropertyToID("_MagnetopauseBlendWidth");
    static readonly int _DayPerpCompressionRatioID = Shader.PropertyToID("_DayPerpCompressionRatio");
    static readonly int _TailPerpFlareRatioID = Shader.PropertyToID("_TailPerpFlareRatio");

    static readonly int _MinDaysideAlongScaleID = Shader.PropertyToID("_MinDaysideAlongScale");
    static readonly int _MinDaysidePerpScaleID = Shader.PropertyToID("_MinDaysidePerpScale");
    static readonly int _StormDaysideCompressionBoostID = Shader.PropertyToID("_StormDaysideCompressionBoost");

    static readonly int _FlankPinchStrengthID = Shader.PropertyToID("_FlankPinchStrength");
    static readonly int _MinFlankPerpScaleID = Shader.PropertyToID("_MinFlankPerpScale");
    static readonly int _FlankPinchWidthID = Shader.PropertyToID("_FlankPinchWidth");
    static readonly int _FlankPinchResponseExpID = Shader.PropertyToID("_FlankPinchResponseExp");

    static readonly int _StormIntensityID = Shader.PropertyToID("_StormIntensity");
    static readonly int _StormWarpScaleID = Shader.PropertyToID("_StormWarpScale");
    static readonly int _StormEnvelopeID = Shader.PropertyToID("_StormEnvelope");

    static readonly int _StormFrontCenterSID = Shader.PropertyToID("_StormFrontCenterS");
    static readonly int _StormFrontThicknessID = Shader.PropertyToID("_StormFrontThickness");
    static readonly int _StormWakeLengthID = Shader.PropertyToID("_StormWakeLength");
    static readonly int _StormFrontFeatherID = Shader.PropertyToID("_StormFrontFeather");
    static readonly int _StormFrontLocalWarpAmountID = Shader.PropertyToID("_StormFrontLocalWarpAmount");
    static readonly int _StormGlobalWarpShareID = Shader.PropertyToID("_StormGlobalWarpShare");

    // ============================================================
    // IDs (material) — legacy + enhanced (shader must support enhanced)
    // ============================================================
    static readonly int _AlphaID = Shader.PropertyToID("_Alpha");
    static readonly int _LineColorNearID = Shader.PropertyToID("_LineColorNear");
    static readonly int _LineColorFarID = Shader.PropertyToID("_LineColorFar");
    static readonly int _ColorMagScaleID = Shader.PropertyToID("_ColorMagScale");
    static readonly int _LocalToWorldID = Shader.PropertyToID("_LocalToWorld");
    static readonly int _UseEnhancedColorMatID = Shader.PropertyToID("_UseEnhancedColor");

    static readonly int _CenterWS_ID = Shader.PropertyToID("_CenterWS");
    static readonly int _MagRadiusWS_ID = Shader.PropertyToID("_MagRadiusWS");
    static readonly int _DipoleAxisWS_ID = Shader.PropertyToID("_DipoleAxisWS");
    static readonly int _PlanetRadiusWS_ID = Shader.PropertyToID("_PlanetRadiusWS");

    static readonly int _ArcInnerColor_ID = Shader.PropertyToID("_ArcInnerColor");
    static readonly int _ArcOuterColor_ID = Shader.PropertyToID("_ArcOuterColor");
    static readonly int _PoleOverlayColor_ID = Shader.PropertyToID("_PoleOverlayColor");
    static readonly int _PoleOverlayRadius_ID = Shader.PropertyToID("_PoleOverlayRadius");
    static readonly int _PoleOverlayStrength_ID = Shader.PropertyToID("_PoleOverlayStrength");
    static readonly int _PoleOverlayPower_ID = Shader.PropertyToID("_PoleOverlayPower");
    static readonly int _RadialColorRadiusScale_ID = Shader.PropertyToID("_RadialColorRadiusScale");

    static readonly int _Pseudo3DEnabledMat_ID = Shader.PropertyToID("_Pseudo3DEnabled");
    static readonly int _BackBrightness_ID = Shader.PropertyToID("_BackBrightness");
    static readonly int _BackAlpha_ID = Shader.PropertyToID("_BackAlpha");

    // ============================================================
    void OnEnable() => Init();
    void OnDisable() => Release();
    void OnDestroy() => Release();

    void OnValidate()
    {
        seedCount = Mathf.Max(16, seedCount);
        maxSteps = Mathf.Max(16, maxSteps);
        step = Mathf.Max(0.001f, step);

        magnetosphereRadius = Mathf.Max(0.1f, magnetosphereRadius);
        planetRadius = Mathf.Max(0.0001f, planetRadius);
        dipoleMoment = Mathf.Max(0.0001f, dipoleMoment);

        if (dipoleAxis.sqrMagnitude < 1e-6f) dipoleAxis = new Vector3(0, 0, 1);
        if (solarWindDirection.sqrMagnitude < 1e-6f) solarWindDirection = Vector3.right;
    }

    void Init()
    {
        if (_inited) return;
        if (!fieldCS || !lineMaterial) return;

        _kIntegrate = fieldCS.FindKernel("IntegrateCS");
        Allocate();
        if (_mpb == null) _mpb = new MaterialPropertyBlock();

        _baseWindDir = NormalizeXZ(solarWindDirection);
        _currentStormDir = _baseWindDir;
        _effectiveWindDir = _baseWindDir;

        _lastStormTime = -Mathf.Max(0.01f, stormInterval);
        _stormFrontCenterS = -999999f;

        EnsureDebugObjects();
        _inited = true;
    }

void Allocate()
{
    ReleaseBuffersOnly();

    // NEW: allocate dummy seeds (seedCount elements)
    _seedsBuf = new ComputeBuffer(Mathf.Max(1, seedCount), Marshal.SizeOf<Vector3>());
    // fill with something harmless (center)
    var tmp = new Vector3[Mathf.Max(1, seedCount)];
    for (int i = 0; i < tmp.Length; i++) tmp[i] = dipolePosition;
    _seedsBuf.SetData(tmp);

    int cap = Mathf.Max(1, seedCount * Mathf.Max(1, maxSteps) * 2);
    _segBuf = new ComputeBuffer(cap, 48, ComputeBufferType.Append);
    _segBuf.SetCounterValue(0);

    _segCountBuf = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.IndirectArguments);
    _argsBuf = new ComputeBuffer(4, sizeof(uint), ComputeBufferType.IndirectArguments);
    _argsBuf.SetData(new uint[4] { 0, 1, 0, 0 });

    // NEW: bind both buffers
    fieldCS.SetBuffer(_kIntegrate, _SeedsID, _seedsBuf);
    fieldCS.SetBuffer(_kIntegrate, _SegmentsID, _segBuf);
}

    void Release()
    {
        ReleaseBuffersOnly();

        if (_debugRoot) Destroy(_debugRoot);
        if (_debugMat) Destroy(_debugMat);

        _debugRoot = null;
        _debugMat = null;
        _dashCurrent = _dashMid = _dashMax = null;

        _inited = false;
    }

    void ReleaseBuffersOnly()
    {
    _seedsBuf?.Release(); _seedsBuf = null;   // NEW
        _segBuf?.Release(); _segBuf = null;
        _segCountBuf?.Release(); _segCountBuf = null;
        _argsBuf?.Release(); _argsBuf = null;
    }

    void Update()
    {
        if (!_inited) Init();
        if (!_inited || !fieldCS || !lineMaterial) return;

        _t += Time.deltaTime;
        UpdateWindAndStorm();

        float stormEnv = Mathf.Clamp01(_stormEnvelope);
        float globalWindStrength = solarWindStrength + (stormIntensity * stormWarpScale * stormEnv);

        // Assist for “always return to poles”
        int stepsNow = maxSteps;
        float minMagNow = minFieldMag;
        if (forceReturnToPoles)
        {
            stepsNow = Mathf.Max(stepsNow, forceMinSteps);
            minMagNow = Mathf.Min(minMagNow, forceMinFieldMag);
        }

        // If stepsNow > maxSteps we should have a big enough buffer cap.
        // Simple safe behavior: if stepsNow is larger, reallocate.
        if (stepsNow > maxSteps)
        {
            int original = maxSteps;
            maxSteps = stepsNow;
            Allocate();
            maxSteps = original; // keep inspector stable
        }

        _segBuf.SetCounterValue(0);

        Vector3 axis = NormalizeXZ(dipoleAxis);
        Vector3 wind = NormalizeXZ(_effectiveWindDir);

        // ---------- compute params ----------
        fieldCS.SetInt(_SeedCountID, seedCount);
        fieldCS.SetInt(_MaxStepsID, stepsNow);
        fieldCS.SetFloat(_StepID, step);
        fieldCS.SetFloat(_MinMagID, Mathf.Max(0f, minMagNow));

        fieldCS.SetFloat(_SeedInnerFillID, seedInnerFill);
        fieldCS.SetFloat(_SeedOuterFillID, seedOuterFill);
        fieldCS.SetFloat(_SeedRadialPowerID, seedRadialPower);
        fieldCS.SetFloat(_SeedJitterID, seedJitter);
        fieldCS.SetInt(_RandomSeedID, unchecked((int)randomSeed));

        fieldCS.SetFloat(_TimeNowID, Time.time);
        fieldCS.SetFloat(_SpinStrengthID, spinStrength);
        fieldCS.SetFloat(_SpinSpeedID, spinSpeed);
        fieldCS.SetFloat(_WarbleStrengthID, warbleStrength);
        fieldCS.SetFloat(_WarbleSpeedID, warbleSpeed);

        // Optional path warble + pseudo3D
        fieldCS.SetFloat(_PathWarbleStrengthID, pathWarbleStrength);
        fieldCS.SetFloat(_PathWarbleSpeedID, pathWarbleSpeed);
        fieldCS.SetFloat(_PathWarbleFrequencyID, pathWarbleFrequency);

        fieldCS.SetFloat(_Pseudo3DEnabledID, pseudo3DEnabled ? 1f : 0f);
        fieldCS.SetFloat(_Pseudo3DAmplitudeID, pseudo3DAmplitude);
        fieldCS.SetFloat(_Pseudo3DSpeedID, pseudo3DSpeed);
        fieldCS.SetFloat(_Pseudo3DTwistID, pseudo3DTwist);

        fieldCS.SetFloat(_WarpAmountID, warpAmount);
        fieldCS.SetFloat(_WarpAlongAmountID, warpAlongAmount);
        fieldCS.SetFloat(_WarpPerpAmountID, warpPerpAmount);
        fieldCS.SetFloat(_WarpStartQID, warpStartQ);
        fieldCS.SetFloat(_WarpRampExpID, warpRampExp);

        fieldCS.SetFloat(_BoundaryEaseStartID, boundaryEaseStart);
        fieldCS.SetFloat(_BoundaryEasePowerID, boundaryEasePower);

        // Global conformity (pre-drape)
        fieldCS.SetFloat(_ConformAmountID, conformAmount);
        fieldCS.SetFloat(_ConformStartQID, conformStartQ);
        fieldCS.SetFloat(_ConformExpID, conformExp);

        fieldCS.SetVector(_CenterID, dipolePosition);
        fieldCS.SetFloat(_PlanetRadiusID, planetRadius);
        fieldCS.SetFloat(_MagnetosphereRadiusID, magnetosphereRadius);
        fieldCS.SetVector(_DipoleAxisID, axis);
        fieldCS.SetFloat(_DipoleMomentID, dipoleMoment);

        fieldCS.SetVector(_WindDirID, wind);
        fieldCS.SetFloat(_SolarWindStrengthID, solarWindStrength);
        fieldCS.SetFloat(_GlobalWindStrengthID, globalWindStrength);

        fieldCS.SetFloat(_DaysideCompressionID, daysideCompression);
        fieldCS.SetFloat(_NightsideStretchID, nightsideStretch);
        fieldCS.SetFloat(_TailFieldStrengthID, tailFieldStrength);

        fieldCS.SetFloat(_PressureGainID, pressureGain);
        fieldCS.SetFloat(_UseSquaredPressureID, useSquaredPressure ? 1f : 0f);

        fieldCS.SetFloat(_DaysideResponseExpID, daysideResponseExp);
        fieldCS.SetFloat(_TailResponseExpID, tailResponseExp);
        fieldCS.SetFloat(_TailPerpResponseExpID, tailPerpResponseExp);

        fieldCS.SetFloat(_MagnetopauseBlendWidthID, magnetopauseBlendWidth);
        fieldCS.SetFloat(_DayPerpCompressionRatioID, dayPerpCompressionRatio);
        fieldCS.SetFloat(_TailPerpFlareRatioID, tailPerpFlareRatio);

        fieldCS.SetFloat(_MinDaysideAlongScaleID, minDaysideAlongScale);
        fieldCS.SetFloat(_MinDaysidePerpScaleID, minDaysidePerpScale);
        fieldCS.SetFloat(_StormDaysideCompressionBoostID, stormDaysideCompressionBoost);

        fieldCS.SetFloat(_FlankPinchStrengthID, flankPinchStrength);
        fieldCS.SetFloat(_MinFlankPerpScaleID, minFlankPerpScale);
        fieldCS.SetFloat(_FlankPinchWidthID, flankPinchWidth);
        fieldCS.SetFloat(_FlankPinchResponseExpID, flankPinchResponseExp);

        fieldCS.SetFloat(_StormIntensityID, stormIntensity);
        fieldCS.SetFloat(_StormWarpScaleID, stormWarpScale);
        fieldCS.SetFloat(_StormEnvelopeID, stormEnv);

        fieldCS.SetFloat(_StormFrontCenterSID, _stormFrontCenterS);
        fieldCS.SetFloat(_StormFrontThicknessID, stormFrontThickness);
        fieldCS.SetFloat(_StormWakeLengthID, stormWakeLength);
        fieldCS.SetFloat(_StormFrontFeatherID, stormFrontFeather);
        fieldCS.SetFloat(_StormFrontLocalWarpAmountID, stormFrontLocalWarpAmount);
        fieldCS.SetFloat(_StormGlobalWarpShareID, stormGlobalWarpShare);
        fieldCS.SetFloat(_UseProceduralSeedsID, useProceduralSeeds ? 1f : 0f);

        // Metal/Unity requires _Seeds to be bound even when procedural seeding is enabled.
        int seedsN = Mathf.Max(1, seedCount);
        if (_seedsBuf == null || !_seedsBuf.IsValid() || _seedsBuf.count != seedsN)
        {
            _seedsBuf?.Release();
            _seedsBuf = new ComputeBuffer(seedsN, Marshal.SizeOf<Vector3>());

            // Fill with harmless values (center). Compute won't use these if useProceduralSeeds == true.
            var tmpSeeds = new Vector3[seedsN];
            for (int i = 0; i < seedsN; i++) tmpSeeds[i] = dipolePosition;
            _seedsBuf.SetData(tmpSeeds);
        }

        // Re-bind buffers every frame (robust against domain reload / reimport / driver quirks)
        fieldCS.SetBuffer(_kIntegrate, _SeedsID, _seedsBuf);
        fieldCS.SetBuffer(_kIntegrate, _SegmentsID, _segBuf);

        // Dispatch
        int groups = Mathf.Max(1, Mathf.CeilToInt(seedCount / 64.0f));
        fieldCS.Dispatch(_kIntegrate, groups, 1, 1);

        // Indirect args via CPU (reliable)
        ComputeBuffer.CopyCount(_segBuf, _segCountBuf, 0);
        _segCountBuf.GetData(_segCountReadback);
        uint segCount = _segCountReadback[0];
        uint vtxCount = segCount * 2u;
        _argsBuf.SetData(new uint[4] { vtxCount, 1, 0, 0 });

        if (debugLogSegmentCount && (++_frame % Mathf.Max(1, debugLogEveryNFrames) == 0))
            Debug.Log($"[{nameof(MagnetosphereFieldLinesGPU2D)}] Segments: {segCount} (vtx {vtxCount})");

        // ---------- material params ----------
        lineMaterial.SetBuffer(_SegmentsID, _segBuf);

        // Bind the StructuredBuffer via MPB at draw time (SRP/Metal-safe)
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        _mpb.Clear();
        _mpb.SetBuffer(_SegmentsID, _segBuf);

        // Legacy params (your current shader uses these)
        lineMaterial.SetFloat(_AlphaID, alpha);
        lineMaterial.SetColor(_LineColorNearID, lineColorNear);
        lineMaterial.SetColor(_LineColorFarID, lineColorFar);
        lineMaterial.SetFloat(_ColorMagScaleID, Mathf.Max(0.0001f, colorByMagScale));
        lineMaterial.SetMatrix(_LocalToWorldID, Matrix4x4.identity);

        // Enhanced shader mode flags (always set so toggles work reliably)
        lineMaterial.SetFloat(_UseEnhancedColorMatID, useEnhancedColor ? 1f : 0f);
        lineMaterial.SetFloat(_Pseudo3DEnabledMat_ID, pseudo3DEnabled ? 1f : 0f);
        lineMaterial.SetFloat(_BackBrightness_ID, pseudo3DDimBackSide ? backSideBrightness : 1f);
        lineMaterial.SetFloat(_BackAlpha_ID, pseudo3DDimBackSide ? backSideAlpha : 1f);

        // Enhanced params (only meaningful when useEnhancedColor is true)
        if (useEnhancedColor)
        {
            lineMaterial.SetVector(_CenterWS_ID, dipolePosition);
            lineMaterial.SetFloat(_MagRadiusWS_ID, Mathf.Max(0.0001f, magnetosphereRadius));
            lineMaterial.SetVector(_DipoleAxisWS_ID, axis);
            lineMaterial.SetFloat(_PlanetRadiusWS_ID, Mathf.Max(0.0001f, planetRadius));

            lineMaterial.SetColor(_ArcInnerColor_ID, arcInnerColor);
            lineMaterial.SetColor(_ArcOuterColor_ID, arcOuterColor);
            lineMaterial.SetColor(_PoleOverlayColor_ID, poleOverlayColor);
            lineMaterial.SetFloat(_PoleOverlayRadius_ID, Mathf.Max(0.0001f, poleOverlayRadius));
            lineMaterial.SetFloat(_PoleOverlayStrength_ID, poleOverlayStrength);
            lineMaterial.SetFloat(_PoleOverlayPower_ID, poleOverlayPower);
            lineMaterial.SetFloat(_RadialColorRadiusScale_ID, Mathf.Max(0.0001f, radialColorRadiusScale));
        }

        // Draw
        if (vtxCount > 0)
        {
            var bounds = new Bounds(dipolePosition, Vector3.one * 99999f);
            Graphics.DrawProceduralIndirect(
                lineMaterial,
                bounds,
                MeshTopology.Lines,
                _argsBuf,
                0,
                null,
                _mpb,
                ShadowCastingMode.Off,
                false,
                gameObject.layer
            );
        }

        // Debug envelopes
        if (showDebugEnvelope)
        {
            EnsureDebugObjects();
            UpdateDebugEnvelopes(globalWindStrength);
        }
        else if (_debugRoot) _debugRoot.SetActive(false);
    }

    // ============================================================
    // Storm timing
    // ============================================================
    void UpdateWindAndStorm()
    {
        if (!driftWindWhenCalm && !_stormActive)
            _baseWindDir = NormalizeXZ(solarWindDirection);

        if (driftWindWhenCalm && !_stormActive)
        {
            float n = Mathf.PerlinNoise(10.1f, _t * calmWindDriftSpeed);
            float a = (n - 0.5f) * 2f * calmWindDriftAmount * Mathf.PI;
            Vector3 drift = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            _baseWindDir = Vector3.Slerp(_baseWindDir, NormalizeXZ(drift), 1f - Mathf.Exp(-1.5f * Time.deltaTime));
        }

        bool stormsEnabled = stormInterval > 0.01f && stormIntensity > 0.001f && stormWarpScale > 0.0001f;
        if (stormsEnabled && !_stormActive && (_t - _lastStormTime > stormInterval))
            StartNewStorm();

        UpdateStormEnvelopeAndFront();

        _effectiveWindDir = _stormActive ? _currentStormDir : _baseWindDir;
        _effectiveWindDir = NormalizeXZ(_effectiveWindDir);
    }

    void StartNewStorm()
    {
        _currentStormDir = randomizeWindOnStormStart ? RandomWindXZ() : _baseWindDir;
        _stormActive = true;
        _currentStormTime = 0f;
        _lastStormTime = _t;
        _stormFrontCenterS = -magnetosphereRadius * 1.25f;
    }

    void UpdateStormEnvelopeAndFront()
    {
        _stormEnvelope = 0f;
        if (!_stormActive) return;

        _currentStormTime += Time.deltaTime;

        float total = Mathf.Max(0.0001f, stormEaseInTime + stormDuration + stormEaseOutTime);
        float t = _currentStormTime;

        if (stormEaseInTime > 0.0001f && t < stormEaseInTime)
        {
            float u = t / stormEaseInTime;
            _stormEnvelope = Smooth01(u);
        }
        else if (t < stormEaseInTime + stormDuration)
        {
            _stormEnvelope = 1f;
        }
        else if (stormEaseOutTime > 0.0001f && t < stormEaseInTime + stormDuration + stormEaseOutTime)
        {
            float u = (t - stormEaseInTime - stormDuration) / stormEaseOutTime;
            _stormEnvelope = 1f - Smooth01(u);
        }
        else
        {
            _stormEnvelope = 0f;
        }

        float t01 = Mathf.Clamp01(_currentStormTime / total);
        float startS = -magnetosphereRadius * 1.25f;
        float endS = magnetosphereRadius * 2.25f;
        _stormFrontCenterS = Mathf.Lerp(startS, endS, t01);

        if (_currentStormTime >= total)
        {
            _stormActive = false;
            _stormFrontCenterS = -999999f;
        }
    }

    // ============================================================
    // Debug envelope rendering (dashed)
    // ============================================================
    void EnsureDebugObjects()
    {
        if (!showDebugEnvelope) return;

        if (_debugRoot == null)
        {
            _debugRoot = new GameObject("GPU_DebugMagnetopauseEnvelopes");
            _debugRoot.transform.SetParent(transform, false);
        }
        _debugRoot.SetActive(true);

        if (_debugMat == null)
            _debugMat = new Material(Shader.Find("Sprites/Default"));

        if (_dashCurrent == null) _dashCurrent = CreateDashSet("Current", debugCurrentColor);
        if (_dashMid == null) _dashMid = CreateDashSet("Mid", debugMidColor);
        if (_dashMax == null) _dashMax = CreateDashSet("Max", debugMaxColor);
    }

    DashSet CreateDashSet(string name, Color c)
    {
        var set = new DashSet();
        set.root = new GameObject($"Envelope_{name}");
        set.root.transform.SetParent(_debugRoot.transform, false);
        set.color = c;
        return set;
    }

    void UpdateDebugEnvelopes(float currentStrength)
    {
        if (_debugRoot == null) return;

        DrawDashedEnvelope(_dashCurrent, currentStrength);

        if (drawMidAndMaxEnvelopes)
        {
            float midStrength = solarWindStrength + (stormIntensity * stormWarpScale * Mathf.Clamp01(debugMidLevel));
            float maxStrength = solarWindStrength + (stormIntensity * stormWarpScale);

            DrawDashedEnvelope(_dashMid, midStrength);
            DrawDashedEnvelope(_dashMax, maxStrength);

            _dashMid.root.SetActive(true);
            _dashMax.root.SetActive(true);
        }
        else
        {
            _dashMid.root.SetActive(false);
            _dashMax.root.SetActive(false);
        }
    }

    void DrawDashedEnvelope(DashSet set, float windStrength)
    {
        int N = Mathf.Clamp(debugEnvelopeSamples, 32, 2048);
        var pts = new Vector3[N + 1];
        float y = dipolePosition.y;

        for (int i = 0; i <= N; i++)
        {
            float a = (i / (float)N) * Mathf.PI * 2f;
            var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)).normalized;

            float r = SolveEnvelopeRadiusAlongDirection(d, windStrength);
            var p = dipolePosition + d * r;
            p.y = y;
            pts[i] = p;
        }

        var segments = BuildDashedSegments(pts, Mathf.Max(0.01f, debugDashLength), Mathf.Max(0.01f, debugGapLength));
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
            lr.SetPositions(seg.ToArray());

            lr.widthMultiplier = debugEnvelopeWidth;
            lr.material = _debugMat;
            lr.useWorldSpace = true;
            lr.alignment = LineAlignment.View;

            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(set.color, 0f), new GradientColorKey(set.color, 1f) },
                new[] { new GradientAlphaKey(set.color.a, 0f), new GradientAlphaKey(set.color.a, 1f) }
            );
            lr.colorGradient = g;
        }
    }

    void EnsureDashRendererCount(DashSet set, int needed)
    {
        while (set.dashes.Count < needed)
        {
            var go = new GameObject($"Dash_{set.dashes.Count:000}");
            go.transform.SetParent(set.root.transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.numCornerVertices = 0;
            lr.numCapVertices = 0;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;

            set.dashes.Add(lr);
        }
    }

    List<List<Vector3>> BuildDashedSegments(Vector3[] polylineClosed, float dashLen, float gapLen)
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
    // CPU envelope (matches compute’s math)
    // ============================================================
    float SolveEnvelopeRadiusAlongDirection(Vector3 dirXZ, float windStrength)
    {
        Vector3 w = NormalizeXZ(_effectiveWindDir);
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

        for (int it = 0; it < 24; it++)
        {
            float mid = 0.5f * (low + high);
            float qm = Q(mid);
            if (qm < 1f) low = mid;
            else high = mid;
        }

        return high;
    }

    void GetLocalEnvelope(float windStrength, float along, out float a, out float b, out float alongScale, out float perpScale)
    {
        float R = magnetosphereRadius;
        float bw = (magnetopauseBlendWidth > 0.001f) ? magnetopauseBlendWidth : (R * 0.35f);
        float tTail = Smoothstep01(-bw, bw, along);

        float w = Mathf.Max(0f, windStrength);
        float x = useSquaredPressure ? (w * w) : w;

        float pressure01 = 1f - Mathf.Exp(-x * Mathf.Max(0.0001f, pressureGain));
        pressure01 = Mathf.Clamp01(pressure01);
        pressure01 = Mathf.Max(1e-6f, pressure01);

        float pDay = Mathf.Pow(pressure01, Mathf.Max(0.01f, daysideResponseExp));
        float pTail = Mathf.Pow(pressure01, Mathf.Max(0.01f, tailResponseExp));
        float pPerp = Mathf.Pow(pressure01, Mathf.Max(0.01f, tailPerpResponseExp));

        float comp = daysideCompression * pDay;
        float stretch = nightsideStretch * pTail;
        float flare = tailFieldStrength * pPerp;

        float stormMaxDrive = Mathf.Max(0.0001f, stormIntensity * stormWarpScale);
        float stormContribution = Mathf.Max(0f, windStrength - solarWindStrength);
        float stormLevel01 = Mathf.Clamp01(stormContribution / stormMaxDrive);
        comp *= (1f + stormDaysideCompressionBoost * stormLevel01);

        float dayAlong = 1f / (1f + comp);
        float dayPerp = 1f / (1f + comp * dayPerpCompressionRatio);

        float tailAlong = 1f + stretch;
        float tailPerp = 1f + flare * tailPerpFlareRatio;

        dayAlong = Mathf.Clamp(dayAlong, minDaysideAlongScale, 1f);
        dayPerp = Mathf.Clamp(dayPerp, minDaysidePerpScale, 1.25f);
        tailAlong = Mathf.Clamp(tailAlong, 1f, 18f);
        tailPerp = Mathf.Clamp(tailPerp, 1f, 6f);

        alongScale = Mathf.Lerp(dayAlong, tailAlong, tTail);
        perpScale = Mathf.Lerp(dayPerp, tailPerp, tTail);

        float fw = (flankPinchWidth > 0.001f) ? flankPinchWidth : bw;
        fw = Mathf.Max(0.001f, fw);

        float flankW = Mathf.Exp(-(along * along) / (fw * fw));
        float pFlank = Mathf.Pow(pressure01, flankPinchResponseExp);
        float pinch01 = Mathf.Clamp01(flankPinchStrength * pFlank);

        float targetPerp = Mathf.Lerp(perpScale, Mathf.Max(0.001f, perpScale * minFlankPerpScale), pinch01);
        perpScale = Mathf.Lerp(perpScale, targetPerp, flankW);

        a = R * alongScale;
        b = R * perpScale;
    }

    static float Smoothstep01(float edge0, float edge1, float x)
    {
        if (edge1 <= edge0) return (x < edge0) ? 0f : 1f;
        float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }

    static Vector3 NormalizeXZ(Vector3 v)
    {
        v.y = 0f;
        if (v.sqrMagnitude < 1e-8f) return Vector3.right;
        return v.normalized;
    }

    static Vector3 RandomWindXZ()
    {
        float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)).normalized;
    }

    static float Smooth01(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }
}
