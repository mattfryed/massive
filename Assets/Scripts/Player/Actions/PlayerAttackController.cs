using System.Collections;
using UnityEngine;
using UnityEngine.Events;


namespace Massive.Player
{
    [DisallowMultipleComponent]
    public class PlayerAttackController : MonoBehaviour
    {
        private Rigidbody rb;
        // Public read-only events for listeners (melee, VFX, etc.)
        public UnityEvent<AttackStage> OnStageStarted  => onStageStarted;
        public UnityEvent<AttackStage> OnStageCompleted => onStageCompleted;

        [Header("Profile")]
        [SerializeField]
        private PlayerAttackProfile attackProfile = null;

        [Header("External Input")]
        [Tooltip("2D move input in X/Z plane, supplied by external input (Rewired).")]
        [SerializeField]
        private Vector2 externalMoveInput;

        private Vector2 lastNonZeroMoveDir = Vector2.right; // default: +X

        public Vector2 ExternalMoveInput
        {
            get => externalMoveInput;
            set
            {
                externalMoveInput = value;

                // Remember last non-zero direction (normalized)
                if (externalMoveInput.sqrMagnitude > 0.0001f)
                {
                    lastNonZeroMoveDir = externalMoveInput.normalized;
                }
            }
        }

        [Header("Cooldown")]
        [SerializeField]
        private float attackCooldown = 0.25f;  // seconds, tweak in inspector

        private float lastAttackEndTime = -Mathf.Infinity;


    // ===== Lock-on / Aim Assist (Stage 0 lunge) =====
    [Header("Lunge Lock-On Assist")]
    [SerializeField] private bool lockOnEnabled = true;

    [SerializeField, Range(0f, 90f)]
    private float lockOnConeHalfAngleDeg = 25f;

    // 0 = use stage TravelDistance
    [SerializeField] private float lockOnMaxDistanceOverride = 0f;

    // 1 = fully snap to target dir, 0.6-0.85 feels more like "assist"
    [SerializeField, Range(0f, 1f)]
    private float lockOnDirectionBlend = 0.85f;

    [SerializeField] private bool lockOnIgnoreSameTeam = true;
    [SerializeField] private bool lockOnIgnoreEliminated = true;

    // Put players on a Player layer if possible; otherwise leave as Everything and rely on filtering.
    [SerializeField] private LayerMask lockOnTargetMask = ~0;

    // Optional but recommended if you have solid walls/obstacles
    [SerializeField] private bool lockOnRequireLineOfSight = false;
    [SerializeField] private LayerMask lockOnLineOfSightMask = ~0;

        // Runtime state (do NOT mutate the stage asset)
    private float stageTravelDistanceWS;
    private float stageStopDistanceWS;

    private PlayerControllerScript ownerController;
    private PlayerControllerScript lockedTarget;
    private readonly Collider[] _lockHits = new Collider[16];



        [Header("Combo Tuning")]
        [SerializeField, Min(0f)]
        private float comboInputBuffer = 0.15f;

        [Header("Combo Window")]
        [Tooltip("If true, combo presses are accepted AFTER the stage's activation window (useful for recovery-cancel style combos).\nIf false, combos use the activation window itself (legacy behavior).")]
        [SerializeField] private bool comboWindowAfterActivationWindow = true;

        [Tooltip("Normalized end of combo window when comboWindowAfterActivationWindow is true. 1 = end of stage.")]
        [SerializeField, Range(0f, 1f)] private float comboWindowEndNormalized = 1f;

        [Header("Combo Swipe")]
        [Tooltip("Easing for the swipe arc across the stage. X=time(0..1), Y=lerp(0..1).")]
        [SerializeField] private AnimationCurve swipeArcCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Attack Visual Direction")]
        [Tooltip("If true, visuals (trail direction, sword arc base) follow the current CombatFacingRoot direction (ForwardReference.right).\nIf false, visuals follow the stage-locked direction used for dash motion.")]
        [SerializeField] private bool visualDirectionFollowsCombatFacing = true;

        [Header("Scene References")]
        [SerializeField]
        public Transform forwardReference = null;

        [Header("Events (internal)")]
        [SerializeField]
        private AttackStageUnityEvent onStageStarted = new AttackStageUnityEvent();

