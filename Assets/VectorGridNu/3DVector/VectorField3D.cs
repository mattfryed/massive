using System;
using System.Runtime.InteropServices;
using UnityEngine;

[ExecuteAlways]
public class VectorField3D : MonoBehaviour
{
    // -------------------- Seeds --------------------
    [Header("Seeds (streamlines)")]
    [Min(1)] public int seedCount = 256;
    [Min(8)] public int maxSteps = 128;
    [Min(0.001f)] public float step = 0.12f;
    [Min(0f)] public float seedShellRadius = 4.0f;
    public uint randomSeed = 12345;

    // -------------------- Dipole field --------------------
    [Header("Field (Dipole)")]
    public Vector3 center = Vector3.zero;             // LOCAL
    public Vector3 moment = new Vector3(0, 1, 0);     // MODEL axis
    [Min(0f)] public float softCoreRadius = 0.3f;
    public float dipoleK = 1.0f;
    [Range(0, 1)] public float dipoleWeight = 1.0f;

    // -------------------- Internal spin --------------------
    [Header("Spin")]
    public bool spin = true;

    [Tooltip("If true, spinAxis is forced to match the (normalized) moment axis for a stable dipole silhouette.")]
    public bool lockSpinAxisToMoment = true;

    public Vector3 spinAxis = new Vector3(0, 1, 0);
    public float spinDegPerSec = 2f;

    // -------------------- Bounds --------------------
    [Header("Bounds (kill integration outside)")]
    public Vector3 boundsMin = new Vector3(-12, -12, -12);
    public Vector3 boundsMax = new Vector3( 12,  12,  12);

    // -------------------- Rendering --------------------
    [Header("Rendering")]
    public Material lineMaterial;
    [Range(0f, 1f)] public float alpha = 1.0f;
    public Color lineColorNear = new Color(1f, 0.75f, 0.7f);
    public Color lineColorFar  = new Color(1f, 0.0f, 0.6f);
    [Min(0.0001f)] public float colorByMagScale = 1.0f;

    // -------------------- Curl --------------------
    [Header("Curl (Procedural)")]
    [Min(0.0001f)] public float curlScale = 0.03f;
    public Vector3 curlScroll = new Vector3(0.06f, 0.07f, 0.03f);
    [Range(0f, 2f)] public float curlWeight = 0.0f;
    [Range(0.001f, 0.1f)] public float curlEpsilon = 0.02f;
    public Vector3 curlSeed = new Vector3(0.02f, 0.09f, 0.02f);

    // -------------------- Wind axis --------------------
    public enum WindAxisMode { LocalFixed = 0, ModelFollowsSpin = 1 }

    [Header("Wind (drives magnetopause deformation)")]
    public WindAxisMode windAxisMode = WindAxisMode.LocalFixed;

    [Tooltip("Wind direction vector. Interpretation depends on Wind Axis Mode.")]
    public Vector3 windDir = new Vector3(1, 0, 0);

    [Range(0f, 3f)] public float windWeight = 0.0f;
    public AnimationCurve windOverTime = AnimationCurve.Linear(0, 1, 10, 1);

    [Header("Wind Plane Readability (top-down)")]
    [Tooltip("If true, wind is projected onto the plane defined by 'windPlaneNormalWorld' each frame.")]
    public bool projectWindToPlane = true;

    [Tooltip("World-space plane normal. For an overhead camera with gameplay on XZ, use (0,1,0).")]
    public Vector3 windPlaneNormalWorld = Vector3.up;

    // -------------------- Magnetopause --------------------
    [Header("Magnetopause (pressure boundary)")]
    public bool magnetopauseEnabled = true;

    [Tooltip("If true, uses Mp Radius for BOTH day & tail base radii (calm symmetric by default).")]
    public bool mpUseSymmetricBase = true;

    [Min(0.01f)] public float mpRadius = 7.5f;
    [Min(0.01f)] public float mpDayRadius  = 7.5f;
    [Min(0.01f)] public float mpTailRadius = 7.5f;

    [Min(0.01f)] public float mpLateralRadius = 7.5f;
    [Min(0.0001f)] public float mpBlend = 0.75f;

    [Header("Magnetopause Wind Response")]
    [Min(0.001f)] public float mpWindNorm = 1.0f;
    [Range(0f, 1f)]  public float mpWindCompress = 0.65f;
    [Range(0f, 2f)]  public float mpWindTailGain = 0.90f;
    [Range(0f, 0.5f)] public float mpWindLateralSqueeze = 0.10f;

