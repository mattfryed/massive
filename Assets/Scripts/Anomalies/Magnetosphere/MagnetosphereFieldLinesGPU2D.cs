using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// MagnetosphereFieldLinesGPU2D
/// GPU streamline integration (XZ plane) using an Append buffer + single DrawProceduralIndirect call.
/// - Integrate streamlines in UNWARPED dipole field (nice nested arcs)
/// - Post-warp endpoints into wind/storm-deformed envelope (visual deformation)
/// - Clip to envelope on GPU (binary-search intersection)
///
/// Poles: mode A (up/down on screen) => dipoleAxis defaults to +Z in XZ plane.
/// </summary>
[DisallowMultipleComponent]
public class MagnetosphereFieldLinesGPU2D : MonoBehaviour
{

    private bool _useSRP;
private int _frame;
[Header("Debug")]
public bool debugLogSegmentCount = false;
public int debugLogEveryNFrames = 60;

    [Header("Editor Visualization")]
    [Tooltip("If true, do not draw in the Scene View while in Play mode.")]
    public bool hideInSceneViewDuringPlay = true;

    [Header("Compute + Material")]
    public ComputeShader fieldCS;
    public Material lineMaterial;

    [Header("Rendering")]
    [Range(0f, 1f)] public float alpha = 1.0f;
    public Color lineColorNear = new Color(1f, 0.75f, 0.70f, 1f);
    public Color lineColorFar  = new Color(1f, 0.00f, 0.60f, 1f);
    [Min(0.0001f)] public float colorByMagScale = 1.0f;

    [Header("Seeds (XZ disk)")]
    [Min(16)] public int seedCount = 256;
    [Min(16)] public int maxSteps = 160;
    [Min(0.001f)] public float step = 0.08f;
    [Tooltip("Stop tracing when |B| falls below this.")]
    [Min(0f)] public float minFieldMag = 0.01f;

    [Range(0.01f, 0.95f)] public float seedInnerFill = 0.12f;
    [Range(0.05f, 1.0f)] public float seedOuterFill = 0.98f;
    [Range(0.25f, 4f)] public float seedRadialPower = 1.0f;
    public uint randomSeed = 12345;

    [Header("Dipole")]
    [Min(0.0001f)] public float dipoleMoment = 170f;
    [Min(0.0001f)] public float planetRadius = 0.03f;
    [Min(0.1f)] public float magnetosphereRadius = 13f;
    public Vector3 dipolePosition = Vector3.zero;

    [Tooltip("Dipole axis in world space. For A (up/down on screen) use (0,0,1).")]
    public Vector3 dipoleAxis = new Vector3(0f, 0f, 1f);

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

    // GPU segment layout (must match FieldLines.shader stride 48 bytes)
    [StructLayout(LayoutKind.Sequential)]
    private struct Segment { public Vector3 a; public Vector3 b; public float mag; }

    private ComputeBuffer _seedsBuf, _segBuf, _segCountBuf, _argsBuf;
    private int _kIntegrate, _kBuildArgs;

    private bool _inited;
    private uint _lastSeedHash;
    private int _lastSeedCount, _lastMaxSteps;

    // storm runtime
    private float _t;
    private float _lastStormTime;
    private float _currentStormTime;
    private bool _stormActive;
    private float _stormEnvelope;
    private float _stormFrontCenterS;

    private Vector3 _baseWindDir;
    private Vector3 _currentStormDir;
    private Vector3 _effectiveWindDir;

    // Shader IDs
    static readonly int _SeedsID = Shader.PropertyToID("_Seeds");
    static readonly int _SegmentsID = Shader.PropertyToID("_Segments");
    static readonly int _SegCountID = Shader.PropertyToID("_SegCount");
    static readonly int _ArgsID = Shader.PropertyToID("_Args");

    static readonly int _SeedCountID = Shader.PropertyToID("_SeedCount");
    static readonly int _MaxStepsID  = Shader.PropertyToID("_MaxSteps");
    static readonly int _StepID      = Shader.PropertyToID("_Step");
    static readonly int _MinMagID    = Shader.PropertyToID("_MinMag");

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

    // FieldLines.shader material IDs
    static readonly int _AlphaID = Shader.PropertyToID("_Alpha");
    static readonly int _LineColorNearID = Shader.PropertyToID("_LineColorNear");
    static readonly int _LineColorFarID = Shader.PropertyToID("_LineColorFar");
    static readonly int _ColorMagScaleID = Shader.PropertyToID("_ColorMagScale");
    static readonly int _LocalToWorldID = Shader.PropertyToID("_LocalToWorld");

void OnEnable()
{
    Init();

    _useSRP = (GraphicsSettings.currentRenderPipeline != null);
    if (_useSRP)
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRenderingSRP;
    else
        Camera.onPostRender += OnCameraPostRenderBuiltin;
}

void OnDisable()
{
    if (_useSRP)
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRenderingSRP;
    else
        Camera.onPostRender -= OnCameraPostRenderBuiltin;

    Release();
}

void OnDestroy()
{
    if (_useSRP)
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRenderingSRP;
    else
        Camera.onPostRender -= OnCameraPostRenderBuiltin;

    Release();
}


