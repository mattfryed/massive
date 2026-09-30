#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Massive.Enemies;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class CarrierPresentationSetup
{
    public const string TelegraphPath = CarrierPrototypeSetup.Folder + "/Carrier Spawn Telegraph.prefab";

    [MenuItem("MASSIVE/Enemies/Carrier/Set Up Arrival and Launch Effects")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Configure Carrier presentation in Edit Mode.");
        var warning = AssetDatabase.LoadAssetAtPath<GameObject>(TelegraphPath);
        if (!warning) warning = CreateTelegraph();
        var collisionMesh = CreateCollisionMesh();
        var root = PrefabUtility.LoadPrefabContents(CarrierPrototypeSetup.PrefabPath);
        try
        {
            var visuals = root.GetComponent<CarrierVisuals>(); var carrier = root.GetComponent<CarrierController>();
            if (!visuals.assembly)
            {
                var assembly = new GameObject("Carrier assembly"); assembly.layer = root.layer;
                assembly.transform.SetParent(root.transform, false); visuals.assembly = assembly.transform;
                visuals.shell.transform.SetParent(assembly.transform, false);
                visuals.core.transform.SetParent(assembly.transform, false);
                foreach (var dock in carrier.docks) if (dock) dock.SetParent(assembly.transform, false);
            }
            if (!carrier.spawnTelegraphPrefab) carrier.spawnTelegraphPrefab = warning.GetComponent<EnemySpawnTelegraph>();
            var hullCollider = visuals.shell.GetComponent<MeshCollider>();
            if (!hullCollider) hullCollider = visuals.shell.gameObject.AddComponent<MeshCollider>();
            hullCollider.sharedMesh = collisionMesh; hullCollider.convex = true; hullCollider.isTrigger = false;
            carrier.shellCollider = hullCollider;
            if (!carrier.damageTrigger)
            {
                // Root Rigidbody callbacks include child hull contacts. Keep damage handling on its own trigger.
                var damage = new GameObject("Damage trigger"); damage.layer = root.layer;
                damage.transform.SetParent(root.transform, false);
                var trigger = damage.AddComponent<SphereCollider>(); trigger.radius = .775f;
                var rootTrigger = root.GetComponent<SphereCollider>();
                if (rootTrigger) EditorUtility.CopySerialized(rootTrigger, trigger);
                trigger.isTrigger = true;
                var hurtbox = damage.AddComponent<EnemyHurtbox>();
                var rootHurtbox = root.GetComponent<EnemyHurtbox>();
                if (rootHurtbox)
                {
                    EditorUtility.CopySerialized(rootHurtbox, hurtbox);
                    Object.DestroyImmediate(rootHurtbox);
                }
                if (rootTrigger) Object.DestroyImmediate(rootTrigger);
                carrier.damageTrigger = trigger;
            }
            var enemy = root.GetComponent<EnemyBase>();
            if (enemy.despawnDelaySeconds <= 0f) enemy.despawnDelaySeconds = .8f;
            visuals.RebuildGeometry();
            PrefabUtility.SaveAsPrefabAsset(root, CarrierPrototypeSetup.PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        Debug.Log("[Carrier] Shared arrival indicator, rotating assembly, launch fuel and face breakup configured.");
    }

    private static Mesh CreateCollisionMesh()
    {
        string path = CarrierPrototypeSetup.Folder + "/Carrier Collision Hull.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh) return mesh;
        CarrierHullGeometry.Create(out var vertices, out var faces);
        var triangles = new List<int>(132);
        foreach (var face in faces)
            for (int i = 1; i < face.Length - 1; i++)
            { triangles.Add(face[0]); triangles.Add(face[i]); triangles.Add(face[i + 1]); }
        mesh = new Mesh { name = "Carrier collision hull" };
        mesh.vertices = vertices; mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static GameObject CreateTelegraph()
    {
        string folder = CarrierPrototypeSetup.Folder;
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(folder + "/Carrier Spawn Outline.asset");
        if (!mesh)
        {
            var vertices = new List<Vector3>(); var tangents = new List<Vector4>();
            var uv = new List<Vector2>(); var indices = new List<int>();
            void Edge(Vector3 a, Vector3 b)
            {
                int n = vertices.Count; Vector3 direction = b - a;
                vertices.Add(a); vertices.Add(a); vertices.Add(b); vertices.Add(b);
                for (int k = 0; k < 4; k++) tangents.Add(new Vector4(direction.x, direction.y, direction.z, 1f));
                uv.Add(new Vector2(0f, -1f)); uv.Add(new Vector2(0f, 1f)); uv.Add(new Vector2(1f, -1f)); uv.Add(new Vector2(1f, 1f));
                indices.Add(n); indices.Add(n + 1); indices.Add(n + 2); indices.Add(n + 2); indices.Add(n + 1); indices.Add(n + 3);
            }
            CarrierHullGeometry.Create(out var points, out var faces);
            var edges = new HashSet<Vector2Int>();
            foreach (var face in faces)
                for (int i = 0; i < face.Length; i++)
                {
                    int a = face[i], b = face[(i + 1) % face.Length];
                    if (edges.Add(new Vector2Int(Mathf.Min(a, b), Mathf.Max(a, b)))) Edge(points[a], points[b]);
                }
            var drone = AssetDatabase.LoadAssetAtPath<GameObject>(DronePrototypeSetup.PrefabPath).GetComponent<DroneVisuals>();
            for (int i = 0; i < 6; i++)
            {
                CarrierHullGeometry.GetDockPose(i, out var position, out var rotation);
                Vector3 Pose(Vector3 p) => position + rotation * Vector3.Scale(p, drone.transform.localScale);
                for (int j = 0; j < 6; j++)
                {
                    float a = j * Mathf.PI / 3f, b = (j + 1) * Mathf.PI / 3f;
                    Vector3 p = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * drone.radius;
                    Vector3 q = new Vector3(Mathf.Cos(b), Mathf.Sin(b), 0f) * drone.radius;
                    Edge(Pose(p), Pose(q)); Edge(Pose(p), Pose(Vector3.forward * drone.noseLength));
                }
            }
            mesh = new Mesh { name = "Carrier spawn outline" };
            mesh.SetVertices(vertices); mesh.SetTangents(tangents); mesh.SetUVs(0, uv); mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds(); var bounds = mesh.bounds; bounds.Expand(.65f); mesh.bounds = bounds;
            AssetDatabase.CreateAsset(mesh, folder + "/Carrier Spawn Outline.asset");
        }
        var material = AssetDatabase.LoadAssetAtPath<Material>(folder + "/Carrier Spawn Glow.mat");
        if (!material)
        {
            material = new Material(Shader.Find("MASSIVE/EnemySpawnOutline")) { name = "Carrier Spawn Glow" };
            material.SetFloat("_GlowWidth", .055f); material.SetFloat("_Intensity", 1f); material.SetFloat("_Contrast", .13f);
            AssetDatabase.CreateAsset(material, folder + "/Carrier Spawn Glow.mat");
        }
        var go = new GameObject("Carrier Spawn Telegraph"); go.layer = LayerMask.NameToLayer("Enemy");
        try
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            var indicator = go.AddComponent<EnemySpawnTelegraph>();
            indicator.rotationAxis = Vector3.up; indicator.rollDegreesPerSecond = 8f; indicator.ghostOpacity = .14f;
            return PrefabUtility.SaveAsPrefabAsset(go, TelegraphPath);
        }
        finally { Object.DestroyImmediate(go); }
    }
}
#endif