    // -------------------- Termination --------------------
    [Header("Pole capture (model space)")]
    [Min(0.0001f)] public float poleCenter = 0.31f;
    [Min(0.0001f)] public float captureRadius = 0.45f;

    [Header("Integration Controls")]
    [Range(0f, 1f)] public float stepNear = 0.35f;
    [Min(0f)] public float minFieldMag = 0.06f;

    // -------------------- Compute --------------------
    [Header("Compute")]
    public ComputeShader fieldCS;

    int _kIntegrate = -1;
    bool _inited;

    [StructLayout(LayoutKind.Sequential)]
    struct Seed { public Vector3 p; }

    // float3 pads to 16 bytes on GPU. Two float3 => 32, plus float => 36, padded => 48 stride.
    [StructLayout(LayoutKind.Sequential)]
    struct Segment { public Vector3 a; public Vector3 b; public float mag; }

    ComputeBuffer _seedsBuf, _segBuf, _segCountBuf, _argsBuf;
    uint _lastSeedHash;
    int _lastSeedCount, _lastMaxSteps;

    // -------------------- Shader IDs --------------------
    static readonly int _SeedsID = Shader.PropertyToID("_Seeds");
    static readonly int _SegmentsID = Shader.PropertyToID("_Segments");

    static readonly int _SeedCountID = Shader.PropertyToID("_SeedCount");
    static readonly int _MaxStepsID  = Shader.PropertyToID("_MaxSteps");
    static readonly int _StepID      = Shader.PropertyToID("_Step");

    static readonly int _MinMagID    = Shader.PropertyToID("_MinMag");
    static readonly int _StepNearID  = Shader.PropertyToID("_StepNear");
    static readonly int _BoundsMinID = Shader.PropertyToID("_BoundsMin");
    static readonly int _BoundsMaxID = Shader.PropertyToID("_BoundsMax");

    static readonly int _CenterID    = Shader.PropertyToID("_Center");
    static readonly int _RID         = Shader.PropertyToID("_R");
    static readonly int _RinvID      = Shader.PropertyToID("_Rinv");

    static readonly int _Moment0ID   = Shader.PropertyToID("_Moment0");
    static readonly int _DipoleKID   = Shader.PropertyToID("_DipoleK");
    static readonly int _DipoleWID   = Shader.PropertyToID("_DipoleWeight");
    static readonly int _SoftCore2ID = Shader.PropertyToID("_SoftCore2");

    static readonly int _PoleCenterID     = Shader.PropertyToID("_PoleCenter");
    static readonly int _CaptureRadiusID  = Shader.PropertyToID("_CaptureRadius");

    static readonly int _CurlScaleID  = Shader.PropertyToID("_CurlScale");
    static readonly int _CurlOffsetID = Shader.PropertyToID("_CurlOffset");
    static readonly int _CurlWeightID = Shader.PropertyToID("_CurlWeight");
    static readonly int _CurlEpsID    = Shader.PropertyToID("_CurlEpsilon");
    static readonly int _CurlSeedID   = Shader.PropertyToID("_CurlSeed");

    static readonly int _WindDirID       = Shader.PropertyToID("_WindDir");
    static readonly int _WindWeightID    = Shader.PropertyToID("_WindWeight");
    static readonly int _WindAxisModeID  = Shader.PropertyToID("_WindAxisMode");

    static readonly int _MPEnableID      = Shader.PropertyToID("_MPEnable");
    static readonly int _MPDayRadiusID   = Shader.PropertyToID("_MPDayRadius");
    static readonly int _MPTailRadiusID  = Shader.PropertyToID("_MPTailRadius");
    static readonly int _MPLateralID     = Shader.PropertyToID("_MPLateralRadius");
    static readonly int _MPBlendID       = Shader.PropertyToID("_MPBlend");

    static readonly int _MPWindNormID     = Shader.PropertyToID("_MPWindNorm");
    static readonly int _MPWindCompressID = Shader.PropertyToID("_MPWindCompress");
    static readonly int _MPWindTailGainID = Shader.PropertyToID("_MPWindTailGain");
    static readonly int _MPWindLatSqID    = Shader.PropertyToID("_MPWindLateralSqueeze");

    // Material IDs
    static readonly int _AlphaID          = Shader.PropertyToID("_Alpha");
    static readonly int _LineColorNearID  = Shader.PropertyToID("_LineColorNear");
    static readonly int _LineColorFarID   = Shader.PropertyToID("_LineColorFar");
    static readonly int _ColorMagScaleID  = Shader.PropertyToID("_ColorMagScale");
    static readonly int _LocalToWorldID   = Shader.PropertyToID("_LocalToWorld");