    void OnValidate()
    {
        seedCount = Mathf.Max(16, seedCount);
        maxSteps = Mathf.Max(16, maxSteps);
        step = Mathf.Max(0.001f, step);

        magnetosphereRadius = Mathf.Max(0.1f, magnetosphereRadius);
        planetRadius = Mathf.Max(0.0001f, planetRadius);
        dipoleMoment = Mathf.Max(0.0001f, dipoleMoment);

        if (dipoleAxis.sqrMagnitude < 1e-6f) dipoleAxis = new Vector3(0f, 0f, 1f);
        if (solarWindDirection.sqrMagnitude < 1e-6f) solarWindDirection = Vector3.right;
    }

    void Init()
    {
        if (_inited) return;
        if (!fieldCS || !lineMaterial) return;

        _kIntegrate = fieldCS.FindKernel("IntegrateCS");
        _kBuildArgs = fieldCS.FindKernel("BuildArgsCS");

        Allocate();
        RebuildSeeds();

        _baseWindDir = NormalizeXZ(solarWindDirection);
        _currentStormDir = _baseWindDir;
        _effectiveWindDir = _baseWindDir;

        _lastStormTime = -Mathf.Max(0.01f, stormInterval);
        _stormFrontCenterS = -999999f;

        _inited = true;
    }

    void Allocate()
    {
        _seedsBuf?.Release(); _seedsBuf = null;
        _segBuf?.Release(); _segBuf = null;
        _segCountBuf?.Release(); _segCountBuf = null;
        _argsBuf?.Release(); _argsBuf = null;

        _seedsBuf = new ComputeBuffer(Mathf.Max(1, seedCount), Marshal.SizeOf<Vector3>());

        int cap = Mathf.Max(1, seedCount * Mathf.Max(1, maxSteps - 1) * 2);
        _segBuf = new ComputeBuffer(cap, 48, ComputeBufferType.Append);
        _segBuf.SetCounterValue(0);

        _segCountBuf = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.IndirectArguments);
        _argsBuf = new ComputeBuffer(4, sizeof(uint), ComputeBufferType.IndirectArguments);
        _argsBuf.SetData(new uint[4] { 0, 1, 0, 0 });

        fieldCS.SetBuffer(_kIntegrate, _SeedsID, _seedsBuf);
        fieldCS.SetBuffer(_kIntegrate, _SegmentsID, _segBuf);
        fieldCS.SetBuffer(_kBuildArgs, _SegCountID, _segCountBuf);
        fieldCS.SetBuffer(_kBuildArgs, _ArgsID, _argsBuf);

        _lastSeedCount = seedCount;
        _lastMaxSteps = maxSteps;
        _lastSeedHash = 0;
    }

    void Release()
    {
        _seedsBuf?.Release(); _seedsBuf = null;
        _segBuf?.Release(); _segBuf = null;
        _segCountBuf?.Release(); _segCountBuf = null;
        _argsBuf?.Release(); _argsBuf = null;
        _inited = false;
    }

    

    void Update()
    {
        if (!_inited) Init();
        if (!_inited || !fieldCS || !lineMaterial) return;

        if (seedCount != _lastSeedCount || maxSteps != _lastMaxSteps)
        {
            Allocate();
            RebuildSeeds();
            _inited = true;
        }
        else
        {
            uint h = HashSeeds();
            if (h != _lastSeedHash) RebuildSeeds();
        }

        _t += Time.deltaTime;
        UpdateWindAndStorm();

        float globalWindStrength = solarWindStrength + (stormIntensity * stormWarpScale * Mathf.Clamp01(_stormEnvelope));

        _segBuf.SetCounterValue(0);

        Vector3 axis = NormalizeXZ(dipoleAxis);
        Vector3 wind = NormalizeXZ(_effectiveWindDir);

        fieldCS.SetInt(_SeedCountID, seedCount);
        fieldCS.SetInt(_MaxStepsID, maxSteps);
        fieldCS.SetFloat(_StepID, step);
        fieldCS.SetFloat(_MinMagID, Mathf.Max(0f, minFieldMag));

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
        fieldCS.SetFloat(_StormEnvelopeID, _stormEnvelope);

        fieldCS.SetFloat(_StormFrontCenterSID, _stormFrontCenterS);
        fieldCS.SetFloat(_StormFrontThicknessID, stormFrontThickness);
        fieldCS.SetFloat(_StormWakeLengthID, stormWakeLength);
        fieldCS.SetFloat(_StormFrontFeatherID, stormFrontFeather);
        fieldCS.SetFloat(_StormFrontLocalWarpAmountID, stormFrontLocalWarpAmount);
        fieldCS.SetFloat(_StormGlobalWarpShareID, stormGlobalWarpShare);

        int groups = Mathf.CeilToInt(seedCount / 64.0f);
        fieldCS.SetBuffer(_kIntegrate, _SeedsID, _seedsBuf);
        fieldCS.SetBuffer(_kIntegrate, _SegmentsID, _segBuf);
        fieldCS.Dispatch(_kIntegrate, Mathf.Max(1, groups), 1, 1);

        ComputeBuffer.CopyCount(_segBuf, _segCountBuf, 0);
        if (debugLogSegmentCount && (++_frame % Mathf.Max(1, debugLogEveryNFrames) == 0))
{
    uint[] c = new uint[1];
    _segCountBuf.GetData(c);
    Debug.Log($"[MagnetosphereFieldLinesGPU2D] Segments: {c[0]}  (vtx {c[0] * 2})");
}

        fieldCS.SetBuffer(_kBuildArgs, _SegCountID, _segCountBuf);
        fieldCS.SetBuffer(_kBuildArgs, _ArgsID, _argsBuf);
        fieldCS.Dispatch(_kBuildArgs, 1, 1, 1);

        lineMaterial.SetBuffer(_SegmentsID, _segBuf);
        lineMaterial.SetFloat(_AlphaID, alpha);
        lineMaterial.SetColor(_LineColorNearID, lineColorNear);
        lineMaterial.SetColor(_LineColorFarID, lineColorFar);
        lineMaterial.SetFloat(_ColorMagScaleID, Mathf.Max(0.0001f, colorByMagScale));
        lineMaterial.SetMatrix(_LocalToWorldID, Matrix4x4.identity);
    }

