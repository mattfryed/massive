using UnityEngine;
using UnityEngine.Rendering;


[DisallowMultipleComponent]
public class PlayerNuggetsGPU : MonoBehaviour
{

    readonly System.Collections.Generic.Dictionary<Camera, bool> _attached = new();
    
    MaterialPropertyBlock _mpb; 
    [Header("References")]
    [SerializeField] PlayerVisualController controller;     // can be on parent/sibling
    [SerializeField] PlayerControllerScript playerController;
    [Tooltip("Compute shader for nugget simulation (NuggetsSim.compute)")]
    public ComputeShader sim;                                // assign in Inspector or place in Resources/NuggetsSim

[Header("Sim Params (controlled by PlayerController)")]
[HideInInspector] public int dotCount = 256;
    public float dotRadius = 0.03f;

[Header("Nugget Count Limits")]
public int minDots = 5;
public int maxDots = 300;

    // legacy names kept for compatibility (compute sets will ignore extras safely)
    // public float wallBias = 28f;
    // public float tangential = 6f;
    // public float damping = 1.5f;
    // public float repel = 18f;

    [Header("Rendering")]
    public Material nuggetMat;     // MASSIVE/NuggetInstanced
    public Mesh quadMesh;          // 4-vert quad (auto-filled if empty)
    public Color colorTeam1 = Color.white;
    public Color colorTeam2 = Color.black;

    [Header("Distribution")]
    [Range(0f,1f)] public float centerDensity = 0.65f; // 0 = uniform, 1 = strong center bias


    [Header("Mass feel (ported from CPU)")]
    public float centerSpring = 18f;    // cohesion to seeded core
    public float radialDamping = 2.0f;  // removes outward velocity near wall
    public float tangentialBias = 0.65f;
    public float rimSoftness = 0.08f;   // inner wall thickness (world units)
    public float rimRepel = 28f;        // soft-wall strength
    public float pullAccel = 8f;        // forward bunching along motion
    public float sloshFactor = 0.05f;   // inertia projection
    public float spinAccel = 2.0f;      // gentle swirl
    public float drag = 1.8f;           // exp(-drag*dt)
    public float jitterAmp = 0.12f;     // subtle noise
    public float jitterFreq = 6.0f;
    public float bounceDamp = 0.65f;
    public float repel = 16f;   // NEW

    
    [Header("Blob Deform (fed from controller)")]
    public float baseRadius = 0.65f;
    public float outlineHalf = 0.03f;
    public float idleWobble = 0.25f;
    public float deformAmt = 0f;
    public Vector2 deformDir = Vector2.zero; // (1,0) moving, (0,0) idle
    public float dirFrontGain = 1.2f, dirBackGain = 0.6f, areaKeep = 0.85f;
    public float hitImpulse = 0f, noisePhase = 0f;
    public float stretchY = 1.0f;

    [Range(0f, 2f)]
    public float teardropK1 = 1.1f;

    [Range(0f, 2f)]
    public float teardropK2 = 0.6f;

    


