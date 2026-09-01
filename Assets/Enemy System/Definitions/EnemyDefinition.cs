using UnityEngine;

namespace Massive.Enemies
{
    [CreateAssetMenu(menuName = "MASSIVE/Enemies/Enemy Definition", fileName = "ED_Enemy")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Optional stable ID. Useful if you want to reference this enemy type from data tables.")]
        public string id = "";

        public EnemyCategory category = EnemyCategory.Melee;

        [Tooltip("Prefab spawned by EnemyDirector.")]
        public GameObject prefab;

        [Header("Spawn")]
        [Min(0f)]
        [Tooltip("Weight used by EnemyDirector when randomly selecting what to spawn.")]
        public float spawnWeight = 1f;

        [Min(0f)]
        [Tooltip("Used by the spawner as a minimum clearance radius so we don't spawn overlapping things.")]
        public float spawnRadiusWorld = 0.8f;

        [Min(0f)]
        [Tooltip("Optional per-enemy cap. 0 = no per-enemy cap (Director caps still apply).")]
        public int maxAliveOverride = 0;

        [Header("Stats")]
        [Min(0f)]
        [Tooltip("How much 'health' the enemy has in mass-equivalent units.")]
        public float healthMassEq = 1f;

        [Min(0f)]
        [Tooltip("How much damage the enemy takes from a single Sword hit (mass-equivalent).")]
        public float damageTakenPerSwordHit = 1f;

        [Min(0f)]
        [Tooltip("Mass removed from a player on a successful enemy hit (massScore space: 0..1).")]
        public float damageToPlayerMass01 = 0.08f;

        [Min(0f)]
        [Tooltip("Cooldown between applying damage to the same player via touch/contact. Prevents multi-hit spam.")]
        public float contactDamageCooldownSeconds = 0.35f;

        [Min(0f)]
        [Tooltip("Lifetime in seconds. 0 or negative = infinite.")]
        public float lifetimeSeconds = 0f;

        [Header("Motion")]
        [Min(0f)]
        public float moveSpeed = 4f;

        [Min(0f)]
        public float turnSpeed = 360f;

        [Header("Audio (Optional)")]
        [Tooltip("Optional: played when this enemy attacks.")]
        public AudioEventId sfxAttack;

        [Tooltip("Optional: played when this enemy takes damage.")]
        public AudioEventId sfxHit;

        [Tooltip("Optional: played when this enemy dies.")]
        public AudioEventId sfxDeath;

        [Header("Debug")]
        public bool drawGizmos = false;

        private void OnValidate()
        {
            spawnWeight = Mathf.Max(0f, spawnWeight);
            spawnRadiusWorld = Mathf.Max(0f, spawnRadiusWorld);
            healthMassEq = Mathf.Max(0f, healthMassEq);
            damageTakenPerSwordHit = Mathf.Max(0f, damageTakenPerSwordHit);
            damageToPlayerMass01 = Mathf.Max(0f, damageToPlayerMass01);
            contactDamageCooldownSeconds = Mathf.Max(0f, contactDamageCooldownSeconds);
            moveSpeed = Mathf.Max(0f, moveSpeed);
            turnSpeed = Mathf.Max(0f, turnSpeed);
        }
    }
}
