using UnityEngine;
using Massive.PowerUps;

namespace Massive.Enemies
{
    [DisallowMultipleComponent, RequireComponent(typeof(EnemyBase))]
    public sealed partial class ParticleBeamTurretController : MonoBehaviour
    {
        public enum WallMount { Authored, Top, Bottom }
        public enum AttackPhase { Seeking, Charging, Firing, Cooldown }
        public EnemyDefinition definition;
        public EnemyDirector sceneDirector;
        public ArenaBoundsFromVectorGrid arenaBounds;
        public Transform firingPivot;
        public Collider shellCollider, damageTrigger;
        public ParticleAcceleratorBeamVisual beamVisual;
        public ParticleBeamPlasmaContact contactPlasma;
        public ParticleBeamPlasmaContact corePlasma;
        [Tooltip("Distance in front of the core center where firing plasma emerges.")]
        [Min(0f)] public float corePlasmaForwardOffset = .1f;
        public EnemySpawnTelegraph spawnTelegraphPrefab;
        [Header("Wall mounting")]
        public WallMount mount = WallMount.Top;
        [Range(-1f, 1f)] public float wallPosition;
        [Tooltip("Moves the octagonal mounting plane inward from the arena boundary.")]
        [Min(0f)] public float wallInset = .04f;
        [Header("Detection and tracking")]
        [Min(.1f)] public float detectionRange = 10f;
        [Range(5f, 85f)] public float maxPivotAngle = 75f;
        [Min(1f)] public float trackingDegreesPerSecond = 80f;
        [Tooltip("Zero locks the telegraphed direction throughout the blast.")]
        [Min(0f)] public float firingTrackingDegreesPerSecond;
        [Tooltip("Time for tracking to ease into motion and settle onto its target.")]
        [Min(.01f)] public float trackingEaseSeconds = .25f;
        [Tooltip("Time for firing aim to ease between its speed and tracking speed, including stopping when a target is lost.")]
        [Min(.01f)] public float firingEaseSeconds = .35f;
        public LayerMask collisionMask = ~0;
        [Header("Attack cycle")]
        [Min(.05f)] public float chargeSeconds = 2f;
        [Min(.05f)] public float fireSeconds = 2f;
        [Min(.05f)] public float cooldownSeconds = 3f;
        [Min(.1f)] public float beamRange = 16f;
        [Min(.01f)] public float beamRadius = .1f;
        [Min(0f)] public float damagePerSecond = .08f;
        [Min(.05f)] public float beamGrowSeconds = .12f;
        [Min(.05f)] public float beamFadeSeconds = .2f;
        [Header("Beam opening pulse")]
        [Range(1f, 3f)] public float openingRadiusMultiplier = 1.7f;
        [Min(.05f)] public float openingSettleSeconds = .32f;
        [Header("Arrival")]
        [Min(0f)] public float spawnWarningSeconds = 2f;
        [Min(.05f)] public float spawnSeconds = 1.1f;
        public AttackPhase Phase { get; private set; }
        public PlayerControllerScript Target { get; private set; }
        public float PhaseAge { get; private set; }
        public float SpawnAge { get; private set; }
        public float RevealProgress => Mathf.Clamp01((SpawnAge - WarningSeconds) / Mathf.Max(.05f, spawnSeconds));
        public bool IsReady => RevealProgress >= 1f;
        public float Charge01 => Phase == AttackPhase.Charging ? Mathf.Clamp01(PhaseAge / Mathf.Max(.05f, chargeSeconds)) : 0f;
        public int TotalBlasts { get; private set; }
        public Vector3 BeamOrigin => firingPivot ? firingPivot.position : transform.position;
        public Vector3 BeamDirection => firingPivot ? firingPivot.forward : transform.forward;
        public Vector3 BeamEnd { get; private set; }
        public float BeamLength { get; private set; }
        public float BeamEnvelope { get; private set; }
        public EnemySpawnTelegraph SpawnWarning => warning;
        public float AimAngularVelocity => aimVelocity;
        private EnemyBase enemy;
        private EnemySpawnTelegraph warning;
        private bool shellEnabled, hurtboxEnabled;
        private float aimVelocity, aimSpeedLimit, stopAcceleration;
        private float WarningSeconds => spawnTelegraphPrefab ? Mathf.Max(0f, spawnWarningSeconds) : 0f;

