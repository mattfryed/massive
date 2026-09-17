#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using Massive.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.EditorTools
{
    /// <summary>Inert fixtures: authored timing, real input queue transitions, world geometry and cancellation.</summary>
    public static class PlayerRepulsorValidation
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("MASSIVE/Player/Validate Repulsor")]
        public static void RunMenu() { Debug.Log(RunChecks()); }

        public static string RunChecks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run Repulsor validation in Edit Mode.");

            int passed = 0;
            Action<bool, string> check = (condition, message) =>
            {
                if (!condition) throw new InvalidOperationException("Repulsor: " + message);
                passed++;
            };
            Action<float, float, string> near = (actual, expected, message) =>
                check(Mathf.Abs(actual - expected) < .0001f,
                    message + " (expected " + expected + ", got " + actual + ")");

            var authored = AssetDatabase.LoadAssetAtPath<PlayerAttackProfile>(
                "Assets/Scripts/Player/Actions/PlayerAttackProfile.asset");
            check(authored && authored.Stages.Count >= 3, "shared attack profile contains the complete chain");
            check(authored.GetStage(0).AllowComboCancel && authored.GetStage(1).AllowComboCancel,
                "both preceding stages allow a deliberate next press");
            AttackStage authoredRepulsor = authored.GetStage(2);
            check(authoredRepulsor.StageType == AttackStageType.FinisherRepulsor, "third stage is the Repulsor");
            near(authoredRepulsor.Duration, .6f, "Repulsor has a readable windup and recovery");
            near(authoredRepulsor.ActivationFrameStart, 10f, "activation begins at frame 10");
            near(authoredRepulsor.ActivationFrameEnd, 25f, "activation ends at frame 25");
            near(authoredRepulsor.TravelDistance, 0f, "authored Repulsor is stationary");

            // Only this transient copy is mutated by the checks below.
            PlayerAttackProfile profile = UnityEngine.Object.Instantiate(authored);
            Scene fixture = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject body = Make("Repulsor validation - inert owner", fixture);
                var attack = body.AddComponent<PlayerAttackController>();
                Set(attack, "attackProfile", profile);
                Set(attack, "lockOnEnabled", false);
                Set(attack, "attackCooldown", 0f);
                Set(attack, "useSharedComboWindow", true);
                Set(attack, "sharedComboWindowSeconds", .15f);
                Set(attack, "comboInputBuffer", .15f);

                attack.BeginAttack();
                check(attack.CurrentStageIndex == 0, "first press starts the lunge");
                Invoke(attack, "UpdateStage", .16f);
                check(!(bool)Get(attack, "comboQueued"), "initial press cannot automatically queue the swipe");
                Invoke(attack, "UpdateStage", .15f);
                check(!attack.IsAttacking, "one press finishes after the lunge");

                for (int index = 0; index < 2; index++)
                {
                    Invoke(attack, "StartStage", index);
                    float duration = profile.GetStage(index).Duration;
                    float start = 1f - .15f / duration;
                    check(!(bool)Invoke(attack, "IsWithinComboWindow", start - .001f),
                        "stage " + index + " rejects before its shared queue window");
                    check((bool)Invoke(attack, "IsWithinComboWindow", start + .001f),
                        "stage " + index + " accepts in its shared queue window");
                    near((1f - start) * duration, .15f, "shared opportunity has the same duration");
                }

                Invoke(attack, "StartStage", 1);
                Set(attack, "lastAttackPressTime", Time.time - .14f);
                Invoke(attack, "UpdateStage", .26f);
                check((bool)Get(attack, "comboQueued"), "a recent early press is buffered into the shared window");
                Invoke(attack, "StartStage", 1);
                Set(attack, "lastAttackPressTime", Time.time - .16f);
                Invoke(attack, "UpdateStage", .26f);
                check(!(bool)Get(attack, "comboQueued"), "an expired early press cannot queue the next stage");

                Invoke(attack, "StartStage", 0);
                Invoke(attack, "UpdateStage", .20f);
                attack.RegisterAttackPress();
                Invoke(attack, "UpdateStage", .11f);
                check(attack.CurrentStageIndex == 1, "second deliberate press reaches the swipe");
                Invoke(attack, "UpdateStage", .41f);
                check(!attack.IsAttacking, "consumed second press cannot automatically queue Repulsor");

                Invoke(attack, "StartStage", 0);
                Invoke(attack, "UpdateStage", .20f);
                attack.RegisterAttackPress();
                Invoke(attack, "UpdateStage", .11f);
                Invoke(attack, "UpdateStage", .30f);
                attack.RegisterAttackPress();
                Invoke(attack, "UpdateStage", .11f);
                check(attack.CurrentStageIndex == 2, "third deliberate press reaches Repulsor");
                Vector3 before = body.transform.position;
                Invoke(attack, "UpdateStage", .61f);
                check(body.transform.position == before, "Repulsor does not lunge");
                check(!attack.IsAttacking, "Repulsor ends the sequence");

                // Future profile changes cannot accidentally turn this radial action into a dash.
                Set(profile.GetStage(2), "travelDistance", 5f);
                Invoke(attack, "StartStage", 2);
                Invoke(attack, "UpdateStage", .3f);
                check(body.transform.position == before, "stationary action policy survives stray profile travel");

                Set(attack, "useSharedComboWindow", false);
                Set(attack, "comboWindowAfterActivationWindow", true);
                Invoke(attack, "StartStage", 0);
                check(!(bool)Invoke(attack, "IsWithinComboWindow", .99f), "legacy lunge recovery window remains available");
                check((bool)Invoke(attack, "IsWithinComboWindow", 1f), "legacy lunge end remains available");
                Invoke(attack, "StartStage", 1);
                check((bool)Invoke(attack, "IsWithinComboWindow", .5f), "legacy swipe recovery window remains available");

                int cancelled = 0, completed = 0;
                attack.StageCancelled += stage => cancelled++;
                attack.OnStageCompleted.AddListener(stage => completed++);
                attack.CancelAttack();
                check(cancelled == 1 && completed == 0 && !attack.IsAttacking,
                    "cancellation emits its own event without successful completion");
                attack.CancelAttack();
                check(cancelled == 1, "repeated cancellation is idempotent");
                Invoke(attack, "StartStage", 2);
                Invoke(attack, "OnDisable");
                check(cancelled == 2 && !attack.IsAttacking, "disabling attack releases its active stage");

                Set(attack, "useSharedComboWindow", true);
                UnityEngine.Events.UnityAction<AttackStage> cancelOnCompletion = stage => attack.CancelAttack();
                attack.OnStageCompleted.AddListener(cancelOnCompletion);
                Invoke(attack, "StartStage", 0);
                Invoke(attack, "UpdateStage", .20f);
                attack.RegisterAttackPress();
                Invoke(attack, "UpdateStage", .11f);
                check(!attack.IsAttacking, "cancellation during completion cannot restart a queued stage");
                attack.OnStageCompleted.RemoveListener(cancelOnCompletion);

                var owner = body.AddComponent<PlayerControllerScript>();
                owner.enabled = false;
                owner.teamID = 1;
                var visuals = body.AddComponent<PlayerVisualController>();
                visuals.enabled = false;
                visuals.visuals = body.transform;
                visuals.baseRadius = .5f;
                visuals.outlineHalf = .05f;
                owner.visualsController = visuals;
                var scale = body.AddComponent<PlayerScaleAdjuster>();
                GameObject shell = Make("Physical shell", fixture);
                shell.transform.SetParent(body.transform, false);
                shell.transform.localScale = Vector3.one * .4f;
                var aoe = shell.AddComponent<PlayerRepulsorAOE>();
                var sphere = shell.GetComponent<SphereCollider>();
                Set(aoe, "owner", owner);
                Set(aoe, "attackController", attack);
                Set(aoe, "hitbox", sphere);
                Set(aoe, "_visuals", visuals);
                Set(aoe, "applyKnockback", false);
                Set(aoe, "applyStun", false);
                Set(aoe, "applyMassLoss", false);

                near(aoe.EffectiveActivationStart(authoredRepulsor) * authoredRepulsor.Duration,
                    10f / 60f, "physical shell onset shares profile time");
                near(aoe.EffectiveActivationEnd(authoredRepulsor) * authoredRepulsor.Duration,
                    25f / 60f, "physical shell ending shares profile time");
                Set(aoe, "gateToStageActivationWindow", false);
                near(aoe.EffectiveActivationStart(authoredRepulsor), 0f, "whole-stage compatibility onset");
                near(aoe.EffectiveActivationEnd(authoredRepulsor), 1f, "whole-stage compatibility end");

                Vector3 origin = new Vector3(4f, 0f, -3f);
                SetAuto(aoe, "OriginWorld", origin);
                foreach (float size in new[] { 1f, .5f, 2f })
                {
                    scale.Size = size;
                    near((float)Invoke(aoe, "GetOutlineRadiusWorld"), .55f * size,
                        "shell starts from scaled body plus outline");
                    visuals.SetRepulsorVisual(.9f, Vector3.zero);
                    near((float)Invoke(aoe, "GetOutlineRadiusWorld"), .55f * size * .9f,
                        "shell includes live body contraction");
                    visuals.ClearRepulsorVisual();
                    float end = authoredRepulsor.RepulsorMaxRadius * PlayerScaleAdjuster.SizeOf(owner);
                    sphere.enabled = true;
                    Invoke(aoe, "SetWorldRadius", end);
                    near(aoe.RadiusWorld, end, "nested collider scale does not multiply reach twice");
                    check((sphere.transform.TransformPoint(sphere.center) - origin).sqrMagnitude < .00001f,
                        "physical shell keeps the release origin while body scale changes");
                }

                GameObject target = Make("Inert opposing player", fixture);
                target.tag = "Player";
                var victim = target.AddComponent<PlayerControllerScript>();
                victim.enabled = false;
                victim.teamID = 2;
                var firstCollider = target.AddComponent<SphereCollider>();
                var secondCollider = target.AddComponent<BoxCollider>();
                // GetComponentInParent follows the runtime active hierarchy. The
                // player behaviour stays disabled, so this remains an inert edit
                // fixture while accurately modelling the target lookup.
                target.SetActive(true);
                check(firstCollider.GetComponentInParent<PlayerControllerScript>() == victim,
                    "the active target collider resolves its disabled player component");
                sphere.enabled = true;
                SetAuto(aoe, "IsPulseActive", true);
                SetAuto(aoe, "EndRadiusWorld", 3f);
                var hits = (HashSet<PlayerControllerScript>)Get(aoe, "_hitVictims");
                Invoke(aoe, "TryHit", firstCollider);
                Invoke(aoe, "TryHit", secondCollider);
                Invoke(aoe, "TryHit", firstCollider);
                check(hits.Count == 1 && hits.Contains(victim), "overlapping colliders and stay callbacks accept each victim once");
                int ended = 0;
                aoe.PulseEnded += pulse => ended++;
                Invoke(aoe, "StopAndReset");
                check(!aoe.IsPulseActive && !sphere.enabled && hits.Count == 0,
                    "cancel clears the active collider and victim set");
                near(sphere.radius, 0f, "cancel removes lingering reach");
                Invoke(aoe, "TryHit", firstCollider);
                check(hits.Count == 0, "late trigger callbacks cannot hit after cancellation");
                Invoke(aoe, "StopAndReset");
                check(ended == 1, "cleanup emits one pulse ending");

                near(authored.GetStage(2).TravelDistance, 0f, "validation leaves the shared asset unchanged");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(fixture);
                UnityEngine.Object.DestroyImmediate(profile);
            }
            return "Repulsor validation: " + passed + " checks passed (inert Edit Mode fixtures; live collision timing requires Play Mode).";
        }

        static GameObject Make(string name, Scene scene)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            go.hideFlags = HideFlags.HideAndDontSave;
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }
        static object Get(object target, string field) => target.GetType().GetField(field, Private).GetValue(target);
        static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
        static void SetAuto(object target, string property, object value) => Set(target, "<" + property + ">k__BackingField", value);
        static object Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
    }
}
#endif
