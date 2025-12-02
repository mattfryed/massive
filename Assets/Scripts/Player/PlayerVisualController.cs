using System.Collections.Generic;
using UnityEngine;
using Rigidbody = UnityEngine.Rigidbody;
using UnityEngine.Rendering;  // built-in pipeline CommandBuffer API

[DisallowMultipleComponent]
public class PlayerVisualController : MonoBehaviour
{
    Material _blobMatInstance;
    float _hitAngle; // radians, blob-local angle of last impact
    float _hitTime;
    // Contact / flatten state
    float _contactStrength;    // 0..1, how hard we're pressing into something
    float _contactAngle;       // radians, blob-local direction of contact
    float _contactDecay = 5f;  // how quickly contact fades when leaving (tune)

    [Header("Rendering")]
    public bool vectorBlobMode = true;
    public Transform visuals;          // "Visuals" child that follows player & gets yaw
    public Material blobMat;           // MASSIVE/PlayerBlobVector

    [Header("Attacks")]
    public Massive.Player.PlayerAttackController attackController;
    [Range(0f, 1f)] public float attackEffortScale = 0.08f;


    [Header("Blob Shape")]
    public float baseRadius = 0.65f;
    public float outlineHalf = 0.03f;

    [Header("Wobble & Reactions")]
    [Range(0,1)] public float idleWobble = 0.25f;  // symmetric idle scallop (no sideways drift)
    public float maxWobble  = 0.12f;               // velocity wobble cap
    public float hitImpulseDecay = 1.0f;

    [Header("Directional Stretch")]
    [Range(0f,2f)] public float dirFrontGain = 1.0f;
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
    public Color team1_Fill    = Color.black;
    public Color team1_Outline = Color.white;
    public Color team2_Fill    = Color.white;
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
    public float stopSpeedEps = 0.02f;

    [Header("Input/Movement (fallback if no RB)")]
    public bool invertY = true;
    public float inputDeadzone = 0.12f;
    public Vector2 velocityWS;


    // --- Ghost trail (lunge only) ---
const int   GhostStepsTotal   = 4;    // conceptual steps from start→end
const int   GhostStepsVisible = 5;    // max visible at once
const float GhostMaxAlpha     = 0.7f; // strongest ghost opacity
const float GhostLifetime     = 0.25f; // seconds each ghost lives after spawn

struct BlobGhost
{
    public Vector3 position;
    public Quaternion rotation;
    public float teardrop;   // snapshot of shape strength at spawn
    public float spawnTime;  // Time.time when created
}

readonly List<BlobGhost> _ghosts = new List<BlobGhost>(GhostStepsTotal);
float _lastGhostSampleT = 0f;
bool _wasLungeActive = false;




    // state
    Vector2 _input, _aimDir = Vector2.right;
    float _yawDeg, _yawVelDeg, _hit, _noise;
    // shared 0..1 teardrop strength for blob + nuggets
    float _teardropStrength;   // what we send to the shader
float _teardropStrengthSmoothed;  // internal smoothing state

    // refs
    Rigidbody _rb;
    PlayerControllerScript _pcs;

    // built-in: per-camera command buffers
    readonly Dictionary<Camera, CommandBuffer> _perCamCB = new();
    MaterialPropertyBlock _ghostProps;

    void Awake()
    {
        TryGetComponent(out _rb);
        TryGetComponent(out _pcs); // has teamID & calls SetMoveInput(...)

        if (!attackController)
            TryGetComponent(out attackController);

        // 2) Clone the blob material so this player owns its own copy
        if (blobMat)
        {
            _blobMatInstance = new Material(blobMat);
            blobMat = _blobMatInstance;
        }
        if (_ghostProps == null)
            _ghostProps = new MaterialPropertyBlock();
    }

    void OnEnable()
    {
        Camera.onPreCull  += HandlePreCull;   // where we (re)attach command buffers
        Camera.onPreRender += HandlePreRender; // where we upload uniforms/MVP for that camera this frame
        Camera.onPostRender += HandlePostRender; // clean up (optional)
    }

