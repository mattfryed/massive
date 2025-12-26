using System;
using UnityEngine;

namespace Massive.PowerUps
{
    [DisallowMultipleComponent]
    public class PlayerPowerUpController : MonoBehaviour
    {
        [Header("Runtime")]
        [SerializeField] private PowerUpDefinition active;
        [SerializeField] private float remaining;
        [SerializeField] private float cooldownRemaining;

        // Outputs used by PlayerControllerScript
        public float MovementMultiplier { get; private set; } = 1f;
        public float MovementMultiplierWhileCharging { get; private set; } = 1f;

        public bool HasActive => active != null && remaining > 0f;
        public PowerUpDefinition ActiveDefinition => active;

        private PlayerControllerScript _player; // your existing controller
        private IPowerUpAbility _ability;

        public event Action<PowerUpDefinition> OnEquipped;
        public event Action OnExpired;

        private void Awake()
        {
            _player = GetComponent<PlayerControllerScript>();
        }

        private void Update()
        {
            if (!HasActive) return;

            remaining -= Time.deltaTime;
            if (cooldownRemaining > 0f) cooldownRemaining -= Time.deltaTime;

            _ability?.Tick(Time.deltaTime);

            if (remaining <= 0f)
            {
                UnequipInternal();
            }
        }

        public void Equip(PowerUpDefinition def)
        {
            if (def == null) return;

            // overwrite behavior: replace existing
            UnequipInternal();

            active = def;
            remaining = Mathf.Max(0.01f, def.effectDurationSeconds);
            cooldownRemaining = 0f;

            _ability = PowerUpAbilityFactory.Create(def, _player, this);

            MovementMultiplier = 1f;
            MovementMultiplierWhileCharging = 1f;

            _ability?.OnEquip();

            OnEquipped?.Invoke(def);
        }

        public void ForceExpire()
        {
            if (!HasActive) return;
            remaining = 0f;
        }

        private void UnequipInternal()
        {
            if (_ability != null)
            {
                _ability.OnUnequip();
                _ability = null;
            }

            active = null;
            remaining = 0f;
            cooldownRemaining = 0f;

            MovementMultiplier = 1f;
            MovementMultiplierWhileCharging = 1f;

            OnExpired?.Invoke();
        }

        /// Call once per frame from PlayerControllerScript with current input.
        /// Returns true if the power-up consumed the attack input (so melee shouldn't trigger).
        public bool HandleInput(in PowerUpInputState input)
        {
            if (!HasActive || _ability == null) return false;

            // ability can set movement modifiers (e.g., slow while charging)
            _ability.PreTickInput(input);

            // gate cooldown for abilities that use it
            if (cooldownRemaining > 0f)
                return _ability.ConsumesAttackWhileOnCooldown(input);

            bool consumed = _ability.HandleInput(input, out float cooldownToApply);
            if (cooldownToApply > 0f)
                cooldownRemaining = cooldownToApply;

            return consumed;
        }

        // --- Used by PlayerMelee shield-hit interception (Decoherence) ---
        public bool TryHandleShieldImpact(PlayerControllerScript attacker, Collider shieldCollider, Vector3 attackDirWS)
        {
            if (!HasActive || _ability == null) return false;
            return _ability.TryHandleShieldImpact(attacker, shieldCollider, attackDirWS);
        }

        // Ability helpers (called from abilities)
        public void SetMovementMultiplier(float m) => MovementMultiplier = Mathf.Max(0.01f, m);
        public void SetMovementMultiplierWhileCharging(float m) => MovementMultiplierWhileCharging = Mathf.Max(0.01f, m);
    }

    internal interface IPowerUpAbility
    {
        void OnEquip();
        void Tick(float dt);

        void PreTickInput(in PowerUpInputState input);

        // Return true if it consumes attack (i.e., PlayerControllerScript should NOT call BeginAttack)
        bool HandleInput(in PowerUpInputState input, out float cooldownToApply);

        // During cooldown, should we still consume attack input? (usually yes for accelerator to prevent melee)
        bool ConsumesAttackWhileOnCooldown(in PowerUpInputState input);

        // For Decoherence
        bool TryHandleShieldImpact(PlayerControllerScript attacker, Collider shieldCollider, Vector3 attackDirWS);

        void OnUnequip();
    }

    internal static class PowerUpAbilityFactory
    {
public static IPowerUpAbility Create(PowerUpDefinition def, PlayerControllerScript owner, PlayerPowerUpController host)
{
    if (def is TimeDilationPowerUpDefinition td)
        return new TimeDilationAbility(td, owner, host);

    if (def is ParticleAcceleratorPowerUpDefinition pa)
        return new ParticleAcceleratorAbility(pa, owner, host);

    if (def is DecoherencePowerUpDefinition dc)
        return new DecoherenceAbility(dc, owner, host);

    Debug.LogError($"PowerUpDefinition has unknown concrete type: {def.GetType().Name}", def);
    return null;
}
    }
}