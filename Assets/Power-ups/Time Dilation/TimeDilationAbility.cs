using UnityEngine;

namespace Massive.PowerUps
{
    internal class TimeDilationAbility : IPowerUpAbility
    {
        private readonly TimeDilationPowerUpDefinition _def;
        private readonly PlayerControllerScript _owner;
        private readonly PlayerPowerUpController _host;

        public TimeDilationAbility(TimeDilationPowerUpDefinition def, PlayerControllerScript owner, PlayerPowerUpController host)
        {
            _def = def;
            _owner = owner;
            _host = host;
        }

        public void OnEquip()
        {
            _host.SetMovementMultiplier(_def.moveSpeedMultiplier);
        }

        public void Tick(float dt) { }

        public void PreTickInput(in PowerUpInputState input)
        {
            _host.SetMovementMultiplierWhileCharging(1f);
        }

        public bool HandleInput(in PowerUpInputState input, out float cooldownToApply)
        {
            cooldownToApply = 0f;
            return false; // does not consume attack
        }

        public bool ConsumesAttackWhileOnCooldown(in PowerUpInputState input) => false;

        public bool TryHandleShieldImpact(PlayerControllerScript attacker, Collider shieldCollider, Vector3 attackDirWS) => false;

        public void OnUnequip()
        {
            _host.SetMovementMultiplier(1f);
        }
    }
}