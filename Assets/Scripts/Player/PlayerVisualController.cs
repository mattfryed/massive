using System.Collections.Generic;
using UnityEngine;
using Rigidbody = UnityEngine.Rigidbody;
using UnityEngine.Rendering;  // built-in pipeline CommandBuffer API

[DisallowMultipleComponent]
public class PlayerVisualController : MonoBehaviour
{
    [Header("Rendering")]
    public bool vectorBlobMode = true;
    public Transform visuals;          // "Visuals" child that follows player & gets yaw
    public Material blobMat;           // MASSIVE/PlayerBlobVector

    [Header("Blob Shape")]
    public float baseRadius = 0.65f;
    public float outlineHalf = 0.03f;

    [Header("Wobble & Reactions")]
    [Range(0,1)] public float idleWobble = 0.25f;  // symmetric idle scallop (no sideways drift)
    public float maxWobble  = 0.12f;               // velocity wobble cap
    public float hitImpulseDecay = 7.5f;

    [Header("Directional Stretch")]
[Range(0f,2f)] public float dirFrontGain = 1.0f;
[Range(0f,2f)] public float dirBackGain  = 0.5f;

    [Header("Team Colors (auto from PlayerControllerScript.teamID)")]
    public Color team1_Fill    = Color.black;
    public Color team1_Outline = Color.white;
    public Color team2_Fill    = Color.white;
    public Color team2_Outline = Color.black;

    [Header("Nuggets (mesh-based)")]
    public PlayerNuggetsMesh nuggets;
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

    // state
    Vector2 _input, _aimDir = Vector2.right;
    float _yawDeg, _yawVelDeg, _hit, _noise;

    // refs
    Rigidbody _rb;
    PlayerControllerScript _pcs;

    // built-in: per-camera command buffers
    readonly Dictionary<Camera, CommandBuffer> _perCamCB = new();

    void Awake()
    {
        TryGetComponent(out _rb);
        TryGetComponent(out _pcs); // has teamID & calls SetMoveInput(...)
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
        _perCamCB.Clear();
    }

    void Start()
    {
        if (arc && arc.TryGetComponent<MeshRenderer>(out var arcMR))
            arcMR.sharedMaterial = IsTeam2() ? arcMatTeam2 : arcMatTeam1;

        if (nuggets)
            nuggets.SetDotColor(IsTeam2() ? nuggetsTeam2 : nuggetsTeam1);
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
        Vector3 v3 = _rb ? _rb.velocity : new Vector3(velocityWS.x, 0f, velocityWS.y);
        Vector2 vWorld = new(v3.x, v3.z);
        float worldMag = vWorld.magnitude;

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

        // Nuggets stay flat on XZ & get fed world planar velocity
        if (nuggets)
        {
            nuggets.transform.position = visuals ? visuals.position : transform.position;
            nuggets.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

            Vector2 vPlanar = _rb ? new Vector2(_rb.velocity.x, _rb.velocity.z) : velocityWS;
            nuggets.blobRadius = baseRadius;
            nuggets.outlineHalf = outlineHalf;
            nuggets.dotRadius = nuggets.dotSize * 0.5f;
            nuggets.velocityWS = vPlanar;
            nuggets.SetDotColor(IsTeam2() ? nuggetsTeam2 : nuggetsTeam1);
        }

        if (arc && visuals)
            arc.Rebuild(_aimDir * Mathf.Max(worldMag, 1f), visuals.position, baseRadius + outlineHalf);

        ApplyBlobUniforms(worldMag);
    }

    void ApplyBlobUniforms(float worldSpeedMag)
    {
        if (!blobMat) return;

        // Team colors from PlayerControllerScript.teamID
        Color fill    = IsTeam2() ? team2_Fill    : team1_Fill;
        Color outline = IsTeam2() ? team2_Outline : team1_Outline;

        // Symmetric idle wobble at rest (no directional bias)
        Vector2 deformDirLocal =
            (worldSpeedMag > 0.05f || _hit > 0.001f) ? new Vector2(1, 0) : Vector2.zero;

        float deformAmt = Mathf.Min(maxWobble, worldSpeedMag * 0.03f);

        blobMat.SetFloat("_Radius",       baseRadius);
        blobMat.SetFloat("_OutlineHalf",  outlineHalf);
        blobMat.SetVector("_DeformDir",   new Vector4(deformDirLocal.x, deformDirLocal.y, 0, 0));
        blobMat.SetFloat("_DeformAmt",    deformAmt);
        blobMat.SetFloat("_HitImpulse",   _hit);
        blobMat.SetFloat("_NoisePhase",   _noise);
        blobMat.SetFloat("_Stretch",      1.0f);
        blobMat.SetFloat("_IdleWobble",   idleWobble);
        blobMat.SetColor("_FillColor",    fill);
        blobMat.SetColor("_OutlineColor", outline);
        blobMat.SetFloat("_DirFrontGain", dirFrontGain);
        blobMat.SetFloat("_DirBackGain",  dirBackGain);

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

        // Build MVP for this camera
        Matrix4x4 M = visuals.localToWorldMatrix;
        Matrix4x4 V = cam.worldToCameraMatrix;
        Matrix4x4 P = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true);
        Matrix4x4 MVP = P * V * M;
        blobMat.SetMatrix("_MVP", MVP);

        const int SEG = 128;
        cb.Clear();


        // cb.DrawProcedural(Matrix4x4.identity, blobMat, 0, MeshTopology.Triangles, SEG * 3); // now depth
        // cb.DrawProcedural(Matrix4x4.identity, blobMat, 1, MeshTopology.Triangles, SEG * 3); // fill
        // cb.DrawProcedural(Matrix4x4.identity, blobMat, 2, MeshTopology.Triangles, SEG * 3); // ring

        // PASS 0: FILL (writes depth)
        cb.DrawProcedural(Matrix4x4.identity, blobMat, 0, MeshTopology.Triangles, SEG * 3);

        // PASS 1: RING (no depth write)
        cb.DrawProcedural(Matrix4x4.identity, blobMat, 1, MeshTopology.Triangles, SEG * 3);

    }

    void HandlePostRender(Camera cam)
    {
        // No-op; keeping hook for debugging if needed.
    }

    public void OnHit(float strength = 1f) { _hit = Mathf.Clamp01(_hit + strength); }

    bool IsTeam2()
    {
        // Treat teamID==1 as Team1; anything else -> Team2
        return _pcs && _pcs.teamID != 1;
    }
}
