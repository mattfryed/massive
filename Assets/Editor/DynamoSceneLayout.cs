#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Massive.Dynamo;
using Massive.Enemies;
using Massive.Levels;
using Massive.Multiplier;
using Massive.Scoring;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>One-time composition of the existing Dynamo scene, preserving its authored systems.</summary>
public static class DynamoSceneLayout
{
    public const string ScenePath = "Assets/Scenes/S-6_DYNAMO.unity";
    const string LatticePath = "Assets/Scenes/S-1_LATTICE.unity";
    const string StagePath = "Assets/Scripts/Anomalies/Magnetosphere/StageProfile-DYNAMO.asset";
    static readonly string[] Roots = { "00_Systems", "01_Presentation", "02_Arena", "GameplayObjects", "04_UI", "90_EditorOnly" };
    static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();
    static string PathOf(Transform t) => AnimationUtility.CalculateTransformPath(t, null);
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("Dynamo layout: " + message); }
    static Transform At(Scene scene, string path) => All<Transform>(scene).Single(t => PathOf(t) == path);
    static Transform Owner(Object value) => value is GameObject go ? go.transform : (value as Component)?.transform;
    static Transform Group(string name, Transform parent = null)
    {
        var go = new GameObject(name);
        if (parent) go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, "Organize Dynamo");
        return go.transform;
    }
    static void Move(Transform child, Transform parent) => Undo.SetTransformParent(child, parent, "Organize Dynamo");

    [MenuItem("MASSIVE/Dynamo/Apply standard scene layout")]
    public static void ApplyMenu() => Debug.Log(Apply());

    public static string Apply(string evidenceDirectory = null)
    {
        var scene = SceneManager.GetActiveScene();
        Require(!EditorApplication.isPlayingOrWillChangePlaymode && scene.path == ScenePath, "open Dynamo in Edit Mode");
        if (All<LevelSceneContext>(scene).Length != 0)
        {
            WirePlayerStormAdapters(scene, (c, field, value) => NovaLevelSetup.Set(c, field, value));
            string result = Validate(scene);
            if (scene.isDirty) EditorSceneManager.SaveScene(scene);
            return result;
        }
        Require(!scene.isDirty, "save authored changes before composition");
        if (!string.IsNullOrEmpty(evidenceDirectory))
        {
            Directory.CreateDirectory(evidenceDirectory);
            string backup = Path.Combine(evidenceDirectory, "S-6_DYNAMO.before-organization.unity");
            if (!File.Exists(backup)) File.Copy(ScenePath, backup);
        }

        var original = All<Transform>(scene).GroupBy(PathOf).ToDictionary(g => g.Key, g => g.First());
        var poses = All<Transform>(scene).ToDictionary(t => t, t => (t.position, t.rotation, t.lossyScale, t.gameObject.activeSelf));
        var tuning = All<MonoBehaviour>(scene).Where(c => c && !(c is Rewired.Initializer)).ToDictionary(c => c, c => JsonUtility.ToJson(c));
        var references = new List<(Component owner, string property, Object value)>();
        foreach (var c in All<Component>(scene).Where(c => c && !(c is Transform) && !(c is Rewired.Initializer)))
        {
            var p = new SerializedObject(c).GetIterator();
            while (p.Next(true))
                if (p.propertyType == SerializedPropertyType.ObjectReference && !p.propertyPath.StartsWith("m_") && p.objectReferenceValue)
                    references.Add((c, p.propertyPath, p.objectReferenceValue));
        }
        var edited = new HashSet<Component>();
        var changedReferences = new HashSet<string>();
        Action<Component, string, object> set = (c, field, value) =>
        {
            edited.Add(c); changedReferences.Add(c.GetInstanceID() + ":" + field);
            Undo.RecordObject(c, "Connect Dynamo shared systems");
            NovaLevelSetup.Set(c, field, value);
        };
        var gm = All<GameManagerScript>(scene).Single();
        var roster = All<PlayerRosterController>(scene).Single();
        var anomalies = All<AnomalyManager>(scene).Single();
        var bounds = All<ArenaBoundsFromVectorGrid>(scene).Single();
        var grid = bounds.GetComponent<VectorGridGPU>();
        var players = All<PlayerControllerScript>(scene);

        Undo.IncrementCurrentGroup();
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Organize Dynamo and connect shared systems");
        var source = EditorSceneManager.OpenPreviewScene(LatticePath);
        try
        {
            var map = new Dictionary<Object, Object>();
            // Map the existing arena/actors/HUD, never replace them with Lattice versions.
            var prefixes = new Dictionary<string, string>
            {
                { "00_Systems/MatchRuntime/GameManager", "Scene Managers/GameManager" },
                { "00_Systems/MatchRuntime/GameplayBootstrap", "Scene Managers/GameplayBootstrap" },
                { "00_Systems/AnomalyRuntime", "Scene Managers/AnomalyManager" },
                { "01_Presentation/CameraRig/Main Camera", "Main Camera" },
                { "02_Arena/Surface/GRID", "PLAYING FIELD/GRID" },
                { "02_Arena/Goals/TEAM 1 goal", "PLAYING FIELD/TEAM 1 goal" },
                { "02_Arena/Goals/TEAM 2 goal", "PLAYING FIELD/TEAM 2 goal" },
                { "04_UI/MatchHUD", "PLAYING FIELD/UI" },
                { "GameplayObjects/Players", "GameplayObjects/Players" }
            };
            foreach (var entry in prefixes) MapTree(At(source, entry.Key), original[entry.Value], map);

            var systems = Group(Roots[0]); var presentation = Group(Roots[1]);
            var arena = Group(Roots[2]); var ui = Group(Roots[4]);
            var editorOnly = Group(Roots[5]); editorOnly.tag = "EditorOnly";
            var match = Group("MatchRuntime", systems);
            Move(gm.transform, match); Move(roster.transform, match);
            var score = Clone(All<MatchScoreService>(source).Single(), match, map);
            Move(anomalies.transform, systems); anomalies.name = "AnomalyRuntime";
            var context = Clone(All<LevelSceneContext>(source).Single(), systems, map);

            Move(original["Main Camera"], Group("CameraRig", presentation));
            foreach (var listener in All<AudioListener>(scene))
                if (listener.transform != original["Main Camera"]) set(listener, "m_Enabled", false);
            Move(original["Directional Light"], Group("Lighting", presentation));
            Move(original["Scene Managers/SFX Manager"], Group("Audio", presentation));
            Move(original["DEATH SPHERE"], Group("MatchEndFX", presentation));
            Move(original["Post process volume"], Group("PostProcessing", presentation));

            var playingField = original["PLAYING FIELD"].gameObject;
            if (PrefabUtility.IsPartOfPrefabInstance(playingField))
                PrefabUtility.UnpackPrefabInstance(playingField, PrefabUnpackMode.OutermostRoot, InteractionMode.UserAction);
            var hud = original["PLAYING FIELD/UI"];
            Move(hud, ui); hud.name = "MatchHUD";
            Move(original["PLAYING FIELD/GRID"], Group("Surface", arena));
            var collision = Group("BoundsAndCollision", arena);
            Move(original["PLAYING FIELD/Ground Plane"], collision); Move(original["PLAYING FIELD/Mask Planes"], collision);
            var goals = Group("Goals", arena);
            Move(original["PLAYING FIELD/TEAM 1 goal"], goals); Move(original["PLAYING FIELD/TEAM 2 goal"], goals);
            var anchors = Group("SpawnAnchors", arena);
            foreach (var player in players.OrderBy(p => p.playerID))
                Group("P" + (player.playerID + 1), anchors).SetPositionAndRotation(player.transform.position, player.transform.rotation);
            Group("NoSpawnZones", arena);

            var gameplay = original["GameplayObjects"];
            var encounters = Group("Encounters", gameplay);
            var resonance = Clone(All<AmplifierResonanceSpawner>(source).Single(), encounters, map);
            var spawners = Group("Spawners", gameplay);
            map[At(source, "GameplayObjects/Spawners").gameObject] = spawners.gameObject;
            map[At(source, "GameplayObjects/Spawners")] = spawners;
            Move(original["Scene Managers/POWERUP_SPAWNER"], spawners);
            Move(original["GameplayObjects/MatterNuggerSpawner"], spawners);
            var level = Group("DYNAMO", Group("LevelContent", gameplay));
            foreach (string path in new[] { "GameplayObjects/DYNAMO", "MAGNETOSPHERE ANOMALY", "Storm Flow Blanket", "Dynamo Bloom" })
                Move(original[path], level);
            var enemy = Clone(All<EnemyDirector>(source).Single(), level, map);
            enemy.name = "DYNAMO Enemy Timeline";
            var previews = Group("Legacy and scientific previews", level);
            foreach (string path in new[] { "Magnetosphere Controller", "Magnetosphere Viz", "Scientific Dynamo" })
                Move(original[path], previews);
            var runtime = Group("Runtime", gameplay);
            Move(original["GameplayObjects/Power Ups"], runtime);
            Move(original["GameplayObjects/Matter nugget"], runtime);

            var timer = Clone(All<MatchTimerPresenter>(source).Single(), hud, map);
            var feedback = Group("Feedback", ui);
            Move(original["Scene Managers/PlayerIDToastSystem"], feedback);
            var toast = Clone(All<TeamAmplifierToastPresenter>(source).Single(), feedback, map);
            Move(original["ANOMALY CANVAS"], Group("AnomalyUI", ui));
            Move(original["MANAGERS/EventSystem"], ui);
            Move(original["StormHUD"], Group("DYNAMO", Group("LevelUI", ui)));
            var treatment = Undo.AddComponent<AmplifierGoalTreatments>(grid.gameObject);
            var sourceTreatment = All<AmplifierGoalTreatments>(source).Single();
            EditorUtility.CopySerialized(sourceTreatment, treatment); map[sourceTreatment] = treatment;

            foreach (var c in new Component[] { score, context, timer, toast, treatment }
                .Concat(resonance.GetComponentsInChildren<Component>(true)).Concat(enemy.GetComponentsInChildren<Component>(true)))
                Remap(c, source, map);
            treatment.Preview = false; toast.Configure(null, null);

            var definition = AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Scripts/Level Select/LevelDefinition-DYNAMO.asset");
            var stage = AssetDatabase.LoadAssetAtPath<StageProfile>(StagePath);
            if (!stage)
            {
                stage = Object.Instantiate(All<LevelSceneContext>(source).Single().stage);
                stage.name = "StageProfile-DYNAMO"; stage.stageId = "STAGE_" + definition.levelNumber.ToString("000"); stage.displayName = "DYNAMO";
                AssetDatabase.CreateAsset(stage, StagePath);
            }
            definition.gameplayScene.SyncFromAsset(); EditorUtility.SetDirty(definition); AssetDatabase.SaveAssetIfDirty(definition);
            context.level = definition; context.stage = stage; context.spawners = spawners.gameObject;
            context.match = gm; context.roster = roster; context.anomalies = anomalies;
            set(gm, "scoreService", score); set(gm, "scoreEconomyProfile", score.Profile);
            set(gm, "levelName", "DYNAMO"); set(gm, "levelNumber", definition.levelNumber.ToString("000"));
            set(anomalies, "stageProfile", stage); set(anomalies, "match", gm);
            set(anomalies, "playerManager", original["GameplayObjects/Players"].GetComponent<PlayerManager>());
            set(context.stageTitleText, "m_text", "DYNAMO");
            set(context.stageNumberText, "m_text", "STAGE_" + definition.levelNumber.ToString("000"));
            foreach (var goal in All<AmplifierGoalCapture>(scene))
            {
                set(goal, "arenaBounds", bounds); set(goal, "treatments", treatment);
            }
            WirePlayerStormAdapters(scene, set);
            foreach (var c in All<MonoBehaviour>(scene).Where(c => c))
            {
                var p = new SerializedObject(c).FindProperty("scoreService");
                if (p != null && p.propertyType == SerializedPropertyType.ObjectReference) set(c, "scoreService", score);
            }
            // LevelSceneContext instantiates the persistent INPUT PREFAB as its own root.
            // Retaining this initializer would persist all of 00_Systems instead.
            foreach (var initializer in All<Rewired.Initializer>(scene)) Undo.DestroyObjectImmediate(initializer.gameObject);
            foreach (string path in new[] { "PLAYING FIELD", "Scene Managers", "MANAGERS" })
            {
                Require(original[path].childCount == 0 && original[path].GetComponents<Component>().Length == 1, "empty legacy wrapper " + path);
                Undo.DestroyObjectImmediate(original[path].gameObject);
            }
            for (int i = 0; i < Roots.Length; i++) scene.GetRootGameObjects().Single(g => g.name == Roots[i]).transform.SetSiblingIndex(i);

            foreach (var entry in poses.Where(p => p.Key))
            {
                var t = entry.Key; var before = entry.Value;
                Require(Vector3.Distance(t.position, before.position) < .001f && Quaternion.Angle(t.rotation, before.rotation) < .01f &&
                    Vector3.Distance(t.lossyScale, before.lossyScale) < .001f && t.gameObject.activeSelf == before.activeSelf, "preserved world pose/activation: " + PathOf(t));
            }
            foreach (var entry in tuning.Where(p => !edited.Contains(p.Key)))
                Require(entry.Key && JsonUtility.ToJson(entry.Key) == entry.Value, "preserved authored tuning: " + entry.Key);
            foreach (var reference in references)
                if (!changedReferences.Contains(reference.owner.GetInstanceID() + ":" + reference.property))
                    Require(reference.owner && new SerializedObject(reference.owner).FindProperty(reference.property).objectReferenceValue == reference.value,
                        "preserved reference: " + reference.owner + "." + reference.property);
            string report = Validate(scene) + $"\nPreservation: {poses.Count} original poses/activation states; {tuning.Count - edited.OfType<MonoBehaviour>().Count(tuning.ContainsKey)} unchanged component configurations; {references.Count} existing references checked.";
            foreach (var c in All<Component>(scene).Where(c => c && PrefabUtility.IsPartOfPrefabInstance(c))) PrefabUtility.RecordPrefabInstancePropertyModifications(c);
            Undo.CollapseUndoOperations(undo);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            if (!string.IsNullOrEmpty(evidenceDirectory)) File.WriteAllText(Path.Combine(evidenceDirectory, "dynamo-organization-preservation.txt"), report);
            return report;
        }
        finally { EditorSceneManager.ClosePreviewScene(source); }
    }

    static void MapTree(Transform source, Transform target, Dictionary<Object, Object> map)
    {
        foreach (var t in source.GetComponentsInChildren<Transform>(true))
        {
            string relative = AnimationUtility.CalculateTransformPath(t, source);
            var destination = relative.Length == 0 ? target : target.Find(relative);
            if (!destination) continue;
            map[t.gameObject] = destination.gameObject;
            foreach (var group in t.GetComponents<Component>().Where(c => c).GroupBy(c => c.GetType()))
            {
                var from = group.ToArray(); var to = destination.GetComponents(group.Key);
                for (int i = 0; i < Math.Min(from.Length, to.Length); i++) map[from[i]] = to[i];
            }
        }
    }
    static void WirePlayerStormAdapters(Scene scene, Action<Component, string, object> set)
    {
        var storm = All<DynamoStormController>(scene).Single();
        foreach (var push in All<PlayerStormPush>(scene))
        {
            // Inactive roster slots have not run Awake yet. Give every slot an
            // explicit scene-local binding instead of relying on first activation.
            var player = push.GetComponent<PlayerControllerScript>() ?? push.GetComponentInParent<PlayerControllerScript>();
            Require(player, "storm adapter has an actor");
            set(push, "player", player); set(push, "storm", storm);
        }
    }
    static T Clone<T>(T source, Transform parent, Dictionary<Object, Object> map) where T : Component
    {
        var clone = Object.Instantiate(source.gameObject, parent, true); clone.name = source.name;
        Undo.RegisterCreatedObjectUndo(clone, "Add Dynamo shared system");
        MapTree(source.transform, clone.transform, map);
        return clone.GetComponent<T>();
    }
    static void Remap(Component component, Scene source, Dictionary<Object, Object> map)
    {
        if (!component || component is Transform) return;
        var so = new SerializedObject(component); var p = so.GetIterator();
        while (p.Next(true))
        {
            if (p.propertyType != SerializedPropertyType.ObjectReference || p.propertyPath.StartsWith("m_")) continue;
            var value = p.objectReferenceValue; var owner = Owner(value);
            if (!owner || owner.gameObject.scene != source) continue;
            Require(map.TryGetValue(value, out var target), "mapped reference for " + component + "." + p.propertyPath);
            p.objectReferenceValue = target;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    [MenuItem("MASSIVE/Dynamo/Validate saved scene layout")]
    public static void ValidateMenu() => Debug.Log(ValidateSaved());
    public static string ValidateSaved()
    {
        var scene = EditorSceneManager.OpenPreviewScene(ScenePath);
        try { return Validate(scene); }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
    static string Validate(Scene scene)
    {
        Require(scene.GetRootGameObjects().Select(g => g.name).SequenceEqual(Roots), "six standard ordered roots");
        var context = All<LevelSceneContext>(scene).Single();
        var gm = All<GameManagerScript>(scene).Single();
        var score = All<MatchScoreService>(scene).Single();
        var bounds = All<ArenaBoundsFromVectorGrid>(scene).Single();
        var resonance = All<AmplifierResonanceSpawner>(scene).Single();
        var enemy = All<EnemyDirector>(scene).Single();
        var field = All<MagnetosphereFieldLinesGPU2D>(scene).Single();
        var storm = All<DynamoStormController>(scene).Single();
        var flow = All<DynamoFlowBlanketRenderer>(scene).Single();
        var bloom = All<DynamoSelectiveBloom>(scene).Single();
        Require(context.level && context.level.levelTitle == "DYNAMO" && context.level.levelNumber == 5 && context.level.gameplayScene.SceneName == "S-6_DYNAMO", "Dynamo catalog identity and scene destination");
        Require(context.stage && context.stage.displayName == "DYNAMO" && context.anomalies.stageProfile == context.stage, "dedicated stage profile");
        Require(context.match == gm && context.roster && context.anomalies && context.inputManagerPrefab, "complete shared scene context");
        Require(context.spawners == At(scene, "GameplayObjects/Spawners").gameObject, "phase-gated standard spawners");
        Require(context.stageTitleText && context.stageTitleText.gameObject.activeInHierarchy && context.stageNumberText && context.stageNumberText.gameObject.activeInHierarchy, "live stage HUD labels");
        Require(new SerializedObject(gm).FindProperty("scoreService").objectReferenceValue == score && score.Profile, "explicit score authority and economy");
        Require(context.anomalies.match == gm && context.anomalies.playerManager, "anomaly match and player registry");
        Require(All<Rewired.Initializer>(scene).Length == 0, "single input bootstrap owner");
        Require(All<AudioListener>(scene).Count(c => c.enabled && c.gameObject.activeInHierarchy) == 1, "one active audio listener");
        Require(All<MatchTimerPresenter>(scene).Length == 1 && All<TeamAmplifierToastPresenter>(scene).Length == 1, "timer and amplifier HUD");
        Require(resonance.scoreService == score && resonance.spawnRegion.arenaBounds == bounds && resonance.corePrefab && resonance.patternOrder.Count > 0, "resonance and amplifier wired to this arena");
        Require(enemy.arenaBounds == bounds && enemy.anomalyManager == context.anomalies && enemy.encounterTimeline && enemy.spawnProfile && enemy.GetComponent<EnemyArenaLayout>().arena == bounds, "enemy encounter wired to this arena");
        Require(storm.GameplayField == field && flow.controller == storm && flow.field == field && bloom.field == field && bloom.flow == flow && bloom.targetCamera, "original field, storm, blanket and bloom connections");
        Require(field.transform.IsChildOf(At(scene, "GameplayObjects/LevelContent/DYNAMO")), "Dynamo gameplay lifetime group");
        Require(All<PlayerControllerScript>(scene).Length == 4 && All<PlayerStormPush>(scene).Length == 4, "four existing storm-aware actors");
        int inspected = 0;
        foreach (var t in All<Transform>(scene)) Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0, "no missing scripts: " + PathOf(t));
        foreach (var c in All<Component>(scene).Where(c => c))
        {
            var p = new SerializedObject(c).GetIterator();
            while (p.Next(true))
            {
                if (p.propertyType != SerializedPropertyType.ObjectReference || p.propertyPath.StartsWith("m_")) continue;
                inspected++;
                Require(p.objectReferenceValue || p.objectReferenceInstanceIDValue == 0, "no broken reference: " + c + "." + p.propertyPath);
                var owner = Owner(p.objectReferenceValue);
                Require(!owner || !owner.gameObject.scene.IsValid() || owner.gameObject.scene == scene, "no cross-scene reference: " + c + "." + p.propertyPath);
            }
        }
        return "PASSED: standard hierarchy, shared systems, Dynamo identity, original storm wiring, and " + inspected + " serialized reference fields.";
    }
}
#endif
