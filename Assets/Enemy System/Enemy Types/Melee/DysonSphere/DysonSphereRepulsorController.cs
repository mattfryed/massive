using System.Collections.Generic;
using Massive.Player;
using UnityEngine;

namespace Massive.Enemies
{
    /// <summary>Experimental radial attack; the original Dyson lunge controller/prefab remain available.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(EnemyBase), typeof(Rigidbody), typeof(EnemyObstacleAvoidance))]
    public sealed class DysonSphereRepulsorController : MonoBehaviour
    {
        public enum AttackPhase { Spawning, Chase, Charge, Expand, Hold, Return, Cooldown }
        [Header("References")]
        public EnemyDefinition definition;
        public EnemyDirector sceneDirector;
        public ArenaBoundsFromVectorGrid arenaBounds;
        public DysonSpherePanels panels;
        public SphereCollider attackVolume;
        public SphereCollider hurtbox;
        public PlayerRepulsorGridPulse ripple;
        [Header("Seeking")]
        [Min(.1f)] public float detectionRange = 8f;
        [Min(.1f)] public float attackDistance = 1.5f;
        [Min(0f)] public float chaseAcceleration = 10f;
        public Vector3 shellSpinDegreesPerSecond = new Vector3(0f, 0f, 22f);
        [Header("Charge contraction")]
        [Min(.05f)] public float chargeSeconds = .75f;
        [Range(.1f, 1f), Tooltip("Fraction of the authored whole-body scale at full charge.")]
        public float chargeScale = .85f;
        [Min(0f)] public float chargePanelVibration = .008f;
        [Min(0f)] public float chargePanelFrequency = 32f;
        [Header("Radial explosion")]
        [Min(.02f)] public float expansionSeconds = .08f;
        [Min(1f)] public float burstScale = 1.15f;
        [Min(1f), Tooltip("Panel vertex radius relative to the base shell, before whole-body scaling. 1.3 = 130%.")]
        public float panelRadiusMultiplier = 1.3f;
        [Min(0f)] public float panelHoldSeconds = .1f;
        [Min(0f)] public float attackPanelVibration = .015f;
        [Min(0f)] public float attackPanelFrequency = 38f;
        [Min(.05f)] public float returnSeconds = .4f;
        [Range(.5f, 4f)] public float bounceCycles = 1.5f;
        [Range(0f, 10f)] public float bounceDamping = 3f;
        [Min(.05f)] public float cooldownSeconds = 1.65f;
        [Min(0f)] public float damageMultiplier = 1.25f;
        [Header("Repulsor recoil")]
        [Min(0f)] public float pushbackSpeed = 4.25f;
        [Min(.02f)] public float pushbackEaseSeconds = .65f;
        [Header("Grid ripple")]
        [Min(1f), Tooltip("Visual ripple travel relative to peak shell radius; does not enlarge damage.")]
        public float rippleTravelMultiplier = 2.5f;
        public AttackPhase Phase { get; private set; }
        public float PhaseAge { get; private set; }
        public float AnimationClock { get; private set; }
        public float DamageRadiusWorld => panels ? panels.ShellRadiusWorld : 0f;
        public bool IsDamaging => Phase == AttackPhase.Expand || Phase == AttackPhase.Hold || Phase == AttackPhase.Return;
        public int TotalBursts { get; private set; }
        public int TotalHits { get; private set; }
        public int TotalBlocks { get; private set; }
        public PlayerControllerScript Target { get; private set; }
        private EnemyBase enemy;
        private Rigidbody body;
        private EnemyObstacleAvoidance avoidance;
        private SphereCollider hull;
        private float hullRadius, decisionAge;
        private readonly Collider[] contacts = new Collider[128];
        private readonly RaycastHit[] obstructions = new RaycastHit[64];
        private readonly HashSet<PlayerControllerScript> resolved = new HashSet<PlayerControllerScript>();
        private ParticleSystem[] coreParticles;
        private bool particlesPaused;

