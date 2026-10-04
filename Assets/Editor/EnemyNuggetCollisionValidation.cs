#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Massive.Enemies;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Real prefab contacts in an isolated physics scene; never modifies the open level.</summary>
[InitializeOnLoad]
public static class EnemyNuggetCollisionValidation
{
    private const string Folder = "Library/EnemyNuggetCollisionValidation", Key = "EnemyNuggetCollisionValidation.Run";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly string[] Enemies = {
        "Melee/Drone/Enemy_Drone", "Ranged/Ranged Drone/Enemy_RangedDrone",
        "Melee/Carrier/Enemy_Carrier", "Ranged/Particle Beam Turret/Enemy_ParticleBeamTurret",
        "Melee/Seeker/Enemy_Seeker", "Melee/DysonSphere/Enemy_DysonSphere_Repulsor",
        "Melee/DysonSphere/Enemy_DysonSphere"
    };
    private static readonly string[] Pickups = {
        "Assets/Prefabs/Matter nugget.prefab",
        "Assets/Scripts/Anomalies/ORBITAL/Orbital Mass Nugget.prefab",
        "Assets/Scripts/Anomalies/ORBITAL/Mass Nugglet.prefab"
    };
    private static readonly List<string> results = new();
    private static int failures;
    static EnemyNuggetCollisionValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                EditorApplication.delayCall += Checks;
            if (state == PlayModeStateChange.EnteredEditMode) SessionState.SetBool(Key, false);
        };
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(Folder + "/run.request") || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Folder + "/run.request"); Run();
        };
    }
    [MenuItem("MASSIVE/Enemies/Validate Nugget Passage")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        Directory.CreateDirectory(Folder); File.WriteAllText(Folder + "/report.txt", "RUNNING\n");
        SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
    }
    private static void Check(bool pass, string label)
    { results.Add((pass ? "PASS " : "FAIL ") + label); if (!pass) failures++; }
    private static GameObject Clone(string path, Scene scene)
    {
        var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        SceneManager.MoveGameObjectToScene(go, scene);
        return go;
    }
    private static void EnableShapes(GameObject go)
    { foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = true; }
    private static void CheckPairs(EnemyBase enemy, MatterNuggetScript pickup, string label)
    {
        var shapes = enemy.GetComponentsInChildren<Collider>(true);
        var pickups = pickup.GetComponentsInChildren<Collider>(true);
        Check(shapes.Length > 0 && pickups.All(p => shapes.All(c => Physics.GetIgnoreCollision(p, c))), label);
    }
    private static void CheckPassage(PhysicsScene physics, MatterNuggetScript pickup, string label)
    {
        var rb = pickup.GetComponent<Rigidbody>(); rb.useGravity = false; rb.linearDamping = 0;
        pickup.Eject(new Vector3(-4, 0, 0), Vector3.right * 6, 20);
        Physics.SyncTransforms();
        for (int i = 0; i < 75; i++) physics.Simulate(.02f);
        Check(rb.position.x > 4.5f && Mathf.Abs(rb.linearVelocity.x - 6) < .01f,
            label + $" (end x={rb.position.x:F2}, speed={rb.linearVelocity.x:F2})");
    }
    private static void Checks()
    {
        results.Clear(); failures = 0;
        Scene scene = default;
        try
        {
            scene = SceneManager.CreateScene("Enemy nugget collision checks", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            var physics = scene.GetPhysicsScene();
            foreach (string enemyPath in Enemies)
            foreach (string pickupPath in Pickups)
            {
                var go = Clone("Assets/Enemy System/Enemy Types/" + enemyPath + ".prefab", scene);
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var enemy = go.GetComponent<EnemyBase>();
                foreach (var component in go.GetComponentsInChildren<MonoBehaviour>(true))
                    if (component != enemy) component.enabled = false;
                EnableShapes(go);
                var body = go.GetComponent<Rigidbody>(); body.isKinematic = true; body.position = Vector3.zero;
                var pickup = Clone(pickupPath, scene).GetComponent<MatterNuggetScript>();
                // Re-enable after moving into the isolated scene to exercise the scene-scoped registration.
                pickup.gameObject.SetActive(false); pickup.gameObject.SetActive(true);
                string label = go.name + " / " + pickup.name;
                CheckPairs(enemy, pickup, label + " contacts ignored on pickup arrival");
                foreach (var c in go.GetComponentsInChildren<Collider>(true)) { c.enabled = false; c.enabled = true; }
                CheckPairs(enemy, pickup, label + " contact policy survives spawn collider toggles");
                CheckPassage(physics, pickup, label + " physically passes through shell");

                var avoidance = go.GetComponent<EnemyObstacleAvoidance>();
                if (avoidance) Check(pickup.GetComponentsInChildren<Collider>().All(c => !avoidance.ShouldAvoid(c)), label + " movement probes ignore pickup");
                var carrier = go.GetComponent<CarrierController>();
                if (carrier) Check(pickup.GetComponentsInChildren<Collider>().All(c =>
                    !(bool)typeof(CarrierController).GetMethod("BlocksSidestep", Private).Invoke(carrier, new object[] { c, Vector3.zero, .7f })), label + " Carrier sidestep ignores pickup");
                var turret = go.GetComponent<ParticleBeamTurretController>();
                if (turret) Check(pickup.GetComponentsInChildren<Collider>().All(c =>
                    !(bool)typeof(ParticleBeamTurretController).GetMethod("CanBlock", Private).Invoke(turret, new object[] { c })), label + " turret beam ignores pickup");

                pickup.gameObject.SetActive(false); pickup.Eject(new Vector3(-4, 0, 0), Vector3.right * 6, 20);
                CheckPairs(enemy, pickup, label + " pooled pickup reuse");
                go.SetActive(false); go.SetActive(true); EnableShapes(go);
                CheckPairs(enemy, pickup, label + " enemy enabled after pickup");
                CheckPassage(physics, pickup, label + " passage after reuse");
                Object.DestroyImmediate(pickup.gameObject); Object.DestroyImmediate(go);
            }

            var shot = Clone("Assets/Enemy System/Enemy Types/Ranged/Ranged Drone/Ranged Drone Metaball Shot.prefab", scene).GetComponent<RangedDroneProjectile>();
            foreach (string path in Pickups)
            {
                var pickup = Clone(path, scene).GetComponent<MatterNuggetScript>();
                // Orbital pickups are authored inactive and become live through Eject.
                pickup.Eject(new Vector3(-4, 0, 0), Vector3.zero, 20);
                Check(pickup.GetComponentsInChildren<Collider>().All(c =>
                    !(bool)typeof(RangedDroneProjectile).GetMethod("CanHit", Private).Invoke(shot, new object[] { c })), pickup.name + " does not stop ranged shots");
                var players = Object.FindObjectsByType<PlayerControllerScript>(FindObjectsSortMode.None);
                Check(players.Length > 0 && players.SelectMany(p => p.GetComponentsInChildren<Collider>(true)).All(c =>
                    pickup.GetComponentsInChildren<Collider>().All(n => !Physics.GetIgnoreCollision(n, c))), pickup.name + " preserves player pickup contacts");
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                SceneManager.MoveGameObjectToScene(wall, scene); wall.transform.localScale = new Vector3(.3f, 5, 5);
                var wallShape = wall.GetComponent<Collider>();
                Check(pickup.GetComponentsInChildren<Collider>().All(c => !Physics.GetIgnoreCollision(c, wallShape)), pickup.name + " retains ordinary obstacle contacts");
                var rb = pickup.GetComponent<Rigidbody>(); rb.linearDamping = 0;
                pickup.Eject(new Vector3(-2, 0, 0), Vector3.right * 4, 20); Physics.SyncTransforms();
                for (int i = 0; i < 50; i++) physics.Simulate(.02f);
                Check(rb.position.x < -.2f && rb.linearVelocity.x < 0, pickup.name + " still bounces off walls");
                Object.DestroyImmediate(wall); Object.DestroyImmediate(pickup.gameObject);
            }
        }
        catch (Exception e) { failures++; results.Add("EXCEPTION " + e); }
        finally
        {
            if (scene.IsValid())
            {
                foreach (var go in scene.GetRootGameObjects()) Object.DestroyImmediate(go);
                SceneManager.UnloadSceneAsync(scene);
            }
            File.WriteAllText(Folder + "/report.txt", (failures == 0 ? "PASSED" : "FAILED") + $" ({results.Count} checks, {failures} failures)\n" + string.Join("\n", results));
            Debug.Log("[Enemy nugget collision] " + (failures == 0 ? "PASSED" : "FAILED") + $" ({results.Count} checks, {failures} failures)");
            EditorApplication.isPlaying = false;
        }
    }
}
#endif
