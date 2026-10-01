using UnityEngine;

namespace Massive.Enemies
{
    /// <summary>Owns the Seeker's paused gameplay clock, movement and one-contact lunge.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(EnemyBase), typeof(Rigidbody), typeof(EnemyObstacleAvoidance))]
    public sealed partial class SeekerController : MonoBehaviour
    {
        public enum AttackPhase { Seeking, Charging, Firing, Hit, Cooldown, Stalking, Gliding }
        public EnemyDefinition definition;
        public EnemyDirector sceneDirector;
        public ArenaBoundsFromVectorGrid arenaBounds;
        public CapsuleCollider shellCollider;
        public Collider damageTrigger;
        public EnemySpawnTelegraph spawnTelegraphPrefab;
        public ParticleBeamPlasmaContact launchPlasma;
        [Header("Seeking and tracking")]
        [Min(.1f)] public float detectionRange = 7f;
        [Min(.1f)] public float disengageRange = 9f;
        [Min(.05f)] public float decisionInterval = .15f;
        [Min(.1f)] public float acceleration = 14f;
        [Min(0f)] public float idleSpeed = .65f;
        [Min(.01f)] public float trackingEaseSeconds = .14f;
        [Header("Charge and lunge")]
        [Tooltip("Start charging within this distance; initially matches the Ranged Drone.")]
        [Min(.1f)] public float attackRange = 4.5f;
        [Min(.05f)] public float chargeSeconds = 1.25f;
        [Tooltip("Final stationary snap together, followed immediately by launch.")]
        [Min(.02f)] public float compressionSeconds = .12f;
        [Tooltip("Fraction of charge after which aim is locked. The lunge never homes.")]
        [Range(0f, 1f)] public float aimLockFraction = .8f;
        [Min(0f)] public float chargeTrackingDegreesPerSecond = 100f;
        [Min(.1f)] public float lungeSpeed = 22f;
        [Min(.1f)] public float lungeDistance = 4.5f;
        [Min(.05f)] public float hitSeconds = .24f;
        [Min(.05f)] public float cooldownSeconds = 1.75f;
        [Header("Thrust recovery")]
        [Tooltip("Cooldown after accepted player damage. Shields and obstacles use the normal cooldown.")]
        [Min(.05f)] public float playerHitCooldownSeconds = .65f;
        [Tooltip("Backward travel after a player, shield, wall or obstacle contact. Swept for safety.")]
        [Min(0f)] public float hitBounceDistance = .18f;
        [Tooltip("Time to ease a missed thrust's remaining forward momentum to rest.")]
        [Min(.02f)] public float missGlideSeconds = .1f;
        [Tooltip("Initial glide speed as a fraction of lunge speed. 1 preserves velocity at the end of thrust.")]
        [Range(0f, 1f)] public float missGlideSpeedFraction = 1f;
        [Header("Stalking after recovery")]
        [Min(0f)] public float stalkSeconds = 3f;
        [Tooltip("Preferred minimum/maximum distance from the player during stalking.")]
        public Vector2 stalkDistanceRange = new Vector2(3f, 4.25f);
        [Min(0f)] public float stalkRadialSpeed = 2.6f;
        [Min(0f)] public float stalkOrbitSpeed = 1.1f;
        [Min(0f)] public float stalkStrafeSpeed = .55f;
        [Min(0f)] public float stalkStrafeFrequency = .7f;
        [Header("Attack avoidance (seeking and stalking)")]
        [Range(0f, 1f)] public float avoidanceChance = .55f;
        [Min(0f)] public float avoidanceReactionSeconds = .16f;
        [Min(.1f)] public float avoidanceRange = 3.5f;
        [Range(1f, 80f)] public float avoidanceConeDegrees = 24f;
        [Min(0f)] public float strafeSpeed = 4f;
        [Min(.02f)] public float strafeSeconds = .3f;
        [Min(0f)] public float avoidanceCooldown = .8f;
        [Header("Arrival and launch plasma")]
        [Min(0f)] public float spawnWarningSeconds = 1.5f;
        [Min(.05f)] public float spawnSeconds = .8f;
        [Range(1, 20)] public int launchDrops = 16;
        [Min(0f)] public float launchPlasmaBackOffset = .15f;
        public AttackPhase Phase { get; private set; }
        public PlayerControllerScript Target { get; private set; }
        public float PhaseAge { get; private set; }
        public float SpawnAge { get; private set; }
        public float RevealProgress => Mathf.Clamp01((SpawnAge - WarningSeconds) / Mathf.Max(.05f, spawnSeconds));
        public bool IsReady => RevealProgress >= 1f;
        public float Charge01 => Phase == AttackPhase.Charging ? Mathf.Clamp01(PhaseAge / Mathf.Max(.05f, chargeSeconds)) : 0f;
        public float Compression01 => Phase == AttackPhase.Charging ? Mathf.Clamp01((PhaseAge - chargeSeconds) / Mathf.Max(.02f, compressionSeconds)) : 0f;
        public float Extension01 => Charge01 * (1f - Mathf.SmoothStep(0f, 1f, Compression01));
        public bool IsStrafing => (Phase == AttackPhase.Seeking || Phase == AttackPhase.Stalking) && dodgeRemaining > 0f;
        public Vector3 LungeDirection { get; private set; }
        public float DistanceTravelled { get; private set; }
        public float GlideDistanceTravelled { get; private set; }
        public Vector3 LungeStartPosition { get; private set; }
        public bool LastAttackHitPlayer { get; private set; }
        public float CurrentCooldownSeconds => LastAttackHitPlayer ? playerHitCooldownSeconds : cooldownSeconds;
        public int TotalLunges { get; private set; }
        public int TotalHits { get; private set; }
        public int TotalDodges { get; private set; }
        public EnemySpawnTelegraph SpawnWarning => warning;
        private EnemyBase enemy;
        private Rigidbody body;
        private EnemyObstacleAvoidance avoidance;
        private SeekerVisuals visual;
        private EnemySpawnTelegraph warning;
        private Vector3 velocity, dodgeDirection;
        private float decisionAge, yawVelocity, idleClock, dodgeRemaining, dodgeCooldown, reactionRemaining = -1f;
        private bool observedAttack;
        private float orbitSign = 1f;
        private System.Random random;
        private float WarningSeconds => spawnTelegraphPrefab ? Mathf.Max(0f, spawnWarningSeconds) : 0f;

