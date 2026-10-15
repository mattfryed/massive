using System.Collections.Generic;
using Massive.Enemies;
using Massive.Player;
using Massive.Multiplier;
using UnityEngine;

namespace Massive.PowerUps
{
    internal partial class DecoherenceAbility
    {
        private bool fieldArmed, activationReady = true;
        private bool equipped;
        private float charge = 1f;
        private float activationAge, activationRadius;
        private int stroke;
        private DecoherenceFieldVisual fieldVisual;
        private ParticleAcceleratorVFXModule meter;
        private DecoherencePaint paint = new();
        private Dictionary<EnemyBase, int> disrupted = new();
        private Dictionary<EnemyBase, EnemyHurtbox[]> hurtboxes = new();
        private readonly List<EnemyBase> staleEnemies = new();
        private readonly RaycastHit[] fieldObstructions = new RaycastHit[64];

        public float Charge01 => charge;
        private bool CanUse => equipped && _host && _host.HasActive && _defender && _defender.isActiveAndEnabled &&
            !_defender.temporarilyEliminated && !_defender.IsSpawning && !_defender.IsMatchInputLocked &&
            !_defender.IsInsideAmplifier && !_defender.IsSingularityTransitControlled &&
            !_defender.IsStunned && !_defender.IsExternallyStunned;
        public bool CanActivate => CanUse && activationReady && charge + .000001f >= Mathf.Clamp(_def.minimumActivationCharge, .01f, 1f);
        public bool FieldActive => fieldArmed && CanUse && _shield && _shield.IsActive;
        private float FieldRadius
        {
            get
            {
                float target = Mathf.Max(.1f, _def.fieldRadius) * PlayerScaleAdjuster.SizeOf(_defender) * charge;
                float t = _def.activationEaseSeconds > 0f ? Mathf.Clamp01(activationAge / _def.activationEaseSeconds) : 1f;
                // Fast ease-out, following the live charge target as it drains.
                return Mathf.Lerp(activationRadius, target, 1f - (1f - t) * (1f - t) * (1f - t));
            }
        }
        public void ObserveShieldInput(bool down, bool held)
        { if (!down && !held) activationReady = true; }

        private void InitializeField()
        {
            charge = 1f; equipped = true;
            foreach (var existing in _defender.GetComponentsInChildren<DecoherenceFieldVisual>())
                if (existing.enabled && existing.Driver != null && existing.Driver.fieldVisual == existing)
                { fieldVisual = existing; break; }
            if (fieldVisual && fieldVisual.Driver != null)
            {
                // A fresh pickup resumes the owner's lingering paint instead of
                // creating a second overlapping source of damage and grid echoes.
                var previous = fieldVisual.Driver;
                paint = previous.paint; stroke = previous.stroke;
                disrupted = previous.disrupted; hurtboxes = previous.hurtboxes;
                previous.fieldVisual = null;
            }
            else
            {
                var go = new GameObject("Decoherence painted field");
                go.transform.SetParent(_defender.transform, false);
                fieldVisual = go.AddComponent<DecoherenceFieldVisual>();
            }
            paint.RecedeSeconds = _def.paintRecedeSeconds;
            fieldVisual.Bind(_defender, this, _def, paint);
            var hub = _defender.GetComponent<PlayerVFXHub>();
            if (!hub) hub = _defender.gameObject.AddComponent<PlayerVFXHub>();
            meter = hub.GetOrCreate<ParticleAcceleratorVFXModule>(_def.energyRingPrefab);
            meter.gameObject.SetActive(true); meter.Bind(_defender);
            meter.SetEnergyMeter(charge, followDecoherenceSplit: true); meter.SetVisible(true);
        }
        private void BeginField()
        {
            fieldArmed = true; activationReady = false; stroke++;
            activationAge = 0f; activationRadius = PlayerScaleAdjuster.BodyRadiusOf(_defender);
            hurtboxes.Clear();
        }
        private void EndField() { fieldArmed = false; }
        private void ReleaseField()
        {
            equipped = false; EndField();
            if (meter) meter.SetVisible(false);
            meter = null; // A replacement power-up may immediately reuse the ring.
            if (paint.Deposits.Count == 0 || !_defender || !_defender.isActiveAndEnabled || _defender.temporarilyEliminated)
                DisposeField();
        }
        internal void DisposeField()
        {
            EndField(); paint.Clear(); hurtboxes.Clear(); disrupted.Clear();
            if (meter) meter.SetVisible(false);
            var visual = fieldVisual;
            fieldVisual = null;
            if (visual) { visual.enabled = false; Object.Destroy(visual.gameObject); }
        }

