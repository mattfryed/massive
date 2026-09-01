using System.Collections.Generic;
using UnityEngine;
using Rigidbody = UnityEngine.Rigidbody;
using UnityEngine.Rendering;  // built-in pipeline CommandBuffer API

[DisallowMultipleComponent]
public class PlayerVisualController : MonoBehaviour
{
    // ===== Rendering / materials =====
    static readonly int _FillColor = Shader.PropertyToID("_FillColor");
    static readonly int _OutlineColor = Shader.PropertyToID("_OutlineColor");
    Material _blobMatInstance;

    [Header("Rendering")]
    public bool vectorBlobMode = true;
    public Transform visuals;          // "Visuals" child that follows player & gets yaw
    public Material blobMat;           // MASSIVE/PlayerBlobVector

[Header("Gameplay Facing Root (for hitboxes)")]
public Transform gameplayFacing;            // Assign CombatFacingRoot here
public bool gameplayFacingInstant = true;
[Range(0.01f, 0.3f)] public float gameplayFacingSmoothTime = 0.04f;
public float gameplayFacingMaxYawSpeed = 2500f;



    // ===== Attacks =====
    [Header("Attacks")]
    public Massive.Player.PlayerAttackController attackController;
    [Range(0f, 1f)] public float attackEffortScale = 0.08f;

    // ===== Blob shape =====
    [Header("Blob Shape")]
    public float baseRadius = 0.5f;
    public float outlineHalf = 0.03f;

    [Header("Wobble & Reactions")]
    [Range(0, 1)] public float idleWobble = 0.25f;  // symmetric idle scallop (no sideways drift)
    public float maxWobble = 0.12f;                 // velocity wobble cap
    public float hitImpulseDecay = 1.0f;

    [Header("Directional Stretch")]
    [Range(0f, 2f)] public float dirFrontGain = 1.0f;
    [Range(0f, 2f)] public float dirBackGain = 0.5f;
    [Range(0f, 1f)] public float areaKeep = 0.85f;  // cancels average growth

    float _deformLag; // smoothed/lagged deformation for the tail

    [Header("Tail Lag")]
    [Range(0f, 80f)] public float tailLagSpeed = 6f;   // how quickly tail catches up
    [Range(0f, 1f)]  public float tailLagWeight = 1f;  // how much lag influences the back

    [Header("Teardrop / Lunge Shape")]
    [Tooltip("Speed (units/sec) at which movement alone gives full teardrop.")]
    public float teardropSpeedForMax = 4f;

    [Range(0f, 1.5f)]
    public float teardropMoveWeight = 0.8f;   // how much normal movement contributes

    [Range(0f, 1.5f)]
    public float teardropAttackWeight = 1.0f; // how much the lunge stage contributes

    [Range(0f, 0.5f)]
    public float idleTeardrop = 0.04f;        // tiny always-on bias so motion is visible

    [Range(0f, 0.7f)]
    public float maxVerticalSquash = 0.45f;   // 0.35 → at max lunge height is 65% of idle

    [Range(0f, 2f)] public float teardropK1 = 1.1f;   // front/back bulge
    [Range(0f, 2f)] public float teardropK2 = 0.6f;   // side pinch

    [Header("Team Colors (auto from PlayerControllerScript.teamID)")]
    public Color team1_Fill = Color.black;
    public Color team1_Outline = Color.white;
    public Color team2_Fill = Color.white;
    public Color team2_Outline = Color.black;

    [Header("Nuggets (GPU-based)")]
    public PlayerNuggetsGPU nuggetsGPU;
    public Color nuggetsTeam1 = Color.white;
    public Color nuggetsTeam2 = Color.black;

    [Header("Arc")]
    public PlayerArc arc;
    public Material arcMatTeam1;
    public Material arcMatTeam2;

    [Header("Heading / Rotation Control")]
    [Range(0.01f, 0.3f)] public float yawSmoothTime = 0.07f;
    public float maxYawSpeed = 900f;

    [Header("Input/Movement (fallback if no RB)")]
    public bool invertY = true;

    [Tooltip("Deadzone applied to stick before we consider it meaningful.")]
    public float inputDeadzone = 0.12f;

    [Header("Aim Stability")]
    [Tooltip("Reject sudden 1-frame aim spikes that flip direction. -0.2 allows up to ~101 degrees flip; closer to 1 is stricter.")]
    [Range(-1f, 1f)] public float aimFlipRejectDot = -0.2f;

    public Vector2 velocityWS;

    // ===== Ghost trail (lunge only) =====
    const int   GhostStepsTotal   = 4;
    const int   GhostStepsVisible = 5;
    const float GhostMaxAlpha     = 0.7f;
    const float GhostLifetime     = 0.25f;

    struct BlobGhost
    {
        public Vector3 position;
        public Quaternion rotation;
        public float teardrop;
        public float spawnTime;
    }

    readonly List<BlobGhost> _ghosts = new List<BlobGhost>(GhostStepsTotal);
    float _lastGhostSampleT = 0f;
    bool _wasLungeActive = false;

