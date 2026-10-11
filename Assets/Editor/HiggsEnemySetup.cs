#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Massive.Enemies;
using Massive.Multiplier;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Adds HIGGS's scene bindings to the same encounter assets used by LATTICE.</summary>
[InitializeOnLoad]
public static class HiggsEnemySetup
{
    public const string ReferenceScene = "Assets/Scenes/S-1_LATTICE.unity";
    public const string Folder = "Library/HiggsEnemyValidation";
    static HiggsEnemySetup() => EditorApplication.delayCall += () =>
    {
        string request = Folder + "/install.request";
        if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(request);
        try { Install(); HiggsEnemyValidation.Run(); }
        catch (Exception e) { File.WriteAllText(Folder + "/install.txt", "FAILED\n" + e); Debug.LogException(e); }
    };

    public static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();

    [MenuItem("MASSIVE/HIGGS/Integrate Enemy Timeline from LATTICE")]
    public static void Install()
    {
        var target = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || target.path != HiggsLevelSetup.ScenePath || target.isDirty)
            throw new InvalidOperationException("Open the saved HIGGS scene in Edit Mode.");
        if (All<EnemyDirector>(target).Length > 0)
            throw new InvalidOperationException("HIGGS already has an enemy Director; edit its existing settings instead of adding another.");
        Directory.CreateDirectory(Folder);
        File.Copy(target.path, Folder + "/HIGGS-before.unity", true);
        string referenceHash = Hash128.Compute(File.ReadAllText(ReferenceScene)).ToString();
        var bounds = All<ArenaBoundsFromVectorGrid>(target).Single();
        var pair = All<AmplifierResonanceSpawner>(target).Single();
        var content = All<Transform>(target).Single(t => AnimationUtility.CalculateTransformPath(t, null) == "GameplayObjects/LevelContent/HIGGS");
        var preserved = All<HiggsExcitationKnotManager>(target).Cast<Component>()
            .Concat(All<HiggsFieldGPU>(target)).Concat(All<SymmetryKnotGoalMouth>(target))
            .Concat(All<AmplifierResonanceSpawner>(target)).Concat(All<AmplifierSpawnRegion>(target)).ToArray();
        var snapshots = preserved.ToDictionary(c => c, c => EditorJsonUtility.ToJson(c));
        EnemyDirector director;
        var source = EditorSceneManager.OpenPreviewScene(ReferenceScene);
        try
        {
            var template = All<EnemyDirector>(source).Single(d => d.encounterTimeline && d.arenaLayout);
            var prefab = PrefabUtility.GetCorrespondingObjectFromSource(template.gameObject);
            if (!prefab || !template.spawnProfile || !template.waitForScoring)
                throw new InvalidOperationException("LATTICE's shared encounter setup is incomplete.");
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, target);
            Undo.RegisterCreatedObjectUndo(root, "Integrate HIGGS enemy timeline");
            root.name = "HIGGS Enemy Timeline"; root.transform.SetParent(content, false);
            director = root.GetComponent<EnemyDirector>();
            EditorUtility.CopySerialized(template, director);
            var layout = Undo.AddComponent<EnemyArenaLayout>(root);
            EditorUtility.CopySerialized(template.arenaLayout, layout);
            layout.arena = bounds; layout.exclusions = Array.Empty<Collider>();
            layout.spawnOverlapRoots = Array.Empty<Transform>();
            // Current shared placement standard: each top/bottom mount can find room
            // within its own quadrant, while already-announced positions stay fixed.
            foreach (var socket in layout.sockets)
            {
                socket.support = null;
                if (socket.id != "TopLeft" && socket.id != "TopRight" && socket.id != "BottomLeft" && socket.id != "BottomRight") continue;
                float x = socket.id.EndsWith("Left") ? -1 : 1, y = socket.id.StartsWith("Top") ? 1 : -1;
                socket.useRange = true; socket.rangeStart = new Vector2(0, y); socket.rangeEnd = new Vector2(x, y);
            }
            // The shared timeline has an approved side-wall turret fallback. These
            // positions sit above/below the goal opening on HIGGS's straight walls.
            void Side(string id, Vector2 position, Vector2 inward)
            {
                if (layout.sockets.Any(s => s.id == id)) return;
                layout.sockets.Add(new EnemyArenaLayout.Socket { id = id, enabled = true,
                    position = position, inward = inward, aimArc = 75, clearanceOffset = 1.12f });
            }
            Side("SideLeftTop", new(-1, .75f), Vector2.right);
            Side("SideLeftBottom", new(-1, -.75f), Vector2.right);
            Side("SideRightTop", new(1, .75f), Vector2.left);
            Side("SideRightBottom", new(1, -.75f), Vector2.left);
            var enemies = new GameObject("Timeline enemies");
            Undo.RegisterCreatedObjectUndo(enemies, "Create HIGGS enemy container");
            enemies.transform.SetParent(root.transform, false);
            director.enemyRoot = enemies.transform;
            director.arenaLayout = layout; director.arenaBounds = bounds;
            director.anomalyManager = All<AnomalyManager>(target).Single();
            director.resonanceSpawner = pair; director.placementRegion = null;
            director.waitForScoring = true; director.previewCue = -1;
            director.enabled = true; root.SetActive(true);
            // The shared prefab also carries the legacy placement region; keep its
            // arena reference local even though timeline placement uses the layout.
            root.GetComponent<AmplifierSpawnRegion>().arenaBounds = bounds;
            foreach (var c in root.GetComponents<Component>())
            {
                EditorUtility.SetDirty(c);
                if (PrefabUtility.IsPartOfPrefabInstance(c)) PrefabUtility.RecordPrefabInstancePropertyModifications(c);
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(root);
        }
        finally { EditorSceneManager.ClosePreviewScene(source); }
        if (preserved.Any(c => snapshots[c] != EditorJsonUtility.ToJson(c)) || referenceHash != Hash128.Compute(File.ReadAllText(ReferenceScene)).ToString())
            throw new InvalidOperationException("HIGGS environment or the reference scene changed unexpectedly.");
        EditorSceneManager.MarkSceneDirty(target);
        if (!EditorSceneManager.SaveScene(target)) throw new IOException("Could not save HIGGS enemy integration.");
        File.WriteAllText(Folder + "/install.txt", "INSTALLED\nShared timeline: " + AssetDatabase.GetAssetPath(director.encounterTimeline) +
            "\nShared profile (same as LATTICE): " + AssetDatabase.GetAssetPath(director.spawnProfile) +
            "\nHIGGS field, Knot, goal anchors and Amplifier configuration preserved.\nLATTICE unchanged.\n");
        Selection.activeGameObject = director.gameObject;
    }
}
#endif
