#if UNITY_EDITOR
using System;
using System.Linq;
using Massive.Demonstrations;
using Massive.Levels;
using Massive.Player;
using Massive.Scoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class PlayerActionTestArenaSetup
{
    public const string ScenePath = "Assets/Scenes/EXPERIMENTS/S-T_PLAYER-ACTIONS.unity";
    const string EconomyPath = "Assets/Scripts/Player/Demonstrations/Action Test Economy.asset";
    const string LevelPath = "Assets/Scripts/Player/Demonstrations/Action Test Level.asset";

    [MenuItem("MASSIVE/Demonstrations/Open Player Action Arena")]
    public static void Open()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (Resources.Load<TMPro.TMP_Settings>("TMP Settings")) EditorSceneManager.OpenScene(ScenePath);
        else
        {
            AssetDatabase.importPackageCompleted += OpenAfterResources;
            PrepareCheckout();
        }
    }

    static void OpenAfterResources(string packageName)
    {
        if (!packageName.Contains("TMP Essential Resources")) return;
        AssetDatabase.importPackageCompleted -= OpenAfterResources;
        EditorSceneManager.OpenScene(ScenePath);
    }
    public static void PrepareCheckoutBatch()
    {
        if (Resources.Load<TMPro.TMP_Settings>("TMP Settings")) { EditorApplication.Exit(0); return; }
        AssetDatabase.importPackageCompleted += package => EditorApplication.delayCall += () => EditorApplication.Exit(0);
        AssetDatabase.importPackageFailed += (package, error) => { Debug.LogError(error); EditorApplication.Exit(1); };
        PrepareCheckout();
    }

    // The repository ignores Assets/TextMesh Pro. A fresh checkout must import
    // the resources bundled with its installed UGUI package before rendering HUD text.
    public static void PrepareCheckout()
    {
        if (Resources.Load<TMPro.TMP_Settings>("TMP Settings")) return;
        var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMPro.TMP_Text).Assembly);
        if (package == null) throw new InvalidOperationException("Cannot locate the installed TextMesh Pro package.");
        AssetDatabase.ImportPackage(System.IO.Path.Combine(package.resolvedPath,
            "Package Resources/TMP Essential Resources.unitypackage"), false);
        AssetDatabase.Refresh();
    }

    // Only creates a new scene; never overwrites authored work or changes the build/catalog.
    public static void Create()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))
            throw new InvalidOperationException("Action arena already exists; refusing to overwrite it.");
        if (!AssetDatabase.CopyAsset("Assets/Scenes/Templates/Level-Planar.unity", ScenePath))
            throw new InvalidOperationException("Could not copy planar arena template.");
        PrepareCheckout(); var scene = EditorSceneManager.OpenScene(ScenePath);
        var context = All<LevelSceneContext>(scene).Single();
        var match = All<GameManagerScript>(scene).Single();
        var roster = All<PlayerRosterController>(scene).Single();
        var score = All<MatchScoreService>(scene).Single();
        var source = AssetDatabase.LoadAssetAtPath<ScoreEconomyProfile>("Assets/Scripts/Scoring/ScoreEconomyProfile.asset");
        if (!source) throw new InvalidOperationException("Missing shared economy profile.");
        var economy = Object.Instantiate(source);
        economy.name = "Action Test Economy";
        AssetDatabase.CreateAsset(economy, EconomyPath);
        Set(economy, "regulationDurationSeconds", 1200f);
        Set(score, "profile", economy);
        Set(match, "scoreEconomyProfile", economy);
        Set(match, "autoReturnOnAllPlayersInactive", false);
        var level = ScriptableObject.CreateInstance<LevelDefinition>();
        level.levelTitle = "ACTION LAB";
        level.levelNumber = 0;
        level.anomalyTypeName = "COMBAT TEST ARENA";
        AssetDatabase.CreateAsset(level, LevelPath);
        var identity = new SerializedObject(level);
        identity.FindProperty("gameplayScene.sceneAsset").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        identity.FindProperty("gameplayScene.sceneName").stringValue = "S-T_PLAYER-ACTIONS";
        identity.ApplyModifiedPropertiesWithoutUndo();
        context.level = level;
        if (context.stageTitleText) context.stageTitleText.text = level.levelTitle;
        if (context.stageNumberText) context.stageNumberText.text = "TEST ARENA";
        // Keep the standard Core / Resonance encounter and goals for real scoring.
        // Random power-up spawning is distracting during repeatable combat tests.
        if (context.spawners) context.spawners.SetActive(false);
        context.spawners = null;
        var root = new GameObject("Player Action Lab");
        var lab = root.AddComponent<PlayerActionTestArena>();
        Set(lab, "roster", roster);
        Set(lab, "match", match);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Validate();
        Debug.Log("PLAYER ACTION ARENA CREATED: " + ScenePath);
    }

    static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    static void Set(Object target, string field, Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }
    static void Set(Object target, string field, float value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    static void Set(Object target, string field, bool value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }
    static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Action arena: " + message); }

    [MenuItem("MASSIVE/Demonstrations/Validate Player Action Arena")]
    public static void Validate()
    {
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool ownsPreview = !scene.IsValid() || !scene.isLoaded;
        if (ownsPreview) scene = EditorSceneManager.OpenPreviewScene(ScenePath);
        try
        {
            var roster = All<PlayerRosterController>(scene).Single();
            var match = All<GameManagerScript>(scene).Single();
            var score = All<MatchScoreService>(scene).Single();
            var context = All<LevelSceneContext>(scene).Single();
            var lab = All<PlayerActionTestArena>(scene).Single();
            Check(context.match == match && context.roster == roster && context.level, "scene composition / identity");
            Check(context.inputManagerPrefab, "direct-launch input bootstrap");
            Check(new SerializedObject(lab).FindProperty("roster").objectReferenceValue == roster, "test driver roster");
            Check(new SerializedObject(lab).FindProperty("match").objectReferenceValue == match, "test driver match");
            Check(All<VectorGridGPU>(scene).Length > 0, "gameplay grid");
            Check(All<ScoreboardManagerScript>(scene).Any(c => c.gameObject.activeInHierarchy) && All<TMPro.TMP_Text>(scene).Length > 0, "world-space scoreboard and text");
            foreach (var go in new[] { roster.P1, roster.P2, roster.P3, roster.P4 })
            {
                Check(go, "explicit player slot");
                var player = go.GetComponent<PlayerControllerScript>();
                Check(player && player.ParticipatesInMatch && player.UsesGameplayTuning, "shared-tuned match actor");
                Check(player.goalZone && player.goalZone.scene == scene, "scene-local goal");
                var anchor = new SerializedObject(player).FindProperty("respawnPointOverride").objectReferenceValue as Transform;
                Check(anchor && anchor.gameObject.scene == scene, "scene-local respawn anchor");
                Check(player.GetComponent<GridInteractor>().grid, "player grid binding");
                Check(player.attackController && player.GetComponent<PlayerShieldAbility>(), "attack and shield");
            }
            var economy = new SerializedObject(score).FindProperty("profile").objectReferenceValue as ScoreEconomyProfile;
            Check(economy && economy.RegulationDurationSeconds == 1200f, "isolated 20-minute test economy");
            Check(new SerializedObject(match).FindProperty("scoreEconomyProfile").objectReferenceValue == economy, "one score profile");
            Check(!new SerializedObject(match).FindProperty("autoReturnOnAllPlayersInactive").boolValue, "idle return disabled locally");
            Debug.Log("PLAYER ACTION ARENA: authored scene bindings passed.");
        }
        finally { if (ownsPreview) EditorSceneManager.ClosePreviewScene(scene); }
    }
}
#endif





