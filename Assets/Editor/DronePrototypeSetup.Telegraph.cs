#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Massive.Enemies;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static partial class DronePrototypeSetup
{
    public const string TelegraphPath = Folder + "/Drone Spawn Telegraph.prefab";

    [MenuItem("MASSIVE/Enemies/Drone/Create Spawn Telegraph")]
    public static void CreateSpawnTelegraph()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Use Edit Mode to configure the warning.");
        var drone = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<DroneVisuals>();
        string meshPath = Folder + "/Drone Spawn Outline.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (mesh == null)
        {
            var vertices = new List<Vector3>(); var tangents = new List<Vector4>();
            var uv = new List<Vector2>(); var indices = new List<int>();
            var ring = new Vector3[6];
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI / 3f;
                ring[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * drone.radius;
            }
            Action<Vector3, Vector3> edge = (a, b) =>
            {
                int n = vertices.Count; Vector3 direction = b - a;
                vertices.Add(a); vertices.Add(a); vertices.Add(b); vertices.Add(b);
                for (int k = 0; k < 4; k++) tangents.Add(new Vector4(direction.x, direction.y, direction.z, 1f));
                uv.Add(new Vector2(0, -1)); uv.Add(new Vector2(0, 1)); uv.Add(new Vector2(1, -1)); uv.Add(new Vector2(1, 1));
                indices.Add(n); indices.Add(n+1); indices.Add(n+2);
                indices.Add(n+2); indices.Add(n+1); indices.Add(n+3);
            };
            for (int i = 0; i < 6; i++)
            {
                edge(ring[i], ring[(i + 1) % 6]);
                edge(ring[i], Vector3.forward * drone.noseLength);
                edge(ring[i], Vector3.back * drone.noseLength * drone.tailLengthRatio);
            }
            mesh = new Mesh { name = "Drone spawn outline — 18 soft edges" };
            mesh.SetVertices(vertices); mesh.SetTangents(tangents); mesh.SetUVs(0, uv); mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds(); var bounds = mesh.bounds; bounds.Expand(.65f); mesh.bounds = bounds;
            AssetDatabase.CreateAsset(mesh, meshPath);
        }
        string materialPath = Folder + "/Drone Spawn Glow.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            var shader = Shader.Find("MASSIVE/EnemySpawnOutline");
            if (shader == null) throw new InvalidOperationException("Import the spawn outline shader first.");
            material = new Material(shader) { name = "Drone Spawn Glow" };
            material.SetFloat("_GlowWidth", .09f);
            material.SetFloat("_Intensity", 1f); material.SetFloat("_Contrast", .2f);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TelegraphPath);
        if (prefab == null)
        {
            var go = new GameObject("Drone Spawn Telegraph"); go.layer = LayerMask.NameToLayer("Enemy");
            try
            {
                go.transform.localScale = Vector3.one;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                go.AddComponent<EnemySpawnTelegraph>();
                prefab = PrefabUtility.SaveAsPrefabAsset(go, TelegraphPath);
            }
            finally { Object.DestroyImmediate(go); }
        }
        var profile = AssetDatabase.LoadAssetAtPath<EnemySpawnProfile>(ProfilePath);
        var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DefinitionPath);
        foreach (var batch in profile.batches)
        {
            if (batch.enemy != definition || batch.telegraphPrefab != null) continue;
            batch.telegraphPrefab = prefab.GetComponent<EnemySpawnTelegraph>(); batch.telegraphSeconds = 3f;
            EditorUtility.SetDirty(profile);
        }
        AssetDatabase.SaveAssetIfDirty(profile);
    }
}
#endif
