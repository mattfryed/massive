#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Massive.Multiplier;
using Massive.Scoring;
using Massive.Singularity;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Creates a playable copy of the saved sandbox. Never reconfigures an
/// existing production scene or overwrites authored instructions/icon assets.</summary>
public static class SingularityPlayableLevelSetup
{
    public const string Folder = "Assets/Scripts/Anomalies/SINGULARITY/";
    public const string PrototypePath = "Assets/Scenes/SINGULARITY-PROTOTYPE.unity";
    public const string ScenePath = "Assets/Scenes/S-7_SINGULARITY.unity";
    public const string DefinitionPath = "Assets/Scripts/Level Select/LevelDefinition-SINGULARITY.asset";
    public const string IconPath = Folder + "LS_SINGULARITY Icon.prefab";
    public const string InstructionsPath = Folder + "InstructionsPanel-SINGULARITY.prefab";
    public const string StagePath = Folder + "SINGULARITY Stage.asset";
    public const string CatalogPath = "Assets/Scripts/CORE/LevelCatalog.asset";
    public const string ReferenceScenePath = "Assets/Scenes/S-6_ORBITAL.unity";
    public const int StageNumber = 6;
    public static string LastReport { get; private set; } = "Not run";
    public static string PrototypeHashBefore { get; private set; }
    public static string PrototypeHashAfter { get; private set; }

