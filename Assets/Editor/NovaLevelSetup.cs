#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Massive.Levels;
using Massive.Multiplier;
using Massive.Scoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>One-time, explicit migration plus reusable prefab/template export.
/// Never runs on import. Existing authored transforms and cross-module bindings are preserved.</summary>
public static class NovaLevelSetup
{
    public const string NovaPath = "Assets/Scenes/S-3_NOVA.unity";
    public const string TemplatePath = "Assets/Scenes/Templates/Level-Planar.unity";
    private const string Prefabs = "Assets/Prefabs/Levels";

    private static IEnumerable<T> All<T>(Scene s) where T : Component =>
        s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true));
    private static Transform At(Scene s, string path) => All<Transform>(s)
        .First(t => AnimationUtility.CalculateTransformPath(t, null) == path);
    private static GameObject Group(string name, Transform parent = null)
    {
        var go = new GameObject(name);
        if (parent) go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, "Compose NOVA level");
        return go;
    }
    private static void Move(Transform child, Transform parent) => Undo.SetTransformParent(child, parent, "Compose NOVA level");
    public static void Set(Object target, string name, object value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(name);
        if (p == null) throw new InvalidOperationException(target.GetType().Name + "." + name + " missing");
        if (value is bool b) p.boolValue = b;
        else if (value is int i) p.intValue = i;
        else if (value is long l) p.longValue = l;
        else if (value is float f) p.floatValue = f;
        else if (value is string text) p.stringValue = text;
        else p.objectReferenceValue = value as Object;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int split = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, split));
        AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
    }
    private static Transform Owner(Object value) => value is GameObject go ? go.transform : (value as Component)?.transform;

    // CopySerialized keeps references to the source scene. Resolve those references
    // by the original hierarchy paths before reorganising NOVA.
    private static void Remap(Component target, Scene source, Dictionary<string, Transform> destination)
    {
        var so = new SerializedObject(target);
        var p = so.GetIterator();
        while (p.Next(true))
        {
            if (p.propertyType != SerializedPropertyType.ObjectReference) continue;
            var value = p.objectReferenceValue;
            var owner = Owner(value);
            if (!owner || owner.gameObject.scene != source) continue;
            string path = AnimationUtility.CalculateTransformPath(owner, null);
            if (!destination.TryGetValue(path, out var mapped)) { p.objectReferenceValue = null; continue; }
            p.objectReferenceValue = value is GameObject ? mapped.gameObject : mapped.GetComponent(value.GetType());
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static T Copy<T>(T source, GameObject target, Scene sourceScene, Dictionary<string, Transform> map) where T : Component
    {
        var result = target.GetComponent<T>() ?? target.AddComponent<T>();
        EditorUtility.CopySerialized(source, result);
        Remap(result, sourceScene, map);
        return result;
    }

    [MenuItem("MASSIVE/Levels/Apply NOVA Universal Structure")]
    public static string Apply()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != NovaPath) throw new InvalidOperationException("Open NOVA before applying the migration.");
        if (All<LevelSceneContext>(scene).Any()) throw new InvalidOperationException("NOVA is already composed; use its prefab and scene overrides for subsequent edits.");
        if (scene.isDirty) throw new InvalidOperationException("Save the authored scene before migration.");
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Compose NOVA universal level");
        var original = All<Transform>(scene).ToDictionary(t => AnimationUtility.CalculateTransformPath(t, null));
        var gm = All<GameManagerScript>(scene).Single();
        var roster = All<PlayerRosterController>(scene).Single();
        var anomalies = All<AnomalyManager>(scene).Single();
        var star = All<NovaStarController>(scene).Single();
        var bounds = All<ArenaBoundsFromVectorGrid>(scene).Single();
        var grid = bounds.GetComponent<VectorGridGPU>();
        var players = original["GameplayObjects/Players"];
        var goals = All<AmplifierGoalCapture>(scene).ToArray();
        var source = EditorSceneManager.OpenPreviewScene("Assets/Scenes/S-6_ORBITAL.unity");
        GameObject encounter;
        try
        {
            // Carry verified common combat additions into NOVA without replacing
            // its player objects, roster references, spawn positions, or HUD.
            foreach (var player in players.GetComponentsInChildren<PlayerControllerScript>(true))
            {
                var from = All<PlayerControllerScript>(source).Single(p => p.playerID == player.playerID);
                var sourceAOE = from.transform.Find("RepulsorAOE");
                var aoe = Object.Instantiate(sourceAOE.gameObject, player.transform, false);
                aoe.name = sourceAOE.name;
                original[AnimationUtility.CalculateTransformPath(sourceAOE, null)] = aoe.transform;
                foreach (var c in aoe.GetComponentsInChildren<Component>(true)) Remap(c, source, original);
                var repulsor = aoe.GetComponent<Massive.Player.PlayerRepulsorAOE>();
                Set(repulsor, "owner", player);
                Set(repulsor, "attackController", player.GetComponent<Massive.Player.PlayerAttackController>());
                Set(repulsor, "hitbox", aoe.GetComponent<SphereCollider>());
                foreach (string typeName in new[]{"PlayerScaleAdjuster", "PlayerMovementReversal", "PlayerRepulsorFeedback", "PlayerRepulsorGridPulse"})
                {
                    var component = from.GetComponents<Component>().Single(c => c.GetType().Name == typeName);
                    var added = player.GetComponent(component.GetType()) ?? player.gameObject.AddComponent(component.GetType());
                    EditorUtility.CopySerialized(component, added);
                    Remap(added, source, original);
                    if (typeName == "PlayerRepulsorGridPulse") Set(added, "grid", grid);
                }
            }
            var treatment = Copy(All<AmplifierGoalTreatments>(source).Single(), grid.gameObject, source, original);
            treatment.Preview = false;
            foreach (var goal in goals)
            {
                goal.SetTreatments(treatment);
                Set(goal, "arenaBounds", bounds);
            }
            var sourceEncounter = All<AmplifierResonanceSpawner>(source).Single();
            encounter = Object.Instantiate(sourceEncounter.gameObject);
            encounter.name = "AmplifierResonance";
            SceneManager.MoveGameObjectToScene(encounter, scene);
            // Internal clone references already point to the cloned region/anchor.
            foreach (var c in encounter.GetComponentsInChildren<Component>(true)) Remap(c, source, original);
        }
        finally { EditorSceneManager.ClosePreviewScene(source); }

        var systems = Group("00_Systems").transform;
        var presentation = Group("01_Presentation").transform;
        var arena = Group("02_Arena").transform;
        var ui = Group("04_UI").transform;
        var editorOnly = Group("90_EditorOnly"); editorOnly.tag = "EditorOnly";
        var runtime = Group("MatchRuntime", systems).transform;
        Move(gm.transform, runtime); Move(roster.transform, runtime);
        var score = Group("MatchScoreService", runtime).AddComponent<MatchScoreService>();
        var profile = AssetDatabase.LoadAssetAtPath<ScoreEconomyProfile>("Assets/Scripts/Scoring/ScoreEconomyProfile.asset");
        InstallReward(profile);
        Set(score, "profile", profile);
        Set(gm, "scoreService", score); Set(gm, "scoreEconomyProfile", profile);
        Set(gm, "postGameLoadDelaySeconds", 2.5f);
        Set(gm, "engageDeathSphereOnEnd", false);
        Move(anomalies.transform, systems); anomalies.gameObject.name = "AnomalyRuntime"; anomalies.match = gm;
        Object.DestroyImmediate(anomalies.GetComponent<NovaScoreSphereBridge>());
        Object.DestroyImmediate(anomalies.GetComponent<NovaCoreRewardsSink>());
        var context = Group("LevelSceneContext", systems).AddComponent<LevelSceneContext>();
        context.level = AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Scripts/Level Select/LevelDefinition-NOVA.asset");
        context.stage = anomalies.stageProfile; context.match = gm; context.roster = roster; context.anomalies = anomalies;
        context.inputManagerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Rewired Input Manager.prefab");
        context.stageTitleText = original["PLAYING FIELD/UI/Text Objects/Stage Title/STAGE TITLE"].GetComponent<TMPro.TMP_Text>();
        context.stageNumberText = original["PLAYING FIELD/UI/Text Objects/Stage Title/STAGE NUMBER"].GetComponent<TMPro.TMP_Text>();
        context.stageTitleText.text = context.level.levelTitle;
        context.stageNumberText.text = "STAGE_" + context.level.levelNumber.ToString("000");
        star.match = gm;

        Move(original["Main Camera"], Group("CameraRig", presentation).transform);
        Move(original["Directional Light"], Group("Lighting", presentation).transform);
        var audio = Group("Audio", presentation).transform;
        Move(original["Scene Managers/SFX Manager"], audio);
        Move(original["Scene Managers/GameMusicManager"], audio);
        var endFX = Group("MatchEndFX", presentation).transform;
        Move(original["DEATH SPHERE"], endFX);
        original["DEATH SPHERE"].gameObject.SetActive(false);
        var sequence = All<SupernovaEndSequence>(scene).Single();
        Move(sequence.transform, endFX); sequence.match = gm;

        var playingField = original["PLAYING FIELD"].gameObject;
        if (PrefabUtility.IsPartOfPrefabInstance(playingField))
            PrefabUtility.UnpackPrefabInstance(playingField, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
        Move(original["PLAYING FIELD/UI"], ui); original["PLAYING FIELD/UI"].name = "MatchHUD";
        Move(original["PLAYING FIELD/GRID"], Group("Surface", arena).transform);
        var collision = Group("BoundsAndCollision", arena).transform;
        Move(original["PLAYING FIELD/Ground Plane"], collision); Move(original["PLAYING FIELD/Mask Planes"], collision);
        var goalRoot = Group("Goals", arena).transform;
        Move(original["PLAYING FIELD/TEAM 1 goal"], goalRoot); Move(original["PLAYING FIELD/TEAM 2 goal"], goalRoot);
        var anchors = Group("SpawnAnchors", arena).transform;
        foreach (var p in players.GetComponentsInChildren<PlayerControllerScript>(true))
        { var anchor = Group("P" + (p.playerID + 1), anchors).transform; anchor.SetPositionAndRotation(p.transform.position, p.transform.rotation); }
        var noGo = Group("NoSpawnZones", arena).transform;
        var zone = Group("NOVA Star Exclusion", noGo).AddComponent<SphereCollider>();
        var entry = (SphereCollider)star.entryCollider;
        zone.transform.position = entry.transform.TransformPoint(entry.center);
        zone.radius = entry.radius * Mathf.Max(Mathf.Abs(entry.transform.lossyScale.x), Mathf.Abs(entry.transform.lossyScale.y), Mathf.Abs(entry.transform.lossyScale.z));
        zone.isTrigger = true; zone.gameObject.layer = LayerMask.NameToLayer("NoSpawnZone");

        var gameplay = original["GameplayObjects"]; gameplay.tag = "GameplayObjects";
        Move(encounter.transform, Group("Encounters", gameplay).transform);
        var region = encounter.GetComponent<AmplifierSpawnRegion>();
        var coordinator = encounter.GetComponent<AmplifierResonanceSpawner>();
        region.arenaBounds = bounds; region.noGoColliders = new Collider[]{zone}; region.previewPattern = null;
        coordinator.spawnRegion = region; coordinator.scoreService = score; coordinator.comparison = null;
        coordinator.standaloneExamples = Array.Empty<GameObject>();
        if (!coordinator.patternAnchor)
        { var anchor = Group("PatternAnchor", encounter.transform).transform; anchor.SetPositionAndRotation(grid.transform.position, grid.transform.rotation); coordinator.patternAnchor = anchor; }
        var spawners = Group("Spawners", gameplay).transform; context.spawners = spawners.gameObject;
        Move(original["Scene Managers/POWERUP_SPAWNER"], spawners);
        Move(original["GameplayObjects/MatterNuggerSpawner"], spawners);
        var powerups = original["Scene Managers/POWERUP_SPAWNER"].GetComponent<Massive.PowerUps.PowerUpSpawner>();
        powerups.vectorGrid = grid; powerups.noSpawnMask |= 1 << zone.gameObject.layer;
        var feature = Group("NOVA", Group("LevelContent", gameplay).transform).transform;
        Move(star.transform, feature); Move(original["NovaAnomalyAdapter"], feature);
        Move(original["GameplayObjects/Power Ups"], Group("Runtime", gameplay).transform);
        Move(original["GameplayObjects/Matter nugget"], editorOnly.transform);

        var feedback = Group("Feedback", ui).transform;
        Move(original["Scene Managers/PlayerIDToastSystem"], feedback);
        var toast = Group("TeamAmplifierToast", feedback).AddComponent<TeamAmplifierToastPresenter>();
        Move(original["ANOMALY CANVAS"], Group("AnomalyUI", ui).transform);
        var levelUI = Group("NOVA", Group("LevelUI", ui).transform).transform;
        Move(original["NovaCorePostResultsPanel"], levelUI); Move(original["NovaBounceProgress"], levelUI);
        Move(original["MANAGERS/EventSystem"], ui);
        var timer = Group("MatchTimerPresenter", original["PLAYING FIELD/UI"]).AddComponent<MatchTimerPresenter>();
        Set(timer, "gameManager", gm);
        Set(timer, "timerText", original["PLAYING FIELD/UI/Text Objects/Timer/TIME quant"].GetComponent<TMPro.TMP_Text>());
        Set(timer, "phaseText", original["PLAYING FIELD/UI/Text Objects/Timer/TIME title"].GetComponent<TMPro.TMP_Text>());
        foreach (var c in All<Component>(scene).Where(c => c))
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty("scoreService");
            if (p != null && p.propertyType == SerializedPropertyType.ObjectReference) Set(c, "scoreService", score);
            if (c is TMPTextTransition && c.transform.IsChildOf(ui)) Set(c, "useUnscaledTime", true);
        }
        Object.DestroyImmediate(playingField);
        Object.DestroyImmediate(original["Scene Managers"].gameObject);
        Object.DestroyImmediate(original["MANAGERS"].gameObject);
        foreach (var c in All<Component>(scene).Where(c => c))
            if (PrefabUtility.IsPartOfPrefabInstance(c)) PrefabUtility.RecordPrefabInstancePropertyModifications(c);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "NOVA hierarchy and universal integrations applied. Export prefabs after validation.";
    }

    public static void InstallReward(ScoreEconomyProfile profile)
    {
        var so = new SerializedObject(profile);
        var rewards = so.FindProperty("rewards");
        bool found = profile.Rewards.Any(r => r.key == ScoreRewardKeys.NovaCoreCapture);
        if (!found)
        {
            int index = rewards.arraySize++; var row = rewards.GetArrayElementAtIndex(index);
            row.FindPropertyRelative("key").stringValue = ScoreRewardKeys.NovaCoreCapture;
            row.FindPropertyRelative("enabled").boolValue = true;
            row.FindPropertyRelative("amount").longValue = 25;
            row.FindPropertyRelative("unit").intValue = (int)EnergyUnit.MilliElectronVolt;
            row.FindPropertyRelative("multiplierEligible").boolValue = false;
            row.FindPropertyRelative("ignoreAllMultipliers").boolValue = true;
            row.FindPropertyRelative("bonusOnly").boolValue = true;
            row.FindPropertyRelative("chainEffect").intValue = (int)ScoreChainAwardMode.None;
            row.FindPropertyRelative("chainCharge").floatValue = 0f;
            row.FindPropertyRelative("repeatPolicy").intValue = (int)ScoreRepeatPolicy.OncePerSourceToken;
            row.FindPropertyRelative("feedbackId").stringValue = "NOVA_CAPTURE";
            row.FindPropertyRelative("expectedOccurrencesPerRound").intValue = 20;
            so.FindProperty("rulesetVersion").intValue = Math.Max(4, profile.RulesetVersion + 1);
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
    }

    private struct Binding { public Component component; public string path; public Object value; }
    private static void SaveModule(GameObject root, string file)
    {
        var bindings = new List<Binding>();
        foreach (var c in root.GetComponentsInChildren<Component>(true).Where(c => c))
        {
            var p = new SerializedObject(c).GetIterator();
            while (p.Next(true))
            {
                if (p.propertyType != SerializedPropertyType.ObjectReference || p.propertyPath.StartsWith("m_")) continue;
                var owner = Owner(p.objectReferenceValue);
                if (owner && owner.gameObject.scene.IsValid() && !owner.IsChildOf(root.transform))
                    bindings.Add(new Binding { component=c, path=p.propertyPath, value=p.objectReferenceValue });
            }
        }
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, file, InteractionMode.AutomatedAction, out bool success);
        if (!success) throw new InvalidOperationException("Could not save " + file);
        foreach (var b in bindings) Set(b.component, b.path, b.value);
    }

    [MenuItem("MASSIVE/Levels/Export NOVA Modules and Planar Template")]
    public static string Export()
    {
        var scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != NovaPath || !All<LevelSceneContext>(scene).Any())
            throw new InvalidOperationException("Open migrated NOVA in Edit Mode first.");
        EnsureFolder(Prefabs); EnsureFolder("Assets/Scenes/Templates");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TemplatePath))
            throw new InvalidOperationException("Template already exists; preserved it and its modules rather than overwrite authored changes.");
        foreach (var pair in new[]{
            new[]{"00_Systems/MatchRuntime","MatchRuntime"},
            new[]{"GameplayObjects/Players","Players_Gameplay"},
            new[]{"02_Arena/Surface","Arena_Planar_Surface"},
            new[]{"02_Arena/BoundsAndCollision","Arena_Planar_Bounds"},
            new[]{"02_Arena/Goals","TeamGoals_Planar"},
            new[]{"04_UI/MatchHUD","MatchHUD"},
            new[]{"GameplayObjects/Encounters/AmplifierResonance","AmplifierResonanceEncounter"},
            new[]{"GameplayObjects/LevelContent/NOVA","NOVA_Feature"},
            new[]{"04_UI/AnomalyUI","AnomalyUI"}})
            SaveModule(At(scene,pair[0]).gameObject, Prefabs + "/" + pair[1] + ".prefab");
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        if (!AssetDatabase.CopyAsset(NovaPath,TemplatePath)) throw new InvalidOperationException("Template copy failed.");
        var template = EditorSceneManager.OpenScene(TemplatePath, OpenSceneMode.Additive);
        try
        {
            var ctx = All<LevelSceneContext>(template).Single();
            ctx.level=null; ctx.stage=null; ctx.anomalies=null;
            var region=All<AmplifierSpawnRegion>(template).Single(); region.noGoColliders=Array.Empty<Collider>();
            foreach (string path in new[]{"00_Systems/AnomalyRuntime","GameplayObjects/LevelContent/NOVA","04_UI/LevelUI/NOVA","04_UI/AnomalyUI","02_Arena/NoSpawnZones/NOVA Star Exclusion","01_Presentation/MatchEndFX/SUPERNOVA Sequence Root","GameplayObjects/Spawners/MatterNuggerSpawner"})
                Object.DestroyImmediate(At(template,path).gameObject);
            foreach (var label in All<TMPro.TMP_Text>(template))
            { if (label.name=="STAGE TITLE") label.text="LEVEL TITLE"; if(label.name=="STAGE NUMBER") label.text="000"; }
            foreach(var c in All<Component>(template).Where(c=>c))
                if(PrefabUtility.IsPartOfPrefabInstance(c)) PrefabUtility.RecordPrefabInstancePropertyModifications(c);
            EditorSceneManager.MarkSceneDirty(template); EditorSceneManager.SaveScene(template);
        }
        finally { EditorSceneManager.CloseScene(template,true); SceneManager.SetActiveScene(scene); }
        AssetDatabase.SaveAssets();
        return "Exported nine reusable modules and Level-Planar starter scene. NOVA remains open.";
    }
}
#endif