private bool ShouldSkipCamera(Camera cam)
{
    if (cam == null) return true;
    if (hideInSceneViewDuringPlay && Application.isPlaying && cam.cameraType == CameraType.SceneView)
        return true;
    return false;
}

private void OnBeginCameraRenderingSRP(ScriptableRenderContext context, Camera cam)
{
    if (!_inited || lineMaterial == null || _argsBuf == null) return;
    if (ShouldSkipCamera(cam)) return;

    var cmd = CommandBufferPool.Get("MagnetosphereFieldLinesGPU2D");
    cmd.DrawProceduralIndirect(Matrix4x4.identity, lineMaterial, 0, MeshTopology.Lines, _argsBuf, 0);
    context.ExecuteCommandBuffer(cmd);
    CommandBufferPool.Release(cmd);
}

private void OnCameraPostRenderBuiltin(Camera cam)
{
    if (!_inited || lineMaterial == null || _argsBuf == null) return;
    if (ShouldSkipCamera(cam)) return;

    Bounds b = new Bounds(dipolePosition, new Vector3(99999f, 99999f, 99999f));
    Graphics.DrawProceduralIndirect(
        lineMaterial,
        b,
        MeshTopology.Lines,
        _argsBuf,
        0,
        null,
        null,
        ShadowCastingMode.Off,
        false,
        gameObject.layer
    );
}


    void RebuildSeeds()
    {
        if (_seedsBuf == null) return;

        Vector3[] arr = new Vector3[seedCount];

        float inner = magnetosphereRadius * Mathf.Clamp(seedInnerFill, 0.01f, 0.95f);
        float outer = magnetosphereRadius * Mathf.Clamp(seedOuterFill, 0.05f, 1.0f);
        inner = Mathf.Min(inner, outer * 0.99f);

        const float golden = 2.39996322972865332f;
        float rot = (Hash01((int)randomSeed) * 2f - 1f) * Mathf.PI;

        for (int i = 0; i < seedCount; i++)
        {
            float u = (i + 0.5f) / seedCount;
            float rr = Mathf.Pow(u, Mathf.Max(0.25f, seedRadialPower));
            float r = Mathf.Lerp(inner, outer, rr);

            float a = i * golden + rot;
            float x = Mathf.Cos(a) * r;
            float z = Mathf.Sin(a) * r;
            arr[i] = new Vector3(dipolePosition.x + x, dipolePosition.y, dipolePosition.z + z);
        }

        _seedsBuf.SetData(arr);
        _lastSeedHash = HashSeeds();
        _lastSeedCount = seedCount;
        _lastMaxSteps = maxSteps;
    }

    uint HashSeeds()
    {
        unchecked
        {
            uint h = 2166136261u;
            h = (h ^ (uint)seedCount) * 16777619u;
            h = (h ^ (uint)maxSteps) * 16777619u;
            h = (h ^ randomSeed) * 16777619u;
            h = (h ^ (uint)Mathf.RoundToInt(seedInnerFill * 10000f)) * 16777619u;
            h = (h ^ (uint)Mathf.RoundToInt(seedOuterFill * 10000f)) * 16777619u;
            h = (h ^ (uint)Mathf.RoundToInt(seedRadialPower * 10000f)) * 16777619u;
            h = (h ^ (uint)Mathf.RoundToInt(magnetosphereRadius * 1000f)) * 16777619u;
            return h;
        }
    }

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

    static float Hash01(int i)
    {
        unchecked
        {
            uint x = (uint)i;
            x ^= x >> 16;
            x *= 0x7feb352du;
            x ^= x >> 15;
            x *= 0x846ca68bu;
            x ^= x >> 16;
            return (x & 0x00FFFFFF) / 16777216f;
        }
    }
}
