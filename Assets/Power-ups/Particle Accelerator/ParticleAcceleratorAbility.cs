using UnityEngine;

namespace Massive.PowerUps
{
    internal class ParticleAcceleratorAbility : IPowerUpAbility
    {
        private readonly ParticleAcceleratorPowerUpDefinition _def;
        private readonly PlayerControllerScript _owner;
        private readonly PlayerPowerUpController _host;

        private bool _charging;
        private float _chargeTime;

        public ParticleAcceleratorAbility(ParticleAcceleratorPowerUpDefinition def, PlayerControllerScript owner, PlayerPowerUpController host)
        {
            _def = def;
            _owner = owner;
            _host = host;
        }

        public void OnEquip() { }

        public void Tick(float dt)
        {
            if (_charging) _chargeTime += dt;
        }

        public void PreTickInput(in PowerUpInputState input)
        {
            _host.SetMovementMultiplierWhileCharging(_charging ? _def.selfSlowWhileCharging : 1f);
        }

        public bool ConsumesAttackWhileOnCooldown(in PowerUpInputState input)
        {
            // While accelerator is active, we generally do NOT want melee to fire on Sword
            // even if we're cooling down.
            return input.attackDown || input.attackHeld || input.attackUp;
        }

        public bool HandleInput(in PowerUpInputState input, out float cooldownToApply)
        {
            cooldownToApply = 0f;

            // Start charge
            if (input.attackDown && !_charging)
            {
                _charging = true;
                _chargeTime = 0f;
                return true; // consume attack
            }

            // Hold charge
            if (_charging && input.attackHeld)
            {
                return true; // consume attack
            }

            // Release -> fire
            if (_charging && input.attackUp)
            {
                float charge01 = Mathf.Clamp01(_chargeTime / Mathf.Max(0.01f, _def.chargeToMaxSeconds));
                Fire(input.aimDirWS, charge01);

                // cooldown based on charge time
                cooldownToApply = _def.baseRechargeDelay + (_chargeTime * _def.rechargeMultiplier);

                _charging = false;
                _chargeTime = 0f;
                return true;
            }

            return false;
        }

        private void Fire(Vector3 aimDirWS, float charge01)
        {
            Vector3 dir = aimDirWS;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = _owner.transform.forward;
            dir.Normalize();

            float dist = Mathf.Lerp(_def.minDistanceOnTap, _def.maxDistance, charge01);
            float thickness = Mathf.Lerp(_def.beamThicknessMin, _def.beamThicknessMax, charge01);

            float massRemove = Mathf.Lerp(_def.massRemovedMin, _def.massRemovedMax, charge01);

            // Spawn projectile
            GameObject go = null;

            if (_def.beamProjectilePrefab != null)
                go = Object.Instantiate(_def.beamProjectilePrefab);
            else
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);

            go.name = "PA_Beam";
            go.transform.position = _owner.transform.position + dir * 0.6f + Vector3.up * 0.05f;
            go.transform.localScale = Vector3.one * thickness;

            var col = go.GetComponent<Collider>();
            if (col) col.isTrigger = true;

            var rb = go.GetComponent<Rigidbody>();
            if (!rb) rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var proj = go.GetComponent<ParticleAcceleratorProjectile>();
            if (!proj) proj = go.AddComponent<ParticleAcceleratorProjectile>();

            proj.Init(
                shooter: _owner,
                dirWS: dir,
                speed: _def.beamSpeed,
                maxDistance: dist,
                radius: thickness * 0.5f,
                massRemove: massRemove,
                transferToShooter: _def.transferMassToShooter
            );
        }

        public bool TryHandleShieldImpact(PlayerControllerScript attacker, Collider shieldCollider, Vector3 attackDirWS) => false;

        public void OnUnequip()
        {
            _charging = false;
            _chargeTime = 0f;
            _host.SetMovementMultiplierWhileCharging(1f);
        }
    }
}