using UnityEngine;

namespace Massive.Enemies
{
    /// <summary>
    /// Lightweight obstacle avoidance for Rigidbody-driven enemies.
    ///
    /// This is intentionally NOT a full navmesh/pathfinding solution.
    /// It provides convincing "go around" behavior using a small set of feeler raycasts/spherecasts:
    /// - Forward feeler detects an impending obstacle.
    /// - Left/right feelers choose a preferred side (more clearance).
    /// - A steering vector is generated that pushes tangentially along the obstacle surface,
    ///   with a little normal push to avoid scraping.
    ///
    /// Works great for top-down arenas with a small number of solid obstacles.
    ///
    /// Tip: you can reuse your "NoSpawn" trigger volumes as *soft avoidance* zones by
    /// putting them on a layer included in <see cref="obstacleMask"/> and enabling
    /// <see cref="triggerInteraction"/> = Collide.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyObstacleAvoidance : MonoBehaviour
    {
        [Header("Mask")]
        [Tooltip("Layers treated as obstacles (walls, obstacles, and optionally NoSpawn/NoGo trigger volumes).")]
        [SerializeField] private LayerMask obstacleMask = ~0;
        [Tooltip("Whether avoidance should consider trigger colliders (use Collide to include NoSpawn volumes).")]
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

        [Header("Cast")]
        [Tooltip("World Y offset from the enemy position for casts (prevents hitting the floor, if any).")]
        [SerializeField, Min(0f)] private float rayHeight = 0.15f;
        [Tooltip("How far ahead we look for obstacles.")]
        [SerializeField, Min(0.05f)] private float lookAheadDistance = 2.25f;
        [Tooltip("Angle (degrees) for side feelers.")]
        [SerializeField, Range(5f, 85f)] private float sideAngleDeg = 28f;
        [Tooltip("Multiplier for side feeler length vs forward feeler.")]
        [SerializeField, Range(0.2f, 1.5f)] private float sideLengthMul = 0.85f;
        [Tooltip("Use a spherecast instead of a raycast. Set to ~enemy radius * 0.4 for 'fatter' avoidance.")]
        [SerializeField, Min(0f)] private float sphereCastRadius = 0f;

        [Header("Steering")]
        [Tooltip("Overall strength of the avoidance steering.")]
        [SerializeField, Range(0f, 5f)] private float avoidanceStrength = 1.35f;
        [Tooltip("How much to push away from the obstacle normal.")]
        [SerializeField, Range(0f, 3f)] private float normalPush = 0.65f;
        [Tooltip("How much to push along the obstacle tangent (this makes agents 'go around').")]
        [SerializeField, Range(0f, 5f)] private float tangentPush = 1.15f;
        [Tooltip("How long to keep a chosen left/right side when the forward path is blocked.")]
        [SerializeField, Range(0f, 2f)] private float sideLockSeconds = 0.45f;

        [Header("Debug")]
        [SerializeField] private bool debugDraw = false;

        // runtime
        private int _sideSign = 1; // +1 = right, -1 = left
        private float _sideLockUntil = -Mathf.Infinity;

        public LayerMask ObstacleMask
        {
            get => obstacleMask;
            set => obstacleMask = value;
        }

        public float LookAheadDistance
        {
            get => lookAheadDistance;
            set => lookAheadDistance = Mathf.Max(0.05f, value);
        }

        /// <summary>
        /// Returns a direction close to <paramref name="desiredDirWS"/>, but steered to go around obstacles.
        /// </summary>
        public Vector3 AdjustDirection(Vector3 desiredDirWS, out bool isAvoiding)
        {
            // Flatten to XZ (top-down plane)
            desiredDirWS.y = 0f;
            if (desiredDirWS.sqrMagnitude < 0.000001f)
            {
                isAvoiding = false;
                return Vector3.zero;
            }

            Vector3 desired = desiredDirWS.normalized;
            Vector3 origin = transform.position + Vector3.up * rayHeight;

            // Build feelers
            Vector3 dirF = desired;
            Vector3 dirL = Quaternion.AngleAxis(-sideAngleDeg, Vector3.up) * desired;
            Vector3 dirR = Quaternion.AngleAxis(+sideAngleDeg, Vector3.up) * desired;

            float lenF = lookAheadDistance;
            float lenS = lookAheadDistance * sideLengthMul;

            bool hitF = Cast(origin, dirF, lenF, out RaycastHit hitFwd);
            float dF = hitF ? hitFwd.distance : lenF;

            bool hitL = Cast(origin, dirL, lenS, out RaycastHit hitLeft);
            float dL = hitL ? hitLeft.distance : lenS;

            bool hitR = Cast(origin, dirR, lenS, out RaycastHit hitRight);
            float dR = hitR ? hitRight.distance : lenS;

            isAvoiding = hitF || hitL || hitR;
            if (!isAvoiding)
            {
                if (debugDraw)
                {
                    Debug.DrawRay(origin, dirF * lenF, Color.gray);
                    Debug.DrawRay(origin, dirL * lenS, Color.gray);
                    Debug.DrawRay(origin, dirR * lenS, Color.gray);
                }
                return desired;
            }

            // If the forward path is blocked, lock a preferred side based on which side has more clearance.
            if (hitF)
            {
                if (Time.time >= _sideLockUntil)
                {
                    _sideSign = (dR > dL) ? +1 : -1;
                    _sideLockUntil = Time.time + Mathf.Max(0f, sideLockSeconds);
                }
            }

            // Steering vector
            Vector3 steer = Vector3.zero;

            // Forward obstacle response is the strongest (this is the 'don't crash' part)
            if (hitF)
            {
                float s = Strength01(lenF, dF);
                Vector3 n = hitFwd.normal;
                n.y = 0f;
                if (n.sqrMagnitude > 0.000001f) n.Normalize();

                Vector3 tangent = Vector3.Cross(Vector3.up, n);
                tangent.y = 0f;
                if (tangent.sqrMagnitude > 0.000001f) tangent.Normalize();
                tangent *= _sideSign;

                steer += n * (normalPush * s) + tangent * (tangentPush * s);
            }
            else
            {
                // Side-only hits: gently bias away so we don't scrape.
                if (hitL)
                {
                    float s = Strength01(lenS, dL) * 0.55f;
                    Vector3 n = hitLeft.normal;
                    n.y = 0f;
                    if (n.sqrMagnitude > 0.000001f) n.Normalize();
                    steer += n * (normalPush * s);
                }
                if (hitR)
                {
                    float s = Strength01(lenS, dR) * 0.55f;
                    Vector3 n = hitRight.normal;
                    n.y = 0f;
                    if (n.sqrMagnitude > 0.000001f) n.Normalize();
                    steer += n * (normalPush * s);
                }
            }

            Vector3 outDir = desired + steer * Mathf.Max(0f, avoidanceStrength);
            outDir.y = 0f;
            if (outDir.sqrMagnitude < 0.000001f)
                outDir = desired;
            else
                outDir.Normalize();

            if (debugDraw)
            {
                Debug.DrawRay(origin, dirF * lenF, hitF ? Color.red : Color.gray);
                Debug.DrawRay(origin, dirL * lenS, hitL ? Color.yellow : Color.gray);
                Debug.DrawRay(origin, dirR * lenS, hitR ? Color.yellow : Color.gray);
                Debug.DrawRay(origin, outDir * lenF, Color.cyan);
            }

            return outDir;
        }

        /// <summary>
        /// Convenience for "can I see the target in this direction" checks.
        /// Returns true if an obstacle is hit within <paramref name="distance"/>.
        /// </summary>
        public bool HasObstacleInDirection(Vector3 dirWS, float distance, out RaycastHit hit)
        {
            dirWS.y = 0f;
            if (dirWS.sqrMagnitude < 0.000001f)
            {
                hit = default;
                return false;
            }

            Vector3 origin = transform.position + Vector3.up * rayHeight;
            float len = Mathf.Max(0.01f, distance);
            Vector3 d = dirWS.normalized;
            return Cast(origin, d, len, out hit);
        }

        private bool Cast(Vector3 origin, Vector3 dir, float length, out RaycastHit hit)
        {
            if (sphereCastRadius > 0.0001f)
            {
                return Physics.SphereCast(origin, sphereCastRadius, dir, out hit, length, obstacleMask, triggerInteraction);
            }
            return Physics.Raycast(origin, dir, out hit, length, obstacleMask, triggerInteraction);
        }

        private static float Strength01(float maxLen, float hitDist)
        {
            if (maxLen <= 0.0001f) return 1f;
            float t = Mathf.Clamp01((maxLen - Mathf.Clamp(hitDist, 0f, maxLen)) / maxLen);
            // Ease in a bit so it doesn't feel too twitchy when just grazing.
            return t * t;
        }
    }
}