        private void Awake()
        {
            enemy = GetComponent<EnemyBase>(); body = GetComponent<Rigidbody>(); avoidance = GetComponent<EnemyObstacleAvoidance>();
            hull = GetComponent<SphereCollider>(); if (hull) hullRadius = hull.radius;
            if (!panels) panels = GetComponentInChildren<DysonSpherePanels>(true);
            coreParticles = GetComponentsInChildren<ParticleSystem>(true);
            body.useGravity = false; body.constraints |= RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
        }
        private void OnEnable()
        {
            Phase = AttackPhase.Spawning; PhaseAge = decisionAge = 0f; AnimationClock = Time.time;
            TotalBursts = TotalHits = TotalBlocks = 0; Target = null; resolved.Clear();
            panels.BeginRadialControl(AnimationClock); if (ripple) ripple.SetExternalClock(AnimationClock);
            if (attackVolume) attackVolume.enabled = false;
            enemy.Died += OnDeath;
        }
        private void Start()
        {
            if (!enemy.Definition && definition)
            {
                if (sceneDirector) sceneDirector.RegisterAuthoredEnemy(enemy, definition);
                else enemy.Init(definition, null);
            }
            if (!arenaBounds) arenaBounds = enemy.Director ? enemy.Director.arenaBounds : FindFirstObjectByType<ArenaBoundsFromVectorGrid>();
        }
        private void OnDisable()
        {
            enemy.Died -= OnDeath; if (attackVolume) attackVolume.enabled = false;
            if (ripple) ripple.ClearPulses(); if (panels) panels.EndRadialControl();
            if (hull) hull.radius = hullRadius;
        }
        private void OnDeath(EnemyBase e, EnemyDamageSource cause)
        { if (attackVolume) attackVolume.enabled = false; if (ripple) ripple.ClearPulses(); }
        private void Enter(AttackPhase next) { Phase = next; PhaseAge = 0f; }
        private bool Eligible(PlayerControllerScript p) => enemy && enemy.CanTarget(p);
        private void Acquire()
        {
            if (Eligible(Target) && Vector3.Distance(Target.transform.position, body.position) <= detectionRange) return;
            Target = null; float best = detectionRange * detectionRange;
            foreach (var p in PlayerControllerScript.ActivePlayers)
            {
                if (!Eligible(p)) continue;
                float d = (p.transform.position - body.position).sqrMagnitude;
                if (d < best) { Target = p; best = d; }
            }
        }
        private bool Unobstructed(Vector3 point)
        {
            Vector3 delta = point - panels.transform.position; float distance = delta.magnitude;
            if (distance < .001f) return true;
            int count = Physics.RaycastNonAlloc(panels.transform.position, delta / distance, obstructions, distance,
                avoidance.ObstacleMask, QueryTriggerInteraction.Collide);
            if (count == obstructions.Length) return false;
            for (int i = 0; i < count; i++) if (avoidance.ShouldAvoid(obstructions[i].collider)) return false;
            return true;
        }
        private void FixedUpdate()
        {
            if (!enemy.Definition) return;
            if (particlesPaused != enemy.IsPaused)
            {
                particlesPaused = enemy.IsPaused;
                foreach (var ps in coreParticles) if (ps) { if (particlesPaused) ps.Pause(false); else ps.Play(false); }
            }
            if (enemy.IsPaused) return;
            float dt = Time.fixedDeltaTime; AnimationClock += dt;
            if (ripple) ripple.SetExternalClock(AnimationClock);
            if (enemy.IsDead)
            {
                // Let health-driven panel shatter finish on the same clock, without restarting damage.
                panels.SetRadialPose(AnimationClock, panels.BodyScaleMultiplier, 0f, 1f, 0f, 0f); return;
            }
            if (Target && !Eligible(Target)) Target = null;
            PhaseAge += dt; decisionAge -= dt;
            if (decisionAge <= 0f) { decisionAge = .2f; Acquire(); }
            if (Phase != AttackPhase.Chase && !body.isKinematic) body.linearVelocity = Vector3.zero;
            switch (Phase)
            {
                case AttackPhase.Spawning: if (!panels.IsSpawning) Enter(AttackPhase.Chase); break;
                case AttackPhase.Chase: Chase(dt); break;
                case AttackPhase.Charge:
                    if (!Eligible(Target)) Enter(AttackPhase.Cooldown);
                    else if (PhaseAge >= chargeSeconds)
                    {
                        Enter(AttackPhase.Expand); resolved.Clear(); TotalBursts++; enemy.PlayAttackSfx();
                        if (ripple) ripple.TriggerWorldPulse(panels.transform.position, DamageRadiusWorld,
                            panels.radius * MaxScale(panels.transform.lossyScale) / panels.BodyScaleMultiplier
                            * burstScale * panelRadiusMultiplier * rippleTravelMultiplier);
                    }
                    break;
                case AttackPhase.Expand: if (PhaseAge >= Mathf.Max(.02f, expansionSeconds)) Enter(AttackPhase.Hold); break;
                case AttackPhase.Hold: if (PhaseAge >= panelHoldSeconds) Enter(AttackPhase.Return); break;
                case AttackPhase.Return: if (PhaseAge >= Mathf.Max(.05f, returnSeconds)) Enter(AttackPhase.Cooldown); break;
                case AttackPhase.Cooldown: if (PhaseAge >= cooldownSeconds) Enter(AttackPhase.Chase); break;
            }
            panels.transform.Rotate(shellSpinDegreesPerSecond * dt, Space.Self);
            ApplyPose();
            if (IsDamaging) ResolveDamage();
        }
        private void Chase(float dt)
        {
            if (enemy.HoldPosition) { if (!body.isKinematic) body.linearVelocity = Vector3.zero; return; }
            Vector3 delta = Target ? Target.transform.position - body.position : Vector3.zero; delta.y = 0f;
            if (enemy.AttacksEnabled && Target && delta.magnitude <= attackDistance && Unobstructed(Target.transform.position))
            { if (!body.isKinematic) body.linearVelocity = Vector3.zero; Enter(AttackPhase.Charge); return; }
            Vector3 direction = delta.sqrMagnitude > .0001f ? avoidance.AdjustDirection(delta.normalized, out _) : Vector3.zero;
            if (arenaBounds && arenaBounds.IsValid)
            {
                Vector3 ahead = body.position + direction;
                Vector3 correction = arenaBounds.ClampWorldPointInside(ahead, hullRadius * MaxScale(transform.lossyScale)) - ahead;
                correction.y = 0f; direction = (direction + correction * 2f).normalized;
            }
            if (!body.isKinematic) body.linearVelocity = Vector3.MoveTowards(body.linearVelocity,
                direction * enemy.Definition.moveSpeed * enemy.ExternalMovementMultiplier, chaseAcceleration * dt);
        }
        private void ApplyPose()
        {
            float scale = 1f, blend = 0f, radius = 1f, vibration = 0f, frequency = chargePanelFrequency;
            if (Phase == AttackPhase.Charge)
            {
                blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(PhaseAge / Mathf.Max(.05f, chargeSeconds)));
                scale = Mathf.Lerp(1f, chargeScale, blend); vibration = chargePanelVibration;
            }
            else if (Phase == AttackPhase.Expand || Phase == AttackPhase.Hold)
            {
                float u = Phase == AttackPhase.Hold ? 1f : 1f - Mathf.Pow(1f - Mathf.Clamp01(PhaseAge / Mathf.Max(.02f, expansionSeconds)), 3f);
                scale = Mathf.Lerp(chargeScale, burstScale, u); radius = Mathf.Lerp(1f, panelRadiusMultiplier, u);
                blend = 1f; vibration = Mathf.Lerp(chargePanelVibration, attackPanelVibration, u); frequency = attackPanelFrequency;
            }
            else if (Phase == AttackPhase.Return)
            {
                float u = Mathf.Clamp01(PhaseAge / Mathf.Max(.05f, returnSeconds));
                blend = 1f - Mathf.SmoothStep(0f, 1f, u); radius = panelRadiusMultiplier;
                scale = 1f + (burstScale - 1f) * Mathf.Cos(u * bounceCycles * Mathf.PI * 2f)
                    * Mathf.Exp(-bounceDamping * u) * (1f - u);
                vibration = attackPanelVibration; frequency = attackPanelFrequency;
            }
            panels.SetRadialPose(AnimationClock, scale, blend, radius, vibration, frequency);
            if (hull) hull.radius = hullRadius * scale;
            FitSphere(hurtbox); FitSphere(attackVolume);
            if (attackVolume) attackVolume.enabled = IsDamaging;
        }
        private void FitSphere(SphereCollider sphere)
        {
            if (!sphere) return;
            sphere.center = sphere.transform.InverseTransformPoint(panels.transform.position);
            sphere.radius = DamageRadiusWorld / Mathf.Max(.0001f, MaxScale(sphere.transform.lossyScale));
        }
        private static float MaxScale(Vector3 v) => Mathf.Max(Mathf.Abs(v.x), Mathf.Max(Mathf.Abs(v.y), Mathf.Abs(v.z)));
        private void ResolveDamage()
        {
            int count = Physics.OverlapSphereNonAlloc(panels.transform.position, DamageRadiusWorld, contacts, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                var c = contacts[i]; if (!c || !c.CompareTag("Player")) continue;
                var p = c.GetComponentInParent<PlayerControllerScript>();
                if (!Eligible(p) || resolved.Contains(p) || !Unobstructed(c.ClosestPoint(panels.transform.position))) continue;
                resolved.Add(p);
                if (p.shieldOn) { TotalBlocks++; continue; }
                if (!p.IsInvulnerable && p.ApplyExternalMassDelta(-enemy.Definition.damageToPlayerMass01 * damageMultiplier,
                    gameObject, true, true).accepted)
                {
                    TotalHits++;
                    p.ApplyEnemyRepulsorRecoil(p.transform.position - panels.transform.position, pushbackSpeed, pushbackEaseSeconds);
                }
            }
        }
        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || !panels) return;
            Gizmos.color = IsDamaging ? Color.red : Color.white;
            Gizmos.DrawWireSphere(panels.transform.position, DamageRadiusWorld);
        }
    }
}
