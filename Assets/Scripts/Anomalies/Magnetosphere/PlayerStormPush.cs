using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class PlayerStormPush : MonoBehaviour
{
    [Header("Auto-wired refs (optional overrides)")]
    [SerializeField] private DynamoStormController storm;
    [SerializeField] private PlayerControllerScript player;
    [Tooltip("Optional observed MHD flow. Assign explicitly to use scientific Dynamo playback.")]
    [SerializeField] private Massive.Dynamo.ScientificMagnetosphere scientificField;

    [Header("Storm Current")]
    [Tooltip("Target drift speed at stormFactor=1.")]
    [SerializeField] private float stormMaxDriftSpeed = 6f;

    [Tooltip("How quickly velocity chases the storm current (higher = sludgier).")]
    [SerializeField] private float currentResponse = 6f;

    [Tooltip("Clamp on acceleration so it can't spike.")]
    [SerializeField] private float maxAcceleration = 30f;

    [Tooltip("Smooth the storm factor so 10Hz updates don't feel steppy.")]
    [SerializeField] private float factorSmoothTime = 0.10f;

    private Rigidbody rb;
    private float fSmoothed;
    private float fVel;

    public void SetScientificField(Massive.Dynamo.ScientificMagnetosphere field) => scientificField = field;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Auto-find player controller on this GO (or parent if your setup nests scripts)
        if (!player)
        {
            player = GetComponent<PlayerControllerScript>();
            if (!player) player = GetComponentInParent<PlayerControllerScript>();
        }

        // Auto-find the one storm controller in the scene
        if (!storm)
            storm = FindFirstObjectByType<DynamoStormController>();
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        if (player && player.temporarilyEliminated)
            return;

        if (scientificField != null)
        {
            // A configured scientific source owns flow, including missing/out-of-domain behavior.
            if (scientificField.TryGetPlanarDrift(rb.position, stormMaxDriftSpeed, out Vector3 drift))
            {
                Vector3 planar = rb.linearVelocity;
                planar.y = 0;
                Vector3 acceleration = Vector3.ClampMagnitude(
                    (drift - planar) * Mathf.Max(.01f, currentResponse), Mathf.Max(0, maxAcceleration));
                if (player && acceleration.sqrMagnitude > .000001f)
                    player.ProtectActionMomentum(.1f);
                rb.AddForce(acceleration, ForceMode.Acceleration);
            }
            return;
        }
        if (!storm) return;

        Transform key = (player != null) ? player.transform : transform;
        float f = storm.GetStormFactor01(key);
        fSmoothed = Mathf.SmoothDamp(fSmoothed, f, ref fVel, factorSmoothTime);

        if (fSmoothed <= 0.0001f) return;

        Vector3 dir = storm.GetStormFlowDirWS();
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-6f) return;
        dir.Normalize();

        Vector3 targetPlanarVel = dir * (stormMaxDriftSpeed * fSmoothed);

        Vector3 v = rb.linearVelocity;
        v.y = 0f;

        Vector3 accel = (targetPlanarVel - v) * Mathf.Max(0.01f, currentResponse);

        float aMag = accel.magnitude;
        if (aMag > maxAcceleration)
            accel *= (maxAcceleration / aMag);

        if (player && accel.sqrMagnitude > .000001f)
            player.ProtectActionMomentum(.1f);
        rb.AddForce(accel, ForceMode.Acceleration);
    }
}
