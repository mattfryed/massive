#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using Massive.Scoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Regression checks for references crossing the shared Actor/roster boundary.</summary>
public static class PlayerActorMigrationValidation
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    static int checks;
    static void Check(bool condition, string label)
    { if (!condition) throw new InvalidOperationException(label); checks++; }

    [MenuItem("MASSIVE/Player/Validate Actor Scene Bindings")]
    public static void ValidateMenu() => Debug.Log(Authored());

    public static string Authored()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        checks = 0;
        var levels = PlayableLevels();
        foreach (string name in levels.Select(d => d.SceneName).Concat(new[] {
            "S-8_DYNAMO-PROTOTYPE", "S-8_DYNAMO-SCIENTIFIC", "S-P_PROTOTYPING", "Templates/Level-Planar" }).Distinct())
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/" + name + ".unity");
            try
            {
                var rosters = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerRosterController>(true)).ToArray();
                Check(rosters.Length == 1, name + " exactly one roster");
                foreach (var roster in rosters)
                {
                    var slots = new[] { roster.P1, roster.P2, roster.P3, roster.P4 };
                    Check(slots.All(p => p) && slots.Distinct().Count() == 4, name + " explicit roster bindings");
                    for (int slot = 0; slot < slots.Length; slot++)
                    {
                        var p = slots[slot].GetComponent<PlayerControllerScript>();
                        Check(p && p.playerID == slot && p.ParticipatesInMatch && p.gameObject.scene == scene, name + " match actor slot " + slot);
                        var anchor = new SerializedObject(p).FindProperty("respawnPointOverride").objectReferenceValue as Transform;
                        Check(anchor && anchor.gameObject.scene == scene, name + " local spawn " + slot);
                        var camera = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)).FirstOrDefault(c => c.isActiveAndEnabled && c.CompareTag("MainCamera"));
                        Check(camera && !SpawnHiddenByArenaMask(scene, camera, anchor.position), name + " spawn not hidden by arena masks " + slot);
                        Check(p.goalZone && p.goalZone.scene == scene, name + " local goal " + slot);
                        if (name == "S-3_NOVA" || name.Contains("Level-Planar"))
                            Check(p.GetComponent<GridInteractor>().grid, name + " local grid " + slot);
                    }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        CheckFallback();
        return checks + " Actor scene binding and missing-object fallback checks passed.";
    }

    static LevelDefinition[] PlayableLevels()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/Scripts/CORE/LevelCatalog.asset");
        if (!catalog || catalog.Count == 0) throw new InvalidOperationException("Playable level catalog is missing or empty.");
        var levels = catalog.Levels.ToArray();
        foreach (var level in levels)
            if (!level || !EditorBuildSettings.scenes.Any(s => s.enabled && Path.GetFileNameWithoutExtension(s.path) == level.SceneName))
                throw new InvalidOperationException("Catalog level lacks an enabled gameplay scene: " + level);
        return levels;
    }

    static bool SpawnHiddenByArenaMask(Scene scene, Camera camera, Vector3 position)
    {
        var ray = camera.ViewportPointToRay(camera.WorldToViewportPoint(position));
        float distance = Vector3.Distance(ray.origin, position);
        return scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Collider>())
            .Where(c => c.enabled && c.name.StartsWith("mask plane", StringComparison.OrdinalIgnoreCase))
            .Any(c => c.bounds.IntersectRay(ray, out float hit) && hit < distance);
    }

    static void CheckFallback()
    {
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            var scope = new GameObject("Inactive roster regression fixture"); scope.SetActive(false);
            var roster = scope.AddComponent<PlayerRosterController>();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(MassiveDemoMigration.ActorPath);
            var expected = new GameObject[4];
            for (int slot = 0; slot < 4; slot++)
            {
                expected[slot] = Object.Instantiate(source, scope.transform);
                expected[slot].GetComponent<PlayerControllerScript>().playerID = slot;
                var stale = new GameObject("Destroyed serialized reference");
                typeof(PlayerRosterController).GetField("player" + (slot + 1), Fields).SetValue(roster, stale);
                Object.DestroyImmediate(stale);
                Check(!stale && !ReferenceEquals(stale, null), "Fixture retains Unity missing-object wrapper");
            }
            var demo = Object.Instantiate(source, scope.transform);
            demo.GetComponent<PlayerControllerScript>().ConfigureDemonstration(scope.transform, 0, 1);
            typeof(PlayerRosterController).GetMethod("AutoAssignIfMissing", Fields).Invoke(roster, null);
            Check(new[] { roster.P1, roster.P2, roster.P3, roster.P4 }.SequenceEqual(expected), "Fallback rebinds actual match actors, excludes demonstration and other loaded scene");
        }
        finally { EditorSceneManager.CloseScene(scene, true); SceneManager.SetActiveScene(previous); }
    }

    static bool sceneReady, started, respawned, background;
    static double deadline;
    static PlayerControllerScript victim;
    static Transform anchor;
    static readonly Dictionary<PlayerControllerScript, Vector3> spawnPositions = new();
    static Vector3 respawnPosition;
    static string screenshot;
    static string targetScene;
    public static string Status { get; private set; } = "Not run";

    public static string StartRun(bool fourPlayers, string screenshotPath, string sceneName = "S-3_NOVA")
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
        Cleanup();
        background = Application.runInBackground; Application.runInBackground = true;
        checks = 0; sceneReady = started = respawned = false; screenshot = screenshotPath; spawnPositions.Clear();
        targetScene = sceneName;
        GameFlowContext.Instance.SetMode(fourPlayers ? GameMode.TwoVTwo : GameMode.OneVOne);
        deadline = EditorApplication.timeSinceStartup + 45;
        Status = "Running " + targetScene + " " + (fourPlayers ? "2v2" : "1v1") + " startup and respawn";
        SceneManager.sceneLoaded += Loaded;
        EditorApplication.update += Tick;
        SceneManager.LoadScene(targetScene);
        return Status;
    }

    static void Loaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != targetScene) return;
        sceneReady = true;
        foreach (var p in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerControllerScript>(true)))
            p.MatchSpawnCompleted += OnSpawn;
    }
    static void OnSpawn(PlayerControllerScript player)
    { spawnPositions[player] = player.transform.position; player.MatchSpawnCompleted -= OnSpawn; }
    static void OnRespawn(PlayerControllerScript player)
    { respawnPosition = player.transform.position; respawned = true; }
    static void Cleanup()
    {
        EditorApplication.update -= Tick; SceneManager.sceneLoaded -= Loaded;
        if (victim) victim.RespawnCompleted -= OnRespawn;
    }
    static void Tick()
    {
        try
        {
            if (!Application.isPlaying) { Cleanup(); Application.runInBackground = background; return; }
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException(Status);
            if (!sceneReady) return;
            var match = Object.FindFirstObjectByType<GameManagerScript>();
            if (!match) return;
            if (!started && match.Phase == MatchRuntimePhase.Regulation)
            {
                started = true;
                typeof(GameManagerScript).GetField("autoReturnOnAllPlayersInactive", Fields).SetValue(match, false);
                typeof(GameManagerScript).GetField("_regulationDurationSeconds", Fields).SetValue(match, 600f);
                typeof(GameManagerScript).GetField("_regulationRemainingSeconds", Fields).SetValue(match, 600f);
                var roster = Object.FindFirstObjectByType<PlayerRosterController>();
                Check(roster && roster.IsRosterReady, "Roster completed normal startup");
                var slots = new[] { roster.P1, roster.P2, roster.P3, roster.P4 };
                for (int slot = 0; slot < 4; slot++)
                {
                    var p = slots[slot].GetComponent<PlayerControllerScript>();
                    bool active = GameFlowContext.Instance.IsTwoVTwo || slot == 0 || slot == 2;
                    Check(p.gameObject.activeInHierarchy == active, "Correct active slot " + slot);
                    if (!active) continue;
                    var spawn = (Transform)new SerializedObject(p).FindProperty("respawnPointOverride").objectReferenceValue;
                    // Record the spawn event, before gravity can move actors during the countdown.
                    Check(spawnPositions.TryGetValue(p, out var initialPosition) && Vector3.Distance(initialPosition, spawn.position) < .01f, "Spawned at authored anchor " + slot);
                    var viewport = Camera.main.WorldToViewportPoint(p.transform.position);
                    Check(viewport.z > 0 && viewport.x > 0 && viewport.x < 1 && viewport.y > 0 && viewport.y < 1, "Spawn inside camera " + slot);
                    Check(!SpawnHiddenByArenaMask(p.gameObject.scene, Camera.main, p.transform.position), "Player not hidden by arena masks " + slot);
                    var dots = p.GetComponentInChildren<PlayerNuggetsGPU>(true);
                    Check(p.visualsController && p.visualsController.enabled && p.visualsController.baseRadius > .01f && dots && dots.enabled && dots.baseRadius > .01f, "Procedural actor render drivers visible " + slot);
                    // SINGULARITY has no VectorGridGPU; a grid binding is required
                    // only in scenes that actually render that grid system.
                    bool gridRequired = Object.FindFirstObjectByType<VectorGridGPU>();
                    Check(p.ParticipatesInMatch && p.goalZone && (!gridRequired || p.GetComponent<GridInteractor>().grid), "Gameplay role/goal/optional grid " + slot);
                }
                victim = roster.P1.GetComponent<PlayerControllerScript>();
                anchor = (Transform)new SerializedObject(victim).FindProperty("respawnPointOverride").objectReferenceValue;
                victim.RespawnCompleted += OnRespawn;
                var result = victim.ApplyExternalMassDelta(-100f, roster.P3, true);
                Check(result.accepted && result.causedDeath && victim.temporarilyEliminated, "Production damage/death accepted");
            }
            if (started && respawned)
            {
                Check(!victim.temporarilyEliminated && victim.massScore > victim.massScoreMin, "Normal lifecycle restores living player");
                Check(Vector3.Distance(respawnPosition, anchor.position) < .2f, "Respawn uses roster anchor");
                if (!string.IsNullOrEmpty(screenshot)) ScreenCapture.CaptureScreenshot(screenshot);
                Status = "PASS: " + checks + " checks; " + targetScene + " " + GameFlowContext.Instance.Mode + " visible spawns and normal death/respawn.";
                Cleanup(); Application.runInBackground = background;
            }
        }
        catch (Exception e) { Status = "FAIL: " + e.Message; Cleanup(); Application.runInBackground = background; Debug.LogException(e); }
    }

    static LevelDefinition[] batchLevels;
    static int batchIndex, settleFrames;
    static string batchDirectory;
    static bool batchBackground;
    static readonly List<string> batchResults = new();
    public static string BatchStatus => string.Join("\n", batchResults) + "\n" + Status;

    public static string StartPlayableCatalogRun(string screenshotDirectory)
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
        EditorApplication.update -= BatchTick;
        batchLevels = PlayableLevels(); batchIndex = 0; settleFrames = 0; batchResults.Clear();
        batchDirectory = screenshotDirectory; Directory.CreateDirectory(batchDirectory);
        batchBackground = Application.runInBackground; Application.runInBackground = true;
        StartBatchCase(); EditorApplication.update += BatchTick;
        return "Validating " + batchLevels.Length + " playable levels in 1v1 and 2v2.";
    }

    static void StartBatchCase()
    {
        var level = batchLevels[batchIndex / 2];
        bool fourPlayers = batchIndex % 2 == 1;
        GameFlowContext.Instance.SelectLevel(level);
        StartRun(fourPlayers, Path.Combine(batchDirectory, level.SceneName + (fourPlayers ? "-2v2.png" : "-1v1.png")), level.SceneName);
    }

    static void BatchTick()
    {
        if (!Application.isPlaying)
        { EditorApplication.update -= BatchTick; Application.runInBackground = batchBackground; return; }
        if (!Status.StartsWith("PASS:") && !Status.StartsWith("FAIL:")) return;
        // Let the screenshot render before loading the next scene.
        if (++settleFrames < 3) return;
        settleFrames = 0; batchResults.Add(batchLevels[batchIndex / 2].SceneName + " " + (batchIndex % 2 == 1 ? "2v2: " : "1v1: ") + Status);
        batchIndex++;
        if (batchIndex < batchLevels.Length * 2) { StartBatchCase(); return; }
        EditorApplication.update -= BatchTick; Application.runInBackground = batchBackground;
        Status = "Catalog complete: " + batchResults.Count(r => r.Contains("PASS:")) + "/" + batchResults.Count + " cases passed.";
        File.WriteAllText(Path.Combine(batchDirectory, "results.txt"), BatchStatus);
    }
}
#endif
