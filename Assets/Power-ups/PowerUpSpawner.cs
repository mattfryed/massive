using System.Collections.Generic;
using UnityEngine;

namespace Massive.PowerUps
{
    [DisallowMultipleComponent]
    public class PowerUpSpawner : MonoBehaviour
    {
        // Preserve legacy scene data for compatibility; the global profile takes precedence.
        [HideInInspector] public float minSpawnDelay = 4f;
        [HideInInspector] public float maxSpawnDelay = 9f;
        [HideInInspector] public int maxActivePickups = 2;
        [HideInInspector] public List<PowerUpDefinition> availablePowerUps = new();
        public VectorGridGPU vectorGrid;
        [HideInInspector] public float borderMarginWorld = .75f;
        [HideInInspector] public float spawnCheckRadius = .6f;
        [HideInInspector] public LayerMask noSpawnMask;
        [HideInInspector] public float minDistanceFromPlayers = 1.75f;

        private static readonly List<PowerUpSpawner> spawners = new();
        private float _nextSpawnTime, scheduledMin, scheduledMax;
        private bool spawningWasEnabled;
        private ArenaBoundsFromVectorGrid bounds;
        public float NextSpawnTime => _nextSpawnTime;
        public bool IsPrimary
        {
            get
            {
                foreach (var other in spawners)
                    if (other && other != this && other.isActiveAndEnabled && other.gameObject.scene == gameObject.scene &&
                        other.GetInstanceID() < GetInstanceID()) return false;
                return true;
            }
        }
        public int ActivePickupCount
        {
            get
            {
                int count = 0;
                foreach (var pickup in PowerUpPickup.ActivePickups)
                    if (pickup && pickup.gameObject.scene == gameObject.scene && pickup.CountsTowardSpawnLimit) count++;
                return count;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => spawners.Clear();
        private void OnEnable()
        {
            if (!spawners.Contains(this)) spawners.Add(this);
            if (!vectorGrid)
                foreach (var root in gameObject.scene.GetRootGameObjects())
                { vectorGrid = root.GetComponentInChildren<VectorGridGPU>(true); if (vectorGrid) break; }
            bounds = vectorGrid ? vectorGrid.GetComponent<ArenaBoundsFromVectorGrid>() : null;
            ScheduleNext();
        }
        private void OnDisable() => spawners.Remove(this);
        private void Update()
        {
            var profile = PowerUpSettings.Current;
            var s = profile ? profile.spawning : null;
            bool spawningEnabled = s == null || s.enabled;
            float min = s != null ? s.minDelay : minSpawnDelay;
            float max = s != null ? s.maxDelay : maxSpawnDelay;
            if (scheduledMin != min || scheduledMax != max || spawningWasEnabled != spawningEnabled)
                ScheduleNext();
            spawningWasEnabled = spawningEnabled;
            if (!spawningEnabled || !IsPrimary || !vectorGrid) return;
            if (ActivePickupCount >= (s != null ? s.maxConcurrent : maxActivePickups) || Time.time < _nextSpawnTime) return;
            TrySpawnOne();
            ScheduleNext();
        }
        public void ScheduleNext()
        {
            var settings = PowerUpSettings.Current;
            scheduledMin = Mathf.Max(.05f, settings ? settings.spawning.minDelay : minSpawnDelay);
            scheduledMax = Mathf.Max(scheduledMin, settings ? settings.spawning.maxDelay : maxSpawnDelay);
            _nextSpawnTime = Time.time + Random.Range(scheduledMin, scheduledMax);
        }
        public PowerUpPickup TrySpawnOne()
        {
            var profile = PowerUpSettings.Current;
            var s = profile ? profile.spawning : null;
            if (!vectorGrid || !isActiveAndEnabled || !IsPrimary || (s != null && !s.enabled) ||
                ActivePickupCount >= (s != null ? s.maxConcurrent : maxActivePickups)) return null;
            var def = profile ? profile.Select(Random.value) : PickLegacy();
            if (!def || !def.pickupPrefab) return null;
            for (int i = 0; i < (s != null ? s.placementAttempts : 30); i++)
            {
                if (!TrySamplePointOnGrid(out Vector3 p, s) || !IsSpawnPointValid(p, s)) continue;
                return PowerUpPickup.Spawn(def, p, def.pickupPrefab.transform.rotation, transform);
            }
            return null;
        }
        private bool TrySamplePointOnGrid(out Vector3 worldPos, PowerUpSpawnSettings s)
        {
            Vector2 half = vectorGrid.size * .5f;
            float margin = s != null ? s.borderMargin : borderMarginWorld;
            worldPos = default;
            if (half.x <= margin || half.y <= margin) return false;
            Vector3 local = new(Random.Range(-half.x + margin, half.x - margin),
                Random.Range(-half.y + margin, half.y - margin), 0);
            worldPos = vectorGrid.transform.TransformPoint(local);
            worldPos.y = vectorGrid.transform.position.y;
            return true;
        }
        private bool IsSpawnPointValid(Vector3 p, PowerUpSpawnSettings s)
        {
            float margin = s != null ? s.borderMargin : borderMarginWorld;
            float radius = s != null ? s.checkRadius : spawnCheckRadius;
            float playerDistance = s != null ? s.minPlayerDistance : minDistanceFromPlayers;
            if (bounds && bounds.ovalOutline && !bounds.ContainsWorldPoint(p, Mathf.Max(margin, radius))) return false;
            if (Physics.CheckSphere(p, radius, s != null ? s.noSpawnMask : noSpawnMask, QueryTriggerInteraction.Collide)) return false;
            foreach (var player in GameObject.FindGameObjectsWithTag("Player"))
                if (player.scene == gameObject.scene && (player.transform.position-p).sqrMagnitude < playerDistance*playerDistance) return false;
            float pickupDistance = s != null ? s.minPickupDistance : radius * 2;
            foreach (var pickup in PowerUpPickup.ActivePickups)
                if (pickup && pickup.CountsTowardSpawnLimit && pickup.gameObject.scene == gameObject.scene &&
                    (pickup.transform.position-p).sqrMagnitude < pickupDistance*pickupDistance) return false;
            return true;
        }
        private PowerUpDefinition PickLegacy()
        {
            float total = 0;
            foreach (var d in availablePowerUps) if (d && d.pickupPrefab) total += Mathf.Max(0,d.spawnWeight);
            if (total <= 0) return null;
            float sample = Random.value * total;
            foreach (var d in availablePowerUps)
            {
                if (!d || !d.pickupPrefab || d.spawnWeight <= 0) continue;
                sample -= d.spawnWeight; if (sample <= 0) return d;
            }
            return null;
        }
    }
}
