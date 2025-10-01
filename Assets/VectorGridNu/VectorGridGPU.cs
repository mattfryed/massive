using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class VectorGridGPU : MonoBehaviour, IVectorGrid
{
    [Header("Resolution (sim grid)")]
    [Min(2)] public int gridX = 64;
    [Min(2)] public int gridY = 32;
    public Vector2 size = new Vector2(16f, 8f);

    [Header("Visible Lines")]
    [Min(2)] public int visibleX = 17;   // vertical lines shown
    [Min(2)] public int visibleY = 9;    // horizontal lines shown

    [Header("Simulation")]
    [Range(0f, 10f)] public float springK = 3.0f;   // spring to origin
    [Range(0.8f, 0.999f)] public float damping = 0.97f;
    public bool pinEdges = true;

    [Header("Force Falloff")]
    [Range(0,4)] public int falloffMode = 1;  // 0=Linear,1=Smooth,2=Quadratic,3=Gaussian,4=InvSq
    [Min(0)]    public float falloffExp = 1.0f;
    [Range(0f,0.9f)] public float innerFrac = 0.2f;   // inner "plateau" portion of radius
    [Min(0f)]  public float sharpness = 2.0f;       // gaussian/inv-sq aggressiveness
    [Min(0f)]  public float maxSpeed = 12.0f;          // 0 = no clamp; try 8–20

    [Header("Crowd / Clamp")]
    [Range(0f,1f)] public float weightCap = 0.8f;     // 0..1 recommended
    [Min(0f)]      public float crowdStiffness = 0.6f;

    [Header("Rendering")]
    public Material lineMaterial;      // uses GridUnlitLines.shader
    [Range(0.5f, 3.0f)] public float lineThickness = 1.3f; // screen pixels
    public Color lineColor = Color.white;
    public float displacementBrightness = 2.0f; // brightness from displacement magnitude

    [Header("Rendering Resolution (upsampled)")]
    [Min(2)] public int renderX = 128;
    [Min(2)] public int renderY = 64;

    [Header("Compute")]
    public ComputeShader vectorCompute;  // CS_Vectors.compute
    private int _kernel = -1;

    // Public props for IVectorGrid
    public float SpringK { get => springK; set => springK = value; }
    public float Damping  { get => damping;  set => damping  = value; }

    // Force layout must match compute shader
    [StructLayout(LayoutKind.Sequential)]
    public struct Force
    {
        public Vector3 position; public float radius;
        public Vector3 direction; public float strength;
        public uint flags;  // bit0: directional?
        public float innerFrac;        // NEW
        public Vector4 color; // reserved for future use
    }

    // Buffers
    ComputeBuffer _posBuf, _velBuf, _origBuf, _forceBuf;
    readonly List<Force> _forces = new();
    int _forceCapacity = 0;

    // Mesh + rendering
    Mesh _mesh;
    MeshFilter _mf;
    MeshRenderer _mr;
    MaterialPropertyBlock _mpb;

    bool _needsRebuild; 

    // Shader property IDs
    static readonly int _PosID          = Shader.PropertyToID("_Pos");
    static readonly int _OrigPosID      = Shader.PropertyToID("_OrigPos");
    static readonly int _VelID          = Shader.PropertyToID("_Vel");
    static readonly int _CountID        = Shader.PropertyToID("_Count");
    static readonly int _DeltaTimeID    = Shader.PropertyToID("_DeltaTime");
    static readonly int _SpringKID      = Shader.PropertyToID("_Kspring");
    static readonly int _DampingID      = Shader.PropertyToID("_Damping");
    static readonly int _ForcesID       = Shader.PropertyToID("_Forces");
    static readonly int _ForceCountID   = Shader.PropertyToID("_ForceCount");
    static readonly int _GridXID        = Shader.PropertyToID("_GridX");
    static readonly int _GridYID        = Shader.PropertyToID("_GridY");
    static readonly int _LinePxID       = Shader.PropertyToID("_LinePixelWidth");
    static readonly int _LineColorID    = Shader.PropertyToID("_LineColor");
    static readonly int _DispBrightID   = Shader.PropertyToID("_DispBrightness");
    static readonly int _GridSizeID     = Shader.PropertyToID("_GridSize");
    static readonly int _SimGridXID = Shader.PropertyToID("_SimGridX");
    static readonly int _SimGridYID = Shader.PropertyToID("_SimGridY");
    static readonly int _CSGridXID = Shader.PropertyToID("_GridX");
    static readonly int _CSGridYID = Shader.PropertyToID("_GridY");
    static readonly int _PinEdgesID = Shader.PropertyToID("_PinEdges");
    static readonly int _FalloffModeID = Shader.PropertyToID("_FalloffMode");
    static readonly int _FalloffExpID  = Shader.PropertyToID("_FalloffExp");
    static readonly int _InnerFracID   = Shader.PropertyToID("_InnerFrac");
    static readonly int _SharpnessID   = Shader.PropertyToID("_Sharpness");
    static readonly int _MaxSpeedID    = Shader.PropertyToID("_MaxSpeed");
    static readonly int _WeightCapID    = Shader.PropertyToID("_WeightCap");
    static readonly int _CrowdStiffID   = Shader.PropertyToID("_CrowdStiffness");

    // change tracking
    int _currGridX, _currGridY, _currRenderX, _currRenderY;
    Vector2 _currSize;

    // -------------------- Lifecycle --------------------

    void OnEnable()
    {
        _mf = GetComponent<MeshFilter>();
        _mr = GetComponent<MeshRenderer>();
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        if (lineMaterial != null) _mr.sharedMaterial = lineMaterial;

        if (vectorCompute == null)
        {
            Debug.LogError("Assign CS_Vectors.compute to VectorGridGPU.");
            return;
        }

        _kernel = vectorCompute.FindKernel("CSMain");

        BuildMesh();
        AllocateBuffers();
        UploadStaticData();
        ApplyMaterialBindings();
        _currGridX = gridX; _currGridY = gridY;
        _currRenderX = renderX; _currRenderY = renderY;
        _currSize = size;
    }

    void RebuildAll()
    {
        BuildMesh();          // uses renderX/renderY
        AllocateBuffers();    // alloc sim buffers gridX*gridY & bind to compute
        UploadStaticData();   // fill sim grid (gridX/gridY) into _orig/_pos, zero _vel
        ApplyMaterialBindings();
        _currGridX = gridX; _currGridY = gridY;
        _currRenderX = renderX; _currRenderY = renderY;
        _currSize = size;
    }

    void OnDisable()
    {
        ReleaseBuffers();
        if (_mesh != null && Application.isPlaying == false)
            DestroyImmediate(_mesh);
    }

    void OnValidate()
    {
        gridX = Mathf.Max(2, gridX);
        gridY = Mathf.Max(2, gridY);
        renderX = Mathf.Max(2, renderX);
        renderY = Mathf.Max(2, renderY);

        _needsRebuild = true;         // << do not call BuildMesh/Allocate here
        // also refresh kernel id if compute assigned (safe)
        if (vectorCompute != null) _kernel = vectorCompute.FindKernel("CSMain");
    }

    void Update()
    {
        if (_posBuf == null || _velBuf == null || _origBuf == null) return;
        if (vectorCompute == null || _kernel < 0) return;

        if (_needsRebuild)
        {
            RebuildAll();                 // your helper that calls BuildMesh, AllocateBuffers, UploadStaticData, ApplyMaterialBindings
            _needsRebuild = false;
        }

        // Re-acquire kernel if needed (after recompile/reset)
        if (_kernel < 0)
        _kernel = vectorCompute.FindKernel("CSMain");


        // live edit support (play/edit mode): if dimensions changed, rebuild
        bool simChanged    = (gridX != _currGridX) || (gridY != _currGridY) || (size != _currSize);
        bool renderChanged = (renderX != _currRenderX) || (renderY != _currRenderY);
        if (simChanged || renderChanged)
        {
            RebuildAll();
            // don’t early-return; we can run this frame after rebuilding
        }

        // Upload forces
        EnsureForceCapacity(_forces.Count);
        if (_forces.Count > 0) _forceBuf.SetData(_forces);
        vectorCompute.SetInt(_ForceCountID, _forces.Count);

        // Sim params
        vectorCompute.SetFloat(_DeltaTimeID, Application.isPlaying ? Time.deltaTime : 1f / 60f);
        vectorCompute.SetFloat(_SpringKID, springK);
        vectorCompute.SetFloat(_DampingID, damping);

        vectorCompute.SetInt(_CSGridXID, gridX);
        vectorCompute.SetInt(_CSGridYID, gridY);
        vectorCompute.SetInt(_PinEdgesID, pinEdges ? 1 : 0);

        // Ensure buffers are (re)bound each frame (defensive against reloads/recompiles)
        vectorCompute.SetBuffer(_kernel, _PosID,    _posBuf);
        vectorCompute.SetBuffer(_kernel, _VelID,    _velBuf);
        vectorCompute.SetBuffer(_kernel, _OrigPosID,_origBuf);
        vectorCompute.SetBuffer(_kernel, _ForcesID, _forceBuf);

        // Sim uniforms (compute)
        vectorCompute.SetInt(_CSGridXID, gridX);
        vectorCompute.SetInt(_CSGridYID, gridY);
        vectorCompute.SetInt(_PinEdgesID, pinEdges ? 1 : 0);
        vectorCompute.SetInt(_ForceCountID, _forces.Count);
        vectorCompute.SetFloat(_DeltaTimeID, Application.isPlaying ? Time.deltaTime : 1f/60f);
        vectorCompute.SetFloat(_SpringKID, springK);
        vectorCompute.SetFloat(_DampingID, damping);
        vectorCompute.SetInt   (_FalloffModeID, falloffMode);
        vectorCompute.SetFloat (_FalloffExpID,  falloffExp);
        vectorCompute.SetFloat (_InnerFracID,   innerFrac);
        vectorCompute.SetFloat (_SharpnessID,   sharpness);
        vectorCompute.SetFloat (_MaxSpeedID,    maxSpeed);
        vectorCompute.SetFloat (_WeightCapID,   weightCap);
        vectorCompute.SetFloat (_CrowdStiffID,  crowdStiffness);

        // Dispatch
        int count = gridX * gridY;
        int groups = Mathf.CeilToInt(count / 256f);
        vectorCompute.Dispatch(_kernel, Mathf.Max(1, groups), 1, 1);

        // Per-frame material params via MPB (SRP-safe)
        if (_mr != null)
        {
            _mpb.SetFloat(_LinePxID, lineThickness);
            _mpb.SetColor(_LineColorID, lineColor);
            _mpb.SetFloat(_DispBrightID, displacementBrightness);
            _mpb.SetInt(_SimGridXID, gridX);
            _mpb.SetInt(_SimGridYID, gridY);
            _mpb.SetVector(_GridSizeID, size);
            _mr.SetPropertyBlock(_mpb);
        }

        // If callers are pushing transient forces every frame, clear here.
        _forces.Clear();
    }

    // -------------------- IVectorGrid --------------------

    public void SetForces(ReadOnlySpan<Force> forces)
    {
        _forces.Clear();
        for (int i = 0; i < forces.Length; i++) _forces.Add(forces[i]);
    }

    public void AddForce(in Force f) => _forces.Add(f);

    public void ResetGrid()
    {
        if (_origBuf == null || _posBuf == null || _velBuf == null) return;
        // Editor-safe CPU copy; for runtime, consider a small compute kernel copy.
        int count = gridX * gridY;
        var orig = new Vector3[count];
        _origBuf.GetData(orig);
        _posBuf.SetData(orig);
        Array.Clear(orig, 0, orig.Length);
        _velBuf.SetData(orig);
    }

    // -------------------- Internals --------------------

    void ApplyMaterialBindings()
    {
        // Ensure renderer + MPB exist
        if (_mr == null) _mr = GetComponent<MeshRenderer>();
        if (_mr == null) return;                    // no renderer yet
        if (_mpb == null) _mpb = new MaterialPropertyBlock();

        // Need a material and a positions buffer to bind
        if (lineMaterial == null) return;
        if (_posBuf == null) return;

        // Make sure the renderer uses the right material
        _mr.sharedMaterial = lineMaterial;

        // Bind per-renderer properties/buffers
        _mpb.Clear();
        _mpb.SetBuffer(_PosID, _posBuf);
        _mpb.SetInt(_GridXID, gridX);
        _mpb.SetInt(_GridYID, gridY);
        _mpb.SetFloat(_LinePxID, lineThickness);
        _mpb.SetColor(_LineColorID, lineColor);
        _mpb.SetFloat(_DispBrightID, displacementBrightness);
        _mr.SetPropertyBlock(_mpb);
        _mpb.SetInt(_SimGridXID, gridX);
        _mpb.SetInt(_SimGridYID, gridY);
        _mpb.SetVector(_GridSizeID, size);
    }

    void BuildMesh()
    {
        if (_mesh == null)
        {
            _mesh = new Mesh { name = "VectorGrid Mesh" };
            _mesh.indexFormat = IndexFormat.UInt32;
            _mesh.MarkDynamic();
        }

        // Dense render grid (for curvature)
        int vCount = renderX * renderY;
        var verts = new Vector3[vCount];
        var uvs   = new Vector2[vCount];

        var half = size * 0.5f;
        for (int y = 0; y < renderY; y++)
        {
            for (int x = 0; x < renderX; x++)
            {
                int i = x + y * renderX;
                float fx = renderX == 1 ? 0f : (float)x / (renderX - 1);
                float fy = renderY == 1 ? 0f : (float)y / (renderY - 1);
                verts[i] = new Vector3(Mathf.Lerp(-half.x, half.x, fx),
                                       Mathf.Lerp(-half.y, half.y, fy), 0f);
                uvs[i] = new Vector2(fx, fy);
            }
        }

        // Build indices so we draw only 'visible' columns/rows,
        // but each as many small segments along the dense grid → curved lines.
        var indices = new List<int>( (visibleX * (renderY - 1) + visibleY * (renderX - 1)) * 2 );

        // Map from visible line index to render-grid column/row
        // (evenly spaced in 0..render-1)
        System.Func<int,int,int> MapToRender = (vis, render) =>
            Mathf.RoundToInt(vis * (render - 1) / (float)(Mathf.Max(1, (vis == 0 ? 1 : 1)))); // dummy to keep delegate happy

        // Vertical visible lines (columns)
        for (int vx = 0; vx < visibleX; vx++)
        {
            int xRender = Mathf.RoundToInt(vx * (renderX - 1) / (float)(visibleX - 1));
            for (int y = 0; y < renderY - 1; y++)
            {
                int a = xRender + y * renderX;
                int b = xRender + (y + 1) * renderX;
                indices.Add(a); indices.Add(b);
            }
        }

        // Horizontal visible lines (rows)
        for (int vy = 0; vy < visibleY; vy++)
        {
            int yRender = Mathf.RoundToInt(vy * (renderY - 1) / (float)(visibleY - 1));
            int row = yRender * renderX;
            for (int x = 0; x < renderX - 1; x++)
            {
                int a = row + x;
                int b = row + x + 1;
                indices.Add(a); indices.Add(b);
            }
        }

        _mesh.Clear();
        _mesh.SetVertices(verts);
        _mesh.SetUVs(0, uvs);
        _mesh.SetIndices(indices, MeshTopology.Lines, 0, true);
        _mesh.RecalculateBounds();

        if (_mf == null) _mf = GetComponent<MeshFilter>();
        _mf.sharedMesh = _mesh;
    }

    void AllocateBuffers()
    {
        int count = gridX * gridY;
        ReleaseBuffers();

        _posBuf  = new ComputeBuffer(count, Marshal.SizeOf<Vector3>(), ComputeBufferType.Structured);
        _velBuf  = new ComputeBuffer(count, Marshal.SizeOf<Vector3>(), ComputeBufferType.Structured);
        _origBuf = new ComputeBuffer(count, Marshal.SizeOf<Vector3>(), ComputeBufferType.Structured);

        // forces buffer sized lazily in EnsureForceCapacity
        _forceBuf = new ComputeBuffer(1, Marshal.SizeOf<Force>(), ComputeBufferType.Structured);
        _forceCapacity = 1;

        // Ensure kernel is valid
        if (_kernel < 0 && vectorCompute != null)
        _kernel = vectorCompute.FindKernel("CSMain");


        if (vectorCompute != null && _kernel >= 0)
        {
            vectorCompute.SetBuffer(_kernel, _PosID, _posBuf);
            vectorCompute.SetBuffer(_kernel, _VelID, _velBuf);
            vectorCompute.SetBuffer(_kernel, _OrigPosID, _origBuf);
            vectorCompute.SetBuffer(_kernel, _ForcesID, _forceBuf);
            vectorCompute.SetInt(_CountID, count);
            vectorCompute.SetInt(_GridXID, gridX);
            vectorCompute.SetInt(_GridYID, gridY);
            vectorCompute.SetInt(_PinEdgesID, pinEdges ? 1 : 0);
        }
    }

    void UploadStaticData()
    {
        if (_origBuf == null || _posBuf == null || _velBuf == null) return;

        int count = gridX * gridY;

        // Build SIM grid (gridX x gridY) in local space:
        var sim = new Vector3[count];
        Vector2 half = size * 0.5f;

        for (int y = 0; y < gridY; y++)
        {
            for (int x = 0; x < gridX; x++)
            {
                int i = x + y * gridX;
                float fx = gridX == 1 ? 0f : (float)x / (gridX - 1);
                float fy = gridY == 1 ? 0f : (float)y / (gridY - 1);
                float px = Mathf.Lerp(-half.x, half.x, fx);
                float py = Mathf.Lerp(-half.y, half.y, fy);
                sim[i] = new Vector3(px, py, 0f);
            }
        }

        _origBuf.SetData(sim);
        _posBuf.SetData(sim);

        Array.Clear(sim, 0, sim.Length);
        _velBuf.SetData(sim); // zero
    }

    void EnsureForceCapacity(int want)
    {
        if (want <= _forceCapacity) return;
        _forceCapacity = Mathf.NextPowerOfTwo(Mathf.Max(1, want));
        _forceBuf?.Release();
        _forceBuf = new ComputeBuffer(_forceCapacity, Marshal.SizeOf<Force>(), ComputeBufferType.Structured);
        if (vectorCompute != null && _kernel >= 0)
            vectorCompute.SetBuffer(_kernel, _ForcesID, _forceBuf);
    }

    void ReleaseBuffers()
    {
        _posBuf?.Release(); _posBuf = null;
        _velBuf?.Release(); _velBuf = null;
        _origBuf?.Release(); _origBuf = null;
        _forceBuf?.Release(); _forceBuf = null;
    }

    // ------------ Helpers to build forces ------------

    public static Force MakeRadial(Vector3 pos, float radius, float strength, float innerFrac = 0f)
    {
        return new Force {
            position = pos, radius = Mathf.Max(0.001f, radius),
            direction = Vector3.zero, strength = strength,
            flags = 0u, innerFrac = Mathf.Clamp(innerFrac, 0f, 0.9f),
            color = Vector4.zero
        };
    }

    public static Force MakeDirectional(Vector3 pos, float radius, Vector3 dir, float strength, float innerFrac = 0f)
    {
        return new Force {
            position = pos, radius = Mathf.Max(0.001f, radius),
            direction = (dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.right),
            strength = strength, flags = 1u, innerFrac = Mathf.Clamp(innerFrac, 0f, 0.9f),
            color = Vector4.zero
        };
    }}
