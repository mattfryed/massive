using System;
using UnityEngine;

namespace Massive.Enemies
{
    [Serializable]
    public sealed class EnemyBatchSpawnRule
    {
        public bool enabled = true;
        public EnemyDefinition enemy;
        [Min(0f)] public float firstBatchDelay = 2f;
        [Min(1)] public int initialBatchSize = 3;
        [Min(1)] public int finalBatchSize = 7;
        [Min(.05f)] public float intervalWithinBatch = .22f;
        [Tooltip("Delay from completion of one batch to the next; interpolates over Ramp Seconds.")]
        [Min(.05f)] public float initialBatchDelay = 10f;
        [Min(.05f)] public float finalBatchDelay = 3f;
        [Tooltip("Active gameplay seconds, independent of display tier and testing match duration.")]
        [Min(1f)] public float rampSeconds = 120f;
        [Min(0)] public int maxAlive = 24;
        [Min(.1f)] public float batchRadius = 1.8f;
        [Tooltip("Abandon an obstructed/capped batch after this long; never release a backlog all at once.")]
        [Min(.1f)] public float blockedBatchTimeout = 2f;

        [Header("Spawn warning")]
        [Tooltip("Optional outline warning at the fixed batch center. No prefab preserves immediate spawning.")]
        public EnemySpawnTelegraph telegraphPrefab;
        [Tooltip("Active gameplay seconds between the warning appearing and the first unit. Pauses with spawning.")]
        [Min(0f)] public float telegraphSeconds = 3f;
        public float WarningSeconds => telegraphPrefab != null ? Mathf.Max(0f, telegraphSeconds) : 0f;

        public int SizeAt(float seconds) => Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(initialBatchSize,
            finalBatchSize, Mathf.Clamp01(seconds / Mathf.Max(1f, rampSeconds)))));
        public float DelayAt(float seconds) => Mathf.Max(.05f, Mathf.Lerp(initialBatchDelay,
            finalBatchDelay, Mathf.Clamp01(seconds / Mathf.Max(1f, rampSeconds))));
        public int EffectiveCap
        {
            get
            {
                int a = Mathf.Max(0, maxAlive), b = enemy != null ? Mathf.Max(0, enemy.maxAliveOverride) : 0;
                return a == 0 ? b : b == 0 ? a : Mathf.Min(a, b);
            }
        }
    }
}
