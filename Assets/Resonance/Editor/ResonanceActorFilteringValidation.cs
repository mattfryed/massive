using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Massive.Resonance;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Isolated native physics and ownership regressions for uniform actor filtering.</summary>
public static class ResonanceActorFilteringValidation
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    [MenuItem("MASSIVE/Resonance/Validate Actor Filtering")]
    public static void RunMenu() => Debug.Log(Run());
    public static string Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run in Edit Mode.");
        var scene = EditorSceneManager.NewPreviewScene(); var physics = scene.GetPhysicsScene();
        var roots = new List<GameObject>(); var definitions = new List<ResonancePatternDefinition>(); var passed = new List<string>();
        void Check(bool value, string name) { if (!value) throw new Exception(name); passed.Add(name); }
        GameObject Root(string name) { var go = new GameObject(name); roots.Add(go); SceneManager.MoveGameObjectToScene(go, scene); return go; }
        ResonancePatternController Pattern(int layer = 0)
        {
            var def = ScriptableObject.CreateInstance<ResonancePatternDefinition>(); definitions.Add(def); def.showCenter = false;
            def.curves = new List<ResonanceCurve> { new ResonanceCurve { kind = ResonanceCurveKind.ClosedPolyline,
                points = new List<Vector2> { new Vector2(0, -4), new Vector2(0, 4) } } };
            def.arcs = new List<ResonanceArc> { new ResonanceArc { startDegrees = 0, sweepDegrees = 180,
                behavior = ResonanceBehavior.HardWall, thickness = .4f, colliderThickness = .3f, endTaperFraction = 0 } };
            var p = Root("Actor filter pattern").AddComponent<ResonancePatternController>(); p.definition = def;
            p.segmentMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resonance/Resonance Ribbon.mat");
            p.playerResponse = ResonancePlayerResponse.Sludge; p.coreResponse = ResonanceCoreResponse.MagneticRepulsion;
            p.obstacleLayer = layer; p.sampleDegrees = 10; p.globalVisualProfile.enabled = p.globalCollisionProfile.enabled = false;
            p.Rebuild(); return p;
        }
        ResonanceInteractionDriver Driver(ResonancePatternController p) { var d = p.gameObject.AddComponent<ResonanceInteractionDriver>(); d.Initialize(p); return d; }
        Collider[] Walls(ResonancePatternController p) => p.InteractiveSegments.SelectMany(s => s.HardColliders).ToArray();
        object Actor(ResonanceInteractionDriver d, Rigidbody b) => Call(d, "GetActor", b, null, null, null);
        int Pairs(object a) => ((IList)a.GetType().GetField("ignored").GetValue(a)).Count;
        void Configure(ResonanceInteractionDriver d, object a) => Call(d, "ConfigureFilters", a);
        void Refresh(ResonancePatternController p) => Call(p, "RefreshEnemyCollisionLayer");
        Rigidbody Body(string name, float z = 0)
        {
            var go = Root(name); go.transform.position = new Vector3(-2, 0, z); go.AddComponent<SphereCollider>().radius = .2f;
            var b = go.AddComponent<Rigidbody>(); b.useGravity = false;
            b.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezePositionZ;
            b.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; return b;
        }
        void Cross(Rigidbody b, float z = 0)
        { b.position = new Vector3(-2, 0, z); b.transform.position = b.position; b.linearVelocity = Vector3.right * 10; Physics.SyncTransforms(); for (int i = 0; i < 50; i++) physics.Simulate(.01f); }
        try
        {
            int layer = ResonanceCollisionPolicy.ActorLayer, mask = ResonanceCollisionPolicy.ActorMask;
            Check(layer == 14 && mask != 0, "Actor contact layer configured");
            var p = Pattern(); var d = Driver(p); var walls = Walls(p);
            Check(walls.Length > 0 && walls.All(w => w.gameObject.layer == layer), "Eligible solid contacts use actor layer");
            Check(p.GetComponentsInChildren<Renderer>().All(r => r.gameObject.layer == 0), "Visual renderer layers unchanged");
            var b = Body("Compound actor"); var c = b.GetComponent<Collider>();
            var child = new GameObject("Compound solid"); child.transform.SetParent(b.transform, false); child.transform.localPosition = Vector3.forward * .5f;
            var c2 = child.AddComponent<SphereCollider>(); c2.radius = .2f;
            var sensor = b.gameObject.AddComponent<SphereCollider>(); sensor.isTrigger = true; sensor.radius = .6f;
            c.excludeLayers = 1 << 5; var a = Actor(d, b); Configure(d, a);
            Check(Pairs(a) == 0, "No native pairs for uniform policy");
            Check((c.excludeLayers.value & mask) != 0 && (c2.excludeLayers.value & mask) != 0, "All attached solid colliders filtered");
            Check(sensor.excludeLayers.value == 0 && b.excludeLayers.value == 0, "Sensors and Rigidbody exclusions unchanged");
            Check((c.excludeLayers.value & (1 << 5)) != 0, "Authored unrelated exclusion preserved");
            Cross(b); Check(b.position.x > 2, "Compound actor physically crosses resonance");
            var raw = Body("Unregistered body", 2); Cross(raw, 2); Check(raw.position.x < 0, "Unregistered body remains blocked"); raw.gameObject.SetActive(false);
            var ordinary = Root("Ordinary wall"); ordinary.transform.position = new Vector3(1, 0, 0); ordinary.AddComponent<BoxCollider>().size = new Vector3(.3f, 3, 6);
            Cross(b); Check(b.position.x > 0 && b.position.x < 1, "Filtered actor still collides with ordinary Default wall"); ordinary.SetActive(false);
            var p2 = Pattern(); var d2 = Driver(p2); var a2 = Actor(d2, b); Configure(d2, a2);
            Check(Pairs(a2) == 0, "Second eligible field uses shared exclusion");
            Call(d, "OnDisable");
            Check(walls.All(w => w.gameObject.layer == ResonanceCollisionPolicy.Layer), "Disabled driver's surviving walls restore their layer");
            Check((c.excludeLayers.value & mask) != 0, "Other field retains exclusion ownership");
            Cross(b); Check(b.position.x < 0, "Surviving disabled-driver walls physically block despite another filter owner");
            p.gameObject.SetActive(false);
            Cross(b); Check(b.position.x > 2, "Remaining active field stays traversable");
            c.excludeLayers = c.excludeLayers.value | (1 << 8); Call(d2, "OnDisable");
            Check(c.excludeLayers.value == ((1 << 5) | (1 << 8)) && c2.excludeLayers.value == 0, "Last release restores owned bits and preserves later unrelated edits");
            Call(d2, "OnDisable"); Check(c.excludeLayers.value == ((1 << 5) | (1 << 8)), "Repeated shutdown is harmless");
            p2.gameObject.SetActive(false);

            var p3 = Pattern(); var d3 = Driver(p3); c.excludeLayers = mask; var a3 = Actor(d3, b); Configure(d3, a3); Call(d3, "OnDisable");
            Check(c.excludeLayers.value == mask, "Authored actor exclusion survives final release"); c.excludeLayers = 0;
            d3.Initialize(p3); a3 = Actor(d3, b); Configure(d3, a3);
            c.enabled = false; Configure(d3, a3); Check(c.excludeLayers.value == 0, "Disabled solid releases its filter");
            c.enabled = true; Configure(d3, a3); Check((c.excludeLayers.value & mask) != 0, "Re-enabled solid reacquires its filter");
            p3.coreResponse = ResonanceCoreResponse.PatternDefault; Refresh(p3); Configure(d3, a3);
            Check(Walls(p3).All(w => w.gameObject.layer == ResonanceCollisionPolicy.Layer), "Mixed response returns contacts to enemy-only layer");
            Check(c.excludeLayers.value == 0 && Pairs(a3) > 0, "Mixed response retains native player fallback");
            Cross(b); Check(b.position.x > 2, "Fallback retains player pass-through behavior");
            p3.playerResponse = ResonancePlayerResponse.HardWall; Refresh(p3); Configure(d3, a3);
            Check(Pairs(a3) == 0, "Hard-wall policy restores all native pairs"); Cross(b); Check(b.position.x < 0, "Hard-wall policy physically blocks the actor");
            p3.playerResponse = ResonancePlayerResponse.Sludge; p3.coreResponse = ResonanceCoreResponse.MagneticRepulsion; Refresh(p3); Configure(d3, a3);
            Check(Pairs(a3) == 0 && (c.excludeLayers.value & mask) != 0, "Returning to uniform response reacquires shared filtering");
            p3.SetInteractionEnabled(false); Configure(d3, a3);
            Check(c.excludeLayers.value == 0, "Interaction gate releases shared filters");
            p3.SetInteractionEnabled(true); Refresh(p3); Configure(d3, a3);
            Check((c.excludeLayers.value & mask) != 0, "Interaction gate restores shared filters");
            Call(d3, "OnDisable"); p3.Rebuild(); d3.Initialize(p3); a3 = Actor(d3, b); Configure(d3, a3);
            Check(Walls(p3).All(w => w.gameObject.layer == layer) && Pairs(a3) == 0, "Rebuilt generation uses actor filtering");
            Object.DestroyImmediate(b.gameObject); Call(d3, "OnDisable");
            Check(true, "Destroyed compound actor releases without exceptions"); p3.gameObject.SetActive(false);

            var custom = Pattern(11); var dc = Driver(custom); var cb = Body("Custom layer actor"); var ca = Actor(dc, cb); Configure(dc, ca);
            Check(Walls(custom).All(w => w.gameObject.layer == 11) && Pairs(ca) > 0, "Custom obstacle layer preserves native fallback");
            var wall = Walls(custom)[0]; var cc = cb.GetComponent<Collider>(); Call(dc, "OnDisable");
            Physics.IgnoreCollision(cc, wall, true); dc.Initialize(custom); ca = Actor(dc, cb); Configure(dc, ca); Call(dc, "OnDisable");
            Check(Physics.GetIgnoreCollision(cc, wall), "Fallback preserves a pre-existing pair exception"); Physics.IgnoreCollision(cc, wall, false);
            custom.gameObject.SetActive(false);
            int matrixOther = 30; bool oldMatrix = Physics.GetIgnoreLayerCollision(layer, matrixOther);
            try { Physics.IgnoreLayerCollision(layer, matrixOther, !oldMatrix); var mismatch = Pattern(); var dm = Driver(mismatch); var am = Actor(dm, cb); Configure(dm, am);
                Check(Walls(mismatch).All(w => w.gameObject.layer != layer) && Pairs(am) > 0, "Mismatched global matrix rejects fast path"); Call(dm, "OnDisable"); }
            finally { Physics.IgnoreLayerCollision(layer, matrixOther, oldMatrix); }
        }
        finally
        {
            foreach (var go in roots) if (go) Object.DestroyImmediate(go);
            foreach (var def in definitions) if (def) Object.DestroyImmediate(def);
            EditorSceneManager.ClosePreviewScene(scene);
        }
        var owned = (IDictionary)typeof(ResonanceCollisionPolicy).GetField("exclusions", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        Check(owned.Count == 0, "All shared ownership released after scene cleanup");
        return "PASSED " + passed.Count + " actor-filter checks\n" + string.Join("\n", passed);
    }
}
