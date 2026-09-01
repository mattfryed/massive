using System;
using System.Collections.Generic;
using UnityEngine;

namespace Massive.Enemies
{
    [Serializable]
    public struct EnemySpawnRule
    {
        [Tooltip("If false, this rule is ignored.")]
        public bool enabled;

        [Tooltip("Enemy type to spawn.")]
        public EnemyDefinition enemy;

        [Tooltip("If >= 0, overrides enemy.spawnWeight.")]
        public float weightOverride;

        [Min(0f)]
        [Tooltip("Won't spawn until this many seconds since match start.")]
        public float minSecondsSinceMatchStart;

        [Min(0)]
        [Tooltip("Cap for this enemy type from this profile. 0 = no cap. If enemy.maxAliveOverride > 0, the smaller one wins.")]
        public int maxAlive;

        public float GetWeight()
        {
            if (enemy == null) return 0f;
            if (weightOverride >= 0f) return Mathf.Max(0f, weightOverride);
            return Mathf.Max(0f, enemy.spawnWeight);
        }

        public int GetMaxAlive()
        {
            int a = Mathf.Max(0, maxAlive);
            int b = enemy != null ? Mathf.Max(0, enemy.maxAliveOverride) : 0;

            if (a == 0) return b;
            if (b == 0) return a;
            return Mathf.Min(a, b);
        }
    }

    [CreateAssetMenu(menuName = "MASSIVE/Enemies/Enemy Spawn Profile", fileName = "ESP_Stage")]
    public sealed class EnemySpawnProfile : ScriptableObject
    {
        [Header("Global")]
        public bool enabled = true;

        [Min(0f)]
        [Tooltip("Wait this long after match start before the first spawn.")]
        public float initialDelaySeconds = 2f;

        [Min(0f)]
        public float minSpawnDelaySeconds = 4f;

        [Min(0f)]
        public float maxSpawnDelaySeconds = 8f;

        [Header("Caps")]
        [Min(0)] public int maxAliveTotal = 6;
        [Min(0)] public int maxAliveInert = 2;
        [Min(0)] public int maxAliveRanged = 2;
        [Min(0)] public int maxAliveMelee = 2;

        [Header("Anomaly Behavior")]
        [Tooltip("If false, EnemyDirector pauses its spawn timer while anomalies are running.")]
        public bool allowSpawningDuringAnomalies = false;

        [Tooltip("If true, EnemyDirector pauses existing enemies while anomalies are running.")]
        public bool freezeExistingEnemiesDuringAnomalies = true;

        [Header("Rules")]
        public List<EnemySpawnRule> rules = new();

        private void OnValidate()
        {
            initialDelaySeconds = Mathf.Max(0f, initialDelaySeconds);
            minSpawnDelaySeconds = Mathf.Max(0f, minSpawnDelaySeconds);
            maxSpawnDelaySeconds = Mathf.Max(minSpawnDelaySeconds, maxSpawnDelaySeconds);

            maxAliveTotal = Mathf.Max(0, maxAliveTotal);
            maxAliveInert = Mathf.Max(0, maxAliveInert);
            maxAliveRanged = Mathf.Max(0, maxAliveRanged);
            maxAliveMelee = Mathf.Max(0, maxAliveMelee);
        }

        public float GetRandomSpawnDelay()
        {
            return UnityEngine.Random.Range(minSpawnDelaySeconds, maxSpawnDelaySeconds);
        }
    }
}