        [SerializeField]
        private AttackStageUnityEvent onStageCompleted = new AttackStageUnityEvent();

        private CharacterController characterController;
        private AttackStage currentStage;
        private int currentStageIndex = -1;
        private float stageTimer;
        private float currentDistanceProgress;
        private float previousNormalizedTime;
        private bool isAttacking;
        private bool comboQueued;
        private float lastAttackPressTime = float.NegativeInfinity;
        private int swipeDirection = 1;
        private Vector3 stageAttackDirectionWS = Vector3.right;

        // Combo-swipe arc offset (degrees). 0 for non-swipe stages.
        private float currentWeaponYawOffsetDeg = 0f;

        public bool IsAttacking => isAttacking;
        public AttackStage CurrentStage => currentStage;
        public int CurrentStageIndex => currentStageIndex;
        public float StageNormalizedTime => currentStage != null ? Mathf.Clamp01(stageTimer / currentStage.Duration) : 0f;

        public Transform ForwardReference => forwardReference != null ? forwardReference : transform;

        private void Awake()
    {
            characterController = GetComponent<CharacterController>();
            rb = GetComponent<Rigidbody>();
            ownerController = GetComponent<PlayerControllerScript>();

            // existing forwardReference auto-wire...


            // Auto-wire forward reference
            if (forwardReference == null)
            {
                var pvc = GetComponent<PlayerVisualController>();
                if (pvc != null)
                {
                    if (pvc.gameplayFacing != null) forwardReference = pvc.gameplayFacing;
                    else if (pvc.visuals != null) forwardReference = pvc.visuals;
                }
            }



        }


        private void Update()
        {
            if (attackProfile == null || attackProfile.Stages.Count == 0)
                return;

            // Starting attacks is done externally via BeginAttack / RegisterAttackPress.
            if (!isAttacking)
                return;

            UpdateStage(Time.deltaTime);
        }


        /// Call this from external input when the attack button is pressed.
        /// Handles combo queuing and input buffering.
        /// </summary>
        public void RegisterAttackPress()
        {
            // Ignore presses when idle; they only start the first stage
            if (!isAttacking)
                return;

            // Now we know we're mid-attack, so this is a potential combo press
            lastAttackPressTime = Time.time;

            if (currentStage != null && currentStage.AllowComboCancel && !comboQueued)
            {
                float normalized = Mathf.Clamp01(stageTimer / currentStage.Duration);
                if (IsWithinComboWindow(normalized))
                {
                    comboQueued = true; // immediate if we're already in the window
                }
            }
        }



        /// Call this from external input to begin a new attack sequence.
        /// </summary>
        public void BeginAttack()
        {
            if (isAttacking || attackProfile == null || attackProfile.Stages.Count == 0)
                return;

            // cooldown gate
            if (Time.time < lastAttackEndTime + attackCooldown)
                return;

            StartStage(0);
        }

        private void UpdateStage(float deltaTime)
        {
            if (currentStage == null)
            {
                EndAttackSequence();
                return;
            }

            // BUGFIX from your current file: this must accumulate, not overwrite
            stageTimer += deltaTime;

            float duration = currentStage.Duration;
            float normalized = Mathf.Clamp01(stageTimer / duration);
            float deltaNormalized = Mathf.Clamp01(normalized - previousNormalizedTime);
            previousNormalizedTime = normalized;

            ApplyStageMotion(normalized, deltaNormalized);
            UpdateComboQueue(normalized);

            if (stageTimer >= duration)
            {
                CompleteStage();
            }
        }

        private Vector3 ComputeAttackDirectionFromInput()
{
    // same logic as your old GetAttackDirection, but without using stageAttackDirection
    if (externalMoveInput.sqrMagnitude > 0.001f)
    {
        var dir = new Vector3(externalMoveInput.x, 0f, externalMoveInput.y);
        if (dir.sqrMagnitude > 0.0001f) return dir.normalized;
    }

    if (lastNonZeroMoveDir.sqrMagnitude > 0.0001f)
    {
        var dir = new Vector3(lastNonZeroMoveDir.x, 0f, lastNonZeroMoveDir.y);
        if (dir.sqrMagnitude > 0.0001f) return dir.normalized;
    }

    // NOTE: in MASSIVE, the blob front is local +X, so we use .right as our gameplay "forward".
    Vector3 fwd = ForwardReference.right;
    fwd.y = 0f;
    if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.right;
    return fwd.normalized;
}

