using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Massive.Enemies;
using Massive.Resonance;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Checks real collider-pair state in an isolated scene without advancing the open match.</summary>
public static class ResonanceCollisionCleanupValidation
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Call(ResonanceInteractionDriver driver, string method, params object[] args) =>
        typeof(ResonanceInteractionDriver).GetMethod(method, Private).Invoke(driver, args);
    static void Check(bool pass, string label, List<string> passed)
    { if (!pass) throw new Exception(label); passed.Add(label); }

    [MenuItem("MASSIVE/Resonance/Validate Collision Cleanup")]
    public static void RunMenu() { Debug.Log(Run()); }

    public static string Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run in Edit Mode.");
        var scene = EditorSceneManager.NewPreviewScene();
        var roots = new List<GameObject>();
        var definitions = new List<ResonancePatternDefinition>();
        var drivers = new List<ResonanceInteractionDriver>();
        var passed = new List<string>();
        try
        {
            ResonancePatternController Pattern()
            {
                var definition = ScriptableObject.CreateInstance<ResonancePatternDefinition>(); definitions.Add(definition);
                definition.Set345HzDefaults();
                definition.arcs = new List<ResonanceArc> { new ResonanceArc { startDegrees = -30, sweepDegrees = 60,
                    thickness = .3f, colliderThickness = .3f, behavior = ResonanceBehavior.HardWall } };
                var go = new GameObject("Collision cleanup fixture"); roots.Add(go); SceneManager.MoveGameObjectToScene(go, scene);
                var pattern = go.AddComponent<ResonancePatternController>(); pattern.definition = definition;
                pattern.segmentMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resonance/Resonance Ribbon.mat");
                pattern.playerResponse = ResonancePlayerResponse.Sludge; pattern.sampleDegrees = 10f;
                pattern.Rebuild();
                return pattern;
            }
            ResonanceInteractionDriver Driver(ResonancePatternController pattern)
            {
                var driver = pattern.gameObject.AddComponent<ResonanceInteractionDriver>(); drivers.Add(driver);
                driver.Initialize(pattern); return driver;
            }
            Rigidbody Body(string name, bool compound)
            {
                var go = new GameObject(name); roots.Add(go); SceneManager.MoveGameObjectToScene(go, scene);
                go.transform.position = new Vector3(20 + roots.Count * 2, 0, 0);
                var body = go.AddComponent<Rigidbody>(); body.isKinematic = true; go.AddComponent<SphereCollider>();
                if (compound)
                {
                    var child = new GameObject("Solid child"); child.transform.SetParent(go.transform, false); child.AddComponent<BoxCollider>();
                    var trigger = new GameObject("Sensor"); trigger.transform.SetParent(go.transform, false); trigger.AddComponent<SphereCollider>().isTrigger = true;
                }
                return body;
            }
            object Actor(ResonanceInteractionDriver driver, Rigidbody body, PlayerControllerScript player = null, EnemyBase enemy = null)
                => Call(driver, "GetActor", body, player, null, enemy);
            void Configure(ResonanceInteractionDriver driver, object actor) => Call(driver, "ConfigureFilters", actor);
            bool State(IEnumerable<Collider> colliders, IEnumerable<Collider> walls, bool ignored) =>
                colliders.All(c => walls.All(w => Physics.GetIgnoreCollision(c, w) == ignored));

            var pattern = Pattern(); var driver = Driver(pattern);
            var walls = pattern.InteractiveSegments.SelectMany(s => s.HardColliders).ToArray();
            Check(walls.Length > 2 && walls.All(c => c.enabled), "Fixture has active real hard-wall colliders", passed);
            var first = Body("Compound player", true); var second = Body("Other actor", false);
            var player = first.gameObject.AddComponent<PlayerControllerScript>();
            var firstSolids = first.GetComponentsInChildren<Collider>().Where(c => !c.isTrigger).ToArray();
            var sensor = first.GetComponentsInChildren<Collider>().Single(c => c.isTrigger);
            var secondCollider = second.GetComponent<Collider>();
            Physics.SyncTransforms(); Physics.IgnoreCollision(firstSolids[0], walls[0], true);
            var a = Actor(driver, first, player); var b = Actor(driver, second);
            Configure(driver, a); Configure(driver, b); Configure(driver, a);
            Check(State(firstSolids, walls, true) && State(new[] { secondCollider }, walls, true),
                "Repeated configuration gives every solid collider pass-through", passed);
            Check(State(new[] { sensor }, walls, false), "Trigger sensors never acquire hard-wall ignores", passed);
            player.SetMovementInfluence(driver, .3f); player.SetMovementInfluence(pattern, .6f);
            Call(driver, "Release", a);
            Check(firstSolids.All(c => walls.All(w => Physics.GetIgnoreCollision(c, w) == (c == firstSolids[0] && w == walls[0]))),
                "Release restores each prior pair value, including a pre-existing ignore", passed);
            Check(State(new[] { secondCollider }, walls, true), "Releasing one actor preserves another actor's filters", passed);
            Check(Mathf.Approximately(player.ExternalMovementMultiplier, .6f), "Release removes only this driver's movement influence", passed);
            Call(driver, "RestorePairs", a);
            Check(Physics.GetIgnoreCollision(firstSolids[0], walls[0]), "Repeated empty cleanup preserves prior ignores", passed);

            pattern.playerResponse = ResonancePlayerResponse.HardWall; Configure(driver, b);
            Check(State(new[] { secondCollider }, walls, false), "Response change immediately restores hard-wall collision", passed);
            pattern.playerResponse = ResonancePlayerResponse.PassThrough; Configure(driver, b);
            secondCollider.enabled = false; Configure(driver, b); secondCollider.enabled = true; Configure(driver, b);
            Check(State(new[] { secondCollider }, walls, true), "Re-enabled colliders reacquire their pass-through filters", passed);
            Object.DestroyImmediate(walls[1]);
            Call(driver, "OnDisable");
            Check(State(new[] { secondCollider }, walls.Where(w => w), false), "Shutdown tolerates destroyed wall colliders and restores survivors", passed);
            Call(driver, "OnDisable");
            Check(Mathf.Approximately(player.ExternalMovementMultiplier, .6f), "Repeated shutdown preserves an unrelated movement source", passed);
            player.RemoveMovementInfluence(pattern);

            // Distinct patterns can overlap spatially; neither owns the other's collider pairs.
            var left = Pattern(); var right = Pattern(); var leftDriver = Driver(left); var rightDriver = Driver(right);
            var leftWalls = left.InteractiveSegments.SelectMany(s => s.HardColliders).ToArray();
            var rightWalls = right.InteractiveSegments.SelectMany(s => s.HardColliders).ToArray();
            var body = Body("Shared actor", false); var collider = body.GetComponent<Collider>(); Physics.SyncTransforms();
            Configure(leftDriver, Actor(leftDriver, body)); Configure(rightDriver, Actor(rightDriver, body));
            Call(leftDriver, "OnDisable");
            Check(State(new[] { collider }, leftWalls, false) && State(new[] { collider }, rightWalls, true),
                "Stopping one overlapping pattern leaves the other pattern's filters intact", passed);
            Call(rightDriver, "OnDisable");
            Check(State(new[] { collider }, rightWalls, false), "Stopping the final pattern restores collision", passed);
            for (int cycle = 0; cycle < 3; cycle++)
            {
                Configure(leftDriver, Actor(leftDriver, body));
                Check(State(new[] { collider }, leftWalls, true), "Restart reacquires ignores " + cycle, passed);
                Call(leftDriver, "OnDisable");
                Check(State(new[] { collider }, leftWalls, false), "Restart cleanup restores collision " + cycle, passed);
            }
            var doomed = Body("Destroyed actor", false); var survivor = Body("Surviving actor", false);
            Configure(leftDriver, Actor(leftDriver, doomed)); Configure(leftDriver, Actor(leftDriver, survivor));
            Object.DestroyImmediate(doomed.gameObject); Call(leftDriver, "OnDisable");
            Check(State(new[] { survivor.GetComponent<Collider>() }, leftWalls, false),
                "Destroyed actors do not prevent cleanup of surviving actors", passed);
            return passed.Count + " collision-cleanup checks passed:\n" + string.Join("\n", passed);
        }
        finally
        {
            foreach (var driver in drivers) if (driver) Call(driver, "OnDisable");
            foreach (var root in roots) if (root) Object.DestroyImmediate(root);
            foreach (var definition in definitions) if (definition) Object.DestroyImmediate(definition);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
