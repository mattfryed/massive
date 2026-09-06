using System.Collections;
using System.Collections.Generic;
using Massive.Player;
using UnityEngine;

namespace Massive.Multiplier
{
    /// <summary>
    /// Physical, capturable Amplifier Core. Team multiplier authority remains
    /// in MatchScoreService; this component owns object lifecycle and impacts.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public sealed class AmplifierCoreGameplay : MonoBehaviour
    {
        private static readonly List<AmplifierCoreGameplay> ActiveCoresInternal =
            new List<AmplifierCoreGameplay>(4);

        [Header("Mode")]
        [Tooltip("Used by the small scoreboard copy. Disables physics and capture registration while retaining the visual.")]
        [SerializeField] private bool presentationOnly;

        [Header("References")]
        [SerializeField] private Rigidbody body;
        [SerializeField] private Collider collisionShape;
        [SerializeField] private AmplifierCoreVisual visual;
        [SerializeField] private Transform visualRoot;

        [Header("Spawn")]
        [SerializeField] private bool playSpawnAnimationOnEnable = true;
        [SerializeField, Min(0.05f)] private float spawnScaleSeconds = 0.38f;
        [SerializeField, Min(0.05f)] private float spawnShellMorphSeconds = 0.62f;
        [SerializeField, Min(0f)] private float spawnCoreDelaySeconds = 0.13f;
        [SerializeField, Min(0.05f)] private float spawnCoreRevealSeconds = 0.34f;

        [Header("Capture / Despawn")]
        [SerializeField, Min(0.05f)] private float captureTravelSeconds = 0.32f;
        [SerializeField] private AnimationCurve captureEase =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField, Min(0.05f)] private float despawnScaleSeconds = 0.4f;
        [SerializeField, Min(0.05f)] private float despawnShellMorphSeconds = 0.56f;
        [SerializeField, Min(0.05f)] private float despawnCoreHideSeconds = 0.2f;
        [SerializeField] private bool respawnAfterCapture = true;
        [SerializeField, Min(0f)] private float respawnDelaySeconds = 8f;

        [Header("Attack Impact")]
        [SerializeField, Min(0f)] private float attackImpulse = 20f;
        [SerializeField, Range(0f, 1f)] private float playerPlanarVelocityRetention = 0.03f;
        [SerializeField, Min(0f)] private float playerImpactStopSeconds = 0.12f;
        [SerializeField, Min(0f)] private float repeatImpactLockoutSeconds = 0.12f;

        private Transform _spawnParent;
        private Vector3 _spawnLocalPosition;
        private Quaternion _spawnLocalRotation;
        private Vector3 _spawnLocalScale;
        private Vector3 _visualBaseScale;
        private bool _bodyWasKinematic;
        private bool _colliderWasEnabled;
        private bool _captured;
        private bool _isSpawning;
        private float _attractionExcitement;
        private float _nextAttackImpactTime;
        private Coroutine _lifecycleRoutine;

        public static IReadOnlyList<AmplifierCoreGameplay> ActiveCores =>
            ActiveCoresInternal;
        public Rigidbody Body => body;
        public bool IsCaptured => _captured || _isSpawning;
        public bool IsSpawning => _isSpawning;
        public bool IsPresentationOnly => presentationOnly;

        private void Reset()
        {
            ResolveReferences();
        }

        private void Awake()
        {
            ResolveReferences();
            CaptureSpawnState();
        }

        private void OnEnable()
        {
            ResolveReferences();

            if (presentationOnly)
            {
                ConfigurePresentationOnly();
                return;
            }

            if (!ActiveCoresInternal.Contains(this))
                ActiveCoresInternal.Add(this);

            if (playSpawnAnimationOnEnable)
                BeginSpawnAnimation();
            else
                CompleteSpawnImmediately();
        }

        private void OnDisable()
        {
            ActiveCoresInternal.Remove(this);
            if (_lifecycleRoutine != null)
            {
                StopCoroutine(_lifecycleRoutine);
                _lifecycleRoutine = null;
            }
        }

        private void OnValidate()
        {
            spawnScaleSeconds = Mathf.Max(0.05f, spawnScaleSeconds);
            spawnShellMorphSeconds = Mathf.Max(spawnScaleSeconds, spawnShellMorphSeconds);
            spawnCoreDelaySeconds = Mathf.Max(0f, spawnCoreDelaySeconds);
            spawnCoreRevealSeconds = Mathf.Max(0.05f, spawnCoreRevealSeconds);
            captureTravelSeconds = Mathf.Max(0.05f, captureTravelSeconds);
            despawnScaleSeconds = Mathf.Max(0.05f, despawnScaleSeconds);
            despawnShellMorphSeconds = Mathf.Max(despawnScaleSeconds, despawnShellMorphSeconds);
            despawnCoreHideSeconds = Mathf.Max(0.05f, despawnCoreHideSeconds);
            attackImpulse = Mathf.Max(0f, attackImpulse);
            playerImpactStopSeconds = Mathf.Max(0f, playerImpactStopSeconds);
            repeatImpactLockoutSeconds = Mathf.Max(0f, repeatImpactLockoutSeconds);
            ResolveReferences();
        }