    void OnDisable()
    {
        Camera.onPreCull  -= HandlePreCull;
        Camera.onPreRender -= HandlePreRender;
        Camera.onPostRender -= HandlePostRender;

        // remove and release CBs from all cameras
        foreach (var kv in _perCamCB)
        {
            var cam = kv.Key;
            var cb  = kv.Value;
            if (cam) cam.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha, cb);
            cb?.Release();
        }
        _perCamCB.Clear();    // 3) Clean up the instance (prevents Editor leaks during play/stop)
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

    // called every frame by PlayerControllerScript
    public void SetMoveInput(Vector2 stick)
    {
        if (invertY) stick.y = -stick.y;
        float m = stick.magnitude;
        if (m < inputDeadzone) stick = Vector2.zero;
        else stick = stick.normalized * ((m - inputDeadzone) / (1f - inputDeadzone));
        _input = stick;
        if (stick.sqrMagnitude > 0f) _aimDir = stick.normalized;
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

            // Approximate dash speed (units/second)
            float baseDashSpeed = (s.Duration > 0.001f) ? (s.TravelDistance / s.Duration) : 0f;

            // Optional: shape the effect so it peaks mid-attack (0..1..0)
            float t     = Mathf.Clamp01(attackController.StageNormalizedTime);
            float ramp  = Mathf.Sin(Mathf.PI * t); // 0 → 1 → 0
            float dashEffort = baseDashSpeed * ramp * attackEffortScale;

            effectiveMag = Mathf.Max(worldMag, dashEffort);
        }

// --- shared teardrop strength 0..1 (used by BOTH blob and nuggets) ---

// 1) movement component (0..1 based on teardropSpeedForMax)
//    Use sqrt so moderate speeds give noticeable shape.
float moveNorm = 0f;
if (_rb)
{
    float speed  = new Vector2(_rb.linearVelocity.x, _rb.linearVelocity.z).magnitude;
    float move01 = Mathf.Clamp01(speed / Mathf.Max(0.01f, teardropSpeedForMax));
    moveNorm = Mathf.Sqrt(move01); // ease-out: boosts low/mid speeds
}

// 2) attack component: treat primary lunge as strongly teardrop-shaped
float attackNorm = 0f;

if (attackController && attackController.IsAttacking && attackController.CurrentStage != null)
{
    var stage = attackController.CurrentStage;
    if (stage.StageType == Massive.Player.AttackStageType.PrimaryLunge)
    {
        float t = Mathf.Clamp01(attackController.StageNormalizedTime);

        // Triangle 0→1→0 across the stage
        float tri = 1f - Mathf.Abs(2f * t - 1f); // 0..1..0

        // Keep it in [0.7, 1.0] so it's always clearly teardrop during the lunge
        attackNorm = Mathf.Lerp(0.7f, 1f, tri);
    }
}

// 3) combine move & attack into a target strength
float moveTerm   = moveNorm   * teardropMoveWeight;
float attackTerm = attackNorm * teardropAttackWeight;

// If attacking, let attack dominate; otherwise use movement
float targetCombined = Mathf.Max(moveTerm, attackTerm);

// Consider "really idle" as: no lunge + very little movement
bool attackActive   = attackNorm > 0.0001f;
bool isReallyIdle   = !attackActive && (moveNorm < 0.10f);  // was 0.02f

float targetStrength;
if (isReallyIdle)
{
    targetStrength = 0f;               // perfect circle at rest
}
else
{
    targetStrength = idleTeardrop + targetCombined;
    targetStrength = Mathf.Clamp01(targetStrength);
}

// 4) Smooth toward target so shapes don't snap
// Different speeds for ramp-up and relax feel nice
float upSpeed   = 12f; // how fast to go toward stronger shape
float downSpeed = 8f;  // how fast to relax back toward idle

float speedFactor = (targetStrength > _teardropStrengthSmoothed) ? upSpeed : downSpeed;
float lerpFactor  = 1f - Mathf.Exp(-speedFactor * Time.deltaTime);

_teardropStrengthSmoothed = Mathf.Lerp(
    _teardropStrengthSmoothed,
    targetStrength,
    lerpFactor
);

// Final value we send to the shader / nuggets
_teardropStrength = Mathf.Clamp01(_teardropStrengthSmoothed);