        private Vector3 GetCombatFacingDirectionWS()
        {
            // NOTE: in MASSIVE, the blob front is local +X, so we use .right as our gameplay "forward".
            Vector3 fwd = ForwardReference.right;
            fwd.y = 0f;

            if (fwd.sqrMagnitude < 0.0001f)
                fwd = Vector3.right;

            return fwd.normalized;
        }


private Vector3 GetAttackDirection()
{
    // NEW: while attacking, use the locked direction for this stage
    if (isAttacking && currentStage != null)
        return stageAttackDirectionWS;

    // otherwise, compute from input
    return ComputeAttackDirectionFromInput();
}

        /// <summary>
        /// Direction used for movement/dash during the current stage (locked at stage start).
        /// </summary>
        public Vector3 CurrentAttackDirectionWS => GetAttackDirection();

        /// <summary>
        /// Visual/weapon direction (includes combo-swipe arc while StageType == ComboSwipe).
        /// Use this for trails / sword visuals / hitboxes that should arc.
        /// </summary>
        public Vector3 CurrentAttackVisualDirectionWS
        {
            get
            {
                Vector3 baseDir = visualDirectionFollowsCombatFacing
                    ? GetCombatFacingDirectionWS()
                    : GetAttackDirection();

                // Only apply arc during the swipe stage.
                if (currentStage != null && currentStage.StageType == AttackStageType.ComboSwipe && Mathf.Abs(currentWeaponYawOffsetDeg) > 0.001f)
                {
                    return Quaternion.AngleAxis(currentWeaponYawOffsetDeg, Vector3.up) * baseDir;
                }

                return baseDir;
            }
        }

        /// <summary>
        /// Current combat-facing direction (ForwardReference.right, flattened to XZ).
        /// </summary>
        public Vector3 CombatFacingDirectionWS => GetCombatFacingDirectionWS();

        public float CurrentWeaponYawOffsetDeg => currentWeaponYawOffsetDeg;

        public int CurrentSwipeDirection => swipeDirection;



        private void ApplyStageMotion(float normalized, float deltaNormalized)
        {
    float targetDistance = stageTravelDistanceWS * currentStage.DistanceCurve.Evaluate(normalized);

    // If we locked a target closer than the full travel, stop exactly at that initial distance.
    if (stageStopDistanceWS < stageTravelDistanceWS)
        targetDistance = Mathf.Min(targetDistance, stageStopDistanceWS);

    float deltaDistance = targetDistance - currentDistanceProgress;
    currentDistanceProgress = targetDistance;

    if (!Mathf.Approximately(deltaDistance, 0f))
    {
        Vector3 displacement = GetAttackDirection() * deltaDistance;
        displacement.y = 0f;
        Move(displacement);
    }

            // Drive the combo-swipe arc (visuals + hitboxes) without rotating the player root.
            UpdateSwipeArc(normalized);
        }

        private void Move(Vector3 displacement)
        {
            // Prefer Rigidbody-based movement for proper collisions
            if (rb != null)
            {
                // MovePosition asks the physics engine to move & resolve collisions
                rb.MovePosition(rb.position + displacement);
            }
            else if (characterController != null)
            {
                characterController.Move(displacement);
            }
            else
            {
                // Last resort: direct transform move
                transform.position += displacement;
            }
        }


        private void UpdateSwipeArc(float stageNormalized)
        {
            // Reset by default.
            currentWeaponYawOffsetDeg = 0f;

            if (currentStage == null)
                return;

            if (currentStage.StageType != AttackStageType.ComboSwipe)
                return;

            if (Mathf.Approximately(currentStage.RotationArc, 0f))
                return;

            // Allow dynamic swipe direction changes if the player changes input mid-swing.
            int desiredDirection = CalculateSwipeDirection();
            if (desiredDirection != 0)
            {
                swipeDirection = desiredDirection;
            }

            // Drive a symmetric arc around the base attack direction.
            // Example: RotationArc=60 => starts at -30 and ends at +30 degrees.
            float arcHalf = currentStage.RotationArc * 0.5f;
            float t = Mathf.Clamp01(stageNormalized);

            float eased = swipeArcCurve != null ? swipeArcCurve.Evaluate(t) : t;
            float baseOffset = Mathf.Lerp(-arcHalf, +arcHalf, eased);

            // swipeDirection = +1 means left->right; -1 flips it.
            currentWeaponYawOffsetDeg = baseOffset * swipeDirection;
        }

