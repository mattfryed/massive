using UnityEngine;
using UnityEngine.Rendering;
using Massive.Player; // for PlayerAttackController / AttackStage

[DisallowMultipleComponent]
public class AttackTrailGPU : MonoBehaviour
{
    MaterialPropertyBlock _mpb;

    [Header("References")]
    [SerializeField] PlayerAttackController attackController;
    [SerializeField] PlayerControllerScript playerController;
    [SerializeField] PlayerVisualController controller;   // for visuals position/orientation
    [Tooltip("Compute shader for attack trail (AttackTrail.compute)")]
    public ComputeShader sim;                             // assign in Inspector or via Resources

    [Header("Sim Params")]
    [Tooltip("Max particle count for the trail.")]
    public int particleCount = 128;

    [Header("Rendering")]
    public Material trailMat;   // MASSIVE/AttackTrailInstanced
    public Mesh quadMesh;       // 4-vert quad (auto-filled if empty)

    [Header("Trail Shape")]
    public float trailLength = 4.0f;     // world units at full extension
    public float baseWidth   = 0.7f;     // radius near player
    public float tipWidth    = 0.25f;    // radius at tip

    [Header("Motion")]
    public float forwardSpeed = 8.0f;    // initial forward speed
    public float trailDrag    = 3.0f;    // drag factor (exp(-trailDrag * dt))

    [Header("Lifetime / Size")]
    public float minLifetime = 0.12f;
    public float maxLifetime = 0.22f;
    public float sizeStart   = 0.25f;    // radius at birth
    public float sizeEnd     = 0.0f;     // radius at death

    [Header("Emission")]
    [Tooltip("Particles per second while an attack stage is active.")]
    public float emissionRate = 220f;

    // runtime
    int _kUpdate = -1;
    ComputeBuffer _particles;
    ComputeBuffer _args;
    Bounds _drawBounds;

    bool _needRebuild;
    int  _allocatedCount = -1;

    float _emitAccumulator;
    float _currentStageDuration = 0.4f; // fallback

    bool  _isStageActive;
    AttackStageType _currentStageType;

    const int THREAD_GROUP_SIZE = 64;

    // layout must match AttackParticle in AttackTrail.compute
    struct AttackParticle
    {
        public Vector3 posWS;
        public Vector2 velXZ;
        public float   u0;
        public float   life;
        public float   maxLife;
        public float   size;
        public uint    state;
    }

    const int STRIDE =
        sizeof(float) * (3 + 2 + 1 + 1 + 1 + 1) + // posWS(3) + velXZ(2) + u0 + life + maxLife + size
        sizeof(uint);                             // state

    void OnEnable()
    {
        if (_mpb == null)
            _mpb = new MaterialPropertyBlock();
        if (!attackController) attackController = GetComponent<PlayerAttackController>();
        if (!playerController) playerController = GetComponent<PlayerControllerScript>();
        if (!controller)       controller       = GetComponentInParent<PlayerVisualController>();

        if (!sim)
            sim = Resources.Load<ComputeShader>("AttackTrail"); // optional fallback

        EnsureKernel();
        InitBuffersIfNeeded();

        attackController.OnStageStarted.AddListener(OnStageStarted);
        attackController.OnStageCompleted.AddListener(OnStageCompleted);

        Camera.onPreCull += HandlePreCull;
    }

    void OnDisable()
    {
        Camera.onPreCull -= HandlePreCull;

        if (attackController != null)
        {
            attackController.OnStageStarted.RemoveListener(OnStageStarted);
            attackController.OnStageCompleted.RemoveListener(OnStageCompleted);
        }

        ReleaseBuffers();
    }

    void OnDestroy()
    {
        ReleaseBuffers();
    }

    void OnValidate()
    {
        if (Application.isPlaying)
        {
            if (particleCount != _allocatedCount)
                _needRebuild = true;
        }
    }

