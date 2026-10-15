using System.Collections;
using System.Collections.Generic;
using Massive.Enemies;
using Massive.Player;
using Massive.PowerUps;
using TMPro;
using UnityEngine;

namespace Massive.Demonstrations
{
    /// <summary>Input-driven field demonstrations; health, damage and cooldowns belong to gameplay.</summary>
    [DisallowMultipleComponent]
    public sealed class DecoherenceDemoLane : MonoBehaviour
    {
        public enum Example { DroneSweep, CarrierExposure, PlayerDefense }
        public Example example;
        public GameObject playerPrefab;
        public DecoherencePowerUpDefinition powerUp;
        public EnemyDefinition enemyDefinition;
        public VectorGridGPU grid;
        public TMP_Text status, health, explanation;
        [Min(.1f)] public float repeatPause = 1.5f;
        [Min(5f)] public float timeout = 65f;
        public GameObject Session { get; private set; }
        public PlayerControllerScript Primary { get; private set; }
        public PlayerControllerScript Partner { get; private set; }
        public int CompletedLoops { get; private set; }
        public int ConfirmedDefeats { get; private set; }
        public float FieldMovementDistance { get; private set; }
        public float LastExposedLoss { get; private set; }
        public float LastBlockedLoss { get; private set; }
        public string Phase { get; private set; } = "Ready";
        public string Failure { get; private set; }
        public bool ComparingShield { get; private set; }
        public IReadOnlyList<EnemyBase> Enemies => enemies;
        private readonly List<EnemyBase> enemies = new();
        private readonly List<Sample> samples = new();
        private PlayerShieldAbility shield, partnerShield;
        private Vector3 home, partnerHome, previousPosition;
        private float nextLabelUpdate;
        private bool hadField, opponentDied;

        private sealed class Sample
        {
            public float initialHealth, health, swordDamage, exposure;
            public bool defeated, resolvedSwordDamage;
        }

        private void OnEnable() { if (Application.isPlaying) StartCoroutine(Run()); }
        private void OnDisable()
        {
            StopAllCoroutines();
            if (Primary) Primary.ClearScriptedInput();
            if (Partner) Partner.ClearScriptedInput();
            if (Session) { Session.SetActive(false); Destroy(Session); }
            Session = null; Primary = Partner = null;
            enemies.Clear(); samples.Clear(); Phase = "Stopped";
        }

        private IEnumerator Run()
        {
            Failure = null; CompletedLoops = ConfirmedDefeats = 0;
            FieldMovementDistance = LastExposedLoss = LastBlockedLoss = 0f;
            hadField = ComparingShield = false;
            if (!playerPrefab || !powerUp || (example != Example.PlayerDefense && (!enemyDefinition || !enemyDefinition.prefab)))
            { Fail("Missing canonical player, power-up or enemy definition"); yield break; }
            Session = new GameObject("Decoherence demo actors");
            Session.transform.SetParent(transform, false); Session.SetActive(false);
            home = transform.position + Vector3.left * 2.5f;
            partnerHome = transform.position + Vector3.right * .6f;
            Primary = CreatePlayer(0, 1, home);
            shield = Primary.GetComponent<PlayerShieldAbility>();
            if (example == Example.PlayerDefense)
            {
                Partner = CreatePlayer(2, 2, partnerHome);
                partnerShield = Partner.GetComponent<PlayerShieldAbility>();
                Partner.DeathStarted += _ => opponentDied = true;
            }
            Session.SetActive(true);
            previousPosition = Primary.transform.position;
            yield return null; yield return null;
            // Like the other action-gallery fixtures, keep this one pickup equipped for the lesson.
            Primary.powerUps.Equip(powerUp, float.PositiveInfinity);
            while (Failure == null && isActiveAndEnabled)
            {
                if (example == Example.PlayerDefense) yield return PlayerComparison();
                else yield return EnemyPass();
                Neutral();
                if (Failure != null) yield break;
                CompletedLoops++;
                yield return new WaitForSeconds(repeatPause);
                Phase = "Release Shield / return";
                yield return MoveTo(Primary, home);
                if (Partner) yield return MoveTo(Partner, partnerHome);
            }
        }