        private void Awake()
        {
            enemy = GetComponent<EnemyBase>(); body = GetComponent<Rigidbody>();
            avoidance = GetComponent<EnemyObstacleAvoidance>(); visual = GetComponent<SeekerVisuals>();
            body.useGravity = false; body.isKinematic = true;
            random = new System.Random(GetInstanceID());
        }
        private void OnEnable()
        {
            enemy.Died += OnDeath; SpawnAge = PhaseAge = TotalLunges = TotalHits = TotalDodges = 0;
            velocity = Vector3.zero; decisionAge = yawVelocity = idleClock = dodgeRemaining = dodgeCooldown = 0f;
            reactionRemaining = -1f; observedAttack = false; Target = null; Phase = AttackPhase.Seeking;
            LastAttackHitPlayer = false; DistanceTravelled = GlideDistanceTravelled = 0f;
            SetColliders(false);
        }
        private void Start()
        {
            // Exhaust is owned by this enemy but lives in world space, including between interpolated physics frames.
            if (launchPlasma) launchPlasma.transform.SetParent(null, true);
            if (!enemy.Definition && definition)
            {
                if (sceneDirector) sceneDirector.RegisterAuthoredEnemy(enemy, definition);
                else enemy.Init(definition, null);
            }
            if (!arenaBounds) arenaBounds = enemy.Director ? enemy.Director.arenaBounds : FindFirstObjectByType<ArenaBoundsFromVectorGrid>();
        }
        private void OnDisable()
        {
            enemy.Died -= OnDeath; CancelWarning(); if (launchPlasma) launchPlasma.Clear();
            velocity = Vector3.zero; Target = null; SetColliders(true);
        }
        private void OnDestroy()
        { if (launchPlasma && launchPlasma.transform.parent != transform) Destroy(launchPlasma.gameObject); }
        private void CancelWarning() { if (warning) warning.Cancel(); warning = null; }
        private void SetColliders(bool value)
        { if (shellCollider) shellCollider.enabled = value; if (damageTrigger) damageTrigger.enabled = value; }
        private void OnDeath(EnemyBase e, EnemyDamageSource source)
        { CancelWarning(); if (launchPlasma) launchPlasma.StopEmission(); velocity = Vector3.zero; Target = null; SetColliders(false); }
        private void Enter(AttackPhase phase)
        {
            Phase = phase; PhaseAge = 0f;
            if (phase != AttackPhase.Seeking) { velocity = Vector3.zero; dodgeRemaining = 0f; reactionRemaining = -1f; }
            if (phase == AttackPhase.Charging) LastAttackHitPlayer = false;
            if (phase == AttackPhase.Stalking) { orbitSign = random.Next(2) == 0 ? -1f : 1f; observedAttack = false; }
        }
        private bool Eligible(PlayerControllerScript p) => enemy && enemy.CanTarget(p);
        private void Acquire()
        {
            if (Eligible(Target) && Flat(Target.transform.position - body.position).sqrMagnitude <= disengageRange * disengageRange) return;
            Target = null; observedAttack = false; reactionRemaining = -1f;
            float best = detectionRange * detectionRange;
            foreach (var p in PlayerControllerScript.ActivePlayers)
            {
                if (!Eligible(p)) continue;
                float distance = Flat(p.transform.position - body.position).sqrMagnitude;
                if (distance < best && CanSee(p)) { Target = p; best = distance; }
            }
        }
        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
        private void Turn(Vector3 direction, float speed, float dt)
        {
            if (direction.sqrMagnitude < .0001f) return;
            float targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float yaw = Mathf.SmoothDampAngle(body.rotation.eulerAngles.y, targetYaw, ref yawVelocity, trackingEaseSeconds, speed, dt);
            body.rotation = Quaternion.Euler(0f, yaw, 0f);
        }
        private void FixedUpdate()
        {
            if (!enemy || !enemy.Definition || enemy.IsPaused) return;
            float dt = Time.fixedDeltaTime;
            if (!enemy.IsDead) Tick(dt);
            // The pool stores world-space drops and shares the owner's paused clock.
            if (launchPlasma) launchPlasma.Advance(dt);
        }
        private void Tick(float dt)
        {
            if (Target && !Eligible(Target)) Target = null;
            if (warning && !warning.Advance(dt)) warning = null;
            if (!IsReady)
            {
                if (SpawnAge == 0f && WarningSeconds > 0f)
                {
                    warning = Instantiate(spawnTelegraphPrefab, transform.position, transform.rotation, transform);
                    warning.Begin(WarningSeconds, transform.lossyScale, visual ? visual.CreateSpawnOutline() : null);
                }
                SpawnAge += dt;
                if (SpawnAge >= WarningSeconds && warning && !warning.IsCompleting) warning.Complete();
                if (IsReady) SetColliders(true);
                return;
            }
            if (enemy.HoldPosition) { idleClock += dt; return; }
            PhaseAge += dt; idleClock += dt; dodgeCooldown = Mathf.Max(0f, dodgeCooldown - dt);
            if (Phase == AttackPhase.Firing)
            {
                float distance = Mathf.Min(lungeSpeed * enemy.ExternalMovementMultiplier * dt, Mathf.Max(0f, lungeDistance - DistanceTravelled));
                DistanceTravelled += MoveSafely(LungeDirection * distance, true);
                if (Phase == AttackPhase.Firing && DistanceTravelled >= lungeDistance - .001f) Enter(AttackPhase.Gliding);
                return;
            }
            if (Phase == AttackPhase.Gliding)
            {
                float duration = Mathf.Max(.02f, missGlideSeconds);
                float before = Mathf.Clamp01((PhaseAge - dt) / duration), after = Mathf.Clamp01(PhaseAge / duration);
                // Integral of a squared ease-out velocity, continuous with the thrust at a fraction of 1.
                float distance = lungeSpeed * missGlideSpeedFraction * duration / 3f
                    * (Mathf.Pow(1f - before, 3f) - Mathf.Pow(1f - after, 3f)) * enemy.ExternalMovementMultiplier;
                GlideDistanceTravelled += MoveSafely(LungeDirection * distance, true);
                if (Phase == AttackPhase.Gliding && PhaseAge >= duration) Enter(AttackPhase.Cooldown);
                return;
            }
            if (Phase == AttackPhase.Hit)
            {
                float duration = Mathf.Max(.05f, hitSeconds);
                float before = Mathf.Clamp01((PhaseAge - dt) / duration), after = Mathf.Clamp01(PhaseAge / duration);
                float distance = hitBounceDistance * ((1f-before)*(1f-before) - (1f-after)*(1f-after));
                MoveSafely(-LungeDirection * distance * enemy.ExternalMovementMultiplier, false);
                if (PhaseAge >= duration) Enter(AttackPhase.Cooldown);
                return;
            }
            if (Phase == AttackPhase.Cooldown) { if (PhaseAge >= CurrentCooldownSeconds) Enter(AttackPhase.Stalking); return; }
            if (Phase == AttackPhase.Stalking) { TickStalking(dt); return; }
            if (Phase == AttackPhase.Charging)
            {
                if (!Eligible(Target)) { Target = null; Enter(AttackPhase.Cooldown); return; }
                if (Charge01 < aimLockFraction) Turn(Flat(Target.transform.position - body.position), chargeTrackingDegreesPerSecond, dt);
                else yawVelocity = 0f;
                if (PhaseAge >= chargeSeconds + compressionSeconds)
                {
                    LungeDirection = body.rotation * Vector3.forward; LungeStartPosition = body.position;
                    DistanceTravelled = GlideDistanceTravelled = 0f; TotalLunges++;
                    Enter(AttackPhase.Firing); enemy.PlayAttackSfx();
                    if (launchPlasma) launchPlasma.EmitBurst(transform.TransformPoint(Vector3.back * launchPlasmaBackOffset), -LungeDirection, launchDrops);
                }
                return;
            }
            decisionAge -= dt;
            if (decisionAge <= 0f) { decisionAge = Mathf.Max(.05f, decisionInterval); Acquire(); }
            TickAvoidance(dt);
            Vector3 toTarget = Eligible(Target) ? Flat(Target.transform.position - body.position) : Vector3.zero;
            if (enemy.AttacksEnabled && toTarget.sqrMagnitude > .001f && toTarget.sqrMagnitude <= attackRange * attackRange && !IsStrafing
                && reactionRemaining < 0f && CanSee(Target) && Vector3.Angle(transform.forward, toTarget) < 12f)
            { Enter(AttackPhase.Charging); return; }
            Vector3 direction = IsStrafing ? dodgeDirection : toTarget.sqrMagnitude > .001f ? toTarget.normalized
                : new Vector3(Mathf.Sin(idleClock * .32f + GetInstanceID()), 0f, Mathf.Cos(idleClock * .32f + GetInstanceID()));
            direction = avoidance.AdjustDirection(direction, out _);
            if (arenaBounds && arenaBounds.IsValid)
            {
                Vector3 ahead = body.position + direction * 1.3f;
                Vector3 correction = Flat(arenaBounds.ClampWorldPointInside(ahead, Clearance) - ahead);
                direction = (direction + correction * 2f).normalized;
            }
            float speed = IsStrafing ? strafeSpeed : Target ? enemy.Definition.moveSpeed : idleSpeed;
            velocity = Vector3.MoveTowards(velocity, direction * speed * enemy.ExternalMovementMultiplier, acceleration * dt);
            Turn(toTarget.sqrMagnitude > .001f ? toTarget : velocity, enemy.Definition.turnSpeed, dt);
            MoveSafely(velocity * dt, false);
        }
        private void TickAvoidance(float dt)
        {
            dodgeRemaining = Mathf.Max(0f, dodgeRemaining - dt);
            bool attacking = Eligible(Target) && Target.attackController && Target.attackController.IsAttacking;
            if (attacking && !observedAttack && dodgeCooldown <= 0f && IsThreat(Target) && random.NextDouble() < avoidanceChance)
            { reactionRemaining = avoidanceReactionSeconds; }
            observedAttack = attacking;
            if (reactionRemaining < 0f) return;
            reactionRemaining -= dt;
            if (reactionRemaining > 0f) return;
            reactionRemaining = -1f;
            if (!attacking || !IsThreat(Target)) return;
            Vector3 attackDirection = Flat(Target.attackController.CurrentAttackDirectionWS).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, attackDirection);
            float sign = Vector3.Dot(body.position - Target.transform.position, side);
            dodgeDirection = side * (Mathf.Abs(sign) > .05f ? Mathf.Sign(sign) : random.Next(2) == 0 ? -1f : 1f);
            if (avoidance.HasObstacleInDirection(dodgeDirection, strafeSpeed * strafeSeconds + Clearance, out _)) dodgeDirection = -dodgeDirection;
            dodgeRemaining = strafeSeconds; dodgeCooldown = avoidanceCooldown; TotalDodges++;
        }
        private bool IsThreat(PlayerControllerScript p)
        {
            Vector3 away = Flat(body.position - p.transform.position);
            return away.sqrMagnitude <= avoidanceRange * avoidanceRange
                && Vector3.Angle(Flat(p.attackController.CurrentAttackDirectionWS), away) <= avoidanceConeDegrees;
        }
    }
}
