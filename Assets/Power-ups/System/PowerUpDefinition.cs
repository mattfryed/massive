using UnityEngine;

namespace Massive.PowerUps
{
    public abstract class PowerUpDefinition : ScriptableObject
    {
        [Header("Identity")]
        public abstract PowerUpType Type { get; }
        public string displayName = "PowerUp";

        
        [Header("UI")]
        [TextArea(1, 3)]
        public string description = "";

        [Tooltip("Weighted random selection. 1 = normal.")]
        public float spawnWeight = 1f;

        [Header("World")]
        [Tooltip("Prefab that exists in the arena (your metaball object). Must include PowerUpPickup + trigger collider.")]
        public GameObject pickupPrefab;

        [Tooltip("Despawn if nobody collects it.")]
        public float worldLifetimeSeconds = 12f;

        [Header("Player Effect")]
        [Tooltip("How long the power-up remains equipped/usable once collected.")]
        public float effectDurationSeconds = 8f;
    }
}