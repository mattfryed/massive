using System.Collections.Generic;
using UnityEngine;
using Massive.Player;

namespace Massive.PowerUps
{
    internal class DecoherenceAbility : IPowerUpAbility
    {

        private readonly DecoherencePowerUpDefinition _def;
        private readonly PlayerControllerScript _defender;
        private readonly PlayerPowerUpController _host;

        private PlayerShieldAbility _shield;
        private PlayerVisualController _visuals;

        // Optional: hide nuggets renderers while split is active
        private readonly List<Renderer> _nuggetRenderers = new();

        public DecoherenceAbility(DecoherencePowerUpDefinition def, PlayerControllerScript defender, PlayerPowerUpController host)
        {
            _def = def;
            _defender = defender;
            _host = host;
        }

        public void OnEquip()
        {
            if (_defender == null) return;

            _shield = _defender.GetComponent<PlayerShieldAbility>();
            _visuals = _defender.visualsController;

            // Cache nugget renderers (optional)
            _nuggetRenderers.Clear();
            var nuggets = _defender.GetComponentInChildren<PlayerNuggetsGPU>(true);
            if (nuggets != null)
            {
                _nuggetRenderers.AddRange(nuggets.GetComponentsInChildren<Renderer>(true));
            }

            if (_shield != null)
            {
                // suppress normal shield VFX while power-up is equipped
                _shield.SuppressDefaultShieldVfx = true;

                _shield.ShieldStarted += OnShieldStarted;
                _shield.ShieldEnded += OnShieldEnded;
            }
        }

        public void OnUnequip()
        {
            if (_shield != null)
            {
                _shield.ShieldStarted -= OnShieldStarted;
                _shield.ShieldEnded -= OnShieldEnded;

                _shield.SuppressDefaultShieldVfx = false;
            }

            // Ensure visuals are restored
            if (_visuals != null)
                _visuals.SetDecoherenceSplitActive(false);

            SetNuggetsVisible(true);

            _shield = null;
            _visuals = null;
        }

        private void OnShieldStarted(PlayerShieldAbility s)
        {
            if (_visuals != null)
                _visuals.SetDecoherenceSplitActive(true /*, optional separation value */);

            SetNuggetsVisible(false);
        }

        private void OnShieldEnded(PlayerShieldAbility s)
        {
            if (_visuals != null)
                _visuals.SetDecoherenceSplitActive(false);

            SetNuggetsVisible(true);
        }

        private void SetNuggetsVisible(bool visible)
        {
            for (int i = 0; i < _nuggetRenderers.Count; i++)
            {
                if (_nuggetRenderers[i] != null)
                    _nuggetRenderers[i].enabled = visible;
            }
        }

        // You can leave the input methods as no-ops for Design A
        public void Tick(float dt) { }
        public void PreTickInput(in PowerUpInputState input) { }
        public bool HandleInput(in PowerUpInputState input, out float cooldownToApply) { cooldownToApply = 0f; return false; }
        public bool ConsumesAttackWhileOnCooldown(in PowerUpInputState input) => false;
        public bool TryHandleShieldImpact(PlayerControllerScript attacker, Collider shieldCollider, Vector3 attackDirWS)
        {
            if (_defender == null || attacker == null) return false;

            // Armed window: defender shield must be active
            if (!_defender.shieldOn) return false;

            // IMPORTANT: because this is only called when sword already hit the shield collider,
            // a root-distance gate can cause false negatives. If you want "always super shield",
            // remove distance gating entirely.
            // float d = Vector3.Distance(attacker.transform.position, _defender.transform.position);
            // if (d > _def.distanceWindow) return false;

            attacker.ExternalStun(_def.attackerStunSeconds);

            // Move attacker through the defender
            Vector3 dir = attackDirWS;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f)
                dir = (_defender.transform.position - attacker.transform.position);

            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f)
                dir = attacker.transform.forward;

            dir.Normalize();

            Vector3 targetPos = _defender.transform.position + dir * _def.passThroughDistance;

            var rb = attacker.GetComponent<Rigidbody>();
            if (rb) rb.MovePosition(targetPos);
            else attacker.transform.position = targetPos;

            _host.StartIgnoreCollisionsTemporarily(attacker, _defender, _def.ignoreCollisionSeconds);

            return true; // prevents normal shield behavior in PlayerMelee
        }


    }
}