        private PlayerControllerScript CreatePlayer(int slot, int team, Vector3 position)
        {
            var go = Instantiate(playerPrefab, position, Quaternion.identity, Session.transform);
            go.name = team == 1 ? "Decoherence player" : "Opposing player";
            var actor = go.GetComponent<PlayerControllerScript>();
            actor.ConfigureDemonstration(Session.transform, slot, team);
            foreach (var interactor in go.GetComponentsInChildren<GridInteractor>(true)) interactor.grid = grid;
            foreach (var pulse in go.GetComponentsInChildren<PlayerRepulsorGridPulse>(true)) pulse.BindGrid(grid);
            if (go.TryGetComponent<PlayerScaleAdjuster>(out var scale)) scale.ApplyScale();
            return actor;
        }

        private void SpawnSample(Vector3 offset)
        {
            var staging = new GameObject("Sample staging");
            staging.transform.SetParent(Session.transform, false); staging.SetActive(false);
            var go = Instantiate(enemyDefinition.prefab, transform.position + offset, Quaternion.identity, staging.transform);
            var enemy = go.GetComponent<EnemyBase>();
            enemy.ConfigureDemonstration(Session.transform, Primary); enemy.Init(enemyDefinition, null);
            enemy.HoldPosition = true; enemy.AttacksEnabled = false;
            // Stationary samples retain normal formation, colliders, health, disruption and death.
            var sample = new Sample { initialHealth = enemy.HealthRemaining, health = enemy.HealthRemaining,
                swordDamage = enemyDefinition.damageTakenPerSwordHit };
            foreach (var hurtbox in enemy.GetComponentsInChildren<EnemyHurtbox>(true))
                sample.swordDamage = Mathf.Max(sample.swordDamage, hurtbox.SwordHitDamage);
            enemy.Damaged += hit =>
            {
                // Some canonical enemies create their hurtbox during formation, after staging.
                if (!sample.resolvedSwordDamage)
                {
                    float actualSwordDamage = 0f;
                    foreach (var hurtbox in enemy.GetComponentsInChildren<EnemyHurtbox>(true))
                        actualSwordDamage = Mathf.Max(actualSwordDamage, hurtbox.SwordHitDamage);
                    if (actualSwordDamage > 0f) sample.swordDamage = actualSwordDamage;
                    sample.resolvedSwordDamage = true;
                }
                float lost = Mathf.Max(0f, sample.health - hit.healthRemaining);
                sample.health = hit.healthRemaining;
                if (hit.source == EnemyDamageSource.Decoherence && sample.swordDamage > 0f)
                    sample.exposure += lost / sample.swordDamage * powerUp.secondsPerSwordHit;
            };
            enemy.Defeated += defeat =>
            {
                if (defeat.source != EnemyDamageSource.Decoherence || defeat.creditedPlayer != Primary)
                { Fail("Sample defeated by something other than this player's field"); return; }
                sample.defeated = true; ConfirmedDefeats++;
            };
            enemies.Add(enemy); samples.Add(sample);
            go.transform.SetParent(Session.transform, true); Destroy(staging);
        }

        private IEnumerator EnemyPass()
        {
            foreach (var enemy in enemies) if (enemy) Destroy(enemy.gameObject);
            enemies.Clear(); samples.Clear(); Phase = "Sample enemies forming";
            if (example == Example.DroneSweep)
            {
                SpawnSample(new Vector3(-.15f, 0, -1.1f));
                SpawnSample(new Vector3(.8f, 0, 0));
                SpawnSample(new Vector3(-.15f, 0, 1.1f));
            }
            else SpawnSample(Vector3.zero);
            float deadline = Time.time + timeout;
            // The Carrier's real warning and formation must finish before its hurtbox is enabled.
            yield return new WaitForSeconds(1f);
            var carrier = enemies[0] ? enemies[0].GetComponent<CarrierController>() : null;
            while (carrier && !carrier.IsSpawnReady && Time.time < deadline) yield return null;
            if (carrier && !carrier.IsSpawnReady) { Fail("Carrier did not finish forming"); yield break; }
            float angle = Mathf.PI; bool painting = true;
            while (Failure == null && samples.Exists(s => !s.defeated) && Time.time < deadline)
            {
                float radius = Mathf.Clamp(powerUp.fieldRadius * PlayerScaleAdjuster.SizeOf(Primary) * .8f, 1.2f, 1.75f);
                angle += Time.deltaTime * .6f;
                Vector3 marker = transform.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
                if (painting && Primary.powerUps.DecoherenceCharge01 < .4f) painting = false;
                if (!painting && Primary.powerUps.DecoherenceCharge01 > .95f) painting = true;
                Drive(Primary, painting ? marker : home, transform.position - Primary.transform.position, painting);
                Phase = painting ? "Hold + move: paint with a shrinking brush" : "Release + leave: painted ground keeps damaging";
                yield return null;
            }
            Neutral();
            if (Failure != null) yield break;
            if (samples.Exists(s => !s.defeated)) { Fail("Field could not defeat every sample within the timeout"); yield break; }
            Phase = "Defeated by accumulated field exposure";
        }

