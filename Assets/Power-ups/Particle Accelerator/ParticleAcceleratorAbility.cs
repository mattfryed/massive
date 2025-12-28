using UnityEngine;

namespace Massive.PowerUps
{
    internal class ParticleAcceleratorAbility : IPowerUpAbility
    {
        private readonly ParticleAcceleratorPowerUpDefinition _def;
        private readonly PlayerControllerScript _owner;
        private readonly PlayerPowerUpController _host;

        private PlayerVFXHub _vfxHub;
        private ParticleAcceleratorVFXModule _vfx;

        private PlayerVisualController _visuals;

        private bool _charging;
        private float _chargeTime;

        private bool _pendingFire;
        private float _pendingCharge01;
        private Vector3 _pendingDirWS;
        private float _telegraphRemaining;

        // local cooldown tracking so we can drive refill visuals
        private float _cooldownRemaining;
        private float _cooldownTotal;
        private int _spentDots;

        public ParticleAcceleratorAbility(ParticleAcceleratorPowerUpDefinition def, PlayerControllerScript owner, PlayerPowerUpController host)
        {
            _def = def;
            _owner = owner;
            _host = host;
        }

        public void OnEquip()
        {
            _vfxHub = _owner.GetComponent<PlayerVFXHub>();
            if (!_vfxHub) _vfxHub = _owner.gameObject.AddComponent<PlayerVFXHub>();

            _vfx = _vfxHub.GetOrCreate<ParticleAcceleratorVFXModule>(_def.vfxModulePrefab);
            _vfx.Bind(_owner);
            _vfx.SetVisible(true);

            _visuals = _owner.visualsController != null
                ? _owner.visualsController
                : _owner.GetComponentInChildren<PlayerVisualController>(true);
        }

        public void Tick(float dt)
        {
            if (_charging) _chargeTime += dt;

            if (_telegraphRemaining > 0f)
            {
                _telegraphRemaining -= dt;
                if (_telegraphRemaining <= 0f && _pendingFire)
                {
                    _pendingFire = false;
                    Fire(_pendingDirWS, _pendingCharge01);
                }
            }

            if (_cooldownRemaining > 0f)
            {
                _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - dt);
                if (_cooldownRemaining <= 0f)
                {
                    _spentDots = 0;
                    _cooldownTotal = 0f;
                }
            }
        }

public void PreTickInput(in PowerUpInputState input)
{
    float charge01 = _charging ? Mathf.Clamp01(_chargeTime / Mathf.Max(0.01f, _def.chargeToMaxSeconds)) : 0f;

    float moveMul = _charging ? Mathf.Lerp(1f, _def.selfSlowWhileCharging, charge01) : 1f;
    _host.SetMovementMultiplierWhileCharging(moveMul);

    Vector3 dir = input.aimDirWS;
    dir.y = 0f;

    if (dir.sqrMagnitude < 0.0001f && _visuals != null && _visuals.visuals != null)
        dir = _visuals.visuals.right;

    if (dir.sqrMagnitude < 0.0001f)
        dir = _owner.transform.forward;

    dir.y = 0f;
    dir.Normalize();

    float cd01 = (_cooldownRemaining > 0f && _cooldownTotal > 0.0001f)
        ? Mathf.Clamp01(_cooldownRemaining / _cooldownTotal)
        : 0f;

    // Use current charge while charging; during telegraph preview pending shot
    float preview01 = _charging ? charge01 : 0f;
    if (_telegraphRemaining > 0f && _pendingFire)
        preview01 = _pendingCharge01;

    float thickness = Mathf.Lerp(_def.beamThicknessMin, _def.beamThicknessMax, preview01);
    float maxDist   = Mathf.Lerp(_def.minDistanceOnTap, _def.maxDistance, preview01);

    Vector3 origin = (_vfx != null) ? _vfx.GetVfxOriginWS() : _owner.transform.position;
    float blocked = ComputeBlockedDistance(origin, dir, maxDist, thickness * 0.5f, _def.blockMask);

    _vfx?.SetState(
        charging: _charging,
        charge01: charge01,
        cooldown01: cd01,
        spentDots: _spentDots,
        aimDirWS: dir,
        blockedDistance: blocked
    );

    if (_visuals != null)
    {
        _visuals.SetExternalChargeJitter01(_charging ? charge01 : 0f);
        _visuals.SetExternalTurnDamp01(_charging ? charge01 : 0f);
    }
}


        public bool ConsumesAttackWhileOnCooldown(in PowerUpInputState input)
        {
            // While equipped, we always consume Sword input so melee doesn't fire.
            return input.attackDown || input.attackHeld || input.attackUp;
        }

