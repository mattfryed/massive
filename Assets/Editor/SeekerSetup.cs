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

public static class SeekerSetup
{
    public const string Folder = "Assets/Enemy System/Enemy Types/Melee/Seeker";
    public const string PrefabPath = Folder + "/Enemy_Seeker.prefab";
    public const string DefinitionPath = Folder + "/ED_Seeker.asset";
    public const string ProfilePath = Folder + "/ESP_SeekerTest.asset";
    public const string WarningPath = Folder + "/Seeker Spawn Telegraph.prefab";
    [MenuItem("MASSIVE/Enemies/Seeker/Create Prefab")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Create the Seeker in Edit Mode.");
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(DronePrototypeSetup.PrefabPath);
        var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DefinitionPath);
        if (!definition)
        {
            definition = Object.Instantiate(AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DronePrototypeSetup.DefinitionPath));
            definition.name = "ED_Seeker"; definition.id = "SEEKER"; definition.category = EnemyCategory.Melee;
            definition.moveSpeed = 3.8f; definition.turnSpeed = 280f; definition.spawnRadiusWorld = .85f;
            definition.maxAliveOverride = 12; definition.defeatRewardKey = "ENEMY_DEFEAT";
            AssetDatabase.CreateAsset(definition, DefinitionPath);
        }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (!prefab)
        {
            var root = new GameObject("Enemy_Seeker"); root.SetActive(false); root.layer = LayerMask.NameToLayer("Enemy");
            try
            {
                var body = root.AddComponent<Rigidbody>(); body.useGravity = false; body.isKinematic = true;
                body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                root.AddComponent<EnemyBase>().despawnDelaySeconds = .75f;
                root.AddComponent<EnemyScoreReward>().scoreToastPrefab = source.GetComponent<EnemyScoreReward>().scoreToastPrefab;
                var avoid = root.AddComponent<EnemyObstacleAvoidance>(); avoid.filterDroneContacts = true;
                var so = new SerializedObject(avoid); so.FindProperty("obstacleMask").intValue = LayerMask.GetMask("Default", "Enemy", "PowerUps", "NoSpawnZone", "Obstacle");
                so.ApplyModifiedPropertiesWithoutUndo();
                var controller = root.AddComponent<SeekerController>(); controller.definition = definition;
                var shell = root.AddComponent<CapsuleCollider>(); shell.direction = 2; shell.radius = .27f; shell.height = 1.32f;
                controller.shellCollider = shell;
                var hurt = Child(root, "Damage trigger"); var trigger = hurt.AddComponent<CapsuleCollider>();
                trigger.direction = 2; trigger.radius = shell.radius; trigger.height = shell.height; trigger.isTrigger = true;
                hurt.AddComponent<EnemyHurtbox>(); controller.damageTrigger = trigger;
                var visual = root.AddComponent<SeekerVisuals>();
                var faces = Child(root, "Nose and counter-rotating rear panels"); visual.panels = faces.AddComponent<MeshFilter>();
                faces.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(DronePrototypeSetup.Folder + "/Drone Black.mat");
                var core = GameObject.CreatePrimitive(PrimitiveType.Cube); core.name = "Stretching energy core";
                Object.DestroyImmediate(core.GetComponent<Collider>()); core.transform.SetParent(root.transform, false); core.layer = root.layer;
                visual.core = core.GetComponent<Renderer>(); visual.core.sharedMaterial = source.GetComponent<DroneVisuals>().engineRenderer.sharedMaterial;
                var turret = AssetDatabase.LoadAssetAtPath<GameObject>(ParticleBeamTurretSetup.PrefabPath).GetComponent<ParticleBeamTurretController>();
                var exhaust = Object.Instantiate(turret.corePlasma.gameObject, root.transform); exhaust.name = "Launch plasma exhaust";
                controller.launchPlasma = exhaust.GetComponent<ParticleBeamPlasmaContact>();
                controller.launchPlasma.dropRadius = .05f; controller.launchPlasma.dropLifetime = .45f;
                controller.launchPlasma.splashSpeed = 3.5f; controller.launchPlasma.contactRadius = .15f;
                controller.launchPlasma.dripAcceleration = Vector3.zero;
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                { renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; }
                controller.spawnTelegraphPrefab = WarningPrefab(visual);
                root.SetActive(true); visual.Rebuild(); prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }
        definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DefinitionPath);
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (definition.prefab != prefab) { definition.prefab = prefab; EditorUtility.SetDirty(definition); AssetDatabase.SaveAssetIfDirty(definition); }
        if (!AssetDatabase.LoadAssetAtPath<EnemySpawnProfile>(ProfilePath))
        {
            var profile = ScriptableObject.CreateInstance<EnemySpawnProfile>(); profile.maxAliveTotal = profile.maxAliveMelee = 8;
            profile.rules.Clear(); profile.batches.Clear(); AssetDatabase.CreateAsset(profile, ProfilePath);
        }
        Selection.activeObject = prefab;
        Debug.Log("[Seeker] Prefab, definition and arrival telegraph ready; existing tuning preserved.");
    }
    private static GameObject Child(GameObject parent, string name)
    { var child = new GameObject(name); child.layer = parent.layer; child.transform.SetParent(parent.transform, false); return child; }
    private static EnemySpawnTelegraph WarningPrefab(SeekerVisuals visual)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WarningPath); if (prefab) return prefab.GetComponent<EnemySpawnTelegraph>();
        var mesh = visual.CreateSpawnOutline(); mesh.hideFlags = HideFlags.None; mesh.name = "Seeker spawn outline";
        AssetDatabase.CreateAsset(mesh, Folder + "/Seeker Spawn Outline.asset");
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ParticleBeamTurretSetup.WarningPath)); root.name = "Seeker Spawn Telegraph";
        try
        {
            root.GetComponent<MeshFilter>().sharedMesh = mesh;
            root.GetComponent<EnemySpawnTelegraph>().rollDegreesPerSecond = 0f;
            return PrefabUtility.SaveAsPrefabAsset(root, WarningPath).GetComponent<EnemySpawnTelegraph>();
        }
        finally { Object.DestroyImmediate(root); }
    }
    [MenuItem("MASSIVE/Enemies/Seeker/Place In Player Actions")]
    public static void Place()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != CarrierPrototypeSetup.ScenePath)
            throw new InvalidOperationException("Open Player Actions in Edit Mode.");
        Install(); var scene = SceneManager.GetActiveScene();
        var existing = Object.FindObjectsByType<SeekerController>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(x => x.gameObject.scene == scene);
        if (existing) { Selection.activeGameObject = existing.gameObject; return; }
        var root = new GameObject("Seeker Test"); Undo.RegisterCreatedObjectUndo(root, "Place Seeker test");
        var director = root.AddComponent<EnemyDirector>(); director.enemyRoot = root.transform; director.waitForScoring = true;
        director.spawnProfile = AssetDatabase.LoadAssetAtPath<EnemySpawnProfile>(ProfilePath);
        director.arenaBounds = Object.FindObjectsByType<ArenaBoundsFromVectorGrid>(FindObjectsSortMode.None).First(x => x.gameObject.scene == scene);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), root.transform);
        go.name = "Seeker — Test"; go.transform.SetPositionAndRotation(new Vector3(0f, 0f, 2.5f), Quaternion.Euler(0f, 180f, 0f));
        var controller = go.GetComponent<SeekerController>(); controller.sceneDirector = director; controller.arenaBounds = director.arenaBounds;
        PrefabUtility.RecordPrefabInstancePropertyModifications(controller); PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        foreach (var turret in Object.FindObjectsByType<ParticleBeamTurretController>(FindObjectsSortMode.None).Where(t => t.gameObject.scene == scene))
        {
            var encounter = turret.sceneDirector ? turret.sceneDirector.gameObject : turret.gameObject;
            Undo.RecordObject(encounter, "Focus Seeker encounter"); encounter.SetActive(false);
            if (!turret.transform.IsChildOf(encounter.transform))
            { Undo.RecordObject(turret.gameObject, "Focus Seeker encounter"); turret.gameObject.SetActive(false); }
        }
        var gallery = Object.FindObjectsByType<PlayerDemoGallery>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(x => x.gameObject.scene == scene);
        if (gallery)
        {
            Undo.RecordObject(gallery, "Register Seeker with gallery pause"); var so = new SerializedObject(gallery); var encounters = so.FindProperty("encounters");
            encounters.GetArrayElementAtIndex(encounters.arraySize++).objectReferenceValue = root; so.ApplyModifiedProperties();
        }
        EditorSceneManager.MarkSceneDirty(scene); Selection.activeGameObject = go;
    }
}
#endif
