#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Massive.Enemies;
using Massive.Multiplier;
using Massive.Orbital;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class OrbitalEncounterSetup
{
    public const string Folder = "Library/OrbitalEncounterValidation";
    public const string ProfilePath = OrbitalLevelSetup.Folder + "ORBITAL Encounter Profile.asset";
    static OrbitalEncounterSetup()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Folder + "/install.request")) return;
            File.Delete(Folder + "/install.request");
            try { Install(); File.WriteAllText(Folder + "/install.txt", "INSTALLED"); }
            catch (Exception e) { File.WriteAllText(Folder + "/install.txt", e.ToString()); Debug.LogException(e); }
        };
    }
    public static T InScene<T>(Scene scene) where T : Component => Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
        .FirstOrDefault(c => c.gameObject.scene == scene);

    [MenuItem("MASSIVE/ORBITAL/Set Up Enemy Lab Timeline")]
    public static void Install()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != OrbitalLevelSetup.ScenePath)
            throw new InvalidOperationException("Open ORBITAL in Edit Mode.");
        var director = InScene<EnemyDirector>(scene);
        var bounds = InScene<ArenaBoundsFromVectorGrid>(scene);
        var cloud = InScene<OrbitalProbabilityCloud>(scene);
        var spawner = InScene<AmplifierResonanceSpawner>(scene);
        var timeline = AssetDatabase.LoadAssetAtPath<EnemyEncounterTimeline>(EnemyEncounterLabSetup.TimelinePath);
        if (!director || !bounds || !cloud || !spawner || !timeline)
            throw new InvalidOperationException("ORBITAL requires its existing Director, arena, Cloud, paired spawner and Enemy Lab timeline.");
        Directory.CreateDirectory(Folder);
        File.Copy(scene.path, Folder + "/ORBITAL-before.unity", true);
        string environmentBefore = EnvironmentSettings(cloud, spawner);
        string timelineBefore = EditorJsonUtility.ToJson(timeline);
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Integrate ORBITAL encounters");
        Undo.RecordObjects(new Object[] { director, director.gameObject }, "Configure ORBITAL Director");
        var profile = AssetDatabase.LoadAssetAtPath<EnemySpawnProfile>(ProfilePath);
        if (!profile)
        {
            profile = ScriptableObject.CreateInstance<EnemySpawnProfile>();
            profile.enabled = true;
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }
        var layout = director.GetComponent<EnemyArenaLayout>();
        if (!layout) layout = Undo.AddComponent<EnemyArenaLayout>(director.gameObject);
        Undo.RecordObject(layout, "Configure ORBITAL layout");
        bounds.RefreshNow(); layout.arena = bounds; layout.gameplayHeight = 0;
        if (layout.regions.Count == 0)
        {
            layout.regions.Add(new EnemyArenaLayout.Region { id = "Arena", rectangle = new Rect(-1, -1, 2, 2) });
            layout.regions.Add(new EnemyArenaLayout.Region { id = "Upper", rectangle = new Rect(-.85f, .45f, 1.7f, .35f) });
            layout.regions.Add(new EnemyArenaLayout.Region { id = "Lower", rectangle = new Rect(-.85f, -.8f, 1.7f, .35f) });
            layout.regions.Add(new EnemyArenaLayout.Region { id = "Left", rectangle = new Rect(-.85f, -.65f, .35f, 1.3f) });
            layout.regions.Add(new EnemyArenaLayout.Region { id = "Right", rectangle = new Rect(.5f, -.65f, .35f, 1.3f) });
        }
        if (layout.sockets.Count == 0)
        {
            void Socket(string id, Vector2 position, Vector2 inward) => layout.sockets.Add(new EnemyArenaLayout.Socket
            { id = id, position = position, inward = inward, clearanceOffset = 1.12f, aimArc = 75,
                useRange = true, rangeStart = new Vector2(0, position.y), rangeEnd = new Vector2(Mathf.Sign(position.x), position.y) });
            Socket("TopLeft", new(-.5f, 1), Vector2.down); Socket("TopRight", new(.5f, 1), Vector2.down);
            Socket("BottomLeft", new(-.5f, -1), Vector2.up); Socket("BottomRight", new(.5f, -1), Vector2.up);
        }
        layout.allowResonanceAndAmplifierOverlap = true;
        layout.spawnOverlapRoots = new[] { cloud.transform };
        if (!director.enemyRoot || director.enemyRoot == director.transform)
        {
            var root = new GameObject("Timeline enemies"); Undo.RegisterCreatedObjectUndo(root, "Create enemy container");
            root.transform.SetParent(director.transform, false); director.enemyRoot = root.transform;
        }
        director.gameObject.name = "ORBITAL Enemy Timeline";
        director.spawnProfile = profile; director.encounterTimeline = timeline; director.arenaLayout = layout;
        director.arenaBounds = bounds; director.anomalyManager = InScene<AnomalyManager>(scene);
        // Timeline layout and masks own enemy placement; the power-up region retains its own policy.
        director.placementRegion = null; director.resonanceSpawner = spawner;
        director.spawnBlockMask = LayerMask.GetMask("Obstacle", "NoSpawnZone", "PowerUps");
        director.borderBufferWorld = .2f; director.spawnCheckRadiusWorld = .55f; director.minDistanceFromPlayers = 1.4f;
        director.waitForScoring = true; director.previewCue = -1; director.encounterSeed = 1;
        director.enabled = true; director.gameObject.SetActive(true);
        EditorUtility.SetDirty(director); EditorUtility.SetDirty(layout);
        PrefabUtility.RecordPrefabInstancePropertyModifications(director);
        PrefabUtility.RecordPrefabInstancePropertyModifications(director.gameObject);
        if (environmentBefore != EnvironmentSettings(cloud, spawner) || timelineBefore != EditorJsonUtility.ToJson(timeline))
            throw new InvalidOperationException("Environment or authored timeline changed during setup.");
        File.WriteAllText(Folder + "/preserved-settings.json", environmentBefore);
        AssetDatabase.SaveAssetIfDirty(profile);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Undo.CollapseUndoOperations(group);
        Selection.activeGameObject = director.gameObject;
        EnemyEncounterComposer.ShowForDirector(director);
        Debug.Log("[ORBITAL encounters] Shared Enemy Lab timeline connected; Cloud and paired spawner settings preserved.", director);
    }
    public static string EnvironmentSettings(OrbitalProbabilityCloud cloud, AmplifierResonanceSpawner spawner) =>
        EditorJsonUtility.ToJson(cloud) + "\n" + EditorJsonUtility.ToJson(cloud.cloud) + "\n" +
        EditorJsonUtility.ToJson(spawner) + "\n" + EditorJsonUtility.ToJson(spawner.spawnRegion);
}
#endif