    void EnsureKernel()
    {
        if (!sim)
        {
            _kUpdate = -1;
            return;
        }
        if (_kUpdate >= 0) return;

        try
        {
            _kUpdate = sim.FindKernel("CSMain"); // must match #pragma kernel CSMain in AttackTrail.compute
        }
        catch
        {
            _kUpdate = -1;
        }

        if (_kUpdate < 0)
        {
            Debug.LogError("AttackTrailGPU: compute kernel 'CSMain' not found on " + sim.name, this);
        }
    }

    void InitBuffersIfNeeded()
    {
        if (_particles != null && _args != null && _allocatedCount == particleCount && !_needRebuild)
            return;

        ReleaseBuffers();

        if (particleCount <= 0) particleCount = 1;

        _particles = new ComputeBuffer(particleCount, STRIDE, ComputeBufferType.Structured);

        if (!quadMesh)
            quadMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

        _args = new ComputeBuffer(1, sizeof(uint) * 5, ComputeBufferType.IndirectArguments);
        uint indexCount = quadMesh ? quadMesh.GetIndexCount(0) : 6u;
        _args.SetData(new uint[] { indexCount, (uint)particleCount, 0, 0, 0 });

        // initialize particles as "dead"
        AttackParticle[] initData = new AttackParticle[particleCount];
        Vector3 center = GetTrailOriginWS();
        for (int i = 0; i < particleCount; i++)
        {
            initData[i].posWS   = center;
            initData[i].velXZ   = Vector2.zero;
            initData[i].u0      = 0f;
            initData[i].life    = 1f;
            initData[i].maxLife = 0.0001f;
            initData[i].size    = 0f;
            initData[i].state   = 0u;
        }
        _particles.SetData(initData);

        if (sim != null && _kUpdate >= 0)
        {
            sim.SetBuffer(_kUpdate, "_Particles", _particles);
        }

        _drawBounds = new Bounds(center, Vector3.one * (trailLength + 5f));

        _allocatedCount = particleCount;
        _needRebuild = false;
    }

    void ReleaseBuffers()
    {
        _particles?.Dispose();
        _particles = null;

        _args?.Dispose();
        _args = null;

        _allocatedCount = -1;
        _needRebuild = false;
    }

    Vector3 GetTrailOriginWS()
    {
        if (controller && controller.visuals) return controller.visuals.position;
        if (controller)                        return controller.transform.position;
        return transform.position;
    }