    [MenuItem("MASSIVE/SINGULARITY/Create Playable Level and Catalog Entry")]
    public static void Create()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Create SINGULARITY in Edit Mode after compilation completes.");
        // Do not save the user's open scene implicitly. The production copy is
        // explicitly sourced from the on-disk checkpoint.
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var open = SceneManager.GetSceneAt(i);
            if (open.path == PrototypePath && open.isDirty)
                throw new InvalidOperationException("Save the SINGULARITY prototype before creating its playable copy.");
        }
        Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(PrototypePath), "Missing saved SINGULARITY prototype.");
        var icon = Require(AssetDatabase.LoadAssetAtPath<GameObject>(IconPath), "Missing SINGULARITY icon.");
        var orbital = Require(AssetDatabase.LoadAssetAtPath<LevelDefinition>(OrbitalLevelSetup.DefinitionPath), "Missing ORBITAL definition.");
        var catalog = Require(AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath), "Missing level catalog.");
        var definition = AssetDatabase.LoadAssetAtPath<LevelDefinition>(DefinitionPath);
        var duplicate = AssetDatabase.FindAssets("t:LevelDefinition")
            .Select(g => AssetDatabase.LoadAssetAtPath<LevelDefinition>(AssetDatabase.GUIDToAssetPath(g)))
            .FirstOrDefault(d => d != null && d != definition && d.levelNumber == StageNumber);
        if (duplicate != null)
            throw new InvalidOperationException("STAGE_006 already belongs to " + AssetDatabase.GetAssetPath(duplicate) + ". No assets changed.");

        PrototypeHashBefore = FileHash(PrototypePath);
        var originalScene = SceneManager.GetActiveScene();
        try
        {
            var instructions = CreateInstructions(icon);
            var stage = AssetDatabase.LoadAssetAtPath<StageProfile>(StagePath);
            if (stage == null)
            {
                stage = ScriptableObject.CreateInstance<StageProfile>();
                stage.stageId = "STAGE_006";
                stage.displayName = "SINGULARITY";
                AssetDatabase.CreateAsset(stage, StagePath);
            }
            bool createdDefinition = definition == null;
            if (createdDefinition)
            {
                definition = ScriptableObject.CreateInstance<LevelDefinition>();
                definition.levelNumber = StageNumber;
                definition.levelTitle = "SINGULARITY";
                definition.anomalyTypeName = "FOLDED SPACETIME";
                // An artistic carousel-scale placement, not a black-hole mass claim.
                definition.scaleExponent = 4;
                definition.audioProfile = orbital.audioProfile;
                definition.iconPrefab = icon;
                definition.instructionsPanelPrefab = instructions;
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }

            bool createdScene = false;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                if (!AssetDatabase.CopyAsset(PrototypePath, ScenePath))
                    throw new InvalidOperationException("Could not copy the saved SINGULARITY prototype.");
                var target = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(target);
                try
                {
                    ConfigureNewScene(target, definition, stage);
                    EditorSceneManager.MarkSceneDirty(target);
                    if (!EditorSceneManager.SaveScene(target))
                        throw new InvalidOperationException("Could not save the new SINGULARITY scene.");
                    createdScene = true;
                }
                catch
                {
                    // Roll back only the copy created by this invocation; never
                    // touch an existing authored production scene or prototype.
                    EditorSceneManager.CloseScene(target, true);
                    AssetDatabase.DeleteAsset(ScenePath);
                    throw;
                }
                finally
                {
                    if (target.IsValid() && target.isLoaded) EditorSceneManager.CloseScene(target, true);
                    if (originalScene.IsValid() && originalScene.isLoaded) SceneManager.SetActiveScene(originalScene);
                }
            }

            // Saving/importing the copied scene can reload newly created assets.
            // Reacquire persistent handles before finishing catalog registration.
            definition = Require(AssetDatabase.LoadAssetAtPath<LevelDefinition>(DefinitionPath), "Missing saved SINGULARITY definition.");
            catalog = Require(AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath), "Missing saved level catalog.");
            if (createdDefinition || string.IsNullOrEmpty(definition.SceneName))
            {
                var data = new SerializedObject(definition);
                var reference = data.FindProperty("gameplayScene");
                reference.FindPropertyRelative("sceneAsset").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
                reference.FindPropertyRelative("sceneName").stringValue = Path.GetFileNameWithoutExtension(ScenePath);
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssetIfDirty(definition);
            }
            else if (definition.SceneName != Path.GetFileNameWithoutExtension(ScenePath))
                throw new InvalidOperationException("Existing SINGULARITY definition points to a different scene; left its authored values unchanged.");

            if (!catalog.Levels.Contains(definition))
            {
                Undo.RecordObject(catalog, "Add SINGULARITY to level catalog");
                var data = new SerializedObject(catalog);
                var levels = data.FindProperty("levels");
                levels.InsertArrayElementAtIndex(levels.arraySize);
                levels.GetArrayElementAtIndex(levels.arraySize - 1).objectReferenceValue = definition;
                data.ApplyModifiedProperties();
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssetIfDirty(catalog);
            }
            var scenes = EditorBuildSettings.scenes.ToList();
            var existing = scenes.FirstOrDefault(s => s.path == ScenePath);
            if (existing == null)
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            else if (!existing.enabled)
                throw new InvalidOperationException("Existing SINGULARITY build entry is disabled; left that authored choice unchanged.");

            PrototypeHashAfter = FileHash(PrototypePath);
            if (PrototypeHashAfter != PrototypeHashBefore)
                throw new InvalidOperationException("The prototype changed during setup. Inspect concurrent edits before continuing.");
            LastReport = "SINGULARITY STAGE_006: " + (createdScene ? "created playable scene" : "kept existing playable scene") +
                "; icon, instructions, catalog and appended build entry ready. Saved prototype unchanged.";
            Debug.Log(LastReport);
        }
        finally
        {
            if (originalScene.IsValid() && originalScene.isLoaded) SceneManager.SetActiveScene(originalScene);
        }
    }

    private static GameObject CreateInstructions(GameObject icon)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(InstructionsPath);
        if (existing != null) return existing;
        const string sourcePath = "Assets/Scripts/Anomalies/ORBITAL/InstructionsPanel-ORBITAL.prefab";
        var contents = PrefabUtility.LoadPrefabContents(sourcePath);
        try
        {
            contents.name = "InstructionsPanel-SINGULARITY";
            var legacy = contents.transform.Find("Electron cross section");
            Vector3 slot = legacy != null ? legacy.localPosition : new Vector3(10.8f, .2f, 0f);
            if (legacy != null) Object.DestroyImmediate(legacy.gameObject);
            foreach (var text in contents.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.name == "Instructions title") text.text = "Folded spacetime";
                if (text.name.Trim() == "Anomaly Type") text.text = "FOLDED SPACETIME";
                if (text.name == "Instructions body")
                    text.text = "Cross the TOP or BOTTOM fold to loop onto the dim BACK FACE. Vertical input reverses there.\n\nThe BLACK HOLE safely transfers players and Amplifier Cores between faces.\n\nBring a Core back to the FRONT FACE and launch it into your team goal to amplify scoring.";
            }
            var display = (GameObject)PrefabUtility.InstantiatePrefab(icon, contents.transform);
            display.name = "SINGULARITY folded space preview";
            display.transform.localPosition = slot;
            display.transform.localRotation = Quaternion.identity;
            display.transform.localScale = Vector3.one * 3.5f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(display.transform);
            return Require(PrefabUtility.SaveAsPrefabAsset(contents, InstructionsPath), "Could not save SINGULARITY instructions.");
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    private static void ConfigureNewScene(Scene target, LevelDefinition definition, StageProfile stage)
    {
        var session = All<SingularityGameplaySession>(target).Single();
        var service = All<MatchScoreService>(target).Single();
        var portal = All<SingularityBlackHolePortal>(target).Single();
        var cycle = All<AmplifierResonanceSpawner>(target).Single();
        if (session.players == null || session.players.Length != 4 || session.players.Any(p => p == null))
            throw new InvalidOperationException("Expected the four existing authored SINGULARITY players.");
        var players = session.players.OrderBy(p => p.playerID).ToArray();
        if (!players.Select(p => p.playerID).SequenceEqual(new[] { 0, 1, 2, 3 }))
            throw new InvalidOperationException("Player identities must be 0 through 3.");
        Require(service.Profile, "SINGULARITY score service needs its existing economy profile.");
        if (All<GameManagerScript>(target).Any() || All<PlayerRosterController>(target).Any())
            throw new InvalidOperationException("Prototype already contains a match owner or roster; review instead of adding duplicates.");
        session.enabled = false;

        var management = new GameObject("SINGULARITY Level Context");
        SceneManager.MoveGameObjectToScene(management, target);
        var manager = management.AddComponent<GameManagerScript>();
        var roster = management.AddComponent<PlayerRosterController>();
        var context = management.AddComponent<SingularityLevelContext>();
        context.level = definition; context.stage = stage; context.match = manager; context.portal = portal;
        Scene source = EditorSceneManager.OpenPreviewScene(ReferenceScenePath);
        try
        {
            EditorUtility.CopySerialized(All<GameManagerScript>(source).Single(), manager);
            EditorUtility.CopySerialized(All<PlayerRosterController>(source).Single(), roster);
            // Preserve the reference scene's audio setup without importing its
            // gameplay, anomaly managers, input manager or EventSystem.
            var music = All<MusicManagerScript>(source).FirstOrDefault();
            if (music != null && !All<MusicManagerScript>(target).Any())
            {
                var clone = Object.Instantiate(music.gameObject);
                clone.name = music.gameObject.name;
                SceneManager.MoveGameObjectToScene(clone, target);
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(source); }
        var matchData = new SerializedObject(manager);
        matchData.FindProperty("scoreService").objectReferenceValue = service;
        matchData.FindProperty("scoreEconomyProfile").objectReferenceValue = service.Profile;
        matchData.FindProperty("engageDeathSphereOnEnd").boolValue = false;
        matchData.FindProperty("disableGameplayOnEnd").boolValue = true;
        matchData.FindProperty("waitForRosterSpawnCompletion").boolValue = true;
        matchData.FindProperty("levelName").stringValue = "SINGULARITY";
        matchData.FindProperty("levelNumber").stringValue = "006";
        matchData.ApplyModifiedPropertiesWithoutUndo();
        var rosterData = new SerializedObject(roster);
        for (int i = 0; i < 4; i++) rosterData.FindProperty("player" + (i + 1)).objectReferenceValue = players[i].gameObject;
        rosterData.FindProperty("oneVOneUsesPlayers1And3").boolValue = true;
        rosterData.ApplyModifiedPropertiesWithoutUndo();

        var world = new GameObject("GameplayObjects") { tag = "GameplayObjects" };
        SceneManager.MoveGameObjectToScene(world, target);
        // New identity parent only: preserve every authored world transform and
        // all surface, score, goal, spawner and player references.
        players[0].transform.parent.SetParent(world.transform, true);
        portal.transform.SetParent(world.transform, true);
        cycle.transform.SetParent(world.transform, true);
        cycle.waitForScoring = true;
        cycle.scoreService = service;
        foreach (var timer in All<MatchTimerPresenter>(target))
        {
            timer.enabled = true;
            var data = new SerializedObject(timer);
            data.FindProperty("gameManager").objectReferenceValue = manager;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        int duration = Mathf.CeilToInt(service.Profile.RegulationDurationSeconds);
        string initialTime = (duration / 60).ToString("00") + ":" + (duration % 60).ToString("00");
        foreach (var text in All<TMP_Text>(target))
        {
            if (text.name == "STAGE TITLE") text.text = "SINGULARITY";
            if (text.name == "STAGE NUMBER") text.text = "STAGE_006";
            if (text.name == "TIME quant") text.text = initialTime;
            if (text.name == "TIME title") text.text = "TIME";
        }
        var camera = All<Camera>(target).Single(c => c.CompareTag("MainCamera"));
        var listeners = All<AudioListener>(target).ToArray();
        foreach (var listener in listeners) listener.enabled = listener.gameObject == camera.gameObject;
        if (camera.GetComponent<AudioListener>() == null) camera.gameObject.AddComponent<AudioListener>();
        foreach (var component in All<Component>(target))
            if (component != null) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
    }

    internal static T[] All<T>(Scene scene) where T : Component
        => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();

    internal static string FileHash(string path)
    {
        using (var digest = System.Security.Cryptography.SHA256.Create())
            return BitConverter.ToString(digest.ComputeHash(File.ReadAllBytes(path)));
    }

    private static T Require<T>(T value, string message) where T : Object
    { if (value == null) throw new InvalidOperationException(message); return value; }
}
#endif
