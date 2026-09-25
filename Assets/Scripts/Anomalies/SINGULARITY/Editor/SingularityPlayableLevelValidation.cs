#if UNITY_EDITOR
using System;
using System.Linq;
using Massive.Multiplier;
using Massive.Scoring;
using Massive.Singularity;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Read-only saved-asset validation in disposable preview scenes.
/// Match timing, controller input and actual loading are separate Play Mode checks.</summary>
public static class SingularityPlayableLevelValidation
{
    public static string LastReport { get; private set; } = "Not run";

    [MenuItem("MASSIVE/SINGULARITY/Validate Playable Level Integration")]
    public static void RunMenu() { Debug.Log(Run()); }

    public static string Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Validate saved SINGULARITY assets in Edit Mode after compilation.");
        int checks = 0;
        Action<bool, string> check = (ok, label) =>
        { if (!ok) throw new InvalidOperationException("SINGULARITY playable level: " + label); checks++; };
        var def = AssetDatabase.LoadAssetAtPath<LevelDefinition>(SingularityPlayableLevelSetup.DefinitionPath);
        var reference = AssetDatabase.LoadAssetAtPath<LevelDefinition>(OrbitalLevelSetup.DefinitionPath);
        check(def != null && reference != null, "both new and reference level definitions exist");
        check(def.levelNumber == 6 && def.levelTitle == "SINGULARITY" && def.anomalyTypeName == "FOLDED SPACETIME", "standard stage identity is configured");
        check(def.SceneName == "S-7_SINGULARITY", "definition resolves the production scene, not the sandbox");
        check(def.iconPrefab != null && AssetDatabase.GetAssetPath(def.iconPrefab) == SingularityPlayableLevelSetup.IconPath, "definition uses the authored SINGULARITY icon");
        check(def.instructionsPanelPrefab != null && AssetDatabase.GetAssetPath(def.instructionsPanelPrefab) == SingularityPlayableLevelSetup.InstructionsPath, "dedicated instructions resolve");
        check(def.audioProfile != null && def.audioProfile == reference.audioProfile, "level uses the standard ORBITAL audio profile");
        var definitions = AssetDatabase.FindAssets("t:LevelDefinition").Select(g => AssetDatabase.LoadAssetAtPath<LevelDefinition>(AssetDatabase.GUIDToAssetPath(g))).Where(d => d != null).ToArray();
        check(definitions.Count(d => d.levelNumber == def.levelNumber) == 1, "stage number is unique");
        var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(SingularityPlayableLevelSetup.CatalogPath);
        check(catalog != null && catalog.Levels.Count(d => d == def) == 1, "catalog contains exactly one SINGULARITY entry");
        check(EditorBuildSettings.scenes.Count(s => s.path == SingularityPlayableLevelSetup.ScenePath && s.enabled) == 1, "one enabled production build scene");
        check(!EditorBuildSettings.scenes.Any(s => s.path == SingularityPlayableLevelSetup.PrototypePath && s.enabled), "prototype has not replaced the playable scene in build settings");
        if (!string.IsNullOrEmpty(SingularityPlayableLevelSetup.PrototypeHashBefore))
            check(SingularityPlayableLevelSetup.PrototypeHashBefore == SingularityPlayableLevelSetup.FileHash(SingularityPlayableLevelSetup.PrototypePath), "saved prototype bytes unchanged by setup and validation");

