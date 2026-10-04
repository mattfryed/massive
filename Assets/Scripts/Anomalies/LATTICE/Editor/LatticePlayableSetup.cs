using System;
using System.Linq;
using Massive.Levels;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Massive.Lattice.Editor
{
    public static class LatticePlayableSetup
    {
        public const string ScenePath = "Assets/Scenes/S-1_LATTICE.unity";
        public const string DefinitionPath = "Assets/Scripts/Level Select/LevelDefinition-LATTICE.asset";
        public const string CatalogPath = "Assets/Scripts/CORE/LevelCatalog.asset";
        public const string Folder = LatticeLevelIconAssets.Folder;

        [MenuItem("MASSIVE/LATTICE/Register Playable Level %#&u")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            var open = SceneManager.GetSceneByPath(ScenePath);
            if (open.IsValid() && open.isDirty) throw new InvalidOperationException("Save LATTICE before registering its level identity.");
            var definition = AssetDatabase.LoadAssetAtPath<LevelDefinition>(DefinitionPath);
            if (!definition)
            {
                definition = ScriptableObject.CreateInstance<LevelDefinition>();
                definition.levelTitle = "LATTICE"; definition.levelNumber = 1;
                definition.scaleExponent = -35; definition.anomalyTypeName = "SPACETIME DISRUPTION";
                definition.iconPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LatticeLevelIconAssets.PrefabPath);
                definition.audioProfile = AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Scripts/Level Select/LevelDefinition-HIGGS.asset").audioProfile;
                AssetDatabase.CreateAsset(definition, DefinitionPath);
                var serialized = new SerializedObject(definition);
                serialized.FindProperty("gameplayScene.sceneAsset").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
                serialized.ApplyModifiedPropertiesWithoutUndo(); definition.gameplayScene.SyncFromAsset();
            }
            definition.instructionsPanelPrefab = CreateInstructions();
            EditorUtility.SetDirty(definition); AssetDatabase.SaveAssetIfDirty(definition);
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
            var levels = catalog.Levels.Where(l => l && l != definition).ToList(); levels.Insert(0, definition);
            var so = new SerializedObject(catalog); var array = so.FindProperty("levels"); array.arraySize = levels.Count;
            for (int i = 0; i < levels.Count; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = levels[i];
                if (levels[i].levelNumber != i + 1)
                {
                    Undo.RecordObject(levels[i], "Number playable levels by scale");
                    levels[i].levelNumber = i + 1; EditorUtility.SetDirty(levels[i]); AssetDatabase.SaveAssetIfDirty(levels[i]);
                }
            }
            so.ApplyModifiedProperties(); AssetDatabase.SaveAssetIfDirty(catalog);
            var builds = EditorBuildSettings.scenes.ToList();
            int index = builds.FindIndex(s => s.path == ScenePath);
            if (index < 0) builds.Add(new EditorBuildSettingsScene(ScenePath, true)); else builds[index].enabled = true;
            EditorBuildSettings.scenes = builds.ToArray();

            var stage = AssetDatabase.LoadAssetAtPath<StageProfile>(Folder + "/StageProfile-LATTICE.asset");
            if (!stage)
            {
                stage = ScriptableObject.CreateInstance<StageProfile>();
                stage.stageId = "STAGE_001"; stage.displayName = "LATTICE";
                AssetDatabase.CreateAsset(stage, Folder + "/StageProfile-LATTICE.asset");
            }
            var previous = SceneManager.GetActiveScene();
            bool opened = !open.IsValid();
            var scene = opened ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive) : open;
            try
            {
                var match = All<GameManagerScript>(scene).Single();
                var roster = All<PlayerRosterController>(scene).Single();
                var anomaly = All<AnomalyManager>(scene).Single();
                var context = All<LevelSceneContext>(scene).SingleOrDefault();
                if (!context)
                {
                    var go = new GameObject("LATTICE Level Context"); SceneManager.MoveGameObjectToScene(go, scene);
                    context = go.AddComponent<LevelSceneContext>();
                }
                context.level = definition; context.stage = stage; context.match = match;
                context.roster = roster; context.anomalies = anomaly;
                context.inputManagerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Rewired Input Manager.prefab");
                anomaly.stageProfile = stage; anomaly.match = match; anomaly.autoSchedule = false;
                foreach (var text in All<TMP_Text>(scene))
                {
                    if (text.name == "STAGE TITLE") { text.text = definition.levelTitle; context.stageTitleText = text; }
                    if (text.name == "STAGE NUMBER") { text.text = "STAGE_001"; context.stageNumberText = text; }
                    PrefabUtility.RecordPrefabInstancePropertyModifications(text);
                }
                var gm = new SerializedObject(match);
                gm.FindProperty("levelName").stringValue = definition.levelTitle;
                gm.FindProperty("levelNumber").stringValue = "001";
                gm.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.RecordPrefabInstancePropertyModifications(match);
                EditorUtility.SetDirty(context); EditorUtility.SetDirty(anomaly);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
            Selection.activeObject = definition;
            Debug.Log("LATTICE registered as STAGE_001: definition, instructions, catalog, gameplay identity and enabled build scene. Icon tuning preserved.");
        }

        static GameObject CreateInstructions()
        {
            string path = Folder + "/InstructionsPanel-LATTICE.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (existing) return existing;
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Scripts/Anomalies/ORBITAL/InstructionsPanel-ORBITAL.prefab");
                var panel = (GameObject)PrefabUtility.InstantiatePrefab(template, scene);
                PrefabUtility.UnpackPrefabInstance(panel, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
                panel.name = "InstructionsPanel-LATTICE";
                foreach (string name in new[] { "ORBITAL Carousel Icon", "Electron hit demonstration", "Electron cross section" })
                { var old = panel.transform.Find(name); if (old) UnityEngine.Object.DestroyImmediate(old.gameObject); }
                foreach (var text in panel.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (text.name == "Instructions body") text.text = "Spacetime forms and unravels around you.\n\nInside disrupted regions, movement snaps from DOT TO DOT in eight directions. Travel speed stays the same.\n\nThreads reconnect as the field shifts. Capture amplifier cores in your goal to boost team scoring.";
                    if (text.name == "Instructions title") text.text = "A grid taking shape";
                    if (text.name.Trim() == "Anomaly Type") { text.text = "SPACETIME DISRUPTION"; text.fontSize = 45; }
                }
                var icon = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(LatticeLevelIconAssets.PrefabPath), scene);
                icon.transform.SetParent(panel.transform, false); icon.name = "LATTICE Carousel Icon";
                icon.transform.localPosition = new Vector3(0, 5, 0); icon.transform.localScale = Vector3.one * 5.8f;
                return PrefabUtility.SaveAsPrefabAsset(panel, path);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        public static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
    }
}
