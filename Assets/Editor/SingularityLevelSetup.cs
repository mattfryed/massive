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
using Massive.Singularity;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SingularityLevelSetup
{
    public const string ScenePath = "Assets/Scenes/S-8_SINGULARITY.unity";
    public const string ReferenceScene = "Assets/Scenes/S-1_LATTICE.unity";
    public const string Folder = "Library/SingularityLevelValidation";
    static SingularityLevelSetup() => EditorApplication.delayCall += () =>
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.Exists(Folder + "/inspect.request"))
        { File.Delete(Folder + "/inspect.request"); Inspect(); }
        if (File.Exists(Folder + "/finalize.request"))
        {
            File.Delete(Folder + "/finalize.request");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || scene.isDirty) throw new InvalidOperationException("Finalize the saved SINGULARITY scene in Edit Mode.");
            var definition = All<LevelSceneContext>(scene).Single().level;
            // OnValidate can sync SceneReference in memory without setting the asset dirty.
            EditorUtility.SetDirty(definition); AssetDatabase.SaveAssetIfDirty(definition);
            File.WriteAllText(Folder + "/saved-validation.txt", SingularityLevelValidation.CheckScene(scene));
            SingularityMaskValidation.Run();
        }
        if (File.Exists(Folder + "/install.request"))
        {
            File.Delete(Folder + "/install.request");
            try { Apply(); SingularityLevelValidation.Run(); }
            catch (Exception e) { File.WriteAllText(Folder + "/install.txt", "FAILED\n" + e); Debug.LogException(e); }
        }
    };
    public static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
    static Transform Group(string name, Transform parent = null)
    {
        var go = new GameObject(name); if (parent) go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, "Compose SINGULARITY"); return go.transform;
    }
    static void Move(Transform child, Transform parent) => Undo.SetTransformParent(child, parent, "Compose SINGULARITY");
    static void Set(UnityEngine.Object target, string property, object value) => NovaLevelSetup.Set(target, property, value);
    static void Module(Transform root, string asset)
    {
        if (PrefabUtility.IsPartOfPrefabInstance(root)) PrefabUtility.UnpackPrefabInstance(root.gameObject, PrefabUnpackMode.Completely, InteractionMode.UserAction);
        PrefabUtility.ConvertToPrefabInstance(root.gameObject, AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Levels/" + asset + ".prefab"),
            new ConvertToPrefabInstanceSettings { objectMatchMode = ObjectMatchMode.ByHierarchy, componentsNotMatchedBecomesOverride = true,
                gameObjectsNotMatchedBecomesOverride = true, recordPropertyOverridesOfMatches = true, changeRootNameToAssetName = false }, InteractionMode.UserAction);
    }
    static VectorGridGPU Domain(string name, Transform parent)
    {
        var t = Group(name, parent); t.gameObject.SetActive(false);
        var grid = Undo.AddComponent<VectorGridGPU>(t.gameObject); grid.enabled = false; grid.registerAsDefault = false;
        var bounds = Undo.AddComponent<ArenaBoundsFromVectorGrid>(t.gameObject); Set(bounds, "grid", grid);
        t.GetComponent<MeshRenderer>().enabled = false; t.gameObject.SetActive(true); return grid;
    }
    [MenuItem("MASSIVE/Levels/Apply SINGULARITY Universal Structure")]
    public static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath || scene.isDirty)
            throw new InvalidOperationException("Open the saved SINGULARITY scene in Edit Mode first.");
        if (All<LevelSceneContext>(scene).Any()) throw new InvalidOperationException("SINGULARITY is already composed; edit its module overrides.");
        Directory.CreateDirectory(Folder);
        if (!File.Exists(Folder + "/SINGULARITY-before.unity")) File.Copy(ScenePath, Folder + "/SINGULARITY-before.unity");
        Undo.IncrementCurrentGroup(); Undo.SetCurrentGroupName("Compose SINGULARITY universal level");
        var original = All<Transform>(scene).ToDictionary(t => AnimationUtility.CalculateTransformPath(t, null));
        var surface = All<SingularitySurface>(scene).Single(); var foldedGrid = All<SingularityGridRenderer>(scene).Single();
        var portal = All<SingularityBlackHolePortal>(scene).Single(); var encounter = All<SingularityAmplifierEncounter>(scene).Single();
        var unique = All<MonoBehaviour>(scene).Where(c => c && c.GetType().Namespace == "Massive.Singularity").ToDictionary(c => c, EditorJsonUtility.ToJson);
        var poses = original.Values.ToDictionary(t => t, t => t.localToWorldMatrix);
        var gm = All<GameManagerScript>(scene).Single(); var roster = All<PlayerRosterController>(scene).Single();
        var scores = All<MatchScoreService>(scene).Single(); var oldContext = All<SingularityLevelContext>(scene).Single();
        var timer = All<MatchTimerPresenter>(scene).Single(); var toast = All<TeamAmplifierToastPresenter>(scene).Single();
        var gameplay = original["GameplayObjects"]; var playersRoot = original["GameplayObjects/Players"];
        var systems = Group("00_Systems"); var presentation = Group("01_Presentation"); var arena = Group("02_Arena");
        var ui = Group("04_UI"); var editor = Group("90_EditorOnly"); editor.tag = "EditorOnly";
        var runtime = Group("MatchRuntime", systems);
        Move(gm.transform, runtime); gm.name = "GameManager"; gm.tag = "GameManager";
        Move(scores.transform, runtime); Module(runtime, "MatchRuntime");
        foreach (var duplicate in All<PlayerRosterController>(scene).Where(c => c != roster)) Undo.DestroyObjectImmediate(duplicate);
        var economy = AssetDatabase.LoadAssetAtPath<ScoreEconomyProfile>("Assets/Scripts/Scoring/ScoreEconomyProfile.asset");
        Set(scores, "profile", economy); Set(gm, "scoreService", scores); Set(gm, "scoreEconomyProfile", economy);
        Set(gm, "regulationFinale", null); Set(gm, "disableGameplayOnEnd", true); Set(gm, "engageDeathSphereOnEnd", false);
        Set(gm, "levelName", oldContext.level.levelTitle); Set(gm, "levelNumber", oldContext.level.levelNumber.ToString("000"));
        var context = Undo.AddComponent<LevelSceneContext>(Group("LevelSceneContext", systems).gameObject);
        context.level = oldContext.level; context.stage = oldContext.stage; context.match = gm; context.roster = roster;
        context.inputManagerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Rewired Input Manager.prefab");
        var definitionData = new SerializedObject(context.level);
        var reference = definitionData.FindProperty("gameplayScene");
        reference.FindPropertyRelative("sceneAsset").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        reference.FindPropertyRelative("sceneName").stringValue = Path.GetFileNameWithoutExtension(ScenePath);
        definitionData.ApplyModifiedProperties(); EditorUtility.SetDirty(context.level); AssetDatabase.SaveAssetIfDirty(context.level);
        Undo.RecordObject(context.stage, "Align SINGULARITY stage identity"); context.stage.stageId = "STAGE_" + context.level.levelNumber.ToString("000");
        EditorUtility.SetDirty(context.stage); AssetDatabase.SaveAssetIfDirty(context.stage);
        Move(original["Main Camera"], Group("CameraRig", presentation));
        Move(original["SINGULARITY - Gameplay/Directional Light"], Group("Lighting", presentation));
        Move(original["SINGULARITY - Gameplay/SFX Manager"], Group("Audio", presentation)); Group("MatchEndFX", presentation);
        var surfaceRoot = Group("Surface", arena); Move(surface.transform, surfaceRoot);
        var boundsRoot = Group("BoundsAndCollision", arena);
        var domains = Undo.AddComponent<SingularityArenaDomains>(surfaceRoot.gameObject); domains.surface = surface;
        domains.frontDomain = Domain("Front face placement", boundsRoot);
        domains.projectedDomain = Domain("Projected arena mask", surfaceRoot);
        var frontBounds = domains.frontDomain.GetComponent<ArenaBoundsFromVectorGrid>();
        var mask = Undo.AddComponent<ArenaOutsideMask>(domains.projectedDomain.gameObject);
        Set(mask, "arena", domains.projectedDomain.GetComponent<ArenaBoundsFromVectorGrid>());
        Set(mask, "maskShader", Shader.Find("MASSIVE/Cosmos/ArenaMask"));
        domains.Refresh();
        var goalsRoot = Group("Goals", arena);
        Move(original["SINGULARITY - Gameplay/TEAM 1 goal"], goalsRoot); Move(original["SINGULARITY - Gameplay/TEAM 2 goal"], goalsRoot); Module(goalsRoot, "TeamGoals_Planar");
        var treatment = All<AmplifierGoalTreatments>(scene).Single(); var goals = All<AmplifierGoalCapture>(scene);
        var goalData = new SerializedObject(treatment); var goalList = goalData.FindProperty("goals"); goalList.arraySize = goals.Length;
        for (int i = 0; i < goals.Length; i++) { goalList.GetArrayElementAtIndex(i).objectReferenceValue = goals[i]; goals[i].SetTreatments(treatment); Set(goals[i], "arenaBounds", frontBounds); }
        goalData.ApplyModifiedPropertiesWithoutUndo(); treatment.Preview = false;
        Move(original["GameplayObjects/Players/PLAYER RESPAWNS"], Group("SpawnAnchors", arena));
        var exclusion = encounter.blackHoleExclusion;
        Move(exclusion.transform, Group("NoSpawnZones", arena)); exclusion.gameObject.layer = LayerMask.NameToLayer("NoSpawnZone");
        if (!exclusion.GetComponent<PowerUpNoSpawnZone>()) Undo.AddComponent<PowerUpNoSpawnZone>(exclusion.gameObject);
        Module(playersRoot, "Players_Gameplay");
        foreach (var player in All<PlayerControllerScript>(scene))
        {
            var aoe = player.GetComponentInChildren<PlayerRepulsorAOE>(true);
            Set(aoe, "owner", player); Set(aoe, "attackController", player.GetComponent<PlayerAttackController>()); Set(aoe, "hitbox", aoe.GetComponent<SphereCollider>());
            Set(player.GetComponent<PlayerRepulsorGridPulse>(), "grid", null); Set(player.GetComponent<PlayerRepulsorGridPulse>(), "foldedGrid", foldedGrid);
            Set(roster, "player" + (player.playerID + 1), player.gameObject);
        }
        var encounters = Group("Encounters", gameplay); Move(encounter.transform, encounters); encounter.name = "AmplifierResonance";
        Module(encounter.transform, "AmplifierResonanceEncounter");
        var pair = encounter.spawner; pair.scoreService = scores; pair.waitForScoring = true;
        pair.spawnRegion.noGoColliders = new Collider[] { exclusion }; encounter.RefreshPlacement();
        var spawners = Group("Spawners", gameplay); context.spawners = spawners.gameObject;
        var pickups = Undo.AddComponent<PowerUpSpawner>(Group("PowerUps", spawners).gameObject);
        pickups.vectorGrid = domains.frontDomain; pickups.noSpawnMask = 1 << exclusion.gameObject.layer;
        var content = Group("LevelContent", gameplay); Move(portal.transform, Group("SINGULARITY", content)); Group("Runtime", gameplay);
        var director = AddEnemies(scene, encounters, frontBounds, pair, exclusion); domains.enemies = director.arenaLayout;
        var hud = original["SINGULARITY - Gameplay/UI"]; Move(hud, ui); hud.name = "MatchHUD";
        Move(timer.transform, hud); Module(hud, "MatchHUD");
        foreach (var duplicate in All<MatchTimerPresenter>(scene).Where(c => c != timer)) Undo.DestroyObjectImmediate(duplicate);
        Set(timer, "gameManager", gm); Set(timer, "timerText", original["SINGULARITY - Gameplay/UI/Text Objects/Timer/TIME quant"].GetComponent<TMP_Text>());
        Set(timer, "phaseText", original["SINGULARITY - Gameplay/UI/Text Objects/Timer/TIME title"].GetComponent<TMP_Text>());
        context.stageTitleText = original["SINGULARITY - Gameplay/UI/Text Objects/Stage Title/STAGE TITLE"].GetComponent<TMP_Text>();
        context.stageNumberText = original["SINGULARITY - Gameplay/UI/Text Objects/Stage Title/STAGE NUMBER"].GetComponent<TMP_Text>();
        context.stageTitleText.text = context.level.levelTitle; context.stageNumberText.text = context.stage.stageId;
        var clock = original["SINGULARITY - Gameplay/UI/Text Objects/Timer/TIME quant"].GetComponent<TMP_Text>();
        int seconds = Mathf.CeilToInt(economy.RegulationDurationSeconds); clock.text = (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
        var feedback = Group("Feedback", ui); Move(original["SINGULARITY - Gameplay/PlayerIDToastSystem"], feedback);
        // Preserve the existing toast component and its authored settings on the score service.
        Move(original["SINGULARITY - Gameplay/EventSystem"], ui); Group("SINGULARITY", Group("LevelUI", ui));
        Move(original["Navigation Legend"], editor); Move(original["SINGULARITY - Surface/Player - Surface Probe"], editor);
        Move(original["SINGULARITY - Gameplay/Sphere"], editor);
        // The disabled sandbox session remains recoverable, with its original references.
        Move(original["SINGULARITY - Gameplay"], editor); original["SINGULARITY - Gameplay"].name = "Legacy sandbox session";
        Undo.DestroyObjectImmediate(original["Rewired Input Manager"].gameObject);
        foreach (var c in All<Component>(scene).Where(c => c))
        {
            var p = new SerializedObject(c).FindProperty("scoreService");
            if (p != null && p.propertyType == SerializedPropertyType.ObjectReference) Set(c, "scoreService", scores);
            if (c is TMPTextTransition && c.transform.IsChildOf(ui)) Set(c, "useUnscaledTime", true);
        }
        foreach (var item in unique) if (EditorJsonUtility.ToJson(item.Key) != item.Value) throw new InvalidOperationException("Authored SINGULARITY component changed: " + item.Key.GetType().Name);
        foreach (var item in poses.Where(p => p.Key))
            for (int i = 0; i < 16; i++) if (Mathf.Abs(item.Key.localToWorldMatrix[i] - item.Value[i]) > .001f) throw new InvalidOperationException("Authored world pose changed: " + item.Key.name);
        foreach (var c in All<Component>(scene).Where(c => c)) { EditorUtility.SetDirty(c); if (PrefabUtility.IsPartOfPrefabInstance(c)) PrefabUtility.RecordPrefabInstancePropertyModifications(c); }
        foreach (var t in All<Transform>(scene)) if (PrefabUtility.IsPartOfPrefabInstance(t)) PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
        foreach (var root in new[] { systems, presentation, arena, gameplay, ui, editor }) root.SetAsLastSibling();
        EditorSceneManager.MarkSceneDirty(scene); if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save SINGULARITY.");
        File.WriteAllText(Folder + "/install.txt", "INSTALLED: common systems, modules, front-face enemy/pickup domains and projected mask. Original folded components and world poses preserved.");
    }
    static EnemyDirector AddEnemies(Scene scene, Transform parent, ArenaBoundsFromVectorGrid bounds, AmplifierResonanceSpawner pair, Collider exclusion)
    {
        var reference = EditorSceneManager.OpenPreviewScene(PulsarLevelSetup.ReferenceScene);
        try
        {
            var template = All<EnemyDirector>(reference).Single(d => d.encounterTimeline && d.arenaLayout);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(PrefabUtility.GetCorrespondingObjectFromSource(template.gameObject), scene);
            Undo.RegisterCreatedObjectUndo(root, "Add SINGULARITY enemy timeline"); root.name = "SINGULARITY Enemy Timeline"; root.transform.SetParent(parent, false);
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
            director.anomalyManager = null; director.resonanceSpawner = pair; director.placementRegion = null; director.restrictTargetsToArena = true;
            director.waitForScoring = true; director.previewCue = -1; director.enabled = true; root.SetActive(true);
            root.GetComponent<AmplifierSpawnRegion>().arenaBounds = bounds; return director;
        }
        finally { EditorSceneManager.ClosePreviewScene(reference); }
    }
    public static void Inspect()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) throw new InvalidOperationException("Open SINGULARITY first.");
        Directory.CreateDirectory(Folder);
        var lines = All<Transform>(scene).Select(t => AnimationUtility.CalculateTransformPath(t, null) +
            " | active=" + t.gameObject.activeSelf + " | pos=" + t.position + " | " +
            string.Join(", ", t.GetComponents<MonoBehaviour>().Where(c => c).Select(c => c.GetType().FullName)));
        File.WriteAllLines(Folder + "/hierarchy-before.txt", new[] { "Scene dirty=" + scene.isDirty }.Concat(lines));
        File.WriteAllText(Folder + "/bindings-before.txt", string.Join("\n\n", All<MonoBehaviour>(scene).Where(c => c &&
            (c.GetType().Namespace?.Contains("Singularity") == true || new[] { "GameManagerScript", "MatchScoreService", "PlayerRosterController", "AmplifierResonanceSpawner", "AmplifierSpawnRegion", "PowerUpSpawner", "EnemyDirector", "GameMusicManager", "AnomalyManager", "RewiredInitializer", "MatchTimerPresenter", "AmplifierGoalCapture", "AmplifierGoalTreatments" }.Contains(c.GetType().Name)))
            .Select(c => AnimationUtility.CalculateTransformPath(c.transform, null) + " " + c.GetType().FullName + "\n" + EditorJsonUtility.ToJson(c, true))));
        File.WriteAllText(Folder + "/console-baseline.txt", string.Join("\n", All<Transform>(scene).Where(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0).Select(t=>t.name)));
        Debug.Log("SINGULARITY scene inventory captured.");
    }
}
#endif
