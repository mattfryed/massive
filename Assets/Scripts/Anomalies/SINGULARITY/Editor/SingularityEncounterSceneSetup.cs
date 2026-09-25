using System;
using System.Linq;
using Massive.Multiplier;
using Massive.Resonance;
using Massive.Scoring;
using Massive.Singularity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Additive, scene-local wiring. Source assets and shared tuning remain untouched.</summary>
public static class SingularityEncounterSceneSetup
{
    [MenuItem("MASSIVE/SINGULARITY/Integrate Amplifier and Resonance Cycle")]
    public static void IntegrateMenu() { Debug.Log(Integrate()); }

    public static string Integrate()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            throw new InvalidOperationException("Use Edit Mode after compilation completes.");
        Scene target = SceneManager.GetActiveScene();
        if (target.path != SingularitySceneSetup.ScenePath)
            throw new InvalidOperationException("Open SINGULARITY-PROTOTYPE first; no other scene will be changed.");
        if (InScene<SingularityAmplifierEncounter>(target) != null)
            return "SINGULARITY encounter already exists; kept all scene tuning unchanged.";
        if (InScene<AmplifierResonanceSpawner>(target) != null)
            throw new InvalidOperationException("An encounter owner already exists. Wire it explicitly rather than adding a duplicate.");
        var surface = InScene<SingularitySurface>(target);
        var grid = InScene<SingularityGridRenderer>(target);
        var scores = InScene<MatchScoreService>(target);
        var portal = InScene<SingularityBlackHolePortal>(target);
        if (surface == null || grid == null || scores == null || portal == null)
            throw new InvalidOperationException("Surface, folded grid, scores and black hole must be configured first.");

        GameObject root;
        Scene source = EditorSceneManager.OpenPreviewScene("Assets/Scenes/S-8_DYNAMO-PROTOTYPE.unity");
        try
        {
            var original = InScene<AmplifierResonanceSpawner>(source);
            if (original == null || original.corePrefab == null || original.patternOrder.Count == 0)
                throw new InvalidOperationException("DYNAMO's authored spawn cycle is not available.");
            root = UnityEngine.Object.Instantiate(original.gameObject);
            SceneManager.MoveGameObjectToScene(root, target);
            root.name = "Amplifier + Resonance - SINGULARITY Spawn Cycle";
            root.SetActive(false);
        }
        finally { EditorSceneManager.ClosePreviewScene(source); }
        Undo.RegisterCreatedObjectUndo(root, "Integrate SINGULARITY encounter");

        var cycle = root.GetComponent<AmplifierResonanceSpawner>();
        var region = root.GetComponent<AmplifierSpawnRegion>();
        cycle.spawnRegion = region;
        cycle.scoreService = scores;
        cycle.comparison = null;
        region.arenaBounds = null;
        // The center is occupied by the black hole and inner Resonance arcs.
        // Keep a neutral central strip, but leave safe pockets beside that exclusion.
        region.neutralWidthFraction = .45f;
        region.noGoColliders = Array.Empty<Collider>();
        region.ignoredColliders = Array.Empty<Collider>();

        var plane = new GameObject("Front face spawn domain (editor guide)").transform;
        plane.SetParent(root.transform, false);
        var exclusion = new GameObject("Black hole - Core spawn exclusion");
        exclusion.transform.SetParent(root.transform, false);
        var zone = exclusion.AddComponent<SphereCollider>();
        zone.isTrigger = true;
        // Explicit spawn policy only. Triggers do not add a Core/player blocking wall.
        region.noGoColliders = new Collider[] { zone };

        int index = Mathf.Clamp(cycle.startingPattern, 0, cycle.patternOrder.Count - 1);
        var template = cycle.patternOrder[index].patternPrefab;
        if (template == null) throw new InvalidOperationException("Starting pattern prefab is missing.");
        var preview = UnityEngine.Object.Instantiate(template, root.transform);
        preview.name = "Resonance - Front Face Edit Preview";
        preview.transform.SetPositionAndRotation(surface.transform.position, surface.transform.rotation);
        preview.arenaBounds = null;
        preview.grid = null;
        preview.energyOrigin = surface.transform;
        var formation = preview.GetComponent<ResonanceManifestation>();
        if (formation == null) formation = preview.gameObject.AddComponent<ResonanceManifestation>();
        formation.spawnPrefabTuningTarget = template.GetComponent<ResonanceManifestation>();
        formation.SetArea(Vector2.zero, new Vector2(surface.Width, surface.FrontHeight) * .5f);
        region.previewPattern = preview;
        region.previewObjectRadius = cycle.CorePlacementRadius;
        cycle.patternAnchor = preview.transform;
        cycle.standaloneExamples = new[] { preview.gameObject };

        var bridge = root.AddComponent<SingularityAmplifierEncounter>();
        bridge.spawner = cycle; bridge.surface = surface; bridge.grid = grid;
        bridge.portal = portal; bridge.frontPlacementPlane = plane;
        bridge.blackHoleExclusion = zone; bridge.editPreview = preview;
        bridge.RefreshPlacement();
        bridge.RefreshEditPreview();
        root.SetActive(true);
        preview.enabled = true;
        preview.gameObject.SetActive(true);
        preview.Rebuild();
        EditorUtility.SetDirty(cycle); EditorUtility.SetDirty(region); EditorUtility.SetDirty(bridge);
        EditorSceneManager.MarkSceneDirty(target);
        if (!EditorSceneManager.SaveScene(target)) throw new InvalidOperationException("Could not save SINGULARITY.");
        Selection.activeGameObject = root;
        return "Integrated DYNAMO's existing spawn cycle in SINGULARITY: front-face Resonance, wrapping Core, neutral placement and black-hole spawn exclusion. Shared presets and other scenes unchanged.";
    }

    private static T InScene<T>(Scene scene) where T : Component
    { return scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).FirstOrDefault(); }
}
