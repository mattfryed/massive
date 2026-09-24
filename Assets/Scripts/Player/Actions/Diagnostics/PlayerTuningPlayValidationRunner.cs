#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using Massive.Player;
using UnityEngine;

namespace Massive.Diagnostics
{
    /// <summary>Isolated runtime input/phase fixture. Does not drive or alter the scene's players.</summary>
    public sealed class PlayerTuningPlayValidationRunner : MonoBehaviour
    {
        public static string Result { get; private set; } = "Not run";
        private GameObject fixture;
        private PlayerAttackProfile profile;
        private PlayerAttackController attack;
        private CapsuleCollider hitbox;
        private int checks, started, completed;
        private float firstCompletion;
        private bool oldHitboxWasOff;

        public static void Begin()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            if (Result == "Running") throw new InvalidOperationException("Validation already running.");
            Result = "Running";
            new GameObject("Player Tuning runtime validation") { hideFlags = HideFlags.DontSave }.AddComponent<PlayerTuningPlayValidationRunner>();
        }
        private IEnumerator Start()
        {
            IEnumerator test = Exercise();
            while (true)
            {
                object next = null; bool running;
                try { running = test.MoveNext(); if (running) next = test.Current; }
                catch (Exception e) { Result = "FAILED: " + e.Message; Debug.LogError(Result); break; }
                if (!running) { Result = "Player Tuning Play Mode: " + checks + " checks passed (early chain, input consumption, hitbox handoff, no-input completion and cancellation)."; Debug.Log(Result); break; }
                yield return next;
            }
            if (fixture) Destroy(fixture);
            if (profile) Destroy(profile);
            Destroy(gameObject);
        }
        private IEnumerator Exercise()
        {
            profile = Instantiate(Massive.Settings.SharedSettingsRuntime.Load<Massive.Settings.PlayerTuningProfile>().attackProfile);
            for (int i = 0; i < 3; i++)
            {
                var stage = profile.GetStage(i);
                Set(stage, "duration", i == 0 ? .6f : .55f);
                Set(stage, "travelDistance", 0f);
                Set(stage, "activationFrameStart", 3f); Set(stage, "activationFrameEnd", 30f);
                Set(stage, "animationFrameRate", 60f);
                Set(stage, "customComboWindow", true); Set(stage, "earlyComboHandoff", true);
                Set(stage, "comboWindowStartSeconds", .12f); Set(stage, "comboWindowEndSeconds", .48f);
                Set(stage, "comboHandoffSeconds", .25f); Set(stage, "allowComboCancel", i < 2);
            }
            fixture = new GameObject("Isolated combo fixture"); fixture.SetActive(false); fixture.transform.position = new Vector3(1000, 0, 1000);
            attack = fixture.AddComponent<PlayerAttackController>(); attack.UseSharedSettings = false;
            Set(attack, "attackProfile", profile); Set(attack, "attackCooldown", 0f); Set(attack, "lockOnEnabled", false);
            var sword = new GameObject("Melee gate"); sword.transform.SetParent(fixture.transform, false);
            hitbox = sword.AddComponent<CapsuleCollider>(); hitbox.isTrigger = true;
            sword.AddComponent<PlayerMelee>();
            fixture.SetActive(true);
            attack.OnStageStarted.AddListener(s => { started++; if (attack.CurrentStageIndex > 0) oldHitboxWasOff &= !hitbox.enabled; });
            attack.OnStageCompleted.AddListener(s => { completed++; if (attack.CurrentStageIndex == 0) firstCompletion = attack.StageNormalizedTime * s.Duration; });
            ResetObservations(); attack.BeginAttack();
            yield return new WaitForSeconds(.15f);
            Check(hitbox.enabled, "Thrust damage window actually enables the collider");
            attack.RegisterAttackPress();
            yield return new WaitForSeconds(.14f);
            Check(attack.CurrentStageIndex == 1, "Queued input enters sweep at early handoff");
            Check(firstCompletion < .6f && Mathf.Abs(firstCompletion - .25f) < .015f, "First stage hands off at the authored early time");
            Check(oldHitboxWasOff, "Old damage collider disabled before new stage begins");
            yield return new WaitForSeconds(.6f);
            Check(started == 2 && completed == 2 && !attack.IsAttacking, "One additional press advances exactly one stage");
            Check(!hitbox.enabled, "Damage collider off after chain finishes");

            ResetObservations(); attack.BeginAttack();
            yield return new WaitForSeconds(.7f);
            Check(started == 1 && !attack.IsAttacking, "No next press does not auto-chain");
            Check(Mathf.Abs(firstCompletion - .6f) < .015f, "No press retains the full attack duration");

            ResetObservations(); attack.BeginAttack();
            yield return new WaitForSeconds(.15f); attack.RegisterAttackPress();
            yield return new WaitForSeconds(.25f); attack.RegisterAttackPress();
            yield return new WaitForSeconds(.25f);
            Check(attack.CurrentStageIndex == 2 && started == 3, "A fresh second press reaches Repulsor");
            yield return new WaitForSeconds(.6f);
            Check(!attack.IsAttacking && !hitbox.enabled, "Finisher ends and releases collider");

            attack.BeginAttack(); yield return new WaitForSeconds(.12f);
            attack.CancelAttack(true);
            Check(!hitbox.enabled && !attack.IsAttacking, "Explicit completion cancellation releases damage immediately");
        }
        private void ResetObservations() { started = completed = 0; firstCompletion = 0; oldHitboxWasOff = true; }
        private void Check(bool result, string label) { if (!result) throw new Exception(label); checks++; }
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private void OnDestroy() { if (fixture) Destroy(fixture); if (profile) Destroy(profile); }
    }
}
#endif
