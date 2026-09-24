using System;
using System.Collections.Generic;
using Massive.Settings;
using Massive.Player;
using Massive.Multiplier;
using Massive.Resonance;
using Massive.Scoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Massive.EditorTools
{
    public static class SharedSettingsValidation
    {
        [MenuItem("MASSIVE/Validate Shared Settings")]
        public static void RunMenu() { Debug.Log(Run()); }
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run in Edit Mode.");
            SharedSettingsEditing.EnsureProfiles();
            var melee = SharedSettingsRuntime.Load<MeleeVisualProfile>();
            var amp = SharedSettingsRuntime.Load<AmplifierSharedProfile>();
            var resonance = SharedSettingsRuntime.Load<ResonanceSharedProfile>();
            var text = SharedSettingsRuntime.Load<TextAnimationSharedProfile>();
            SharedSettingsProfile[] profiles = { melee, amp, resonance, text };
            var snapshots = new string[profiles.Length];
            for (int i = 0; i < profiles.Length; i++) snapshots[i] = EditorJsonUtility.ToJson(profiles[i]);
            int passed = 0;
            Action<bool, string> check = (ok, message) => { if (!ok) throw new InvalidOperationException("Shared settings: " + message); passed++; };
            Action<float, float, string> near = (a, b, message) => check(Mathf.Abs(a-b) < .0001f, message + " expected " + b + ", got " + a);
            var previous = SceneManager.GetActiveScene();
            try
            {
                foreach (var profile in profiles)
                {
                    check(profile && EditorUtility.IsPersistent(profile), "Resources resolves a persistent " + profile.name);
                    var serialized = new SerializedObject(profile);
                    var it = serialized.GetIterator();
                    while (it.Next(true))
                        if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue)
                            check(EditorUtility.IsPersistent(it.objectReferenceValue), profile.name + "." + it.propertyPath + " contains no scene reference");
                    serialized.Dispose();
                    profile.sharedEnabled = true;
                }
                melee.visualStyle = MeleeVisualStyle.SwordSlashes;
                melee.arcSweepTreatment = ArcSweepTreatment.Volumetric;
                melee.volumeDensity = 3.17f;
                melee.volumeSheathIntensity = .42f;
                melee.body.expansion = .19f;
                melee.grid.travelRadius = 1.8f;
                amp.core.coreScale = 1.5f; amp.core.mass = 6.5f; amp.core.linearDrag = .72f;
                amp.goal.repulsionAcceleration = 41f;
                amp.toast.lightAnchor = new Vector2(.23f, .68f);
                amp.toast.lightLabel = "SHARED TEST";
                amp.treatment.allowEffectsOverGrid = false;
                resonance.formation.spawnSeconds = 2f;
                resonance.formation.despawnSeconds = .8f;
                resonance.cycle.maximumActiveSeconds = 19f;
                text.scoreDigitMorphIntensity = 1.23f;

                for (int sceneIndex = 0; sceneIndex < 2; sceneIndex++)
                {
                    var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                    try
                    {
                    SceneManager.SetActiveScene(scene);
                    var plasma = Make<PlayerMeleePlasma>(scene);
                    plasma.visualStyle = MeleeVisualStyle.Off;
                    plasma.volumeDensity = .3f;
                    plasma.previewPlaybackSpeed = .27f;
                    plasma.previewProfile = AssetDatabase.LoadAssetAtPath<PlayerAttackProfile>("Assets/Scripts/Player/Actions/PlayerAttackProfile.asset");
                    check(plasma.Effective_visualStyle == MeleeVisualStyle.SwordSlashes, "melee style crosses scene boundary");
                    near(plasma.Effective_volumeDensity, 3.17f, "melee density");
                    near(plasma.previewPlaybackSpeed, .27f, "preview speed remains local");
                    plasma.gameObject.SetActive(true);
                    plasma.ScrubPreview(1, .55f);
                    check(plasma.IsRendering, "shared sweep creates a visible render volume");
                    var emission = typeof(PlayerMeleePlasma).GetField("activeVolume", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(plasma);
                    var renderer = (MeshRenderer)emission.GetType().GetField("renderer").GetValue(emission);
                    var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
                    near(block.GetVector("_ArcLight").x, 3.17f, "shared density reaches actual shader property block");
                    near(block.GetVector("_ArcWeight").x, .42f, "shared sheath intensity reaches shader");
                    plasma.StopPreview();
                    plasma.UseSharedSettings = false;
                    near(plasma.Effective_volumeDensity, .3f, "local melee density restored");
                    check(plasma.Effective_visualStyle == MeleeVisualStyle.Off, "local style restored");
                    plasma.UseSharedSettings = true;
                    var feedback = Make<PlayerRepulsorFeedback>(scene);
                    near(feedback.Effective_expansion, .19f, "body feedback shares melee profile");
                    var pulse = Make<PlayerRepulsorGridPulse>(scene);
                    near(pulse.Effective_travelRadius, 1.8f, "grid pulse shares melee profile");

                    var core = Make<AmplifierCoreGameplay>(scene);
                    var body = core.GetComponent<Rigidbody>();
                    var localCore = new SerializedObject(core);
                    localCore.FindProperty("coreScale").floatValue = .75f;
                    localCore.FindProperty("mass").floatValue = 2f;
                    localCore.ApplyModifiedPropertiesWithoutUndo();
                    core.ApplyTuning();
                    near(core.transform.localScale.x, 1.5f, "global Core scale reaches transform");
                    near(body.mass, 6.5f, "global mass reaches Rigidbody");
                    near(body.linearDamping, .72f, "global drag reaches Rigidbody");
                    core.ApplyTuning();
                    near(core.transform.localScale.x, 1.5f, "reapply does not compound scale");
                    core.UseSharedSettings = false; core.ApplyTuning();
                    near(core.transform.localScale.x, .75f, "Core local size restored");
                    near(body.mass, 2f, "Core local mass restored");
                    localCore.Dispose();

                    var goal = Make<AmplifierGoalCapture>(scene);
                    near(goal.Effective_repulsionAcceleration, 41f, "shared capped-goal repulsion");
                    var toast = Make<TeamAmplifierToastPresenter>(scene);
                    check(toast.Effective_lightAnchor == amp.toast.lightAnchor, "Light toast placement is shared");
                    check(toast.Effective_lightLabel == "SHARED TEST", "Light toast label is shared");
                    var effects = Make<AmplifierGoalTreatments>(scene);
                    effects.PlaybackSpeed = .35f;
                    check(ReferenceEquals(effects.Settings, amp.treatment), "goal effect consumes shared treatment");
                    near(effects.PlaybackSpeed, .35f, "capture playback remains local");
                    check(!effects.Preview, "shared tuning does not start capture preview");
                    effects.UseSharedSettings = false;
                    check(!ReferenceEquals(effects.Settings, amp.treatment), "local goal treatment remains intact");

                    var formation = Make<ResonanceManifestation>(scene);
                    formation.spawnSeconds = 8f;
                    formation.SetArea(new Vector2(1, 2), new Vector2(3, 4));
                    formation.SetImmediate(false); formation.BeginSpawn(); formation.Advance(1f);
                    near(formation.NormalizedProgress, .5f, "shared formation duration drives animation");
                    formation.SetImmediate(true); formation.BeginDespawn(); formation.Advance(.4f);
                    near(formation.NormalizedProgress, .5f, "shared dissolution duration drives animation");
                    check(formation.dispersalHalfExtents == new Vector2(3, 4), "level dimensions remain local");
                    formation.UseSharedSettings = false;
                    formation.SetImmediate(false); formation.BeginSpawn(); formation.Advance(1f);
                    near(formation.NormalizedProgress, .125f, "local formation duration restored");
                    var cycle = Make<AmplifierResonanceSpawner>(scene);
                    near(cycle.Effective_maximumActiveSeconds, 19f, "sequence timeout is shared");
                    cycle.maximumActiveSeconds = 47f; cycle.UseSharedSettings = false;
                    near(cycle.Effective_maximumActiveSeconds, 47f, "local sequence timeout restored");
                    var scoreboard = Make<ScoreboardManagerScript>(scene);
                    near(scoreboard.ScoreDigitMorphIntensity, 1.23f, "score intensity is shared");
                    check(scoreboard.ScoreDigitMorphPreset == text.scoreDigitMorphPreset, "score preset is shared");
                    var tiers = Make<EnergyTierVisualController>(scene);
                    check(tiers.Profile == text.energyTierProfile, "tier profile assignment is shared");

                    melee.sharedEnabled = false;
                    near(plasma.Effective_volumeDensity, .3f, "profile-wide off restores local fallback");
                    melee.sharedEnabled = true;
                    }
                    finally { EditorSceneManager.CloseScene(scene, true); }
                }
                SharedSettingsRuntime.Reload();
                check(SharedSettingsRuntime.Load<MeleeVisualProfile>() == melee, "reload resolves the same asset");
                return passed + " shared-settings checks passed in two independent scenes, including rendered sweep parameters, physics, formation timing, local overrides, preview isolation and persistent asset references.";
            }
            finally
            {
                for (int i = 0; i < profiles.Length; i++) EditorJsonUtility.FromJsonOverwrite(snapshots[i], profiles[i]);
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                SharedSettingsRuntime.Reload();
            }
        }
        private static T Make<T>(Scene scene) where T : Component
        {
            var root = new GameObject("Shared settings fixture — " + typeof(T).Name);
            root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, scene);
            if (typeof(T) == typeof(AmplifierCoreGameplay) || typeof(T) == typeof(AmplifierGoalCapture)) root.AddComponent<SphereCollider>();
            return root.AddComponent<T>();
        }
    }
}