    void OnEnable() => Init();
    void OnDisable() => Release();

    // -------------------- Public helpers --------------------
    /// <summary>Set wind direction from a world-space direction.</summary>
    public void SetWindDirWorld(Vector3 worldDir)
    {
        if (worldDir.sqrMagnitude < 1e-6f) worldDir = Vector3.right;
        worldDir.Normalize();
        // Store in local so it stays consistent with object orientation.
        windDir = transform.InverseTransformDirection(worldDir);
    }

    /// <summary>Set wind direction from world-space direction, projected to the specified plane normal.</summary>
    public void SetWindDirWorldPlanar(Vector3 worldDir, Vector3 planeNormalWorld)
    {
        if (worldDir.sqrMagnitude < 1e-6f) worldDir = Vector3.right;
        Vector3 n = (planeNormalWorld.sqrMagnitude > 1e-6f) ? planeNormalWorld.normalized : Vector3.up;
        Vector3 proj = Vector3.ProjectOnPlane(worldDir, n);
        if (proj.sqrMagnitude < 1e-6f) proj = Vector3.Cross(n, Vector3.forward);
        SetWindDirWorld(proj.normalized);
    }

    // -------------------- Init / Release --------------------
    void Init()
    {
        if (_inited) return;
        if (!fieldCS)
        {
            Debug.LogError("VectorField3D: Assign VF3D.compute to 'fieldCS'.");
            return;
        }

        _kIntegrate = fieldCS.FindKernel("IntegrateCS");
        Allocate();
        BuildSeeds();
        _inited = true;
    }

    void Allocate()
    {
        Release();

        _seedsBuf = new ComputeBuffer(Mathf.Max(1, seedCount), Marshal.SizeOf<Seed>());

        // Forward + backward tracing, up to one segment per step.
        int cap = Mathf.Max(1, seedCount * Mathf.Max(1, maxSteps - 1) * 2);
        _segBuf = new ComputeBuffer(cap, 48, ComputeBufferType.Append);
        _segBuf.SetCounterValue(0);

        _segCountBuf = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.IndirectArguments);
        _argsBuf = new ComputeBuffer(4, sizeof(uint), ComputeBufferType.IndirectArguments);
        _argsBuf.SetData(new uint[4] { 0, 1, 0, 0 });

