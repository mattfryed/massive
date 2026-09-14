#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Massive.Enemies;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class DysonSpawnIndicatorSetup
{
    public const string Folder = "Assets/Enemy System/Enemy Types/Melee/DysonSphere";
    public const string PrefabPath = Folder + "/Enemy_DysonSphere.prefab";
    public const string DefinitionPath = Folder + "/ED_DysonSphere.asset";
    public const string TelegraphPath = Folder + "/Dyson Spawn Telegraph.prefab";

    [MenuItem("MASSIVE/Enemies/Dyson Sphere/Set Up Spawn Indicator and Score Toast")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode to configure spawn assets.");
        var dyson = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var panels = dyson.GetComponentInChildren<DysonSpherePanels>(true);
        string meshPath = Folder + "/Dyson Spawn Outline.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (mesh == null)
        {
            var points = new List<Vector3>(); var triangles = new List<int>();
            int subdivisions = 0; for (int faces = 20; faces < panels.maxPanels && subdivisions < 3; faces *= 4) subdivisions++;
            DysonSpherePanels.GenerateIcosphere(subdivisions, panels.radius, points, triangles);
            var vertices = new List<Vector3>(); var tangents = new List<Vector4>();
            var uv = new List<Vector2>(); var indices = new List<int>(); var edges = new HashSet<long>();
            for (int i = 0; i < triangles.Count; i += 3)
                for (int side = 0; side < 3; side++)
                {
                    int a = triangles[i + side], b = triangles[i + (side + 1) % 3];
                    long key = ((long)Mathf.Min(a, b) << 32) | (uint)Mathf.Max(a, b);
                    if (!edges.Add(key)) continue;
                    int n = vertices.Count; Vector3 direction = points[b] - points[a];
                    vertices.Add(points[a]); vertices.Add(points[a]); vertices.Add(points[b]); vertices.Add(points[b]);
                    for (int k = 0; k < 4; k++) tangents.Add(new Vector4(direction.x, direction.y, direction.z, 1f));
                    uv.Add(new Vector2(0, -1)); uv.Add(new Vector2(0, 1)); uv.Add(new Vector2(1, -1)); uv.Add(new Vector2(1, 1));
                    indices.Add(n); indices.Add(n + 1); indices.Add(n + 2); indices.Add(n + 2); indices.Add(n + 1); indices.Add(n + 3);
                }
            mesh = new Mesh { name = "Dyson spawn outline" };
            mesh.SetVertices(vertices); mesh.SetTangents(tangents); mesh.SetUVs(0, uv); mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds(); var bounds = mesh.bounds; bounds.Expand(.65f); mesh.bounds = bounds;
            AssetDatabase.CreateAsset(mesh, meshPath);
        }
        string materialPath = Folder + "/Dyson Spawn Glow.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("MASSIVE/EnemySpawnOutline")) { name = "Dyson Spawn Glow" };
            material.SetFloat("_GlowWidth", .055f);
            material.SetFloat("_Intensity", 1f); material.SetFloat("_Contrast", .13f);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        var indicator = AssetDatabase.LoadAssetAtPath<GameObject>(TelegraphPath);
        if (indicator == null)
        {
            var go = new GameObject("Dyson Spawn Telegraph"); go.layer = LayerMask.NameToLayer("Enemy");
            try
            {
                go.transform.localScale = Vector3.one;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                var warning = go.AddComponent<EnemySpawnTelegraph>();
                warning.rollDegreesPerSecond = 18f; warning.ghostOpacity = .14f;
                indicator = PrefabUtility.SaveAsPrefabAsset(go, TelegraphPath);
            }
            finally { Object.DestroyImmediate(go); }
        }
        var toast = AssetDatabase.LoadAssetAtPath<GameObject>(DronePrototypeSetup.ToastPath).GetComponent<EnemyScoreToast>();
        var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var reward = contents.GetComponent<EnemyScoreReward>();
            if (!reward) reward = contents.AddComponent<EnemyScoreReward>();
            if (!reward.scoreToastPrefab) reward.scoreToastPrefab = toast;
            PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
        var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DefinitionPath);
        foreach (string guid in AssetDatabase.FindAssets("t:EnemySpawnProfile"))
        {
            var profile = AssetDatabase.LoadAssetAtPath<EnemySpawnProfile>(AssetDatabase.GUIDToAssetPath(guid));
            bool changed = false;
            for (int i = 0; i < profile.rules.Count; i++)
            {
                var rule = profile.rules[i]; if (rule.enemy != definition || rule.telegraphPrefab) continue;
                rule.telegraphPrefab = indicator.GetComponent<EnemySpawnTelegraph>();
                rule.telegraphSeconds = 3f; rule.blockedSpawnTimeout = 2f; profile.rules[i] = rule; changed = true;
            }
            foreach (var rule in profile.batches)
            {
                if (rule == null || rule.enemy != definition || rule.telegraphPrefab) continue;
                rule.telegraphPrefab = indicator.GetComponent<EnemySpawnTelegraph>(); rule.telegraphSeconds = 3f; changed = true;
            }
            if (changed) { EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile); }
        }
    }
}
#endif
