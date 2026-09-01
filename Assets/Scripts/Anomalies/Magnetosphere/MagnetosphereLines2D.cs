using System.Runtime.InteropServices;
using UnityEngine;

[ExecuteAlways]
public class MagnetosphereLines2D : MonoBehaviour
{
    [Header("Rendering")]
    public Material lineMaterial;              // your existing FieldLines material
    public ComputeShader fieldCS;              // MagnetosphereLines2D.compute
    [Range(0, 1)] public float alpha = 1f;
    public Color lineColorNear = new Color(1f, 0.75f, 0.7f);
    public Color lineColorFar  = new Color(1f, 0f, 0.6f);
    [Min(0.0001f)] public float colorByMagScale = 1f;

    [Header("Line Set")]
    [Min(8)] public int lineCount = 220;
    [Min(16)] public int samplesPerLine = 128;
    [Min(0.01f)] public float LMin = 1.25f;
    [Min(0.01f)] public float LMax = 10.0f;

    [Header("Dipole Axis (local XY plane)")]
    public Vector2 dipoleAxisLocal = Vector2.up;
    public bool spin = true;
    public float spinDegPerSec = 4f;

    [Header("Magnetopause")]
    public bool magnetopauseEnabled = true;
    [Min(0.01f)] public float mpBaseRadius = 7.5f;
    [Min(0.01f)] public float mpLateralRadius = 7.6f;
    [Min(0.0001f)] public float mpBlend = 1.1f;

    [Header("Wind (local XY)")]
    public Vector2 windDirLocal = Vector2.right;
    [Range(0, 3)] public float windWeight = 0f;

    [Header("Wind Deformation")]
    [Min(0.001f)] public float mpWindNorm = 1.0f;
    [Range(0, 1f)] public float mpWindCompress = 0.45f;
    [Range(0, 2f)] public float mpWindTailGain = 0.65f;
    [Range(0, 0.5f)] public float mpWindLateralSqueeze = 0.075f;

    // ---- buffers ----
    [StructLayout(LayoutKind.Sequential)]
    struct Segment { public Vector3 a; public Vector3 b; public float mag; }

    ComputeBuffer _segBuf, _segCountBuf, _argsBuf;
    int _k;
    bool _inited;

    // ids
    static readonly int _SegmentsID = Shader.PropertyToID("_Segments");
    static readonly int _LocalToWorldID = Shader.PropertyToID("_LocalToWorld");
    static readonly int _AlphaID = Shader.PropertyToID("_Alpha");
    static readonly int _LineColorNearID = Shader.PropertyToID("_LineColorNear");
    static readonly int _LineColorFarID = Shader.PropertyToID("_LineColorFar");
    static readonly int _ColorMagScaleID = Shader.PropertyToID("_ColorMagScale");

    void OnEnable() => Init();
    void OnDisable() => Release();

    public void SetWindDirFromGridLocal(Vector2 dir)
    {
        if (dir.sqrMagnitude < 1e-6f) dir = Vector2.right;
        windDirLocal = dir.normalized;
    }

    void Init()
    {
        if (_inited) return;
        if (!fieldCS || !lineMaterial)
        {
            // allow scene to exist without spamming errors
            return;
        }

        _k = fieldCS.FindKernel("BuildLinesCS");

        Allocate();
        _inited = true;
    }

    void Allocate()
    {
        Release();

        int cap = Mathf.Max(1, lineCount * Mathf.Max(1, samplesPerLine - 1));
        _segBuf = new ComputeBuffer(cap, 48, ComputeBufferType.Append);
        _segBuf.SetCounterValue(0);

        _segCountBuf = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.IndirectArguments);
        _argsBuf = new ComputeBuffer(4, sizeof(uint), ComputeBufferType.IndirectArguments);
        _argsBuf.SetData(new uint[4] { 0, 1, 0, 0 });

