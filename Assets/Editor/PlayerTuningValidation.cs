using System;
using System.Reflection;
using Massive.Player;
using Massive.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Massive.EditorTools
{
    public static class PlayerTuningValidation
    {
        private static int passed;
        [MenuItem("MASSIVE/Player/Validate Player Tuning")]
        public static void RunMenu() { Debug.Log(RunChecks()); }

        public static string RunChecks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run in Edit Mode.");
            passed = 0;
            var live = PlayerTuningEditing.GetOrCreate();
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                using (var f = new Fixture(live.attackProfile, preview))
                {
                    f.Begin(); f.Step(.15f); f.attack.RegisterAttackPress(); f.Step(.10f);
                    Check(f.attack.CurrentStageIndex == 0, "Legacy queued press waits until stage end");
                    f.Step(.05f); Check(f.attack.CurrentStageIndex == 1, "Legacy queued press advances at end");
                    f.Step(.4f); Check(!f.attack.IsAttacking, "Consumed press cannot automatically enter finisher");
                }
                using (var f = new Fixture(live.attackProfile, preview))
                {
                    f.Early(.08f, .26f, .18f); f.Begin(); f.Step(.10f); f.attack.RegisterAttackPress(); f.Step(.06f);
                    Check(f.attack.CurrentStageIndex == 0, "Queue waits for earliest handoff");
                    f.Step(.08f); Check(f.attack.CurrentStageIndex == 1, "Early handoff starts second stage before full duration");
                    Check(Mathf.Abs(f.finishedAt - .18f) < .0001f, "Completion event reports exact handoff time");
                    Check(f.events == "S0,C0,S1,", "Old stage completes before new stage starts");
                    Check(Mathf.Abs(f.go.transform.position.x - .6f) < .0001f, "Old travel is cut at handoff without jumping to endpoint");
                    f.Step(.4f); Check(!f.attack.IsAttacking, "Early handoff consumes exactly one press");
                }
                using (var f = new Fixture(live.attackProfile, preview))
                {
                    f.Early(.08f, .26f, .18f); f.Begin(); f.Step(.23f); float before = f.go.transform.position.x;
                    f.attack.RegisterAttackPress(); f.Step(.02f);
                    Check(f.attack.CurrentStageIndex == 1, "Late accepted press transitions next update");
                    Check(f.go.transform.position.x >= before, "Late handoff never rewinds movement");
                }
                using (var f = new Fixture(live.attackProfile, preview))
                {
                    f.Early(.08f, .26f, .18f); f.Begin(); f.Step(.30f);
                    Check(!f.attack.IsAttacking, "No press completes full first stage without chaining");
                    Check(Mathf.Abs(f.go.transform.position.x - 1) < .0001f, "Unchained attack keeps full travel");
                }
                using (var f = new Fixture(live.attackProfile, preview))
                {
                    f.Early(.08f, .20f, .18f); f.Begin(); f.Step(.25f); f.attack.RegisterAttackPress(); f.Step(.05f);
                    Check(!f.attack.IsAttacking, "Press after engagement window cannot chain");
                }
                using (var f = new Fixture(live.attackProfile, preview))
                {
                    f.Early(.15f, .23f, .18f); f.Begin();
                    SetPrivate(f.attack, "lastAttackPressTime", Time.time - .12f);
                    f.Step(.24f);
                    Check(f.attack.CurrentStageIndex == 1, "Buffered press survives a whole window crossed in one update");
                }
                using (var f = new Fixture(live.attackProfile, preview))
                {
                    f.Early(.15f, .23f, .18f); f.Begin();
                    SetPrivate(f.attack, "lastAttackPressTime", Time.time - .8f);
                    f.Step(.30f); Check(!f.attack.IsAttacking, "Expired buffer cannot chain");
                }
                using (var f = new Fixture(live.attackProfile, preview))
                {
                    f.Early(.15f, .25f, .05f); f.Begin();
                    SetPrivate(f.attack, "lastAttackPressTime", Time.time - .12f); f.Step(.24f);
                    Check(f.attack.CurrentStageIndex == 1 && Mathf.Abs(f.finishedAt - .15f) < .0001f,
                        "Handoff before engagement waits until buffered input is accepted");
                }
                using (var f = new Fixture(live.attackProfile, preview))
                {
                    f.Early(.15f, .25f, .18f); f.Begin();
                    SetPrivate(f.attack, "lastAttackPressTime", Time.time); f.Step(.24f);
                    Check(f.attack.CurrentStageIndex == 1 && Mathf.Abs(f.finishedAt - .24f) < .0001f,
                        "Press during crossed input window transitions at actual acceptance, not earlier");
                }
                using (var f = new Fixture(live.attackProfile, preview))
                {
                    f.Early(.02f, .25f, .1f); Set(f.profile, "stages.Array.data[0].allowComboCancel", false);
                    f.Begin(); f.Step(.1f); f.attack.RegisterAttackPress(); f.Step(.2f);
                    Check(!f.attack.IsAttacking, "Non-chainable stage ignores next press");
                    f.Begin(); f.Step(.1f); f.attack.CancelAttack(); f.Step(.1f);
                    Check(!f.attack.IsAttacking && f.attack.CurrentStage == null, "Cancellation clears stage state");
                }
                using (var f = new Fixture(live.attackProfile, preview))
                {
                    Set(f.attack, "attackCooldown", .5f); f.Begin(); f.Step(.3f); f.Begin();
                    Check(!f.attack.IsAttacking, "Post-combo cooldown remains enforced");
                    SetPrivate(f.attack, "lastAttackEndTime", Time.time - 1f); f.Begin();
                    Check(f.attack.IsAttacking, "Attack resumes after cooldown");
                    f.Early(.4f, -.5f, .8f);
                    var stage = f.profile.GetStage(0);
                    Check(stage.ComboWindowStartSeconds <= stage.Duration && stage.ComboWindowEndSeconds >= stage.ComboWindowStartSeconds && stage.ComboHandoffSeconds <= stage.Duration, "Invalid authored combo bounds resolve safely");
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            ValidateSharedAcrossScenes(live);
            return "Player Tuning: " + passed + " checks passed; temporary fixtures removed and shared values restored.";
        }

        private static void ValidateSharedAcrossScenes(PlayerTuningProfile live)
        {
            string saved = EditorJsonUtility.ToJson(live);
            var original = SceneManager.GetActiveScene();
            Scene scene = default;
            try
            {
                live.sharedEnabled = true; live.movement.maxMoveSpeed = 8.75f; live.combat.attackCooldown = .37f; live.repulsor.knockbackVelocity = 9.2f;
                for (int sceneIndex = 0; sceneIndex < 2; sceneIndex++)
                {
                    scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                    var go = new GameObject("Shared tuning fixture"); go.SetActive(false); SceneManager.MoveGameObjectToScene(go, scene);
                    var player = go.AddComponent<PlayerControllerScript>(); player.enabled = false;
                    var attack = go.AddComponent<PlayerAttackController>();
                    var repulsor = go.AddComponent<PlayerRepulsorAOE>();
                    Set(player, "maxMoveSpeed", 3f); Set(attack, "attackCooldown", .9f); Set(repulsor, "knockbackVelocity", 4f);
                    Check(player.Effective_maxMoveSpeed == 8.75f, "Movement shared in scene " + scene.handle);
                    Check(attack.Profile == live.attackProfile && attack.Effective_attackCooldown == .37f, "Combat and attack profile shared in scene " + scene.handle);
                    Check(repulsor.Effective_knockbackVelocity == 9.2f, "Repulsor impact shared in scene " + scene.handle);
                    player.UseSharedSettings = false; attack.UseSharedSettings = false; repulsor.UseSharedSettings = false;
                    Check(player.Effective_maxMoveSpeed == 3 && attack.Effective_attackCooldown == .9f && repulsor.Effective_knockbackVelocity == 4, "Per-component local fallback " + scene.handle);
                    player.UseSharedSettings = true; attack.UseSharedSettings = true; repulsor.UseSharedSettings = true;
                    live.sharedEnabled = false;
                    Check(player.Effective_maxMoveSpeed == 3 && attack.Effective_attackCooldown == .9f, "Master-off restores local values " + scene.handle);
                    live.sharedEnabled = true;
                    Set(player, "isPseudoPlayer", true);
                    Check(player.Effective_maxMoveSpeed == 3 && attack.Effective_attackCooldown == .9f && repulsor.Effective_knockbackVelocity == 4,
                        "Pseudo players keep local movement, combat and impact even before Awake");
                    EditorSceneManager.CloseScene(scene, true);
                    scene = default;
                    SceneManager.SetActiveScene(original);
                }
            }
            finally
            {
                EditorJsonUtility.FromJsonOverwrite(saved, live);
                AssetDatabase.SaveAssetIfDirty(live);
                if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
                SceneManager.SetActiveScene(original);
            }
        }

        private static void Check(bool success, string name)
        { if (!success) throw new Exception("Player Tuning check failed: " + name); passed++; }
        private static void Set(Object target, string path, object value)
        {
            using (var d = new SerializedObject(target))
            {
                var p = d.FindProperty(path);
                if (value is bool flag) p.boolValue = flag;
                else if (value is float number) p.floatValue = number;
                else if (value is AnimationCurve curve) p.animationCurveValue = curve;
                else p.objectReferenceValue = value as Object;
                d.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        private static void SetPrivate(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private sealed class Fixture : IDisposable
        {
            internal readonly PlayerAttackProfile profile;
            internal readonly GameObject go;
            internal readonly PlayerAttackController attack;
            internal string events = "";
            internal float finishedAt;
            private static readonly MethodInfo Tick = typeof(PlayerAttackController).GetMethod("UpdateStage", BindingFlags.Instance | BindingFlags.NonPublic);
            public Fixture(PlayerAttackProfile source, Scene scene)
            {
                profile = Object.Instantiate(source);
                Set(profile, "stages.Array.data[0].duration", .3f);
                Set(profile, "stages.Array.data[0].customComboWindow", false);
                Set(profile, "stages.Array.data[0].earlyComboHandoff", false);
                Set(profile, "stages.Array.data[0].travelDistance", 1f);
                Set(profile, "stages.Array.data[0].distanceCurve", AnimationCurve.Linear(0, 0, 1, 1));
                Set(profile, "stages.Array.data[1].duration", .4f);
                go = new GameObject("Attack timing fixture"); go.SetActive(false); SceneManager.MoveGameObjectToScene(go, scene);
                attack = go.AddComponent<PlayerAttackController>(); attack.UseSharedSettings = false;
                Set(attack, "attackProfile", profile); Set(attack, "attackCooldown", 0f);
                Set(attack, "lockOnEnabled", false); Set(attack, "comboInputBuffer", .15f);
                Set(attack, "useSharedComboWindow", true); Set(attack, "sharedComboWindowSeconds", .15f);
                attack.OnStageStarted.AddListener(s => events += "S" + attack.CurrentStageIndex + ",");
                attack.OnStageCompleted.AddListener(s => { events += "C" + attack.CurrentStageIndex + ","; finishedAt = attack.StageNormalizedTime * s.Duration; });
            }
            public void Early(float start, float end, float handoff)
            {
                Set(profile, "stages.Array.data[0].customComboWindow", true);
                Set(profile, "stages.Array.data[0].earlyComboHandoff", true);
                Set(profile, "stages.Array.data[0].comboWindowStartSeconds", start);
                Set(profile, "stages.Array.data[0].comboWindowEndSeconds", end);
                Set(profile, "stages.Array.data[0].comboHandoffSeconds", handoff);
            }
            public void Begin() => attack.BeginAttack();
            public void Step(float dt) { if (attack.IsAttacking) Tick.Invoke(attack, new object[] { dt }); }
            public void Dispose() { Object.DestroyImmediate(go); Object.DestroyImmediate(profile); }
        }
    }
}