        // --- Ghost trail sampling (lunge only) ---

        bool lungeActive = false;
        float stageT = 0f;

        if (attackController && attackController.IsAttacking && attackController.CurrentStage != null)
        {
            // Only for the primary lunge stage
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

            // Detect lunge start: reset sampling so we don't mix trails between lunges
            if (lungeActive && !_wasLungeActive)
            {
                _ghosts.Clear();
                _lastGhostSampleT = 0f;
            }

            if (lungeActive)
            {
                // Wrapped stage inside a single lunge (e.g. timeline looped)
                if (stageT < _lastGhostSampleT)
                {
                    _ghosts.Clear();
                    _lastGhostSampleT = 0f;
                }

                // Sample along the attack timeline at discrete normalized steps
                while (stageT >= _lastGhostSampleT + stepNorm && _ghosts.Count < GhostStepsTotal)
                {
                    _lastGhostSampleT += stepNorm;

                    var g = new BlobGhost
                    {
                        position  = visuals.position,
                        rotation  = visuals.rotation,
                        teardrop  = _teardropStrength, // snapshot shape at spawn
                        spawnTime = Time.time
                    };

                    _ghosts.Add(g);
                }
            }

            // Lifetime cull: ghosts fade out over a fixed time, even after lunge ends
            float now = Time.time;
            for (int i = _ghosts.Count - 1; i >= 0; i--)
            {
                if (now - _ghosts[i].spawnTime > GhostLifetime)
                    _ghosts.RemoveAt(i);
            }

            _wasLungeActive = lungeActive;
        }

        // smooth yaw to last aim
        float targetYaw = (_aimDir.sqrMagnitude > 0f)
            ? Mathf.Atan2(_aimDir.y, _aimDir.x) * Mathf.Rad2Deg
            : _yawDeg;

        float nextYaw = Mathf.SmoothDampAngle(_yawDeg, targetYaw, ref _yawVelDeg, yawSmoothTime);
        float maxStep = maxYawSpeed * Time.deltaTime;
        float delta   = Mathf.DeltaAngle(_yawDeg, nextYaw);
        if (Mathf.Abs(delta) > maxStep) nextYaw = _yawDeg + Mathf.Clamp(delta, -maxStep, +maxStep);
        _yawDeg = nextYaw;

        // visuals: XZ plane in vector mode
        Quaternion baseRot = vectorBlobMode ? Quaternion.identity : Quaternion.Euler(-90f, 0f, 0f);
        Quaternion yawRot  = Quaternion.AngleAxis(_yawDeg, Vector3.up);
        if (visuals)
        {
            visuals.position = transform.position;
            visuals.rotation = yawRot * baseRot;
        }

        _noise += Time.deltaTime * 1.3f;
        _hit    = Mathf.Max(0, _hit - hitImpulseDecay * Time.deltaTime);
        _hitTime += Time.deltaTime; 