        private void UpdateComboQueue(float normalized)
        {
            if (!currentStage.AllowComboCancel || comboQueued)
                return;

            if (!IsWithinComboWindow(normalized))
                return;

            if (Time.time - lastAttackPressTime <= comboInputBuffer)
            {
                comboQueued = true;
            }
        }

        private bool IsWithinComboWindow(float normalized)
        {
            if (currentStage == null)
                return false;

            float start;
            float end;

            if (comboWindowAfterActivationWindow)
            {
                // "After-window" combos: let the player chain during recovery.
                start = currentStage.ActivationEndNormalized;
                end = Mathf.Clamp01(comboWindowEndNormalized);

                // Safety: ensure end is never before start.
                if (end < start)
                    end = 1f;
            }
            else
            {
                // Legacy behavior: combos only within the activation window.
                start = currentStage.ActivationStartNormalized;
                end = currentStage.ActivationEndNormalized;
            }

            return normalized >= start && normalized <= end;
        }

        private void CompleteStage()
        {
            AttackStage finishedStage = currentStage;
            onStageCompleted.Invoke(finishedStage);

            int nextStageIndex = currentStageIndex + 1;
            bool hasNextStage = attackProfile.GetStage(nextStageIndex) != null;

            if (comboQueued && hasNextStage)
            {
                StartStage(nextStageIndex);
            }
            else
            {
                EndAttackSequence();
            }
        }

        private void StartStage(int stageIndex)
        {
            AttackStage stage = attackProfile.GetStage(stageIndex);
            if (stage == null)
            {
                EndAttackSequence();
                return;
            }

            currentStage = stage;
            currentStageIndex = stageIndex;
            stageTimer = 0f;
            currentDistanceProgress = 0f;
            previousNormalizedTime = 0f;
            comboQueued = false;
            isAttacking = true;

            // Clear any buffered press that was used to ENTER this stage,
            // so we don't accidentally auto-chain into the next stage.
            lastAttackPressTime = float.NegativeInfinity;

            // Reset swipe offset at stage start.
            currentWeaponYawOffsetDeg = 0f;

            // Lock attack direction for this stage (movement direction).
            stageAttackDirectionWS = ComputeAttackDirectionFromInput();

            // Per-stage travel (so we never edit the AttackStage asset)
            stageTravelDistanceWS = currentStage.TravelDistance;
            stageStopDistanceWS = stageTravelDistanceWS;
            lockedTarget = null;

            // Only apply lock-on to the primary lunge (stage 0)
            if (lockOnEnabled && stageIndex == 0)
            {
                TryApplyLungeLockOn();
            }

            DetermineSwipeDirection();

            // GPU VFX (AttackTrailGPU) are driven by OnStageStarted
            onStageStarted.Invoke(stage);
        }

        

        private void DetermineSwipeDirection()
        {
            int desiredDirection = CalculateSwipeDirection();
            if (desiredDirection == 0)
            {
                if (swipeDirection != 1 && swipeDirection != -1)
                {
                    swipeDirection = 1;
                }
                return;
            }

            swipeDirection = desiredDirection;
        }

        private int CalculateSwipeDirection()
        {
            // NOTE: in MASSIVE, the blob front is local +X, so we use .right as our gameplay "forward".
            Vector3 referenceForward = ForwardReference.right;
            referenceForward.y = 0f;
            if (referenceForward.sqrMagnitude < 0.001f)
            {
                referenceForward = Vector3.right;
            }
            else
            {
                referenceForward.Normalize();
            }

            Vector2 move2D = ExternalMoveInput;
            if (move2D.sqrMagnitude < 0.001f)
                return 0;

            Vector3 input = new Vector3(move2D.x, 0f, move2D.y).normalized;

            float direction = Vector3.Cross(referenceForward, input).y;
            if (Mathf.Approximately(direction, 0f))
                return 0;

            return direction > 0f ? 1 : -1;
        }



