using UnityEngine;

namespace Massive.Enemies
{
    /// <summary>
    /// Minimal "can be hit by player sword" component.
    /// Attach to a trigger collider on an enemy prefab (or on the root).
    ///
    /// - Detects PlayerMelee hitboxes (same pattern used by PowerUpPickup).
    /// - Applies Definition.damageTakenPerSwordHit to the EnemyBase.
    ///
    /// NOTE: This does NOT yet implement special parry rules (Phase Sickle visibility, Dyson stun, etc).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class EnemyHurtbox : MonoBehaviour
    {
        [SerializeField] private EnemyBase enemy;

        [Tooltip("Optional override. If <= 0, uses EnemyDefinition.damageTakenPerSwordHit.")]
        [SerializeField] private float damageTakenPerSwordHitOverride = -1f;

        private void Reset()
        {
            var c = GetComponent<Collider>();
            if (c != null) c.isTrigger = true;
        }

        private void Awake()
        {
            if (enemy == null) enemy = GetComponentInParent<EnemyBase>();

            var c = GetComponent<Collider>();
            if (c != null) c.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (enemy == null || enemy.IsDead) return;
            if (enemy.IsPaused) return;

            // Require a real PlayerMelee hitbox
            var melee = other.GetComponent<PlayerMelee>();
            if (!melee) melee = other.GetComponentInParent<PlayerMelee>();
            if (!melee) return;

            float dmg = damageTakenPerSwordHitOverride;
            if (dmg <= 0f && enemy.Definition != null)
                dmg = enemy.Definition.damageTakenPerSwordHit;

            if (dmg <= 0f) return;

            enemy.TakeDamage(dmg, EnemyDamageSource.Sword, melee.Owner);
        }
    }
}
