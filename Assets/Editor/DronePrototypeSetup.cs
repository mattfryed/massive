#if UNITY_EDITOR
using System;
using System.Linq;
using Massive.Enemies;
using Massive.Multiplier;
using Massive.Scoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static partial class DronePrototypeSetup
{
    public const string Folder = "Assets/Enemy System/Enemy Types/Melee/Drone";
    public const string PrefabPath = Folder + "/Enemy_Drone.prefab";
    public const string DefinitionPath = Folder + "/ED_Drone.asset";
    public const string ProfilePath = Folder + "/ESP_DronePrototype.asset";
    public const string EncounterPath = Folder + "/Drone Swarm Spawner.prefab";
    public const string ToastPath = Folder + "/Drone Score Toast.prefab";

    [MenuItem("MASSIVE/Enemies/Drone/Apply Visual and Avoidance Refinements")]
    public static void ApplyRefinements()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Use Edit Mode to refine assets.");
        var toastPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ToastPath);
        if (toastPrefab == null)
        {
            var go = new GameObject("Drone Score Toast"); go.layer = LayerMask.NameToLayer("Enemy");
            try
            {
                var label = go.AddComponent<TMPro.TextMeshPro>();
                var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Power-ups/PU_PickupToast.prefab").GetComponentInChildren<TMPro.TMP_Text>(true);
                label.font = source.font; label.fontSize = 1.3f; label.alignment = TMPro.TextAlignmentOptions.Center;
                label.rectTransform.sizeDelta = new Vector2(2f, .6f); label.text = "+100 meV"; label.color = Color.white;
                label.GetComponent<Renderer>().sortingOrder = 30;
                go.AddComponent<EnemyScoreToast>().label = label;
                toastPrefab = PrefabUtility.SaveAsPrefabAsset(go, ToastPath);
            }
            finally { Object.DestroyImmediate(go); }
        }
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var avoid = root.GetComponent<EnemyObstacleAvoidance>();
            avoid.ObstacleMask = LayerMask.GetMask("Default", "Enemy", "PowerUps", "NoSpawnZone", "Obstacle");
            avoid.filterDroneContacts = true; avoid.LookAheadDistance = 1.5f;
            var visual = root.GetComponent<DroneVisuals>();
            visual.coreRadius = .042f; visual.coreOrbit = .052f; visual.facetTimingScatter = .28f;
            visual.exhaustPerSecond = 42f; visual.exhaustLifetime = .58f; visual.exhaustSpeed = 1.1f;
            visual.exhaustRadius = .045f; visual.exhaustVariation = .28f; visual.exhaustSpreadDegrees = 12f;
            root.GetComponent<EnemyScoreReward>().scoreToastPrefab = toastPrefab.GetComponent<EnemyScoreToast>();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        var engine = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Drone Engine.mat");
        engine.SetFloat("_SmoothK", .008f); EditorUtility.SetDirty(engine); AssetDatabase.SaveAssetIfDirty(engine);
    }

    [MenuItem("MASSIVE/Enemies/Drone/Create Prototype Assets")]
    public static void CreateAssets()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Use Edit Mode to create assets.");
        var def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DefinitionPath);
        if (def == null)
        {
            def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.id = "DRONE"; def.category = EnemyCategory.Melee; def.defeatRewardKey = ScoreRewardKeys.DroneDefeat;
            def.healthMassEq = 1f; def.damageTakenPerSwordHit = 1f; def.damageToPlayerMass01 = .04f;
            def.moveSpeed = 2.4f; def.turnSpeed = 220f; def.spawnRadiusWorld = .55f; def.maxAliveOverride = 24;
            AssetDatabase.CreateAsset(def, DefinitionPath);
        }
        bool newFill = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Drone Black.mat") == null;
        bool newEngine = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Drone Engine.mat") == null;
        Material fill = MaterialAt("Drone Black", "Unlit/Color"); if (newFill) fill.color = Color.black;
        Material engine = MaterialAt("Drone Engine", "MASSIVE/MetaballSDF");
        if (newEngine)
        {
            engine.SetColor("_LitColor", Color.white); engine.SetColor("_UnlitColor", Color.white); engine.SetColor("_OutlineColor", Color.white);
            engine.SetFloat("_SmoothK", .018f); engine.SetFloat("_SurfaceEps", .001f); engine.SetInt("_MaxSteps", 48);
            EditorUtility.SetDirty(engine);
        }
        if (newFill) EditorUtility.SetDirty(fill);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        bool creatingPrefab = prefab == null;
        if (prefab == null)
        {
            var root = new GameObject("Drone"); root.SetActive(false); root.layer = LayerMask.NameToLayer("Enemy");
            try
            {
                var body = root.AddComponent<Rigidbody>(); body.useGravity = false; body.mass = .2f;
                body.interpolation = RigidbodyInterpolation.Interpolate; body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
                var e = root.AddComponent<EnemyBase>(); e.despawnDelaySeconds = .32f;
                var collider = root.AddComponent<CapsuleCollider>(); collider.direction = 2; collider.radius = .22f;
                collider.height = .825f; collider.center = new Vector3(0f, 0f, .1375f); collider.isTrigger = true;
                root.AddComponent<EnemyHurtbox>(); root.AddComponent<EnemyScoreReward>();
                var avoid = root.AddComponent<EnemyObstacleAvoidance>();
                var so = new SerializedObject(avoid);
                so.FindProperty("obstacleMask").intValue = (1 << 9) | (1 << 11);
                so.FindProperty("sphereCastRadius").floatValue = .23f;
                so.FindProperty("rayHeight").floatValue = 0f;
                so.FindProperty("lookAheadDistance").floatValue = 1.2f; so.ApplyModifiedPropertiesWithoutUndo();
                var controller = root.AddComponent<DroneController>(); controller.definition = def;
                var visual = root.AddComponent<DroneVisuals>();
                var shell = new GameObject("Crystal — six facets per half"); shell.layer = root.layer; shell.transform.SetParent(root.transform, false);
                visual.shell = shell.transform;
                visual.noseMesh = shell.AddComponent<MeshFilter>();
                var renderer = shell.AddComponent<MeshRenderer>(); renderer.sharedMaterial = fill;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
                var volume = GameObject.CreatePrimitive(PrimitiveType.Cube); volume.name = "Engine core + exhaust";
                Object.DestroyImmediate(volume.GetComponent<Collider>());
                volume.layer = root.layer; volume.transform.SetParent(root.transform, false);
                volume.transform.localPosition = Vector3.back * .5f; volume.transform.localScale = Vector3.one * 3f;
                visual.engineRenderer = volume.GetComponent<Renderer>(); visual.engineRenderer.sharedMaterial = engine;
                visual.engineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                visual.engineRenderer.receiveShadows = false;
                root.SetActive(true);
                prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }
        def.prefab = prefab; EditorUtility.SetDirty(def);
        var profile = AssetDatabase.LoadAssetAtPath<EnemySpawnProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<EnemySpawnProfile>();
            profile.initialDelaySeconds = 4f; profile.minSpawnDelaySeconds = 5f; profile.maxSpawnDelaySeconds = 8f;
            profile.maxAliveTotal = 26; profile.maxAliveMelee = 26;
            profile.batches.Add(new EnemyBatchSpawnRule { enemy = def });
            var dyson = AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/Enemy System/Enemy Types/Melee/DysonSphere/ED_DysonSphere.asset");
            profile.rules.Add(new EnemySpawnRule { enabled = true, enemy = dyson, weightOverride = -1f, minSecondsSinceMatchStart = 25f, maxAlive = 2 });
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }
        AddScoreRow();
        CreateSpawnTelegraph();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(EncounterPath) == null)
        {
            var root = new GameObject("Enemies — Drone Swarm");
            try
            {
                var region = root.AddComponent<AmplifierSpawnRegion>();
                region.neutralWidthFraction = 1f; region.clearanceWorld = .08f; region.borderMarginWorld = .4f;
                region.minDistanceFromPlayers = 2f; region.previewObjectRadius = .55f; region.drawZones = false;
                var director = root.AddComponent<EnemyDirector>(); director.spawnProfile = profile;
                director.placementRegion = region; director.waitForScoring = true; director.spawnCheckRadiusWorld = .25f;
                director.minDistanceFromPlayers = 2f; director.borderBufferWorld = .4f;
                director.spawnBlockMask = (1 << 6) | (1 << 9) | (1 << 11); director.enemyRoot = root.transform;
                PrefabUtility.SaveAsPrefabAsset(root, EncounterPath);
            }
            finally { Object.DestroyImmediate(root); }
        }
        var catalog = AssetDatabase.LoadAssetAtPath<Massive.EditorTools.Prototyping.GameplayPrefabGalleryCatalog>("Assets/Editor/Gameplay Prefab Gallery Catalog.asset");
        if (catalog != null)
        {
            var category = catalog.Categories.FirstOrDefault(x => x.HierarchyName.IndexOf("Enemies", StringComparison.OrdinalIgnoreCase) >= 0);
            if (category != null && !category.Entries.Any(x => x.Prefab == prefab))
            { category.Entries.Add(new Massive.EditorTools.Prototyping.GameplayPrefabGalleryEntry("Drone", prefab)); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog); }
        }
        AssetDatabase.SaveAssetIfDirty(def); AssetDatabase.SaveAssetIfDirty(fill); AssetDatabase.SaveAssetIfDirty(engine);
        if (creatingPrefab) ApplyRefinements();
        Debug.Log("[Drone] Prototype assets ready; authored tuning is preserved on subsequent runs.");
    }
    private static Material MaterialAt(string name, string shader)
    {
        string path = Folder + "/" + name + ".mat";
        var result = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (result != null) return result;
        var found = Shader.Find(shader); if (found == null) throw new Exception("Missing shader: " + shader);
        result = new Material(found); AssetDatabase.CreateAsset(result, path); return result;
    }
    private static void AddScoreRow()
    {
        var profile = AssetDatabase.LoadAssetAtPath<ScoreEconomyProfile>("Assets/Scripts/Scoring/ScoreEconomyProfile.asset");
        if (profile.TryGetReward(ScoreRewardKeys.DroneDefeat, out _)) return;
        var so = new SerializedObject(profile); var rows = so.FindProperty("rewards");
        int index = rows.arraySize; rows.InsertArrayElementAtIndex(index); var row = rows.GetArrayElementAtIndex(index);
        row.FindPropertyRelative("key").stringValue = ScoreRewardKeys.DroneDefeat;
        row.FindPropertyRelative("enabled").boolValue = true; row.FindPropertyRelative("amount").longValue = 100;
        row.FindPropertyRelative("unit").enumValueIndex = 0; row.FindPropertyRelative("multiplierEligible").boolValue = true;
        row.FindPropertyRelative("chainEffect").enumValueIndex = 2; row.FindPropertyRelative("chainCharge").floatValue = .25f;
        row.FindPropertyRelative("repeatPolicy").enumValueIndex = 1; row.FindPropertyRelative("feedbackId").stringValue = "ENEMY_DEFEAT";
        row.FindPropertyRelative("expectedOccurrencesPerRound").intValue = 60;
        so.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(profile);
    }

    [MenuItem("MASSIVE/Enemies/Drone/Add Swarm To Active Scene")]
    public static void AddToScene()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Use Edit Mode to wire the scene.");
        Scene scene = SceneManager.GetActiveScene();
        if (Object.FindObjectsByType<EnemyDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(x => x.gameObject.scene == scene))
            throw new InvalidOperationException("An EnemyDirector already exists. Assign the Drone prototype profile there instead of adding a duplicate.");
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(EncounterPath), scene);
        Undo.RegisterCreatedObjectUndo(root, "Add Drone swarm");
        var director = root.GetComponent<EnemyDirector>();
        director.arenaBounds = Object.FindObjectsByType<ArenaBoundsFromVectorGrid>(FindObjectsSortMode.None).FirstOrDefault(x => x.gameObject.scene == scene);
        director.placementRegion.arenaBounds = director.arenaBounds;
        director.resonanceSpawner = Object.FindObjectsByType<AmplifierResonanceSpawner>(FindObjectsSortMode.None).FirstOrDefault(x => x.gameObject.scene == scene);
        PrefabUtility.RecordPrefabInstancePropertyModifications(director);
        PrefabUtility.RecordPrefabInstancePropertyModifications(director.placementRegion);
        EditorSceneManager.MarkSceneDirty(scene); Selection.activeGameObject = root;
    }
}
#endif
