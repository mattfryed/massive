using System;
using UnityEngine;

namespace Massive.Enemies
{
    /// <summary>A mostly stationary mothership with gentle turret-lane clearance. Docked drones are presentation; released drones use the normal enemy prefab.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(EnemyBase), typeof(Rigidbody))]
    public sealed partial class CarrierController : MonoBehaviour
    {
        public enum CyclePhase { Launching, Cooldown, Rebuilding }
        public EnemyDefinition definition;
        public EnemyDefinition droneDefinition;
        [Tooltip("Optional scene owner for a hand-placed Carrier. Director-spawned instances receive this automatically through EnemyBase.")]
        public EnemyDirector sceneDirector;
        [Tooltip("Clockwise launch order. Each transform contains a visual-only Drone.")]
        public Transform[] docks = new Transform[6];
        [Tooltip("Solid hull used by Drone obstacle sweeps; separate from the damage trigger.")]
        public Collider shellCollider;
        [Tooltip("Dedicated child hurtbox so shell contacts cannot duplicate sword damage.")]
        public SphereCollider damageTrigger;
        [Header("Arrival — shared enemy warning, then face assembly")]
        public EnemySpawnTelegraph spawnTelegraphPrefab;
        [Tooltip("Set to zero if an external spawn rule already supplies the warning.")]
        [Min(0f)] public float spawnWarningSeconds = 3f;
        [Min(0f)] public float initialLaunchDelay = 1f;
        [Min(.05f)] public float launchInterval = .5f;
        [Min(0f)] public float cooldownSeconds = 5f;
        [Min(.05f)] public float respawnInterval = .5f;
        [Min(0f)] public float launchSpeed = 3.5f;
        [Min(.05f)] public float launchClearanceSeconds = .45f;
        [Header("Boundary-aware departures")]
        [Tooltip("Within this distance of an arena edge, prefer a departure that turns back toward open space.")]
        [Min(.1f)] public float boundaryInfluenceDistance = 2.5f;
        [Tooltip("Initial outward flight before a boundary turn; lets the Drone's tail clear the shell.")]
        [Min(.05f)] public float launchStraightSeconds = .12f;
        [Min(.1f)] public float launchBendSeconds = .55f;
        [Tooltip("Standalone/legacy safety cap. With an authored timeline, only its Composer population limits apply.")]
        [Min(6)] public int maxActiveDrones = 24;
        public CyclePhase Phase { get; private set; }
        public int NextSlot { get; private set; }
        public float RemainingSeconds { get; private set; }
        public int TotalLaunched { get; private set; }
        public int CompletedCycles { get; private set; }
        public float SpawnAge { get; private set; }
        public bool IsSpawnReady { get; private set; }
        public bool IsRevealing { get; private set; }
        public EnemySpawnTelegraph SpawnWarning => warning;
        public float RevealProgress => IsRevealing ? Mathf.Clamp01((SpawnAge - WarningDuration) / FormationDuration) : 0f;
        public event Action<int, EnemyBase> DroneLaunched;
        public event Action<int> DroneRebuilt;
        public int DockedCount
        {
            get { int n = 0; foreach (var dock in docks) if (dock && dock.gameObject.activeSelf) n++; return n; }
        }
        private EnemyBase enemy;
        private CarrierVisuals visuals;
        private SphereCollider hitbox;
        private bool hitboxWasEnabled;
        private bool shellWasEnabled;
        private EnemySpawnTelegraph warning;
        private float WarningDuration => spawnTelegraphPrefab ? Mathf.Max(0f, spawnWarningSeconds) : 0f;
        private float FormationDuration => visuals ? Mathf.Max(.01f, visuals.spawnSeconds) : .01f;
        private ArenaBoundsFromVectorGrid bounds;
        private readonly Collider[] launchOverlaps = new Collider[32];