    // ===== External charge jitter (power-ups) =====
    [Header("External Modifiers (Power-ups)")]
    [SerializeField] private float chargeJitterEaseUp = 18f;
    [SerializeField] private float chargeJitterEaseDown = 10f;

    [SerializeField] private float chargeTurnDampEaseUp = 18f;
    [SerializeField] private float chargeTurnDampEaseDown = 12f;

    [SerializeField] private float chargingYawSmoothMultiplier = 2.6f; // higher = slower turn response
    [SerializeField] private float chargingMaxYawSpeedMultiplier = 0.45f; // lower = capped turn speed

    [Header("Decoherence Split")]
    [SerializeField] private float decoSplitSeparationWorld = 1.05f;
    [SerializeField, Range(0f, 1f)] private float decoSplitFillAlpha = 0.20f;
    [SerializeField, Range(0f, 1f)] private float decoSplitOutlineAlpha = 0.90f;
    [SerializeField] private float decoSplitFadeSeconds = 0.07f;

    [Header("Decoherence Split - Jitter + Opacity")]
    [SerializeField, Min(0f)] private float decoPreSplitJitterSeconds = 0.10f;   // time to vibrate before separation starts
    [SerializeField, Min(0f)] private float decoPreSplitJitterAmp = 0.06f;       // world units
    [SerializeField, Min(0f)] private float decoPreSplitJitterFreq = 48f;        // Hz-ish

    [SerializeField, Min(0f)] private float decoHoldJitterAmp = 0.03f;           // world units
    [SerializeField, Min(0f)] private float decoHoldJitterFreq = 26f;

    [SerializeField, Range(0f, 1f)] private float decoGhostOpacity = 0.80f;      // master opacity slider (blob + nuggets ghosts)

    // --- Decoherence read-only access (for nuggets + other render helpers) ---
    public float  DecoSplit01                => _decoSplit01;               // 0..1
    public Vector3 DecoSideWS                => _decoSideWS;                // normalized
    public Vector3 DecoPreJitterWS           => _decoPreJitterWS;
    public Vector3 DecoGhostJitterA_WS       => _decoGhostJitterA_WS;
    public Vector3 DecoGhostJitterB_WS       => _decoGhostJitterB_WS;
    public float  DecoSplitSeparationWorld   => decoSplitSeparationWorld;
    public float  DecoGhostOpacity           => decoGhostOpacity;
    public float  DecoSplitFillAlpha         => decoSplitFillAlpha;

    private float _decoSplitTarget01 = 0f;
    private float _decoSplit01 = 0f;

    private MaterialPropertyBlock _tmpProps;

    private float _turnDampTarget01 = 0f;  // 0..1
    private float _turnDamp01 = 0f;

    public void SetExternalTurnDamp01(float t01)
    {
        _turnDampTarget01 = Mathf.Clamp01(t01);
    }


    private float _chargeJitterTarget01 = 0f;
    private float _chargeJitter01 = 0f;

    public void SetExternalChargeJitter01(float j01)
    {
        _chargeJitterTarget01 = Mathf.Clamp01(j01);
    }

    private bool _decoRequestedActive = false;
    private float _decoRequestedStartTime = -999f;

    private float _decoSeed0, _decoSeed1, _decoSeed2;
    private Vector3 _decoPreJitterWS;
    private Vector3 _decoGhostJitterA_WS;
    private Vector3 _decoGhostJitterB_WS;
    private Vector3 _decoSideWS = Vector3.forward;


    // ===== Hit/contact state =====
    float _hitAngle;     // radians
    float _hitTime;      // seconds since last hit (we keep counting up)
    float _hit;          // 0..1 impulse
    float _noise;        // phase

    float _contactStrength; // 0..1
    float _contactAngle;    // radians
    float _contactDecay = 5f;

    // ===== Shared teardrop strength (blob + nuggets) =====
    float _teardropStrength;
    float _teardropStrengthSmoothed;

    // ===== Input state =====
    Vector2 _input;
    Vector2 _aimDir = Vector2.right; // last meaningful stick direction

Vector2 _gameplayFacing = Vector2.right;  // unfiltered direction for combat-facing
float _gameplayYawDeg;
float _gameplayYawVelDeg;


    // ===== Yaw state =====
    float _yawDeg;
    float _yawVelDeg;

    // ===== Refs =====
    Rigidbody _rb;
    PlayerControllerScript _pcs;

    // ===== Per-camera command buffers =====
    readonly Dictionary<Camera, CommandBuffer> _perCamCB = new();
    MaterialPropertyBlock _ghostProps;
    MaterialPropertyBlock _mainProps;

    public void SetDecoherenceSplitActive(bool active, float separationWorld = -1f)
    {
        if (active && !_decoRequestedActive)
            _decoRequestedStartTime = Time.time;

        _decoRequestedActive = active;

        if (separationWorld > 0f)
            decoSplitSeparationWorld = separationWorld;

        // collapse target when turning off (the Update() will drive it down)
        if (!active)
            _decoSplitTarget01 = 0f;
    }

