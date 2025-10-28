using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class VectorGridGPU : MonoBehaviour, IVectorGrid
{
    [SerializeField] Material borderMaterial;

    [Header("Resolution (sim grid)")]
    [Min(2)] public int gridX = 64;
    [Min(2)] public int gridY = 32;
    public Vector2 size = new Vector2(16f, 8f);

    [Header("Visible Lines")]
    [Min(2)] public int visibleX = 17;   // vertical lines shown
    [Min(2)] public int visibleY = 9;    // horizontal lines shown

    [Header("Simulation")]
    [Tooltip("Pull to rest. Higher = faster return. Units ~1/s² with dt.")]
    [Range(0f, 25f)] public float springK = 12f;

    [Tooltip("Viscous damping (rate). Higher = less bounce, faster settle. Units ~1/s.")]
    [Range(0f, 5f)] public float damping = 2.2f;
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

    [System.Serializable]
    public struct BoundarySettings
    {
        [Header("Soft edge band")]
        [Min(0)] public int featherCells;                 // 0..6 good
        [Range(0.25f, 4f)] public float featherSharp;     // falloff curve
        [Range(1f, 4f)] public float edgeSpringMul;       // extra spring near edge
        [Range(1f, 3f)] public float edgeDampMul;         // extra damping near edge
        [Range(0f, 0.2f)] public float edgeForceMin;      // leave a whisper of force

        [Header("Projection / Bounce")]
        public bool projectAtEdge;                        // enable project/bounce
        [Range(0f, 0.05f)] public float edgeAllowance;    // tiny overshoot allowance
        [Range(0f, 1.5f)] public float restitution;       // 0=stick, 1=perfect bounce

        [Header("Border Overlay (visual)")]
        public bool borderOverlay;                        // do second draw for border
        [Range(1f, 4f)] public float borderWidthMul;      // thickness multiplier
        [Range(0f, 0.25f)] public float borderWidthWorld;  // full width in world units

        public Color borderColor;

        [Header("Feather curve")]
        public bool useExpFeather;              // enable exponential shaping
        [Range(0f, 16f)] public float featherExpK;  // 0..16; 6 is a good start
    }

    [Header("Boundary")]
    public BoundarySettings boundary = new BoundarySettings
    {
        featherCells = 3,
        featherSharp = 2f,
        useExpFeather = true,
        featherExpK = 6f,
        edgeSpringMul = 2f,
        edgeDampMul = 1.5f,
        edgeForceMin = 0f,
        projectAtEdge = true,
        edgeAllowance = 0f,
        restitution = 0.6f,
        borderOverlay = true,
        borderWidthMul = 2f,
        borderWidthWorld = 0.02f,
        borderColor = new Color(1, 1, 1, 1)
    };

    [Header("Intro")]
    public bool playIntroOnEnable = true;
    [Min(0.1f)] public float introDuration = 1.0f;
    [Min(1f)] public float introStartScale = 6f;   // how “zoomed” it begins
    [Range(0f, 1f)] public float introFisheyeK = 0.35f;
    [Range(0f, 0.1f)] public float introWarpAmp = 0.02f;  // NDC units
    [Range(0f, 12f)] public float introWarpFreq = 4f;    // Hz

    // IDs
    static readonly int _IntroTID = Shader.PropertyToID("_IntroT");
    static readonly int _IntroScale0ID = Shader.PropertyToID("_IntroScale0");
    static readonly int _IntroFishKID = Shader.PropertyToID("_IntroFisheyeK");
    static readonly int _IntroWarpAmpID = Shader.PropertyToID("_IntroWarpAmp");
    static readonly int _IntroWarpFreqID = Shader.PropertyToID("_IntroWarpFreq");
    static readonly int _IntroTimeID = Shader.PropertyToID("_IntroTime");

    // state
    float _introT = 1f;   // 1 = off by default



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
    Mesh _borderMesh;


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

    // ---- Shader property IDs (compute + render) ----
    static readonly int _FeatherCellsID = Shader.PropertyToID("_FeatherCells");
    static readonly int _FeatherSharpID = Shader.PropertyToID("_FeatherSharp");
    static readonly int _FeatherUseExpID = Shader.PropertyToID("_FeatherUseExp");
    static readonly int _FeatherExpKID = Shader.PropertyToID("_FeatherExpK");
    static readonly int _EdgeSpringMulID = Shader.PropertyToID("_EdgeSpringMul");
    static readonly int _EdgeDampMulID = Shader.PropertyToID("_EdgeDampMul");
    static readonly int _EdgeForceMinID = Shader.PropertyToID("_EdgeForceMin");

    static readonly int _ProjectAtEdgeID = Shader.PropertyToID("_ProjectAtEdge"); // int/bool in HLSL
    static readonly int _EdgeAllowanceID = Shader.PropertyToID("_EdgeAllowance");
    static readonly int _RestitutionID = Shader.PropertyToID("_Restitution");

    // render-side (overlay)
    static readonly int _BorderOnlyID = Shader.PropertyToID("_BorderOnly");
    static readonly int _BorderWidthMulID = Shader.PropertyToID("_BorderWidthMul");
    static readonly int _BorderColorID = Shader.PropertyToID("_BorderColor");
    static readonly int _BorderHalfWidthID = Shader.PropertyToID("_BorderHalfWidth");


    // Optional: keep a second MPB for overlay
    MaterialPropertyBlock _mpbBorder;

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

        // Ensure we have a dedicated border material
        if (borderMaterial == null && lineMaterial != null)
            borderMaterial = new Material(lineMaterial) { name = lineMaterial.name + " (Border)" };

        if (vectorCompute == null)
        {
            Debug.LogError("Assign CS_Vectors.compute to VectorGridGPU.");
            return;
        }

        _kernel = vectorCompute.FindKernel("CSMain");

        BuildMesh();
        BuildBorderStripMesh();
        AllocateBuffers();
        UploadStaticData();
        ApplyMaterialBindings();
        _currGridX = gridX; _currGridY = gridY;
        _currRenderX = renderX; _currRenderY = renderY;
        _currSize = size;

        if (playIntroOnEnable)
        {
            _introT = 0f;
            StopAllCoroutines();
            StartCoroutine(RunIntro());
        }
        else _introT = 1f;
    }

    System.Collections.IEnumerator RunIntro()
    {
        float t = 0f;
        while (t < introDuration)
        {
            _introT = Mathf.Clamp01(t / introDuration);
            t += Application.isPlaying ? Time.deltaTime : (1f / 60f);
            yield return null;
        }
        _introT = 1f; // end clean
    }

    void RebuildAll()
    {
        BuildMesh();          // uses renderX/renderY
        BuildBorderStripMesh();
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
        if (_borderMesh != null && Application.isPlaying == false)
        DestroyImmediate(_borderMesh);

    }

    void OnValidate()
    {
        gridX = Mathf.Max(2, gridX);
        gridY = Mathf.Max(2, gridY);
        renderX = Mathf.Max(2, renderX);
        renderY = Mathf.Max(2, renderY);

        boundary.featherCells = Mathf.Max(0, boundary.featherCells);
        boundary.borderWidthMul = Mathf.Max(1f, boundary.borderWidthMul);

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
        bool simChanged = (gridX != _currGridX) || (gridY != _currGridY) || (size != _currSize);
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


        // Ensure buffers are (re)bound each frame (defensive against reloads/recompiles)
        vectorCompute.SetBuffer(_kernel, _PosID, _posBuf);
        vectorCompute.SetBuffer(_kernel, _VelID, _velBuf);
        vectorCompute.SetBuffer(_kernel, _OrigPosID, _origBuf);
        vectorCompute.SetBuffer(_kernel, _ForcesID, _forceBuf);

        // Sim uniforms (compute)
        vectorCompute.SetInt(_CSGridXID, gridX);
        vectorCompute.SetInt(_CSGridYID, gridY);
        vectorCompute.SetInt(_PinEdgesID, pinEdges ? 1 : 0);
        vectorCompute.SetInt(_ForceCountID, _forces.Count);
        vectorCompute.SetFloat(_DeltaTimeID, Application.isPlaying ? Time.deltaTime : 1f / 60f);
        vectorCompute.SetFloat(_SpringKID, springK);
        vectorCompute.SetFloat(_DampingID, Mathf.Max(0f, damping));
        vectorCompute.SetInt(_FalloffModeID, falloffMode);
        vectorCompute.SetFloat(_FalloffExpID, falloffExp);
        vectorCompute.SetFloat(_InnerFracID, innerFrac);
        vectorCompute.SetFloat(_SharpnessID, sharpness);
        vectorCompute.SetFloat(_MaxSpeedID, maxSpeed);
        vectorCompute.SetFloat(_WeightCapID, weightCap);
        vectorCompute.SetFloat(_CrowdStiffID, crowdStiffness);

        // NEW: boundary uniforms every frame (handles live edits)
        ApplyBoundaryUniforms();

        // Dispatch
        int count = gridX * gridY;
        int groups = Mathf.CeilToInt(count / 256f);
        vectorCompute.Dispatch(_kernel, Mathf.Max(1, groups), 1, 1);

        // -------- Main grid draw (MeshRenderer) ----------
        if (_mr != null)
        {
            _mpb.Clear();

            // Always bind buffer first
            _mpb.SetBuffer(_PosID, _posBuf);

            // Shared uniforms
            _mpb.SetInt(_SimGridXID, gridX);
            _mpb.SetInt(_SimGridYID, gridY);
            _mpb.SetVector(_GridSizeID, size);

            // Visuals
            _mpb.SetFloat(_LinePxID, lineThickness);
            _mpb.SetColor(_LineColorID, lineColor);
            _mpb.SetFloat(_DispBrightID, displacementBrightness);
            _mpb.SetInt(_BorderOnlyID, 0);

            // Intro warp
            PushIntroToMPB(_mpb);

            _mr.SetPropertyBlock(_mpb);
        }

        // -------- Border strip draw (second draw) ----------
        if (boundary.borderOverlay && _borderMesh != null && _posBuf != null && borderMaterial != null)
        {
            if (_mpbBorder == null) _mpbBorder = new MaterialPropertyBlock();
            _mpbBorder.Clear();

            _mpbBorder.SetBuffer(_PosID, _posBuf);
            _mpbBorder.SetInt(_SimGridXID, gridX);
            _mpbBorder.SetInt(_SimGridYID, gridY);
            _mpbBorder.SetVector(_GridSizeID, size);

            float halfW = 0.5f * Mathf.Max(0f, boundary.borderWidthWorld) * Mathf.Max(1f, boundary.borderWidthMul);
            _mpbBorder.SetFloat(_BorderHalfWidthID, halfW);
            _mpbBorder.SetColor(_BorderColorID, boundary.borderColor);

            // Intro warp on the border too
            PushIntroToMPB(_mpbBorder);

            if (_borderMesh.subMeshCount > 0)
                Graphics.DrawMesh(_borderMesh, transform.localToWorldMatrix, borderMaterial,
                                  gameObject.layer, null, 0, _mpbBorder, false, false);
        }

        void PushIntroToMPB(MaterialPropertyBlock mpb)
        {
            if (mpb == null) return;
            mpb.SetFloat(_IntroTID, _introT);
            mpb.SetFloat(_IntroScale0ID, introStartScale);
            mpb.SetFloat(_IntroFishKID, introFisheyeK);
            mpb.SetFloat(_IntroWarpAmpID, introWarpAmp);
            mpb.SetFloat(_IntroWarpFreqID, introWarpFreq);
            mpb.SetFloat(_IntroTimeID, Time.time);   // <— drives the warble
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
        _mpb.SetInt(_SimGridXID, gridX);
        _mpb.SetInt(_SimGridYID, gridY);
        _mpb.SetVector(_GridSizeID, size);


            _mr.SetPropertyBlock(_mpb);
        }

    void ApplyBoundaryUniforms()
    {
        if (vectorCompute == null || _kernel < 0) return;

        vectorCompute.SetInt(_FeatherCellsID, boundary.featherCells);
        vectorCompute.SetFloat(_FeatherSharpID, boundary.featherSharp);
        vectorCompute.SetInt(_FeatherUseExpID, boundary.useExpFeather ? 1 : 0);
        vectorCompute.SetFloat(_FeatherExpKID, Mathf.Max(0f, boundary.featherExpK));
        vectorCompute.SetFloat(_EdgeSpringMulID, boundary.edgeSpringMul);
        vectorCompute.SetFloat(_EdgeDampMulID, boundary.edgeDampMul);
        vectorCompute.SetFloat(_EdgeForceMinID, boundary.edgeForceMin);

        // HLSL 'bool' is int on many backends; be explicit:
        vectorCompute.SetInt(_ProjectAtEdgeID, boundary.projectAtEdge ? 1 : 0);
        vectorCompute.SetFloat(_EdgeAllowanceID, boundary.edgeAllowance);
        vectorCompute.SetFloat(_RestitutionID, boundary.restitution);
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

        // Inflate bounds so shader-side intro zoom/warp isn't frustum-culled
        float maxScaleForBounds = Mathf.Max(1f, introStartScale);
        var expandedBoundsMain = new Bounds(
            Vector3.zero,
            new Vector3(size.x * maxScaleForBounds * 1.1f,
                        size.y * maxScaleForBounds * 1.1f,
                        4f) // small thickness in Z
        );
        _mesh.bounds = expandedBoundsMain;

        if (_mf == null) _mf = GetComponent<MeshFilter>();
        _mf.sharedMesh = _mesh;
    }
    void BuildBorderStripMesh()
        {
            if (_borderMesh == null) _borderMesh = new Mesh { name = "VectorGrid BorderStrip" };
            _borderMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            _borderMesh.MarkDynamic();

            var verts = new List<Vector3>();
            var uv0   = new List<Vector2>();   // uv of the strip vertex
            var uv1   = new List<Vector2>();   // step along the edge (to compute tangent)
            var uv2   = new List<Vector2>();   // x = side (-1 or +1), y unused
            var idx   = new List<int>();

            // helper to append one edge as a triangle strip
            void AppendEdge(bool horizontal, bool atMax, int count, ref int baseV)
            {
                // uv step along the edge (render grid)
                float du = horizontal ? (count > 1 ? 1f / (count - 1) : 0f) : 0f;
                float dv = horizontal ? 0f : (count > 1 ? 1f / (count - 1) : 0f);

                for (int i = 0; i < count; i++)
                {
                    float fx = horizontal ? (float)i / Mathf.Max(1, count - 1) : (atMax ? 1f : 0f);
                    float fy = horizontal ? (atMax ? 1f : 0f) : (float)i / Mathf.Max(1, count - 1);

                    // two verts per sample: side = -1 (inner) and +1 (outer)
                    for (int s = -1; s <= 1; s += 2)
                    {
                        verts.Add(Vector3.zero);                // shader computes position
                        uv0.Add(new Vector2(fx, fy));
                        uv1.Add(new Vector2(du, dv));
                        uv2.Add(new Vector2((float)s, 0f));
                    }
                }

                // triangles
                int pairs = count - 1;
                for (int i = 0; i < pairs; i++)
                {
                    int a = baseV + i * 2;
                    int b = a + 1;
                    int c = a + 2;
                    int d = a + 3;
                    idx.Add(a); idx.Add(b); idx.Add(c);
                    idx.Add(b); idx.Add(d); idx.Add(c);
                }
                baseV += count * 2;
            }

            int v = 0;
            // bottom (y=0), horizontal along X
            AppendEdge(horizontal: true,  atMax: false, count: renderX, ref v);
            // top (y=1), horizontal along X
            AppendEdge(horizontal: true,  atMax: true,  count: renderX, ref v);
            // left (x=0), vertical along Y
            AppendEdge(horizontal: false, atMax: false, count: renderY, ref v);
            // right (x=1), vertical along Y
            AppendEdge(horizontal: false, atMax: true,  count: renderY, ref v);

            _borderMesh.Clear();
            _borderMesh.SetVertices(verts);
            _borderMesh.SetUVs(0, uv0);
            _borderMesh.SetUVs(1, uv1);
            _borderMesh.SetUVs(2, uv2);
            _borderMesh.SetIndices(idx, MeshTopology.Triangles, 0, true);
            _borderMesh.RecalculateBounds();

            float maxScaleForBounds2 = Mathf.Max(1f, introStartScale);
            var expandedBoundsBorder = new Bounds(
                Vector3.zero,
                new Vector3(size.x * maxScaleForBounds2 * 1.1f,
                            size.y * maxScaleForBounds2 * 1.1f,
                            4f)
            );
            _borderMesh.bounds = expandedBoundsBorder;

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
            // NEW: push boundary params once here (also pushed every frame in Update)
            ApplyBoundaryUniforms();
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
