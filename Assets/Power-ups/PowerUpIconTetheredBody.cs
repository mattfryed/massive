using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class PowerUpIconTetheredBody : MonoBehaviour
{
    [Header("Tether")]
    [Tooltip("How far (world units) the icon is allowed to drift from its spawn point.")]
    [Min(0f)] public float maxDriftRadius = 0.75f;

    [Tooltip("Spring strength pulling back toward the anchor. Higher = snappier return.")]
    [Min(0f)] public float spring = 35f;

    [Tooltip("Damping against current velocity. Higher = less wobble.")]
    [Min(0f)] public float damping = 10f;

    [Header("Snap")]
    [Tooltip("If close enough + slow enough, snap exactly to anchor and sleep.")]
    public bool snapWhenSettled = true;

    [Min(0f)] public float snapDistance = 0.03f;
    [Min(0f)] public float snapSpeed = 0.08f;

    [Header("Plane Lock")]
    public bool lockToXZPlane = true;

    private Rigidbody _rb;
    private Vector3 _anchorPos;
    private float _anchorY;

    // ✅ missing field (your compile error)
    private bool _pendingAnchorInit;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        if (!_rb) _rb = GetComponent<Rigidbody>();

        // Defer anchor capture until first FixedUpdate
        _pendingAnchorInit = true;

        // Sensible defaults for this use-case (safe if you already set them in inspector)
        _rb.useGravity = false;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;

        // You probably want these constraints for MASSIVE pickups:
        _rb.constraints |= RigidbodyConstraints.FreezePositionY;
        _rb.constraints |= RigidbodyConstraints.FreezeRotationX;
        _rb.constraints |= RigidbodyConstraints.FreezeRotationZ;
    }

    private void FixedUpdate()
    {
        if (_rb == null) return;

        if (_pendingAnchorInit)
        {
            // Capture after any spawner reposition has happened
            _anchorPos = _rb.position;
            _anchorY = _anchorPos.y;

            // Clear any leftover motion from pooling / teleporting
            _rb.linearVelocity = Vector3.zero;   // Unity 6+
            _rb.angularVelocity = Vector3.zero;

            _pendingAnchorInit = false;
            return; // don’t apply spring on the same tick you capture
        }

        Vector3 pos = _rb.position;

        if (lockToXZPlane)
        {
            pos.y = _anchorY;
        }

        Vector3 offset = pos - _anchorPos;

        // Clamp drift so it can’t be shoved too far
        float r = offset.magnitude;
        if (maxDriftRadius > 0f && r > maxDriftRadius)
        {
            Vector3 clamped = _anchorPos + offset.normalized * maxDriftRadius;
            if (lockToXZPlane) clamped.y = _anchorY;

            _rb.MovePosition(clamped);

            // Kill outward velocity so it doesn't keep trying to escape the leash
            Vector3 v = _rb.linearVelocity;
            v.y = 0f;
            _rb.linearVelocity = v;

            pos = clamped;
            offset = pos - _anchorPos;
        }

        // Spring back toward anchor
        Vector3 vel = _rb.linearVelocity;
        if (lockToXZPlane)
        {
            offset.y = 0f;
            vel.y = 0f;
        }

        Vector3 accel = (-offset * spring) + (-vel * damping);
        _rb.AddForce(accel, ForceMode.Acceleration);

        // Optional snap when settled
        if (snapWhenSettled)
        {
            float dist = offset.magnitude;
            float speed = vel.magnitude;

            if (dist <= snapDistance && speed <= snapSpeed)
            {
                Vector3 snapPos = _anchorPos;
                if (lockToXZPlane) snapPos.y = _anchorY;

                _rb.MovePosition(snapPos);
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                _rb.Sleep();
            }
        }
    }

    // Call this after you spawn/reposition the object to define the new "home"
    public void ResetAnchorHere()
    {
        if (!_rb) _rb = GetComponent<Rigidbody>();

        _anchorPos = _rb ? _rb.position : transform.position;
        _anchorY = _anchorPos.y;

        // If spawner sets anchor explicitly, don’t recapture next FixedUpdate
        _pendingAnchorInit = false;
    }
}
