using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.Enemies
{
    [DisallowMultipleComponent, RequireComponent(typeof(EnemyBase))]
    public sealed class AmplifiedDysonLoot : MonoBehaviour
    {
        [Serializable] public sealed class Drop
        {
            public EnemyLootPickup prefab;
            [Min(0f)] public float weight = 1f;
        }
        [Header("On surviving damage")]
        public EnemyLootPickup amplifierNugglet;
        [Range(0f, 100f)] public float hitDropChancePercent = 25f;
        [Min(0f), Tooltip("Minimum active seconds between chance rolls, including unsuccessful rolls. Bounds sustained-beam emissions.")]
        public float hitRollInterval = .35f;
        [Min(0), Tooltip("Maximum successful hit drops per enemy life. Zero disables hit drops.")]
        public int maxHitDrops = 8;
        [Header("On defeat")]
        public Vector2Int deathDropCount = new Vector2Int(6, 10);
        public Drop[] deathDrops = Array.Empty<Drop>();
        [Header("Ejection")]
        public Vector2 ejectionSpeed = new Vector2(2f, 4f);
        [Min(.1f)] public float pickupLifetime = 12f;
        [Min(0f)] public float spawnOffset = .3f;
        public int HitDropsReleased { get; private set; }
        public int DeathDropsReleased { get; private set; }
        private EnemyBase enemy;
        private System.Random random;
        private float clock, nextRoll;
        private string life;
        private bool deathReleased;

        private void Awake() => enemy = GetComponent<EnemyBase>();
        private void OnEnable() { enemy.Damaged += OnDamage; enemy.Defeated += OnDefeat; life = null; }
        private void OnDisable() { enemy.Damaged -= OnDamage; enemy.Defeated -= OnDefeat; }
        private void Update() { if (!enemy.IsPaused && !enemy.IsDead) clock += Time.deltaTime; }
        private void EnsureLife()
        {
            if (life == enemy.SourceLifeToken && random != null) return;
            life = enemy.SourceLifeToken; random = new System.Random((life ?? Guid.NewGuid().ToString()).GetHashCode());
            clock = nextRoll = 0f; HitDropsReleased = DeathDropsReleased = 0; deathReleased = false;
        }
        private void OnDamage(EnemyDamageContext damage)
        {
            EnsureLife();
            if (damage.healthRemaining <= 0f || !amplifierNugglet || HitDropsReleased >= Mathf.Max(0, maxHitDrops) || clock < nextRoll) return;
            nextRoll = clock + Mathf.Max(0f, hitRollInterval);
            if (random.NextDouble() * 100d >= Mathf.Clamp(hitDropChancePercent, 0f, 100f)) return;
            Release(amplifierNugglet); HitDropsReleased++;
        }
        private void OnDefeat(EnemyDefeatContext defeat)
        {
            EnsureLife(); if (deathReleased) return; deathReleased = true;
            int low = Mathf.Clamp(deathDropCount.x, 0, 64), high = Mathf.Clamp(deathDropCount.y, low, 64);
            double total = 0;
            foreach (var drop in deathDrops) if (Valid(drop)) total += drop.weight;
            if (total <= 0) return;
            int count = random.Next(low, high + 1);
            for (int i = 0; i < count; i++)
            {
                double sample = random.NextDouble() * total;
                foreach (var drop in deathDrops)
                {
                    if (!Valid(drop)) continue;
                    sample -= drop.weight; if (sample >= 0) continue;
                    Release(drop.prefab); DeathDropsReleased++; break;
                }
            }
        }
        private static bool Valid(Drop drop) => drop != null && drop.prefab && drop.weight > 0 && !float.IsNaN(drop.weight) && !float.IsInfinity(drop.weight);
        private void Release(EnemyLootPickup prefab)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            float low = Mathf.Max(0f, Mathf.Min(ejectionSpeed.x, ejectionSpeed.y));
            float high = Mathf.Max(low, Mathf.Max(ejectionSpeed.x, ejectionSpeed.y));
            float speed = Mathf.Lerp(low, high, (float)random.NextDouble());
            // No parent under the dying enemy: released rewards outlive its shell animation.
            var pickup = Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(pickup.gameObject, gameObject.scene);
            if (enemy.SimulationRoot) pickup.transform.SetParent(enemy.SimulationRoot, true);
            else if (enemy.Director) pickup.transform.SetParent(enemy.Director.enemyRoot ? enemy.Director.enemyRoot : enemy.Director.transform, true);
            pickup.Bind(enemy);
            pickup.Eject(transform.position + direction * spawnOffset, direction * speed, Mathf.Max(.1f, pickupLifetime));
        }
    }
}
