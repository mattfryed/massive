using System;
using UnityEngine;

namespace Massive.Enemies
{
    /// <summary>Attack-only companion to the shared Drone perception, movement and visuals.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(DroneController))]
    public sealed class RangedDroneController : MonoBehaviour
    {
        public enum AttackPhase { Seeking, Charging, Bursting, Cooldown }
        public EnemyDirector sceneDirector;
        public RangedDroneProjectile projectilePrefab;
        [Min(.1f)] public float firingRange = 4.5f;
        [Min(.05f)] public float chargeSeconds = 2f;
        [Min(.05f)] public float burstSeconds = 1.5f;
        [Min(.05f)] public float shotInterval = .5f;
        [Min(1)] public int shotsPerBurst = 3;
        [Min(.05f)] public float cooldownSeconds = 3f;
        [Tooltip("Local forward offset from the metaball core. Zero emits directly from the core.")]
        [Min(0f)] public float muzzleDistance;
        [Tooltip("Maximum shot travel relative to the positioning/firing range, including reflected travel.")]
        [Min(.1f)] public float projectileRangeMultiplier = 1.5f;
        public LayerMask sightMask = 2753;
        public AttackPhase Phase { get; private set; }
        public float PhaseAge { get; private set; }
        public float Charge01 => Phase == AttackPhase.Charging ? Mathf.Clamp01(PhaseAge / chargeSeconds) : 0f;
        public float ShotPulse { get; private set; }
        public int TotalShots { get; private set; }
        public int TotalBursts { get; private set; }
        public event Action<RangedDroneProjectile> ShotFired;
        private EnemyBase enemy;
        private DroneController drone;
        private int shots;
        private PlayerControllerScript burstTarget;
        private readonly RaycastHit[] sightHits = new RaycastHit[32];

        private void Awake() { enemy = GetComponent<EnemyBase>(); drone = GetComponent<DroneController>(); }
        private void OnEnable()
        {
            Phase = AttackPhase.Seeking; PhaseAge = ShotPulse = 0f; TotalBursts = TotalShots = shots = 0; burstTarget = null;
            enemy.Died += OnDeath;
        }
        private void Start()
        {
            if (sceneDirector) sceneDirector.RegisterAuthoredEnemy(enemy, drone.definition);
        }
        private void OnDisable() { enemy.Died -= OnDeath; burstTarget = null; ShotPulse = 0f; }
        private void OnDeath(EnemyBase source, EnemyDamageSource cause)
        { Phase = AttackPhase.Seeking; PhaseAge = ShotPulse = 0f; burstTarget = null; }

        public bool ShouldHoldPosition(PlayerControllerScript target) =>
            Phase == AttackPhase.Charging || Phase == AttackPhase.Bursting ||
            (target && InRange(target) && HasLineOfSight(target));

        private bool InRange(PlayerControllerScript target)
        {
            Vector3 delta = target.transform.position - transform.position; delta.y = 0f;
            return delta.sqrMagnitude <= firingRange * firingRange;
        }
        private bool HasLineOfSight(PlayerControllerScript target)
        {
            Vector3 delta = target.transform.position - transform.position; delta.y = 0f;
            int count = Physics.RaycastNonAlloc(transform.position, delta.normalized, sightHits, delta.magnitude,
                sightMask, QueryTriggerInteraction.Ignore);
            if (count == sightHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var hit = sightHits[i].collider;
                if (!hit || hit.transform.IsChildOf(transform) || hit.GetComponentInParent<PlayerControllerScript>()) continue;
                var otherEnemy = hit.GetComponentInParent<EnemyBase>();
                if (otherEnemy && !enemy.SharesSimulationWith(otherEnemy)) continue;
                if (hit.GetComponentInParent<Massive.Resonance.ResonanceSegment>()) continue;
                if (hit.bounds.max.y < transform.position.y - .05f || hit.GetComponent<VectorGridGPU>()) continue;
                return false;
            }
            return true;
        }
        private void Enter(AttackPhase phase) { Phase = phase; PhaseAge = 0f; }

        // Called by DroneController inside its fixed step; there is one movement/attack clock and pause gate.
        public void TickAttack(float delta, PlayerControllerScript target)
        {
            if (!isActiveAndEnabled || !enemy || enemy.IsDead || enemy.IsPaused) return;
            ShotPulse = Mathf.MoveTowards(ShotPulse, 0f, delta / .3f);
            PhaseAge += delta;
            bool valid = target && InRange(target) && HasLineOfSight(target);
            if ((Phase == AttackPhase.Charging || Phase == AttackPhase.Bursting) && (!valid || target != burstTarget))
            { burstTarget = null; Enter(AttackPhase.Cooldown); return; }
            switch (Phase)
            {
                case AttackPhase.Seeking:
                    if (enemy.AttacksEnabled && valid && projectilePrefab) { burstTarget = target; Enter(AttackPhase.Charging); }
                    break;
                case AttackPhase.Charging:
                    if (PhaseAge + .00001f >= chargeSeconds)
                    { shots = 0; TotalBursts++; Enter(AttackPhase.Bursting); Fire(target); }
                    break;
                case AttackPhase.Bursting:
                    if (shots < shotsPerBurst && PhaseAge + .00001f >= shots * shotInterval) Fire(target);
                    if (PhaseAge + .00001f >= Mathf.Max(burstSeconds, shotsPerBurst * shotInterval))
                    { burstTarget = null; Enter(AttackPhase.Cooldown); }
                    break;
                case AttackPhase.Cooldown:
                    if (PhaseAge + .00001f >= cooldownSeconds) Enter(AttackPhase.Seeking);
                    break;
            }
        }
        private void Fire(PlayerControllerScript target)
        {
            Vector3 muzzle = transform.TransformPoint(Vector3.forward * muzzleDistance);
            Vector3 direction = target.transform.position - muzzle; direction.y = 0f;
            direction = direction.sqrMagnitude > .0001f ? direction.normalized : transform.forward;
            var projectile = Instantiate(projectilePrefab, muzzle, Quaternion.LookRotation(direction, Vector3.up));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(projectile.gameObject, gameObject.scene);
            projectile.Init(enemy, direction);
            projectile.maxTravelDistance = firingRange * projectileRangeMultiplier;
            shots++; TotalShots++; ShotPulse = 1f; enemy.PlayAttackSfx(); ShotFired?.Invoke(projectile);
        }
    }
}
