#if UNITY_EDITOR
using System;
using System.Linq;
using Massive.Demonstrations;
using Massive.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class RangedDroneSetup
{
    public const string Folder = "Assets/Enemy System/Enemy Types/Ranged/Ranged Drone";
    public const string PrefabPath = Folder + "/Enemy_RangedDrone.prefab";
    public const string DefinitionPath = Folder + "/ED_RangedDrone.asset";
    public const string ProjectilePath = Folder + "/Ranged Drone Metaball Shot.prefab";
    public const string ProfilePath = Folder + "/ESP_RangedDroneTest.asset";

    [MenuItem("MASSIVE/Enemies/Ranged Drone/Create Prefab")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Create Ranged Drone in Edit Mode.");
        EnsureFolder(Folder);
        var normal = AssetDatabase.LoadAssetAtPath<GameObject>(DronePrototypeSetup.PrefabPath);
        var sourceDefinition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DronePrototypeSetup.DefinitionPath);
        if (!normal || !sourceDefinition) throw new InvalidOperationException("The canonical Drone is required.");
        var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DefinitionPath);
        if (!definition)
        {
            definition = Object.Instantiate(sourceDefinition); definition.name = "ED_RangedDrone";
            definition.id = "RANGED_DRONE"; definition.category = EnemyCategory.Ranged;
            definition.damageToPlayerMass01 = 0f;
            AssetDatabase.CreateAsset(definition, DefinitionPath);
        }
        var projectile = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePath);
        if (!projectile)
        {
            var go = new GameObject("Ranged Drone Metaball Shot"); go.layer = normal.layer;
            try
            {
                go.AddComponent<Rigidbody>().isKinematic = true;
                var collider = go.AddComponent<SphereCollider>(); collider.radius = .085f; collider.isTrigger = true;
                var shot = go.AddComponent<RangedDroneProjectile>();
                shot.speed = 5f; shot.lifetimeSeconds = 4f; shot.useRigidbody = false;
                shot.damageToPlayerMass01 = sourceDefinition.damageToPlayerMass01;
                shot.reflectable = true; shot.reflectedDamageToEnemyMassEq = sourceDefinition.damageTakenPerSwordHit;
                var volume = GameObject.CreatePrimitive(PrimitiveType.Cube); volume.name = "Metaball shot volume";
                Object.DestroyImmediate(volume.GetComponent<Collider>()); volume.layer = go.layer;
                volume.transform.SetParent(go.transform, false); volume.transform.localScale = Vector3.one * .65f;
                shot.volume = volume.GetComponent<Renderer>();
                shot.volume.sharedMaterial = normal.GetComponent<DroneVisuals>().engineRenderer.sharedMaterial;
                shot.volume.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; shot.volume.receiveShadows = false;
                projectile = PrefabUtility.SaveAsPrefabAsset(go, ProjectilePath);
            }
            finally { Object.DestroyImmediate(go); }
        }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (!prefab)
        {
            var root = PrefabUtility.LoadPrefabContents(DronePrototypeSetup.PrefabPath);
            try
            {
                root.name = "Enemy_RangedDrone";
                root.GetComponent<DroneController>().definition = definition;
                root.AddComponent<RangedDroneController>().projectilePrefab = projectile.GetComponent<RangedDroneProjectile>();
                root.GetComponent<DroneVisuals>().floatingFrontFaces = true;
                prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        // Closing prefab contents may unload the definition created earlier in this import pass.
        definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DefinitionPath);
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (definition.prefab != prefab) { definition.prefab = prefab; EditorUtility.SetDirty(definition); AssetDatabase.SaveAssetIfDirty(definition); }
        if (!AssetDatabase.LoadAssetAtPath<EnemySpawnProfile>(ProfilePath))
        {
            var profile = ScriptableObject.CreateInstance<EnemySpawnProfile>();
            profile.maxAliveTotal = 8; profile.maxAliveRanged = 8; profile.rules.Clear(); profile.batches.Clear();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }
        Selection.activeObject = prefab;
        Debug.Log("[Ranged Drone] Prefab, definition and metaball projectile ready; existing authored tuning preserved.");
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/'); string parent = path.Substring(0, slash);
        EnsureFolder(parent); AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
    }
    [MenuItem("MASSIVE/Enemies/Ranged Drone/Place In Player Actions")]
    public static void Place()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Place Ranged Drone in Edit Mode.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != CarrierPrototypeSetup.ScenePath) throw new InvalidOperationException("Open Player Actions first.");
        Install();
        var existing = Object.FindObjectsByType<RangedDroneController>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(x => x.gameObject.scene == scene);
        if (existing) { Selection.activeGameObject = existing.gameObject; return; }
        var owner = new GameObject("Ranged Drone Test"); SceneManager.MoveGameObjectToScene(owner, scene);
        Undo.RegisterCreatedObjectUndo(owner, "Place Ranged Drone test");
        var director = owner.AddComponent<EnemyDirector>();
        director.spawnProfile = AssetDatabase.LoadAssetAtPath<EnemySpawnProfile>(ProfilePath);
        director.arenaBounds = Object.FindObjectsByType<ArenaBoundsFromVectorGrid>(FindObjectsSortMode.None).FirstOrDefault(x => x.gameObject.scene == scene);
        director.enemyRoot = owner.transform; director.waitForScoring = true;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), owner.transform);
        go.GetComponent<RangedDroneController>().sceneDirector = director;
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.GetComponent<RangedDroneController>());
        foreach (var carrier in Object.FindObjectsByType<CarrierController>(FindObjectsSortMode.None).Where(x => x.gameObject.scene == scene))
        {
            var encounter = carrier.sceneDirector ? carrier.sceneDirector.gameObject : carrier.gameObject;
            Undo.RecordObject(encounter, "Focus the Ranged Drone encounter"); encounter.SetActive(false);
        }
        var gallery = Object.FindObjectsByType<PlayerDemoGallery>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(x => x.gameObject.scene == scene);
        if (gallery)
        {
            Undo.RecordObject(gallery, "Register Ranged Drone test with gallery pause");
            var so = new SerializedObject(gallery); var encounters = so.FindProperty("encounters");
            encounters.GetArrayElementAtIndex(encounters.arraySize++).objectReferenceValue = owner; so.ApplyModifiedProperties();
        }
        EditorSceneManager.MarkSceneDirty(scene); Selection.activeGameObject = go;
    }
}
#endif
