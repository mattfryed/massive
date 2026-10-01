#if UNITY_EDITOR
using System;
using System.Linq;
using Massive.Demonstrations;
using Massive.Enemies;
using Massive.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class DysonRepulsorSetup
{
    public const string Folder = "Assets/Enemy System/Enemy Types/Melee/DysonSphere";
    public const string PrefabPath = Folder + "/Enemy_DysonSphere_Repulsor.prefab";
    public const string DefinitionPath = Folder + "/ED_DysonSphere.asset";
    public const string ProfilePath = Folder + "/ESP_DysonRepulsorTest.asset";
    [MenuItem("MASSIVE/Enemies/Dyson Repulsor/Ensure Current Prefab")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DefinitionPath);
        if (!definition) throw new InvalidOperationException("The canonical Dyson definition is missing.");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (!prefab)
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Enemy_DysonSphere.prefab"));
            try
            {
                root.name = "Enemy_DysonSphere_Repulsor";
                // A real prefab variant inherits the existing shell/core, health presentation and score wiring.
                Object.DestroyImmediate(root.GetComponent<DysonSphereController>());
                var panels = root.GetComponentInChildren<DysonSpherePanels>(true);
                panels.SetSpear01(0f); panels.SetStun01(0f);
                var rotator = panels.GetComponent<MirzaBeig.ParticleSystems.Rotator>(); if (rotator) rotator.enabled = false;
                var so = new SerializedObject(panels); so.FindProperty("generateInEditMode").boolValue = false; so.ApplyModifiedPropertiesWithoutUndo();
                root.GetComponent<EnemyBase>().despawnDelaySeconds = .8f;
                var avoid = root.GetComponent<EnemyObstacleAvoidance>(); avoid.filterDroneContacts = true;
                so = new SerializedObject(avoid); so.FindProperty("debugDraw").boolValue = false; so.ApplyModifiedPropertiesWithoutUndo();
                var c = root.AddComponent<DysonSphereRepulsorController>(); c.panels = panels; c.definition = definition;
                foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
                { var main = ps.main; main.scalingMode = ParticleSystemScalingMode.Hierarchy; main.simulationSpace = ParticleSystemSimulationSpace.Local; }
                c.hurtbox = root.GetComponentInChildren<EnemyHurtbox>(true).GetComponent<SphereCollider>();
                var volume = new GameObject("Repulsor damage sphere"); volume.layer = root.layer; volume.transform.SetParent(root.transform, false);
                c.attackVolume = volume.AddComponent<SphereCollider>(); c.attackVolume.isTrigger = true; c.attackVolume.enabled = false;
                var rippleObject = new GameObject("Repulsor grid ripple"); rippleObject.transform.SetParent(root.transform, false);
                c.ripple = rippleObject.AddComponent<PlayerRepulsorGridPulse>(); c.ripple.UseSharedSettings = false;
                c.ripple.intensity = .12f; c.ripple.duration = .45f; c.ripple.packetWidth = .55f;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }
        definition.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        EditorUtility.SetDirty(definition); AssetDatabase.SaveAssetIfDirty(definition);
        if (!AssetDatabase.LoadAssetAtPath<EnemySpawnProfile>(ProfilePath))
        {
            var profile = ScriptableObject.CreateInstance<EnemySpawnProfile>(); profile.rules.Clear(); profile.batches.Clear();
            profile.maxAliveTotal = profile.maxAliveMelee = 4; AssetDatabase.CreateAsset(profile, ProfilePath);
        }
    }
    [MenuItem("MASSIVE/Enemies/Dyson Repulsor/Place In Player Actions")]
    public static void Place()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != CarrierPrototypeSetup.ScenePath)
            throw new InvalidOperationException("Open Player Actions in Edit Mode.");
        Install(); var scene = SceneManager.GetActiveScene();
        var existing = Object.FindObjectsByType<DysonSphereRepulsorController>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(x => x.gameObject.scene == scene);
        if (existing) { Selection.activeGameObject = existing.gameObject; return; }
        var root = new GameObject("Dyson Repulsor Test"); Undo.RegisterCreatedObjectUndo(root, "Place Dyson test");
        var director = root.AddComponent<EnemyDirector>(); director.enemyRoot = root.transform; director.waitForScoring = true;
        director.spawnProfile = AssetDatabase.LoadAssetAtPath<EnemySpawnProfile>(ProfilePath);
        director.arenaBounds = Object.FindObjectsByType<ArenaBoundsFromVectorGrid>(FindObjectsSortMode.None).First(x => x.gameObject.scene == scene);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), root.transform);
        go.name = "Dyson Sphere — Repulsor Test"; go.transform.position = new Vector3(0f,0f,2.5f);
        var c = go.GetComponent<DysonSphereRepulsorController>(); c.sceneDirector = director; c.arenaBounds = director.arenaBounds;
        PrefabUtility.RecordPrefabInstancePropertyModifications(c); PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        foreach (var s in Object.FindObjectsByType<SeekerController>(FindObjectsSortMode.None).Where(x => x.gameObject.scene == scene))
        { var encounter = s.sceneDirector ? s.sceneDirector.gameObject : s.gameObject; Undo.RecordObject(encounter, "Focus Dyson encounter"); encounter.SetActive(false); }
        var gallery = Object.FindObjectsByType<PlayerDemoGallery>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(x => x.gameObject.scene == scene);
        if (gallery)
        {
            var so = new SerializedObject(gallery); var encounters = so.FindProperty("encounters");
            encounters.GetArrayElementAtIndex(encounters.arraySize++).objectReferenceValue = root; so.ApplyModifiedProperties();
        }
        EditorSceneManager.MarkSceneDirty(scene); Selection.activeGameObject = go;
    }
}
#endif
