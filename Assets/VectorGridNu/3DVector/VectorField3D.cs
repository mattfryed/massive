// Assets/VectorGridNu/VectorField3D.cs
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

[ExecuteAlways]
public class VectorField3D : MonoBehaviour
{
    [Header("Seeds (streamlines)")]
    [Min(1)] public int seedCount = 256;
    [Min(8)] public int maxSteps = 64;
    [Min(0.001f)] public float step = 0.12f;
    public float seedShellRadius = 2.0f;        // sphere radius to place seeds
    public uint randomSeed = 12345;

    [Header("Field (Dipole)")]
    public Vector3 center = Vector3.zero;       // local space center
    public Vector3 moment = new Vector3(0, 1, 0);  // dipole moment (will be rotated)
    public float softCoreRadius = 0.3f;

    public float dipoleK = 1.0f;
    [Range(0, 1)] public float dipoleWeight = 1.0f;

    [Header("Spin")]
    public bool spin = true;
    public Vector3 spinAxis = new Vector3(0, 1, 0);
    public float spinDegPerSec = 30f;

    [Header("Bounds (kill integration outside)")]
    public Vector3 boundsMin = new Vector3(-6, -6, -6);
    public Vector3 boundsMax = new Vector3( 6,  6,  6);

    [Header("Rendering")]
    public Material lineMaterial;   // use FieldLines.shader below
    [Range(0.0f, 1.0f)] public float alpha = 1.0f;
    public Color lineColorNear = new Color(0.6f, 0.9f, 1f);
    public Color lineColorFar  = new Color(0.1f, 0.4f, 1f);
    public float colorByMagScale = 1.0f;   // color gradient driven by |B|*scale

    [Header("Curl (Procedural)")]
    public float curlScale = 0.25f;               // bigger = finer detail
    public Vector3 curlScroll = new(0.12f, 0.07f, 0.03f);
    [Range(0,2)] public float curlWeight = 0.6f;
    [Range(0.001f, 0.1f)] public float curlEpsilon = 0.02f;
    public Vector3 curlSeed = new(10.7f, 23.1f, 47.3f); // decorrelates axes    

    [Header("Solar Wind (reconnection)")]
    public Vector3 windDirModel = new(1,0,0);
    [Range(0,3)] public float windWeight = 0.0f;
    public AnimationCurve windOverTime = AnimationCurve.Linear(0,0,10,1);

    [Header("Pole capture (model space)")]
    public float poleCenter   = 0.35f;   // distance of capture sphere center along ±moment
    public float captureRadius = 0.45f;  // sphere radius



    [Header("Integration Controls")]
    [Range(0.0f,1.0f)] public float stepNear = 0.35f;   // smaller = tighter near core
    [Min(0f)] public float minFieldMag = 0.03f;        // stop threshold


    // [Header("Noise (Curl)")]
    // public Texture3D noiseTex;
    // public float noiseScale = 0.3f;
    // [Range(0,2)] public float curlWeight = 0.5f;
    // public Vector3 windDirModel = new Vector3(1,0,0);
    // [Range(0,3)] public float windWeight = 0f;
    // public AnimationCurve windOverTime = AnimationCurve.EaseInOut(0,0, 5,1);

    [StructLayout(LayoutKind.Sequential)]
    struct Seed { public Vector3 p; } // local-space

    [StructLayout(LayoutKind.Sequential)]
    struct Segment { public Vector3 a; public Vector3 b; public float mag; }

    // Buffers
    ComputeBuffer seedsBuf, segBuf, segCountBuf, argsBuf;