    void LateUpdate()
    {
        if (_needRebuild) InitBuffersIfNeeded();
        if (sim == null || trailMat == null || _particles == null || _args == null) return;

        EnsureKernel();
        if (_kUpdate < 0) return;

        float dt = Mathf.Max(Time.deltaTime, 1e-4f);

        // --- determine origin & direction ---
        Vector3 origin = GetTrailOriginWS();
        Vector3 fwdWS;

        // Prefer the attack controller's notion of attack direction
        if (attackController != null && attackController.IsAttacking && attackController.CurrentStage != null)
        {
            fwdWS = attackController.CurrentAttackDirectionWS;
        }
        else if (controller && controller.visuals)
        {
            // Fallback: blob facing
            fwdWS = controller.visuals.right; // local +X is blob front
        }
        else
        {
            fwdWS = transform.right; // blob front default
        }

        // Ensure flat XZ and normalized
        fwdWS.y = 0f;
        if (fwdWS.sqrMagnitude < 0.0001f) fwdWS = Vector3.right;
        fwdWS.Normalize();



        // Stage time 0..1
        float stageT = (attackController != null && attackController.IsAttacking && attackController.CurrentStage != null)
            ? attackController.StageNormalizedTime
            : 0f;

        // Emit at a constant rate during the stage; let u0-tip bias handle shape.
        _emitAccumulator += (_isStageActive ? emissionRate : 0f) * dt;
        int emitCount = Mathf.FloorToInt(_emitAccumulator);
        _emitAccumulator -= emitCount;
        emitCount = Mathf.Clamp(emitCount, 0, particleCount);

        // Set compute params
        sim.SetInt("_ParticleCount", particleCount);
        sim.SetInt("_EmitCount", emitCount);
        sim.SetInt("_StageActive", _isStageActive ? 1 : 0);
        sim.SetFloat("_Dt", dt);
        sim.SetVector("_AttackOriginWS", origin);
        sim.SetVector("_AttackDirWS", fwdWS);
        sim.SetFloat("_TrailLength", trailLength);
        sim.SetFloat("_BaseWidth", baseWidth);
        sim.SetFloat("_TipWidth", tipWidth);
        sim.SetFloat("_ForwardSpeed", forwardSpeed);
        sim.SetFloat("_TrailDrag", trailDrag);
        sim.SetFloat("_MinLife", minLifetime);
        sim.SetFloat("_MaxLife", maxLifetime);
        sim.SetFloat("_SizeStart", sizeStart);
        sim.SetFloat("_SizeEnd", sizeEnd);
        sim.SetFloat("_StageT", stageT);
        sim.SetFloat("_StageDuration", _currentStageDuration);


        // Bind buffer
        sim.SetBuffer(_kUpdate, "_Particles", _particles);


var cam = Camera.main != null ? Camera.main : Camera.current;
if (cam != null)
{
    sim.SetVector("_CamForwardWS", cam.transform.forward);
}


        // Dispatch
        int groups = Mathf.CeilToInt(particleCount / (float)THREAD_GROUP_SIZE);
        sim.Dispatch(_kUpdate, Mathf.Max(1, groups), 1, 1);

        // Update draw bounds
        _drawBounds.center  = origin;
        _drawBounds.extents = Vector3.one * (trailLength + 5f);
    }

void HandlePreCull(Camera cam)
{
    if (sim == null || trailMat == null || quadMesh == null || _args == null || _particles == null) return;

    if (!trailMat.enableInstancing) trailMat.enableInstancing = true;

    bool team2 = playerController ? playerController.teamID != 1 : false;

    // per-camera basis (same as nuggets)
    _mpb.SetVector("_CamRightWS", cam.transform.right);
    _mpb.SetVector("_CamUpWS",    cam.transform.up);
    _mpb.SetBuffer("_Particles",  _particles);

    if (!team2)
    {
        // TEAM 1: single pass, solid white
        _mpb.SetFloat("_IsTeam2", 0f);
        _mpb.SetFloat("_SizeScale", 1.0f);

        Graphics.DrawMeshInstancedIndirect(
            quadMesh, 0, trailMat, _drawBounds, _args,
            0, _mpb,
            ShadowCastingMode.Off, false, gameObject.layer, cam,
            LightProbeUsage.Off
        );
    }
    else
    {
        // TEAM 2: union outline trick (two passes)

        // PASS 1: slightly larger white silhouette (outline)
        _mpb.SetFloat("_IsTeam2", 0f);          // use Team1Color as outline color (white)
        _mpb.SetFloat("_SizeScale", 1.5f);     // inflate radius ~8% (tune this)
        Graphics.DrawMeshInstancedIndirect(
            quadMesh, 0, trailMat, _drawBounds, _args,
            0, _mpb,
            ShadowCastingMode.Off, false, gameObject.layer, cam,
            LightProbeUsage.Off
        );

        // PASS 2: normal black trail on top
        _mpb.SetFloat("_IsTeam2", 1f);          // use Team2Color (black)
        _mpb.SetFloat("_SizeScale", 1.0f);      // normal size

        Graphics.DrawMeshInstancedIndirect(
            quadMesh, 0, trailMat, _drawBounds, _args,
            0, _mpb,
            ShadowCastingMode.Off, false, gameObject.layer, cam,
            LightProbeUsage.Off
        );
    }
}

    void OnStageStarted(AttackStage stage)
    {
        _isStageActive   = true;
        _currentStageType = stage.StageType;
        _currentStageDuration = stage.Duration; // tie to profile

        // Optionally adjust trailLength / forwardSpeed per stage type
        // e.g. if (_currentStageType == AttackStageType.ComboSwipe) trailLength = shorterValue;
    }

    void OnStageCompleted(AttackStage stage)
    {
        _isStageActive = false;
    }
}
