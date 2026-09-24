using System;
using Massive.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.EditorTools
{
    public static class PlayerGlobalModifiersValidation
    {
        [MenuItem("MASSIVE/Player/Validate Global Player Modifiers")]
        public static void RunMenu() { Debug.Log(RunChecks()); }

        public static string RunChecks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
            var profile = PlayerGlobalModifiersEditing.GetOrCreate();
            string saved = EditorJsonUtility.ToJson(profile);
            var original = SceneManager.GetActiveScene();
            // A normal, empty scene exercises global resolution; preview scenes intentionally
            // bypass globals so unrelated isolated physics/geometry tests stay deterministic.
            var fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            int passed = 0;
            Action<bool, string> check = (ok, label) => { if (!ok) throw new Exception(label); passed++; };
            try
            {
                using (var data = new SerializedObject(profile))
                {
                    data.FindProperty("globalSizeEnabled").boolValue = true;
                    data.FindProperty("globalReversalEnabled").boolValue = true;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                for (int i = 0; i < 4; i++)
                {
                    var go = new GameObject("Global tuning validation P" + (i + 1));
                    SceneManager.MoveGameObjectToScene(go, fixture);
                    go.SetActive(false);
                    var player = go.AddComponent<PlayerControllerScript>(); player.playerID = i; player.enabled = false;
                    var sphere = go.AddComponent<SphereCollider>(); sphere.radius = .5f;
                    var scale = go.AddComponent<PlayerScaleAdjuster>();
                    var reversal = go.AddComponent<PlayerMovementReversal>();
                    scale.Size = 1.2f; reversal.backwardConeDegrees = 45f; reversal.retainedMomentum = .9f;
                    var slot = profile.ForPlayer(i);
                    slot.size = .55f + i * .1f; slot.retainedMomentum = i * .2f; slot.backwardConeDegrees = 80f + i * 10f;
                    scale.ApplyScale();
                    check(Mathf.Approximately(scale.Size, slot.size), "Slot mapping " + i);
                    check(Mathf.Approximately(PlayerScaleAdjuster.BodyRadiusOf(player), .5f * slot.size), "Body collider follows global size " + i);
                    check(Mathf.Approximately(reversal.RetainedMomentum, slot.retainedMomentum) && Mathf.Approximately(reversal.BackwardConeDegrees, slot.backwardConeDegrees), "Reversal follows global slot " + i);
                    for (int repeat = 0; repeat < 5; repeat++) scale.ApplyScale();
                    check(Mathf.Approximately(go.transform.localScale.x, slot.size), "Repeated application never compounds " + i);
                    scale.UseGlobalModifiers = false; reversal.UseGlobalModifiers = false;
                    check(Mathf.Approximately(scale.Size, 1.2f) && Mathf.Approximately(go.transform.localScale.x, 1.2f), "Local size fallback " + i);
                    check(Mathf.Approximately(reversal.RetainedMomentum, .9f) && Mathf.Approximately(reversal.BackwardConeDegrees, 45f), "Local reversal fallback " + i);
                    scale.UseGlobalModifiers = true; reversal.UseGlobalModifiers = true;
                    using (var data = new SerializedObject(profile))
                    {
                        data.FindProperty("globalSizeEnabled").boolValue = false;
                        data.FindProperty("globalReversalEnabled").boolValue = false;
                        data.ApplyModifiedPropertiesWithoutUndo();
                    }
                    scale.ApplyScale();
                    check(Mathf.Approximately(scale.Size, 1.2f) && Mathf.Approximately(reversal.RetainedMomentum, .9f), "Master toggles restore locals " + i);
                    using (var data = new SerializedObject(profile))
                    {
                        data.FindProperty("globalSizeEnabled").boolValue = true;
                        data.FindProperty("globalReversalEnabled").boolValue = true;
                        data.ApplyModifiedPropertiesWithoutUndo();
                    }
                    slot.scaleMovement = true; slot.scaleActionReach = false;
                    scale.ApplyScale();
                    check(Mathf.Approximately(PlayerScaleAdjuster.MovementOf(player), slot.size) && PlayerScaleAdjuster.ActionReachOf(player) == 1f, "Global size relationships " + i);
                    using (var data = new SerializedObject(player))
                    {
                        data.FindProperty("isPseudoPlayer").boolValue = true;
                        data.ApplyModifiedPropertiesWithoutUndo();
                    }
                    scale.ApplyScale();
                    check(!scale.UsesGlobalModifiers && !reversal.UsesGlobalModifiers, "Pseudo players excluded " + i);
                    UnityEngine.Object.DestroyImmediate(go);
                }
                check(profile.ForPlayer(-1) == null && profile.ForPlayer(4) == null, "Out-of-range slots excluded");
            }
            finally
            {
                EditorJsonUtility.FromJsonOverwrite(saved, profile);
                AssetDatabase.SaveAssetIfDirty(profile);
                EditorSceneManager.CloseScene(fixture, true);
                SceneManager.SetActiveScene(original);
                PlayerGlobalModifiersEditing.RefreshPlayers();
            }
            return "Global player modifiers: " + passed + " checks passed; project settings restored and temporary scene removed.";
        }
    }
}