        private void Awake() { enemy = GetComponent<EnemyBase>(); }
        private void OnEnable()
        {
            enemy = GetComponent<EnemyBase>(); enemy.Died += OnDeath;
            shellEnabled = shellCollider && shellCollider.enabled;
            hurtboxEnabled = damageTrigger && damageTrigger.enabled;
            SetColliders(false); SpawnAge = 0f; TotalBlasts = 0;
            aimVelocity = aimSpeedLimit = stopAcceleration = 0f;
            Target = null; Enter(AttackPhase.Seeking); HideBeam(true);
        }
        private void Start()
        {
            if (!enemy.Definition && definition)
            {
                if (sceneDirector) sceneDirector.RegisterAuthoredEnemy(enemy, definition);
                else enemy.Init(definition, null);
            }
            if (!arenaBounds && enemy.Director) arenaBounds = enemy.Director.arenaBounds;
            if (mount != WallMount.Authored) ApplyWallMount();
            if (TryGetComponent<ParticleBeamTurretVisuals>(out var visual)) visual.ApplyDimensions();
        }
        [ContextMenu("Apply wall mount")]
        public void ApplyWallMount()
        {
            if (mount == WallMount.Authored || !arenaBounds || !arenaBounds.IsValid) return;
            arenaBounds.RefreshNow(false);
            var grid = arenaBounds.Grid.transform; Vector2 half = arenaBounds.Current.halfSizeLocal;
            // The planar grid's local Y may point toward either end of world Z.
            // Top is the +Z wall in the gameplay camera, independently of grid orientation.
            float topSign = Vector3.Dot(arenaBounds.Current.axisY_WS, Vector3.forward) >= 0f ? 1f : -1f;
            float sign = mount == WallMount.Top ? topSign : -topSign;
            float inset = wallInset / Mathf.Max(.0001f, Mathf.Abs(grid.lossyScale.y));
            Vector3 position = grid.TransformPoint(new Vector3(half.x * Mathf.Clamp(wallPosition, -.9f, .9f), sign * (half.y - inset), 0f));
            position.y = 0f;
            Vector3 inward = -sign * arenaBounds.Current.axisY_WS; inward.y = 0f;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(inward.normalized, Vector3.up));
            if (TryGetComponent<Rigidbody>(out var rb)) { rb.position = position; rb.rotation = transform.rotation; }
        }
        private void OnDisable()
        {
            if (enemy) enemy.Died -= OnDeath;
            CancelWarning(); HideBeam(true); Target = null;
            SetColliders(true);
        }
        private void SetColliders(bool enabled)
        { if (shellCollider) shellCollider.enabled = enabled && shellEnabled; if (damageTrigger) damageTrigger.enabled = enabled && hurtboxEnabled; }
        private void CancelWarning() { if (warning) warning.Cancel(); warning = null; }
        private void OnDeath(EnemyBase source, EnemyDamageSource cause)
        { CancelWarning(); HideBeam(true); Target = null; Enter(AttackPhase.Seeking); SetColliders(false); }
        private void Enter(AttackPhase phase) { Phase = phase; PhaseAge = 0f; }
        private bool Eligible(PlayerControllerScript p) => p && p.isActiveAndEnabled && p.gameObject.scene == gameObject.scene
            && !p.IsPseudoPlayer && !p.temporarilyEliminated && !p.IsMatchInputLocked && p.massScore > p.massScoreMin
            && (enemy.OwnerTeamId < 0 || p.teamID != enemy.OwnerTeamId);
        private bool InArc(PlayerControllerScript p)
        {
            if (!Eligible(p)) return false;
            Vector3 direction = p.transform.position - BeamOrigin; direction.y = 0f;
            return direction.sqrMagnitude <= detectionRange * detectionRange
                && Vector3.Angle(transform.forward, direction) <= maxPivotAngle;
        }
        private void Acquire()
        {
            if (InArc(Target) && CanSee(Target)) return;
            Target = null; float best = detectionRange * detectionRange;
            foreach (var p in PlayerControllerScript.ActivePlayers)
            {
                if (!InArc(p)) continue;
                float d = (p.transform.position - BeamOrigin).sqrMagnitude;
                if (d < best && CanSee(p)) { best = d; Target = p; }
            }
        }
        private void Track(float speed, float dt)
        {
            if (!firingPivot) return;
            float ease = Mathf.Max(.01f, Phase == AttackPhase.Firing ? firingEaseSeconds : trackingEaseSeconds);
            float current = Vector3.SignedAngle(transform.forward, firingPivot.forward, Vector3.up);
            bool following = Target && InArc(Target) && speed > 0f;
            float wantedLimit = following ? speed : 0f;
            aimSpeedLimit = Mathf.MoveTowards(aimSpeedLimit, wantedLimit,
                Mathf.Max(1f, Mathf.Max(trackingDegreesPerSecond, firingTrackingDegreesPerSecond)) * dt / ease);
            float next = current;
            if (following)
            {
                Vector3 direction = Target.transform.position - BeamOrigin; direction.y = 0f;
                float wanted = Mathf.Clamp(Vector3.SignedAngle(transform.forward, direction, Vector3.up), -maxPivotAngle, maxPivotAngle);
                next = Mathf.SmoothDampAngle(current, wanted, ref aimVelocity, ease, aimSpeedLimit, dt);
                stopAcceleration = 0f;
            }
            else
            {
                aimVelocity = Mathf.SmoothDamp(aimVelocity, 0f, ref stopAcceleration, ease, Mathf.Infinity, dt);
                next += aimVelocity * dt;
            }
            next = Mathf.Clamp(next, -maxPivotAngle, maxPivotAngle);
            firingPivot.rotation = Quaternion.AngleAxis(next, Vector3.up) * transform.rotation;
        }
        private void FixedUpdate()
        {
            if (!enemy || !enemy.Definition || enemy.IsDead || enemy.IsPaused) return;
            float dt = Time.fixedDeltaTime;
            if (contactPlasma) contactPlasma.Advance(dt);
            if (corePlasma) corePlasma.Advance(dt);
            if (warning && !warning.Advance(dt)) warning = null;
            if (!IsReady)
            {
                if (SpawnAge == 0f && WarningSeconds > 0f)
                {
                    warning = Instantiate(spawnTelegraphPrefab, transform.position, transform.rotation, transform);
                    Vector3 scale = transform.lossyScale;
                    if (TryGetComponent<ParticleBeamTurretVisuals>(out var visual))
                        scale = Vector3.Scale(scale, new Vector3(visual.baseRadius / ParticleBeamTurretGeometry.DefaultBaseRadius,
                            visual.baseRadius / ParticleBeamTurretGeometry.DefaultBaseRadius, visual.baseHeight / ParticleBeamTurretGeometry.DefaultBaseHeight));
                    warning.Begin(WarningSeconds, scale);
                }
                SpawnAge += dt;
                if (SpawnAge >= WarningSeconds && warning && !warning.IsCompleting) warning.Complete();
                if (IsReady) SetColliders(true);
                return;
            }
            PhaseAge += dt;
            if (Phase == AttackPhase.Firing)
            {
                if (firingTrackingDegreesPerSecond > 0f) Track(firingTrackingDegreesPerSecond, dt);
                if (PhaseAge >= fireSeconds) { HideBeam(); Enter(AttackPhase.Cooldown); return; }
                FireBeam(dt); return;
            }
            if (Phase == AttackPhase.Charging && (!InArc(Target) || !CanSee(Target)))
            { Target = null; Enter(AttackPhase.Cooldown); Track(0f, dt); return; }
            Acquire();
            // A locked blast brakes before the charge ends, keeping the firing direction stationary.
            float trackingSpeed = trackingDegreesPerSecond;
            if (Phase == AttackPhase.Charging && firingTrackingDegreesPerSecond <= 0f)
                trackingSpeed *= Mathf.SmoothStep(0f, 1f, (chargeSeconds - PhaseAge) / Mathf.Max(.01f, trackingEaseSeconds));
            Track(trackingSpeed, dt);
            if (Phase == AttackPhase.Seeking && Target && beamVisual) Enter(AttackPhase.Charging);
            else if (Phase == AttackPhase.Charging && PhaseAge >= chargeSeconds)
            {
                Enter(AttackPhase.Firing); TotalBlasts++; enemy.PlayAttackSfx(); FireBeam(dt);
                if (firingTrackingDegreesPerSecond <= 0f) aimVelocity = aimSpeedLimit = stopAcceleration = 0f;
            }
            else if (Phase == AttackPhase.Cooldown && PhaseAge >= cooldownSeconds) Enter(AttackPhase.Seeking);
            if (Phase == AttackPhase.Charging) UpdateAimEndpoint();
        }
    }
}