    // runtime
    int _kUpdate = -1;
    ComputeBuffer _pos, _prev, _vel, _seed, _args;
    Bounds _drawBounds;
    Vector2[] _seedsCPU; // keep for initialization

Vector2[] BuildSeeds(int count, float baseR, float density)
{
    float k = Mathf.Lerp(1.0f, 0.5f, Mathf.Clamp01(density)); // 1.0 = uniform, 0.5 = more center
    var outArr = new Vector2[count];
    float golden = 2.39996323f;
    float packR  = baseR * 0.86f;
    for (int i=0;i<count;i++)
    {
        float u = (i + 0.5f) / (float)count;
        float r = packR * Mathf.Pow(u, k);
        float a = i * golden;
        outArr[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
    }
    return outArr;
}





int _allocatedCount = -1;
float _lastCenterDensity = -1f;
float _lastBaseRadius = -1f;
bool _needRebuild;
void OnEnable()  { Camera.onPreCull += HandlePreCull; }
void OnDisable() { Camera.onPreCull -= HandlePreCull; }

void HandlePreCull(Camera cam)
{
    if (sim == null || nuggetMat == null || quadMesh == null || _args == null || _pos == null) return;

    // Billboard vectors per camera
    _mpb.SetVector("_CamRightWS", cam.transform.right);
    _mpb.SetVector("_CamUpWS",    cam.transform.up);

    // Bind render data (safe to redundantly set here)
    _mpb.SetBuffer("_NuggetPos", _pos);
    _mpb.SetFloat("_DotRadius", dotRadius);

    // Base center (plus deco pre-jitter if active)
    Vector3 baseCenter = GetBlobCenterWS();
    if (controller != null)
        baseCenter += controller.DecoPreJitterWS;

    // Determine if we are in split mode
    float splitT = (controller != null) ? Mathf.Clamp01(controller.DecoSplit01) : 0f;
    bool splitActive = (controller != null && splitT > 0.001f);

    // Opacity control:
    // - When split is active, match blob fill alpha behavior: ghostOpacity * splitFillAlpha
    float alphaMul = 1f;
    if (splitActive)
        alphaMul = Mathf.Clamp01(controller.DecoGhostOpacity * controller.DecoSplitFillAlpha);

    // Team color + optional outline (your existing behavior)
    bool team2 = playerController ? playerController.teamID != 1 : false;

    Color dot = team2 ? colorTeam2 : colorTeam1;
    dot.a *= alphaMul;
    _mpb.SetColor("_Color", dot);

    _mpb.SetFloat("_DrawOutline", team2 ? 1f : 0f);
    if (team2)
    {
        Color oc = Color.white;
        oc.a *= alphaMul;
        _mpb.SetColor("_OutlineColor", oc);
        _mpb.SetFloat("_OutlineWidth", 0.02f);
    }
    else
    {
        _mpb.SetFloat("_OutlineWidth", 0f);
    }

    if (!nuggetMat.enableInstancing) nuggetMat.enableInstancing = true;

    // ---- Normal (no split) ----
    if (!splitActive)
    {
        _mpb.SetVector("_CenterWS", baseCenter);

        Graphics.DrawMeshInstancedIndirect(
            quadMesh, 0, nuggetMat, _drawBounds, _args,
            0, _mpb,
            ShadowCastingMode.Off, false, 0, cam,
            LightProbeUsage.Off
        );
        return;
    }

    // ---- Split: draw TWO copies (A/B) ----
    Vector3 side = controller.DecoSideWS;
    float halfSep = 0.5f * controller.DecoSplitSeparationWorld * splitT;
    Vector3 off = side * halfSep;

    // Ghost A
    _mpb.SetVector("_CenterWS", baseCenter + off + controller.DecoGhostJitterA_WS);
    Graphics.DrawMeshInstancedIndirect(
        quadMesh, 0, nuggetMat, _drawBounds, _args,
        0, _mpb,
        ShadowCastingMode.Off, false, 0, cam,
        LightProbeUsage.Off
    );

    // Ghost B
    _mpb.SetVector("_CenterWS", baseCenter - off + controller.DecoGhostJitterB_WS);
    Graphics.DrawMeshInstancedIndirect(
        quadMesh, 0, nuggetMat, _drawBounds, _args,
        0, _mpb,
        ShadowCastingMode.Off, false, 0, cam,
        LightProbeUsage.Off
    );
}


    void OnValidate()
    {
        if (!controller) controller = GetComponentInParent<PlayerVisualController>();
        if (!playerController) playerController = GetComponentInParent<PlayerControllerScript>();

    // Always clamp
    dotCount = Mathf.Clamp(dotCount, minDots, maxDots);

        if (Application.isPlaying)
        {
            // if (dotCount != _allocatedCount 
            // || !Mathf.Approximately(centerDensity, _lastCenterDensity)
            // || !Mathf.Approximately(baseRadius,    _lastBaseRadius))
                _needRebuild = true;
        }
    }


    void Awake()
    {    
        _mpb = new MaterialPropertyBlock();
        if (!controller)       controller       = GetComponentInParent<PlayerVisualController>();
        if (!playerController) playerController = GetComponentInParent<PlayerControllerScript>();

        if (!sim)
            sim = Resources.Load<ComputeShader>("NuggetsSim");

        if (!sim)
        {
            Debug.LogError("PlayerNuggetsGPU: assign NuggetsSim.compute (Inspector) or place it in Resources as 'NuggetsSim'.", this);
            enabled = false;
            return;
        }

        EnsureKernel(); // sets _kUpdate or logs

        // buffers
        _pos  = new ComputeBuffer(dotCount, sizeof(float) * 3);
        _prev = new ComputeBuffer(dotCount, sizeof(float) * 3);
        _vel  = new ComputeBuffer(dotCount, sizeof(float) * 2);
        _seed = new ComputeBuffer(dotCount, sizeof(float) * 2);

        // seeds: vogel spiral inside the blob
        _seedsCPU = new Vector2[dotCount];
        float golden = 2.39996323f;
        float packR  = baseRadius * 0.86f; // slightly smaller than blob
        for (int i = 0; i < dotCount; i++)
        {
            float r = Mathf.Sqrt((i + 0.5f) / (float)dotCount) * packR;
            float a = i * golden;
            _seedsCPU[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }
        
        _seed.SetData(_seedsCPU);

        // initialize LOCAL positions (no center added)
        var P = new Vector3[dotCount];
        var V = new Vector2[dotCount];
        for (int i = 0; i < dotCount; i++)
        {
            var s = _seedsCPU[i];
            P[i] = new Vector3(s.x, 0, s.y);
            V[i] = Vector2.zero;
        }
        _pos.SetData(P);
        _prev.SetData(P);
        _vel.SetData(V);

        if (!quadMesh)
            quadMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx"); // built-in Unity quad

        // indirect args: indexCountPerInstance, instanceCount, startIndex, baseVertex, startInstance
        _args = new ComputeBuffer(1, sizeof(uint) * 5, ComputeBufferType.IndirectArguments);
        uint indexCount = quadMesh ? quadMesh.GetIndexCount(0) : 6u;
        _args.SetData(new uint[] { indexCount, (uint)dotCount, 0, 0, 0 });

        _drawBounds = new Bounds(GetBlobCenterWS(), Vector3.one * 20f);
    }

    void OnDestroy()
    {
        _pos?.Dispose();  _pos  = null;
        _prev?.Dispose(); _prev = null;
        _vel?.Dispose();  _vel  = null;
        _seed?.Dispose(); _seed = null;
        _args?.Dispose(); _args = null;
    }

    void EnsureKernel()
    {
        if (sim == null) { _kUpdate = -1; return; }
        if (_kUpdate >= 0) return;

        try
        {
            _kUpdate = sim.FindKernel("CSUpdate"); // must match: #pragma kernel CSUpdate
        }
        catch
        {
            _kUpdate = -1;
        }

        if (_kUpdate < 0)
        {
            Debug.LogError("PlayerNuggetsGPU: compute kernel 'CSUpdate' not found on " + sim.name, this);
        }
    }

    Vector3 GetBlobCenterWS()
    {
        if (controller && controller.visuals) return controller.visuals.position;
        if (controller)                        return controller.transform.position;
        return transform.position;
    }

    void LateUpdate()
    {
        if (_needRebuild) RebuildSeedsAndBuffers();
        if (sim == null || nuggetMat == null) return;

        // Re-acquire kernel if domain reload / asset reimport happened
        if (_kUpdate < 0) EnsureKernel();
        if (_kUpdate < 0) return; // still invalid; bail safely

        // swap pos/prev (local offsets)
        var tmp = _prev; _prev = _pos; _pos = tmp;

        Vector3 center = GetBlobCenterWS();

        // --- Player planar velocity (for slosh/bunching) ---
        Vector3 v3;
if (playerController && playerController.TryGetComponent(out Rigidbody rb))
    v3 = rb.linearVelocity;
else if (controller && controller.TryGetComponent(out Rigidbody rb2))
    v3 = rb2.linearVelocity;
        else
            v3 = Vector3.zero;

        // --- Compute shader parameters (deform + world) ---
        sim.SetInt("_DotCount", dotCount);
        sim.SetFloat("_BaseRadius",  baseRadius);
        sim.SetFloat("_OutlineHalf", outlineHalf);
        sim.SetFloat("_IdleWobble",  idleWobble);
        sim.SetFloat("_DeformAmt",   deformAmt);
        sim.SetFloats("_DeformDir",  deformDir.x, deformDir.y);
        sim.SetFloat("_TeardropK1",  teardropK1);
        sim.SetFloat("_TeardropK2",  teardropK2);

        sim.SetFloat("_DirFrontGain", dirFrontGain);
        sim.SetFloat("_DirBackGain",  dirBackGain);
        sim.SetFloat("_AreaKeep",     areaKeep);
        sim.SetFloat("_HitImpulse",   hitImpulse);
        sim.SetFloat("_NoisePhase",   noisePhase);
        sim.SetFloat("_StretchY",     stretchY);
        sim.SetFloat("_DotRadius",    dotRadius);
        sim.SetFloat("_Dt",           Mathf.Max(Time.deltaTime, 1e-4f));
        sim.SetFloats("_VelWS",       v3.x, v3.z);

        // Mesh feel
        sim.SetFloat("_CenterSpring",  centerSpring);
        sim.SetFloat("_SloshFactor",   sloshFactor);
        sim.SetFloat("_JitterAmp",     jitterAmp);
        sim.SetFloat("_JitterFreq",    jitterFreq);
        sim.SetFloat("_PullAccel",     pullAccel);
        sim.SetFloat("_SpinAccel",     spinAccel);
        sim.SetFloat("_RimSoftness",   rimSoftness);
        sim.SetFloat("_RimRepel",      rimRepel);
        sim.SetFloat("_RadialDamping", radialDamping);
        sim.SetFloat("_Drag",          drag);
        sim.SetFloat("_TangentialBias", tangentialBias);
        sim.SetFloat("_BounceDamp",     bounceDamp);
        sim.SetFloat("_Repel",         repel);

        // Legacy params (harmless if kernel ignores them)
        // sim.SetFloat("_WallBias",      wallBias);
        // sim.SetFloat("_Tangential",    tangential);
        // sim.SetFloat("_Damp",          damping);

        // Bind buffers AFTER swaps
        sim.SetBuffer(_kUpdate, "_PrevPos", _prev);
        sim.SetBuffer(_kUpdate, "_Pos",     _pos);
        sim.SetBuffer(_kUpdate, "_Vel",     _vel);
        sim.SetBuffer(_kUpdate, "_Seed",    _seed);

        // Dispatch
        if (dotCount <= 0) return;

        int groups = Mathf.CeilToInt(dotCount / 128f);
        sim.Dispatch(_kUpdate, Mathf.Max(1, groups), 1, 1);

        // --- Render setup (always-on-top dots) ---
        bool team2 = playerController ? playerController.teamID != 1 : false;
        _mpb.SetColor("_Color", team2 ? colorTeam2 : colorTeam1);
        // choose per-team outline
        _mpb.SetFloat("_DrawOutline", team2 ? 1f : 0f);
        if (team2)
        {
            _mpb.SetColor("_OutlineColor", Color.white); // or expose a field
            _mpb.SetFloat("_OutlineWidth", team2 ? 0.02f : 0f);       // tweak to taste
        }
        else
        {
            // make sure outline is effectively off for Team 1
            _mpb.SetFloat("_OutlineWidth", 0f);
        }
        _mpb.SetBuffer("_NuggetPos", _pos);
        _mpb.SetFloat("_DotRadius", dotRadius);

        _drawBounds.center  = center;
        _drawBounds.extents = Vector3.one * 20f;

        var cam = Camera.main != null ? Camera.main : Camera.current;
        if (cam != null)
        {
            _mpb.SetVector("_CamRightWS", cam.transform.right);
            _mpb.SetVector("_CamUpWS",    cam.transform.up);
            _mpb.SetVector("_CenterWS",   center); // shader adds center to local offsets
        }
        if (!nuggetMat.enableInstancing) nuggetMat.enableInstancing = true;
        
        
        
        

    }

        // call this whenever dotCount changes (OnValidate + play)
    void RebuildSeedsAndBuffers()
    {

        // Clamp and skip if zero
        dotCount = Mathf.Clamp(dotCount, minDots, maxDots);
        if (dotCount <= 0) return;

        // dispose old
        _pos?.Dispose(); _prev?.Dispose(); _vel?.Dispose(); _seed?.Dispose(); _args?.Dispose();

        

        // buffers
        _pos = new ComputeBuffer(dotCount, sizeof(float) * 3);
        _prev = new ComputeBuffer(dotCount, sizeof(float) * 3);
        _vel = new ComputeBuffer(dotCount, sizeof(float) * 2);
        _seed = new ComputeBuffer(dotCount, sizeof(float) * 2);

        // seeds (see center-biased distribution in §2)
        _seedsCPU = BuildSeeds(dotCount, baseRadius, centerDensity);   // <-- new (below)
        _seed.SetData(_seedsCPU);

        // init LOCAL positions
        var P = new Vector3[dotCount];
        var V = new Vector2[dotCount];
        for (int i = 0; i < dotCount; i++) { var s = _seedsCPU[i]; P[i] = new Vector3(s.x, 0, s.y); V[i] = Vector2.zero; }
        _pos.SetData(P); _prev.SetData(P); _vel.SetData(V);

        if (!quadMesh) quadMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

        _args = new ComputeBuffer(1, sizeof(uint) * 5, ComputeBufferType.IndirectArguments);
        uint indexCount = quadMesh ? quadMesh.GetIndexCount(0) : 6u;
        _args.SetData(new uint[] { indexCount, (uint)dotCount, 0, 0, 0 });

        _allocatedCount   = dotCount;
        _lastCenterDensity = centerDensity;
        _lastBaseRadius    = baseRadius;
        _needRebuild = false;
    }


    // controller → GPU feed (you already call this from PlayerVisualController)

    public void SetDotCount(int newCount)
{
    // Clamp and avoid zero-length buffers
    newCount = Mathf.Clamp(newCount, minDots, maxDots);

    if (newCount == dotCount) return;

    dotCount = newCount;
    _needRebuild = true; // triggers RebuildSeedsAndBuffers in LateUpdate
}
public void FeedFromController(
    float _idle, float _deform, Vector2 _dir,
    float _front, float _back, float _keep,
    float _hit, float _noise,
    float _baseR, float _outline, float _stretch,
    float _k1, float _k2)
{
    idleWobble   = _idle;
    deformAmt    = _deform;     // interpreted as teardrop strength 0..1 in the compute
    deformDir    = _dir;

    dirFrontGain = _front;
    dirBackGain  = _back;
    areaKeep     = _keep;

    hitImpulse   = _hit;
    noisePhase   = _noise;

    baseRadius   = _baseR;
    outlineHalf  = _outline;
    stretchY     = _stretch;

    teardropK1   = _k1;
    teardropK2   = _k2;
}


    // convenience for old calls
    public void SetDotColor(Color c)
    {
        if (nuggetMat) _mpb.SetColor("_Color", c);
    }
}