        // Contact decays when we're not refreshing it
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

// Lagged deform for tail wobble (unchanged)
float lagAlpha = 1f - Mathf.Exp(-tailLagSpeed * Time.deltaTime);
_deformLag = Mathf.Lerp(_deformLag, deformAmtNow, lagAlpha);

// --- Direction for blob deformation: follow *movement* ---

Vector2 deformDirNow = Vector2.zero;

// 1) Use world velocity as primary direction
if (worldMag > 0.05f)
{
    // Convert world velocity into blob-local XZ
    Transform refT = visuals ? visuals : transform;
    Vector3 velLocal3 = refT.InverseTransformDirection(v3);
    Vector2 velLocal  = new Vector2(velLocal3.x, velLocal3.z);

    if (velLocal.sqrMagnitude > 1e-4f)
        deformDirNow = velLocal.normalized;   // this is "front" (nose)
}

// 2) If we're not really moving but just took a hit, orient along hit
if (deformDirNow == Vector2.zero && _hit > 0.001f)
{
    // _hitAngle is already blob-local radians
    deformDirNow = new Vector2(Mathf.Cos(_hitAngle), Mathf.Sin(_hitAngle));
}

// 3) Absolute fallback: point along +X so we don't feed (0,0) to shader
if (deformDirNow == Vector2.zero)
{
    deformDirNow = Vector2.right;
}

// --- Nuggets, if present ---
if (nuggetsGPU)
{
    float stretchY = 1.0f; // if you later squash the nuggets vertically, wire that here too

    nuggetsGPU.FeedFromController(
        idleWobble, _teardropStrength, deformDirNow,
        dirFrontGain, dirBackGain, areaKeep,
        _hit, _noise,
        baseRadius, outlineHalf, stretchY,
        teardropK1, teardropK2
    );

    if (arc && visuals)
        arc.Rebuild(_aimDir * Mathf.Max(effectiveMag, 1f), visuals.position, baseRadius + outlineHalf);
}

// --- Blob always updated ---
ApplyBlobUniforms(deformAmtNow, _deformLag, deformDirNow);


    }
    public void OnContact(Vector3 worldContactPoint, Vector3 worldContactNormal, float strength)
{
    // strength is 0..1, from your collision logic
    if (strength <= 0.001f) return;

    _contactStrength = Mathf.Clamp01(Mathf.Max(_contactStrength, strength));

    if (visuals)
    {
        // Convert the *contact normal* into blob-local axis
        // We want the direction "into the blob" from the wall
        Vector3 localNormal = visuals.InverseTransformDirection(-worldContactNormal); // minus: from wall into blob
        Vector2 dir = new Vector2(localNormal.x, localNormal.z);
        if (dir.sqrMagnitude > 1e-5f)
        {
            _contactAngle = Mathf.Atan2(dir.y, dir.x); // -pi..pi
        }
    }
}


void ApplyBlobUniforms(float deformAmtHead, float deformAmtTail, Vector2 deformDirLocal)
{
    if (!blobMat) return;

    // Team colors
    Color fill    = IsTeam2() ? team2_Fill    : team1_Fill;
    Color outline = IsTeam2() ? team2_Outline : team1_Outline;

    // Core shape + orientation
    blobMat.SetFloat("_Radius",      baseRadius);
    blobMat.SetFloat("_OutlineHalf", outlineHalf);
    blobMat.SetVector("_DeformDir",  new Vector4(deformDirLocal.x, deformDirLocal.y, 0, 0));
    blobMat.SetFloat("_DeformAmtHead", deformAmtHead);
    blobMat.SetFloat("_DeformAmtTail", deformAmtTail);

    // Shared teardrop parameters
    blobMat.SetFloat("_TeardropK1", teardropK1);
    blobMat.SetFloat("_TeardropK2", teardropK2);
    blobMat.SetFloat("_DeformLerp", _teardropStrength); // 0..1

    // Vertical squash: 1 → circle, (1 - maxVerticalSquash) at full lunge
    float stretchY = 1f - _teardropStrength * maxVerticalSquash;
    blobMat.SetFloat("_Stretch", stretchY);

    // Reactions / wobble / contact
    blobMat.SetFloat("_IdleWobble", idleWobble);
    blobMat.SetFloat("_HitImpulse", _hit);
    blobMat.SetFloat("_NoisePhase", _noise);
    blobMat.SetFloat("_HitAngle",   _hitAngle);
    blobMat.SetFloat("_HitTime",    _hitTime);
    blobMat.SetFloat("_ContactStrength", _contactStrength);
    blobMat.SetFloat("_ContactAngle",    _contactAngle);

    // Directional stretch scalars (still used for some wobble components in the shader)
    blobMat.SetFloat("_DirFrontGain", dirFrontGain);
    blobMat.SetFloat("_DirBackGain",  dirBackGain);
    blobMat.SetFloat("_AreaKeep",     areaKeep);

    // Colors
    blobMat.SetColor("_FillColor",    fill);
    blobMat.SetColor("_OutlineColor", outline);
}



    // Ensure a CB exists and is attached to this camera before transparents
    void HandlePreCull(Camera cam)
    {
        if (!vectorBlobMode || !blobMat || !visuals) return;

        if (!_perCamCB.TryGetValue(cam, out var cb) || cb == null)
        {
            cb = new CommandBuffer { name = "Draw Player Blob (Vector)" };
            _perCamCB[cam] = cb;
            cam.AddCommandBuffer(CameraEvent.BeforeForwardAlpha, cb);
        }
    }

    // Update the CB contents for this camera this frame (upload MVP + issue draws)
