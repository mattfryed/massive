using System.Collections;
using System.Collections.Generic;
using Massive.Enemies;
using Massive.Player;
using TMPro;
using UnityEngine;

namespace Massive.Demonstrations
{
    public sealed class EnemyLabColumn : MonoBehaviour
    {
        public EnemyLab lab;
        public EnemyDefinition definition;
        public GameObject playerPrefab;
        public Transform enemyStart, playerStart;
        public VectorGridGPU grid;
        public EnemySpawnTelegraph externalTelegraph;
        public TMP_Text status;
        [Min(1)] public int attacksPerLoop = 3;
        [Min(.1f)] public float warningSeconds = 1.5f;
        [Min(0f)] public float counterPause = .7f, respawnPause = 1.2f;
        public float columnHalfWidth = 2f;
        public GameObject Session { get; private set; }
        public PlayerControllerScript Player { get; private set; }
        public EnemyBase Enemy { get; private set; }
        public int CompletedLoops { get; private set; }
        public int Counterkills { get; private set; }
        public int LastAttackCount { get; private set; }
        public int LastShotCount { get; private set; }
        public string Phase { get; private set; } = "Ready";
        public string Failure { get; private set; }
        private DroneController drone;
        private RangedDroneController ranged;
        private CarrierController carrier;
        private SeekerController seeker;
        private ParticleBeamTurretController turret;
        private DysonSphereRepulsorController dyson;
        private PlayerShieldAbility shield;
        private readonly List<GameObject> spawned = new();
        private float lastHitTime, nextIsolation;
        private bool counterConfirmed;
        public int Attacks => ranged ? ranged.TotalBursts :
            carrier ? carrier.TotalLaunched : turret ? turret.TotalBlasts : seeker ? seeker.TotalLunges :
            dyson ? dyson.TotalBursts : drone ? drone.TotalContacts : LastAttackCount;
        public PlayerControllerScript CurrentTarget => drone ? drone.Target : turret ? turret.Target : seeker ? seeker.Target : dyson ? dyson.Target : null;
        private bool Settled => ranged ? ranged.Phase == RangedDroneController.AttackPhase.Cooldown :
            turret ? turret.Phase == ParticleBeamTurretController.AttackPhase.Cooldown :
            seeker ? seeker.Phase == SeekerController.AttackPhase.Cooldown || seeker.Phase == SeekerController.AttackPhase.Stalking :
            dyson ? dyson.Phase == DysonSphereRepulsorController.AttackPhase.Cooldown : true;
        private void OnEnable() { if (Application.isPlaying) StartCoroutine(Sequence()); }
        private void OnDisable()
        {
            StopAllCoroutines(); if (Session) Destroy(Session);
            foreach (var obj in spawned) if (obj) Destroy(obj); spawned.Clear();
            Session = null; Player = null; Enemy = null;
        }
        private IEnumerator Sequence()
        {
            if (!definition || !definition.prefab || !playerPrefab) { Fail("Missing prefab"); yield break; }
            Session = new GameObject("Runtime actors"); Session.transform.SetParent(transform, false); Session.SetActive(false);
            var go = Instantiate(playerPrefab, playerStart.position, Quaternion.identity, Session.transform);
            go.name = "Player actor"; Player = go.GetComponent<PlayerControllerScript>();
            Player.ConfigureDemonstration(Session.transform, 0, 1);
            foreach (var interactor in go.GetComponentsInChildren<GridInteractor>(true)) interactor.grid = grid;
            foreach (var pulse in go.GetComponentsInChildren<PlayerRepulsorGridPulse>(true)) pulse.BindGrid(grid);
            var scale = go.GetComponent<PlayerScaleAdjuster>(); if (scale) scale.ApplyScale();
            shield = go.GetComponent<PlayerShieldAbility>();
            Player.HitAccepted += hit => { lastHitTime = Time.time; Player.ApplyExternalMassDelta(hit.massLost01); };
            Session.SetActive(true); yield return null; yield return null;
            lab.Isolate(this, go);
            while (isActiveAndEnabled)
            {
                Phase = "Spawn telegraph"; LastAttackCount = 0; counterConfirmed = false;
                EnemySpawnTelegraph warning = null;
                if (externalTelegraph)
                {
                    warning = Instantiate(externalTelegraph, enemyStart.position, enemyStart.rotation, Session.transform);
                    warning.Begin(warningSeconds, definition.prefab.transform.localScale);
                    for (float age = 0; age < warningSeconds; age += Time.deltaTime) { warning.Advance(Time.deltaTime); yield return null; }
                    warning.Complete();
                }
                SpawnEnemy();
                if (warning)
                {
                    while (warning.Advance(Time.deltaTime)) { Defend(); yield return null; }
                    Destroy(warning.gameObject);
                }
                Phase = "Enemy attacks";
                float deadline = Time.time + 100f;
                while (Enemy && !Enemy.IsDead && (Attacks < attacksPerLoop || !Settled))
                {
                    if (Attacks >= attacksPerLoop) Enemy.AttacksEnabled = false;
                    Defend();
                    if (Time.time > deadline) { Fail("Attack cycle timed out"); yield break; }
                    yield return null;
                }
                if (!Enemy || Enemy.IsDead) { Fail("Enemy died before counterattack"); yield break; }
                LastAttackCount = Attacks;
                if (ranged) LastShotCount = ranged.TotalShots;
                Enemy.AttacksEnabled = false; Enemy.HoldPosition = true;
                // Allow the third carrier drone / last ranged projectile to finish its real attack.
                if (carrier || ranged) { float end = Time.time + 2f; while (Time.time < end) { Defend(); yield return null; } }
                foreach (var obj in spawned) if (obj) Destroy(obj); spawned.Clear();
                Phase = "Player counterattack"; Frame(Vector2.zero, Enemy.transform.position - Player.transform.position);
                if (shield) shield.ForceStopShield();
                yield return new WaitForSeconds(counterPause);
                Enemy.DemonstrationCounterkill = true;
                deadline = Time.time + 16f;
                while (Enemy && !Enemy.IsDead && Vector3.Distance(Player.transform.position, Enemy.transform.position) > 1.3f)
                {
                    Move(Enemy.transform.position, 1.15f);
                    if (Time.time > deadline) { Fail("Player could not approach enemy"); yield break; }
                    yield return null;
                }
                Vector3 aim = Enemy.transform.position - Player.transform.position;
                Frame(new Vector2(aim.x, aim.z).normalized, aim, attack: true); yield return null;
                Frame(Vector2.zero, aim, release: true); yield return null;
                Frame(Vector2.zero, aim);
                deadline = Time.time + 4f;
                while (!counterConfirmed && Time.time < deadline) yield return null;
                if (!counterConfirmed) { Fail("Player counterattack missed"); yield break; }
                Phase = "Defeated"; yield return new WaitForSeconds(respawnPause);
                Phase = "Returning"; deadline = Time.time + 16f;
                while (Vector3.Distance(Player.transform.position, playerStart.position) > .12f)
                {
                    Move(playerStart.position, 0f);
                    if (Time.time > deadline) { Fail("Player could not return home"); yield break; }
                    yield return null;
                }
                Frame(Vector2.zero, Vector3.forward); Player.ApplyExternalMassDelta(1f);
                CompletedLoops++; yield return new WaitForSeconds(.4f);
            }
        }
        private void SpawnEnemy()
        {
            // Inactive staging guarantees demo scope and mounting are installed before OnEnable.
            var staging = new GameObject("Spawn staging"); staging.transform.SetParent(Session.transform, false); staging.SetActive(false);
            var go = Instantiate(definition.prefab, enemyStart.position, enemyStart.rotation, staging.transform);
            Enemy = go.GetComponent<EnemyBase>(); Enemy.ConfigureDemonstration(Session.transform, Player); Enemy.Init(definition, null);
            drone = go.GetComponent<DroneController>(); ranged = go.GetComponent<RangedDroneController>();
            carrier = go.GetComponent<CarrierController>(); seeker = go.GetComponent<SeekerController>();
            turret = go.GetComponent<ParticleBeamTurretController>(); dyson = go.GetComponent<DysonSphereRepulsorController>();
            float detect = Vector3.Distance(enemyStart.position, playerStart.position) + 3f;
            if (drone) { drone.detectionRange = Mathf.Max(drone.detectionRange, detect); drone.disengageRange = Mathf.Max(drone.disengageRange, detect); }
            if (turret)
            {
                turret.sceneDirector = null; turret.mount = ParticleBeamTurretController.WallMount.Top;
                foreach (var bounds in FindObjectsByType<ArenaBoundsFromVectorGrid>(FindObjectsSortMode.None))
                    if (bounds.Grid == grid) { turret.arenaBounds = bounds; bounds.RefreshNow(); break; }
                if (turret.arenaBounds)
                    turret.wallPosition = grid.transform.InverseTransformPoint(enemyStart.position).x / turret.arenaBounds.Current.halfSizeLocal.x;
                turret.wallInset = 0f;
                turret.detectionRange = Mathf.Max(turret.detectionRange, detect);
            }
            if (carrier) { carrier.sceneDirector = null; carrier.DroneLaunched += (_, child) => { child.ConfigureDemonstration(Session.transform, Player); Own(child.gameObject); }; }
            if (seeker) { seeker.sceneDirector = null; seeker.detectionRange = Mathf.Max(seeker.detectionRange, detect); seeker.disengageRange = Mathf.Max(seeker.disengageRange, detect); }
            if (dyson) { dyson.sceneDirector = null; dyson.detectionRange = Mathf.Max(dyson.detectionRange, detect); }
            if (ranged) { ranged.sceneDirector = null; ranged.ShotFired += shot => Own(shot.gameObject); }
            Enemy.Defeated += defeat => { if (defeat.creditedPlayer == Player) { counterConfirmed = true; Counterkills++; } };
            go.transform.SetParent(Session.transform, true); Destroy(staging); lab.Isolate(this, go);
        }
        private void Own(GameObject obj) { obj.transform.SetParent(Session.transform, true); spawned.Add(obj); lab.Isolate(this, obj); }
        private void Defend()
        {
            if (!Player || !Enemy) return;
            var delta = Enemy.transform.position - Player.transform.position;
            // Step inside the range during the telegraph so projectile recoil cannot cancel the burst.
            if (ranged && ranged.Phase == RangedDroneController.AttackPhase.Charging)
            {
                Move(Enemy.transform.position, Mathf.Max(1f, ranged.firingRange - .8f));
                return;
            }
            if (ranged && ranged.Phase == RangedDroneController.AttackPhase.Bursting)
            {
                Frame(Vector2.zero, delta);
                return;
            }
            bool block = drone && !ranged && delta.magnitude < 1.7f && Attacks < attacksPerLoop;
            // Release between rebounds, giving the real shield time to re-arm.
            block &= shield && (shield.IsActive || shield.ActivationCooldownRemaining <= 0f);
            Frame(Vector2.zero, delta, block: block);
            if (!block && Time.time - lastHitTime > 1f && (Player.transform.position - playerStart.position).sqrMagnitude > .15f)
                Move(playerStart.position, 0f);
        }
        private void Frame(Vector2 move, Vector3 aim, bool attack = false, bool release = false, bool block = false)
        {
            Player.SetScriptedInput(new PlayerInputFrame { moveInput = move, hasAimDirWS = true, aimDirWS = aim,
                attackDown = attack, attackHeld = attack, attackUp = release,
                shieldDown = block && shield && !shield.IsActive, shieldHeld = block });
        }
        private void Move(Vector3 destination, float stop)
        {
            Vector3 delta = destination - Player.transform.position; delta.y = 0f;
            Vector2 stick = new Vector2(delta.x, delta.z).normalized * Mathf.Clamp01((delta.magnitude - stop) * 1.6f);
            Frame(stick, delta.sqrMagnitude > .001f ? delta : Vector3.forward);
        }
        private void Fail(string message) { Failure = message; Phase = message; if (Player) Player.ClearScriptedInput(); Debug.LogError("[Enemy Lab] " + name + ": " + message, this); }
        private void Update()
        {
            if (status) status.text = Phase + (Phase == "Enemy attacks" ? " " + Mathf.Min(Attacks, attacksPerLoop) + "/" + attacksPerLoop : "") + "  |  " + CompletedLoops;
            if (Session && Time.time >= nextIsolation) { nextIsolation = Time.time + .5f; lab.Isolate(this, Session); }
        }
    }
}
