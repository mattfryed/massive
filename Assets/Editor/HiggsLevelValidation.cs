#if UNITY_EDITOR
using System;
using System.Linq;
using Massive.Levels;
using Massive.Multiplier;
using Massive.Player;
using Massive.Scoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class HiggsLevelValidation
{
    private static int checks;
    private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("HIGGS composition: " + message); checks++; }
    private static Object Ref(Object owner, string property) => new SerializedObject(owner).FindProperty(property).objectReferenceValue;
    private static Transform Owner(Object obj) => obj is GameObject go ? go.transform : (obj as Component)?.transform;

    public static string CheckScene(Scene scene)
    {
        checks = 0;
        string[] names = { "00_Systems", "01_Presentation", "02_Arena", "GameplayObjects", "04_UI", "90_EditorOnly" };
        Check(scene.GetRootGameObjects().Select(g => g.name).SequenceEqual(names), "six ordered universal roots");
        var all = All<Transform>(scene);
        var context = All<LevelSceneContext>(scene).Single();
        var score = All<MatchScoreService>(scene).Single();
        Check(context.level && context.level.levelTitle == "HIGGS" && context.match && context.roster && context.inputManagerPrefab, "HIGGS identity and bootstrap");
        Check(context.stageTitleText && context.stageTitleText.text == "HIGGS" && context.stageNumberText.text == "STAGE_002", "HIGGS title and number");
        Check(Ref(context.match, "scoreService") == score && Ref(score, "profile") != null, "single explicit score authority");
        Check(Ref(context.match, "regulationFinale") == null && All<NovaStarController>(scene).Length == 0 && All<SupernovaEndSequence>(scene).Length == 0, "no NOVA finale or star");
        Check(context.spawners && context.spawners.transform.parent.CompareTag("GameplayObjects"), "phase-gated spawners");
        foreach (var pair in new[] {
            new[]{"00_Systems/MatchRuntime","MatchRuntime"},
            new[]{"GameplayObjects/Players","Players_Gameplay"},
            new[]{"02_Arena/Surface","Arena_Planar_Surface"},
            new[]{"02_Arena/BoundsAndCollision","Arena_Planar_Bounds"},
            new[]{"02_Arena/Goals","TeamGoals_Planar"},
            new[]{"GameplayObjects/Encounters/AmplifierResonance","AmplifierResonanceEncounter"},
            new[]{"04_UI/MatchHUD","MatchHUD"}, new[]{"04_UI/AnomalyUI","AnomalyUI"} })
        {
            var root = all.Single(t => AnimationUtility.CalculateTransformPath(t, null) == pair[0]);
            Check(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root.gameObject) == "Assets/Prefabs/Levels/" + pair[1] + ".prefab", "module " + pair[1]);
        }
        foreach (var player in All<PlayerControllerScript>(scene))
        {
            var aoe = player.GetComponentInChildren<PlayerRepulsorAOE>(true);
            Check(aoe && player.GetComponent<PlayerScaleAdjuster>() && player.GetComponent<PlayerMovementReversal>() && player.GetComponent<PlayerRepulsorFeedback>(), "common player additions " + player.name);
            Check(Ref(aoe, "owner") == player && Ref(aoe, "attackController") == player.GetComponent<PlayerAttackController>() && Ref(aoe, "hitbox") == aoe.GetComponent<SphereCollider>(), "Repulsor bindings " + player.name);
            Check(Ref(player.GetComponent<PlayerRepulsorGridPulse>(), "grid") == All<VectorGridGPU>(scene).Single(), "Repulsor grid " + player.name);
        }
        Check(All<PlayerControllerScript>(scene).Length == 4 && All<MatchTimerPresenter>(scene).Length == 1 && All<TeamAmplifierToastPresenter>(scene).Length == 1, "roster and common feedback counts");
        Check(Ref(All<MatchTimerPresenter>(scene).Single(), "gameManager") == context.match, "timer authority");
        var manager = All<HiggsExcitationKnotManager>(scene).Single();
        var field = All<HiggsFieldGPU>(scene).Single();
        var encounter = All<AmplifierResonanceSpawner>(scene).Single();
        Check(Ref(manager, "higgsField") == field && Ref(manager, "amplifierEncounter") == encounter && Ref(manager, "knotPrefab"), "HIGGS field/Knot/encounter bindings");
        Check(Ref(manager, "spawnRegion") == manager.GetComponent<AmplifierSpawnRegion>(), "HIGGS placement owner");
        Check(field.WorldSizeXZ == new Vector2(28, 12), "HIGGS field extent");
        Check(All<SymmetryKnotGoalMouth>(scene).Select(m => m.teamID).OrderBy(id => id).SequenceEqual(new[] { 1, 2 }), "both unique goal mouths");
        Check(encounter.patternAnchor && encounter.spawnRegion.arenaBounds && encounter.scoreService == score && encounter.UseSharedSettings, "shared encounter bindings");
        Check(All<AmplifierGoalTreatments>(scene).Length == 1 && All<AmplifierGoalCapture>(scene).Length == 2, "shared goal presentation");
        Check(all.Single(t => t.name == "SymmetryKnotSpawnTest").GetComponentInParent<Transform>().root.CompareTag("EditorOnly"), "HIGGS fixture preserved outside builds");
        Check(all.All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), "no missing scripts");
        int references = 0;
        foreach (var c in All<Component>(scene).Where(c => c))
        {
            var p = new SerializedObject(c).GetIterator();
            while (p.Next(true))
            {
                if (p.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (!p.objectReferenceValue && p.objectReferenceInstanceIDValue != 0)
                    throw new InvalidOperationException("Broken reference " + c.name + "." + p.propertyPath);
                var owner = Owner(p.objectReferenceValue);
                if (owner && owner.gameObject.scene.IsValid() && owner.gameObject.scene != scene)
                    throw new InvalidOperationException("Cross-scene reference " + c.name + "." + p.propertyPath);
                references++;
            }
        }
        checks++;
        return checks + " composition checks passed; " + references + " serialized reference slots inspected.";
    }

    [MenuItem("MASSIVE/Levels/Validate HIGGS Universal Structure")]
    public static string Authored()
    {
        var scene = EditorSceneManager.OpenPreviewScene(HiggsLevelSetup.ScenePath);
        try { return CheckScene(scene); }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    public static string Runtime()
    {
        checks = 0;
        var context = Object.FindFirstObjectByType<LevelSceneContext>();
        Check(Application.isPlaying && context && context.level.levelTitle == "HIGGS", "HIGGS Play Mode");
        Check(Rewired.ReInput.isReady && Object.FindObjectsByType<Rewired.InputManager>(FindObjectsSortMode.None).Length == 1, "one ready input manager after direct launch");
        Check(context.match.Phase == MatchRuntimePhase.Regulation && context.match.ScoreService.IsScoringOpen, "direct launch reaches regulation");
        Check(context.roster.IsRosterReady && Object.FindObjectsByType<PlayerControllerScript>(FindObjectsSortMode.None).Length == (GameFlowContext.Instance.IsTwoVTwo ? 4 : 2), "selected roster");
        Check(Object.FindObjectsByType<MatchScoreService>(FindObjectsSortMode.None).Length == 1 && context.match.RegulationDurationSeconds == 120, "one score authority and production clock");
        Check(context.spawners.activeInHierarchy && context.stageTitleText.text == "HIGGS", "spawner and title setup");
        var timer = Object.FindFirstObjectByType<MatchTimerPresenter>();
        var text = (TMPro.TMP_Text)Ref(timer, "timerText");
        Check(text && !string.IsNullOrWhiteSpace(text.text), "visible timer digits");
        var manager = Object.FindFirstObjectByType<HiggsExcitationKnotManager>();
        Check(manager && manager.OpportunitiesSpawned > 0, "HIGGS opportunity after migration");
        var encounter = Object.FindFirstObjectByType<AmplifierResonanceSpawner>();
        Check(encounter && encounter.PairsSpawned > 0, "shared Core/Resonance opportunity");
        return checks + " direct-launch runtime checks passed in " + GameFlowContext.Instance.Mode + ".";
    }
}
#endif