        private IEnumerator PlayerComparison()
        {
            Phase = "Approach opposing player";
            ComparingShield = false;
            yield return WaitForReady(); if (Failure != null) yield break;
            // Keep both bodies apart while their real field volumes overlap.
            float reach = powerUp.fieldRadius * PlayerScaleAdjuster.SizeOf(Primary);
            float gap = Mathf.Max(PlayerScaleAdjuster.BodyRadiusOf(Primary) + PlayerScaleAdjuster.BodyRadiusOf(Partner) + .25f, reach * .8f);
            Vector3 marker = partnerHome + Vector3.left * gap;
            yield return MoveTo(Primary, marker); if (Failure != null) yield break;
            float before = Partner.massScore; opponentDied = false;
            Phase = "Opponent exposed: field chips mass";
            yield return ComparisonHold(marker, false);
            LastExposedLoss = opponentDied ? before : Mathf.Max(0f, before - Partner.massScore);
            if (Failure != null) yield break;
            if (LastExposedLoss <= 0f) { Fail("Exposed opponent did not take field damage"); yield break; }
            Phase = "Release: old paint stays while charge refills";
            yield return WaitForReady(); if (Failure != null) yield break;
            ComparingShield = true;
            Phase = "Opponent holds Shield: mass protected";
            yield return ComparisonHold(marker, true);
            if (LastBlockedLoss > .001f) { Fail("Shielded opponent lost mass during the comparison"); yield break; }
            Phase = "Exposure damages / held Shield protects";
        }

        private IEnumerator ComparisonHold(Vector3 marker, bool block)
        {
            // Raise the opponent's shield one frame first so the comparison starts protected.
            if (block) { Drive(Partner, partnerHome, Vector3.left, true); yield return null; }
            float duration = Mathf.Max(.1f, Mathf.Min(shield.MaxHoldSeconds, partnerShield.MaxHoldSeconds) - .45f);
            float before = Partner.massScore;
            float end = Time.time + duration;
            while (Time.time < end && !Primary.temporarilyEliminated && !Partner.temporarilyEliminated)
            {
                Vector3 drift = Vector3.forward * Mathf.Sin((end - Time.time) * 2f) * .3f;
                Drive(Primary, marker + drift, Vector3.right, true);
                Drive(Partner, partnerHome, Vector3.left, block);
                yield return null;
            }
            if (block) LastBlockedLoss = Mathf.Max(0f, before - Partner.massScore);
            Neutral();
            // Release completes the normal minimum tap window; do not cancel an accepted shield.
            while (shield.IsActive || partnerShield.IsActive) yield return null;
        }

        private IEnumerator WaitForReady()
        {
            Neutral(); float deadline = Time.time + timeout;
            while (Time.time < deadline)
            {
                if (!Primary.temporarilyEliminated && !Partner.temporarilyEliminated && !Primary.IsSpawning && !Partner.IsSpawning &&
                    !Partner.IsInvulnerable && !shield.IsActive && !partnerShield.IsActive &&
                    Primary.powerUps.DecoherenceCharge01 >= .99f && partnerShield.ActivationCooldownRemaining <= 0 &&
                    partnerShield.CooldownRemaining <= 0) yield break;
                yield return null;
            }
            Fail("Players did not finish recharging / respawning");
        }

