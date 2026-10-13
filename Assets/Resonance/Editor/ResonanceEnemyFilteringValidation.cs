using System;
using System.Collections;
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

/// <summary>Native collision and movement-policy checks in an isolated physics scene.</summary>
public static class ResonanceEnemyFilteringValidation
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Call(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, Private).Invoke(target, args);
    static void Check(bool condition, string name, List<string> passed)
    { if (!condition) throw new InvalidOperationException(name); passed.Add(name); }

    [MenuItem("MASSIVE/Resonance/Validate Enemy Filtering")]
    public static void RunMenu() => Debug.Log(Run());

    public static string Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run in Edit Mode.");
        var scene = EditorSceneManager.NewPreviewScene();
        var physics = scene.GetPhysicsScene();
        var roots = new List<GameObject>();
        var definitions = new List<ResonancePatternDefinition>();
        var drivers = new List<ResonanceInteractionDriver>();
        var passed = new List<string>();
        try
        {
            GameObject Root(string name)
            { var go = new GameObject(name); roots.Add(go); SceneManager.MoveGameObjectToScene(go, scene); return go; }
            ResonancePatternController Pattern(int layer = 0)
            {
                var def = ScriptableObject.CreateInstance<ResonancePatternDefinition>(); definitions.Add(def);
                def.showCenter = false;
                def.curves = new List<ResonanceCurve> { new ResonanceCurve { kind = ResonanceCurveKind.ClosedPolyline,
                    points = new List<Vector2> { new Vector2(0, -4), new Vector2(0, 4) } } };
                def.arcs = new List<ResonanceArc> { new ResonanceArc { startDegrees = 0, sweepDegrees = 180,
                    behavior = ResonanceBehavior.HardWall, thickness = .4f, colliderThickness = .3f, endTaperFraction = 0 } };
                var p = Root("Filter pattern").AddComponent<ResonancePatternController>();
                p.definition = def; p.obstacleLayer = layer;
                p.segmentMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resonance/Resonance Ribbon.mat");
                p.playerResponse = ResonancePlayerResponse.HardWall;
                p.sludgeMovementMultiplier = .65f; p.sludgeDrag = 8; p.interactionReach = .1f; p.sampleDegrees = 10;
                p.globalVisualProfile.enabled = p.globalCollisionProfile.enabled = false;
                p.Rebuild(); return p;
            }
            ResonanceInteractionDriver Driver(ResonancePatternController p)
            { var d = p.gameObject.AddComponent<ResonanceInteractionDriver>(); d.Initialize(p); drivers.Add(d); return d; }
            Rigidbody Body(string name, float z = 0)
            {
                var go = Root(name); go.transform.position = new Vector3(-2, 0, z);
                var c = go.AddComponent<SphereCollider>(); c.radius = .2f;
                var body = go.AddComponent<Rigidbody>(); body.useGravity = false;
                body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezePositionZ;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                return body;
            }
            void Place(Rigidbody body, Vector3 position, Vector3 velocity)
            { body.position = position; body.transform.position = position; body.linearVelocity = velocity; Physics.SyncTransforms(); }
            void Simulate(int steps) { for (int i = 0; i < steps; i++) physics.Simulate(.01f); }
            object Actor(ResonanceInteractionDriver driver, Rigidbody body, EnemyBase enemy) =>
                Call(driver, "GetActor", body, null, null, enemy);
            int Pairs(object actor) => ((IList)actor.GetType().GetField("ignored").GetValue(actor)).Count;
            void Refresh(ResonancePatternController p) => Call(p, "RefreshEnemyCollisionLayer");

            int layer = ResonanceCollisionPolicy.Layer, mask = ResonanceCollisionPolicy.Mask;
            Check(layer == 13 && mask != 0, "Reserved layer is configured in TagManager", passed);
            Check(Enumerable.Range(0, 32).All(i => Physics.GetIgnoreLayerCollision(0, i) == Physics.GetIgnoreLayerCollision(layer, i)),
                "Reserved layer preserves Default's global collision permissions", passed);
            var pattern = Pattern(); var driver = Driver(pattern);
            var walls = pattern.InteractiveSegments.SelectMany(s => s.HardColliders).ToArray();
            Check(walls.Length > 0 && walls.All(c => c.gameObject.layer == layer && !c.isTrigger),
                "Actual generated solid walls use the enemy pass-through layer", passed);
            Check(pattern.GetComponentsInChildren<Renderer>().All(r => r.gameObject.layer == 0),
                "Renderers retain their original layer", passed);
            var body = Body("Enemy", 0); var enemy = body.gameObject.AddComponent<EnemyBase>();
            int original = 1 << 5; body.excludeLayers = original;
            var actor = Actor(driver, body, enemy); Call(driver, "ConfigureFilters", actor);
            Check((body.excludeLayers.value & mask) != 0 && (body.excludeLayers.value & original) != 0,
                "Enemy body gains the exclusion without losing existing bits", passed);
            Check(Pairs(actor) == 0 && walls.All(w => !Physics.GetIgnoreCollision(body.GetComponent<Collider>(), w)),
                "Enemy creates zero native ignored pairs", passed);
            var child = new GameObject("Late compound collider"); child.transform.SetParent(body.transform, false);
            child.transform.localPosition = Vector3.forward * .5f; child.AddComponent<SphereCollider>().radius = .2f;
            Place(body, new Vector3(-2, 0, 0), Vector3.right * 10); Simulate(50);
            Check(body.position.x > 2, "Enemy and a late-added compound collider physically cross the hard wall", passed);
            var ordinaryBody = Body("Unfiltered body", 2);
            Place(ordinaryBody, new Vector3(-2, 0, 2), Vector3.right * 10); Simulate(50);
            Check(ordinaryBody.position.x < 0, "Unfiltered bodies remain physically blocked by the same wall", passed);
            ordinaryBody.gameObject.SetActive(false);

            Place(body, Vector3.zero, Vector3.right * 2); Call(driver, "Tick", actor);
            Check(Mathf.Abs(enemy.ExternalMovementMultiplier - .65f) < .001f && body.linearVelocity.x < 2,
                "Enemy receives the same full-contact sludge multiplier and drag", passed);
            Place(body, new Vector3(4, 0, 0), Vector3.zero); Call(driver, "Tick", actor);
            Check(enemy.ExternalMovementMultiplier == 1, "Leaving the field restores enemy movement", passed);
            var second = Pattern(); second.sludgeMovementMultiplier = .8f; var secondDriver = Driver(second);
            var secondActor = Actor(secondDriver, body, enemy);
            Place(body, Vector3.zero, Vector3.zero); Call(driver, "Tick", actor); Call(secondDriver, "Tick", secondActor);
            Check(Mathf.Abs(enemy.ExternalMovementMultiplier - .65f) < .001f, "Overlapping fields keep the strongest sludge", passed);
            Call(driver, "OnDisable");
            Check(Mathf.Abs(enemy.ExternalMovementMultiplier - .8f) < .001f, "Removing one field preserves the other slowdown", passed);
            Call(secondDriver, "OnDisable");
            Check(enemy.ExternalMovementMultiplier == 1, "Removing the final field restores full movement", passed);
            second.SetInteractionEnabled(false);

            var avoidance = body.gameObject.AddComponent<EnemyObstacleAvoidance>();
            Check(!avoidance.ShouldAvoid(walls[0]) && (avoidance.ObstacleMask.value & mask) == 0,
                "Steering ignores the pass-through layer before collecting hits", passed);
            var carrier = body.gameObject.AddComponent<CarrierController>();
            Check(!(bool)Call(carrier, "BlocksSidestep", walls[0], Vector3.zero, .2f),
                "Carrier sidesteps also ignore sludge walls", passed);
            var solid = Root("Ordinary wall"); solid.transform.position = new Vector3(2, 0, 0);
            var box = solid.AddComponent<BoxCollider>(); box.size = new Vector3(.2f, 3, 6);
            Check(avoidance.ShouldAvoid(box) && (bool)Call(carrier, "BlocksSidestep", box, Vector3.zero, .2f),
                "Steering and Carrier sidesteps still detect ordinary walls", passed);
            Place(body, new Vector3(-2, 0, 0), Vector3.right * 10); Simulate(70);
            Check(body.position.x > 0 && body.position.x < 2,
                "Enemy crosses resonance then collides with a normal wall behind it", passed);
            // Exercise a query more crowded than Seeker's 64-hit safety buffer.
            for (int i = 0; i < 80; i++)
            {
                var cell = Root("Dense resonance query fixture"); cell.layer = layer;
                cell.transform.position = new Vector3(0, 0, i * .001f); cell.AddComponent<BoxCollider>().size = Vector3.one;
            }
            Physics.SyncTransforms(); var hits = new Collider[64];
            int count = physics.OverlapSphere(Vector3.zero, 5, hits, ResonanceCollisionPolicy.EnemyQueryMask(~0), QueryTriggerInteraction.Collide);
            Check(count < hits.Length && hits.Take(count).Contains(box) && hits.Take(count).All(c => c.gameObject.layer != layer),
                "Dense resonance cannot fill enemy query buffers or hide an ordinary wall", passed);

            pattern.enemiesUseSludge = false; Refresh(pattern);
            Check(walls.All(w => w.gameObject.layer == 0) && avoidance.ShouldAvoid(walls[0]),
                "Disabling enemy sludge restores authored wall layer and avoidance", passed);
            Place(body, new Vector3(-2, 0, -2), Vector3.right * 10); Simulate(35);
            Check(body.position.x < 0, "Enemy hard-wall behavior is preserved when sludge is off", passed);
            pattern.enemiesUseSludge = true; Refresh(pattern);
            actor = Actor(driver, body, enemy); Call(driver, "ConfigureFilters", actor);
            Check(Pairs(actor) == 0 && walls.All(w => w.gameObject.layer == layer),
                "Re-enabling sludge resumes pair-free filtering", passed);

            body.excludeLayers = body.excludeLayers.value | (1 << 9);
            Call(enemy, "OnDisable");
            Check((body.excludeLayers.value & mask) == 0 && (body.excludeLayers.value & (original | 1 << 9)) == (original | 1 << 9),
                "Enemy disable releases only the owned exclusion bit", passed);
            Call(enemy, "OnEnable"); Check((body.excludeLayers.value & mask) != 0, "Pooled re-enable restores the exclusion", passed);
            Call(enemy, "OnDisable"); body.excludeLayers = body.excludeLayers.value | mask;
            Call(enemy, "OnEnable"); Call(enemy, "OnDisable");
            Check((body.excludeLayers.value & mask) != 0, "An authored resonance exclusion survives lifecycle cleanup", passed);

            var custom = Pattern(11); var customDriver = Driver(custom);
            var customWalls = custom.InteractiveSegments.SelectMany(s => s.HardColliders).ToArray();
            var customActor = Actor(customDriver, body, enemy); Call(customDriver, "ConfigureFilters", customActor);
            Check(customWalls.All(w => w.gameObject.layer == 11) && Pairs(customActor) > 0,
                "Custom obstacle layers retain compatible pair-filter fallback", passed);
            Call(customDriver, "OnDisable");
            Check(customWalls.All(w => !Physics.GetIgnoreCollision(body.GetComponent<Collider>(), w)),
                "Custom-layer fallback still restores its ignored pairs", passed);

            pattern.Rebuild();
            Check(pattern.InteractiveSegments.SelectMany(s => s.HardColliders).All(c => c.gameObject.layer == layer),
                "Pattern rebuild reapplies the reserved contact layer", passed);
            return passed.Count + " enemy-filter checks passed:\n" + string.Join("\n", passed);
        }
        finally
        {
            foreach (var driver in drivers) if (driver) Call(driver, "OnDisable");
            foreach (var root in roots) if (root) Object.DestroyImmediate(root);
            foreach (var def in definitions) if (def) Object.DestroyImmediate(def);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
