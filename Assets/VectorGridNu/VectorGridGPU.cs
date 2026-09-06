using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class VectorGridGPU : MonoBehaviour, IVectorGrid
{
    public static VectorGridGPU Instance { get; private set; }

    [Header("Grid Layout")]
    [Tooltip("Authoritative arena size in the grid object's local XY plane. " +
             "Changing Grid Scale never changes this value.")]
    public Vector2 size = new Vector2(16f, 8f);

    [Tooltip("Target side length of one visible square. The arena Size remains fixed. " +
             "The script resolves this to the nearest even X-cell count so a vertical center line always exists, " +
             "then uses that same resolved spacing on Y to keep the visible cells square.")]
    [Range(0.25f, 4f)]
    public float gridScale = 1f;

    [Header("Resolution Multipliers")]
    [Tooltip("Approximate simulation intervals inside one resolved visible square. " +
             "X divides exactly; Y is fitted as closely as possible while keeping the fixed arena size.")]
    [Range(1, 8)]
    public int simulationSubdivisionsPerCell = 2;

    [Tooltip("Render segments inside each simulation interval. Higher values make curved deformation smoother " +
             "without increasing the number of simulated points.")]
    [Range(1, 8)]
    public int renderSubdivisionsPerSimulationInterval = 5;

    [Header("Presentation / Intro-Outro")]
    [Tooltip("Smallest continuous presentation-scale multiplier the prebuilt overscan mesh must support. " +
             "Use 0.5 to allow a smooth 2x zoom-out. Lower values create a larger mesh.")]
    [Range(0.25f, 1f)]
    [SerializeField] private float minimumPresentationScale = 0.5f;

    [Tooltip("Largest continuous presentation-scale multiplier supported by the mesh's culling bounds.")]
    [Range(1f, 8f)]
    [SerializeField] private float maximumPresentationScale = 4f;

    [Tooltip("Runtime-only visual spacing multiplier. 1 is the authored layout; values above 1 enlarge the squares, " +
             "and values below 1 shrink them. This does not rebuild simulation buffers or change arena bounds.")]
    [SerializeField, Min(0.01f)] private float presentationScale = 1f;

    [Tooltip("When enabled, presentation lines may render outside the authoritative arena rectangle. " +
             "Useful during level intro/outro transitions.")]
    [SerializeField] private bool presentationIgnoresBounds;

    // Derived settled layout. These replace the old independently-authored
    // gridX/gridY, visibleX/visibleY, and renderX/renderY inspector fields.
    [SerializeField, HideInInspector] private int gridX;
    [SerializeField, HideInInspector] private int gridY;
    [SerializeField, HideInInspector] private int visibleX;
    [SerializeField, HideInInspector] private int visibleY;
    [SerializeField, HideInInspector] private int renderX;
    [SerializeField, HideInInspector] private int renderY;
    [SerializeField, HideInInspector] private float resolvedGridScale;
    [SerializeField, HideInInspector] private float yEdgeRemainderPerSide;

    public int GridX => gridX;
    public int GridY => gridY;
    public int VisibleX => visibleX;
    public int VisibleY => visibleY;
    public int RenderX => renderX;
    public int RenderY => renderY;
    public float ResolvedGridScale => resolvedGridScale;
    public float YEdgeRemainderPerSide => yEdgeRemainderPerSide;
    public float MinimumPresentationScale => minimumPresentationScale;
    public float MaximumPresentationScale => maximumPresentationScale;
    public float PresentationScale => presentationScale;
    public bool PresentationIgnoresBounds => presentationIgnoresBounds;
    public Vector2Int VisibleCellCounts =>
        new Vector2Int(Mathf.Max(0, visibleX - 1), Mathf.Max(0, visibleY - 1));
    public Vector2 SimulationCellSize => new Vector2(
        gridX > 1 ? size.x / (gridX - 1) : 0f,
        gridY > 1 ? size.y / (gridY - 1) : 0f);
    public int RenderSegmentsPerVisibleCell =>
        simulationSubdivisionsPerCell * renderSubdivisionsPerSimulationInterval;

    [Header("Simulation")]
    [Tooltip("Pull to rest. Higher = faster return. Units ~1/s² with dt.")]
    [Range(0f, 25f)] public float springK = 12f;

    [Tooltip("Viscous damping (rate). Higher = less bounce, faster settle. Units ~1/s.")]
    [Range(0f, 5f)] public float damping = 2.2f;
    public bool pinEdges = true;

    [Header("Force Falloff")]
    [Range(0, 4)] public int falloffMode = 1;  // 0=Linear,1=Smooth,2=Quadratic,3=Gaussian,4=InvSq
    [Min(0)] public float falloffExp = 1.0f;
    [Range(0f, 0.9f)] public float innerFrac = 0.2f;
    [Min(0f)] public float sharpness = 2.0f;
    [Min(0f)] public float maxSpeed = 12.0f;   // 0 = no clamp

    [Header("Crowd / Clamp")]
    [Range(0f, 1f)] public float weightCap = 0.8f;
    [Min(0f)] public float crowdStiffness = 0.6f;

    [Header("Rendering")]
    public Material lineMaterial;
    [Range(0.5f, 3.0f)] public float lineThickness = 1.3f;
    public Color lineColor = Color.white;
    public float displacementBrightness = 2.0f;

    [Header("Compute")]
    public ComputeShader vectorCompute;
    private int _kernel = -1;

    [Serializable]
    public struct BoundarySettings
    {
        [Header("Soft edge band")]
        [Min(0)] public int featherCells;
        [Range(0.25f, 4f)] public float featherSharp;
        [Range(1f, 4f)] public float edgeSpringMul;
        [Range(1f, 3f)] public float edgeDampMul;
        [Range(0f, 0.2f)] public float edgeForceMin;

        [Header("Projection / Bounce")]
        public bool projectAtEdge;
        [Range(0f, 0.05f)] public float edgeAllowance;
        [Range(0f, 1.5f)] public float restitution;

        [Header("Border Overlay (visual)")]
        public bool borderOverlay;
        [Range(1f, 4f)] public float borderWidthMul;
        [Range(0f, 0.25f)] public float borderWidthWorld;
        public Color borderColor;

        [Header("Feather curve")]
        public bool useExpFeather;
        [Range(0f, 16f)] public float featherExpK;
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
        borderColor = new Color(1f, 1f, 1f, 1f)
    };

    public float SpringK { get => springK; set => springK = value; }
    public float Damping { get => damping; set => damping = value; }

    [StructLayout(LayoutKind.Sequential)]
    public struct Force
    {
        public Vector3 position;
        public float radius;
        public Vector3 direction;
        public float strength;
        public uint flags;
        public float innerFrac;
        public Vector4 color;
    }

    ComputeBuffer _posBuf;
    ComputeBuffer _velBuf;
    ComputeBuffer _origBuf;
    ComputeBuffer _forceBuf;

    readonly List<Force> _forces = new();
    int _forceCapacity;

    Mesh _mesh;
    Mesh _borderMesh;
    MeshFilter _mf;
    MeshRenderer _mr;
    MaterialPropertyBlock _mpb;
    MaterialPropertyBlock _mpbBorder;

    bool _needsRebuild;

    // These meshes are derived entirely from the serialized grid settings.
    // Keeping them out of scenes prevents ExecuteAlways rebuilds from embedding
    // large vertex/index buffers in scene YAML; OnEnable recreates them.
    const HideFlags RuntimeMeshHideFlags =
        HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

    static readonly int _PosID = Shader.PropertyToID("_Pos");
    static readonly int _OrigPosID = Shader.PropertyToID("_OrigPos");
    static readonly int _VelID = Shader.PropertyToID("_Vel");
    static readonly int _CountID = Shader.PropertyToID("_Count");
    static readonly int _DeltaTimeID = Shader.PropertyToID("_DeltaTime");
    static readonly int _SpringKID = Shader.PropertyToID("_Kspring");
    static readonly int _DampingID = Shader.PropertyToID("_Damping");
    static readonly int _ForcesID = Shader.PropertyToID("_Forces");
    static readonly int _ForceCountID = Shader.PropertyToID("_ForceCount");
    static readonly int _GridXID = Shader.PropertyToID("_GridX");
    static readonly int _GridYID = Shader.PropertyToID("_GridY");
    static readonly int _LinePxID = Shader.PropertyToID("_LinePixelWidth");
    static readonly int _LineColorID = Shader.PropertyToID("_LineColor");
    static readonly int _DispBrightID = Shader.PropertyToID("_DispBrightness");
    static readonly int _GridSizeID = Shader.PropertyToID("_GridSize");
    static readonly int _PresentationScaleID = Shader.PropertyToID("_PresentationScale");
    static readonly int _ClipToGridBoundsID = Shader.PropertyToID("_ClipToGridBounds");
    static readonly int _SimGridXID = Shader.PropertyToID("_SimGridX");
    static readonly int _SimGridYID = Shader.PropertyToID("_SimGridY");
    static readonly int _CSGridXID = Shader.PropertyToID("_GridX");
    static readonly int _CSGridYID = Shader.PropertyToID("_GridY");
    static readonly int _PinEdgesID = Shader.PropertyToID("_PinEdges");
    static readonly int _FalloffModeID = Shader.PropertyToID("_FalloffMode");
    static readonly int _FalloffExpID = Shader.PropertyToID("_FalloffExp");
    static readonly int _InnerFracID = Shader.PropertyToID("_InnerFrac");
    static readonly int _SharpnessID = Shader.PropertyToID("_Sharpness");
    static readonly int _MaxSpeedID = Shader.PropertyToID("_MaxSpeed");
    static readonly int _WeightCapID = Shader.PropertyToID("_WeightCap");
    static readonly int _CrowdStiffID = Shader.PropertyToID("_CrowdStiffness");

    static readonly int _FeatherCellsID = Shader.PropertyToID("_FeatherCells");
    static readonly int _FeatherSharpID = Shader.PropertyToID("_FeatherSharp");
    static readonly int _FeatherUseExpID = Shader.PropertyToID("_FeatherUseExp");
    static readonly int _FeatherExpKID = Shader.PropertyToID("_FeatherExpK");
    static readonly int _EdgeSpringMulID = Shader.PropertyToID("_EdgeSpringMul");
    static readonly int _EdgeDampMulID = Shader.PropertyToID("_EdgeDampMul");
    static readonly int _EdgeForceMinID = Shader.PropertyToID("_EdgeForceMin");
    static readonly int _ProjectAtEdgeID = Shader.PropertyToID("_ProjectAtEdge");
    static readonly int _EdgeAllowanceID = Shader.PropertyToID("_EdgeAllowance");
    static readonly int _RestitutionID = Shader.PropertyToID("_Restitution");

    static readonly int _BorderWidthMulID = Shader.PropertyToID("_BorderWidthMul");
    static readonly int _BorderColorID = Shader.PropertyToID("_BorderColor");
    static readonly int _BorderHalfWidthID = Shader.PropertyToID("_BorderHalfWidth");

    int _currGridX;
    int _currGridY;
    int _currVisibleX;
    int _currVisibleY;
    int _currRenderX;
    int _currRenderY;
    float _currResolvedGridScale;
    float _currMinimumPresentationScale;
    float _currMaximumPresentationScale;
    Vector2 _currSize;

    void OnEnable()
    {
        if (Instance != null && Instance != this)
            Debug.LogWarning("Multiple VectorGridGPU detected; Instance will be overwritten.");

        Instance = this;

        ResolveDerivedLayout();

        _mf = GetComponent<MeshFilter>();
        _mr = GetComponent<MeshRenderer>();
        _mpb ??= new MaterialPropertyBlock();

        if (lineMaterial != null)
            _mr.sharedMaterial = lineMaterial;

        if (vectorCompute == null)
        {
            Debug.LogError("Assign GridUnlitLines.compute to VectorGridGPU.");
            return;
        }

        _kernel = vectorCompute.FindKernel("CSMain");
        RebuildAll();
    }

    void OnDisable()
    {
        if (Instance == this)
            Instance = null;

        ReleaseBuffers();

        if (_mf != null && _mf.sharedMesh == _mesh)
            _mf.sharedMesh = null;

        DestroyRuntimeMesh(ref _mesh);
        DestroyRuntimeMesh(ref _borderMesh);
    }

    static void DestroyRuntimeMesh(ref Mesh mesh)
    {
        if (mesh == null)
            return;

        if (Application.isPlaying)
            Destroy(mesh);
        else
            DestroyImmediate(mesh);

        mesh = null;
    }

    void OnValidate()
    {
        ResolveDerivedLayout();

        boundary.featherCells = Mathf.Max(0, boundary.featherCells);
        boundary.borderWidthMul = Mathf.Max(1f, boundary.borderWidthMul);

        _needsRebuild = true;

        if (vectorCompute != null)
            _kernel = vectorCompute.FindKernel("CSMain");
    }

    void Update()
    {
        ResolveDerivedLayout();

        if (vectorCompute == null)
            return;

        if (_kernel < 0)
            _kernel = vectorCompute.FindKernel("CSMain");

        if (_needsRebuild || HasLayoutChanged() ||
            _posBuf == null || _velBuf == null || _origBuf == null)
        {
            RebuildAll();
            _needsRebuild = false;
        }

        if (_posBuf == null || _velBuf == null || _origBuf == null || _kernel < 0)
            return;

        EnsureForceCapacity(_forces.Count);
        if (_forces.Count > 0)
            _forceBuf.SetData(_forces);

        float dt = Application.isPlaying ? Time.deltaTime : 1f / 60f;

        vectorCompute.SetBuffer(_kernel, _PosID, _posBuf);
        vectorCompute.SetBuffer(_kernel, _VelID, _velBuf);
        vectorCompute.SetBuffer(_kernel, _OrigPosID, _origBuf);
        vectorCompute.SetBuffer(_kernel, _ForcesID, _forceBuf);

        vectorCompute.SetInt(_CSGridXID, gridX);
        vectorCompute.SetInt(_CSGridYID, gridY);
        vectorCompute.SetInt(_PinEdgesID, pinEdges ? 1 : 0);
        vectorCompute.SetInt(_ForceCountID, _forces.Count);
        vectorCompute.SetFloat(_DeltaTimeID, dt);
        vectorCompute.SetFloat(_SpringKID, springK);
        vectorCompute.SetFloat(_DampingID, Mathf.Max(0f, damping));
        vectorCompute.SetInt(_FalloffModeID, falloffMode);
        vectorCompute.SetFloat(_FalloffExpID, falloffExp);
        vectorCompute.SetFloat(_InnerFracID, innerFrac);
        vectorCompute.SetFloat(_SharpnessID, sharpness);
        vectorCompute.SetFloat(_MaxSpeedID, maxSpeed);
        vectorCompute.SetFloat(_WeightCapID, weightCap);
        vectorCompute.SetFloat(_CrowdStiffID, crowdStiffness);

        ApplyBoundaryUniforms();

        int count = gridX * gridY;
        int groups = Mathf.CeilToInt(count / 256f);
        vectorCompute.Dispatch(_kernel, Mathf.Max(1, groups), 1, 1);

        if (_mr != null)
        {
            _mpb.SetBuffer(_PosID, _posBuf);
            _mpb.SetInt(_GridXID, gridX);
            _mpb.SetInt(_GridYID, gridY);
            _mpb.SetInt(_SimGridXID, gridX);
            _mpb.SetInt(_SimGridYID, gridY);
            _mpb.SetFloat(_LinePxID, lineThickness);
            _mpb.SetColor(_LineColorID, lineColor);
            _mpb.SetFloat(_DispBrightID, displacementBrightness);
            _mpb.SetVector(_GridSizeID, size);
            _mpb.SetFloat(_PresentationScaleID, presentationScale);
            _mpb.SetFloat(
                _ClipToGridBoundsID,
                presentationIgnoresBounds ? 0f : 1f);
            _mr.SetPropertyBlock(_mpb);
        }

        if (boundary.borderOverlay && _borderMesh != null && lineMaterial != null)
        {
            _mpbBorder ??= new MaterialPropertyBlock();
            _mpbBorder.Clear();

            _mpbBorder.SetBuffer(_PosID, _posBuf);
            _mpbBorder.SetInt(_GridXID, gridX);
            _mpbBorder.SetInt(_GridYID, gridY);
            _mpbBorder.SetInt(_SimGridXID, gridX);
            _mpbBorder.SetInt(_SimGridYID, gridY);
            _mpbBorder.SetVector(_GridSizeID, size);

            float halfW = 0.5f * Mathf.Max(0f, boundary.borderWidthWorld) *
                          Mathf.Max(1f, boundary.borderWidthMul);
            _mpbBorder.SetFloat(_BorderHalfWidthID, halfW);
            _mpbBorder.SetFloat(_BorderWidthMulID, boundary.borderWidthMul);
            _mpbBorder.SetColor(_BorderColorID, boundary.borderColor);

            if (_borderMesh.subMeshCount > 0)
            {
                Graphics.DrawMesh(
                    _borderMesh,
                    transform.localToWorldMatrix,
                    lineMaterial,
                    gameObject.layer,
                    null,
                    0,
                    _mpbBorder,
                    false,
                    false);
            }
        }

        _forces.Clear();
    }

    void ResolveDerivedLayout()
    {
        gridScale = Mathf.Max(0.001f, gridScale);
        simulationSubdivisionsPerCell = Mathf.Max(1, simulationSubdivisionsPerCell);
        renderSubdivisionsPerSimulationInterval =
            Mathf.Max(1, renderSubdivisionsPerSimulationInterval);

        minimumPresentationScale = Mathf.Clamp(minimumPresentationScale, 0.25f, 1f);
        maximumPresentationScale = Mathf.Max(1f, maximumPresentationScale);
        presentationScale = Mathf.Clamp(
            presentationScale,
            minimumPresentationScale,
            maximumPresentationScale);

        // Size is authoritative. Layout resolution must conform to it, never rewrite it.
        size.x = Mathf.Max(0.001f, size.x);
        size.y = Mathf.Max(0.001f, size.y);

        // Fit the requested scale to the nearest EVEN number of X cells.
        // Even X cells => odd X lines => a guaranteed vertical line at local X = 0.
        int cellsX = NearestEvenAtLeastTwo(size.x / gridScale);
        resolvedGridScale = size.x / cellsX;

        visibleX = cellsX + 1;

        // Keep Y spacing identical to X so every complete visible cell is square.
        // If Size Y is not an exact multiple of the resolved spacing, the separate
        // border cuts through a partial row at the top and bottom rather than
        // stretching the cells or changing Size.
        float halfY = size.y * 0.5f;
        float tolerance = resolvedGridScale * 0.0001f;
        int halfVisibleStepsY = Mathf.Max(
            0,
            Mathf.FloorToInt((halfY + tolerance) / resolvedGridScale));

        visibleY = halfVisibleStepsY * 2 + 1;
        yEdgeRemainderPerSide = Mathf.Max(
            0f,
            halfY - halfVisibleStepsY * resolvedGridScale);

        // Simulation uses one physical target spacing for both axes. X divides
        // exactly by construction. Y is fitted to the fixed arena height and kept
        // even so the simulation also has a center row.
        int simIntervalsX = cellsX * simulationSubdivisionsPerCell;
        float targetSimulationStep =
            resolvedGridScale / simulationSubdivisionsPerCell;
        int simIntervalsY = NearestEvenAtLeastTwo(
            size.y / Mathf.Max(0.0001f, targetSimulationStep));

        gridX = simIntervalsX + 1;
        gridY = simIntervalsY + 1;

        renderX = simIntervalsX * renderSubdivisionsPerSimulationInterval + 1;
        renderY = simIntervalsY * renderSubdivisionsPerSimulationInterval + 1;
    }

    static int NearestEvenAtLeastTwo(float value)
    {
        int rounded = Mathf.Max(2, Mathf.RoundToInt(value));
        if ((rounded & 1) == 0)
            return rounded;

        int lower = Mathf.Max(2, rounded - 1);
        int upper = rounded + 1;

        float lowerError = Mathf.Abs(value - lower);
        float upperError = Mathf.Abs(upper - value);
        return upperError <= lowerError ? upper : lower;
    }

    bool HasLayoutChanged()
    {
        return gridX != _currGridX ||
               gridY != _currGridY ||
               visibleX != _currVisibleX ||
               visibleY != _currVisibleY ||
               renderX != _currRenderX ||
               renderY != _currRenderY ||
               !Mathf.Approximately(resolvedGridScale, _currResolvedGridScale) ||
               !Mathf.Approximately(
                   minimumPresentationScale,
                   _currMinimumPresentationScale) ||
               !Mathf.Approximately(
                   maximumPresentationScale,
                   _currMaximumPresentationScale) ||
               size != _currSize;
    }

    void CaptureLayoutState()
    {
        _currGridX = gridX;
        _currGridY = gridY;
        _currVisibleX = visibleX;
        _currVisibleY = visibleY;
        _currRenderX = renderX;
        _currRenderY = renderY;
        _currResolvedGridScale = resolvedGridScale;
        _currMinimumPresentationScale = minimumPresentationScale;
        _currMaximumPresentationScale = maximumPresentationScale;
        _currSize = size;
    }

    void RebuildAll()
    {
        ResolveDerivedLayout();
        BuildMesh();
        BuildBorderStripMesh();
        AllocateBuffers();
        UploadStaticData();
        ApplyMaterialBindings();
        CaptureLayoutState();
    }

    public void SetForces(ReadOnlySpan<Force> forces)
    {
        _forces.Clear();
        for (int i = 0; i < forces.Length; i++)
            _forces.Add(forces[i]);
    }

    public void AddForce(in Force f) => _forces.Add(f);

    public void ResetGrid()
    {
        if (_origBuf == null || _posBuf == null || _velBuf == null)
            return;

        int count = gridX * gridY;
        var orig = new Vector3[count];
        _origBuf.GetData(orig);
        _posBuf.SetData(orig);
        Array.Clear(orig, 0, orig.Length);
        _velBuf.SetData(orig);
    }

    /// <summary>
    /// Changes only the visual spacing used for presentation. This does not
    /// rebuild the simulation grid, change Size, or move arena colliders.
    /// </summary>
    public void SetPresentation(
        float scaleMultiplier,
        bool ignoreOuterBounds)
    {
        presentationScale = Mathf.Clamp(
            scaleMultiplier,
            minimumPresentationScale,
            maximumPresentationScale);
        presentationIgnoresBounds = ignoreOuterBounds;
    }

    /// <summary>
    /// Changes the presentation scale while preserving the current clipping mode.
    /// </summary>
    public void SetPresentationScale(float scaleMultiplier)
    {
        presentationScale = Mathf.Clamp(
            scaleMultiplier,
            minimumPresentationScale,
            maximumPresentationScale);
    }

    public void SetPresentationIgnoresBounds(bool ignoreOuterBounds)
    {
        presentationIgnoresBounds = ignoreOuterBounds;
    }

    public void ResetPresentation()
    {
        presentationScale = 1f;
        presentationIgnoresBounds = false;
    }

    void ApplyMaterialBindings()
    {
        _mr ??= GetComponent<MeshRenderer>();
        if (_mr == null || lineMaterial == null || _posBuf == null)
            return;

        _mpb ??= new MaterialPropertyBlock();
        _mr.sharedMaterial = lineMaterial;

        _mpb.Clear();
        _mpb.SetBuffer(_PosID, _posBuf);
        _mpb.SetInt(_GridXID, gridX);
        _mpb.SetInt(_GridYID, gridY);
        _mpb.SetInt(_SimGridXID, gridX);
        _mpb.SetInt(_SimGridYID, gridY);
        _mpb.SetFloat(_LinePxID, lineThickness);
        _mpb.SetColor(_LineColorID, lineColor);
        _mpb.SetFloat(_DispBrightID, displacementBrightness);
        _mpb.SetVector(_GridSizeID, size);
        _mpb.SetFloat(_PresentationScaleID, presentationScale);
        _mpb.SetFloat(
            _ClipToGridBoundsID,
            presentationIgnoresBounds ? 0f : 1f);
        _mr.SetPropertyBlock(_mpb);
    }

    void ApplyBoundaryUniforms()
    {
        if (vectorCompute == null || _kernel < 0)
            return;

        vectorCompute.SetInt(_FeatherCellsID, boundary.featherCells);
        vectorCompute.SetFloat(_FeatherSharpID, boundary.featherSharp);
        vectorCompute.SetInt(_FeatherUseExpID, boundary.useExpFeather ? 1 : 0);
        vectorCompute.SetFloat(_FeatherExpKID, Mathf.Max(0f, boundary.featherExpK));
        vectorCompute.SetFloat(_EdgeSpringMulID, boundary.edgeSpringMul);
        vectorCompute.SetFloat(_EdgeDampMulID, boundary.edgeDampMul);
        vectorCompute.SetFloat(_EdgeForceMinID, boundary.edgeForceMin);
        vectorCompute.SetInt(_ProjectAtEdgeID, boundary.projectAtEdge ? 1 : 0);
        vectorCompute.SetFloat(_EdgeAllowanceID, boundary.edgeAllowance);
        vectorCompute.SetFloat(_RestitutionID, boundary.restitution);
    }

    void BuildMesh()
    {
        if (_mesh == null)
        {
            _mesh = new Mesh
            {
                name = "VectorGrid Mesh",
                indexFormat = IndexFormat.UInt32,
                hideFlags = RuntimeMeshHideFlags
            };
            _mesh.MarkDynamic();
        }

        Vector2 half = size * 0.5f;
        float minScale = Mathf.Max(0.25f, minimumPresentationScale);

        // Prebuild enough square-lattice lines to cover the arena when the
        // presentation multiplier is at its smallest supported value.
        // Extra lines live outside the settled arena and are normally clipped
        // in the shader. During intro/outro they can be revealed without any
        // topology or compute-buffer rebuild.
        int meshHalfStepsX = Mathf.CeilToInt(
            half.x / Mathf.Max(0.0001f, resolvedGridScale * minScale)) + 1;
        int meshHalfStepsY = Mathf.CeilToInt(
            half.y / Mathf.Max(0.0001f, resolvedGridScale * minScale)) + 1;

        int meshLineCountX = meshHalfStepsX * 2 + 1;
        int meshLineCountY = meshHalfStepsY * 2 + 1;

        // Preserve approximately the same render-segment length at the minimum
        // presentation scale by extending the prebuilt samples with the overscan.
        int meshRenderX = Mathf.Max(
            2,
            Mathf.CeilToInt((renderX - 1) / minScale) + 1);
        int meshRenderY = Mathf.Max(
            2,
            Mathf.CeilToInt((renderY - 1) / minScale) + 1);

        int vertexCapacity =
            meshLineCountX * meshRenderY +
            meshLineCountY * meshRenderX;
        int indexCapacity =
            (meshLineCountX * (meshRenderY - 1) +
             meshLineCountY * (meshRenderX - 1)) * 2;

        var verts = new List<Vector3>(vertexCapacity);
        var uvs = new List<Vector2>(vertexCapacity);
        var indices = new List<int>(indexCapacity);

        float baseMinX = -half.x / minScale;
        float baseMaxX = half.x / minScale;
        float baseMinY = -half.y / minScale;
        float baseMaxY = half.y / minScale;

        // Vertical lines. Their X coordinates are integer multiples of the one
        // resolved square size, centered on local X = 0.
        for (int stepX = -meshHalfStepsX;
             stepX <= meshHalfStepsX;
             stepX++)
        {
            float x = stepX * resolvedGridScale;
            float u = 0.5f + x / size.x;
            int lineStart = verts.Count;

            for (int sampleY = 0; sampleY < meshRenderY; sampleY++)
            {
                float t = meshRenderY <= 1
                    ? 0f
                    : (float)sampleY / (meshRenderY - 1);
                float y = Mathf.Lerp(baseMinY, baseMaxY, t);
                float v = 0.5f + y / size.y;

                verts.Add(new Vector3(x, y, 0f));
                uvs.Add(new Vector2(u, v));

                if (sampleY > 0)
                {
                    indices.Add(lineStart + sampleY - 1);
                    indices.Add(lineStart + sampleY);
                }
            }
        }

        // Horizontal lines use the exact same resolved spacing, so all complete
        // cells are square. A fixed-size arena may leave a partial row at the
        // top/bottom; the independent border remains authoritative.
        for (int stepY = -meshHalfStepsY;
             stepY <= meshHalfStepsY;
             stepY++)
        {
            float y = stepY * resolvedGridScale;
            float v = 0.5f + y / size.y;
            int lineStart = verts.Count;

            for (int sampleX = 0; sampleX < meshRenderX; sampleX++)
            {
                float t = meshRenderX <= 1
                    ? 0f
                    : (float)sampleX / (meshRenderX - 1);
                float x = Mathf.Lerp(baseMinX, baseMaxX, t);
                float u = 0.5f + x / size.x;

                verts.Add(new Vector3(x, y, 0f));
                uvs.Add(new Vector2(u, v));

                if (sampleX > 0)
                {
                    indices.Add(lineStart + sampleX - 1);
                    indices.Add(lineStart + sampleX);
                }
            }
        }

        _mesh.Clear();
        _mesh.SetVertices(verts);
        _mesh.SetUVs(0, uvs);
        _mesh.SetIndices(indices, MeshTopology.Lines, 0, false);

        // The vertex shader can move overscan vertices farther than their CPU
        // positions. Use explicit generous bounds so Unity does not cull the
        // transition while the presentation scale is above 1.
        float baseHalfX = Mathf.Max(
            meshHalfStepsX * resolvedGridScale,
            half.x / minScale);
        float baseHalfY = Mathf.Max(
            meshHalfStepsY * resolvedGridScale,
            half.y / minScale);
        float displacementPadding = Mathf.Max(size.x, size.y);

        float cullHalfX =
            baseHalfX * maximumPresentationScale + displacementPadding;
        float cullHalfY =
            baseHalfY * maximumPresentationScale + displacementPadding;

        _mesh.bounds = new Bounds(
            Vector3.zero,
            new Vector3(
                cullHalfX * 2f,
                cullHalfY * 2f,
                displacementPadding * 2f));

        _mf ??= GetComponent<MeshFilter>();
        _mf.sharedMesh = _mesh;
    }

    void BuildBorderStripMesh()
    {
        if (_borderMesh == null)
        {
            _borderMesh = new Mesh
            {
                name = "VectorGrid BorderStrip",
                indexFormat = IndexFormat.UInt32,
                hideFlags = RuntimeMeshHideFlags
            };
            _borderMesh.MarkDynamic();
        }

        var verts = new List<Vector3>();
        var uv0 = new List<Vector2>();
        var uv1 = new List<Vector2>();
        var uv2 = new List<Vector2>();
        var idx = new List<int>();

        void AppendEdge(bool horizontal, bool atMax, int count, ref int baseV)
        {
            float du = horizontal ? (count > 1 ? 1f / (count - 1) : 0f) : 0f;
            float dv = horizontal ? 0f : (count > 1 ? 1f / (count - 1) : 0f);

            for (int i = 0; i < count; i++)
            {
                float fx = horizontal
                    ? (float)i / Mathf.Max(1, count - 1)
                    : (atMax ? 1f : 0f);

                float fy = horizontal
                    ? (atMax ? 1f : 0f)
                    : (float)i / Mathf.Max(1, count - 1);

                for (int s = -1; s <= 1; s += 2)
                {
                    verts.Add(Vector3.zero);
                    uv0.Add(new Vector2(fx, fy));
                    uv1.Add(new Vector2(du, dv));
                    uv2.Add(new Vector2(s, 0f));
                }
            }

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
        AppendEdge(horizontal: true, atMax: false, count: renderX, ref v);
        AppendEdge(horizontal: true, atMax: true, count: renderX, ref v);
        AppendEdge(horizontal: false, atMax: false, count: renderY, ref v);
        AppendEdge(horizontal: false, atMax: true, count: renderY, ref v);

        _borderMesh.Clear();
        _borderMesh.SetVertices(verts);
        _borderMesh.SetUVs(0, uv0);
        _borderMesh.SetUVs(1, uv1);
        _borderMesh.SetUVs(2, uv2);
        _borderMesh.SetIndices(idx, MeshTopology.Triangles, 0, true);
        _borderMesh.RecalculateBounds();
    }

    void AllocateBuffers()
    {
        int count = gridX * gridY;
        ReleaseBuffers();

        _posBuf = new ComputeBuffer(count, Marshal.SizeOf<Vector3>(), ComputeBufferType.Structured);
        _velBuf = new ComputeBuffer(count, Marshal.SizeOf<Vector3>(), ComputeBufferType.Structured);
        _origBuf = new ComputeBuffer(count, Marshal.SizeOf<Vector3>(), ComputeBufferType.Structured);

        _forceBuf = new ComputeBuffer(1, Marshal.SizeOf<Force>(), ComputeBufferType.Structured);
        _forceCapacity = 1;

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
            ApplyBoundaryUniforms();
        }
    }

    void UploadStaticData()
    {
        if (_origBuf == null || _posBuf == null || _velBuf == null)
            return;

        int count = gridX * gridY;
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
        _velBuf.SetData(sim);
    }

    void EnsureForceCapacity(int want)
    {
        if (want <= _forceCapacity)
            return;

        _forceCapacity = Mathf.NextPowerOfTwo(Mathf.Max(1, want));
        _forceBuf?.Release();
        _forceBuf = new ComputeBuffer(
            _forceCapacity,
            Marshal.SizeOf<Force>(),
            ComputeBufferType.Structured);

        if (vectorCompute != null && _kernel >= 0)
            vectorCompute.SetBuffer(_kernel, _ForcesID, _forceBuf);
    }

    void ReleaseBuffers()
    {
        _posBuf?.Release();
        _posBuf = null;

        _velBuf?.Release();
        _velBuf = null;

        _origBuf?.Release();
        _origBuf = null;

        _forceBuf?.Release();
        _forceBuf = null;
        _forceCapacity = 0;
    }

    public static Force MakeRadial(
        Vector3 pos,
        float radius,
        float strength,
        float innerFrac = 0f)
    {
        return new Force
        {
            position = pos,
            radius = Mathf.Max(0.001f, radius),
            direction = Vector3.zero,
            strength = strength,
            flags = 0u,
            innerFrac = Mathf.Clamp(innerFrac, 0f, 0.9f),
            color = Vector4.zero
        };
    }

    public static Force MakeDirectional(
        Vector3 pos,
        float radius,
        Vector3 dir,
        float strength,
        float innerFrac = 0f)
    {
        return new Force
        {
            position = pos,
            radius = Mathf.Max(0.001f, radius),
            direction = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.right,
            strength = strength,
            flags = 1u,
            innerFrac = Mathf.Clamp(innerFrac, 0f, 0.9f),
            color = Vector4.zero
        };
    }
}