        public bool HandleInput(in PowerUpInputState input, out float cooldownToApply)
        {
            cooldownToApply = 0f;

            // If we’re in telegraph window, just consume input
            if (_telegraphRemaining > 0f)
                return input.attackDown || input.attackHeld || input.attackUp;

            // Start charge
            if (input.attackDown && !_charging)
            {
                _charging = true;
                _chargeTime = 0f;
                return true;
            }

            // Hold charge
            if (_charging && input.attackHeld)
            {
                // Auto-fire at max charge
                if (_chargeTime >= _def.chargeToMaxSeconds)
                {
                    TriggerShot(1f, input.aimDirWS);

                    cooldownToApply = _def.baseRechargeDelay + (_chargeTime * _def.rechargeMultiplier);
                    BeginCooldown(cooldownToApply);

                    return true;
                }

                return true;
            }

            // Release -> fire
            if (_charging && input.attackUp)
            {
                float charge01 = Mathf.Clamp01(_chargeTime / Mathf.Max(0.01f, _def.chargeToMaxSeconds));
                TriggerShot(charge01, input.aimDirWS);

                cooldownToApply = _def.baseRechargeDelay + (_chargeTime * _def.rechargeMultiplier);
                BeginCooldown(cooldownToApply);

                return true;
            }

            return false;
        }

        private void BeginCooldown(float cooldownSeconds)
        {
            _cooldownTotal = Mathf.Max(0.01f, cooldownSeconds);
            _cooldownRemaining = _cooldownTotal;
        }

        private void TriggerShot(float charge01, Vector3 aimDirWS)
        {
            _charging = false;

            Vector3 dir = aimDirWS;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = _owner.transform.forward;
            dir.Normalize();

            _pendingFire = true;
            _pendingCharge01 = Mathf.Clamp01(charge01);
            _pendingDirWS = dir;

            _telegraphRemaining = Mathf.Max(0.01f, _def.preFireTelegraphSeconds);

            // Spend dots proportional to charge
            _spentDots = _vfx != null ? _vfx.ComputeSpentDots(_pendingCharge01) : 1;

            // Burst telegraph so others get a dodge window
            _vfx?.TriggerTelegraphBurst(_pendingCharge01);

            // reset charge timer
            _chargeTime = 0f;
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

            var prb = _owner.GetComponent<Rigidbody>();
            if (prb != null)
            {
                float recoil = Mathf.Lerp(_def.recoilVelocityMin, _def.recoilVelocityMax, charge01);

                // Kick opposite shot direction (planar)
                Vector3 kick = -dir * recoil;
                kick.y = 0f;

                // VelocityChange is great here (consistent regardless of mass)
                prb.AddForce(kick, ForceMode.VelocityChange);
            }


            // Spawn projectile
            GameObject go;

            if (_def.beamProjectilePrefab != null)
                go = Object.Instantiate(_def.beamProjectilePrefab);
            else
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);

            go.name = "PA_Beam";
            Vector3 origin = (_vfx != null) ? _vfx.GetVfxOriginWS() : _owner.transform.position;
            go.transform.position = origin + dir * 0.6f; // + optional y lift if you want

            go.transform.localScale = Vector3.one; // IMPORTANT: don't scale the hierarchy
            go.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);



            var col = go.GetComponent<Collider>();
            if (col) col.isTrigger = true;

            var rb = go.GetComponent<Rigidbody>();
            if (!rb) rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var proj = go.GetComponent<ParticleAcceleratorProjectile>();
            if (!proj) proj = go.AddComponent<ParticleAcceleratorProjectile>();

            float capLen = Mathf.Lerp(_def.beamVisualMinLength, _def.beamVisualMaxLength, charge01);

            // Optional: never exceed the actual travel distance for this shot
            capLen = Mathf.Min(capLen, dist);

            proj.Init(
                shooter: _owner,
                dirWS: dir,
                speed: _def.beamSpeed,
                maxDistance: dist,
                radius: thickness * 0.5f,
                massRemove: massRemove,
                transferToShooter: _def.transferMassToShooter,
                blockMask: _def.blockMask,
                charge01: charge01,
                beamVisualMaxLength: capLen
                // impactPrefabOverride: (optional) you can pass one here if you add it to the definition

            );
        }

        private static float ComputeBlockedDistance(Vector3 origin, Vector3 dir, float maxDist, float radius, LayerMask blockMask)
        {
            if (blockMask.value == 0)
                return maxDist; // treat as "no blockers configured"

            if (Physics.SphereCast(origin, radius, dir, out var hit, maxDist, blockMask, QueryTriggerInteraction.Collide))
                return hit.distance;

            return maxDist;
        }

        public bool TryHandleShieldImpact(PlayerControllerScript attacker, Collider shieldCollider, Vector3 attackDirWS) => false;

        public void OnUnequip()
        {
            _charging = false;
            _chargeTime = 0f;

            _pendingFire = false;
            _telegraphRemaining = 0f;

            _cooldownRemaining = 0f;
            _cooldownTotal = 0f;
            _spentDots = 0;

            _host.SetMovementMultiplierWhileCharging(1f);

            if (_visuals != null)
                _visuals.SetExternalChargeJitter01(0f);

            if (_vfx != null)
                _vfx.SetVisible(false);
        }
    }
}