    void Awake()
    {
        TryGetComponent(out _rb);
        TryGetComponent(out _pcs);
        

        // Attack controller is often on the same GO or parent
        if (!attackController)
            attackController = GetComponentInParent<Massive.Player.PlayerAttackController>();

        // Find visuals child if not assigned
        if (!visuals)
        {
            var t = transform.Find("Visuals");
            if (t) visuals = t;
        }
        if (!gameplayFacing)
        {
            var t = transform.Find("CombatFacingRoot");
            if (!t) t = transform.Find("GameplayFacing");
            if (t) gameplayFacing = t;
        }


        // Clone blob material so each player owns their instance (safe to set uniforms per-player)
        if (blobMat)
        {
            _blobMatInstance = new Material(blobMat);
            blobMat = _blobMatInstance;
        }

        _ghostProps ??= new MaterialPropertyBlock();
        _mainProps  ??= new MaterialPropertyBlock();
        _tmpProps = new MaterialPropertyBlock();
        _decoSeed0 = Random.value * 1000f;
        _decoSeed1 = Random.value * 1000f;
        _decoSeed2 = Random.value * 1000f;
    }

    void OnEnable()
    {
        Camera.onPreCull += HandlePreCull;
        Camera.onPreRender += HandlePreRender;
        Camera.onPostRender += HandlePostRender;
    }

    void OnDisable()
    {
        Camera.onPreCull -= HandlePreCull;
        Camera.onPreRender -= HandlePreRender;
        Camera.onPostRender -= HandlePostRender;

        // Detach and release command buffers
        foreach (var kv in _perCamCB)
        {
            var cam = kv.Key;
            var cb = kv.Value;
            if (cam && cb != null)
            {
                cb.Clear();
                cam.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha, cb);
            }
            cb?.Release();
        }
        _perCamCB.Clear();
    }

    void OnDestroy()
    {
        if (_blobMatInstance)
        {
        #if UNITY_EDITOR
            DestroyImmediate(_blobMatInstance);
        #else
            Destroy(_blobMatInstance);
        #endif
            _blobMatInstance = null;
        }
    }

    void Start()
    {
        if (arc && arc.TryGetComponent<MeshRenderer>(out var arcMR))
            arcMR.sharedMaterial = IsTeam2() ? arcMatTeam2 : arcMatTeam1;

        if (nuggetsGPU)
            nuggetsGPU.SetDotColor(IsTeam2() ? nuggetsTeam2 : nuggetsTeam1);
    }

    // Called every frame by PlayerControllerScript
