using System.Collections.Generic;
using UnityEngine;

namespace Massive.Enemies
{
    /// <summary>
    /// Scene-level spawner / manager for enemies.
    /// - Owns timing, selection and safe spawning.
    /// - Tracks alive enemies to enforce caps.
    /// - Optionally freezes/spawn-pauses when AnomalyManager is running.
    ///
    /// This is Step A: foundation only (no special behaviors per enemy type yet).
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyDirector : MonoBehaviour
    {
        [Header("Profile")]
        [Tooltip("Defines what enemy types can spawn and how often.")]
        public EnemySpawnProfile spawnProfile;

        [Header("Arena / Placement")]
        [Tooltip("Playable bounds provider. If left empty, we'll auto-find one.")]
        public ArenaBoundsFromVectorGrid arenaBounds;

        [Tooltip("Do not spawn inside this padding from the arena border (world units).")]
        [Min(0f)] public float borderBufferWorld = 0.75f;

        [Tooltip("Base clearance used for spawn overlap checks (world units). Final radius = max(this, enemy.spawnRadiusWorld).")]
        [Min(0f)] public float spawnCheckRadiusWorld = 0.75f;

        [Tooltip("Physics layers that block spawns (players, enemies, powerups, obstacles, etc).\nIMPORTANT: Do NOT include the arena floor / grid collider here or nothing will ever spawn.")]
        public LayerMask spawnBlockMask;

        [Tooltip("Extra min distance from players even if colliders are smaller than spawnCheckRadius.")]
        [Min(0f)] public float minDistanceFromPlayers = 1.25f;

        [Tooltip("If assigned, enemies are parented here (nice for hierarchy organization).")]
        public Transform enemyRoot;

        [Header("Anomaly Integration")]
        [Tooltip("If left empty, we'll auto-find an AnomalyManager.")]
        public AnomalyManager anomalyManager;

        [Header("Debug")]
        public bool drawSpawnAttempts = false;
        public int spawnAttemptsPerTick = 30;

        // --- runtime state ---
        private readonly List<EnemyBase> _alive = new();
        private readonly Dictionary<EnemyDefinition, int> _aliveByDef = new();

        private int _aliveInert;
        private int _aliveRanged;
        private int _aliveMelee;

        private float _matchStartTime;
        private float _nextSpawnTime;

        private bool _cachedAnomalyRunning;
        private float _pausedSpawnRemaining = -1f;

        private void Start()
        {
            _matchStartTime = Time.time;

            if (arenaBounds == null)
                arenaBounds = FindFirstObjectByType<ArenaBoundsFromVectorGrid>(FindObjectsInactive.Include);

            if (anomalyManager == null)
                anomalyManager = FindFirstObjectByType<AnomalyManager>(FindObjectsInactive.Include);

            if (enemyRoot == null)
                enemyRoot = transform;

            ScheduleNextSpawn(initial: true);
        }

        private void Update()
        {
            CleanupDeadRefs();

            if (spawnProfile == null || !spawnProfile.enabled) return;

            // Pause/unpause behavior during anomalies
            bool anomalyRunning = (anomalyManager != null && anomalyManager.IsAnomalyRunning);
            if (anomalyRunning != _cachedAnomalyRunning)
            {
                OnAnomalyStateChanged(anomalyRunning);
                _cachedAnomalyRunning = anomalyRunning;
            }

            if (_cachedAnomalyRunning && !spawnProfile.allowSpawningDuringAnomalies)
                return;

            if (Time.time < _nextSpawnTime) return;

            TrySpawnOne();
            ScheduleNextSpawn(initial: false);
        }

        private void OnDisable()
        {
            // Stop all movement/behavior when disabled (match end, scene transitions)
            PauseAll(true);
        }

        private void OnEnable()
        {
            // If we come back, only unpause if anomalies aren't running
            bool anomalyRunning = (anomalyManager != null && anomalyManager.IsAnomalyRunning);
            PauseAll(anomalyRunning && spawnProfile != null && spawnProfile.freezeExistingEnemiesDuringAnomalies);
        }

        private void OnAnomalyStateChanged(bool running)
        {
            if (spawnProfile == null) return;

            if (!spawnProfile.allowSpawningDuringAnomalies)
            {
                // Pause spawn timer (so we don't instantly spawn the moment the anomaly ends)
                if (running)
                {
                    _pausedSpawnRemaining = Mathf.Max(0f, _nextSpawnTime - Time.time);
                }
                else
                {
                    if (_pausedSpawnRemaining >= 0f)
                        _nextSpawnTime = Time.time + _pausedSpawnRemaining;

                    _pausedSpawnRemaining = -1f;
                }
            }

            if (spawnProfile.freezeExistingEnemiesDuringAnomalies)
                PauseAll(running);
        }

        private void ScheduleNextSpawn(bool initial)
        {
            if (spawnProfile == null) return;

            float delay = initial ? spawnProfile.initialDelaySeconds : spawnProfile.GetRandomSpawnDelay();
            _nextSpawnTime = Time.time + Mathf.Max(0f, delay);
        }

        private void CleanupDeadRefs()
        {
            // Remove nulls (destroyed), keep counts correct
            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                if (_alive[i] != null) continue;
                _alive.RemoveAt(i);
            }

            // NOTE: We keep counts/dicts updated via NotifyEnemyDestroyed.
        }

