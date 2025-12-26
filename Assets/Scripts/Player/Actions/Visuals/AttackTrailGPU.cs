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

    [Header("External Activation (e.g., Time Dilation)")]
    [SerializeField] private bool allowExternalActivation = true;

    [Tooltip("If true, always treat external as 'moving' (emits even while standing).")]
    [SerializeField] private bool externalAlwaysFull = false;

    [Tooltip("Minimum planar speed required before external trail starts emitting.")]
    [SerializeField] private float externalMinSpeed = 0.25f;

    [Tooltip("Planar speed that maps to external StageT = 1.")]
    [SerializeField] private float externalSpeedForFull = 6.0f;

    [Tooltip("Prefer Rigidbody velocity over input vector for external direction.")]
    [SerializeField] private bool externalUseRBVelocity = true;

    [Header("External Ghost Trail (Time Dilation)")]
    [Tooltip("If true, external activation emits ONLY a ghost trail behind the player (no forward 'sword').")]
    [SerializeField] private bool externalGhostTrailOnly = true;

    [Tooltip("Ghost trail length (world units) while externally active.")]
    [SerializeField] private float ghostTrailLength = 1.8f;

    [Tooltip("Ghost trail particle radius (no taper).")]
    [SerializeField] private float ghostWidth = 0.28f;

    [Tooltip("Forward speed used for ghost particles (moves them behind the player).")]
    [SerializeField] private float ghostForwardSpeed = 6.0f;

    [Tooltip("Emission rate while ghost trail is active.")]
    [SerializeField] private float ghostEmissionRate = 140f;

    [Tooltip("Lifetime range while ghost trail is active.")]
    [SerializeField] private float ghostMinLifetime = 0.10f;
    [SerializeField] private float ghostMaxLifetime = 0.22f;

    [Tooltip("Keep StageT small so we don’t draw a long spear segment. Must be >= 0.02 to emit.")]
    [SerializeField] private float ghostStageT = 0.22f;

    private bool _externalActive = false;

    public void SetExternalActive(bool active)
    {
        _externalActive = active;
    }

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

        if (!attackController) attackController = GetComponentInParent<PlayerAttackController>();
        if (!playerController) playerController = GetComponentInParent<PlayerControllerScript>();
        if (!controller)       controller       = GetComponentInParent<PlayerVisualController>();

        if (!sim)
            sim = Resources.Load<ComputeShader>("AttackTrail"); // optional fallback

        EnsureKernel();
        InitBuffersIfNeeded();

        if (attackController != null)
        {
            attackController.OnStageStarted.AddListener(OnStageStarted);
            attackController.OnStageCompleted.AddListener(OnStageCompleted);
        }

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

        // --- determine origin ---
        Vector3 origin = GetTrailOriginWS();

        bool attackActive = (attackController != null && attackController.IsAttacking && attackController.CurrentStage != null);
        bool externalActive = allowExternalActivation && _externalActive;

        // --- external motion (Time Dilation) ---
        Vector3 extVel = Vector3.zero;
        float extT = 0f;

        if (externalActive && !attackActive)
        {
            if (externalUseRBVelocity && playerController != null)
            {
                var rb = playerController.GetComponent<Rigidbody>();
                if (rb != null) extVel = rb.linearVelocity;
            }

            if (extVel.sqrMagnitude < 0.0001f && playerController != null)
                extVel = playerController.movement;

            extVel.y = 0f;

            float spd = extVel.magnitude;
            if (externalAlwaysFull)
            {
                extT = 1f;
            }
            else if (spd >= externalMinSpeed)
            {
                extT = Mathf.Clamp01(spd / Mathf.Max(0.01f, externalSpeedForFull));
            }
        }

        // --- choose forward direction (attack > external > visual forward) ---
        Vector3 fwdWS;

        if (attackActive)
        {
            fwdWS = attackController.CurrentAttackDirectionWS;
        }
        else if (extT > 0.001f && extVel.sqrMagnitude > 0.0001f)
        {
            fwdWS = extVel.normalized;
        }
        else if (controller && controller.visuals)
        {
            fwdWS = controller.visuals.right; // local +X is blob front in this project
        }
        else
        {
            fwdWS = transform.right;
        }

        // Ensure flat XZ and normalized
        fwdWS.y = 0f;
        if (fwdWS.sqrMagnitude < 0.0001f) fwdWS = Vector3.right;
        fwdWS.Normalize();

        // Stage time 0..1 (attack uses real stage time; external uses extT)
        float stageT = attackActive ? attackController.StageNormalizedTime : extT;

        // Defaults (attack look)
        float useTrailLength  = trailLength;
        float useBaseWidth    = baseWidth;
        float useTipWidth     = tipWidth;
        float useForwardSpeed = forwardSpeed;
        float useEmitRate     = emissionRate;
        float useMinLife      = minLifetime;
        float useMaxLife      = maxLifetime;
        float useStageDuration = _currentStageDuration;

        // External ghost-only mode: behind-player trail, no forward “sword”
        bool ghostMode = externalActive && externalGhostTrailOnly && !attackActive;
        if (ghostMode)
        {
            bool movingEnough = externalAlwaysFull || extT > 0.001f;

            if (!movingEnough)
            {
                stageT = 0f; // no emission while standing still
            }
            else
            {
                // Keep StageT small so we don’t draw a long spear segment.
                stageT = Mathf.Max(0.02f, ghostStageT);
            }

            // Emit behind the player instead of forward
            fwdWS = -fwdWS;

            // Remove taper so it doesn’t read as a weapon tip
            useTrailLength  = ghostTrailLength;
            useBaseWidth    = ghostWidth;
            useTipWidth     = ghostWidth;

            useForwardSpeed = ghostForwardSpeed;
            useEmitRate     = ghostEmissionRate;
            useMinLife      = ghostMinLifetime;
            useMaxLife      = ghostMaxLifetime;

            useStageDuration = 1f;
        }
        else if (!attackActive)
        {
            // External non-ghost mode: use a sane duration
            useStageDuration = 1f;
        }

        // Emit only if we have meaningful stageT (compute won't spawn otherwise)
        bool stageActiveForEmit = (stageT >= 0.02f);

        // Emit at a constant rate during the stage/external
        _emitAccumulator += (stageActiveForEmit ? useEmitRate : 0f) * dt;
        int emitCount = Mathf.FloorToInt(_emitAccumulator);
        _emitAccumulator -= emitCount;
        emitCount = Mathf.Clamp(emitCount, 0, particleCount);

        // Set compute params
        sim.SetInt("_ParticleCount", particleCount);
        sim.SetInt("_EmitCount", emitCount);
        sim.SetInt("_StageActive", stageActiveForEmit ? 1 : 0);
        sim.SetFloat("_Dt", dt);
        sim.SetVector("_AttackOriginWS", origin);
        sim.SetVector("_AttackDirWS", fwdWS);
        sim.SetFloat("_TrailLength", useTrailLength);
        sim.SetFloat("_BaseWidth", useBaseWidth);
        sim.SetFloat("_TipWidth", useTipWidth);
        sim.SetFloat("_ForwardSpeed", useForwardSpeed);
        sim.SetFloat("_TrailDrag", trailDrag);
        sim.SetFloat("_MinLife", useMinLife);
        sim.SetFloat("_MaxLife", useMaxLife);
        sim.SetFloat("_SizeStart", sizeStart);
        sim.SetFloat("_SizeEnd", sizeEnd);
        sim.SetFloat("_StageT", stageT);
        sim.SetFloat("_StageDuration", useStageDuration);

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
        _drawBounds.center = origin;
        _drawBounds.extents = Vector3.one * (useTrailLength + 5f);
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
        _isStageActive = true;
        _currentStageType = stage.StageType;
        _currentStageDuration = stage.Duration; // tie to profile
    }

    void OnStageCompleted(AttackStage stage)
    {
        _isStageActive = false;
    }
}