        private void EndAttackSequence()
        {
            currentStage = null;
            currentStageIndex = -1;
            stageTimer = 0f;
            currentDistanceProgress = 0f;
            previousNormalizedTime = 0f;
            comboQueued = false;
            isAttacking = false;
            currentWeaponYawOffsetDeg = 0f;
            lastAttackEndTime = Time.time;
        }

        public void CancelAttack(bool signalComplete = false)
        {
            if (!isAttacking)
                return;

            if (signalComplete && currentStage != null)
            {
                onStageCompleted.Invoke(currentStage);
            }

            EndAttackSequence();
        }


    private void TryApplyLungeLockOn()
{
    Vector3 origin = (rb != null) ? rb.position : transform.position;

    Vector3 aimDir = stageAttackDirectionWS;
    aimDir.y = 0f;
    if (aimDir.sqrMagnitude < 0.0001f) return;
    aimDir.Normalize();

    float maxDist = (lockOnMaxDistanceOverride > 0f) ? lockOnMaxDistanceOverride : stageTravelDistanceWS;
    if (maxDist <= 0.0001f) return;

    float cosLimit = Mathf.Cos(lockOnConeHalfAngleDeg * Mathf.Deg2Rad);

    int count = Physics.OverlapSphereNonAlloc(
        origin,
        maxDist,
        _lockHits,
        lockOnTargetMask,
        QueryTriggerInteraction.Collide // safer: includes triggers if your player collider is trigger for some reason
    );

    float bestScore = float.NegativeInfinity;
    PlayerControllerScript best = null;
    Vector3 bestDir = aimDir;
    float bestDist = 0f;

    for (int i = 0; i < count; i++)
    {
        Collider col = _lockHits[i];
        if (col == null) continue;

        // Your PlayerMelee expects PlayerControllerScript on the same object as the collider/tagged "Player".
        // If your body collider is on a child, change this to GetComponentInParent<PlayerControllerScript>().
        var pcs = col.GetComponent<PlayerControllerScript>();
        if (pcs == null) continue;
        if (pcs == ownerController) continue;

        if (lockOnIgnoreEliminated && pcs.temporarilyEliminated) continue;
        if (lockOnIgnoreSameTeam && ownerController != null && pcs.teamID == ownerController.teamID) continue;

        Vector3 to = pcs.transform.position - origin;
        to.y = 0f;

        float dist = to.magnitude;
        if (dist < 0.001f) continue;

        Vector3 dir = to / dist;

        // Cone check
        float dot = Vector3.Dot(aimDir, dir);
        if (dot < cosLimit) continue;

        // Optional line of sight check
        if (lockOnRequireLineOfSight)
        {
            if (Physics.Raycast(origin, dir, out RaycastHit hit, dist, lockOnLineOfSightMask, QueryTriggerInteraction.Ignore))
            {
                var hitPcs = hit.collider.GetComponentInParent<PlayerControllerScript>();
                if (hitPcs != pcs) continue;
            }
        }

        // Score: prioritize "most centered in cone", then closeness
        float distance01 = 1f - Mathf.Clamp01(dist / maxDist);
        float score = dot * 0.75f + distance01 * 0.25f;

        if (score > bestScore)
        {
            bestScore = score;
            best = pcs;
            bestDir = dir;
            bestDist = dist;
        }
    }

    if (best == null) return;

    lockedTarget = best;

    // Blend for feel (avoid “magnet snap”)
    Vector3 blendedDir = Vector3.Slerp(aimDir, bestDir, Mathf.Clamp01(lockOnDirectionBlend));
    blendedDir.y = 0f;
    if (blendedDir.sqrMagnitude > 0.0001f)
        stageAttackDirectionWS = blendedDir.normalized;

    // Stop at initial distance (but preserve speed by clamping, not scaling travelDistance)
    stageStopDistanceWS = Mathf.Clamp(bestDist, 0f, stageTravelDistanceWS);
}
    }

    [System.Serializable]
    public class AttackStageUnityEvent : UnityEvent<AttackStage>
    {
    }

}
