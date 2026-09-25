using System;
using System.Linq;
using Massive.Scoring;
using Massive.Singularity;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One-time, scene-local import of the authored DYNAMO systems. Source scenes are preview copies only.</summary>
public static class SingularityGameplaySceneSetup
{
    private const string IntegrationName = "SINGULARITY - Gameplay";

    [MenuItem("MASSIVE/SINGULARITY/Integrate DYNAMO Players UI and Goals")]
    public static void IntegrateMenu() { Debug.Log(Integrate()); }

    public static string Integrate()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            throw new InvalidOperationException("Integration requires Edit Mode with compilation complete.");
        Scene target = SceneManager.GetActiveScene();
        if (target.path != SingularitySceneSetup.ScenePath)
            throw new InvalidOperationException("Open SINGULARITY-PROTOTYPE first. No other scene will be changed.");
        if (target.GetRootGameObjects().Any(g => g.name == IntegrationName))
            return "Gameplay import already exists; left the scene unchanged.";
        var surface = target.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SingularitySurface>(true)).Single();
        var grid = surface.GetComponentInChildren<SingularityGridRenderer>(true);
        GameObject imported = null;
        Scene source = EditorSceneManager.OpenPreviewScene("Assets/Scenes/S-8_DYNAMO-PROTOTYPE.unity");
        try
        {
            var sourceRoots = source.GetRootGameObjects();
            // The goals and UI are nested inside PLAYING FIELD's prefab instance.
            // Unpack only this disposable preview copy before selecting its subtrees.
            var field = sourceRoots.First(g => g.name == "PLAYING FIELD");
            if (PrefabUtility.IsPartOfPrefabInstance(field))
                PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(field),
                    PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Func<string, Transform> find = path =>
            {
                string[] parts = path.Split('/');
                Transform t = sourceRoots.First(g => g.name == parts[0]).transform;
                for (int i = 1; i < parts.Length; i++) t = t.Find(parts[i]);
                if (!t) throw new InvalidOperationException("Missing source group: " + path);
                return t;
            };
            var staging = new GameObject(IntegrationName);
            SceneManager.MoveGameObjectToScene(staging, source);
            // Clone together so references between the score service, UI, players and goals remap together.
            foreach (string path in new[] {
                "GameplayObjects/Players", "PLAYING FIELD/UI", "PLAYING FIELD/TEAM 1 goal", "PLAYING FIELD/TEAM 2 goal",
                "Scene Managers/MatchScoreService", "Scene Managers/SFX Manager", "Scene Managers/PlayerIDToastSystem",
                "MANAGERS/EventSystem", "Directional Light" })
                find(path).SetParent(staging.transform, true);
            imported = UnityEngine.Object.Instantiate(staging);
            imported.name = IntegrationName;
            SceneManager.MoveGameObjectToScene(imported, target);
            Undo.RegisterCreatedObjectUndo(imported, "Import SINGULARITY gameplay");
        }
        finally { EditorSceneManager.ClosePreviewScene(source); }

        // No finite match timer, level-loading logic or menu roster in this mechanics sandbox.
        foreach (var roster in imported.GetComponentsInChildren<PlayerRosterController>(true)) UnityEngine.Object.DestroyImmediate(roster);
        foreach (var timer in imported.GetComponentsInChildren<MatchTimerPresenter>(true)) timer.enabled = false;
        var playersRoot = imported.transform.Find("Players");
        var players = playersRoot.GetComponentsInChildren<PlayerControllerScript>(true).OrderBy(p => p.playerID).ToArray();
        if (players.Length != 4) throw new InvalidOperationException("Expected the four authored players.");
        var goals = new[] { imported.transform.Find("TEAM 1 goal"), imported.transform.Find("TEAM 2 goal") };
        var adapters = new SingularityPlayerAdapter[players.Length];
        for (int i = 0; i < players.Length; i++)
        {
            var player = players[i];
            player.goalZone = goals[player.teamID == 2 ? 1 : 0].gameObject;
            // The newer detached sword-plasma prefabs are planar. Retain actual
            // attacks and the surface-mapped trail; defer those optional overlays.
            var planarPlasma = player.GetComponent<Massive.Player.PlayerMeleePlasma>();
            if (planarPlasma) planarPlasma.enabled = false;
            foreach (var particles in player.GetComponentsInChildren<ParticleSystemRenderer>(true)) particles.enabled = false;
            player.gameObject.SetActive(true);
            adapters[i] = player.gameObject.AddComponent<SingularityPlayerAdapter>();
            adapters[i].Configure(surface, grid);
        }
        var registry = playersRoot.GetComponent<PlayerManager>();
        if (registry) registry.RefreshPlayersFromChildren();
        var session = imported.AddComponent<SingularityGameplaySession>();
        session.players = players;
        session.scoreService = imported.GetComponentInChildren<MatchScoreService>(true);
        // Shared-world grid interactors are not the folded-surface attraction driver.
        foreach (var interactor in imported.GetComponentsInChildren<GridInteractor>(true)) interactor.enabled = false;

        Transform ui = imported.transform.Find("UI");
        foreach (var text in ui.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.name == "STAGE TITLE") text.text = "SINGULARITY";
            if (text.name == "STAGE NUMBER") text.text = "PROTOTYPE";
            if (text.name == "TIME quant") text.text = "FREE PLAY";
            if (text.name == "TIME title") text.text = "SURFACE LOOP";
        }
        var textObjects = ui.Find("Text Objects");
        var oldTitle = textObjects.Find("Stage Title old");
        if (oldTitle) oldTitle.gameObject.SetActive(false);

        Scene holeSource = EditorSceneManager.OpenPreviewScene("Assets/Scenes/S-P_PROTOTYPING.unity");
        GameObject hole;
        try
        {
            var sourceHole = holeSource.GetRootGameObjects().First(g => g.name == "GameplayObjects").transform.Find("Sphere");
            if (!sourceHole || !sourceHole.Find("event horizon")) throw new InvalidOperationException("Authored black-hole group not found.");
            hole = UnityEngine.Object.Instantiate(sourceHole.gameObject);
            SceneManager.MoveGameObjectToScene(hole, target);
            hole.transform.SetParent(imported.transform, true);
            hole.name = "Black Hole - Face Passage";
            hole.transform.position = surface.transform.position;
            hole.transform.rotation = Quaternion.identity;
            foreach (var damage in hole.GetComponentsInChildren<BlackHoleScript>(true)) UnityEngine.Object.DestroyImmediate(damage);
            foreach (var pull in hole.GetComponentsInChildren<AttractScript>(true)) pull.enabled = false;
            foreach (var force in hole.GetComponentsInChildren<GridForceSource>(true)) force.enabled = false;
            foreach (var collider in hole.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var behavior in hole.GetComponents<MonoBehaviour>()) if (behavior.GetType().Name == "Rotator") behavior.enabled = false;
        }
        finally { EditorSceneManager.ClosePreviewScene(holeSource); }
        // The source accretion discs lie in local XY and normally tumble. Keep
        // the portal stable, but turn the copied visual toward the overhead view.
        var portalRoot = new GameObject("Black Hole - Face Passage");
        SceneManager.MoveGameObjectToScene(portalRoot, target);
        portalRoot.transform.SetParent(imported.transform, false);
        portalRoot.transform.position = surface.transform.position;
        hole.name = "Black Hole - Visual";
        hole.transform.SetParent(portalRoot.transform, true);
        hole.transform.localRotation = Quaternion.Euler(80f, 0f, 0f);
        var portal = portalRoot.AddComponent<SingularityBlackHolePortal>();
        portal.Configure(surface, adapters);
        grid.blackHole = portal;
        portalRoot.AddComponent<SingularityBlackHoleFeedback>().Configure(portal, grid);

        var probe = surface.GetComponentInChildren<SingularityPlayerMotor>(true);
        if (probe) probe.gameObject.SetActive(false);
        foreach (var legend in target.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SingularityPrototypeHUD>(true))) legend.enabled = false;
        grid.player = null;
        grid.rearGapLength = grid.rearDashLength;
        grid.RefreshPresentation();
        // Drop obsolete references to objects from the source scene (e.g. its flat grid); never alter referenced assets.
        ClearExternalSceneReferences(imported, target);
        SingularityStandardLayout.Apply(target);
        SingularityBorderSceneSetup.Apply(target);
        EditorSceneManager.MarkSceneDirty(target);
        if (!EditorSceneManager.SaveScene(target)) throw new InvalidOperationException("Could not save SINGULARITY.");
        Selection.activeGameObject = imported;
        return "Imported four DYNAMO players, both team HUDs/goals and the authored black hole into SINGULARITY only.";
    }

    private static void ClearExternalSceneReferences(GameObject root, Scene target)
    {
        foreach (var component in root.GetComponentsInChildren<Component>(true))
        {
            if (!component || component is Transform) continue;
            var serialized = new SerializedObject(component);
            var property = serialized.GetIterator();
            bool changed = false;
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                var value = property.objectReferenceValue;
                if (!value || EditorUtility.IsPersistent(value)) continue;
                var go = value as GameObject;
                var referenceComponent = value as Component;
                if (referenceComponent) go = referenceComponent.gameObject;
                if (go && go.scene.IsValid() && go.scene != target) { property.objectReferenceValue = null; changed = true; }
            }
            if (changed) serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