        fieldCS.SetBuffer(_kIntegrate, _SeedsID, _seedsBuf);
        fieldCS.SetBuffer(_kIntegrate, _SegmentsID, _segBuf);
    }

    void Release()
    {
        _seedsBuf?.Release(); _seedsBuf = null;
        _segBuf?.Release(); _segBuf = null;
        _segCountBuf?.Release(); _segCountBuf = null;
        _argsBuf?.Release(); _argsBuf = null;
        _inited = false;
    }

    // -------------------- Seeds --------------------
    void BuildSeeds()
    {
        if (_seedsBuf == null) return;

        var arr = new Seed[seedCount];

        System.Random rng = new System.Random((int)randomSeed);
        float rot = (float)rng.NextDouble() * Mathf.PI * 2f;
        const float phi = 1.61803398875f;

        for (int i = 0; i < seedCount; i++)
        {
            float t = (i + 0.5f) / seedCount;
            float theta = 2f * Mathf.PI * i / phi + rot;
            float z = 1f - 2f * t;
            float r = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
            Vector3 dir = new Vector3(r * Mathf.Cos(theta), z, r * Mathf.Sin(theta));
            arr[i].p = dir * seedShellRadius;
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
            uint h = (uint)seedCount;
            h = h * 16777619u ^ (uint)(seedShellRadius * 1000f);
            h = h * 16777619u ^ randomSeed;
            return h;
        }
    }

    // -------------------- Main update --------------------
    void Update()
    {
        if (!_inited) Init();
        if (!_inited) return;

        // Live-resize
        if (_seedsBuf == null || seedCount != _lastSeedCount || maxSteps != _lastMaxSteps)
        {
            Allocate();
            BuildSeeds();
        }
        else
        {
            uint s = HashSeeds();
            if (s != _lastSeedHash) BuildSeeds();
        }

        if (_seedsBuf == null || _segBuf == null) return;


        float tNow = Application.isPlaying ? Time.time : 0f;

        // Determine stable moment axis (MODEL space)
        Vector3 m0 = (moment.sqrMagnitude > 1e-6f) ? moment.normalized : Vector3.up;

        // Determine spin axis
        Vector3 axis = spinAxis;
        if (lockSpinAxisToMoment) axis = m0;
        if (axis.sqrMagnitude < 1e-6f) axis = Vector3.up;
        axis.Normalize();

        // Internal spin rotation (MODEL->LOCAL)
        Quaternion q = Quaternion.identity;
        if (spin && Mathf.Abs(spinDegPerSec) > 0.0001f)
            q = Quaternion.AngleAxis(tNow * spinDegPerSec, axis);

        Matrix4x4 R = Matrix4x4.Rotate(q);
        Matrix4x4 Rinv = R.transpose;

        // Upload matrices
        fieldCS.SetFloats(_RID,
            R.m00, R.m01, R.m02, R.m03,
            R.m10, R.m11, R.m12, R.m13,
            R.m20, R.m21, R.m22, R.m23,
            R.m30, R.m31, R.m32, R.m33);

        fieldCS.SetFloats(_RinvID,
            Rinv.m00, Rinv.m01, Rinv.m02, Rinv.m03,
            Rinv.m10, Rinv.m11, Rinv.m12, Rinv.m13,
            Rinv.m20, Rinv.m21, Rinv.m22, Rinv.m23,
            Rinv.m30, Rinv.m31, Rinv.m32, Rinv.m33);

        // Reset append counter
        _segBuf.SetCounterValue(0);

        // Core field
        fieldCS.SetInt(_SeedCountID, seedCount);
        fieldCS.SetInt(_MaxStepsID, maxSteps);
        fieldCS.SetFloat(_StepID, step);

        fieldCS.SetFloats(_CenterID, center.x, center.y, center.z);
        fieldCS.SetFloats(_Moment0ID, m0.x, m0.y, m0.z);
        fieldCS.SetFloat(_DipoleKID, dipoleK);
        fieldCS.SetFloat(_DipoleWID, dipoleWeight);
        fieldCS.SetFloat(_SoftCore2ID, softCoreRadius * softCoreRadius);

        fieldCS.SetFloats(_BoundsMinID, boundsMin.x, boundsMin.y, boundsMin.z);
        fieldCS.SetFloats(_BoundsMaxID, boundsMax.x, boundsMax.y, boundsMax.z);

        fieldCS.SetFloat(_MinMagID, Mathf.Max(0f, minFieldMag));
        fieldCS.SetFloat(_StepNearID, Mathf.Clamp01(stepNear));

        fieldCS.SetFloat(_PoleCenterID, Mathf.Max(0.0001f, poleCenter));
        fieldCS.SetFloat(_CaptureRadiusID, Mathf.Max(0.0001f, captureRadius));

        // Curl (animated offset)
        Vector3 offs = tNow * curlScroll;
        fieldCS.SetFloat(_CurlScaleID, Mathf.Max(0.0001f, curlScale));
        fieldCS.SetFloats(_CurlOffsetID, offs.x, offs.y, offs.z);
        fieldCS.SetFloat(_CurlWeightID, curlWeight);
        fieldCS.SetFloat(_CurlEpsID, Mathf.Clamp(curlEpsilon, 0.001f, 0.1f));
        fieldCS.SetFloats(_CurlSeedID, curlSeed.x, curlSeed.y, curlSeed.z);

        // Wind envelope
        float windScale = 1f;
        if (windOverTime != null && windOverTime.length > 0)
        {
            float endT = windOverTime.keys[windOverTime.length - 1].time;
            float evalT = (Application.isPlaying && endT > 0.0001f) ? (tNow % endT) : tNow;
            windScale = windOverTime.Evaluate(evalT);
        }

        float w = windWeight * windScale;

        // Wind direction prep
        Vector3 wdirLocalOrModel = windDir;
        if (wdirLocalOrModel.sqrMagnitude < 1e-6f) wdirLocalOrModel = Vector3.right;

        // If projecting to the arena plane, do it in WORLD, then bring back to LOCAL for readability.
        if (projectWindToPlane)
        {
            Vector3 n = (windPlaneNormalWorld.sqrMagnitude > 1e-6f) ? windPlaneNormalWorld.normalized : Vector3.up;

            // Interpret windDir as LOCAL if mode is LocalFixed; otherwise as MODEL (still relative to this script’s space).
            Vector3 worldDir = transform.TransformDirection(wdirLocalOrModel.normalized);
            Vector3 proj = Vector3.ProjectOnPlane(worldDir, n);
            if (proj.sqrMagnitude < 1e-6f) proj = Vector3.Cross(n, Vector3.forward);
            Vector3 projLocal = transform.InverseTransformDirection(proj.normalized);

            wdirLocalOrModel = projLocal;
        }
        else
        {
            wdirLocalOrModel.Normalize();
        }

        fieldCS.SetFloats(_WindDirID, wdirLocalOrModel.x, wdirLocalOrModel.y, wdirLocalOrModel.z);
        fieldCS.SetFloat(_WindWeightID, w);
        fieldCS.SetInt(_WindAxisModeID, (int)windAxisMode);

        // Magnetopause base radii
        float baseDay  = mpUseSymmetricBase ? mpRadius : mpDayRadius;
        float baseTail = mpUseSymmetricBase ? mpRadius : mpTailRadius;

        fieldCS.SetFloat(_MPEnableID, magnetopauseEnabled ? 1f : 0f);
        fieldCS.SetFloat(_MPDayRadiusID, Mathf.Max(0.01f, baseDay));
        fieldCS.SetFloat(_MPTailRadiusID, Mathf.Max(0.01f, baseTail));
        fieldCS.SetFloat(_MPLateralID, Mathf.Max(0.01f, mpLateralRadius));
        fieldCS.SetFloat(_MPBlendID, Mathf.Max(0.0001f, mpBlend));

        fieldCS.SetFloat(_MPWindNormID, Mathf.Max(0.001f, mpWindNorm));
        fieldCS.SetFloat(_MPWindCompressID, mpWindCompress);
        fieldCS.SetFloat(_MPWindTailGainID, mpWindTailGain);
        fieldCS.SetFloat(_MPWindLatSqID, mpWindLateralSqueeze);

        // Dispatch
        int groups = Mathf.CeilToInt(seedCount / 64.0f);
        // Unity can drop bindings after .compute recompiles (common in ExecuteAlways while iterating).
        fieldCS.SetBuffer(_kIntegrate, _SeedsID, _seedsBuf);
        fieldCS.SetBuffer(_kIntegrate, _SegmentsID, _segBuf);


        fieldCS.Dispatch(_kIntegrate, Mathf.Max(1, groups), 1, 1);

        // Build indirect args from append count
        ComputeBuffer.CopyCount(_segBuf, _segCountBuf, 0);
        uint[] segCount = { 0 };
        _segCountBuf.GetData(segCount);

        uint vtxCount = segCount[0] * 2u;
        _argsBuf.SetData(new uint[4] { vtxCount, 1, 0, 0 });

        // Draw
        if (lineMaterial && vtxCount > 0)
        {
            lineMaterial.SetBuffer(_SegmentsID, _segBuf);
            lineMaterial.SetFloat(_AlphaID, alpha);
            lineMaterial.SetColor(_LineColorNearID, lineColorNear);
            lineMaterial.SetColor(_LineColorFarID, lineColorFar);
            lineMaterial.SetFloat(_ColorMagScaleID, Mathf.Max(0.0001f, colorByMagScale));

            // Force unit scale
            var l2w = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            lineMaterial.SetMatrix(_LocalToWorldID, l2w);

            Graphics.DrawProceduralIndirect(
                lineMaterial,
                new Bounds(transform.position, Vector3.one * 9999f),
                MeshTopology.Lines,
                _argsBuf,
                0,
                null,
                null,
                UnityEngine.Rendering.ShadowCastingMode.Off,
                false,
                gameObject.layer
            );
        }
    }

    void OnValidate()
    {
        seedCount = Mathf.Max(1, seedCount);
        maxSteps = Mathf.Max(8, maxSteps);
        step = Mathf.Max(0.001f, step);
        seedShellRadius = Mathf.Max(0f, seedShellRadius);

        mpRadius = Mathf.Max(0.01f, mpRadius);
        mpDayRadius = Mathf.Max(0.01f, mpDayRadius);
        mpTailRadius = Mathf.Max(0.01f, mpTailRadius);
        mpLateralRadius = Mathf.Max(0.01f, mpLateralRadius);
        mpBlend = Mathf.Max(0.0001f, mpBlend);

        mpWindNorm = Mathf.Max(0.001f, mpWindNorm);
        minFieldMag = Mathf.Max(0f, minFieldMag);
        softCoreRadius = Mathf.Max(0f, softCoreRadius);
        poleCenter = Mathf.Max(0.0001f, poleCenter);
        captureRadius = Mathf.Max(0.0001f, captureRadius);
    }
}
