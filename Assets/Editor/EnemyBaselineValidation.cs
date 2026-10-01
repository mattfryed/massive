#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Massive.Enemies;
using Massive.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Focused physics regression checks in a disposable scene, preserving the open lab.</summary>
[InitializeOnLoad]
public static partial class EnemyBaselineValidation
{
    private const string Key = "EnemyBaselineValidation.", Folder = "Library/EnemyBaselineValidation";
    private static IEnumerator routine;
    private static float resumeAt;
    private static int frame;
    private static double deadline;
    private static readonly List<string> results = new(), errors = new();
    private static readonly List<Object> owned = new();
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static EnemyBaselineValidation()
    {
        EditorApplication.playModeStateChanged += State;
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(Folder + "/run.request") || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Folder + "/run.request");
            EnemyBaselineIntegration.Integrate(); Run();
        };
    }
    [MenuItem("MASSIVE/Enemies/Validate Baseline Fixes")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        var original = SceneManager.GetActiveScene();
        string path = "Assets/EnemyBaselineValidation-" + Guid.NewGuid().ToString("N") + ".unity";
        var fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(fixture);
            var camera = new GameObject("Validation Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
            camera.transform.SetPositionAndRotation(Vector3.up * 20f, Quaternion.Euler(90, 0, 0)); camera.orthographic = true;
            camera.gameObject.AddComponent<AudioListener>();
            EditorSceneManager.SaveScene(fixture, path);
        }
        finally { EditorSceneManager.CloseScene(fixture, true); SceneManager.SetActiveScene(original); }
        Directory.CreateDirectory(Folder); File.WriteAllText(Folder + "/report.txt", "RUNNING\n");
        SessionState.SetString(Key + "Previous", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetString(Key + "Scene", path); SessionState.SetBool(Key + "Pending", true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        EditorApplication.isPlaying = true;
    }
    private static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key + "Pending", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            results.Clear(); errors.Clear(); owned.Clear(); frame = 0; resumeAt = 0f;
            deadline = EditorApplication.timeSinceStartup + 160;
            SessionState.SetBool(Key + "Background", Application.runInBackground); Application.runInBackground = true;
            routine = Checks(); Application.logMessageReceived += Log; EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
            Application.runInBackground = SessionState.GetBool(Key + "Background", true);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key + "Previous", ""));
            string path = SessionState.GetString(Key + "Scene", "");
            if (path.StartsWith("Assets/EnemyBaselineValidation-", StringComparison.Ordinal)) AssetDatabase.DeleteAsset(path);
            SessionState.SetBool(Key + "Pending", false);
        }
    }
    private static void Log(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || routine == null || Time.frameCount <= frame || Time.time < resumeAt) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timed out");
            if (routine.MoveNext()) { frame = Time.frameCount; resumeAt = Time.time + (routine.Current is float seconds ? seconds : 0f); }
            else Finish(null);
        }
        catch (Exception exception) { Finish(exception); }
    }
    private static void Finish(Exception exception)
    {
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log; routine = null;
        bool passed = exception == null && errors.Count == 0;
        File.WriteAllText(Folder + "/report.txt", (passed ? "PASSED" : "FAILED") + "\n" + string.Join("\n", results) + "\n" + exception + "\n" + string.Join("\n", errors));
        Debug.Log("[Enemy baseline validation] " + (passed ? "PASSED" : "FAILED: " + exception));
        foreach (var item in owned) if (item) Object.Destroy(item);
        EditorApplication.isPlaying = false;
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        results.Add("PASS " + message); File.WriteAllText(Folder + "/report.txt", "RUNNING\n" + string.Join("\n", results));
    }
    private static T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
    private static void Field(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static void Property(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
    private static void Place(PlayerControllerScript p, Vector3 position)
    { p.transform.position = position; p.GetComponent<Rigidbody>().position = position; Physics.SyncTransforms(); }
    private static GameObject Prefab(string path, Vector3 position)
    { return Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), position, Quaternion.identity)); }
    private static PlayerControllerScript Player()
    {
        var go = Own(new GameObject("Baseline player")); go.SetActive(false); go.tag = "Player";
        var rb = go.AddComponent<Rigidbody>(); rb.useGravity = false; rb.constraints = RigidbodyConstraints.FreezeAll;
        go.AddComponent<SphereCollider>().radius = .25f;
        var p = go.AddComponent<PlayerControllerScript>(); p.playerID = 0; p.teamID = 1;
        p.SetControlMode(PlayerControlMode.Scripted);
        Field(p, "playMatchSpawnOnSceneLoad", false); Field(p, "showPlayerIdToastOnMatchStart", false);
        Field(p, "respawnFxSeconds", .2f); Field(p, "respawnDelaySeconds", .1f); Field(p, "respawnInvulnSeconds", .4f);
        go.SetActive(true); return p;
    }
    private static IEnumerator Checks()
    {
        var p = Player(); yield return .1f;
        var actors = new List<MonoBehaviour>();
        string[] paths = { DronePrototypeSetup.PrefabPath, RangedDroneSetup.PrefabPath,
            ParticleBeamTurretSetup.PrefabPath, SeekerSetup.PrefabPath, DysonRepulsorSetup.PrefabPath };
        for (int i = 0; i < paths.Length; i++)
        {
            var go = Prefab(paths[i], new Vector3(-30f - i * 4f, 0, 0));
            MonoBehaviour actor = go.GetComponent<DroneController>();
            if (!actor) actor = go.GetComponent<ParticleBeamTurretController>();
            if (!actor) actor = go.GetComponent<SeekerController>();
            if (!actor) actor = go.GetComponent<DysonSphereRepulsorController>();
            actor.enabled = false;
            var def = (EnemyDefinition)actor.GetType().GetField("definition").GetValue(actor);
            go.GetComponent<EnemyBase>().Init(def, null); actors.Add(actor);
        }
        bool AllEligible() => actors.All(a => (bool)Call(a, a is DroneController ? "IsValidTarget" : "Eligible", p));
        bool NoneEligible() => actors.All(a => !(bool)Call(a, a is DroneController ? "IsValidTarget" : "Eligible", p));
        Check(AllEligible(), "All five targeting controllers accept a live opposing player");
        p.StartCoroutine((IEnumerator)Call(p, "MatchSpawnRoutine"));
        Check(p.IsSpawning && !p.temporarilyEliminated && NoneEligible(), "Initial match spawn is excluded even though temporarilyEliminated is false");
        while (p.IsSpawning) yield return null;
        Check(p.IsInvulnerable && NoneEligible(), "Post-spawn protection prevents target acquisition");
        while (p.IsInvulnerable) yield return null;
        Check(AllEligible(), "Targeting resumes after spawn protection expires");
        bool respawnProtected = false;
        p.RespawnStarted += _ => respawnProtected = p.IsSpawning && NoneEligible();
        p.ApplyExternalMassDelta(-2f, null);
        Check(p.temporarilyEliminated && NoneEligible(), "Death immediately removes player eligibility");
        while (p.temporarilyEliminated) yield return null;
        Check(respawnProtected && p.IsInvulnerable && NoneEligible(), "Normal death/respawn animation and its protection both exclude targets");
        while (p.IsInvulnerable) yield return null;
        Check(AllEligible(), "All controllers recover eligibility after normal respawn");

        var turret = (ParticleBeamTurretController)actors[2];
        turret.mount = ParticleBeamTurretController.WallMount.Authored;
        turret.transform.SetPositionAndRotation(new Vector3(0, 0, -3f), Quaternion.identity);
        turret.firingPivot.localRotation = Quaternion.identity; turret.beamRange = 8f;
        Property(turret, "PhaseAge", .7f);
        Place(p, turret.BeamOrigin + turret.BeamDirection * 3f);
        Field(p, "_invulnUntil", Time.time + 5f); float mass = p.massScore;
        Call(turret, "FireBeam", .1f);
        Check(p.massScore == mass, "Active turret beam respects player invulnerability");
        Field(p, "_invulnUntil", 0f); Call(turret, "FireBeam", .1f);
        Check(p.massScore < mass, "The same visible beam damages an unprotected player");
        Property(turret, "Target", p); Field(p, "_matchSpawning", true);
        Property(turret, "SpawnAge", 10f); Property(turret, "Phase", ParticleBeamTurretController.AttackPhase.Firing);
        Call(turret, "FixedUpdate");
        Check(!turret.Target, "A firing turret immediately drops a player who starts spawning");
        Field(p, "_matchSpawning", false);
        Place(p, new Vector3(25f, 0f, 0f));
        BeamChecks(turret);
        turret.gameObject.SetActive(false);

        var drone = (DroneController)actors[0]; var enemy = drone.GetComponent<EnemyBase>();
        drone.transform.position = Vector3.zero; drone.GetComponent<Rigidbody>().position = Vector3.zero;
        var attack = p.gameObject.AddComponent<PlayerAttackController>(); p.attackController = attack;
        var so = new SerializedObject(attack); so.FindProperty("attackProfile").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<PlayerAttackProfile>("Assets/Scripts/Player/Actions/PlayerAttackProfile.asset"); so.ApplyModifiedPropertiesWithoutUndo();
        attack.BeginAttack(); Check(attack.IsAttacking, "Player performs a real attack stage");
        var weapon = Own(new GameObject("Actual melee hitbox")); weapon.SetActive(false); weapon.transform.SetParent(p.transform, false);
        var hitbox = weapon.AddComponent<SphereCollider>(); hitbox.radius = .15f;
        var melee = weapon.AddComponent<PlayerMelee>(); Field(melee, "gateHitboxToActivationWindow", false);
        weapon.transform.position = new Vector3(1f, 0, 0); weapon.SetActive(true); Physics.SyncTransforms(); yield return .08f;
        Check(!enemy.IsDead && enemy.HealthRemaining == enemy.Definition.healthMassEq, "A missed attack with no physical hitbox overlap cannot kill a Drone");
        weapon.transform.position = drone.transform.position; Physics.SyncTransforms(); yield return .08f;
        Check(enemy.IsDead, "Actual player melee hitbox contact kills the one-hit Drone");
        weapon.SetActive(false); attack.CancelAttack();
        foreach (var actor in actors) if (actor) actor.gameObject.SetActive(false);
        p.gameObject.SetActive(false);
        var carrierChecks = CarrierChecks(); while (carrierChecks.MoveNext()) yield return carrierChecks.Current;
        var canonical = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DysonRepulsorSetup.DefinitionPath);
        Check(canonical.prefab.GetComponent<DysonSphereRepulsorController>() && !canonical.prefab.GetComponent<DysonSphereController>()
            && canonical.healthMassEq == 20f && canonical.defeatRewardKey == "ENEMY_DYSON_DEFEAT",
            "Canonical Dyson spawns only the Repulsor controller with preserved health and scoring");
        foreach (string guid in AssetDatabase.FindAssets("t:EnemySpawnProfile"))
        {
            var profile = AssetDatabase.LoadAssetAtPath<EnemySpawnProfile>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var rule in profile.rules)
                if (rule.enemy && rule.enemy.name.Contains("Dyson")) Check(rule.enemy == canonical, "Dyson spawn rule uses canonical definition: " + profile.name);
        }
        Check(errors.Count == 0, "No runtime errors during focused regression checks");
    }
}
#endif
