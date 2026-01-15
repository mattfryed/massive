using System.Collections.Generic;
using UnityEngine;

namespace Massive.Enemies
{
    /// <summary>
    /// Generic "contact hurts" component.
    ///
    /// For Step A this gives you a simple way to make Melee enemies dangerous
    /// before their bespoke attack patterns are implemented.
    ///
    /// - Applies EnemyDefinition.damageToPlayerMass01 to any Player collider we touch.
    /// - Uses EnemyDefinition.contactDamageCooldownSeconds to avoid multi-hit spam.
    ///
    /// You can disable damage to shielded players (default) so the existing Shield system
    /// naturally counters some enemies.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyTouchDamage : MonoBehaviour
    {
        [SerializeField] private EnemyBase enemy;

        [Tooltip("If true, will not damage players currently shielding.")]
        [SerializeField] private bool ignoreShieldedPlayers = true;

        // Next allowed hit time per player
        private readonly Dictionary<PlayerControllerScript, float> _nextHitTime = new();

        private void Awake()
        {
            if (enemy == null) enemy = GetComponentInParent<EnemyBase>();
        }

        private void OnTriggerStay(Collider other) => TryDamage(other);
        private void OnCollisionStay(Collision collision) => TryDamage(collision.collider);

        private void TryDamage(Collider other)
        {
            if (enemy == null || enemy.IsDead || enemy.IsPaused) return;
            if (enemy.Definition == null) return;

            if (!other.CompareTag("Player")) return;

            var player = other.GetComponentInParent<PlayerControllerScript>();
            if (player == null) return;
            if (player.temporarilyEliminated) return;
            if (player.IsInvulnerable) return;

            if (ignoreShieldedPlayers && player.shieldOn)
                return;

            float now = Time.time;
            if (_nextHitTime.TryGetValue(player, out float next) && now < next)
                return;

            float dmg = Mathf.Max(0f, enemy.Definition.damageToPlayerMass01);
            if (dmg <= 0f) return;

            // Apply enemy damage as a raw mass delta (massScore space)
            player.ApplyExternalMassDelta(-dmg, allowDeath: true);

            _nextHitTime[player] = now + Mathf.Max(0f, enemy.Definition.contactDamageCooldownSeconds);

            // Optional hit SFX
            enemy.PlayAttackSfx();
        }
    }
}