        public void NotifyEnemyDestroyed(EnemyBase enemy)
        {
            if (enemy == null) return;

            // Remove from list (if present)
            _alive.Remove(enemy);

            if (enemy.Definition != null)
            {
                if (_aliveByDef.TryGetValue(enemy.Definition, out int count))
                {
                    count = Mathf.Max(0, count - 1);
                    if (count == 0) _aliveByDef.Remove(enemy.Definition);
                    else _aliveByDef[enemy.Definition] = count;
                }
            }

            // Category counts
            if (enemy.Definition != null)
            {
                switch (enemy.Definition.category)
                {
                    case EnemyCategory.Inert: _aliveInert = Mathf.Max(0, _aliveInert - 1); break;
                    case EnemyCategory.Ranged: _aliveRanged = Mathf.Max(0, _aliveRanged - 1); break;
                    case EnemyCategory.Melee: _aliveMelee = Mathf.Max(0, _aliveMelee - 1); break;
                }
            }
        }

        private void RegisterEnemy(EnemyBase enemy)
        {
            if (enemy == null) return;
            if (!_alive.Contains(enemy)) _alive.Add(enemy);

            if (enemy.Definition != null)
            {
                _aliveByDef.TryGetValue(enemy.Definition, out int count);
                _aliveByDef[enemy.Definition] = count + 1;

                switch (enemy.Definition.category)
                {
                    case EnemyCategory.Inert: _aliveInert++; break;
                    case EnemyCategory.Ranged: _aliveRanged++; break;
                    case EnemyCategory.Melee: _aliveMelee++; break;
                }
            }
        }

        private void PauseAll(bool paused)
        {
            for (int i = 0; i < _alive.Count; i++)
            {
                var e = _alive[i];
                if (e != null) e.Pause(paused);
            }
        }

