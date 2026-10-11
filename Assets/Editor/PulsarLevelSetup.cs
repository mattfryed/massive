#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Massive.Enemies;
using Massive.Levels;
using Massive.Multiplier;
using Massive.Player;
using Massive.PowerUps;
using Massive.Scoring;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PulsarLevelSetup
{
    public const string ReferenceScene = "Assets/Scenes/S-1_LATTICE.unity";
    public const string ScenePath = "Assets/Scenes/S-5_PULSAR.unity", Folder = "Library/PulsarLevelValidation";
    static PulsarLevelSetup() => EditorApplication.delayCall += () =>
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.Exists(Folder + "/inspect.request")) { File.Delete(Folder + "/inspect.request"); Inspect(); }
        if (File.Exists(Folder + "/mask.request"))
        {
            File.Delete(Folder + "/mask.request");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) throw new InvalidOperationException("Open PULSAR first.");
            PulsarLevelValidation.CheckScene(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save PULSAR.");
            ArenaOutsideMaskValidation.Run(); return;
        }
        if (File.Exists(Folder + "/bindings.request"))
        {
            File.Delete(Folder + "/bindings.request");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) throw new InvalidOperationException("Open PULSAR first.");
            CompleteBindings(scene); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            PulsarLevelValidation.Run(); return;
        }
        if (!File.Exists(Folder + "/install.request")) return;
        File.Delete(Folder + "/install.request");
        try { Apply(); PulsarLevelValidation.Run(); }
        catch (Exception e) { File.WriteAllText(Folder + "/install.txt", "FAILED\n" + e); Debug.LogException(e); }
    };
    public static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
    static Transform Group(string name, Transform parent = null)
    {
        var go = new GameObject(name); if (parent) go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, "Compose PULSAR level"); return go.transform;
    }
    static void Move(Transform child, Transform parent) => Undo.SetTransformParent(child, parent, "Compose PULSAR level");
    static void Set(Object target, string property, object value) => NovaLevelSetup.Set(target, property, value);
    static void Module(Transform root, string asset)
    {
        if (PrefabUtility.IsPartOfPrefabInstance(root))
            PrefabUtility.UnpackPrefabInstance(root.gameObject, PrefabUnpackMode.Completely, InteractionMode.UserAction);
        PrefabUtility.ConvertToPrefabInstance(root.gameObject,
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Levels/" + asset + ".prefab"),
            new ConvertToPrefabInstanceSettings { objectMatchMode = ObjectMatchMode.ByHierarchy,
                componentsNotMatchedBecomesOverride = true, gameObjectsNotMatchedBecomesOverride = true,
                recordPropertyOverridesOfMatches = true, changeRootNameToAssetName = false }, InteractionMode.UserAction);
    }

    [MenuItem("MASSIVE/Levels/Apply PULSAR Universal Structure")]
    public static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath || scene.isDirty)
            throw new InvalidOperationException("Open the saved PULSAR scene in Edit Mode first.");
        if (All<LevelSceneContext>(scene).Any()) throw new InvalidOperationException("PULSAR is already composed; edit its module overrides.");
        Directory.CreateDirectory(Folder);
        if (!File.Exists(Folder + "/PULSAR-before.unity")) File.Copy(ScenePath, Folder + "/PULSAR-before.unity");
        Undo.IncrementCurrentGroup(); Undo.SetCurrentGroupName("Compose PULSAR universal level");
        var original = All<Transform>(scene).ToDictionary(t => AnimationUtility.CalculateTransformPath(t, null));
        var pulsar = original["GameplayObjects/PULSAR"];
        var prototype = original["PULSAR SYSTEM"];
        var poses = pulsar.GetComponentsInChildren<Transform>(true).Concat(prototype.GetComponentsInChildren<Transform>(true))
            .ToDictionary(t => t, t => (matrix: t.localToWorldMatrix, active: t.gameObject.activeSelf));
        var preserved = pulsar.GetComponentsInChildren<Component>(true).Where(c => c && !(c is Transform))
            .ToDictionary(c => c, EditorJsonUtility.ToJson);
        var gm = All<GameManagerScript>(scene).Single(); var roster = All<PlayerRosterController>(scene).Single();
        var anomaly = All<AnomalyManager>(scene).Single(); var bounds = All<ArenaBoundsFromVectorGrid>(scene).Single();
        var grid = bounds.GetComponent<VectorGridGPU>(); var players = original["GameplayObjects/Players"];
        var systems = Group("00_Systems"); var presentation = Group("01_Presentation"); var arena = Group("02_Arena");
        var ui = Group("04_UI"); var editor = Group("90_EditorOnly"); editor.tag = "EditorOnly";
        var runtime = Group("MatchRuntime", systems); Move(gm.transform, runtime); Move(roster.transform, runtime); Module(runtime, "MatchRuntime");
        var scores = runtime.GetComponentInChildren<MatchScoreService>(true);
        var economy = AssetDatabase.LoadAssetAtPath<ScoreEconomyProfile>("Assets/Scripts/Scoring/ScoreEconomyProfile.asset");
        Set(scores, "profile", economy); Set(gm, "scoreService", scores); Set(gm, "scoreEconomyProfile", economy);
        Set(gm, "regulationFinale", null); Set(gm, "disableGameplayOnEnd", true);
        Set(gm, "levelName", "PULSAR"); Set(gm, "levelNumber", "004");
        // Preserve PULSAR's existing death-sphere ending outside the gameplay shutdown root.
        Move(anomaly.transform, systems); anomaly.name = "AnomalyRuntime"; anomaly.match = gm;
        const string stagePath = "Assets/Scripts/Anomalies/PULSAR/StageProfile-PULSAR.asset";
        var stage = AssetDatabase.LoadAssetAtPath<StageProfile>(stagePath);
        if (!stage) { stage = ScriptableObject.CreateInstance<StageProfile>(); stage.stageId = "STAGE_004"; stage.displayName = "PULSAR"; AssetDatabase.CreateAsset(stage, stagePath); }
        anomaly.stageProfile = stage;
        var context = Undo.AddComponent<LevelSceneContext>(Group("LevelSceneContext", systems).gameObject);
        context.level = AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Scripts/Level Select/LevelDefinition-PULSAR.asset");
        context.match = gm; context.roster = roster; context.anomalies = anomaly; context.stage = stage;
        context.inputManagerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Rewired Input Manager.prefab");
        Move(original["Main Camera"], Group("CameraRig", presentation)); Move(original["Directional Light"], Group("Lighting", presentation));
        Move(original["Scene Managers/SFX Manager"], Group("Audio", presentation));
        Move(original["DEATH SPHERE"], Group("MatchEndFX", presentation));

        PrefabUtility.UnpackPrefabInstance(original["PLAYING FIELD"].gameObject, PrefabUnpackMode.OutermostRoot, InteractionMode.UserAction);
        var surface = Group("Surface", arena); Move(original["PLAYING FIELD/GRID"], surface); Module(surface, "Arena_Planar_Surface");
        var collision = Group("BoundsAndCollision", arena); Move(original["PLAYING FIELD/Ground Plane"], collision);
        Move(original["PLAYING FIELD/Mask Planes"], collision); Module(collision, "Arena_Planar_Bounds");
        var goalRoot = Group("Goals", arena); Move(original["PLAYING FIELD/TEAM 1 goal"], goalRoot);
        Move(original["PLAYING FIELD/TEAM 2 goal"], goalRoot); Module(goalRoot, "TeamGoals_Planar");
        var treatment = grid.GetComponent<AmplifierGoalTreatments>(); Set(treatment, "grid", grid); treatment.Preview = false;
        var goals = All<AmplifierGoalCapture>(scene);
        var goalData = new SerializedObject(treatment); var list = goalData.FindProperty("goals"); list.arraySize = goals.Length;
        for (int i = 0; i < goals.Length; i++) { list.GetArrayElementAtIndex(i).objectReferenceValue = goals[i]; goals[i].SetTreatments(treatment); Set(goals[i], "arenaBounds", bounds); }
        goalData.ApplyModifiedPropertiesWithoutUndo();
        var zoneRoot = Group("NoSpawnZones", arena);
        var starZone = Group("PULSAR exclusion", zoneRoot); starZone.gameObject.layer = LayerMask.NameToLayer("NoSpawnZone");
        var exclusion = Undo.AddComponent<SphereCollider>(starZone.gameObject); exclusion.isTrigger = true;
        Undo.AddComponent<PowerUpNoSpawnZone>(starZone.gameObject);

        var gameplay = original["GameplayObjects"]; Module(players, "Players_Gameplay");
        var anchors = Group("SpawnAnchors", arena);
        foreach (var player in players.GetComponentsInChildren<PlayerControllerScript>(true))
        {
            var anchor = Group("P" + (player.playerID + 1), anchors); anchor.SetPositionAndRotation(player.transform.position, player.transform.rotation);
            var aoe = player.GetComponentInChildren<PlayerRepulsorAOE>(true);
            Set(aoe, "owner", player); Set(aoe, "attackController", player.GetComponent<PlayerAttackController>()); Set(aoe, "hitbox", aoe.GetComponent<SphereCollider>());
            Set(player.GetComponent<PlayerRepulsorGridPulse>(), "grid", grid); Set(roster, "player" + (player.playerID + 1), player.gameObject);
        }
        var encounters = Group("Encounters", gameplay);
        var pairRoot = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Levels/AmplifierResonanceEncounter.prefab"), encounters);
        Undo.RegisterCreatedObjectUndo(pairRoot, "Add PULSAR encounter"); pairRoot.name = "AmplifierResonance";
        var pair = pairRoot.GetComponent<AmplifierResonanceSpawner>(); pair.scoreService = scores;
        pair.spawnRegion.arenaBounds = bounds; pair.spawnRegion.noGoColliders = new Collider[] { exclusion };
        var spawners = Group("Spawners", gameplay); context.spawners = spawners.gameObject;
        Move(original["Scene Managers/POWERUP_SPAWNER"], spawners);
        var powerups = spawners.GetComponentInChildren<PowerUpSpawner>(); powerups.vectorGrid = grid; powerups.noSpawnMask = 1 << starZone.gameObject.layer;
        Move(original["GameplayObjects/Power Ups"], Group("Runtime", gameplay));
        var content = Group("LevelContent", gameplay); Move(pulsar, content); Move(prototype, content);
        var session = prototype.GetComponentInChildren<PulsarRhythmSession>(true);
        var rhythm = prototype.GetComponentInChildren<Dypsloom.RhythmTimeline.Core.Managers.RhythmDirector>(true);
        Set(session, "rhythmDirector", rhythm); Set(session, "playableDirector", rhythm.GetComponent<PlayableDirector>()); Set(session, "pulsarCenter", pulsar);
        AddEnemies(scene, encounters, bounds, pair, anomaly, exclusion);
        Move(original["GameplayObjects/Matter nugget"], editor);
        Undo.DestroyObjectImmediate(original["MANAGERS/Rewired Initializer"].gameObject);

        var hud = original["PLAYING FIELD/UI"]; Move(hud, ui); hud.name = "MatchHUD";
        // Use the visible PULSAR title as the module's title, retaining its authored pose.
        var texts = original["PLAYING FIELD/UI/Text Objects"];
        var oldTitle = texts.Find("Stage Title");
        if (oldTitle) { Move(oldTitle, editor); oldTitle.name = "Legacy Stage Title"; oldTitle.gameObject.SetActive(false); }
        Move(original["Stage Title"], texts); Module(hud, "MatchHUD");
        var timer = hud.GetComponentInChildren<MatchTimerPresenter>(true); Set(timer, "gameManager", gm);
        Set(timer, "timerText", original["PLAYING FIELD/UI/Text Objects/Timer/TIME quant"].GetComponent<TMP_Text>());
        Set(timer, "phaseText", original["PLAYING FIELD/UI/Text Objects/Timer/TIME title"].GetComponent<TMP_Text>());
        context.stageTitleText = original["Stage Title/STAGE TITLE"].GetComponent<TMP_Text>();
        context.stageNumberText = original["Stage Title/STAGE NUMBER"].GetComponent<TMP_Text>();
        context.stageTitleText.text = "PULSAR"; context.stageNumberText.text = "STAGE_004";
        var feedback = Group("Feedback", ui); Move(original["Scene Managers/PlayerIDToastSystem"], feedback);
        Undo.AddComponent<TeamAmplifierToastPresenter>(Group("TeamAmplifierToast", feedback).gameObject);
        var anomalyUI = Group("AnomalyUI", ui); Move(original["ANOMALY CANVAS"], anomalyUI); Module(anomalyUI, "AnomalyUI");
        anomaly.ui = anomalyUI.GetComponentInChildren<AnomalyUIController>(true); anomaly.playerManager = players.GetComponent<PlayerManager>();
        Move(original["MANAGERS/EventSystem"], ui); Group("PULSAR", Group("LevelUI", ui));
        foreach (var c in All<Component>(scene).Where(c => c))
        {
            var p = new SerializedObject(c).FindProperty("scoreService");
            if (p != null && p.propertyType == SerializedPropertyType.ObjectReference) Set(c, "scoreService", scores);
            if (c is TMPTextTransition && c.transform.IsChildOf(ui)) Set(c, "useUnscaledTime", true);
        }
        foreach (string empty in new[] { "PLAYING FIELD", "Scene Managers", "MANAGERS" })
        { if (original[empty].childCount != 0) throw new InvalidOperationException("Unmigrated children in " + empty); Undo.DestroyObjectImmediate(original[empty].gameObject); }
        foreach (var item in preserved) if (EditorJsonUtility.ToJson(item.Key) != item.Value) throw new InvalidOperationException("PULSAR authored component changed: " + item.Key.name);
        foreach (var item in poses)
        {
            if (item.Key.gameObject.activeSelf != item.Value.active) throw new InvalidOperationException("PULSAR active state changed: " + item.Key.name);
            for (int i = 0; i < 16; i++) if (Mathf.Abs(item.Key.localToWorldMatrix[i] - item.Value.matrix[i]) > .001f) throw new InvalidOperationException("PULSAR pose changed: " + item.Key.name);
        }
        CompleteBindings(scene);
        foreach (var c in All<Component>(scene).Where(c => c)) { EditorUtility.SetDirty(c); if (PrefabUtility.IsPartOfPrefabInstance(c)) PrefabUtility.RecordPrefabInstancePropertyModifications(c); }
        foreach (var t in All<Transform>(scene)) if (PrefabUtility.IsPartOfPrefabInstance(t)) PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
        foreach (var root in new[] { systems, presentation, arena, gameplay, ui, editor }) root.SetAsLastSibling();
        string report = PulsarLevelValidation.CheckScene(scene);
        EditorSceneManager.MarkSceneDirty(scene); if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save PULSAR.");
        AssetDatabase.SaveAssets();
        File.WriteAllText(Folder + "/install.txt", "INSTALLED\n" + report + "\nPULSAR authored geometry, components and prototype poses preserved.\nStar exclusion radius: " + exclusion.radius);
    }

    static void CompleteBindings(Scene scene)
    {
        var grid = All<VectorGridGPU>(scene).Single();
        foreach (var force in All<GridForceSource>(scene)) Set(force, "grid", grid);
        var pulsar = All<AttractScript>(scene).Single().transform;
        // Particle bounds can be empty outside Play Mode. Use the stable physical core.
        var core = pulsar.GetComponentsInChildren<SphereCollider>(true).Where(c => c.enabled && !c.isTrigger && c.gameObject.activeInHierarchy).OrderByDescending(c => c.radius * Mathf.Max(Mathf.Abs(c.transform.lossyScale.x), Mathf.Abs(c.transform.lossyScale.y), Mathf.Abs(c.transform.lossyScale.z))).First();
        var exclusion = All<PowerUpNoSpawnZone>(scene).Single().GetComponent<SphereCollider>();
        Undo.RecordObjects(new Object[] { exclusion, exclusion.transform }, "Bind PULSAR spawn exclusion");
        var center = core.transform.TransformPoint(core.center);
        exclusion.transform.position = new Vector3(center.x, 0, center.z);
        var scale = core.transform.lossyScale;
        exclusion.radius = core.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)) + .35f;
    }

    static void AddEnemies(Scene scene, Transform parent, ArenaBoundsFromVectorGrid bounds, AmplifierResonanceSpawner pair, AnomalyManager anomaly, Collider exclusion)
    {
        var reference = EditorSceneManager.OpenPreviewScene(ReferenceScene);
        try
        {
            var template = All<EnemyDirector>(reference).Single(d => d.encounterTimeline && d.arenaLayout);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(PrefabUtility.GetCorrespondingObjectFromSource(template.gameObject), scene);
            Undo.RegisterCreatedObjectUndo(root, "Add PULSAR enemy timeline"); root.name = "PULSAR Enemy Timeline"; root.transform.SetParent(parent, false);
            var director = root.GetComponent<EnemyDirector>(); EditorUtility.CopySerialized(template, director);
            var layout = Undo.AddComponent<EnemyArenaLayout>(root); EditorUtility.CopySerialized(template.arenaLayout, layout);
            layout.arena = bounds; layout.exclusions = new[] { exclusion }; layout.spawnOverlapRoots = Array.Empty<Transform>();
            foreach (var socket in layout.sockets)
            {
                socket.support = null;
                if (socket.id != "TopLeft" && socket.id != "TopRight" && socket.id != "BottomLeft" && socket.id != "BottomRight") continue;
                float x = socket.id.EndsWith("Left") ? -1 : 1, y = socket.id.StartsWith("Top") ? 1 : -1;
                socket.useRange = true; socket.rangeStart = new Vector2(0, y); socket.rangeEnd = new Vector2(x, y);
            }
            foreach (string side in new[] { "Left", "Right" }) foreach (string vertical in new[] { "Top", "Bottom" })
            {
                string id = "Side" + side + vertical; if (layout.sockets.Any(s => s.id == id)) continue;
                layout.sockets.Add(new EnemyArenaLayout.Socket { id = id, enabled = true, position = new Vector2(side == "Left" ? -1 : 1, vertical == "Top" ? .75f : -.75f),
                    inward = side == "Left" ? Vector2.right : Vector2.left, aimArc = 75, clearanceOffset = 1.12f });
            }
            director.enemyRoot = Group("Timeline enemies", root.transform); director.arenaLayout = layout; director.arenaBounds = bounds;
            director.anomalyManager = anomaly; director.resonanceSpawner = pair; director.placementRegion = null;
            director.waitForScoring = true; director.previewCue = -1; director.enabled = true; root.SetActive(true);
            root.GetComponent<AmplifierSpawnRegion>().arenaBounds = bounds;
        }
        finally { EditorSceneManager.ClosePreviewScene(reference); }
    }
    public static void Inspect()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) throw new InvalidOperationException("Open PULSAR first.");
        Directory.CreateDirectory(Folder);
        var lines = All<Transform>(scene).Select(t =>
        {
            var path = AnimationUtility.CalculateTransformPath(t, null);
            var scripts = t.GetComponents<MonoBehaviour>().Where(c => c).Select(c => c.GetType().FullName).ToArray();
            return path.Count(c => c == '/') <= 2 || scripts.Any(s => s.Contains("Pulsar") || s.Contains("Enemy") || s.Contains("Amplifier"))
                ? path + " | active=" + t.gameObject.activeSelf + " | " + string.Join(", ", scripts) : null;
        }).Where(s => s != null);
        File.WriteAllLines(Folder + "/hierarchy-before.txt", lines);
        var types = new[] { "GameManagerScript", "PlayerRosterController", "AnomalyManager", "PulsarRhythmSession", "AmplifierResonanceSpawner", "AmplifierSpawnRegion", "EnemyDirector", "PowerUpSpawner", "GameMusicManager", "RewiredInitializer", "MatchScoreService", "MatchTimerPresenter" };
        File.WriteAllText(Folder + "/bindings-before.txt", string.Join("\n\n", All<MonoBehaviour>(scene).Where(c => c && types.Contains(c.GetType().Name))
            .Select(c => AnimationUtility.CalculateTransformPath(c.transform, null) + " " + c.GetType().FullName + "\n" + EditorJsonUtility.ToJson(c, true))));
        Debug.Log("PULSAR scene inventory captured.");
    }
}
#endif