        private void Awake()
        {
            enemy = GetComponent<EnemyBase>();
            // The hull is an immovable obstacle except for its deliberate, swept sidestep.
            // Kinematic MovePosition also supports older scene instances with FreezeAll.
            var body = GetComponent<Rigidbody>(); body.isKinematic = true;
            body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
        }
        private void OnEnable()
        {
            enemy = GetComponent<EnemyBase>(); ResetLaneAvoidance();
            visuals = GetComponent<CarrierVisuals>(); hitbox = damageTrigger ? damageTrigger : GetComponent<SphereCollider>();
            hitboxWasEnabled = hitbox && hitbox.enabled;
            if (hitbox) hitbox.enabled = false;
            shellWasEnabled = shellCollider && shellCollider.enabled;
            if (shellCollider) shellCollider.enabled = false;
            enemy.Died += OnDeath;
            SpawnAge = 0f; IsSpawnReady = IsRevealing = false;
            Phase = CyclePhase.Launching; NextSlot = 0; RemainingSeconds = initialLaunchDelay;
            TotalLaunched = CompletedCycles = 0;
            foreach (var dock in docks) if (dock) dock.gameObject.SetActive(false);
        }
        private void Start()
        {
            if (enemy.Definition == null && definition)
            {
                if (sceneDirector) sceneDirector.RegisterAuthoredEnemy(enemy, definition);
                else enemy.Init(definition, null);
            }
            bounds = enemy.Director ? enemy.Director.arenaBounds : null;
            if (!bounds)
                foreach (var candidate in FindObjectsByType<ArenaBoundsFromVectorGrid>(FindObjectsSortMode.None))
                    if (candidate.gameObject.scene == gameObject.scene) { bounds = candidate; break; }
        }
        private void OnDisable()
        {
            if (enemy) enemy.Died -= OnDeath;
            ResetLaneAvoidance(); CancelWarning();
            if (hitbox) hitbox.enabled = hitboxWasEnabled;
            if (shellCollider) shellCollider.enabled = shellWasEnabled;
        }
        private void OnDeath(EnemyBase source, EnemyDamageSource cause)
        {
            ResetLaneAvoidance(); CancelWarning();
            foreach (var dock in docks) if (dock) dock.gameObject.SetActive(false);
        }
        private void CancelWarning() { if (warning) warning.Cancel(); warning = null; }
        private void TickSpawn(float delta)
        {
            if (SpawnAge == 0f && WarningDuration > 0f)
            {
                warning = Instantiate(spawnTelegraphPrefab, transform.position, transform.rotation, transform);
                warning.Begin(WarningDuration, transform.lossyScale, visuals ? visuals.CreateSpawnOutline() : null);
            }
            SpawnAge += delta;
            if (!IsRevealing && SpawnAge >= WarningDuration)
            {
                IsRevealing = true;
                if (warning) warning.Complete();
                foreach (var dock in docks) if (dock) dock.gameObject.SetActive(true);
            }
            if (RevealProgress >= 1f)
            {
                IsSpawnReady = true;
                if (hitbox) hitbox.enabled = hitboxWasEnabled;
                if (shellCollider) shellCollider.enabled = shellWasEnabled;
            }
        }
        private void Update()
        {
            if (visuals) visuals.ApplyBodyScale();
            if (!enemy || enemy.IsDead || enemy.IsPaused || !enemy.Definition || !droneDefinition || !droneDefinition.prefab || docks.Length != 6) return;
            if (warning && !warning.Advance(Time.deltaTime)) warning = null;
            if (!IsSpawnReady) { TickSpawn(Time.deltaTime); return; }
            RemainingSeconds -= Time.deltaTime;
            if (RemainingSeconds > .00001f) return;
            if (Phase == CyclePhase.Cooldown)
            { Phase = CyclePhase.Rebuilding; NextSlot = 0; }
            if (Phase == CyclePhase.Rebuilding)
            {
                if (docks[NextSlot]) docks[NextSlot].gameObject.SetActive(true);
                DroneRebuilt?.Invoke(NextSlot);
                NextSlot++;
                RemainingSeconds = Mathf.Max(.05f, respawnInterval);
                if (NextSlot == 6)
                {
                    CompletedCycles++; NextSlot = 0; Phase = CyclePhase.Launching;
                    RemainingSeconds = Mathf.Max(.05f, launchInterval);
                }
                return;
            }
            // A blocked bay waits in order; it never silently skips or moves its launch point.
            if (!TryLaunch(NextSlot)) { RemainingSeconds = .1f; return; }
            NextSlot++;
            if (NextSlot == 6)
            { NextSlot = 0; Phase = CyclePhase.Cooldown; RemainingSeconds = Mathf.Max(0f, cooldownSeconds); }
            else RemainingSeconds = Mathf.Max(.05f, launchInterval);
        }
        private bool TryLaunch(int slot)
        {
            if (!enemy.AttacksEnabled) return false;
            Transform dock = docks[slot];
            if (!dock) return false;
            if (!enemy.Director || !enemy.Director.encounterTimeline)
            {
                int count = 0;
                foreach (var live in EnemyBase.ActiveEnemies)
                    if (live && !live.IsDead && live.gameObject.scene == gameObject.scene && live.Definition == droneDefinition) count++;
                int cap = droneDefinition.maxAliveOverride > 0 ? Mathf.Min(maxActiveDrones, droneDefinition.maxAliveOverride) : maxActiveDrones;
                if (count >= cap) return false;
            }
            float clearance = Mathf.Max(.1f, droneDefinition.spawnRadiusWorld);
            if (bounds && !bounds.ContainsWorldPoint(dock.position, clearance)) return false;
            if (!TryPlanLaunch(dock, clearance, out var trajectory)) return false;
            int overlaps = Physics.OverlapSphereNonAlloc(dock.position, .24f, launchOverlaps, (1 << 9) | (1 << 11), QueryTriggerInteraction.Collide);
            if (overlaps > 0) return false;
            EnemyBase drone;
            if (enemy.Director)
            {
                if (!enemy.Director.TryLaunchEnemy(droneDefinition, dock.position, dock.rotation, out drone)) return false;
            }
            else
            {
                var go = Instantiate(droneDefinition.prefab, dock.position, dock.rotation);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameObject.scene);
                drone = go.GetComponent<EnemyBase>();
                drone.Init(droneDefinition, null);
            }
            drone.OwnerTeamId = enemy.OwnerTeamId;
            drone.GetComponent<DroneController>().Launch(trajectory, shellCollider);
            dock.gameObject.SetActive(false);
            TotalLaunched++;
            enemy.PlayAttackSfx();
            DroneLaunched?.Invoke(slot, drone);
            return true;
        }
    }
}
