using UnityEngine;
using Rigidbody = UnityEngine.Rigidbody;

[DisallowMultipleComponent]
public class PlayerVisualController : MonoBehaviour
{
#if UNITY_EDITOR
    public bool debugOverlay = true;
    Vector2 _dbgStick, _dbgVFromWorld, _dbgVPlanar, _dbgHeading;

    // joystick input cached by the visuals (x → world X, y → world Z)
    Vector2 _input;                    // joystick input cached (x → world X, y → world Z)
    Vector2 _heading = Vector2.right;  // persistent facing used for yaw (defaults to +X)


    // API your PlayerControllerScript calls each frame
    public void SetMoveInput(Vector2 stick)
    {
        if (invertY) stick.y = -stick.y;

        float m = stick.magnitude;
        if (m < inputDeadzone) stick = Vector2.zero;
        else stick = stick.normalized * ((m - inputDeadzone) / (1f - inputDeadzone));

        _input = stick;
        if (stick.sqrMagnitude > 0f)
            _aimDir = stick.normalized; // <-- remember last pressed direction
    }

    //void OnGUI()
    //{
    //    if (!debugOverlay) return;
    //    var s = $"vPlanar=({_dbgVPlanar.x:F2},{_dbgVPlanar.y:F2})  speed={_dbgVPlanar.magnitude:F2}  heading=({_dbgHeading.x:F2},{_dbgHeading.y:F2})";
    //    GUI.Label(new Rect(20, 20, 800, 30), s);
    //}

    //void OnGUI()
    //{
    //    if (!debugOverlay) return;
    //    string s =
    //        $"Stick: {_dbgStick.x:F2},{_dbgStick.y:F2} | " +
    //        $"RB: {_dbgVFromWorld.x:F2},{_dbgVFromWorld.y:F2} | " +
    //        $"Planar: {_dbgVPlanar.x:F2},{_dbgVPlanar.y:F2} | " +
    //        $"Heading: {_dbgHeading.x:F2},{_dbgHeading.y:F2} | " +
    //        $"Blend: {headingBlendWithInput:F2}";
    //    GUI.Label(new Rect(16, 16, 1000, 24), s);
    //}

    //void OnDrawGizmosSelected()
    //{
    //    if (!debugOverlay) return;
    //    Gizmos.color = Color.yellow;
    //    var p = visuals ? visuals.position : transform.position;
    //    var h = new Vector3(_dbgHeading.x, 0, _dbgHeading.y);
    //    Gizmos.DrawLine(p, p + h * 1.0f);
    //}
#endif

    // keep Visuals “flat on XZ” and add a yaw in LOCAL space
    static readonly Quaternion VISUALS_BASE = Quaternion.Euler(-90f, 0f, 0f);

    Quaternion _visualsBase = Quaternion.Euler(-90f, 0f, 0f); // cancel the root’s +90°X
    Quaternion _visualsYaw = Quaternion.identity;

    [Header("Blob Shader (MASSIVE/PlayerBlob)")]
    public Material blobMat;
    public float baseRadius = 0.65f;
    public float outlineHalf = 0.03f;
    public float maxWobble = 0.12f;
    public float hitImpulseDecay = 7.5f;
    public bool rightTeam = false; // false: white fill/black outline; true: black fill/white outline

    [Header("Blob Colors")]
    public Color blobFillLeft = Color.black;
    public Color blobFillRight = Color.black;
    public Color outlineLeft = Color.black;
    public Color outlineRight = Color.white;

    [Header("Nuggets (mesh-based)")]
    public PlayerNuggetsMesh nuggets;           // <- mesh renderer script
    public Color nuggetsColorLeft = Color.white;
    public Color nuggetsColorRight = Color.black;

    [Header("Heading / Rotation Control")]
    [Range(0f, 1f)]
    public float headingBlendWithInput = 1.0f;   // 1 = joystick dominates, 0 = physics velocity only
    [Range(1f, 30f)]
    public float headingSmoothing = 12f;         // higher = faster yaw response

