using System.Collections.Generic;
using UnityEngine;

namespace Massive.PowerUps
{
    [DisallowMultipleComponent]
    public class PowerUpSpawner : MonoBehaviour
    {
        [Header("Timing")]
        public float minSpawnDelay = 4f;
        public float maxSpawnDelay = 9f;
        public int maxActivePickups = 2;

        [Header("Selection")]
        public List<PowerUpDefinition> availablePowerUps = new();

        [Header("Arena / Placement")]
        public VectorGridGPU vectorGrid;          // your playfield
        public float borderMarginWorld = 0.75f;   // "no spawn border"
        public float spawnCheckRadius = 0.6f;
        public LayerMask noSpawnMask;             // include NoSpawnZone + obstacles if desired
        public float minDistanceFromPlayers = 1.75f;

        private readonly List<GameObject> _active = new();
        private float _nextSpawnTime;

        private void Start()
        {
            ScheduleNext();
        }

        private void Update()
        {
            _active.RemoveAll(x => x == null);

            if (_active.Count >= maxActivePickups) return;
            if (Time.time < _nextSpawnTime) return;

            TrySpawnOne();
            ScheduleNext();
        }

        private void ScheduleNext()
        {
            _nextSpawnTime = Time.time + Random.Range(minSpawnDelay, maxSpawnDelay);
        }

        private void TrySpawnOne()
        {
            if (vectorGrid == null || availablePowerUps.Count == 0) return;

            var def = PickWeighted(availablePowerUps);
            if (!def || !def.pickupPrefab) return;

            const int attempts = 30;
            for (int i = 0; i < attempts; i++)
            {
                if (!TrySamplePointOnGrid(out Vector3 p)) continue;
                if (!IsSpawnPointValid(p)) continue;

                var go = Instantiate(def.pickupPrefab);
                go.transform.position = p;

                var pickup = go.GetComponent<PowerUpPickup>();
                if (!pickup) pickup = go.AddComponent<PowerUpPickup>();
                pickup.definition = def;

                _active.Add(go);
                return;
            }
        }

        private bool TrySamplePointOnGrid(out Vector3 worldPos)
        {
            // sample in VectorGrid local XY rectangle, then convert to world
            Vector2 half = vectorGrid.size * 0.5f;

            float x = Random.Range(-half.x + borderMarginWorld, half.x - borderMarginWorld);
            float y = Random.Range(-half.y + borderMarginWorld, half.y - borderMarginWorld);

            Vector3 local = new Vector3(x, y, 0f);
            Vector3 ws = vectorGrid.transform.TransformPoint(local);

            // Snap to grid plane height (players live on XZ; this assumes your arena is flat)
            ws.y = vectorGrid.transform.position.y;
            worldPos = ws;
            return true;
        }

        private bool IsSpawnPointValid(Vector3 p)
        {
            // 1) reject inside no-spawn colliders
            if (Physics.CheckSphere(p, spawnCheckRadius, noSpawnMask, QueryTriggerInteraction.Collide))
                return false;

            // 2) reject too close to players
            var players = GameObject.FindGameObjectsWithTag("Player");
            for (int i = 0; i < players.Length; i++)
            {
                if ((players[i].transform.position - p).sqrMagnitude < minDistanceFromPlayers * minDistanceFromPlayers)
                    return false;
            }

            return true;
        }

        private static PowerUpDefinition PickWeighted(List<PowerUpDefinition> defs)
        {
            float total = 0f;
            for (int i = 0; i < defs.Count; i++)
                total += Mathf.Max(0f, defs[i] ? defs[i].spawnWeight : 0f);

            if (total <= 0.0001f) return defs[Random.Range(0, defs.Count)];

            float r = Random.value * total;
            float a = 0f;
            for (int i = 0; i < defs.Count; i++)
            {
                var d = defs[i];
                if (!d) continue;
                a += Mathf.Max(0f, d.spawnWeight);
                if (r <= a) return d;
            }
            return defs[defs.Count - 1];
        }
    }
}