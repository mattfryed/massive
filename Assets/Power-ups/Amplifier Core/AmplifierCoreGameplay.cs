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
    public sealed partial class AmplifierCoreGameplay : MonoBehaviour
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

        [Header("Size & Motion")]
        [Tooltip("Scales the shell, energy core, and physical collider together. 1 preserves the authored size. Mass remains an independent setting.")]
        [SerializeField, Min(0.05f)] private float coreScale = 1f;
        [SerializeField, HideInInspector] private float appliedCoreScale = 1f;
        [SerializeField, Min(0.01f)] private float mass = 3f;
        [Tooltip("Slows linear motion over time. Zero keeps momentum until an impact or goal force changes it.")]
        [SerializeField, Min(0f)] private float linearDrag = 0.42f;
        [SerializeField, Min(0f)] private float angularDrag = 0.6f;
        [Tooltip("Maximum speed across the playing field, in world units per second. Zero removes the limit.")]
        [SerializeField, Min(0f)] private float maximumPlanarSpeed = 25f;
        [Tooltip("Energy retained in the direction perpendicular to a wall. Wall friction is removed so glancing hits keep sliding. Also applies to solid Resonance surfaces.")]
        [SerializeField, Range(0f, 1f)] private float wallRestitution = 0.85f;

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
        private bool _isDespawning;
        private float _attractionExcitement;
        private float _nextAttackImpactTime;
        private Coroutine _lifecycleRoutine;
        private bool _externalRespawnManaged;
        private bool _spawnStateCaptured;
        private Vector3 _incomingVelocity;
        private PhysicsMaterial _glideMaterial;
        private PhysicsMaterial _originalCollisionMaterial;

        // An optional encounter owner may schedule a new neutral spawn after this capture.
        // Existing standalone cores keep their inspector-driven respawn behavior.
        public event System.Action<AmplifierCoreGameplay> Captured;
        public bool HasBeenCaptured => _captured;
        public void SetExternalRespawnManaged(bool managed) { _externalRespawnManaged = managed; }

        public static IReadOnlyList<AmplifierCoreGameplay> ActiveCores =>
            ActiveCoresInternal;
        public Rigidbody Body => body;
        public bool IsCaptured => _captured || _isSpawning || _isDespawning;
        public bool IsSpawning => _isSpawning;
        public bool IsDespawning => _isDespawning;
        public bool IsPresentationOnly => presentationOnly;
        public float CoreScale => Mathf.Max(0.05f, Effective_coreScale);
        // Prefab placement queries must include a size change not yet applied to its Transform.
        public float PendingScaleRatio => CoreScale / Mathf.Max(0.05f, appliedCoreScale);

        // Called on an inactive preview clone before its first OnEnable.
        public void SetTreatmentPreview()
        {
            presentationOnly = true;
            ResolveReferences();
            CaptureSpawnState();
            ConfigurePresentationOnly();
        }

        private void Reset()
        {
            ResolveReferences();
        }

        private void Awake()
        {
            ResolveReferences();
            ApplyTuning();
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
            RevokeExternalTransit();
            ActiveCoresInternal.Remove(this);
            if (_lifecycleRoutine != null)
            {
                StopCoroutine(_lifecycleRoutine);
                _lifecycleRoutine = null;
            }
        }

        private void OnDestroy()
        {
            if (_glideMaterial == null) return;
            if (collisionShape != null && collisionShape.sharedMaterial == _glideMaterial)
                collisionShape.sharedMaterial = _originalCollisionMaterial;
            Destroy(_glideMaterial);
        }

        private void OnValidate()
        {
            coreScale = Mathf.Max(0.05f, coreScale);
            mass = Mathf.Max(0.01f, mass);
            linearDrag = Mathf.Max(0f, linearDrag);
            angularDrag = Mathf.Max(0f, angularDrag);
            maximumPlanarSpeed = Mathf.Max(0f, maximumPlanarSpeed);
            wallRestitution = Mathf.Clamp01(wallRestitution);
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
            if (presentationOnly || IsCaptured || IsInExternalTransit)
                return;

            if (body != null && !body.isKinematic)
            {
                Vector3 velocity = LimitPlanarSpeed(GetVelocity(body), Effective_maximumPlanarSpeed);
                SetVelocity(body, velocity);
                _incomingVelocity = velocity;
            }

            _attractionExcitement = Mathf.MoveTowards(
                _attractionExcitement,
                0f,
                Time.fixedDeltaTime * 2.5f);
            if (visual != null)
                visual.SetExcitement(_attractionExcitement);
        }

        private void OnCollisionEnter(Collision collision)
        {
            BounceOffSolidSurface(collision);
            TryApplyCollisionAttackImpact(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            BounceOffSolidSurface(collision);
            TryApplyCollisionAttackImpact(collision);
        }

        /// <summary>Apply the Inspector dials without recapturing a spawn position or lifecycle state.</summary>
        public void ApplyTuning()
        {
            ResolveReferences();
            if (presentationOnly) return;
            float scaleRatio = PendingScaleRatio;
            transform.localScale *= scaleRatio;
            if (_spawnStateCaptured)
            {
                _spawnLocalScale *= scaleRatio;
                if (visualRoot == transform) _visualBaseScale *= scaleRatio;
            }
            appliedCoreScale = CoreScale;
            if (body == null) return;
            body.mass = Mathf.Max(0.01f, Effective_mass);
#if UNITY_6000_0_OR_NEWER
            body.linearDamping = Mathf.Max(0f, Effective_linearDrag);
            body.angularDamping = Mathf.Max(0f, Effective_angularDrag);
#else
            body.drag = Mathf.Max(0f, Effective_linearDrag);
            body.angularDrag = Mathf.Max(0f, Effective_angularDrag);
#endif
            if (!Application.isPlaying || !gameObject.scene.IsValid()) return;
            body.sleepThreshold = 0f;
            if (_glideMaterial == null && collisionShape != null)
            {
                _originalCollisionMaterial = collisionShape.sharedMaterial;
                _glideMaterial = new PhysicsMaterial("Amplifier Core — frictionless contact")
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    dynamicFriction = 0f,
                    staticFriction = 0f,
                    bounciness = 0f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounceCombine = PhysicsMaterialCombine.Minimum
                };
                collisionShape.sharedMaterial = _glideMaterial;
            }
            if (!body.isKinematic)
            {
                SetVelocity(body, LimitPlanarSpeed(GetVelocity(body), Effective_maximumPlanarSpeed));
                body.WakeUp();
            }
        }

        private void BounceOffSolidSurface(Collision collision)
        {
            if (presentationOnly || IsCaptured || body == null || body.isKinematic || collision == null ||
                (collision.rigidbody != null && !collision.rigidbody.isKinematic)) return;
            // PhysX suppresses ordinary material bounce below bounceThreshold. Reflect the
            // pre-solver planar motion ourselves, as with the nugglets, even on a slow contact.
            for (int i = 0; i < collision.contactCount; i++)
            {
                Vector3 normal = collision.GetContact(i).normal;
                if (Mathf.Abs(normal.y) > 0.5f) continue;
                normal.y = 0f;
                normal.Normalize();
                float towardWall = Vector3.Dot(_incomingVelocity, normal);
                if (towardWall >= 0f) continue;
                _incomingVelocity -= (1f + Effective_wallRestitution) * towardWall * normal;
                _incomingVelocity = LimitPlanarSpeed(_incomingVelocity, Effective_maximumPlanarSpeed);
                SetVelocity(body, _incomingVelocity);
                body.WakeUp();
            }
        }

        private static Vector3 LimitPlanarSpeed(Vector3 velocity, float maximumSpeed)
        {
            if (maximumSpeed <= 0f) return velocity;
            Vector3 planar = new Vector3(velocity.x, 0f, velocity.z);
            planar = Vector3.ClampMagnitude(planar, maximumSpeed);
            return new Vector3(planar.x, velocity.y, planar.z);
        }

        public void SetAttractionExcitement(float excitement01)
        {
            if (!presentationOnly && !IsCaptured && !IsInExternalTransit)
            {
                _attractionExcitement = Mathf.Max(
                    _attractionExcitement,
                    Mathf.Clamp01(excitement01));
            }
        }

        public bool TryCapture(AmplifierGoalCapture goal)
        {
            if (presentationOnly || IsCaptured || IsInExternalTransit || goal == null)
                return false;

            if (!goal.TryAdvanceTeamAmplifier())
                return false;

            _captured = true;
            goal.PlayCaptureFeedback();
            StartLifecycle(CaptureRoutine(goal.CapturePoint));
            Captured?.Invoke(this);
            return true;
        }

        /// <summary>Retire an unclaimed Core in place. Never awards a multiplier or self-respawns.</summary>
        public bool BeginTimeoutDespawn()
        {
            if (!Application.isPlaying || presentationOnly || _captured || _isDespawning || !isActiveAndEnabled) return false;
            // A timeout owns the lifecycle even during a portal sequence. Release
            // its deformation before CaptureRoutine snapshots the visual scale.
            RevokeExternalTransit();
            _isDespawning = true;
            _isSpawning = false;
            StartLifecycle(CaptureRoutine(null, true));
            return true;
        }

        public void CompleteSpawnImmediately()
        {
            RevokeExternalTransit();
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
            _isDespawning = false;
            _captured = false;
            _attractionExcitement = 0f;
        }

        private void BeginSpawnAnimation()
        {
            StartLifecycle(SpawnRoutine());
        }

        private IEnumerator SpawnRoutine()
        {
            RevokeExternalTransit();
            _captured = false;
            _isDespawning = false;
            _isSpawning = true;
            RestoreSpawnTransform();
            SuspendPhysics();

            if (visualRoot != null)
                visualRoot.localScale = Vector3.zero;
            if (visual != null)
                visual.PrepareSpawnPresentation();

            float totalDuration = Mathf.Max(
                Effective_spawnShellMorphSeconds,
                Effective_spawnCoreDelaySeconds + Effective_spawnCoreRevealSeconds);
            totalDuration = Mathf.Max(totalDuration, Effective_spawnScaleSeconds);
            float elapsed = 0f;

            while (elapsed < totalDuration)
            {
                elapsed += Time.deltaTime;
                float scaleT = Mathf.Clamp01(elapsed / Effective_spawnScaleSeconds);
                float shellT = Mathf.Clamp01(elapsed / Effective_spawnShellMorphSeconds);
                float coreT = Mathf.Clamp01(
                    (elapsed - Effective_spawnCoreDelaySeconds) / Effective_spawnCoreRevealSeconds);

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

        private IEnumerator CaptureRoutine(Transform target, bool timedOut = false)
        {
            Vector3 startPosition = transform.position;
            Vector3 startScale = visualRoot != null ? visualRoot.localScale : Vector3.one;
            Vector3 targetPosition = target != null ? target.position : startPosition;

            SuspendPhysics();
            if (visual != null)
                visual.SetExcitement(1f);

            float duration = Mathf.Max(
                Mathf.Max(Effective_captureTravelSeconds, Effective_despawnScaleSeconds),
                Effective_despawnShellMorphSeconds);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float travelT = Mathf.Clamp01(elapsed / Effective_captureTravelSeconds);
                float travelEase = Effective_captureEase != null
                    ? Effective_captureEase.Evaluate(travelT)
                    : SmoothStep01(travelT);
                transform.position = Vector3.LerpUnclamped(
                    startPosition,
                    targetPosition,
                    travelEase);

                float scaleT = Mathf.Clamp01(elapsed / Effective_despawnScaleSeconds);
                float scale = scaleT < 1f
                    ? Mathf.Lerp(1f, 0.16f, SmoothStep01(scaleT))
                    : Mathf.Lerp(
                        0.16f,
                        0f,
                        Mathf.InverseLerp(Effective_despawnScaleSeconds, duration, elapsed));
                if (visualRoot != null)
                    visualRoot.localScale = startScale * scale;

                float shellReveal = 1f - Mathf.Clamp01(elapsed / Effective_despawnShellMorphSeconds);
                float coreReveal = 1f - Mathf.Clamp01(elapsed / Effective_despawnCoreHideSeconds);
                if (visual != null)
                {
                    visual.SetLifecycleReveal(
                        SmoothStep01(shellReveal),
                        SmoothStep01(coreReveal));
                }

                yield return null;
            }

            if (timedOut || _externalRespawnManaged || !respawnAfterCapture)
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
            if (presentationOnly || IsCaptured || IsInExternalTransit || body == null || body.isKinematic ||
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
                thrustDirection * (Effective_attackImpulse * angleStrength),
                ForceMode.Impulse);
            attack.StopAtSolidImpact(
                Effective_playerPlanarVelocityRetention,
                Effective_playerImpactStopSeconds);

            _nextAttackImpactTime = Time.time + Effective_repeatImpactLockoutSeconds;
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
            _incomingVelocity = Vector3.zero;
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
            _incomingVelocity = Vector3.zero;
            _nextAttackImpactTime = 0f;
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
            _spawnStateCaptured = true;
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

        private static Vector3 GetVelocity(Rigidbody targetBody)
        {
#if UNITY_6000_0_OR_NEWER
            return targetBody.linearVelocity;
#else
            return targetBody.velocity;
#endif
        }
    }
}
