#if UNITY_EDITOR
using System;
using System.Linq;
using Massive.Demonstrations;
using Massive.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class CarrierPrototypeSetup
{
    public const string Folder = "Assets/Enemy System/Enemy Types/Melee/Carrier";
    public const string PrefabPath = Folder + "/Enemy_Carrier.prefab";
    public const string DefinitionPath = Folder + "/ED_Carrier.asset";
    public const string ProfilePath = Folder + "/ESP_CarrierTest.asset";
    public const string ScenePath = "Assets/Scenes/S-T_PLAYER-ACTIONS.unity";

    [MenuItem("MASSIVE/Enemies/Carrier/Create Prefab")]
    public static void Install()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Create Carrier assets in Edit Mode.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Enemy System/Enemy Types/Melee", "Carrier");
        var drone = AssetDatabase.LoadAssetAtPath<GameObject>(DronePrototypeSetup.PrefabPath);
        var droneDefinition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DronePrototypeSetup.DefinitionPath);
        if (!drone || !droneDefinition) throw new InvalidOperationException("The existing Drone prefab and definition are required.");
        var def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DefinitionPath);
        if (!def)
        {
            def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.id = "CARRIER"; def.category = EnemyCategory.Melee;
            def.defeatRewardKey = "ENEMY_DEFEAT"; def.healthMassEq = 6f;
            def.damageTakenPerSwordHit = 1f; def.damageToPlayerMass01 = 0f;
            def.spawnRadiusWorld = 1.25f; def.maxAliveOverride = 2; def.moveSpeed = 0f;
            AssetDatabase.CreateAsset(def, DefinitionPath);
        }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (!prefab)
        {
            var root = new GameObject("Enemy_Carrier"); root.SetActive(false); root.layer = LayerMask.NameToLayer("Enemy");
            try
            {
                var body = root.AddComponent<Rigidbody>(); body.useGravity = false; body.isKinematic = true; body.mass = 6f;
                body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
                var enemy = root.AddComponent<EnemyBase>();
                var damage = new GameObject("Damage trigger"); damage.layer = root.layer; damage.transform.SetParent(root.transform, false);
                var hitbox = damage.AddComponent<SphereCollider>(); hitbox.radius = .775f; hitbox.isTrigger = true;
                damage.AddComponent<EnemyHurtbox>();
                var reward = root.AddComponent<EnemyScoreReward>();
                reward.scoreToastPrefab = drone.GetComponent<EnemyScoreReward>().scoreToastPrefab;
                var controller = root.AddComponent<CarrierController>(); controller.definition = def; controller.droneDefinition = droneDefinition;
                controller.damageTrigger = hitbox;
                var visual = root.AddComponent<CarrierVisuals>();
                var shell = new GameObject("Truncated rhombicuboctahedron hull (open hexagonal crown)"); shell.layer = root.layer; shell.transform.SetParent(root.transform, false);
                visual.shell = shell.AddComponent<MeshFilter>();
                var renderer = shell.AddComponent<MeshRenderer>(); renderer.sharedMaterial = drone.GetComponent<DroneVisuals>().noseMesh.GetComponent<Renderer>().sharedMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                var core = Object.Instantiate(drone.GetComponent<DroneVisuals>().engineRenderer.gameObject, root.transform);
                core.name = "Exposed Drone-style core"; core.transform.localPosition = Vector3.up * .55f; core.transform.localRotation = Quaternion.identity; core.transform.localScale = Vector3.one * .8f;
                visual.core = core.GetComponent<Renderer>();
                for (int i = 0; i < 6; i++)
                {
                    float angle = i * 60f;
                    var dock = new GameObject("Dock " + (i + 1) + " — " + angle + " degrees"); dock.layer = root.layer;
                    dock.transform.SetParent(root.transform, false);
                    CarrierHullGeometry.GetDockPose(i, out var position, out var rotation);
                    dock.transform.SetLocalPositionAndRotation(position, rotation);
                    controller.docks[i] = dock.transform;
                    // Clone only the presentation hierarchy. No health, physics, swarm registration or score source in a bay.
                    var dv = drone.GetComponent<DroneVisuals>();
                    var display = new GameObject("Docked Drone (visual)"); display.layer = root.layer; display.transform.SetParent(dock.transform, false);
                    display.transform.localScale = drone.transform.localScale;
                    var shellCopy = Object.Instantiate(dv.shell.gameObject, display.transform);
                    var coreCopy = Object.Instantiate(dv.engineRenderer.gameObject, display.transform);
                    var copy = display.AddComponent<DroneVisuals>(); EditorUtility.CopySerialized(dv, copy);
                    copy.shell = shellCopy.transform; copy.noseMesh = shellCopy.GetComponent<MeshFilter>(); copy.engineRenderer = coreCopy.GetComponent<Renderer>();
                    copy.rollDegreesPerSecond = 0f;
                }
                root.SetActive(true); visual.RebuildGeometry();
                prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }
        if (def.prefab != prefab) { def.prefab = prefab; EditorUtility.SetDirty(def); AssetDatabase.SaveAssetIfDirty(def); }
        if (!AssetDatabase.LoadAssetAtPath<EnemySpawnProfile>(ProfilePath))
        {
            var profile = ScriptableObject.CreateInstance<EnemySpawnProfile>();
            profile.maxAliveTotal = 25; profile.maxAliveMelee = 25;
            profile.rules.Clear(); profile.batches.Clear();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }
        CarrierPresentationSetup.Configure();
        Selection.activeObject = prefab;
        Debug.Log("[Carrier] Prefab ready. Existing authored tuning is preserved.");
    }

    [MenuItem("MASSIVE/Enemies/Carrier/Apply 50-Face Hull")]
    public static void ApplyCompactHull()
    {
        CarrierPresentationSetup.Configure();
        SceneView.RepaintAll();
        Debug.Log("[Carrier] 50-face hull, open hexagonal crown and body-scale docking applied; existing tuning preserved.");
    }
    [MenuItem("MASSIVE/Enemies/Carrier/Place In Player Actions")]
    public static void PlaceInScene()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Place the Carrier in Edit Mode.");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) throw new InvalidOperationException("Open Player Actions before placing its Carrier test.");
        Install();
        var existing = Object.FindObjectsByType<CarrierController>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(x => x.gameObject.scene == scene);
        if (existing)
        {
            if (existing.sceneDirector) AddToGalleryEncounters(existing.sceneDirector.gameObject, scene);
            Selection.activeGameObject = existing.gameObject; return;
        }
        var owner = new GameObject("Carrier Test"); SceneManager.MoveGameObjectToScene(owner, scene);
        Undo.RegisterCreatedObjectUndo(owner, "Add Carrier test");
        var director = owner.AddComponent<EnemyDirector>();
        director.spawnProfile = AssetDatabase.LoadAssetAtPath<EnemySpawnProfile>(ProfilePath);
        director.arenaBounds = Object.FindObjectsByType<ArenaBoundsFromVectorGrid>(FindObjectsSortMode.None).FirstOrDefault(x => x.gameObject.scene == scene);
        director.enemyRoot = owner.transform; director.waitForScoring = true;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), owner.transform);
        go.transform.position = new Vector3(0f, 0f, 0f);
        go.GetComponent<CarrierController>().sceneDirector = director;
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.GetComponent<CarrierController>());
        var arena = Object.FindObjectsByType<PlayerActionTestArena>(FindObjectsSortMode.None).FirstOrDefault(x => x.gameObject.scene == scene);
        if (arena)
        {
            Undo.RecordObject(arena, "Use manual controls for Carrier test");
            var so = new SerializedObject(arena); so.FindProperty("mode").enumValueIndex = (int)PlayerActionTestArena.TestMode.Manual; so.ApplyModifiedProperties();
        }
        AddToGalleryEncounters(owner, scene);
        EditorSceneManager.MarkSceneDirty(scene); Selection.activeGameObject = go;
        if (SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.Frame(new Bounds(go.transform.position, Vector3.one * 7f), false);
    }
    private static void AddToGalleryEncounters(GameObject owner, Scene scene)
    {
        var gallery = Object.FindObjectsByType<PlayerDemoGallery>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(x => x.gameObject.scene == scene);
        if (!gallery) return;
        var so = new SerializedObject(gallery); var encounters = so.FindProperty("encounters");
        for (int i = 0; i < encounters.arraySize; i++) if (encounters.GetArrayElementAtIndex(i).objectReferenceValue == owner) return;
        Undo.RecordObject(gallery, "Pause Carrier during action gallery");
        int index = encounters.arraySize++; encounters.GetArrayElementAtIndex(index).objectReferenceValue = owner;
        so.ApplyModifiedProperties(); EditorSceneManager.MarkSceneDirty(scene);
    }
}
#endif
