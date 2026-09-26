#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Massive.Levels;
using Massive.Multiplier;
using Massive.Player;
using Massive.Scoring;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Explicit migration onto the common planar modules, retaining HIGGS objects and overrides.</summary>
public static class HiggsLevelSetup
{
    public const string ScenePath = "Assets/Scenes/S-9_HIGGS.unity";
    private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    private static Transform Group(string name, Transform parent = null)
    {
        var go = new GameObject(name);
        if (parent) go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, "Compose HIGGS level");
        return go.transform;
    }
    private static void Move(Transform child, Transform parent) =>
        Undo.SetTransformParent(child, parent, "Compose HIGGS level");
    private static void Set(Object target, string property, Object value) => NovaLevelSetup.Set(target, property, value);
    private static void SetReferences(Object target, string property, IEnumerable<Object> values)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(property);
        var array = values.ToArray();
        p.arraySize = array.Length;
        for (int i = 0; i < array.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = array[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }
    private static void Module(Transform root, string asset)
    {
        if (PrefabUtility.IsPartOfPrefabInstance(root))
            PrefabUtility.UnpackPrefabInstance(root.gameObject, PrefabUnpackMode.Completely, InteractionMode.UserAction);
        PrefabUtility.ConvertToPrefabInstance(root.gameObject,
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Levels/" + asset + ".prefab"),
            new ConvertToPrefabInstanceSettings
            {
                objectMatchMode = ObjectMatchMode.ByHierarchy,
                componentsNotMatchedBecomesOverride = true,
                gameObjectsNotMatchedBecomesOverride = true,
                recordPropertyOverridesOfMatches = true,
                changeRootNameToAssetName = false
            }, InteractionMode.UserAction);
    }

    [MenuItem("MASSIVE/Levels/Apply HIGGS Universal Structure")]
    public static string Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != ScenePath || scene.isDirty)
            throw new InvalidOperationException("Open the saved HIGGS scene outside Play Mode first.");
        if (All<LevelSceneContext>(scene).Any())
            throw new InvalidOperationException("HIGGS is already composed. Edit its module overrides instead of rerunning migration.");
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Compose HIGGS universal level");
        var original = All<Transform>(scene).ToDictionary(t => AnimationUtility.CalculateTransformPath(t, null));
        var field = All<HiggsFieldGPU>(scene).Single();
        var knotManager = All<HiggsExcitationKnotManager>(scene).Single();
        var mouths = All<SymmetryKnotGoalMouth>(scene);
        var encounter = All<AmplifierResonanceSpawner>(scene).Single();
        var preserved = new Component[] { field, knotManager }.Concat(mouths)
            .Concat(All<AmplifierSpawnRegion>(scene)).ToArray();
        var snapshots = preserved.ToDictionary(c => c, EditorJsonUtility.ToJson);
        var positions = original.Values.Where(t => t.IsChildOf(original["HIGGS"]) || t == original["HIGGS"])
            .ToDictionary(t => t, t => t.localToWorldMatrix);
        var gm = All<GameManagerScript>(scene).Single();
        var roster = All<PlayerRosterController>(scene).Single();
        var anomaly = All<AnomalyManager>(scene).Single();
        var bounds = All<ArenaBoundsFromVectorGrid>(scene).Single();
        var grid = bounds.GetComponent<VectorGridGPU>();
        var players = original["GameplayObjects/Players"];

        // Reparent existing objects first, so old scene bindings and file IDs survive.
        var systems = Group("00_Systems");
        var presentation = Group("01_Presentation");
        var arena = Group("02_Arena");
        var ui = Group("04_UI");
        var editorOnly = Group("90_EditorOnly"); editorOnly.tag = "EditorOnly";
        var runtime = Group("MatchRuntime", systems);
        Move(gm.transform, runtime); Move(roster.transform, runtime);
        Module(runtime, "MatchRuntime");
        var scores = runtime.GetComponentInChildren<MatchScoreService>(true);
        var economy = AssetDatabase.LoadAssetAtPath<ScoreEconomyProfile>("Assets/Scripts/Scoring/ScoreEconomyProfile.asset");
        Set(scores, "profile", economy); Set(gm, "scoreService", scores); Set(gm, "scoreEconomyProfile", economy);
        Set(gm, "regulationFinale", null); // HIGGS keeps its ordinary timed ending, with no NOVA bonus.
        NovaLevelSetup.Set(gm, "postGameLoadDelaySeconds", 2.5f);
        NovaLevelSetup.Set(gm, "engageDeathSphereOnEnd", false);
        Move(anomaly.transform, systems); anomaly.name = "AnomalyRuntime"; anomaly.match = gm;
        var context = Group("LevelSceneContext", systems).gameObject.AddComponent<LevelSceneContext>();
        context.level = AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Scripts/Level Select/LevelDefinition-HIGGS.asset");
        context.match = gm; context.roster = roster; context.anomalies = anomaly; context.stage = anomaly.stageProfile;
        context.inputManagerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Rewired Input Manager.prefab");

        Move(original["Main Camera"], Group("CameraRig", presentation));
        Move(original["GameplayObjects/Directional Light"], Group("Lighting", presentation));
        var audio = Group("Audio", presentation);
        Move(original["Scene Managers/SFX Manager"], audio); Move(original["Scene Managers/GameMusicManager"], audio);
        Move(original["DEATH SPHERE"], Group("MatchEndFX", presentation));
        original["DEATH SPHERE"].gameObject.SetActive(false);

        PrefabUtility.UnpackPrefabInstance(original["PLAYING FIELD"].gameObject, PrefabUnpackMode.OutermostRoot, InteractionMode.UserAction);
        var surface = Group("Surface", arena); Move(original["PLAYING FIELD/GRID"], surface);
        Module(surface, "Arena_Planar_Surface");
        var collision = Group("BoundsAndCollision", arena);
        Move(original["PLAYING FIELD/Ground Plane"], collision); Move(original["PLAYING FIELD/Mask Planes"], collision);
        Module(collision, "Arena_Planar_Bounds");
        var goalRoot = Group("Goals", arena);
        Move(original["PLAYING FIELD/TEAM 1 goal"], goalRoot); Move(original["PLAYING FIELD/TEAM 2 goal"], goalRoot);
        Module(goalRoot, "TeamGoals_Planar");
        var goals = All<AmplifierGoalCapture>(scene);
        var treatment = grid.GetComponent<AmplifierGoalTreatments>();
        Set(treatment, "grid", grid); SetReferences(treatment, "goals", goals); treatment.Preview = false;
        foreach (var goal in goals) { goal.SetTreatments(treatment); Set(goal, "arenaBounds", bounds); }
        Group("NoSpawnZones", arena); // HIGGS has no NOVA star exclusion.

        var gameplay = original["GameplayObjects"];
        Module(players, "Players_Gameplay");
        var spawnAnchors = Group("SpawnAnchors", arena);
        foreach (var player in players.GetComponentsInChildren<PlayerControllerScript>(true))
        {
            var anchor = Group("P" + (player.playerID + 1), spawnAnchors);
            anchor.SetPositionAndRotation(player.transform.position, player.transform.rotation);
            var aoe = player.GetComponentInChildren<PlayerRepulsorAOE>(true);
            Set(aoe, "owner", player); Set(aoe, "attackController", player.GetComponent<PlayerAttackController>());
            Set(aoe, "hitbox", aoe.GetComponent<SphereCollider>());
            Set(player.GetComponent<PlayerRepulsorGridPulse>(), "grid", grid);
        }
        var rosterData = new SerializedObject(roster);
        foreach (var player in players.GetComponentsInChildren<PlayerControllerScript>(true))
            rosterData.FindProperty("player" + (player.playerID + 1)).objectReferenceValue = player.gameObject;
        rosterData.ApplyModifiedPropertiesWithoutUndo();
        var encounters = Group("Encounters", gameplay);
        Move(encounter.transform, encounters); encounter.name = "AmplifierResonance";
        Module(encounter.transform, "AmplifierResonanceEncounter");
        encounter.scoreService = scores;
        encounter.patternAnchor = encounter.transform.Find("Pattern anchor");
        var spawners = Group("Spawners", gameplay); context.spawners = spawners.gameObject;
        Move(original["Scene Managers/POWERUP_SPAWNER"], spawners);
        Move(original["GameplayObjects/MatterNuggerSpawner"], spawners);
        original["Scene Managers/POWERUP_SPAWNER"].GetComponent<Massive.PowerUps.PowerUpSpawner>().vectorGrid = grid;
        Move(original["GameplayObjects/Power Ups"], Group("Runtime", gameplay));
        Move(original["HIGGS"], Group("LevelContent", gameplay));
        Move(original["HIGGS/SymmetryKnotSpawnTest"], Group("HIGGS", editorOnly));
        Move(original["GameplayObjects/Matter nugget"], editorOnly);
        Move(original["MANAGERS/Rewired Initializer"], editorOnly);

        var hud = original["PLAYING FIELD/UI"]; Move(hud, ui); hud.name = "MatchHUD";
        Module(hud, "MatchHUD");
        var timer = hud.GetComponentInChildren<MatchTimerPresenter>(true);
        Set(timer, "gameManager", gm);
        Set(timer, "timerText", original["PLAYING FIELD/UI/Text Objects/Timer/TIME quant"].GetComponent<TMP_Text>());
        Set(timer, "phaseText", original["PLAYING FIELD/UI/Text Objects/Timer/TIME title"].GetComponent<TMP_Text>());
        context.stageTitleText = original["PLAYING FIELD/UI/Text Objects/Stage Title/STAGE TITLE"].GetComponent<TMP_Text>();
        context.stageNumberText = original["PLAYING FIELD/UI/Text Objects/Stage Title/STAGE NUMBER"].GetComponent<TMP_Text>();
        context.stageTitleText.text = context.level.levelTitle;
        context.stageNumberText.text = "STAGE_" + context.level.levelNumber.ToString("000");
        var feedback = Group("Feedback", ui);
        Move(original["Scene Managers/PlayerIDToastSystem"], feedback);
        Group("TeamAmplifierToast", feedback).gameObject.AddComponent<TeamAmplifierToastPresenter>();
        var anomalyUI = Group("AnomalyUI", ui); Move(original["ANOMALY CANVAS"], anomalyUI);
        Module(anomalyUI, "AnomalyUI");
        anomaly.ui = anomalyUI.GetComponentInChildren<AnomalyUIController>(true);
        anomaly.playerManager = players.GetComponent<PlayerManager>();
        Move(original["MANAGERS/EventSystem"], ui);
        Group("HIGGS", Group("LevelUI", ui));
        foreach (var c in All<Component>(scene).Where(c => c))
        {
            var p = new SerializedObject(c).FindProperty("scoreService");
            if (p != null && p.propertyType == SerializedPropertyType.ObjectReference) Set(c, "scoreService", scores);
            if (c is TMPTextTransition && c.transform.IsChildOf(ui)) NovaLevelSetup.Set(c, "useUnscaledTime", true);
        }
        foreach (string empty in new[] { "PLAYING FIELD", "Scene Managers", "MANAGERS" })
        {
            if (original[empty].childCount != 0) throw new InvalidOperationException("Unmigrated children in " + empty);
            Undo.DestroyObjectImmediate(original[empty].gameObject);
        }
        foreach (var item in snapshots)
            if (EditorJsonUtility.ToJson(item.Key) != item.Value)
                throw new InvalidOperationException("HIGGS-specific configuration changed on " + item.Key.name);
        foreach (var item in positions)
            for (int i = 0; i < 16; i++)
                if (Mathf.Abs(item.Key.localToWorldMatrix[i] - item.Value[i]) > .001f)
                    throw new InvalidOperationException("HIGGS-specific transform changed on " + item.Key.name);
        foreach (var c in All<Component>(scene).Where(c => c))
            if (PrefabUtility.IsPartOfPrefabInstance(c)) PrefabUtility.RecordPrefabInstancePropertyModifications(c);
        foreach (var root in new[] { systems, presentation, arena, gameplay, ui, editorOnly }) root.SetAsLastSibling();
        HiggsLevelValidation.CheckScene(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "HIGGS composed with eight shared prefab modules; field, Knots, mouths and both placement regions preserved.";
    }
}
#endif
