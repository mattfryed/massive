using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerVisualController : MonoBehaviour
{
    [Header("Material & Shader Properties")]
    [Tooltip("Material using MASSIVE/PlayerPlasma")]
    public Material playerMat;
    [Range(0f, 1f)] public float plasmaFill = 0.25f;           // normalized mass level
    public float arcAppearSpeed = 0.2f;                         // min speed for arc to show
    public float arcMaxLengthDeg = 120f;                        // arc length at max speed
    public AnimationCurve arcLengthBySpeed = AnimationCurve.Linear(0, 0, 1, 1);
    public AnimationCurve arcThicknessBySpeed = AnimationCurve.Linear(0, 0.5f, 1, 1.5f);

    [Header("Elastic Body (non-uniform scale)")]
    public float stretchFactor = 0.10f;                         // forward scale gain per 1 m/s
    public float compressFactor = 0.10f;                        // sideways compression per 1 m/s
    public float maxStretch = 0.25f;
    public float returnSmooth = 12f;                            // return to 1.0 scale
    public float impactDeform = 0.18f;                          // squash amount on hit
    public float impactRecover = 18f;

    [Header("Noise Motion")]
    public float plasmaAdvectScale = 0.06f;                     // how much plasma condenses in velocity direction
    public float plasmaTurbulence = 1.0f;                       // use to increase internal noise speed at high mass

    [Header("Debug")]
    public bool visualizeForwardGizmo = false;
    public Color gizmoColor = new Color(1, 0.8f, 0.2f, 0.6f);

    // cached
    Rigidbody rb;
    Vector3 baseScale, targetScale;

    // shader property IDs
    static readonly int _VelocityVector   = Shader.PropertyToID("_VelocityVector");
    static readonly int _VelocityMag      = Shader.PropertyToID("_VelocityMag");
    static readonly int _PlasmaFill       = Shader.PropertyToID("_PlasmaFill");
    static readonly int _PlasmaAdvect     = Shader.PropertyToID("_PlasmaAdvect");
    static readonly int _PlasmaTurb       = Shader.PropertyToID("_PlasmaTurb");
    static readonly int _ArcEnable        = Shader.PropertyToID("_ArcEnable");
    static readonly int _ArcMidDir        = Shader.PropertyToID("_ArcMidDir");
    static readonly int _ArcLengthCos     = Shader.PropertyToID("_ArcLengthCos");
    static readonly int _ArcThickness     = Shader.PropertyToID("_ArcThickness");

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        baseScale = transform.localScale;
        targetScale = baseScale;
    }

    void Update()
    {
        if (!playerMat) return;

        // velocity & direction
        Vector3 v = rb.linearVelocity;
        float speed = v.magnitude;
        Vector3 dir = speed > 0.0001f ? v / speed : Vector3.right;

        // 1) INNER PLASMA: density + directional condensation
        playerMat.SetVector(_VelocityVector, new Vector4(dir.x, dir.y, dir.z, 0));
        playerMat.SetFloat(_VelocityMag, speed);
        playerMat.SetFloat(_PlasmaFill, Mathf.Clamp01(plasmaFill));
        playerMat.SetFloat(_PlasmaAdvect, plasmaAdvectScale * speed);
        playerMat.SetFloat(_PlasmaTurb, plasmaTurbulence * Mathf.Lerp(0.5f, 1.5f, plasmaFill));

        // 3) ARC OUTLINE (shader-based)
        bool arcOn = speed >= arcAppearSpeed;
        playerMat.SetFloat(_ArcEnable, arcOn ? 1f : 0f);
        playerMat.SetVector(_ArcMidDir, new Vector4(dir.x, dir.y, dir.z, 0));

        // arc length/thickness scale with normalized speed (0..1 at some reference)
        float normSpeed = Mathf.Clamp01(speed / 12f); // tune denominator to your top speed
        float arcLenDeg = Mathf.Clamp(arcMaxLengthDeg * arcLengthBySpeed.Evaluate(normSpeed), 0f, 179f);
        // We send cosine of half-length for cheap angular test in shader
        float halfLenCos = Mathf.Cos(arcLenDeg * 0.5f * Mathf.Deg2Rad);
        playerMat.SetFloat(_ArcLengthCos, halfLenCos);
        playerMat.SetFloat(_ArcThickness, arcThicknessBySpeed.Evaluate(normSpeed));

        // 2) ELASTIC BODY (non-uniform scale in velocity direction, smooth return)
        // Build a basis: forward = dir (velocity), choose any perpendicular for up/right
        Vector3 fwd = dir;
        Vector3 up = Vector3.up;
        if (Mathf.Abs(Vector3.Dot(fwd, up)) > 0.95f) up = Vector3.forward; // avoid colinear
        Vector3 right = Vector3.Cross(up, fwd).normalized;
        up = Vector3.Cross(fwd, right).normalized;

        float stretch = Mathf.Min(maxStretch, stretchFactor * speed);
        float compress = Mathf.Min(maxStretch, compressFactor * speed);

        // target scale in local player space: 1+stretch along forward, 1-compress sideways
        // (approximate by building a directional scaling and applying as world-space)
        Vector3 sF = Vector3.one + new Vector3(stretch, stretch, stretch) * 0f; // base
        // project scale along our axes
        float sForward = 1f + stretch;
        float sSide = Mathf.Max(0.7f, 1f - compress);

        // compose a world-space scale by rotating the object so forward = fwd, apply scale, rotate back
        // simpler: lerp between baseScale and a skew approximation using local z-forward (if your model uses z-forward).
        // Most reliable cross-pipeline approach: just slightly skew by setting localScale with a directional bias in Update:
       // targetScale = new Vector3(baseScale.x * sSide, baseScale.y * sSide, baseScale.z * sForward);
       // transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * returnSmooth);
    }

    /// <summary>Call this when a strong impact happens. Pass contact normal (world), or just call with forward if unknown.</summary>
    public void VisualImpact(Vector3 hitNormal)
    {
        // quick perpendicular squash
        Vector3 loc = transform.InverseTransformDirection(hitNormal);
        float x = Mathf.Abs(loc.x);
        float y = Mathf.Abs(loc.y);
        float z = Mathf.Abs(loc.z);

        // squash perpendicular axes
        Vector3 s = transform.localScale;
        if (x > y && x > z)      s.x = s.x * (1f - impactDeform);
        else if (y > x && y > z) s.y = s.y * (1f - impactDeform);
        else                     s.z = s.z * (1f - impactDeform);
        transform.localScale = s;
        // spring back
        StopAllCoroutines();
        StartCoroutine(RecoverScale());
    }

    System.Collections.IEnumerator RecoverScale()
    {
        float t = 0f;
        Vector3 start = transform.localScale;
        while (t < 1f)
        {
            t += Time.deltaTime * (impactRecover * 0.1f);
            transform.localScale = Vector3.Lerp(start, targetScale, t);
            yield return null;
        }
        transform.localScale = targetScale;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!visualizeForwardGizmo || rb == null) return;
        Gizmos.color = gizmoColor;
        Vector3 p = transform.position;
        Gizmos.DrawLine(p, p + rb.linearVelocity.normalized * 1.0f);
    }
#endif
}