public void SetMoveInput(Vector2 stick)
{
    if (invertY) stick.y = -stick.y;

    // Deadzone + remap to 0..1
    float m = stick.magnitude;
    if (m < inputDeadzone)
    {
        stick = Vector2.zero;
    }
    else
    {
        stick = stick.normalized * ((m - inputDeadzone) / (1f - inputDeadzone));
    }

    _input = stick;

    // --- GameplayFacing: ALWAYS accept flips (this is for combat/hitboxes) ---
    if (stick.sqrMagnitude > 0.0001f)
    {
        _gameplayFacing = stick.normalized;

        // Aim stability (optional): only for visuals/aim if you want flip rejection
        if (_aimDir.sqrMagnitude < 0.0001f || Vector2.Dot(_gameplayFacing, _aimDir) > aimFlipRejectDot)
            _aimDir = _gameplayFacing;
    }
    // else: keep both facings as last meaningful direction
}


    void Update()
    {
        Vector3 v3 = _rb ? _rb.linearVelocity : new Vector3(velocityWS.x, 0f, velocityWS.y);
        Vector2 vWorld = new(v3.x, v3.z);
        float worldMag = vWorld.magnitude;

        // attack stage effort
        float effectiveMag = worldMag;

        if (attackController && attackController.IsAttacking && attackController.CurrentStage != null)
        {
            var s = attackController.CurrentStage;
            float baseDashSpeed = (s.Duration > 0.001f) ? (s.TravelDistance / s.Duration) : 0f;

            float t = Mathf.Clamp01(attackController.StageNormalizedTime);
            float ramp = Mathf.Sin(Mathf.PI * t); // 0 → 1 → 0
            float dashEffort = baseDashSpeed * ramp * attackEffortScale;

            effectiveMag = Mathf.Max(worldMag, dashEffort);
        }

        // --- Shared teardrop strength 0..1 ---
        float moveNorm = 0f;
        if (_rb)
        {
            float speed = new Vector2(_rb.linearVelocity.x, _rb.linearVelocity.z).magnitude;
            float move01 = Mathf.Clamp01(speed / Mathf.Max(0.01f, teardropSpeedForMax));
            moveNorm = Mathf.Sqrt(move01);
        }

        float attackNorm = 0f;
        if (attackController && attackController.IsAttacking && attackController.CurrentStage != null)
        {
            var stage = attackController.CurrentStage;
            if (stage.StageType == Massive.Player.AttackStageType.PrimaryLunge)
            {
                float t = Mathf.Clamp01(attackController.StageNormalizedTime);
                float tri = 1f - Mathf.Abs(2f * t - 1f); // 0..1..0
                attackNorm = Mathf.Lerp(0.7f, 1f, tri);
            }
        }

        float moveTerm = moveNorm * teardropMoveWeight;
        float attackTerm = attackNorm * teardropAttackWeight;
        float targetCombined = Mathf.Max(moveTerm, attackTerm);

        bool attackActive = attackNorm > 0.0001f;
        bool isReallyIdle = !attackActive && (moveNorm < 0.10f);

        float targetStrength = isReallyIdle ? 0f : Mathf.Clamp01(idleTeardrop + targetCombined);

        float upSpeed = 12f;
        float downSpeed = 8f;
        float speedFactor = (targetStrength > _teardropStrengthSmoothed) ? upSpeed : downSpeed;
        float lerpFactor = 1f - Mathf.Exp(-speedFactor * Time.deltaTime);

        _teardropStrengthSmoothed = Mathf.Lerp(_teardropStrengthSmoothed, targetStrength, lerpFactor);
        _teardropStrength = Mathf.Clamp01(_teardropStrengthSmoothed);

        // --- Ghost trail sampling (lunge only) ---
        bool lungeActive = false;
        float stageT = 0f;

        if (attackController && attackController.IsAttacking && attackController.CurrentStage != null)
        {
            if (attackController.CurrentStage.StageType == Massive.Player.AttackStageType.PrimaryLunge)
            {
                lungeActive = true;
                stageT = Mathf.Clamp01(attackController.StageNormalizedTime);
            }
        }

        if (!visuals)
        {
            _ghosts.Clear();
            _lastGhostSampleT = 0f;
            _wasLungeActive = false;
        }
        else
        {
            float stepNorm = 1f / GhostStepsTotal;

            if (lungeActive && !_wasLungeActive)
            {
                _ghosts.Clear();
                _lastGhostSampleT = 0f;
            }

            if (lungeActive)
            {
                if (stageT < _lastGhostSampleT)
                {
                    _ghosts.Clear();
                    _lastGhostSampleT = 0f;
                }

                while (stageT >= _lastGhostSampleT + stepNorm && _ghosts.Count < GhostStepsTotal)
                {
                    _lastGhostSampleT += stepNorm;
                    _ghosts.Add(new BlobGhost
                    {
                        position = visuals.position,
                        rotation = visuals.rotation,
                        teardrop = _teardropStrength,
                        spawnTime = Time.time
                    });
                }
            }

            float ghostNow = Time.time;
                for (int i = _ghosts.Count - 1; i >= 0; i--)
                {
                    if (ghostNow - _ghosts[i].spawnTime > GhostLifetime)
                        _ghosts.RemoveAt(i);
                }

            _wasLungeActive = lungeActive;
        }



        // --- Yaw: follow gameplay-facing (updates even on instant 180 reversals) ---
        Vector2 yawDir = _gameplayFacing;
        if (yawDir.sqrMagnitude < 0.0001f)
            yawDir = Vector2.right;

        float targetYaw = Mathf.Atan2(yawDir.y, yawDir.x) * Mathf.Rad2Deg;


        float turnSpeed = (_turnDampTarget01 > _turnDamp01) ? chargeTurnDampEaseUp : chargeTurnDampEaseDown;
        float turnK = 1f - Mathf.Exp(-Mathf.Max(0.01f, turnSpeed) * Time.deltaTime);
        _turnDamp01 = Mathf.Lerp(_turnDamp01, _turnDampTarget01, turnK);
        _turnDamp01 = Mathf.Clamp01(_turnDamp01);

        if (_turnDamp01 > 0.95f)
            _yawVelDeg = 0f;

        float yawSmooth = yawSmoothTime * Mathf.Lerp(1f, chargingYawSmoothMultiplier, _turnDamp01);
        float maxYaw    = maxYawSpeed  * Mathf.Lerp(1f, chargingMaxYawSpeedMultiplier, _turnDamp01);

        float nextYaw = Mathf.SmoothDampAngle(_yawDeg, targetYaw, ref _yawVelDeg, yawSmooth);
        float maxStep = maxYaw * Time.deltaTime;


        float delta = Mathf.DeltaAngle(_yawDeg, nextYaw);
        if (Mathf.Abs(delta) > maxStep)
            nextYaw = _yawDeg + Mathf.Clamp(delta, -maxStep, +maxStep);

        _yawDeg = nextYaw;

        // visuals: XZ plane in vector mode
        Quaternion baseRot = vectorBlobMode ? Quaternion.identity : Quaternion.Euler(-90f, 0f, 0f);
        Quaternion yawRot = Quaternion.AngleAxis(_yawDeg, Vector3.up);
        if (visuals)
        {
            visuals.position = transform.position;
            visuals.rotation = yawRot * baseRot;
        }

        UpdateGameplayFacingRoot();

        _noise += Time.deltaTime * 1.3f;
        _hit = Mathf.Max(0, _hit - hitImpulseDecay * Time.deltaTime);
        _hitTime += Time.deltaTime;

        _contactStrength = Mathf.Max(0f, _contactStrength - _contactDecay * Time.deltaTime);

        // --- compute deformAmtNow & deformDirNow once ---
        float baseWobble = worldMag * 0.03f;
        float attackWobble = 0f;

        if (attackController && attackController.IsAttacking && attackController.CurrentStage != null)
        {
            var s = attackController.CurrentStage;
            float baseDashSpeed = (s.Duration > 0.001f) ? (s.TravelDistance / s.Duration) : 0f;
            float t = Mathf.Clamp01(attackController.StageNormalizedTime);
            float ramp = Mathf.Sin(Mathf.PI * t);
            attackWobble = baseDashSpeed * ramp * attackEffortScale * 0.06f;
        }

        float deformAmtNow = Mathf.Min(maxWobble, baseWobble + attackWobble);

        float lagAlpha = 1f - Mathf.Exp(-tailLagSpeed * Time.deltaTime);
        _deformLag = Mathf.Lerp(_deformLag, deformAmtNow, lagAlpha);

        // --- Direction for blob deformation: follow movement velocity ---
        Vector2 deformDirNow = Vector2.zero;

        if (worldMag > 0.05f)
        {
            Transform refT = visuals ? visuals : transform;
            Vector3 velLocal3 = refT.InverseTransformDirection(v3);
            Vector2 velLocal = new Vector2(velLocal3.x, velLocal3.z);

            if (velLocal.sqrMagnitude > 1e-4f)
                deformDirNow = velLocal.normalized;
        }

        if (deformDirNow == Vector2.zero && _hit > 0.001f)
        {
            deformDirNow = new Vector2(Mathf.Cos(_hitAngle), Mathf.Sin(_hitAngle));
        }

        if (deformDirNow == Vector2.zero)
            deformDirNow = Vector2.right;

        // Nuggets
        if (nuggetsGPU)
        {
            float stretchY = 1.0f;
            nuggetsGPU.FeedFromController(
                idleWobble, _teardropStrength, deformDirNow,
                dirFrontGain, dirBackGain, areaKeep,
                _hit, _noise,
                baseRadius, outlineHalf, stretchY,
                teardropK1, teardropK2
            );

            if (arc && visuals)
                arc.Rebuild(yawDir * Mathf.Max(effectiveMag, 1f), visuals.position, baseRadius + outlineHalf);

        }

        // Smooth jitter up/down so it doesn't snap off on release
        float jitterSpeed = (_chargeJitterTarget01 > _chargeJitter01) ? chargeJitterEaseUp : chargeJitterEaseDown;
        float jitterK = 1f - Mathf.Exp(-Mathf.Max(0.01f, jitterSpeed) * Time.deltaTime);
        _chargeJitter01 = Mathf.Lerp(_chargeJitter01, _chargeJitterTarget01, jitterK);

        ApplyBlobUniforms(deformAmtNow, _deformLag, deformDirNow);

        // --- Decoherence: pre-jitter then split ---
        float now = Time.time;

        bool inPreJitter = _decoRequestedActive &&
                        (now - _decoRequestedStartTime) < Mathf.Max(0.0001f, decoPreSplitJitterSeconds);

        float desiredSplitTarget = (_decoRequestedActive && !inPreJitter) ? 1f : 0f;
        _decoSplitTarget01 = desiredSplitTarget;

        // drive split 0..1 with your existing fade time
        float fade = Mathf.Max(0.0001f, decoSplitFadeSeconds);
        _decoSplit01 = Mathf.MoveTowards(_decoSplit01, _decoSplitTarget01, Time.deltaTime / fade);

        // compute sideways axis once per frame (used by blob + nuggets)
        if (visuals != null)
        {
            Vector3 face = visuals.right;
            face.y = 0f;
            if (face.sqrMagnitude < 0.0001f) face = Vector3.right;
            face.Normalize();

            _decoSideWS = new Vector3(-face.z, 0f, face.x);
            if (_decoSideWS.sqrMagnitude < 0.0001f) _decoSideWS = Vector3.forward;
            _decoSideWS.Normalize();
        }
        else
        {
            _decoSideWS = Vector3.forward;
        }

        // jitter right before splitting
        float pre01 = 0f;
        if (inPreJitter)
        {
            float dur = Mathf.Max(0.0001f, decoPreSplitJitterSeconds);
            pre01 = Mathf.Clamp01((now - _decoRequestedStartTime) / dur);
        }
        _decoPreJitterWS = DecoJitterWS(now, decoPreSplitJitterFreq, decoPreSplitJitterAmp * pre01, _decoSeed0);

        // jitter while holding max separation (ramps in near full split)
        float hold01 = Mathf.SmoothStep(0.75f, 1f, _decoSplit01);
        _decoGhostJitterA_WS = DecoJitterWS(now, decoHoldJitterFreq, decoHoldJitterAmp * hold01, _decoSeed1);
        _decoGhostJitterB_WS = DecoJitterWS(now, decoHoldJitterFreq, decoHoldJitterAmp * hold01, _decoSeed2);
    }

    public void OnContact(Vector3 worldContactPoint, Vector3 worldContactNormal, float strength)
    {
        if (strength <= 0.001f) return;

        _contactStrength = Mathf.Clamp01(Mathf.Max(_contactStrength, strength));

        if (visuals)
        {
            Vector3 localNormal = visuals.InverseTransformDirection(-worldContactNormal);
            Vector2 dir = new Vector2(localNormal.x, localNormal.z);
            if (dir.sqrMagnitude > 1e-5f)
                _contactAngle = Mathf.Atan2(dir.y, dir.x);
        }
    }

    void ApplyBlobUniforms(float deformAmtHead, float deformAmtTail, Vector2 deformDirLocal)
    {
        if (!blobMat) return;

        Color fill = IsTeam2() ? team2_Fill : team1_Fill;
        Color outline = IsTeam2() ? team2_Outline : team1_Outline;

        blobMat.SetFloat("_Radius", baseRadius);
        blobMat.SetFloat("_OutlineHalf", outlineHalf);
        blobMat.SetVector("_DeformDir", new Vector4(deformDirLocal.x, deformDirLocal.y, 0, 0));
        blobMat.SetFloat("_DeformAmtHead", deformAmtHead);
        blobMat.SetFloat("_DeformAmtTail", deformAmtTail);

        blobMat.SetFloat("_TeardropK1", teardropK1);
        blobMat.SetFloat("_TeardropK2", teardropK2);
        blobMat.SetFloat("_DeformLerp", _teardropStrength);

        float stretchY = 1f - _teardropStrength * maxVerticalSquash;
        blobMat.SetFloat("_Stretch", stretchY);

        blobMat.SetFloat("_IdleWobble", idleWobble);
        blobMat.SetFloat("_HitImpulse", _hit);
        blobMat.SetFloat("_NoisePhase", _noise);
        blobMat.SetFloat("_HitAngle", _hitAngle);
        blobMat.SetFloat("_HitTime", _hitTime);
        blobMat.SetFloat("_ContactStrength", _contactStrength);
        blobMat.SetFloat("_ContactAngle", _contactAngle);

        blobMat.SetFloat("_DirFrontGain", dirFrontGain);
        blobMat.SetFloat("_DirBackGain", dirBackGain);
        blobMat.SetFloat("_AreaKeep", areaKeep);

        blobMat.SetColor("_FillColor", fill);
        blobMat.SetColor("_OutlineColor", outline);
    }

    // Ensure a CB exists and is attached to this camera before transparents
    void HandlePreCull(Camera cam)
    {
        if (!vectorBlobMode || !blobMat || !visuals) return;

        if (!_perCamCB.TryGetValue(cam, out var cb) || cb == null)
        {
            cb = new CommandBuffer { name = $"Draw Player Blob (Vector) - {name}" };
            _perCamCB[cam] = cb;
            cam.AddCommandBuffer(CameraEvent.BeforeForwardAlpha, cb);
        }
    }

    // Update the CB contents for this camera this frame (upload MVP + issue draws)
 void HandlePreRender(Camera cam)
{
    // If a CB exists for this camera, ALWAYS clear it first.
    // This prevents “stuck blob” artifacts if we early-return.
    if (_perCamCB.TryGetValue(cam, out var cb) && cb != null)
        cb.Clear();
    else
        return;

    // After clearing, it’s safe to bail out.
    if (!vectorBlobMode || !blobMat || !visuals) return;

    Matrix4x4 V = cam.worldToCameraMatrix;
    Matrix4x4 P = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true);
    const int SEG = 128;

    // --- Ghost trail (outline only) ---
    if (_ghosts != null && _ghosts.Count > 0)
    {
        Color baseOutline = IsTeam2() ? team2_Outline : team1_Outline;
        float now = Time.time;
        int drawn = 0;

        for (int i = _ghosts.Count - 1; i >= 0 && drawn < GhostStepsVisible; i--)
        {
            var ghost = _ghosts[i];
            float lifeNorm = (now - ghost.spawnTime) / GhostLifetime;
            if (lifeNorm < 0f || lifeNorm > 1f) continue;

            float fade = (1f - lifeNorm) * GhostMaxAlpha;
            if (fade <= 0.01f) continue;

            _ghostProps.Clear();

            Color ghostOutline = baseOutline;
            ghostOutline.a *= fade;
            _ghostProps.SetColor("_OutlineColor", ghostOutline);

            _ghostProps.SetFloat("_DeformLerp", ghost.teardrop);
            float ghostStretch = 1f - ghost.teardrop * maxVerticalSquash;
            _ghostProps.SetFloat("_Stretch", ghostStretch);

            Matrix4x4 Mg = Matrix4x4.TRS(ghost.position, ghost.rotation, visuals.lossyScale);
            Matrix4x4 MVPg = P * V * Mg;
            _ghostProps.SetMatrix("_MVP", MVPg);

            cb.DrawProcedural(
                Matrix4x4.identity,
                blobMat,
                1,
                MeshTopology.Triangles,
                SEG * 3,
                1,
                _ghostProps
            );

            drawn++;
        }
    }

    // --- Main blob OR Decoherence split ---
    {
        // Apply power-up jitter to the *rendered* blob only (not transforms / not VFX modules)
        Vector3 jitter = Vector3.zero;
        if (_chargeJitter01 > 0.001f)
        {
            float amp = 0.06f * _chargeJitter01;
            float tt = Time.time * (16f + 20f * _chargeJitter01);
            jitter = new Vector3(Mathf.Sin(tt * 1.13f), 0f, Mathf.Sin(tt * 0.97f + 1.7f)) * amp;
        }

        Vector3 basePos = visuals.position + jitter + _decoPreJitterWS;
        Quaternion baseRot = visuals.rotation;
        Vector3 baseScale = visuals.lossyScale;

        // 0 = normal, 1 = fully split
        float t = Mathf.Clamp01(_decoSplit01);

        if (t <= 0.001f)
        {
            // --- Normal main blob (fill + outline) ---
            Matrix4x4 M = Matrix4x4.TRS(basePos, baseRot, baseScale);
            Matrix4x4 MVP = P * V * M;

            _mainProps.Clear();
            _mainProps.SetMatrix("_MVP", MVP);

            cb.DrawProcedural(
                Matrix4x4.identity,
                blobMat,
                0,
                MeshTopology.Triangles,
                SEG * 3,
                1,
                _mainProps
            );

            cb.DrawProcedural(
                Matrix4x4.identity,
                blobMat,
                1,
                MeshTopology.Triangles,
                SEG * 3,
                1,
                _mainProps
            );
        }
        else
        {
            // --- Decoherence split: don't draw the original. Draw two sideways copies. ---

            // Determine sideways axis from facing.
            // NOTE: visuals.right is treated as "front" elsewhere in your project.
            Vector3 face = visuals.right;
            face.y = 0f;
            if (face.sqrMagnitude < 0.0001f) face = Vector3.right;
            face.Normalize();

            Vector3 side = new Vector3(-face.z, 0f, face.x); // 90° rotated in XZ
            if (side.sqrMagnitude < 0.0001f) side = Vector3.forward;
            side.Normalize();

            float halfSep = 0.5f * decoSplitSeparationWorld * t;
            Vector3 off = side * halfSep;

            // Optional: ghosty transparency (safe if shader has these properties)
            // If you don't want transparency, set these to 1f in the inspector.


            // Color outline = IsTeam2() ? team2_Outline : team1_Outline;
            // outline.a *= decoSplitOutlineAlpha;

            // bool hasFillColor = blobMat.HasProperty("_FillColor");
            // Color fill = hasFillColor ? blobMat.GetColor("_FillColor") : Color.white;
            // if (hasFillColor) fill.a *= decoSplitFillAlpha;



            Color fill = IsTeam2() ? team2_Fill : team1_Fill;
            Color outline = IsTeam2() ? team2_Outline : team1_Outline;

            float o = Mathf.Clamp01(decoGhostOpacity);

            fill.a *= o * decoSplitFillAlpha;
            outline.a *= o * decoSplitOutlineAlpha;

            // ---- Draw copy A ----
            {
                Vector3 posA = basePos + off + _decoGhostJitterA_WS;
                Matrix4x4 MA = Matrix4x4.TRS(posA, baseRot, baseScale);
                Matrix4x4 MVPA = P * V * MA;

                _mainProps.Clear();
                _mainProps.SetMatrix("_MVP", MVPA);

                _mainProps.SetColor(_OutlineColor, outline);
                _mainProps.SetColor(_FillColor, fill);

                cb.DrawProcedural(
                    Matrix4x4.identity,
                    blobMat,
                    0,
                    MeshTopology.Triangles,
                    SEG * 3,
                    1,
                    _mainProps
                );

                cb.DrawProcedural(
                    Matrix4x4.identity,
                    blobMat,
                    1,
                    MeshTopology.Triangles,
                    SEG * 3,
                    1,
                    _mainProps
                );
            }

            // ---- Draw copy B ----
            {
                Vector3 posB = basePos - off + _decoGhostJitterB_WS;
                Matrix4x4 MB = Matrix4x4.TRS(posB, baseRot, baseScale);
                Matrix4x4 MVPB = P * V * MB;

                _mainProps.Clear();
                _mainProps.SetMatrix("_MVP", MVPB);

                _mainProps.SetColor(_OutlineColor, outline);
                _mainProps.SetColor(_FillColor, fill);

                cb.DrawProcedural(
                    Matrix4x4.identity,
                    blobMat,
                    0,
                    MeshTopology.Triangles,
                    SEG * 3,
                    1,
                    _mainProps
                );

                cb.DrawProcedural(
                    Matrix4x4.identity,
                    blobMat,
                    1,
                    MeshTopology.Triangles,
                    SEG * 3,
                    1,
                    _mainProps
                );
            }
        }
    }
}

    void HandlePostRender(Camera cam)
    {
        // No-op; keeping hook for debugging if needed.
    }

    public void OnHit(float strength, Vector3 worldHitPos)
    {
        _hit = Mathf.Clamp01(_hit + strength);
        _hitTime = 0f;

        if (visuals)
        {
            Vector3 local = visuals.InverseTransformPoint(worldHitPos);
            Vector2 p = new Vector2(local.x, local.z);
            if (p.sqrMagnitude > 1e-5f)
                _hitAngle = Mathf.Atan2(p.y, p.x);
        }
    }

