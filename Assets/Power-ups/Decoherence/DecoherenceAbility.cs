using System.Collections;
using UnityEngine;

namespace Massive.PowerUps
{
    internal class DecoherenceAbility : IPowerUpAbility
    {
        private readonly DecoherencePowerUpDefinition _def;
        private readonly PlayerControllerScript _defender;
        private readonly PlayerPowerUpController _host;

        public DecoherenceAbility(DecoherencePowerUpDefinition def, PlayerControllerScript defender, PlayerPowerUpController host)
        {
            _def = def;
            _defender = defender;
            _host = host;
        }

        public void OnEquip() { }

        public void Tick(float dt) { }

        public void PreTickInput(in PowerUpInputState input)
        {
            _host.SetMovementMultiplierWhileCharging(1f);
        }

        public bool HandleInput(in PowerUpInputState input, out float cooldownToApply)
        {
            cooldownToApply = 0f;
            return false; // does not consume attack input
        }

        public bool ConsumesAttackWhileOnCooldown(in PowerUpInputState input) => false;

        public bool TryHandleShieldImpact(PlayerControllerScript attacker, Collider shieldCollider, Vector3 attackDirWS)
        {
            // Only active if defender is holding shield
            // (this is your “instead of normal parry/block, they decohere while defending”)
            if (_defender == null) return false;
            if (!_defender.shieldOn) return false;

            // distance window
            float d = Vector3.Distance(attacker.transform.position, _defender.transform.position);
            if (d > _def.distanceWindow) return false;

            // Success: attacker stunned (without knockback), defender takes no damage, attacker phases through
            attacker.ExternalStun(_def.attackerStunSeconds);

            // push attacker through defender along attack direction
            var rb = attacker.GetComponent<Rigidbody>();
            Vector3 dir = attackDirWS;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = (attacker.transform.position - _defender.transform.position).normalized;
            if (dir.sqrMagnitude < 0.0001f) dir = attacker.transform.forward;

            Vector3 targetPos = attacker.transform.position + dir.normalized * _def.passThroughDistance;
            if (rb) rb.MovePosition(targetPos);
            else attacker.transform.position = targetPos;

            // temporarily ignore collisions between the two players (prevents immediate “catch”)
            _host.StartIgnoreCollisionsTemporarily(attacker, _defender, _def.ignoreCollisionSeconds);

            return true; // handled: PlayerMelee should NOT do default stun/behavior
        }

        public void OnUnequip() { }
    }

    // helper extensions implemented as small MonoBehaviour proxy on the controller object
    internal static class PowerUpCollisionHelpers
    {
        public static void StartIgnoreCollisionsTemporarily(this PlayerPowerUpController host,
            PlayerControllerScript a, PlayerControllerScript b, float seconds)
        {
            host.StartCoroutine(IgnoreCollisionsCo(a, b, seconds));
        }

        private static IEnumerator IgnoreCollisionsCo(PlayerControllerScript a, PlayerControllerScript b, float seconds)
        {
            if (!a || !b) yield break;

            var ac = a.GetComponentsInChildren<Collider>();
            var bc = b.GetComponentsInChildren<Collider>();

            for (int i = 0; i < ac.Length; i++)
                for (int j = 0; j < bc.Length; j++)
                    if (ac[i] && bc[j]) Physics.IgnoreCollision(ac[i], bc[j], true);

            yield return new WaitForSeconds(seconds);

            for (int i = 0; i < ac.Length; i++)
                for (int j = 0; j < bc.Length; j++)
                    if (ac[i] && bc[j]) Physics.IgnoreCollision(ac[i], bc[j], false);
        }
    }
}