        private void FixedUpdate()
        {
            if (presentationOnly || IsCaptured)
                return;

            _attractionExcitement = Mathf.MoveTowards(
                _attractionExcitement,
                0f,
                Time.fixedDeltaTime * 2.5f);
            if (visual != null)
                visual.SetExcitement(_attractionExcitement);
        }

        private void OnCollisionEnter(Collision collision)
        {
            TryApplyCollisionAttackImpact(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            TryApplyCollisionAttackImpact(collision);
        }

        public void SetAttractionExcitement(float excitement01)
        {
            if (!presentationOnly && !IsCaptured)
            {
                _attractionExcitement = Mathf.Max(
                    _attractionExcitement,
                    Mathf.Clamp01(excitement01));
            }
        }

        public bool TryCapture(AmplifierGoalCapture goal)
        {
            if (presentationOnly || IsCaptured || goal == null)
                return false;

            if (!goal.TryAdvanceTeamAmplifier())
                return false;

            _captured = true;
            goal.PlayCaptureFeedback();
            StartLifecycle(CaptureRoutine(goal.CapturePoint));
            return true;
        }

        public void CompleteSpawnImmediately()
        {
            if (_lifecycleRoutine != null)
            {
                StopCoroutine(_lifecycleRoutine);
                _lifecycleRoutine = null;
            }

            RestoreSpawnTransform();
            if (visualRoot != null)
                visualRoot.localScale = _visualBaseScale;
            if (visual != null)
                visual.RestorePresentation();

            RestorePhysics();
            _isSpawning = false;
            _captured = false;
            _attractionExcitement = 0f;
        }

        private void BeginSpawnAnimation()
        {
            StartLifecycle(SpawnRoutine());
        }

        private IEnumerator SpawnRoutine()
        {
            _captured = false;
            _isSpawning = true;
            RestoreSpawnTransform();
            SuspendPhysics();

            if (visualRoot != null)
                visualRoot.localScale = Vector3.zero;
            if (visual != null)
                visual.PrepareSpawnPresentation();

            float totalDuration = Mathf.Max(
                spawnShellMorphSeconds,
                spawnCoreDelaySeconds + spawnCoreRevealSeconds);
            totalDuration = Mathf.Max(totalDuration, spawnScaleSeconds);
            float elapsed = 0f;

            while (elapsed < totalDuration)
            {
                elapsed += Time.deltaTime;
                float scaleT = Mathf.Clamp01(elapsed / spawnScaleSeconds);
                float shellT = Mathf.Clamp01(elapsed / spawnShellMorphSeconds);
                float coreT = Mathf.Clamp01(
                    (elapsed - spawnCoreDelaySeconds) / spawnCoreRevealSeconds);

                if (visualRoot != null)
                    visualRoot.localScale = _visualBaseScale * BackOut(scaleT);
                if (visual != null)
                    visual.SetLifecycleReveal(shellT, SmoothStep01(coreT));

                yield return null;
            }

            if (visualRoot != null)
                visualRoot.localScale = _visualBaseScale;
            if (visual != null)
                visual.RestorePresentation();

            RestorePhysics();
            _isSpawning = false;
            _lifecycleRoutine = null;
        }

        private IEnumerator CaptureRoutine(Transform target)
        {
            Vector3 startPosition = transform.position;
            Vector3 startScale = visualRoot != null ? visualRoot.localScale : Vector3.one;
            Vector3 targetPosition = target != null ? target.position : startPosition;

            SuspendPhysics();
            if (visual != null)
                visual.SetExcitement(1f);

            float duration = Mathf.Max(
                Mathf.Max(captureTravelSeconds, despawnScaleSeconds),
                despawnShellMorphSeconds);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float travelT = Mathf.Clamp01(elapsed / captureTravelSeconds);
                float travelEase = captureEase != null
                    ? captureEase.Evaluate(travelT)
                    : SmoothStep01(travelT);
                transform.position = Vector3.LerpUnclamped(
                    startPosition,
                    targetPosition,
                    travelEase);

                float scaleT = Mathf.Clamp01(elapsed / despawnScaleSeconds);
                float scale = scaleT < 1f
                    ? Mathf.Lerp(1f, 0.16f, SmoothStep01(scaleT))
                    : Mathf.Lerp(
                        0.16f,
                        0f,
                        Mathf.InverseLerp(despawnScaleSeconds, duration, elapsed));
                if (visualRoot != null)
                    visualRoot.localScale = startScale * scale;

                float shellReveal = 1f - Mathf.Clamp01(elapsed / despawnShellMorphSeconds);
                float coreReveal = 1f - Mathf.Clamp01(elapsed / despawnCoreHideSeconds);
                if (visual != null)
                {
                    visual.SetLifecycleReveal(
                        SmoothStep01(shellReveal),
                        SmoothStep01(coreReveal));
                }

                yield return null;
            }

            if (!respawnAfterCapture)
            {
                _lifecycleRoutine = null;
                gameObject.SetActive(false);
                yield break;
            }

            if (respawnDelaySeconds > 0f)
                yield return new WaitForSeconds(respawnDelaySeconds);

            yield return SpawnRoutine();
        }