        internal void AdvanceField(float dt)
        {
            if (dt <= 0f) return;
            if (!_defender || !_defender.isActiveAndEnabled || _defender.temporarilyEliminated)
            {
                if (_shield) _shield.ForceStopShield();
                if (!equipped) DisposeField(); else paint.Clear();
                activationReady = false; return;
            }
            if (_defender.IsMatchInputLocked)
            { paint.Clear(); if (_shield) _shield.ForceStopShield(); if (!equipped) DisposeField(); return; }
            paint.Advance(dt);
            if (fieldArmed && !CanUse && _shield) _shield.ForceStopShield();
            float idleTime = dt;
            if (FieldActive)
            {
                float duration = Mathf.Max(.05f, _def.fullMeterPaintSeconds);
                float funded = Mathf.Min(dt, charge * duration);
                // Deposit only during a funded held frame. A press already released before
                // simulation advances cannot leave a free full-radius footprint.
                if (funded > 0f && (paint.Deposits.Count == 0 || paint.Deposits[paint.Deposits.Count - 1].stroke != stroke))
                    paint.Paint(_defender.transform.position, FieldRadius, _def.paintLifetimeSeconds, stroke, false);
                charge = Mathf.Max(0, charge - funded / duration);
                activationAge += funded;
                bool connect = paint.Deposits.Count == 0 || CanReach(paint.Deposits[paint.Deposits.Count - 1].center,
                    _defender.transform.position, _defender.transform);
                paint.Paint(_defender.transform.position, FieldRadius, _def.paintLifetimeSeconds, stroke, connect);
                idleTime = Mathf.Max(0, dt - funded);
                if (charge <= .000001f) { charge = 0; _shield.ForceStopShield(); activationReady = false; }
            }
            if (equipped && idleTime > 0) charge = Mathf.Min(1f, charge + idleTime / Mathf.Max(.05f, _def.emptyToFullRefillSeconds));
            if (meter) meter.SetEnergyMeter(charge, followDecoherenceSplit: true);
            if (paint.Deposits.Count == 0)
            { hurtboxes.Clear(); disrupted.Clear(); if (!equipped) DisposeField(); return; }
            TickPaintDamage(dt);
        }

