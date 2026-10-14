#if UNITY_EDITOR
using System;
using System.Reflection;
using Massive.Cosmos;
using Massive.Player;
using Massive.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Exercises the production movement method in isolated physics scenes, without
// entering Play Mode, changing physics settings, or saving/replacing open scenes.
public static class PlayerAttackBoundaryValidation
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly MethodInfo Move = typeof(PlayerAttackController).GetMethod("Move", Private);

    [MenuItem("MASSIVE/COSMOS/Validate attack boundary containment")]
    static void MenuRun() => Debug.Log(Run());

    public static string Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run boundary validation outside Play Mode.");
        var source = EditorSceneManager.OpenPreviewScene("Assets/Scenes/S-9_COSMOS.unity");
        try
        {
            CosmosArenaLayout layout = null;
            foreach (var root in source.GetRootGameObjects())
                if (!layout) layout = root.GetComponentInChildren<CosmosArenaLayout>(true);
            if (!layout) throw new Exception("Saved COSMOS layout is missing.");
            layout.Rebuild();
            var bounds = layout.GetComponent<ArenaBoundsFromVectorGrid>();
            using (var fixture = new Fixture())
            {
                foreach (var wall in layout.Walls.GetComponentsInChildren<BoxCollider>())
                    fixture.CopyWall(wall);
                Vector3 edge = layout.transform.TransformPoint(new Vector3(0, -bounds.OutlineHalfHeightLocal(0), 0));
                edge.y = 0;
                Vector3 outward = layout.transform.TransformDirection(Vector3.down).normalized;
                Vector3 start = edge - outward * .4f;
                fixture.Reset(start, .25f);
                fixture.body.MovePosition(start + outward * 2);
                fixture.Step(.02f);
                fixture.Check(!bounds.ContainsWorldPoint(fixture.body.position), "Unswept baseline must reproduce the escape");
                fixture.Reset(start, .25f);
                fixture.body.linearVelocity = outward * 100;
                fixture.Step(.02f);
                fixture.Check(bounds.ContainsWorldPoint(fixture.body.position), "Continuous physics movement must hit the same wall");

                float halfX = bounds.Grid.size.x * .5f;
                foreach (float x in new[] { -halfX * .8f, -.1f, 0, .1f, halfX * .8f })
                foreach (int side in new[] { -1, 1 })
                foreach (float radius in new[] { .125f, .25f, .5f })
                foreach (float distance in new[] { .25f, 2f, 6f })
                {
                    edge = layout.transform.TransformPoint(new Vector3(x, side * bounds.OutlineHalfHeightLocal(x), 0));
                    edge.y = 0;
                    outward = layout.transform.TransformDirection(new Vector3(x / bounds.OvalHalfWidthLocal, side, 0)).normalized;
                    fixture.Reset(edge - outward * (radius + .2f), radius);
                    for (int i = 0; i < 8; i++) { fixture.MoveBy(outward * distance); fixture.Step(.02f); }
                    fixture.Check(bounds.ContainsWorldPoint(fixture.body.position), "Repeated attack movement escaped at x=" + x + ", side=" + side + ", radius=" + radius + ", step=" + distance);
                }
                foreach (int side in new[] { -1, 1 })
                {
                    edge = layout.transform.TransformPoint(new Vector3(side * halfX, 0, 0)); edge.y = 0;
                    outward = layout.transform.TransformDirection(Vector3.right * side);
                    fixture.Reset(edge - outward * .5f, .25f);
                    fixture.MoveBy(outward * 6); fixture.Step(.02f);
                    fixture.Check(bounds.ContainsWorldPoint(fixture.body.position), "Goal separator must remain solid");
                }

                var tuning = AssetDatabase.LoadAssetAtPath<PlayerTuningProfile>("Assets/Scripts/Settings/Resources/PlayerTuningProfile.asset");
                var stage = tuning.attackProfile.GetStage(0);
                edge = layout.transform.TransformPoint(new Vector3(0, -bounds.OutlineHalfHeightLocal(0), 0)); edge.y = 0;
                outward = layout.transform.TransformDirection(Vector3.down);
                foreach (int fps in new[] { 15, 30, 60, 120 })
                foreach (float charge in new[] { 1f, tuning.combat.thrustChargeMaxDistanceMultiplier })
                {
                    fixture.Reset(edge - outward * .6f, .25f);
                    float previous = 0, accumulator = 0;
                    for (int frame = 1; frame <= Mathf.CeilToInt(stage.Duration * fps); frame++)
                    {
                        float progress = stage.DistanceCurve.Evaluate(Mathf.Clamp01(frame / (fps * stage.Duration)));
                        fixture.MoveBy(outward * ((progress - previous) * stage.TravelDistance * charge));
                        previous = progress; accumulator += 1f / fps;
                        while (accumulator >= .02f) { fixture.Step(.02f); accumulator -= .02f; }
                        fixture.Check(bounds.ContainsWorldPoint(fixture.body.position), "Authored thrust curve escaped at " + fps + " FPS, charge=" + charge);
                    }
                }
                int boundaryChecks = fixture.checks;
                return "Attack boundary validation passed: " + boundaryChecks + " COSMOS checks; " + ValidateFiltering() +
                    " movement/filter checks. Unswept escape reproduced. No Play Mode or saved-scene changes.";
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(source); }
    }

    static int ValidateFiltering()
    {
        using (var f = new Fixture())
        {
            var wall = f.Box("solid wall", new Vector3(2, 0, 0), new Vector3(.3f, 4, 4));
            var ignored = f.Box("ignored collider", Vector3.right, new Vector3(.1f, 2, 2));
            f.Box("floor", Vector3.down * .3f, new Vector3(20, .1f, 20));
            Physics.IgnoreCollision(f.sphere, ignored);
            f.Reset(Vector3.zero, .25f); f.MoveBy(Vector3.right * 4); f.Step(.02f);
            f.Check(f.body.position.x > 1 && f.body.position.x < 1.61f, "Ignored pair must not obscure the wall behind it");
            wall.isTrigger = true;
            f.Reset(Vector3.zero, .25f); f.MoveBy(Vector3.right * 4); f.Step(.02f);
            f.Check(Mathf.Abs(f.body.position.x - 4) < .001f, "Triggers, ignored pairs and floor contact must not shorten clear travel");
            wall.isTrigger = false;
            f.Reset(new Vector3(1.59f, 0, 0), .25f);
            f.MoveBy(Vector3.forward); f.Step(.02f);
            f.Check(Mathf.Abs(f.body.position.z - 1) < .001f, "Motion along a wall must remain free");
            f.MoveBy(Vector3.left); f.Step(.02f);
            f.Check(f.body.position.x < .6f, "Motion away from a wall must remain free");
            foreach (float penetration in new[] { 0f, .0001f, .001f, .01f })
            {
                f.Reset(new Vector3(1.6f + penetration, 0, 0), .25f);
                f.MoveBy(Vector3.right * 6); f.Step(.02f);
                f.Check(f.body.position.x < 1.65f, "Attack starting in wall contact must not escape, penetration=" + penetration);
                f.Reset(new Vector3(1.6f + penetration, 0, 0), .25f);
                f.MoveBy(Vector3.left); f.Step(.02f);
                f.Check(f.body.position.x < .65f, "Starting contact must allow movement away from the wall");
            }
            return f.checks;
        }
    }

    sealed class Fixture : IDisposable
    {
        readonly Scene scene = EditorSceneManager.NewPreviewScene();
        public readonly Rigidbody body;
        public readonly SphereCollider sphere;
        readonly PlayerAttackController attack;
        public int checks;
        public Fixture()
        {
            var go = Create("Attack movement probe");
            sphere = go.AddComponent<SphereCollider>();
            body = go.AddComponent<Rigidbody>(); body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
            attack = go.AddComponent<PlayerAttackController>();
            typeof(PlayerAttackController).GetField("rb", Private).SetValue(attack, body);
            typeof(PlayerAttackController).GetField("movementCollider", Private).SetValue(attack, sphere);
        }
        GameObject Create(string name)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }
        public BoxCollider Box(string name, Vector3 position, Vector3 size)
        {
            var go = Create(name); go.transform.position = position;
            var c = go.AddComponent<BoxCollider>(); c.size = size; return c;
        }
        public void CopyWall(BoxCollider source)
        {
            var c = Box(source.name, source.transform.position, source.size);
            c.transform.rotation = source.transform.rotation;
            c.transform.localScale = source.transform.lossyScale; c.center = source.center;
        }
        public void Reset(Vector3 position, float radius)
        {
            sphere.radius = radius; body.position = position; body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero; Physics.SyncTransforms();
        }
        public void MoveBy(Vector3 displacement) => Move.Invoke(attack, new object[] { displacement });
        public void Step(float dt) => scene.GetPhysicsScene().Simulate(dt);
        public void Check(bool condition, string message)
        { if (!condition) throw new Exception(message); checks++; }
        public void Dispose() => EditorSceneManager.ClosePreviewScene(scene);
    }
}
#endif