    // Shader props
    static readonly int _Seeds = Shader.PropertyToID("_Seeds");
    static readonly int _Segments = Shader.PropertyToID("_Segments");
    static readonly int _Step = Shader.PropertyToID("_Step");
    static readonly int _MaxSteps = Shader.PropertyToID("_MaxSteps");
    static readonly int _SeedCount = Shader.PropertyToID("_SeedCount");
    static readonly int _Center = Shader.PropertyToID("_Center");
    static readonly int _Moment = Shader.PropertyToID("_Moment");
    static readonly int _DipoleK = Shader.PropertyToID("_DipoleK");
    static readonly int _DipoleW = Shader.PropertyToID("_DipoleWeight");
    static readonly int _BoundsMin = Shader.PropertyToID("_BoundsMin");
    static readonly int _BoundsMax = Shader.PropertyToID("_BoundsMax");
    static readonly int _Alpha = Shader.PropertyToID("_Alpha");
    static readonly int _LineColorNear = Shader.PropertyToID("_LineColorNear");
    static readonly int _LineColorFar  = Shader.PropertyToID("_LineColorFar");
    static readonly int _ColorMagScale = Shader.PropertyToID("_ColorMagScale");
    static readonly int _LocalToWorld = Shader.PropertyToID("_LocalToWorld");
    static readonly int _R      = Shader.PropertyToID("_R");     // 3x3 packed as float3x3 (3 float4s)
    static readonly int _Rinv   = Shader.PropertyToID("_Rinv");
    static readonly int _Moment0= Shader.PropertyToID("_Moment0"); // fixed model-space moment
    static readonly int _SoftCore2 = Shader.PropertyToID("_SoftCore2");
    static readonly int _CurlScaleID = Shader.PropertyToID("_CurlScale");
    static readonly int _CurlOffsetID = Shader.PropertyToID("_CurlOffset");
    static readonly int _CurlWeightID = Shader.PropertyToID("_CurlWeight");
    static readonly int _CurlEpsID    = Shader.PropertyToID("_CurlEpsilon");
    static readonly int _CurlSeedID   = Shader.PropertyToID("_CurlSeed");
    static readonly int _MinMagID = Shader.PropertyToID("_MinMag");
    static readonly int _StepNearID = Shader.PropertyToID("_StepNear");
    static readonly int _WindDirMID    = Shader.PropertyToID("_WindDirM");
    static readonly int _WindWeightID  = Shader.PropertyToID("_WindWeight");
    static readonly int _CaptureRadiusID = Shader.PropertyToID("_CaptureRadius");
    static readonly int _PoleCenterID = Shader.PropertyToID("_PoleCenter");



    

    [Header("Compute")]
    public ComputeShader fieldCS;      // VF3D.compute
    int kIntegrate = -1;

    // Working
    bool inited;
    uint lastSeedHash;
    Matrix4x4 lastL2W;
    int lastSeedCount, lastMaxSteps;

    void OnEnable() { Init(); }
    void OnDisable() { Release(); }

    void Init()
    {
        if (inited) return;
        if (!fieldCS) { Debug.LogError("Assign VF3D.compute to VectorField3D."); return; }

        kIntegrate = fieldCS.FindKernel("IntegrateCS");
        Allocate();
        BuildSeeds();
        inited = true;
    }

    void Allocate()
    {
        Release();

        // seeds
        seedsBuf = new ComputeBuffer(Mathf.Max(1, seedCount), Marshal.SizeOf<Seed>());

        // segments are (seedCount * (maxSteps-1)) in the worst case (one segment per step)
        int cap = Mathf.Max(1, seedCount * Mathf.Max(1, maxSteps - 1));
        // 48-byte stride to match HLSL packing
        segBuf = new ComputeBuffer(cap, 48, ComputeBufferType.Append);
        segBuf.SetCounterValue(0);

        // count + args (for DrawProceduralIndirect)
        segCountBuf = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.IndirectArguments);
        argsBuf = new ComputeBuffer(4, sizeof(uint), ComputeBufferType.IndirectArguments);
        // args: vertexCountPerInstance, instanceCount, startVertex, startInstance
        var initArgs = new uint[4] { 0, 1, 0, 0 };
        argsBuf.SetData(initArgs);