        private void TrySpawnOne()
        {
            if (spawnProfile == null) return;
            if (arenaBounds == null)
            {
                Debug.LogWarning("[EnemyDirector] No ArenaBoundsFromVectorGrid assigned/found; cannot spawn.", this);
                return;
            }

            // Global caps
            int totalAlive = _alive.Count;
            if (spawnProfile.maxAliveTotal > 0 && totalAlive >= spawnProfile.maxAliveTotal) return;

            // Pick an eligible enemy based on weights
            EnemyDefinition def = PickEligibleEnemy();
            if (def == null || def.prefab == null) return;

            // Category caps
            if (!CategoryCapAllows(def.category)) return;

            // Try find a valid spawn point
            float clearance = Mathf.Max(spawnCheckRadiusWorld, def.spawnRadiusWorld);

            for (int attempt = 0; attempt < Mathf.Max(1, spawnAttemptsPerTick); attempt++)
            {
                if (!TrySamplePointInArena(clearance, out Vector3 p)) continue;
                if (!IsSpawnPointClear(p, clearance)) continue;
                if (!IsFarEnoughFromPlayers(p)) continue;

                // Spawn
                Quaternion rot = def.prefab.transform.rotation;
                var go = Instantiate(def.prefab, p, rot, enemyRoot);

                // If it has a Rigidbody, make sure physics agrees with spawn position
                if (go.TryGetComponent<Rigidbody>(out var rb))
                {
                    rb.position = p;
                    rb.rotation = rot;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                var enemy = go.GetComponent<EnemyBase>();
                if (enemy == null) enemy = go.AddComponent<EnemyBase>();

                enemy.Init(def, this);
                RegisterEnemy(enemy);

                // Optional: immediately pause if anomaly is running and we freeze existing enemies
                if (_cachedAnomalyRunning && spawnProfile.freezeExistingEnemiesDuringAnomalies)
                    enemy.Pause(true);

                return;
            }
        }

        private EnemyDefinition PickEligibleEnemy()
        {
            if (spawnProfile == null || spawnProfile.rules == null || spawnProfile.rules.Count == 0)
                return null;

            float t = Time.time - _matchStartTime;

            // Build a weighted selection among eligible rules
            float total = 0f;
            for (int i = 0; i < spawnProfile.rules.Count; i++)
            {
                var r = spawnProfile.rules[i];
                if (!r.enabled) continue;
                if (r.enemy == null || r.enemy.prefab == null) continue;
                if (t < r.minSecondsSinceMatchStart) continue;

                // Per-enemy cap
                int cap = r.GetMaxAlive();
                if (cap > 0)
                {
                    _aliveByDef.TryGetValue(r.enemy, out int alive);
                    if (alive >= cap) continue;
                }

                // Category caps (soft-filter so we don't pick something we can't spawn)
                if (!CategoryCapAllows(r.enemy.category)) continue;

                float w = r.GetWeight();
                if (w <= 0f) continue;
                total += w;
            }

            if (total <= 0.0001f) return null;

            float pick = Random.value * total;
            float acc = 0f;

            for (int i = 0; i < spawnProfile.rules.Count; i++)
            {
                var r = spawnProfile.rules[i];
                if (!r.enabled) continue;
                if (r.enemy == null || r.enemy.prefab == null) continue;
                if (t < r.minSecondsSinceMatchStart) continue;

                int cap = r.GetMaxAlive();
                if (cap > 0)
                {
                    _aliveByDef.TryGetValue(r.enemy, out int alive);
                    if (alive >= cap) continue;
                }

                if (!CategoryCapAllows(r.enemy.category)) continue;

                float w = r.GetWeight();
                if (w <= 0f) continue;

                acc += w;
                if (pick <= acc)
                    return r.enemy;
            }

            return null;
        }

        private bool CategoryCapAllows(EnemyCategory cat)
        {
            if (spawnProfile == null) return true;

            switch (cat)
            {
                case EnemyCategory.Inert:
                    return (spawnProfile.maxAliveInert <= 0) || (_aliveInert < spawnProfile.maxAliveInert);
                case EnemyCategory.Ranged:
                    return (spawnProfile.maxAliveRanged <= 0) || (_aliveRanged < spawnProfile.maxAliveRanged);
                case EnemyCategory.Melee:
                    return (spawnProfile.maxAliveMelee <= 0) || (_aliveMelee < spawnProfile.maxAliveMelee);
                default:
                    return true;
            }
        }

        private bool TrySamplePointInArena(float extraPaddingWorld, out Vector3 worldPos)
        {
            worldPos = default;

            if (arenaBounds == null) return false;

            Vector2 half = arenaBounds.GetHalfSizeLocalInset();
            if (half.x <= 0.0001f || half.y <= 0.0001f) return false;

            // Convert padding from world -> local per axis
            Vector3 ls = arenaBounds.transform.lossyScale;
            float sx = Mathf.Max(1e-6f, Mathf.Abs(ls.x));
            float sy = Mathf.Max(1e-6f, Mathf.Abs(ls.y));

            float padLX = (borderBufferWorld + extraPaddingWorld) / sx;
            float padLY = (borderBufferWorld + extraPaddingWorld) / sy;

            float xMin = -half.x + padLX;
            float xMax = half.x - padLX;
            float yMin = -half.y + padLY;
            float yMax = half.y - padLY;

            if (xMin >= xMax || yMin >= yMax) return false;

            float x = Random.Range(xMin, xMax);
            float y = Random.Range(yMin, yMax);

            Vector3 local = new Vector3(x, y, 0f);
            Vector3 ws = arenaBounds.transform.TransformPoint(local);
            ws.y = arenaBounds.transform.position.y;

            worldPos = ws;

            if (drawSpawnAttempts)
                Debug.DrawLine(ws + Vector3.up * 0.1f, ws + Vector3.up * 1.0f, Color.white, 0.35f);

            return true;
        }

        private bool IsSpawnPointClear(Vector3 p, float radius)
        {
            if (radius <= 0.0001f) return true;

            // Use Collide so triggers (power-up triggers, melee hitboxes) still count as occupied.
            if (Physics.CheckSphere(p, radius, spawnBlockMask, QueryTriggerInteraction.Collide))
                return false;

            return true;
        }

        private bool IsFarEnoughFromPlayers(Vector3 p)
        {
            if (minDistanceFromPlayers <= 0.0001f) return true;

            float min2 = minDistanceFromPlayers * minDistanceFromPlayers;

            // Quick + simple: tag scan (OK at spawn frequency scale)
            var players = GameObject.FindGameObjectsWithTag("Player");
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null) continue;
                if ((players[i].transform.position - p).sqrMagnitude < min2)
                    return false;
            }

            return true;
        }
    }
}