        public bool TryApplyAttackImpact(PlayerAttackController attack)
        {
            if (presentationOnly || IsCaptured || body == null ||
                Time.time < _nextAttackImpactTime)
                return false;
            if (attack == null || !attack.IsAttacking)
                return false;

            Vector3 thrustDirection = attack.CurrentAttackDirectionWS;
            thrustDirection.y = 0f;
            if (thrustDirection.sqrMagnitude < 0.0001f)
                return false;
            thrustDirection.Normalize();

            Vector3 fromPlayer = body.worldCenterOfMass - attack.transform.position;
            fromPlayer.y = 0f;
            if (fromPlayer.sqrMagnitude < 0.0001f)
                fromPlayer = thrustDirection;
            else
                fromPlayer.Normalize();

            float alignment = Vector3.Dot(thrustDirection, fromPlayer);
            float angleStrength = Mathf.Lerp(
                0.65f,
                1.1f,
                Mathf.InverseLerp(-0.2f, 1f, alignment));

            body.AddForce(
                thrustDirection * (attackImpulse * angleStrength),
                ForceMode.Impulse);
            attack.StopAtSolidImpact(
                playerPlanarVelocityRetention,
                playerImpactStopSeconds);

            _nextAttackImpactTime = Time.time + repeatImpactLockoutSeconds;
            _attractionExcitement = 1f;
            if (visual != null)
                visual.SetExcitement(1f);
            return true;
        }

        private void TryApplyCollisionAttackImpact(Collision collision)
        {
            if (collision == null)
                return;

            PlayerAttackController attack =
                collision.collider.GetComponentInParent<PlayerAttackController>();
            TryApplyAttackImpact(attack);
        }

        private void StartLifecycle(IEnumerator routine)
        {
            if (_lifecycleRoutine != null)
                StopCoroutine(_lifecycleRoutine);
            _lifecycleRoutine = StartCoroutine(routine);
        }

        private void SuspendPhysics()
        {
            if (collisionShape != null)
                collisionShape.enabled = false;

            if (body != null)
            {
                SetVelocity(body, Vector3.zero);
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
            }
        }

        private void RestorePhysics()
        {
            if (body != null)
            {
                body.isKinematic = _bodyWasKinematic;
                SetVelocity(body, Vector3.zero);
                body.angularVelocity = Vector3.zero;
            }

            if (collisionShape != null)
                collisionShape.enabled = _colliderWasEnabled;
        }

        private void RestoreSpawnTransform()
        {
            transform.SetParent(_spawnParent, false);
            transform.localPosition = _spawnLocalPosition;
            transform.localRotation = _spawnLocalRotation;
            transform.localScale = _spawnLocalScale;
        }

        private void ResolveReferences()
        {
            if (body == null)
                body = GetComponent<Rigidbody>();
            if (collisionShape == null)
                collisionShape = GetComponent<Collider>();
            if (visual == null)
                visual = GetComponent<AmplifierCoreVisual>();
            if (visualRoot == null)
            {
                Transform candidate = transform.Find("Visual Root");
                visualRoot = candidate != null ? candidate : transform;
            }
        }

        private void CaptureSpawnState()
        {
            _spawnParent = transform.parent;
            _spawnLocalPosition = transform.localPosition;
            _spawnLocalRotation = transform.localRotation;
            _spawnLocalScale = transform.localScale;
            _visualBaseScale = visualRoot != null ? visualRoot.localScale : Vector3.one;
            _bodyWasKinematic = body != null && body.isKinematic;
            _colliderWasEnabled = collisionShape == null || collisionShape.enabled;
        }

        private void ConfigurePresentationOnly()
        {
            ActiveCoresInternal.Remove(this);
            if (body != null)
            {
                SetVelocity(body, Vector3.zero);
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
            }

            if (collisionShape != null)
                collisionShape.enabled = false;

            GridInteractor interactor = GetComponent<GridInteractor>();
            if (interactor != null)
                interactor.enabled = false;

            if (visualRoot != null)
                visualRoot.localScale = _visualBaseScale;
            if (visual != null)
                visual.RestorePresentation();
        }

        private static float BackOut(float value)
        {
            value = Mathf.Clamp01(value);
            const float overshoot = 1.70158f;
            float shifted = value - 1f;
            return 1f + ((overshoot + 1f) * shifted * shifted * shifted) +
                   (overshoot * shifted * shifted);
        }

        private static float SmoothStep01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - (2f * value));
        }

        private static void SetVelocity(Rigidbody targetBody, Vector3 velocity)
        {
#if UNITY_6000_0_OR_NEWER
            targetBody.linearVelocity = velocity;
#else
            targetBody.velocity = velocity;
#endif
        }
    }
}