//     private void DrawBlob(CommandBuffer cb, Camera cam, Vector3 posWS, float t01, float fillAlphaMul, float outlineAlphaMul)
// {
//     var vp = cam.projectionMatrix * cam.worldToCameraMatrix;

//     _tmpProps.Clear();
//     _tmpProps.SetMatrix(_MVP, vp * Matrix4x4.Translate(posWS));

//     // Override _PlayerPos so any shader effects anchored to PlayerPos follow the split copy
//     _tmpProps.SetVector(_PlayerPos, new Vector4(posWS.x, posWS.z, baseRadius, 0f));

//     Color fc = fillColor;
//     fc.a *= (t01 * fillAlphaMul);

//     Color oc = outlineColor;
//     oc.a *= (t01 * outlineAlphaMul);

//     _tmpProps.SetColor(_FillColor, fc);
//     _tmpProps.SetColor(_OutlineColor, oc);

//     cb.DrawProcedural(Matrix4x4.identity, blobMat, 0, MeshTopology.Triangles, SEG * 3, 1, _tmpProps);
//     cb.DrawProcedural(Matrix4x4.identity, blobMat, 1, MeshTopology.Triangles, SEG * 3, 1, _tmpProps);
// }

private void DrawBlob(
    CommandBuffer cb,
    Matrix4x4 V,
    Matrix4x4 P,
    Vector3 posWS,
    Quaternion rotWS,
    Vector3 scaleWS,
    Color fill,
    Color outline)
{
    const int SEG = 128;

    Matrix4x4 M = Matrix4x4.TRS(posWS, rotWS, scaleWS);
    Matrix4x4 MVP = P * V * M;

    _tmpProps.Clear();
    _tmpProps.SetMatrix("_MVP", MVP);
    _tmpProps.SetColor("_FillColor", fill);
    _tmpProps.SetColor("_OutlineColor", outline);

    cb.DrawProcedural(Matrix4x4.identity, blobMat, 0, MeshTopology.Triangles, SEG * 3, 1, _tmpProps);
    cb.DrawProcedural(Matrix4x4.identity, blobMat, 1, MeshTopology.Triangles, SEG * 3, 1, _tmpProps);
}

    // old API retained
    public void OnHit(float strength = 1f)
    {
        _hit = Mathf.Clamp01(_hit + strength);
    }

    bool IsTeam2()
    {
        return _pcs && _pcs.teamID != 1;
    }

    private Vector3 DecoJitterWS(float now, float freq, float amp, float seed)
{
    if (amp <= 0.0001f || freq <= 0.0001f) return Vector3.zero;

    float t = now * freq;

    // layered sines = stable “vibration” without Random popping
    float x = Mathf.Sin(t * 1.13f + seed) + Mathf.Sin(t * 2.07f + seed * 1.7f);
    float z = Mathf.Sin(t * 0.97f + seed * 2.3f) + Mathf.Sin(t * 1.71f + seed * 0.9f);

    return new Vector3(x, 0f, z) * (0.5f * amp);
}

    void UpdateGameplayFacingRoot()
{
    if (!gameplayFacing) return;

    Vector2 dir = _gameplayFacing;
    if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;

    float targetYaw = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

    float nextYaw;

    if (gameplayFacingInstant)
    {
        nextYaw = targetYaw;
        _gameplayYawVelDeg = 0f;
    }
    else
    {
        nextYaw = Mathf.SmoothDampAngle(_gameplayYawDeg, targetYaw, ref _gameplayYawVelDeg, gameplayFacingSmoothTime);

        float maxStep = gameplayFacingMaxYawSpeed * Time.deltaTime;
        float delta = Mathf.DeltaAngle(_gameplayYawDeg, nextYaw);
        if (Mathf.Abs(delta) > maxStep)
            nextYaw = _gameplayYawDeg + Mathf.Clamp(delta, -maxStep, +maxStep);
    }

    _gameplayYawDeg = nextYaw;

    // IMPORTANT: hitbox root should be a pure yaw (no -90 baseRot stuff)
    gameplayFacing.rotation = Quaternion.AngleAxis(_gameplayYawDeg, Vector3.up);
}



}