        private IEnumerator MoveTo(PlayerControllerScript actor, Vector3 target)
        {
            float deadline = Time.time + timeout;
            while (Time.time < deadline && Failure == null)
            {
                if (!actor.temporarilyEliminated && Vector3.Distance(actor.transform.position, target) < .15f)
                { actor.ClearScriptedInput(); yield break; }
                Drive(actor, target, target - actor.transform.position, false); yield return null;
            }
            Fail("Player could not return through ordinary movement");
        }

        private static void Drive(PlayerControllerScript actor, Vector3 target, Vector3 aim, bool hold)
        {
            if (!actor || actor.temporarilyEliminated) return;
            var ability = actor.GetComponent<PlayerShieldAbility>();
            bool ready = actor.powerUps && actor.powerUps.UsesShieldCharge ? actor.powerUps.CanActivateDecoherence : ability.ActivationCooldownRemaining <= 0f;
            bool press = hold && !ability.IsActive && ready;
            var delta = target - actor.transform.position;
            Vector2 move = delta.magnitude < .08f ? Vector2.zero : Vector2.ClampMagnitude(new Vector2(delta.x, delta.z) * 1.2f, .55f);
            actor.SetScriptedInput(new PlayerInputFrame { moveInput = move, hasAimDirWS = true,
                aimDirWS = aim.sqrMagnitude > .001f ? aim : Vector3.right, shieldDown = press,
                shieldHeld = hold && (ability.IsActive || press) });
        }
        private void Neutral()
        { if (Primary) Primary.ClearScriptedInput(); if (Partner) Partner.ClearScriptedInput(); }
        private void Fail(string reason)
        { if (Failure != null) return; Failure = reason; Phase = reason; Neutral(); Debug.LogError("[Decoherence demo] " + reason, this); }

        private void Update()
        {
            if (!Primary) return;
            if (hadField && Primary.powerUps.DecoherenceFieldActive)
                FieldMovementDistance += Vector3.Distance(Primary.transform.position, previousPosition);
            hadField = Primary.powerUps.DecoherenceFieldActive; previousPosition = Primary.transform.position;
            if (Time.unscaledTime < nextLabelUpdate) return;
            nextLabelUpdate = Time.unscaledTime + .1f;
            if (status) status.text = Phase + "\n" + (hadField ? "PAINTING" : "REFILLING") + "  " + Primary.powerUps.DecoherenceCharge01.ToString("P0") +
                "  |  Activate from " + powerUp.minimumActivationCharge.ToString("P0") + "  |  Loops " + CompletedLoops;
            if (status) status.color = Failure != null ? new Color(1f, .35f, .3f) : new Color(.6f, .9f, 1f);
            if (!health) return;
            if (Partner)
            {
                health.text = "OPPONENT MASS  " + Partner.massScore.ToString("P0") + "\n" +
                    "Exposed loss " + LastExposedLoss.ToString("P1") + "   |   Shielded loss " + LastBlockedLoss.ToString("P1");
                if (explanation) explanation.text = "Painted ground remains dangerous after release\n" + powerUp.playerDamagePerSecond.ToString("P0") + " mass / second; ordinary Shield blocks it";
            }
            else
            {
                var lines = new List<string>();
                for (int i = 0; i < samples.Count; i++)
                {
                    var s = samples[i];
                    float expected = s.swordDamage > 0 ? s.initialHealth / s.swordDamage * powerUp.secondsPerSwordHit : 0;
                    lines.Add((samples.Count > 1 ? "Drone " + (i + 1) : "Carrier") + "  " + (s.defeated ? "DEFEATED" : (s.health / s.initialHealth).ToString("P0") + " HP") +
                        "   |   " + s.exposure.ToString("F1") + " / " + expected.ToString("F1") + "s exposure");
                }
                health.text = string.Join("\n", lines);
                if (explanation) explanation.text = example == Example.DroneSweep ? "Drag a shrinking brush across sample enemies\nEach painted area lasts " + powerUp.paintLifetimeSeconds.ToString("F1") + "s" :
                    "Tougher enemies need more accumulated exposure\nLeave the paint behind while charge refills";
            }
        }
    }
}
