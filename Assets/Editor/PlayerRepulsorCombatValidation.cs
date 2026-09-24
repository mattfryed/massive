#if UNITY_EDITOR
using System;
using System.Reflection;
using Massive.Enemies;
using Massive.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.EditorTools
{
    /// <summary>
    /// Isolated nonlethal combat rules. Physics delivery and defeat/scoring are
    /// checked separately in Play Mode because they require the runtime lifecycle.
    /// </summary>
    public static class PlayerRepulsorCombatValidation
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("MASSIVE/Player/Validate Repulsor Combat")]
        public static void RunMenu() { Debug.Log(RunChecks()); }

        public static string RunChecks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run Repulsor combat validation in Edit Mode.");

            int passed = 0;
            Action<bool, string> check = (condition, description) =>
            {
                if (!condition) throw new InvalidOperationException("Repulsor combat: " + description);
                passed++;
            };
            Action<float, float, string> near = (actual, expected, description) =>
                check(Mathf.Abs(actual - expected) < .0001f,
                    description + " (expected " + expected + ", got " + actual + ")");

            Scene fixture = EditorSceneManager.NewPreviewScene();
            var definition = ScriptableObject.CreateInstance<EnemyDefinition>();
            definition.hideFlags = HideFlags.HideAndDontSave;
            definition.healthMassEq = 10f;
            definition.defeatRewardKey = "";
            try
            {
                var stage = new AttackStage();
                Set(stage, "repulsorMaxRadius", 3f);
                Set(stage, "repulsorScale", 1f);
                near(stage.GetRepulsorRadius(.5f, .25f), 1.5f,
                    "half-size player uses half the default reach");
                Set(stage, "repulsorScale", 2f);
                near(stage.GetRepulsorRadius(.5f, .25f), 3f,
                    "overall attack scale composes once with player size");
                near(stage.GetRepulsorRadius(1f, .5f), 6f,
                    "full-size player uses the same independent attack scale");
                Set(stage, "repulsorScale", .1f);
                near(stage.GetRepulsorRadius(.5f, .4f), .4f,
                    "a small attack cannot contract inside its emission outline");

                GameObject body = Make("Repulsor combat - inert owner", fixture);
                var owner = body.AddComponent<PlayerControllerScript>();
                owner.enabled = false;
                owner.teamID = 1;
                GameObject shell = Make("Repulsor combat - shell", fixture);
                shell.transform.SetParent(body.transform, false);
                var pulse = shell.AddComponent<PlayerRepulsorAOE>();
                var hitbox = shell.GetComponent<SphereCollider>();
                Set(pulse, "owner", owner);
                Set(pulse, "hitbox", hitbox);
                Set(pulse, "ignoreTeamMates", true);
                Set(pulse, "applyKnockback", false);
                Set(pulse, "applyStun", false);
                Set(pulse, "applyMassLoss", false);
                SetAuto(pulse, "OriginWorld", Vector3.zero);
                SetAuto(pulse, "EndRadiusWorld", 3f);

                var neutral = MakeEnemy("Neutral NPC with two hurtboxes", fixture, definition);
                Collider first = MakeHurtbox(neutral, "Body hurtbox");
                Collider second = MakeHurtbox(neutral, "Second hurtbox");
                check(first.GetComponentInParent<EnemyBase>() == neutral,
                    "a child hurtbox resolves the NPC root");
                Arm(pulse, hitbox, 2.5f);
                Hit(pulse, first);
                near(neutral.HealthRemaining, 7.5f, "an untagged NPC takes configured damage");
                Hit(pulse, second);
                Hit(pulse, first);
                near(neutral.HealthRemaining, 7.5f,
                    "multiple hurtboxes and repeated stay callbacks cannot multiply one pulse's damage");

                Invoke(pulse, "StopAndReset");
                check(!pulse.IsPulseActive && !hitbox.enabled,
                    "ending a pulse disables its physical hitbox");
                Hit(pulse, first);
                near(neutral.HealthRemaining, 7.5f,
                    "late contact after cancellation causes no damage");
                Arm(pulse, hitbox, 1.25f);
                Hit(pulse, second);
                near(neutral.HealthRemaining, 6.25f,
                    "a subsequent pulse can hit the same NPC with its new damage amount");

                var friendly = MakeEnemy("Team-affiliated friendly NPC", fixture, definition);
                friendly.OwnerTeamId = owner.teamID;
                Hit(pulse, MakeHurtbox(friendly, "Friendly hurtbox"));
                near(friendly.HealthRemaining, 10f, "friendly NPCs are excluded");

                var paused = MakeEnemy("Paused NPC", fixture, definition);
                paused.Pause(true);
                Collider pausedCollider = MakeHurtbox(paused, "Paused hurtbox");
                Hit(pulse, pausedCollider);
                near(paused.HealthRemaining, 10f, "paused NPCs are excluded");
                paused.Pause(false);
                Hit(pulse, pausedCollider);
                near(paused.HealthRemaining, 8.75f,
                    "rejected paused contact does not consume the later eligible hit");

                var dead = MakeEnemy("Dead NPC fixture", fixture, definition);
                SetAuto(dead, "IsDead", true);
                Hit(pulse, MakeHurtbox(dead, "Dead hurtbox"));
                near(dead.HealthRemaining, 10f, "dead NPCs are excluded");

                var disabled = MakeEnemy("Disabled NPC", fixture, definition);
                disabled.enabled = false;
                Hit(pulse, MakeHurtbox(disabled, "Disabled hurtbox"));
                near(disabled.HealthRemaining, 10f, "disabled NPC components are excluded");

                var weaponOwner = MakeEnemy("NPC with separate weapon collider", fixture, definition);
                var weaponObject = new GameObject("Weapon collider is not a hurtbox");
                weaponObject.hideFlags = HideFlags.HideAndDontSave;
                weaponObject.transform.SetParent(weaponOwner.transform, false);
                Collider weapon = weaponObject.AddComponent<BoxCollider>();
                Hit(pulse, weapon);
                near(weaponOwner.HealthRemaining, 10f,
                    "an NPC's extended weapon cannot receive body damage");
                Collider disabledHurtbox = MakeHurtbox(weaponOwner, "Temporarily disabled hurtbox");
                disabledHurtbox.GetComponent<EnemyHurtbox>().enabled = false;
                Hit(pulse, disabledHurtbox);
                near(weaponOwner.HealthRemaining, 10f,
                    "disabled hurtboxes cannot receive damage");

                Invoke(pulse, "StopAndReset");
                Arm(pulse, hitbox, 0f);
                Hit(pulse, first);
                near(neutral.HealthRemaining, 6.25f, "zero damage leaves NPC health unchanged");
                Invoke(pulse, "StopAndReset");

                check((int)EnemyDamageSource.Repulsor > (int)EnemyDamageSource.LifetimeExpired,
                    "Repulsor's damage source preserves earlier serialized enum values");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(fixture);
                UnityEngine.Object.DestroyImmediate(definition);
            }
            return "Repulsor combat validation: " + passed +
                " checks passed (isolated Edit Mode rules; real trigger delivery and defeat attribution require Play Mode).";
        }

        static GameObject Make(string name, Scene scene)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            go.hideFlags = HideFlags.HideAndDontSave;
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }

        static EnemyBase MakeEnemy(string name, Scene scene, EnemyDefinition definition)
        {
            GameObject go = Make(name, scene);
            var enemy = go.AddComponent<EnemyBase>();
            enemy.Init(definition, null);
            go.SetActive(true);
            return enemy;
        }

        static Collider MakeHurtbox(EnemyBase enemy, string name)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetParent(enemy.transform, false);
            var collider = go.AddComponent<SphereCollider>();
            collider.radius = .2f;
            collider.isTrigger = true;
            var hurtbox = go.AddComponent<EnemyHurtbox>();
            Set(hurtbox, "enemy", enemy);
            go.SetActive(true);
            return collider;
        }

        static void Arm(PlayerRepulsorAOE pulse, SphereCollider collider, float damage)
        {
            Set(pulse, "_enemyDamage", damage);
            SetAuto(pulse, "IsPulseActive", true);
            collider.radius = 3f;
            collider.enabled = true;
        }

        static void Hit(PlayerRepulsorAOE pulse, Collider collider) => Invoke(pulse, "TryHit", collider);
        static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, Private).SetValue(target, value);
        static void SetAuto(object target, string property, object value) =>
            Set(target, "<" + property + ">k__BackingField", value);
        static object Invoke(object target, string method, params object[] arguments) =>
            target.GetType().GetMethod(method, Private).Invoke(target, arguments);
    }
}
#endif