    [Header("Arc")]
    public PlayerArc arc;
    public Material arcMatLeft;   // black
    public Material arcMatRight;  // white

    [Header("Team Colors")]
    public Color fillColorLeft = Color.black;  // interior fill color for Left
    public Color fillColorRight = Color.black;  // interior fill color for Right

    [Header("Movement (optional fallback)")]
    public Vector2 velocityWS;   // XZ velocity if you’re not using a Rigidbody

    [Header("Visuals root (unrotated)")]
    public Transform visuals;    // assign your "Visuals" child

    [Header("Yaw Smoothing")]
    [Range(0.01f, 0.3f)] public float yawSmoothTime = 0.07f; // seconds
    public float maxYawSpeed = 900f;   // deg/sec clamp
    public float stopSpeedEps = 0.02f;           // below this = treat as stopped (keeps last heading)

    // persistent state
    Vector2 _aimDir = Vector2.right; // LAST non-zero input dir (what we face)
    float _yawDeg;    // current smoothed yaw (deg)
    float _yawVelDeg; // velocity for SmoothDampAngle

    [Header("Input")]
    public bool invertY = true; // your cabinet needs this
    public float inputDeadzone = 0.12f;

    float _hit, _noise;

    void Start()
    {
        if (arc && arc.GetComponent<MeshRenderer>())
            arc.GetComponent<MeshRenderer>().sharedMaterial = rightTeam ? arcMatRight : arcMatLeft;


        // Auto-bind the PlayerBlob material instance on BlobQuad if not assigned
        if (blobMat == null && visuals != null)
        {
            var mr = visuals.GetComponentInChildren<MeshRenderer>();
            if (mr != null && mr.sharedMaterial != null && mr.sharedMaterial.shader != null &&
                mr.sharedMaterial.shader.name.Contains("MASSIVE/PlayerBlob"))
            {
                blobMat = mr.material; // instance, not shared
            }
        }

        // set initial nugget color
        if (nuggets)
            nuggets.SetDotColor(rightTeam ? nuggetsColorRight : nuggetsColorLeft);
    }