        var scene = EditorSceneManager.OpenPreviewScene(SingularityPlayableLevelSetup.ScenePath);
        var prototype = EditorSceneManager.OpenPreviewScene(SingularityPlayableLevelSetup.PrototypePath);
        var standard = EditorSceneManager.OpenPreviewScene(SingularityStandardLayout.ReferenceScene);
        try
        {
            var managers = All<GameManagerScript>(scene);
            var rosters = All<PlayerRosterController>(scene);
            var services = All<MatchScoreService>(scene);
            check(managers.Length == 1 && managers[0].enabled, "one active standard match owner");
            check(rosters.Length == 1 && rosters[0].enabled, "one active standard roster");
            check(services.Length == 1 && services[0].enabled && services[0].Profile != null, "one authoritative score service with profile");
            var manager = managers[0]; var roster = rosters[0]; var service = services[0];
            var managerData = new SerializedObject(manager);
            check(Ref(managerData, "scoreService") == service && Ref(managerData, "scoreEconomyProfile") == service.Profile, "match and score service share authoritative economy");
            check(managerData.FindProperty("waitForRosterSpawnCompletion").boolValue && !managerData.FindProperty("engageDeathSphereOnEnd").boolValue, "standard roster gate without legacy death-sphere broadcast");
            check(managerData.FindProperty("disableGameplayOnEnd").boolValue, "world gameplay is disabled when match resolves");
            check(All<DSScript>(scene).Length == 0, "no deprecated death-sphere implementation imported");
            check(All<SingularityGameplaySession>(scene).Length == 1 && !All<SingularityGameplaySession>(scene)[0].enabled, "prototype free-play startup disabled");
            check(All<GameManagerScript>(prototype).Length == 0 && All<SingularityGameplaySession>(prototype).Single().enabled, "source remains the free-play sandbox");
            var players = All<PlayerControllerScript>(scene).OrderBy(p => p.playerID).ToArray();
            check(players.Length == 4 && players.Select(p => p.playerID).SequenceEqual(new[] { 0, 1, 2, 3 }), "four distinct standard player identities");
            var rosterData = new SerializedObject(roster);
            for (int i = 0; i < 4; i++)
            {
                var player = players[i];
                check(Ref(rosterData, "player" + (i + 1)) == player.gameObject, "roster player " + (i + 1) + " explicitly wired");
                check(player.goalZone != null && player.goalZone.scene == scene, "player " + (i + 1) + " has local goal");
                var adapter = player.GetComponent<SingularityPlayerAdapter>();
                check(adapter != null && adapter.enabled && adapter.Surface != null, "player " + (i + 1) + " retains folded-surface adapter");
            }
            check(rosterData.FindProperty("oneVOneUsesPlayers1And3").boolValue && players[0].teamID != players[2].teamID,
                "1v1 roster chooses opposing teams; 2v2 retains all four");
            var world = scene.GetRootGameObjects().Where(g => g.CompareTag("GameplayObjects")).ToArray();
            check(world.Length == 1, "exactly one world-gameplay disable scope");
            check(players.All(p => p.transform.IsChildOf(world[0].transform)), "world scope owns all players");
            check(!service.transform.IsChildOf(world[0].transform) && !manager.transform.IsChildOf(world[0].transform), "score service and match owner outlive world shutdown");
            check(All<MatchTimerPresenter>(scene).Length == 1, "one standard timer presenter");
            var timer = All<MatchTimerPresenter>(scene).Single();
            var timerData = new SerializedObject(timer);
            check(timer.enabled && Ref(timerData, "gameManager") == manager && Ref(timerData, "timerText") != null, "timer enabled and explicitly bound");
            check(!timer.transform.IsChildOf(world[0].transform), "timer UI survives end-of-match world shutdown");
            foreach (var text in All<TMP_Text>(scene))
            {
                if (text.name == "STAGE TITLE") check(text.text == "SINGULARITY", "HUD stage title");
                if (text.name == "STAGE NUMBER") check(text.text == "STAGE_006", "HUD standard stage number");
                if (text.name == "TIME quant") check(text.text != "FREE PLAY", "HUD no longer advertises free play");
                if (text.name == "TIME title") check(text.text == "TIME", "HUD timer label");
            }

            var surface = All<SingularitySurface>(scene).Single();
            var grid = All<SingularityGridRenderer>(scene).Single();
            var portal = All<SingularityBlackHolePortal>(scene).Single();
            var context = All<SingularityLevelContext>(scene).Single();
            var encounter = All<SingularityAmplifierEncounter>(scene).Single();
            var cycle = All<AmplifierResonanceSpawner>(scene).Single();
            check(context.level == def && context.match == manager && context.portal == portal, "local identity and portal cleanup bridge wired");
            check(context.stage != null && context.stage.stageId == "STAGE_006" && context.stage.displayName == "SINGULARITY" && context.stage.anomalyPool.Count == 0,
                "empty stage profile provides identity without introducing scheduled mechanics");
            check(portal.transform.IsChildOf(world[0].transform) && cycle.transform.IsChildOf(world[0].transform), "world scope owns black-hole interactions and spawn cycle");
            check(grid.surface == surface && grid.blackHole == portal, "folded grid retains black-hole attraction");
            check(encounter.surface == surface && encounter.grid == grid && encounter.portal == portal && encounter.spawner == cycle, "encounter retains its folded presentation and portal routing");
            check(cycle.waitForScoring && cycle.scoreService == service && cycle.corePrefab != null && cycle.patternOrder.Count > 0, "existing Core/Resonance cycle waits for live match scoring");
            check(All<AudioListener>(scene).Count(l => l.enabled && l.gameObject.activeInHierarchy) == 1, "one active audio listener");
            check(All<UnityEngine.EventSystems.EventSystem>(scene).Length == All<UnityEngine.EventSystems.EventSystem>(prototype).Length, "no duplicate EventSystem imported");
            check(All<Transform>(scene).Count(t => t.name == "Rewired Input Manager") == All<Transform>(prototype).Count(t => t.name == "Rewired Input Manager"), "no duplicate input manager imported");
            check(All<Transform>(scene).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)) == 0, "no missing scripts in playable scene");
            CheckSceneReferences(scene, check);
            CheckSpatialLayout(scene, prototype, standard, check);
            CheckInstructions(def.instructionsPanelPrefab, reference.instructionsPanelPrefab, check);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(standard);
            EditorSceneManager.ClosePreviewScene(prototype);
            EditorSceneManager.ClosePreviewScene(scene);
        }
        LastReport = "SINGULARITY playable level: PASS " + checks + " saved-asset checks (match/roster/timer, catalog/build, instructions, folded mechanics, local references and preserved layout).";
        return LastReport;
    }

    private static void CheckSceneReferences(Scene scene, Action<bool, string> check)
    {
        int external = 0;
        foreach (var component in All<Component>(scene))
        {
            if (component == null) continue;
            var iterator = new SerializedObject(component).GetIterator();
            while (iterator.Next(true))
            {
                if (iterator.propertyType != SerializedPropertyType.ObjectReference) continue;
                var value = iterator.objectReferenceValue;
                if (value == null || EditorUtility.IsPersistent(value)) continue;
                var go = value as GameObject;
                if (value is Component other) go = other.gameObject;
                if (go != null && go.scene.IsValid() && go.scene != scene) external++;
            }
        }
        check(external == 0, "all non-asset object references stay within the playable scene");
    }

    private static void CheckSpatialLayout(Scene scene, Scene prototype, Scene standard, Action<bool, string> check)
    {
        var surface = All<SingularitySurface>(scene).Single();
        var original = All<SingularitySurface>(prototype).Single();
        check(Mathf.Abs(surface.Width - original.Width) < .0001f && Mathf.Abs(surface.ProjectedHeight - original.ProjectedHeight) < .0001f &&
            Mathf.Abs(surface.Depth - original.Depth) < .0001f && Mathf.Abs(surface.RearScale - original.RearScale) < .0001f,
            "production preserves authored folded-surface shape");
        var planar = All<VectorGridGPU>(standard).First(g => g.gameObject.activeInHierarchy);
        Vector3 a = planar.transform.TransformPoint(new Vector3(-planar.size.x * .5f, -planar.size.y * .5f, 0f));
        Vector3 b = planar.transform.TransformPoint(new Vector3(planar.size.x * .5f, planar.size.y * .5f, 0f));
        check(Mathf.Abs(surface.Width - Mathf.Abs(b.x - a.x)) < .0001f && Mathf.Abs(surface.ProjectedHeight - Mathf.Abs(b.z - a.z)) < .0001f,
            "full folded bounds match the standard DYNAMO field");
        foreach (string name in new[] { "UI", "TEAM 1 goal", "TEAM 2 goal", "Main Camera" })
        {
            var target = All<Transform>(scene).Single(t => t.name == name);
            var source = All<Transform>(prototype).Single(t => t.name == name);
            check(SameTransform(source, target), name + " preserves authored position, rotation and size");
            if (name == "UI" || name.StartsWith("TEAM", StringComparison.Ordinal))
            {
                var standardTransform = All<Transform>(standard).Single(t => t.name == name);
                check(SameTransform(standardTransform, target), name + " matches conventional level layout");
            }
        }
        var originals = All<PlayerControllerScript>(prototype);
        foreach (var player in All<PlayerControllerScript>(scene))
            check(SameTransform(originals.Single(p => p.playerID == player.playerID).transform, player.transform), player.name + " retains authored spawn transform");
    }

    private static void CheckInstructions(GameObject panel, GameObject reference, Action<bool, string> check)
    {
        var labels = panel.GetComponentsInChildren<TMP_Text>(true);
        var referenceLabels = reference.GetComponentsInChildren<TMP_Text>(true);
        var body = labels.Single(t => t.name == "Instructions body");
        check(body.text.Contains("BACK FACE") && body.text.Contains("BLACK HOLE") && body.text.Contains("FRONT FACE"), "instructions explain folds, transfer and goal access");
        foreach (string name in new[] { "Instructions title", "Instructions body" })
        {
            var current = labels.Single(t => t.name == name);
            var source = referenceLabels.Single(t => t.name == name);
            check(current.font == source.font && current.fontSize == source.fontSize && current.alignment == source.alignment,
                name + " preserves reference typography");
        }
        var icon = panel.GetComponentInChildren<SingularityLevelIcon>(true);
        check(icon != null && panel.GetComponentsInChildren<ParticleSystem>(true).Length > 0, "instructions contain folded icon with authored particle black hole");
        check(panel.transform.Find("Electron cross section") == null, "old ORBITAL visualization replaced");
    }

    private static bool SameTransform(Transform a, Transform b) =>
        (a.position - b.position).sqrMagnitude < .00001f && Quaternion.Angle(a.rotation, b.rotation) < .001f &&
        (a.lossyScale - b.lossyScale).sqrMagnitude < .00001f;
    private static UnityEngine.Object Ref(SerializedObject owner, string field) => owner.FindProperty(field).objectReferenceValue;
    private static T[] All<T>(Scene scene) where T : Component => SingularityPlayableLevelSetup.All<T>(scene);
}
#endif
