#if UNITY_EDITOR
using System;
using System.Linq;
using Massive.Enemies;
using Massive.Multiplier;
using Massive.Orbital;
using Massive.Scoring;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class OrbitalLevelSetup
{
    public const string Folder = "Assets/Scripts/Anomalies/ORBITAL/";
    public const string ScenePath = "Assets/Scenes/S-6_ORBITAL.unity";
    public const string DefinitionPath = "Assets/Scripts/Level Select/LevelDefinition-ORBITAL.asset";

    [MenuItem("MASSIVE/ORBITAL/Create missing level assets")]
    public static void Create()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play Mode before creating ORBITAL.");
        var original = SceneManager.GetActiveScene();
        var particle = MaterialAt("Probability Particles.mat", "MASSIVE/Orbital/Probability Particle");
        var line = MaterialAt("Electron Impacts.mat", "MASSIVE/Orbital/Impact");
        var definition = AssetDatabase.LoadAssetAtPath<LevelDefinition>(DefinitionPath);
        if (!definition)
        {
            definition = ScriptableObject.CreateInstance<LevelDefinition>();
            definition.levelNumber = 5; definition.levelTitle = "ORBITAL";
            definition.anomalyTypeName = "ELECTRON PROBABILITY";
            definition.audioProfile = AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Scripts/Level Select/LevelDefinition-DYNAMO.asset").audioProfile;
            AssetDatabase.CreateAsset(definition, DefinitionPath);
        }
        var stage = AssetDatabase.LoadAssetAtPath<StageProfile>(Folder + "ORBITAL Stage.asset");
        if (!stage)
        {
            stage = ScriptableObject.CreateInstance<StageProfile>();
            stage.stageId = "STAGE_005"; stage.displayName = "ORBITAL";
            AssetDatabase.CreateAsset(stage, Folder + "ORBITAL Stage.asset");
        }

        var work = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(work);
        try
        {
            var nugget = CreateNugglet();
            definition.iconPrefab = CreateIcon(particle);
            definition.instructionsPanelPrefab = CreateInstructions(particle);
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "ORBITAL Cloud.prefab"))
            {
                var go = CreateCloud(particle, line, nugget);
                PrefabUtility.SaveAsPrefabAsset(go, Folder + "ORBITAL Cloud.prefab");
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
        finally { EditorSceneManager.CloseScene(work, true); SceneManager.SetActiveScene(original); }

        if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))
        {
            if (!AssetDatabase.CopyAsset("Assets/Scenes/S-8_DYNAMO-PROTOTYPE.unity", ScenePath))
                throw new InvalidOperationException("Could not copy the gameplay foundation.");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                string[] removeRoots = { "Magnetosphere Controller", "MAGNETOSPHERE ANOMALY", "StormHUD",
                    "Resonance 345 Hz Prototype", "Resonance 345 Hz — Option B (Particles)", "Resonance — A-B Comparison",
                    "Amplifier + Resonance — Spawn Cycle" };
                foreach (var root in scene.GetRootGameObjects())
                    if (removeRoots.Contains(root.name)) UnityEngine.Object.DestroyImmediate(root);
                var gameplay = scene.GetRootGameObjects().First(g => g.name == "GameplayObjects");
                string[] removeChildren = { "DYNAMO", "Matter nugget", "MatterNuggerSpawner", "Abiogenesis visualization", "PREFAB GALLERY - DISPLAY ONLY" };
                foreach (Transform child in gameplay.transform.Cast<Transform>().ToArray())
                    if (removeChildren.Contains(child.name)) UnityEngine.Object.DestroyImmediate(child.gameObject);
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (!component) continue;
                        string type = component.GetType().Name;
                        if (type == "PlayerStormPush" || type == "DynamoStormController" || type == "MagnetosphereStormController")
                            component.enabled = false;
                    }
                    foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
                    {
                        if (label.name == "STAGE TITLE") label.text = "ORBITAL";
                        if (label.name == "STAGE NUMBER") label.text = "STAGE_005";
                        PrefabUtility.RecordPrefabInstancePropertyModifications(label);
                    }
                }
                var score = InScene<MatchScoreService>(scene);
                var anomaly = InScene<AnomalyManager>(scene);
                anomaly.autoSchedule = false; anomaly.stageProfile = stage;
                var cloudGo = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "ORBITAL Cloud.prefab"), scene);
                cloudGo.transform.SetParent(gameplay.transform, false);
                cloudGo.transform.position = new Vector3(0f, .12f, 0f);
                var hazard = cloudGo.GetComponent<OrbitalProbabilityCloud>();
                hazard.scoreService = score; hazard.anomalyManager = anomaly;
                PrefabUtility.RecordPrefabInstancePropertyModifications(hazard);
                var context = new GameObject("ORBITAL Level Context").AddComponent<OrbitalLevelContext>();
                context.level = definition;
                var director = InScene<EnemyDirector>(scene);
                director.anomalyManager = anomaly;
                director.arenaBounds = InScene<ArenaBoundsFromVectorGrid>(scene);
                director.resonanceSpawner = null;
                director.placementRegion = director.GetComponent<AmplifierSpawnRegion>();
                ConfigureAmplifierCycle(scene);
                foreach (var listener in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<AudioListener>(true)))
                    if (listener.GetComponent<Camera>() != scene.GetRootGameObjects().First(g => g.name == "Main Camera").GetComponent<Camera>())
                        UnityEngine.Object.DestroyImmediate(listener);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally { EditorSceneManager.CloseScene(scene, true); SceneManager.SetActiveScene(original); }
        }
        var data = new SerializedObject(definition);
        data.FindProperty("gameplayScene").FindPropertyRelative("sceneAsset").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        data.FindProperty("gameplayScene").FindPropertyRelative("sceneName").stringValue = "S-6_ORBITAL";
        data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(definition);
        var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/Scripts/CORE/LevelCatalog.asset");
        if (!catalog.Levels.Contains(definition))
        {
            var catalogData = new SerializedObject(catalog); var levels = catalogData.FindProperty("levels");
            levels.InsertArrayElementAtIndex(levels.arraySize);
            levels.GetArrayElementAtIndex(levels.arraySize - 1).objectReferenceValue = definition;
            catalogData.ApplyModifiedPropertiesWithoutUndo();
        }
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == ScenePath))
        {
            // Append to preserve every existing numeric scene index.
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = scenes.ToArray();
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[ORBITAL] Scene, particle cloud, nuggets, instructions, icon, catalog and Build Settings configured.");
    }

    private static T InScene<T>(Scene scene) where T : Component
    { return scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).First(); }

    public static AmplifierResonanceSpawner ConfigureAmplifierCycle(Scene scene)
    {
        if (EditorApplication.isPlaying || scene.path != ScenePath)
            throw new InvalidOperationException("Configure the saved ORBITAL scene outside Play Mode.");
        var existing = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<AmplifierResonanceSpawner>(true)).FirstOrDefault();
        if (existing) return existing;
        var sourceScene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/S-8_DYNAMO-PROTOTYPE.unity");
        try
        {
            var source = InScene<AmplifierResonanceSpawner>(sourceScene);
            var go = UnityEngine.Object.Instantiate(source.gameObject);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.name = "Amplifier + Resonance — Spawn Cycle";
            var cycle = go.GetComponent<AmplifierResonanceSpawner>();
            cycle.spawnRegion = go.GetComponent<AmplifierSpawnRegion>();
            cycle.spawnRegion.arenaBounds = InScene<ArenaBoundsFromVectorGrid>(scene);
            cycle.spawnRegion.noGoColliders = Array.Empty<Collider>();
            cycle.spawnRegion.ignoredColliders = Array.Empty<Collider>();
            cycle.spawnRegion.previewPattern = null;
            cycle.scoreService = InScene<MatchScoreService>(scene);
            cycle.comparison = null;
            cycle.standaloneExamples = Array.Empty<GameObject>();
            var anchor = new GameObject("Pattern anchor").transform;
            anchor.SetParent(go.transform, false);
            var sourceAnchor = source.patternAnchor ? source.patternAnchor : source.transform;
            anchor.SetPositionAndRotation(sourceAnchor.position, sourceAnchor.rotation);
            cycle.patternAnchor = anchor;
            // One owner for gameplay cores: preserve any old example as an inactive scene object.
            foreach (var core in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<AmplifierCoreGameplay>(true)))
                if (!core.IsPresentationOnly) { core.gameObject.SetActive(false); PrefabUtility.RecordPrefabInstancePropertyModifications(core.gameObject); }
            foreach (var director in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EnemyDirector>(true)))
            {
                director.resonanceSpawner = cycle;
                PrefabUtility.RecordPrefabInstancePropertyModifications(director);
            }
            go.SetActive(true);
            EditorSceneManager.MarkSceneDirty(scene);
            return cycle;
        }
        finally { EditorSceneManager.ClosePreviewScene(sourceScene); }
    }

    private static Material MaterialAt(string name, string shaderName)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + name);
        if (material) return material;
        var shader = Shader.Find(shaderName);
        if (!shader) throw new InvalidOperationException("Compile shader first: " + shaderName);
        material = new Material(shader); AssetDatabase.CreateAsset(material, Folder + name); return material;
    }

    private static OrbitalMassNugget CreateNugget()
    {
        string path = Folder + "Orbital Mass Nugget.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing) return existing.GetComponent<OrbitalMassNugget>();
        var go = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Matter nugget.prefab"));
        go.name = "Orbital Mass Nugget"; go.transform.position = Vector3.zero;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<MatterNuggetScript>());
        ConfigurePickup(go.AddComponent<OrbitalMassNugget>());
        var body = go.GetComponent<Rigidbody>();
        body.useGravity = false; body.linearDamping = .025f;
        body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        go.SetActive(false);
        existing = PrefabUtility.SaveAsPrefabAsset(go, path);
        UnityEngine.Object.DestroyImmediate(go); return existing.GetComponent<OrbitalMassNugget>();
    }

    public static void ConfigurePickup(MatterNuggetScript pickup)
    {
        const string path = "Assets/Physics Materials/Mass Pickup Glide.physicMaterial";
        var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
        if (!material)
        {
            material = new PhysicsMaterial("Mass Pickup Glide")
            {
                dynamicFriction = 0f, staticFriction = 0f, bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum
            };
            AssetDatabase.CreateAsset(material, path);
        }
        pickup.glideMaterial = material;
        pickup.rewardToastPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Enemy System/Enemy Types/Melee/Drone/Drone Score Toast.prefab").GetComponent<EnemyScoreToast>();
        foreach (var collider in pickup.GetComponentsInChildren<Collider>(true))
            if (!collider.isTrigger) collider.sharedMaterial = material;
    }

    public static OrbitalMassNugget CreateNugglet()
    {
        string path = Folder + "Mass Nugglet.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing) return existing.GetComponent<OrbitalMassNugget>();
        var go = (GameObject)PrefabUtility.InstantiatePrefab(CreateNugget().gameObject);
        go.name = "Mass Nugglet";
        go.transform.localScale = Vector3.one / 3f;
        go.GetComponent<OrbitalMassNugget>().rewardMultiplier = .1f;
        // The source uses local particle scaling. Include the smaller root in both particle sizes and shapes.
        foreach (var particles in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = particles.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
        existing = PrefabUtility.SaveAsPrefabAsset(go, path);
        UnityEngine.Object.DestroyImmediate(go);
        return existing.GetComponent<OrbitalMassNugget>();
    }

    private static GameObject CreateCloud(Material particle, Material line, OrbitalMassNugget nugget)
    {
        var go = new GameObject("ORBITAL — Electron Probability Cloud");
        var visual = go.AddComponent<OrbitalCloudVisual>(); visual.particleMaterial = particle;
        visual.outerTailExtension = .25f; visual.particleCount = 23000; visual.sceneIntroSeconds = 1.4f;
        visual.cycleFormations = true; visual.Rebuild();
        var impact = go.AddComponent<OrbitalImpactVisual>(); impact.lineMaterial = line;
        var hazard = go.AddComponent<OrbitalProbabilityCloud>(); hazard.cloud = visual; hazard.impacts = impact; hazard.nuggetPrefab = nugget;
        return go;
    }

    private static GameObject CreateIcon(Material particle)
    {
        string path = Folder + "LS_ORBITAL Icon.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (existing) return existing;
        var go = new GameObject("ORBITAL Icon");
        var v = go.AddComponent<OrbitalCloudVisual>();
        v.radius = .5f; v.particleCount = 7000; v.pointSize = new Vector2(.007f, .014f);
        v.fullVolume = true;
        v.particleMaterial = particle; v.opacity = .7f; v.useUnscaledTime = true; v.Rebuild();
        go.AddComponent<LevelIcon>();
        existing = PrefabUtility.SaveAsPrefabAsset(go, path); UnityEngine.Object.DestroyImmediate(go); return existing;
    }

    private static GameObject CreateInstructions(Material particle)
    {
        string path = Folder + "InstructionsPanel-ORBITAL.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (existing) return existing;
        var go = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Scripts/Instructions/InstructionsPanel-DYNAMO.prefab"));
        go.name = "InstructionsPanel-ORBITAL";
        var legacy = go.transform.Find("LS_DYNAMO icon");
        if (legacy) UnityEngine.Object.DestroyImmediate(legacy.gameObject);
        var legacyPreview = go.transform.Find("Mid panel (1)");
        if (legacyPreview) UnityEngine.Object.DestroyImmediate(legacyPreview.gameObject);
        foreach (var component in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (!component) continue;
            string type = component.GetType().Name;
            if (type.Contains("Dynamo") || type.Contains("Magnetosphere") || type.Contains("Storm")) UnityEngine.Object.DestroyImmediate(component);
        }
        foreach (var text in go.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.name == "Instructions body")
                text.text = "The cloud cycles through SIX FORMATIONS. Dense regions mean more ELECTRON STRIKES.\n\nStrikes push players, cores and enemies. Collect mass nugglets for a small boost.\n\nCapture amplifier cores in your goal to boost team scoring.";
            if (text.name == "Instructions title") text.text = "Electron probability";
            if (text.name.Trim() == "Anomaly Type") { text.text = "PROBABILITY CLOUD"; text.fontSize = 45f; }
        }
        var display = new GameObject("Electron cross section"); display.transform.SetParent(go.transform, false);
        display.transform.localPosition = new Vector3(10.8f, .2f, 0f);
        var v = display.AddComponent<OrbitalCloudVisual>(); v.particleMaterial = particle; v.radius = 1.75f;
        v.pointSize = new Vector2(.012f, .025f);
        v.particleCount = 14000; v.useUnscaledTime = true; v.Rebuild();
        existing = PrefabUtility.SaveAsPrefabAsset(go, path); UnityEngine.Object.DestroyImmediate(go); return existing;
    }
}
#endif