        // Bind static params
        fieldCS.SetInt(_SeedCount, seedCount);
        fieldCS.SetInt(_MaxSteps, maxSteps);
        fieldCS.SetFloat(_Step, step);
        fieldCS.SetBuffer(kIntegrate, _Seeds, seedsBuf);
        fieldCS.SetBuffer(kIntegrate, _Segments, segBuf);
    }

    void Release()
    {
        seedsBuf?.Release(); seedsBuf = null;
        segBuf?.Release(); segBuf = null;
        segCountBuf?.Release(); segCountBuf = null;
        argsBuf?.Release(); argsBuf = null;
        inited = false;
    }

    void BuildSeeds()
    {
        if (seedsBuf == null) return;
        var arr = new Seed[seedCount];

        // Golden-spiral distribution on a sphere
        // Deterministic but seedable (rotate phi by random).
        System.Random rng = new System.Random((int)randomSeed);
        float rot = (float)rng.NextDouble() * Mathf.PI * 2f;
        const float phi = 1.61803398875f; // golden ratio
        for (int i = 0; i < seedCount; i++)
        {
            float t = (i + 0.5f) / seedCount;             // (0,1)
            float theta = 2f * Mathf.PI * i / phi + rot;  // azimuth
            float z = 1f - 2f * t;
            float r = Mathf.Sqrt(1f - z * z);
            Vector3 dir = new Vector3(r * Mathf.Cos(theta), z, r * Mathf.Sin(theta));
            arr[i].p = dir * seedShellRadius;
        }

        seedsBuf.SetData(arr);
        lastSeedHash = HashSeeds();
        lastSeedCount = seedCount;
        lastMaxSteps = maxSteps;
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

    void Update()
    {
        if (!inited) Init();
        if (!inited) return;

        // live-resizable
        if (seedsBuf == null || seedCount != lastSeedCount || maxSteps != lastMaxSteps)
        {
            Allocate();
            BuildSeeds();
        }
        else
        {
            uint s = HashSeeds();
            if (s != lastSeedHash) BuildSeeds();
        }

        // Build rotation
        var q = (spin && spinDegPerSec != 0f)
            ? Quaternion.AngleAxis(Time.time * spinDegPerSec, (spinAxis.sqrMagnitude > 1e-6f ? spinAxis.normalized : Vector3.up))
            : Quaternion.identity;
        var R = Matrix4x4.Rotate(q);
        var Rinv = R.transpose;

        // Upload full 4x4 (16 floats each)
        fieldCS.SetFloats(_R,
            R.m00, R.m01, R.m02, R.m03,
            R.m10, R.m11, R.m12, R.m13,
            R.m20, R.m21, R.m22, R.m23,
            R.m30, R.m31, R.m32, R.m33);

        fieldCS.SetFloats(_Rinv,
            Rinv.m00, Rinv.m01, Rinv.m02, Rinv.m03,
            Rinv.m10, Rinv.m11, Rinv.m12, Rinv.m13,
            Rinv.m20, Rinv.m21, Rinv.m22, Rinv.m23,
            Rinv.m30, Rinv.m31, Rinv.m32, Rinv.m33);

        // Stable model-space moment
        var m0 = moment.normalized;
        fieldCS.SetFloats(_Moment0, m0.x, m0.y, m0.z);


        // Reset segment append counter
        segBuf.SetCounterValue(0);

        // Set uniforms (compute in LOCAL space)
        fieldCS.SetFloats(_Center, center.x, center.y, center.z);
        fieldCS.SetFloat(_DipoleK, dipoleK);
        fieldCS.SetFloat(_DipoleW, dipoleWeight);
        fieldCS.SetFloats(_BoundsMin, boundsMin.x, boundsMin.y, boundsMin.z);
        fieldCS.SetFloats(_BoundsMax, boundsMax.x, boundsMax.y, boundsMax.z);
        fieldCS.SetFloat(_SoftCore2, softCoreRadius * softCoreRadius);

        // Animate curl offset over time
        float t = Application.isPlaying ? Time.time : 0f;
        Vector3 offs = t * curlScroll;

        // Curl
        fieldCS.SetFloat(_CurlScaleID, Mathf.Max(0.0001f, curlScale));
        fieldCS.SetFloats(_CurlOffsetID, offs.x, offs.y, offs.z);
        fieldCS.SetFloat(_CurlWeightID, curlWeight);
        fieldCS.SetFloat(_CurlEpsID, Mathf.Clamp(curlEpsilon, 0.001f, 0.1f));
        fieldCS.SetFloats(_CurlSeedID, curlSeed.x, curlSeed.y, curlSeed.z);
        fieldCS.SetFloat(_MinMagID, Mathf.Max(0f, minFieldMag));
        fieldCS.SetFloat(_StepNearID, Mathf.Clamp01(stepNear));

        // Wind (animate if you like)
        float wind = windWeight * (Application.isPlaying ? windOverTime.Evaluate(t % windOverTime.keys[^1].time) : 1f);
        Vector3 wdir = windDirModel.sqrMagnitude > 1e-6f ? windDirModel.normalized : Vector3.right;
        fieldCS.SetFloats(_WindDirMID,  wdir.x, wdir.y, wdir.z);
        fieldCS.SetFloat (_WindWeightID, wind);

        fieldCS.SetFloat(_CaptureRadiusID, Mathf.Max(0.001f, captureRadius));
        fieldCS.SetFloat(_PoleCenterID,   Mathf.Max(0.0001f, poleCenter));

        if (kIntegrate < 0)
        {
            // Re-find after a domain reload / import
            kIntegrate = fieldCS.FindKernel("IntegrateCS");
            if (kIntegrate < 0)
            {
                Debug.LogError("VF3D: kernel 'IntegrateCS' not found. Is VF3D.compute assigned?");
                return;
            }

            // Rebind buffers after re-finding
            fieldCS.SetBuffer(kIntegrate, _Seeds, seedsBuf);
            fieldCS.SetBuffer(kIntegrate, _Segments, segBuf);
        }

        fieldCS.SetBuffer(kIntegrate, _Seeds, seedsBuf);
        fieldCS.SetBuffer(kIntegrate, _Segments, segBuf);



        // Dispatch
        int groups = Mathf.CeilToInt(seedCount / 64.0f);
        fieldCS.Dispatch(kIntegrate, Mathf.Max(1, groups), 1, 1);

        // Copy segment count -> CPU for indirect args (simple first pass)
        ComputeBuffer.CopyCount(segBuf, segCountBuf, 0);
        uint[] segCount = { 0 };
        segCountBuf.GetData(segCount);
        uint vtxCount = segCount[0] * 2u; // each segment = 2 vertices

        // Write args
        uint[] args = { vtxCount, 1, 0, 0 };
        argsBuf.SetData(args);

        // Draw
        if (lineMaterial && vtxCount > 0)
        {
            lineMaterial.SetBuffer(_Segments, segBuf);
            lineMaterial.SetFloat(_Alpha, alpha);
            lineMaterial.SetColor(_LineColorNear, lineColorNear);
            lineMaterial.SetColor(_LineColorFar,  lineColorFar);
            lineMaterial.SetFloat(_ColorMagScale, Mathf.Max(0.0001f, colorByMagScale));
            lineMaterial.SetMatrix(_LocalToWorld, transform.localToWorldMatrix);

            Graphics.DrawProceduralIndirect(lineMaterial, new Bounds(transform.position, Vector3.one * 9999f),
                MeshTopology.Lines, argsBuf, 0, null, null,
                UnityEngine.Rendering.ShadowCastingMode.Off, false, gameObject.layer);
        }

        lastL2W = transform.localToWorldMatrix;
    }
}
