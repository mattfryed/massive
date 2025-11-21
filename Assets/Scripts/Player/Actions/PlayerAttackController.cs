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


        [Header("Combo Tuning")]
        [SerializeField, Min(0f)]
        private float comboInputBuffer = 0.15f;

        [Header("Scene References")]
        [SerializeField]
        public Transform forwardReference = null;

        [SerializeField]
        private Transform particleAnchor = null;

        [SerializeField]
        private Animator animator = null;

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
        private ParticleSystem activeParticleInstance;
        private Coroutine particleCleanupRoutine;

        public bool IsAttacking => isAttacking;
        public AttackStage CurrentStage => currentStage;
        public int CurrentStageIndex => currentStageIndex;
        public float StageNormalizedTime => currentStage != null ? Mathf.Clamp01(stageTimer / currentStage.Duration) : 0f;

        public Transform ForwardReference => forwardReference != null ? forwardReference : transform;
        private Transform ParticleAnchor => particleAnchor != null ? particleAnchor : transform;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            rb = GetComponent<Rigidbody>();

            // Auto-wire forward reference to the visual controller's 'visuals' child if not set
            if (forwardReference == null)
            {
                var pvc = GetComponent<PlayerVisualController>();   // global type, no namespace
                if (pvc != null && pvc.visuals != null)
                {
                    forwardReference = pvc.visuals;
                }
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
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

        private Vector3 GetAttackDirection()
        {
            // 1) If there's live stick input, use that
            if (externalMoveInput.sqrMagnitude > 0.001f)
            {
                var dir = new Vector3(externalMoveInput.x, 0f, externalMoveInput.y);
                return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
            }

            // 2) If we have a remembered direction, use that (last facing)
            if (lastNonZeroMoveDir.sqrMagnitude > 0.0001f)
            {
                var dir = new Vector3(lastNonZeroMoveDir.x, 0f, lastNonZeroMoveDir.y);
                return dir.normalized;
            }

            // 3) Fallback to forward reference, then world forward
            Vector3 fwd = ForwardReference.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            return fwd.normalized;
        }



        private void ApplyStageMotion(float normalized, float deltaNormalized)
        {
            float targetDistance = currentStage.TravelDistance * currentStage.DistanceCurve.Evaluate(normalized);
            float deltaDistance = targetDistance - currentDistanceProgress;
            currentDistanceProgress = targetDistance;

            if (!Mathf.Approximately(deltaDistance, 0f))
            {
                Vector3 displacement = GetAttackDirection() * deltaDistance;
                displacement.y = 0f;      // <— force attack moves to stay on the ground plane
                Move(displacement);

            }

            switch (currentStage.StageType)
            {
                case AttackStageType.ComboSwipe:
                    ApplySwipeRotation(deltaNormalized);
                    break;
                case AttackStageType.FinisherRepulsor:
                    // No additional motion required for repulsor stage by default.
                    break;
            }
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


        private void ApplySwipeRotation(float deltaNormalized)
        {
            int desiredDirection = CalculateSwipeDirection();
            if (desiredDirection != 0)
            {
                swipeDirection = desiredDirection;
            }

            if (Mathf.Approximately(currentStage.RotationArc, 0f))
                return;

            float rotationAmount = currentStage.RotationArc * deltaNormalized * swipeDirection;
            transform.Rotate(Vector3.up, rotationAmount, Space.World);
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

            float start = currentStage.ActivationStartNormalized;
            float end = currentStage.ActivationEndNormalized;
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

            DetermineSwipeDirection();
            PlayStageAnimation(stage);
            PlayStageParticles(stage);

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
            Vector3 referenceForward = ForwardReference.forward;
            referenceForward.y = 0f;
            if (referenceForward.sqrMagnitude < 0.001f)
            {
                referenceForward = Vector3.forward;
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

        private void PlayStageAnimation(AttackStage stage)
        {
            if (animator == null || string.IsNullOrEmpty(stage.AnimationStateName))
                return;

            animator.CrossFade(stage.AnimationStateName, stage.AnimationTransitionDuration, 0, 0f);
        }

        private void PlayStageParticles(AttackStage stage)
        {
            if (stage.ParticlePrefab == null)
                return;

            if (particleCleanupRoutine != null)
            {
                StopCoroutine(particleCleanupRoutine);
                particleCleanupRoutine = null;
            }

            if (activeParticleInstance != null)
            {
                Destroy(activeParticleInstance.gameObject);
                activeParticleInstance = null;
            }

            activeParticleInstance = Instantiate(stage.ParticlePrefab, ParticleAnchor.position, ParticleAnchor.rotation, ParticleAnchor);
            activeParticleInstance.Play(true);
            particleCleanupRoutine = StartCoroutine(CleanupParticleInstance(activeParticleInstance));
        }

        private IEnumerator CleanupParticleInstance(ParticleSystem particle)
        {
            if (particle == null)
            {
                particleCleanupRoutine = null;
                yield break;
            }

            var main = particle.main;
            if (main.loop)
            {
                particleCleanupRoutine = null;
                yield break;
            }

            float maxLifetime;
            if (main.startLifetime.mode == ParticleSystemCurveMode.Constant)
            {
                maxLifetime = main.startLifetime.constant;
            }
            else
            {
                maxLifetime = main.startLifetime.constantMax;
            }

            float delay = main.duration + Mathf.Max(0f, maxLifetime);
            yield return new WaitForSeconds(delay);

            if (particle != null)
            {
                Destroy(particle.gameObject);
            }

            if (ReferenceEquals(activeParticleInstance, particle))
            {
                activeParticleInstance = null;
            }

            particleCleanupRoutine = null;
        }

        private void EndAttackSequence()
        {
            if (particleCleanupRoutine != null)
            {
                StopCoroutine(particleCleanupRoutine);
                particleCleanupRoutine = null;
            }

            if (activeParticleInstance != null)
            {
                Destroy(activeParticleInstance.gameObject);
                activeParticleInstance = null;
            }

            currentStage = null;
            currentStageIndex = -1;
            stageTimer = 0f;
            currentDistanceProgress = 0f;
            previousNormalizedTime = 0f;
            comboQueued = false;
            isAttacking = false;
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
    }

    [System.Serializable]
    public class AttackStageUnityEvent : UnityEvent<AttackStage>
    {
    }
}