        private void TickPaintDamage(float dt)
        {
            var enemies = EnemyBase.ActiveEnemies;
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                var enemy = enemies[i];
                if (!enemy || enemy.IsDead || enemy.IsPaused || !enemy.Definition ||
                    enemy.gameObject.scene != _defender.gameObject.scene || !enemy.SharesSimulationWith(_defender) ||
                    enemy.OwnerTeamId == _defender.teamID) continue;
                if (!hurtboxes.TryGetValue(enemy, out var bodies))
                {
                    bodies = enemy.GetComponentsInChildren<EnemyHurtbox>(true);
                    // Formation may create a hurtbox later. Do not cache an empty result forever.
                    if (bodies.Length > 0) hurtboxes.Add(enemy, bodies);
                }
                float swordDamage = 0f; int contactStroke = -1;
                foreach (var body in bodies)
                {
                    if (!body || !body.isActiveAndEnabled || body.Enemy != enemy) continue;
                    var collider = body.GetComponent<Collider>();
                    if (!collider || !collider.enabled) continue;
                    for (int j = paint.Deposits.Count - 1; j >= 0; j--)
                    {
                        var deposit = paint.Deposits[j];
                        Vector3 point = collider.ClosestPoint(deposit.center);
                        if ((point - deposit.center).sqrMagnitude > deposit.radius * deposit.radius ||
                            !CanReach(deposit.center, point, enemy.transform)) continue;
                        swordDamage = Mathf.Max(swordDamage, body.SwordHitDamage);
                        contactStroke = Mathf.Max(contactStroke, deposit.stroke); break;
                    }
                }
                if (swordDamage <= 0f) continue;
                if (!disrupted.TryGetValue(enemy, out int previousStroke) || contactStroke > previousStroke)
                {
                    disrupted[enemy] = contactStroke;
                    enemy.TryDisrupt(_def.disruptionSeconds, _def.disruptionRecoverySeconds);
                }
                var effect = enemy.GetComponent<EnemyDecoherenceVisual>();
                if (!effect) effect = enemy.gameObject.AddComponent<EnemyDecoherenceVisual>();
                effect.Expose(_def.enemySplitDistance, _def.fieldOpacity, dt);
                // All deposits from this ability form one field, including overlapping strokes.
                enemy.TakeDamage(swordDamage * dt / Mathf.Max(.05f, _def.secondsPerSwordHit), EnemyDamageSource.Decoherence, _defender, EnemyDamageDelivery.Sustained);
            }
            staleEnemies.Clear();
            foreach (var entry in hurtboxes) if (!entry.Key || entry.Key.IsDead) staleEnemies.Add(entry.Key);
            foreach (var enemy in staleEnemies) { hurtboxes.Remove(enemy); disrupted.Remove(enemy); }

            if (_def.playerDamagePerSecond <= 0f) return;
            var players = PlayerControllerScript.ActivePlayers;
            for (int i = players.Count - 1; i >= 0; i--)
            {
                var player = players[i];
                if (!player || player == _defender || player.teamID == _defender.teamID || player.IsSpawning || player.IsInvulnerable ||
                    player.temporarilyEliminated || player.IsMatchInputLocked || player.IsInsideAmplifier || player.IsSingularityTransitControlled ||
                    player.gameObject.scene != _defender.gameObject.scene || !_defender.SharesSimulationWith(player)) continue;
                bool inside = false;
                for (int j = paint.Deposits.Count - 1; j >= 0; j--)
                {
                    var deposit = paint.Deposits[j];
                    float reach = deposit.radius + PlayerScaleAdjuster.BodyRadiusOf(player);
                    if ((player.transform.position - deposit.center).sqrMagnitude <= reach * reach &&
                        CanReach(deposit.center, player.transform.position, player.transform)) { inside = true; break; }
                }
                if (!inside) continue;
                float damage = _def.playerDamagePerSecond * dt;
                if (player.shieldOn)
                {
                    var shield = player.GetComponent<PlayerShieldAbility>();
                    damage *= shield && shield.IsActive ? (shield.BlocksAllDamage ? 0f : 1f - shield.CurrentStrength01) : 0f;
                }
                if (damage > 0f) player.ApplyExternalMassDelta(-damage, _defender.gameObject, allowDeath: true);
            }
        }

        private bool CanReach(Vector3 origin, Vector3 target, Transform targetRoot)
        {
            origin.y += .2f; target.y += .2f;
            Vector3 delta = target - origin;
            if (delta.sqrMagnitude < .00001f) return true;
            int count = Physics.RaycastNonAlloc(origin, delta.normalized, fieldObstructions, delta.magnitude,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == fieldObstructions.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var c = fieldObstructions[i].collider;
                if (!c || c.transform.IsChildOf(_defender.transform) || c.transform.IsChildOf(targetRoot)) continue;
                if (c.GetComponentInParent<EnemyBase>() || c.GetComponentInParent<PlayerControllerScript>() ||
                    c.GetComponentInParent<MatterNuggetScript>() || c.GetComponentInParent<PowerUpPickup>() ||
                    c.GetComponentInParent<AmplifierCoreGameplay>() || c.GetComponent<VectorGridGPU>()) continue;
                return false;
            }
            return true;
        }
    }
}