void HandlePreRender(Camera cam)
{
    if (!vectorBlobMode || !blobMat || !visuals) return;
    if (!_perCamCB.TryGetValue(cam, out var cb) || cb == null) return;

    // Build view/projection for this camera
    Matrix4x4 V = cam.worldToCameraMatrix;
    Matrix4x4 P = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true);
    const int SEG = 128;

    cb.Clear();

    // --- 1) Ghost trail (outline only), drawn FIRST so main blob appears on top ---
    if (_ghosts != null && _ghosts.Count > 0)
    {
        // Base outline color from team settings
        Color baseOutline = IsTeam2() ? team2_Outline : team1_Outline;

        float now   = Time.time;
        int   drawn = 0;

        // Draw newest ghosts first, up to GhostStepsVisible
        for (int i = _ghosts.Count - 1; i >= 0 && drawn < GhostStepsVisible; i--)
        {
            var ghost = _ghosts[i];

            // Lifetime 0..1 over GhostLifetime
            float lifeNorm = (now - ghost.spawnTime) / GhostLifetime;
            if (lifeNorm < 0f || lifeNorm > 1f) continue;

            float fade = (1f - lifeNorm) * GhostMaxAlpha;
            if (fade <= 0.01f) continue;

            if (_ghostProps == null)
                _ghostProps = new MaterialPropertyBlock();
            _ghostProps.Clear();

            // Per-ghost outline color
            Color ghostOutline = baseOutline;
            ghostOutline.a *= fade;
            _ghostProps.SetColor("_OutlineColor", ghostOutline);

            // Per-ghost shape (frozen at spawn)
            _ghostProps.SetFloat("_DeformLerp", ghost.teardrop);
            float ghostStretch = 1f - ghost.teardrop * maxVerticalSquash;
            _ghostProps.SetFloat("_Stretch", ghostStretch);

            // Per-ghost transform
            Matrix4x4 Mg   = Matrix4x4.TRS(ghost.position, ghost.rotation, visuals.lossyScale);
            Matrix4x4 MVPg = P * V * Mg;
            _ghostProps.SetMatrix("_MVP", MVPg);

            // Only draw the outline pass for ghosts (pass 1), no fill
            cb.DrawProcedural(
                Matrix4x4.identity,
                blobMat,
                1,                        // outline pass
                MeshTopology.Triangles,
                SEG * 3,
                1,
                _ghostProps
            );

            drawn++;
        }
    }

    // --- 2) Main blob (fill + outline), drawn LAST so it always appears on top of ghosts ---
    {
        Matrix4x4 M   = visuals.localToWorldMatrix;
        Matrix4x4 MVP = P * V * M;

        // Use global MVP for the main blob (no per-draw overrides needed)
        cb.SetGlobalMatrix("_MVP", MVP);

        // Fill for the current, real blob pose (writes depth, pass 0)
        cb.DrawProcedural(
            Matrix4x4.identity,
            blobMat,
            0,                        // FILL pass
            MeshTopology.Triangles,
            SEG * 3
        );

        // Outline ring for the current blob pose (no depth write, pass 1)
        cb.DrawProcedural(
            Matrix4x4.identity,
            blobMat,
            1,                        // RING pass
            MeshTopology.Triangles,
            SEG * 3
        );
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
            // Convert hit point into blob-local XZ and get angle
            Vector3 local = visuals.InverseTransformPoint(worldHitPos);
            Vector2 p = new Vector2(local.x, local.z);
            if (p.sqrMagnitude > 1e-5f)
            {
                _hitAngle = Mathf.Atan2(p.y, p.x); // -pi..pi
            }
        }
    }

    // keep the old API for existing callsites if any:
    public void OnHit(float strength = 1f)
    {
        _hit = Mathf.Clamp01(_hit + strength);
    }

    bool IsTeam2()
    {
        // Treat teamID==1 as Team1; anything else -> Team2
        return _pcs && _pcs.teamID != 1;
    }
}