        fieldCS.SetBuffer(_k, _SegmentsID, _segBuf);
    }

    void Release()
    {
        _segBuf?.Release(); _segBuf = null;
        _segCountBuf?.Release(); _segCountBuf = null;
        _argsBuf?.Release(); _argsBuf = null;
        _inited = false;
    }

    void Update()
    {
        if (!_inited) Init();
        if (!_inited) return;

        // Resize if needed
        int neededCap = Mathf.Max(1, lineCount * Mathf.Max(1, samplesPerLine - 1));
        if (_segBuf == null || _segBuf.count < neededCap)
            Allocate();

        _segBuf.SetCounterValue(0);

        // Axis (spin only rotates the axis, it doesn't “tear” lines)
        float ang = (spin ? Time.time * spinDegPerSec : 0f) * Mathf.Deg2Rad;
        Vector2 a0 = (dipoleAxisLocal.sqrMagnitude < 1e-6f) ? Vector2.up : dipoleAxisLocal.normalized;
        Vector2 a = new Vector2(
            a0.x * Mathf.Cos(ang) - a0.y * Mathf.Sin(ang),
            a0.x * Mathf.Sin(ang) + a0.y * Mathf.Cos(ang)
        );
        Vector2 p = new Vector2(-a.y, a.x);

        Vector2 w = (windDirLocal.sqrMagnitude < 1e-6f) ? Vector2.right : windDirLocal.normalized;

        // Set params
        fieldCS.SetInt("_LineCount", lineCount);
        fieldCS.SetInt("_SamplesPerLine", samplesPerLine);
        fieldCS.SetVector("_Center", Vector3.zero);

        fieldCS.SetVector("_AxisA", new Vector4(a.x, a.y, 0, 0));
        fieldCS.SetVector("_AxisP", new Vector4(p.x, p.y, 0, 0));

        fieldCS.SetFloat("_LMin", LMin);
        fieldCS.SetFloat("_LMax", LMax);

        fieldCS.SetFloat("_MPEnable", magnetopauseEnabled ? 1f : 0f);
        fieldCS.SetFloat("_MPBaseRadius", mpBaseRadius);
        fieldCS.SetFloat("_MPLateralRadius", mpLateralRadius);
        fieldCS.SetFloat("_MPBlend", mpBlend);

        fieldCS.SetVector("_WindDir", new Vector4(w.x, w.y, 0, 0));
        fieldCS.SetFloat("_WindWeight", windWeight);

        fieldCS.SetFloat("_MPWindNorm", mpWindNorm);
        fieldCS.SetFloat("_MPWindCompress", mpWindCompress);
        fieldCS.SetFloat("_MPWindTailGain", mpWindTailGain);
        fieldCS.SetFloat("_MPWindLateralSqueeze", mpWindLateralSqueeze);

        // Rebind (safe during compute recompiles)
        fieldCS.SetBuffer(_k, _SegmentsID, _segBuf);

        int groups = Mathf.CeilToInt(lineCount / 64.0f);
        fieldCS.Dispatch(_k, Mathf.Max(1, groups), 1, 1);

        // Indirect args
        ComputeBuffer.CopyCount(_segBuf, _segCountBuf, 0);
        uint[] segCount = { 0 };
        _segCountBuf.GetData(segCount);

        uint vtx = segCount[0] * 2u;
        _argsBuf.SetData(new uint[4] { vtx, 1, 0, 0 });

        if (vtx == 0) return;

        // Draw
        lineMaterial.SetBuffer(_SegmentsID, _segBuf);
        lineMaterial.SetFloat(_AlphaID, alpha);
        lineMaterial.SetColor(_LineColorNearID, lineColorNear);
        lineMaterial.SetColor(_LineColorFarID, lineColorFar);
        lineMaterial.SetFloat(_ColorMagScaleID, colorByMagScale);

        // Force unit scale (same convention as your previous system)
        lineMaterial.SetMatrix(_LocalToWorldID, Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one));

        Graphics.DrawProceduralIndirect(
            lineMaterial,
            new Bounds(transform.position, Vector3.one * 9999f),
            MeshTopology.Lines,
            _argsBuf
        );
    }
}
