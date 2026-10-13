using System;
using System.IO;
using System.Linq;
using Massive.Enemies;
using Massive.Levels;
using Massive.Multiplier;
using Massive.Scoring;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Massive.Emergence.Editor
{
    public static class EmergenceLevelSetup
    {
        public const string Folder = "Assets/Scripts/Anomalies/EMERGENCE";
        public const string ScenePath = "Assets/Scenes/S-10_EMERGENCE.unity";
        public const string SourceScene = "Assets/Scenes/S-5_PULSAR.unity";
        public const string DefinitionPath = Folder + "/LevelDefinition-EMERGENCE.asset";
        public const string ProfilePath = Folder + "/Primordial Ecology.asset";
        public const string Evidence = "Library/EmergenceValidation";
        public static T[] All<T>(Scene s) where T : Component => s.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
        static void Set(Object target, string name, Object value)
        { var so = new SerializedObject(target); so.FindProperty(name).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        static T Asset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!asset) { asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); }
            return asset;
        }

        [MenuItem("MASSIVE/EMERGENCE/Create Missing Level from PULSAR")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Create the level in Edit Mode.");
            Directory.CreateDirectory(Evidence);
            var profile = Asset<EmergenceProfile>(ProfilePath);
            var previewProfile = AssetDatabase.LoadAssetAtPath<EmergenceProfile>(Folder + "/Preview Ecology.asset");
            if (!previewProfile)
            {
                previewProfile = Asset<EmergenceProfile>(Folder + "/Preview Ecology.asset");
                int[] counts = { 72, 48, 24, 160, 48 };
                for (int i = 0; i < EmergenceProfile.SpeciesCount; i++) previewProfile.species[i].count = counts[i];
                EditorUtility.SetDirty(previewProfile);
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Ecology Particles.mat");
            if (!material)
            {
                var shader = Shader.Find("MASSIVE/Emergence/Particles"); if (!shader) throw new InvalidOperationException("Import the ecology shader first.");
                material = new Material(shader); AssetDatabase.CreateAsset(material, Folder + "/Ecology Particles.mat");
            }
            var sourceDef = AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Scripts/Level Select/LevelDefinition-PULSAR.asset");
            var definition = Asset<LevelDefinition>(DefinitionPath);
            definition.levelTitle = "EMERGENCE"; definition.levelNumber = 10; definition.scaleExponent = -6;
            definition.anomalyTypeName = "ACTIVE MATTER"; definition.audioProfile = sourceDef.audioProfile;
            var stage = Asset<StageProfile>(Folder + "/StageProfile-EMERGENCE.asset");
            stage.stageId = "STAGE_010"; stage.displayName = "EMERGENCE"; stage.anomalyPool.Clear(); EditorUtility.SetDirty(stage);
            definition.iconPrefab = EmergenceLevelIconAssets.CreateOrUpdate();
            bool instructionsMissing = !AssetDatabase.LoadAssetAtPath<GameObject>(EmergenceInstructionsSetup.PanelPath);
            definition.instructionsPanelPrefab = CreateInstructions(sourceDef, definition.iconPrefab);
            var nugget = CreateNugget();
            if (!File.Exists(ScenePath))
            {
                if (!AssetDatabase.CopyAsset(SourceScene, ScenePath)) throw new IOException("Could not duplicate PULSAR.");
                var previous = SceneManager.GetActiveScene();
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                try
                {
                    SceneManager.SetActiveScene(scene);
                    var transforms = All<Transform>(scene);
                    var context = All<LevelSceneContext>(scene).Single();
                    var levelContent = transforms.Single(t => AnimationUtility.CalculateTransformPath(t, null) == "GameplayObjects/LevelContent");
                    var removed = levelContent.Cast<Transform>().Select(t => t.gameObject).ToList();
                    removed.Add(transforms.Single(t => AnimationUtility.CalculateTransformPath(t, null) == "02_Arena/NoSpawnZones/PULSAR exclusion").gameObject);
                    var objects = removed.SelectMany(g => g.GetComponentsInChildren<Component>(true)).Cast<Object>()
                        .Concat(removed.SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Select(t => (Object)t.gameObject)).ToHashSet();
                    var cleared = new System.Collections.Generic.List<string>();
                    foreach (var c in All<Component>(scene).Where(c => c && !(c is Transform) && !objects.Contains(c)))
                    {
                        var so = new SerializedObject(c); var p = so.GetIterator(); bool changed = false;
                        while (p.Next(true)) if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue && objects.Contains(p.objectReferenceValue))
                        { cleared.Add(c.GetType().Name + "." + p.propertyPath); p.objectReferenceValue = null; changed = true; }
                        if (changed) so.ApplyModifiedPropertiesWithoutUndo();
                    }
                    foreach (var g in removed) Object.DestroyImmediate(g);
                    foreach (var region in All<AmplifierSpawnRegion>(scene)) region.noGoColliders = Array.Empty<Collider>();
                    foreach (var layout in All<EnemyArenaLayout>(scene)) layout.exclusions = Array.Empty<Collider>();
                    foreach (var t in transforms.Where(t => t && t.name.Contains("PULSAR"))) t.name = t.name.Replace("PULSAR", "EMERGENCE");
                    context.level = definition; context.stage = stage;
                    context.stageTitleText.text = "EMERGENCE"; context.stageNumberText.text = "STAGE_010";
                    if (context.anomalies) context.anomalies.stageProfile = stage;
                    var gm = new SerializedObject(context.match);
                    gm.FindProperty("levelName").stringValue = "EMERGENCE"; gm.FindProperty("levelNumber").stringValue = "010"; gm.ApplyModifiedPropertiesWithoutUndo();
                    var go = new GameObject("EMERGENCE Particle Ecology"); go.transform.SetParent(levelContent, false);
                    var field = go.AddComponent<EmergenceField>();
                    field.profile = profile; field.particleMaterial = material; field.arena = All<ArenaBoundsFromVectorGrid>(scene).Single();
                    field.scores = All<MatchScoreService>(scene).Single(); field.match = context.match; field.anomalies = context.anomalies;
                    field.players = All<PlayerControllerScript>(scene); ConfigurePickups(field);
                    field.RebuildPreview();
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save EMERGENCE.");
                    File.WriteAllText(Evidence + "/creation.txt", "Created from saved PULSAR. Source unchanged. Removed PULSAR body, rhythm prototype and central spawn exclusion.\nCleared source-specific references:\n" + string.Join("\n", cleared));
                }
                finally { EditorSceneManager.CloseScene(scene, true); if (previous.IsValid()) SceneManager.SetActiveScene(previous); }
            }
            var def = new SerializedObject(definition);
            def.FindProperty("gameplayScene.sceneAsset").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            def.FindProperty("gameplayScene.sceneName").stringValue = Path.GetFileNameWithoutExtension(ScenePath); def.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            EmergenceLevelIconAssets.Register(definition);
            // Append only: shared SceneFlow uses names and all existing build indices remain unchanged.
            if (!EditorBuildSettings.scenes.Any(s => s.path == ScenePath))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
            foreach (var asset in new Object[] { profile, previewProfile, definition, stage, material }) AssetDatabase.SaveAssetIfDirty(asset);
            if (instructionsMissing) EmergenceInstructionsSetup.Install();
            Debug.Log("EMERGENCE ready in the level carousel. Existing open scenes were preserved.");
        }
        public static void ConfigurePickups(EmergenceField field)
        {
            field.nuggetPrefab = CreateNugget();
            field.matterNuggletPrefab = CreatePickupVariant(false, true);
            field.amplifierNuggletPrefab = CreatePickupVariant(true, true);
            field.amplifierNuggetPrefab = CreatePickupVariant(true, false);
        }
        static EmergenceMassNugget CreatePickupVariant(bool amplifier, bool small)
        {
            string name = "Metabolic " + (amplifier ? "Amplifier " : "Matter ") + (small ? "Nugglet" : "Nugget");
            string path = Folder + "/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing) return existing.GetComponent<EmergenceMassNugget>();
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(CreateNugget().gameObject, scene);
                if (amplifier)
                {
                    PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    for (int i = root.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                    var visual = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Power-ups/Amplifier Nuggets/Amplifier Nugget Visual.prefab");
                    if (!visual) throw new InvalidOperationException("Assign the shared amplifier nugget visual asset.");
                    var child = (GameObject)PrefabUtility.InstantiatePrefab(visual, scene);
                    child.transform.SetParent(root.transform, false);
                    child.transform.localPosition = Vector3.zero;
                    child.SetActive(true);
                }
                root.name = name;
                root.transform.localScale = Vector3.one * (small ? 1f / 3 : 1);
                foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
                { var main = ps.main; main.scalingMode = ParticleSystemScalingMode.Hierarchy; }
                var pickup = root.GetComponent<EmergenceMassNugget>();
                pickup.rewardMultiplier = small ? .1f : 1;
                pickup.scoreRewardKey = amplifier ? "" : small ? ScoreRewardKeys.MassNugglet : ScoreRewardKeys.MassNugget;
                pickup.personalMultiplierGain = amplifier ? (small ? .1f : 1) : 0;
                root.SetActive(false);
                return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<EmergenceMassNugget>();
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        static EmergenceMassNugget CreateNugget()
        {
            string path = Folder + "/Metabolic Mass Nugget.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (existing) return existing.GetComponent<EmergenceMassNugget>();
            var root = PrefabUtility.LoadPrefabContents("Assets/Scripts/Anomalies/ORBITAL/Orbital Mass Nugget.prefab");
            try
            {
                var old = root.GetComponent<MatterNuggetScript>(); var json = EditorJsonUtility.ToJson(old);
                Object.DestroyImmediate(old); var pickup = root.AddComponent<EmergenceMassNugget>();
                EditorJsonUtility.FromJsonOverwrite(json, pickup);
                pickup.scoreRewardKey = ScoreRewardKeys.MassNugget; pickup.rewardMultiplier = 1;
                root.name = "Metabolic Mass Nugget";
                return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<EmergenceMassNugget>();
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        static GameObject CreateInstructions(LevelDefinition source, GameObject icon)
        {
            string path = Folder + "/InstructionsPanel-EMERGENCE.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (existing) return existing;
            var root = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(source.instructionsPanelPrefab));
            try
            {
                var oldIcon = root.transform.Find("LS_PULSAR-Icon");
                Vector3 pos = oldIcon.localPosition;
                Object.DestroyImmediate(oldIcon.gameObject);
                var preview = (GameObject)PrefabUtility.InstantiatePrefab(icon, root.scene); preview.transform.SetParent(root.transform, false);
                preview.transform.localPosition = pos;
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (text.name == "Instructions title") text.text = "A LIVING RESOURCE FIELD";
                    else if (text.name == "Instructions body") text.text = "Follow feeding colonies to collect the MASS NUGGETS they release.\n\nTHRUST and SWEEP disrupt their bodies. REPULSOR scatters the soup.\n\nKeep a colony intact for a steady supply, or interrupt your opponent's harvest.";
                    else if (text.name == "Anomaly Type Title") text.text = "LEVEL PHENOMENON";
                    else if (text.name.Trim() == "Anomaly Type") text.text = "ACTIVE MATTER";
                }
                root.name = "InstructionsPanel-EMERGENCE";
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