    void Update()
    {
        // --- Movement (world) ---
        Vector3 v3;
        if (TryGetComponent<Rigidbody>(out var rb)) v3 = rb.velocity;
        else v3 = new Vector3(velocityWS.x, 0f, velocityWS.y);

        // world planar velocity & magnitude (for speed/slosh only)
        Vector2 vFromWorld = new Vector2(v3.x, v3.z);
        float worldMag = vFromWorld.magnitude;

        // --- Input (already preprocessed in SetMoveInput) ---
        Vector2 vFromInput = _input; // 0..1 after deadzone
        _dbgStick = _input;
        _dbgVFromWorld = vFromWorld;

        // --- Facing/Aim: ALWAYS use the last non-zero input dir ---
        float targetYaw =
            (_aimDir.sqrMagnitude > 0f)
            ? Mathf.Atan2(_aimDir.y, _aimDir.x) * Mathf.Rad2Deg
            : _yawDeg; // keep current if no aim yet

        // Smooth toward target yaw
        float nextYaw = Mathf.SmoothDampAngle(_yawDeg, targetYaw, ref _yawVelDeg, yawSmoothTime);
        float maxStep = maxYawSpeed * Time.deltaTime;
        float delta = Mathf.DeltaAngle(_yawDeg, nextYaw);
        if (Mathf.Abs(delta) > maxStep)
            nextYaw = _yawDeg + Mathf.Clamp(delta, -maxStep, +maxStep);

        _yawDeg = nextYaw;
        Quaternion baseRot = Quaternion.Euler(-90f, 0f, 0f);
        Quaternion yawRot = Quaternion.AngleAxis(_yawDeg, Vector3.up);

        // Drive Visuals in world space (decoupled from RB rotation)
        if (visuals)
        {
            visuals.position = transform.position;
            visuals.rotation = yawRot * baseRot; // WORLD rotation
        }

        if (nuggets)
        {
            // same position as Visuals, but world rotation is ONLY the -90° X base (no yaw)
            nuggets.transform.position = visuals ? visuals.position : transform.position;
            nuggets.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        }

        // --- Speed for deform/arc uses physical movement (or blended if you prefer) ---
        float speed = worldMag; // or: Mathf.Lerp(worldMag, vFromInput.magnitude, 0.3f);

        // --- Build a planar vector to drive slosh/arc based on AIM (direction) ---
        Vector2 vPlanarForVfx = _aimDir * Mathf.Max(worldMag, vFromInput.magnitude);

        // --- Velocity in VISUALS-LOCAL XZ (so “forward” = +local X) ---
        // NOTE: we negate the Z projection to fix your up/down inversion while keeping L/R correct.
        Vector2 vLocal;
        if (visuals)
        {
            var pv = new Vector3(vPlanarForVfx.x, 0f, vPlanarForVfx.y);
            float lx = Vector3.Dot(pv, visuals.right);          // left/right OK
            float lz = -Vector3.Dot(pv, visuals.forward);       // <-- NEGATE fixes up/down flip
            vLocal = new Vector2(lx, lz);
        }
        else
        {
            vLocal = vPlanarForVfx;
        }

        // debug
        _dbgVPlanar = vPlanarForVfx;
        _dbgHeading = new Vector2(Mathf.Cos(_yawDeg * Mathf.Deg2Rad), Mathf.Sin(_yawDeg * Mathf.Deg2Rad));

        float spLocal = vLocal.magnitude;



        // --- Blob uniforms ---
        _noise += Time.deltaTime * 1.3f;
        _hit = Mathf.Max(0, _hit - hitImpulseDecay * Time.deltaTime);

        // Because visuals rotates to heading, inside the shader “forward” is +X of the quad.
        // So we can just set DeformDir = (1,0) in LOCAL space.
        Vector2 deformDirLocal = new Vector2(1, 0);

        blobMat.SetFloat("_Radius", baseRadius);
        blobMat.SetFloat("_OutlineHalfWidth", outlineHalf);
        blobMat.SetVector("_DeformDir", new Vector4(1, 0, 0, 0)); // local +X (because we rotate Visuals)
        blobMat.SetFloat("_DeformAmt", Mathf.Min(maxWobble, speed * 0.03f));
        blobMat.SetFloat("_HitImpulse", _hit);
        blobMat.SetFloat("_NoisePhase", _noise);
        blobMat.SetInt("_TeamMode", rightTeam ? 1 : 0);
        blobMat.SetVector("_Center", Vector2.zero);
        blobMat.SetColor("_FillColor", rightTeam ? blobFillRight : blobFillLeft);
        blobMat.SetColor("_OutlineColor", rightTeam ? outlineRight : outlineLeft);
        blobMat.SetFloat("_Taper", 0.18f);
        blobMat.SetFloat("_Stretch", 1.0f);

        // --- Arc (use world center of visuals) ---
        if (arc)
        {
            // arc wants world XZ velocity
            arc.Rebuild(_aimDir * Mathf.Max(worldMag, 1f), visuals.position, baseRadius + outlineHalf);
        }

        // --- Nuggets (mesh-based) ---
        if (nuggets)
        {
            //nuggets.transform.localRotation = Quaternion.identity; // stays flat under Visuals

            // feed world XZ inertia (so they slosh with movement but do NOT rotate with yaw)
            Vector2 vWorldPlanar = new Vector2(rb ? rb.velocity.x : velocityWS.x,
                                               rb ? rb.velocity.z : velocityWS.y);
            nuggets.blobRadius = baseRadius;
            nuggets.outlineHalf = outlineHalf;
            nuggets.dotRadius = nuggets.dotSize * 0.5f;
            nuggets.velocityWS = vWorldPlanar; // <-- world planar now

            nuggets.SetDotColor(rightTeam ? nuggetsColorRight : nuggetsColorLeft);
        }
    }

    // Call from gameplay on impact/knockback/etc.
    public void OnHit(float strength = 1f) { _hit = Mathf.Clamp01(_hit + strength); }
}