#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Massive.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Isolated player contact, damage and firing regression checks; never saves the user's open scene.</summary>
[InitializeOnLoad]
public static class RangedDroneCollisionValidation
{
    private const string Key = "MASSIVE.RangedDroneCollision.Validation.";
    private static IEnumerator run;
    private static float resumeAt;
    private static int resumeFrame, passed;
    private static double deadline;
    private static readonly List<string> errors = new();
    public static string LastReport => SessionState.GetString(Key + "Report", "Not run");
    static RangedDroneCollisionValidation() { EditorApplication.playModeStateChanged += OnState; }

    [MenuItem("MASSIVE/Enemies/Ranged Drone/Validate Body Collision")]
    public static void Start()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Wait for Edit Mode and compilation.");
        Scene original = SceneManager.GetActiveScene();
        string path = "Assets/RangedDroneCollisionValidation-" + Guid.NewGuid().ToString("N") + ".unity";
        Scene fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(fixture);
            var camera = new GameObject("Ranged Drone collision validation camera"); camera.AddComponent<Camera>(); camera.AddComponent<AudioListener>();
            new GameObject("Ranged Drone collision validation light").AddComponent<Light>().type = LightType.Directional;
            if (!EditorSceneManager.SaveScene(fixture, path)) throw new Exception("Cannot save fixture.");
        }
        finally { EditorSceneManager.CloseScene(fixture, true); SceneManager.SetActiveScene(original); }
        SessionState.SetString(Key + "Previous", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetString(Key + "Scene", path); SessionState.SetString(Key + "Report", "Running");
        SessionState.SetBool(Key + "Pending", true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        EditorApplication.isPlaying = true;
    }
    private static void OnState(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key + "Pending", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            SessionState.SetBool(Key + "Background", Application.runInBackground); Application.runInBackground = true;
            passed = 0; errors.Clear(); run = Checks(); resumeAt = 0f; resumeFrame = 0;
            deadline = EditorApplication.timeSinceStartup + 90d;
            Application.logMessageReceived += Log; EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick; Application.logMessageReceived -= Log; run = null;
            Application.runInBackground = SessionState.GetBool(Key + "Background", Application.runInBackground);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key + "Previous", ""));
            string path = SessionState.GetString(Key + "Scene", "");
            if (path.StartsWith("Assets/RangedDroneCollisionValidation-", StringComparison.Ordinal) && path.EndsWith(".unity", StringComparison.Ordinal)) AssetDatabase.DeleteAsset(path);
            SessionState.SetBool(Key + "Pending", false);
        }
    }
    private static void Log(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message); }
    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup > deadline) { Finish("FAILED: timed out"); return; }
        if (!EditorApplication.isPlaying || run == null || Time.time < resumeAt || Time.frameCount < resumeFrame) return;
        try
        {
            if (run.MoveNext()) { resumeAt = Time.time + (run.Current is float seconds ? seconds : .02f); resumeFrame = Time.frameCount + 2; return; }
            Finish(passed + " Ranged Drone collision checks passed (isolated Play Mode).");
        }
        catch (Exception ex) { Finish("FAILED after " + passed + " checks: " + ex); }
    }
    private static void Finish(string report)
    {
        SessionState.SetString(Key + "Report", report); Debug.Log("[Ranged Drone collision validation] " + report);
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log; run = null; EditorApplication.isPlaying = false;
    }
    private static void Check(bool valid, string description)
    { if (!valid) throw new Exception(description); passed++; }

    private static IEnumerator Checks()
    {
        var scope = new GameObject("Ranged collision fixture"); scope.SetActive(false);
        var playerObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerActor.prefab"), scope.transform);
        var player = playerObject.GetComponent<PlayerControllerScript>();
        player.ConfigureDemonstration(scope.transform, 0, 1);
        player.GetComponent<Rigidbody>().useGravity = false;
        scope.SetActive(true); Place(player, new Vector3(-2f, 0f, 3f));
        yield return .2f;
        player.SetScriptedInput(new PlayerInputFrame { moveInput = Vector2.right });
        yield return 1.5f;
        Check(player.transform.position.x > .5f, "Control: actual scripted player moves freely through empty space; position=" + player.transform.position + ", velocity=" + player.GetComponent<Rigidbody>().linearVelocity + ", input=" + player.movement + ", locked=" + player.IsMatchInputLocked);
        player.ClearScriptedInput();

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RangedDroneSetup.PrefabPath);
        var root = Object.Instantiate(prefab); var enemy = root.GetComponent<EnemyBase>();
        var drone = root.GetComponent<DroneController>(); var ranged = root.GetComponent<RangedDroneController>();
        enemy.Init(drone.definition, null); enemy.ConfigureDemonstration(scope.transform, player);
        enemy.AttacksEnabled = false; enemy.HoldPosition = true;
        var body = root.GetComponent<Rigidbody>(); body.isKinematic = true;
        Place(player, new Vector3(-2f, 0f, 0f)); float mass = player.massScore;
        player.SetScriptedInput(new PlayerInputFrame { moveInput = Vector2.right });
        yield return 1.2f;
        Check(player.transform.position.x < -.25f && player.transform.position.x > -1.9f,
            "Player reaches but cannot cross the stationary Ranged Drone; final x=" + player.transform.position.x);
        Check(!enemy.IsDead && Mathf.Approximately(player.massScore, mass), "Body contact causes no melee damage or Drone suicide");
        var shell = root.GetComponent<CapsuleCollider>(); var hurt = root.GetComponentInChildren<EnemyHurtbox>();
        Check(shell && !shell.isTrigger && hurt && hurt.transform != root.transform && hurt.GetComponent<Collider>().isTrigger,
            "Solid root body and separate child damage trigger survive Awake");
        Check(root.GetComponentsInChildren<EnemyHurtbox>().Length == 1, "Exactly one damage receiver prevents duplicate sword hits");

        // Dynamic movement must preserve ordering as the player pushes the light Drone.
        player.ClearScriptedInput(); Place(player, new Vector3(-1.5f, 0f, 0f)); body.isKinematic = false;
        player.SetScriptedInput(new PlayerInputFrame { moveInput = Vector2.right });
        yield return .9f;
        Check(player.transform.position.x < body.position.x - .2f, "Dynamic Ranged Drone also keeps player and enemy bodies separated");
        player.ClearScriptedInput(); Place(player, body.position + Vector3.right * 3f);
        enemy.AttacksEnabled = true;
        yield return 3.1f;
        Check(ranged.TotalShots > 0 && !enemy.IsDead, "Ranged Drone still charges and fires with its solid body present");
        enemy.AttacksEnabled = false; ranged.enabled = false;
        foreach (var shot in Object.FindObjectsByType<RangedDroneProjectile>(FindObjectsSortMode.None)) Object.Destroy(shot.gameObject);
        Place(player, body.position + Vector3.left * 2f);
        int defeats = 0; PlayerControllerScript credited = null;
        enemy.Defeated += result => { defeats++; credited = result.creditedPlayer; };
        // Real PlayerMelee trigger exercises Unity dispatch into the relocated hurtbox.
        var sword = new GameObject("Collision validation sword"); sword.SetActive(false);
        sword.transform.SetParent(player.transform, false); sword.transform.position = body.position;
        sword.AddComponent<SphereCollider>().radius = .25f;
        var melee = sword.AddComponent<PlayerMelee>();
        typeof(PlayerMelee).GetField("gateHitboxToActivationWindow", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(melee, false);
        sword.SetActive(true); Physics.SyncTransforms(); yield return .08f;
        Check(enemy && enemy.IsDead && defeats == 1 && credited == player, "One actual sword overlap defeats the Drone once with correct player credit");
        Check(root.GetComponentsInChildren<Collider>().All(c => !c.enabled), "Death immediately disables both solid body and damage trigger");
        Object.Destroy(sword); yield return .4f;
        Check(!root, "Death animation despawns the complete enemy");
        var normal = AssetDatabase.LoadAssetAtPath<GameObject>(DronePrototypeSetup.PrefabPath);
        Check(normal.GetComponent<CapsuleCollider>().isTrigger && normal.GetComponent<EnemyHurtbox>(), "Melee Drone contact setup is unchanged");
        Check(errors.Count == 0, "No runtime errors: " + string.Join("; ", errors));
    }
    private static void Place(PlayerControllerScript player, Vector3 position)
    {
        player.transform.position = position;
        var body = player.GetComponent<Rigidbody>(); body.position = position; body.linearVelocity = Vector3.zero;
        Physics.SyncTransforms();
    }
}
#endif
