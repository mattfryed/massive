#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Massive.Enemies;
using Massive.Levels;
using Massive.Multiplier;
using Massive.Player;
using Massive.PowerUps;
using Massive.Scoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Read-only catalog audit. Runtime fixtures never save scene changes.</summary>
[InitializeOnLoad]
public static partial class UniversalLevelValidation
{
    public const string Folder = "Library/UniversalLevelValidation";
    static readonly string[] Roots = { "00_Systems", "01_Presentation", "02_Arena", "GameplayObjects", "04_UI", "90_EditorOnly" };
    static UniversalLevelValidation()
    {
        InitializeRuntime();
        InitializePresentationCapture();
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Folder + "/audit.request")) return;
            File.Delete(Folder + "/audit.request"); Audit();
        };
    }
    public static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
    static Object Ref(Object owner, string property) => owner ? new SerializedObject(owner).FindProperty(property)?.objectReferenceValue : null;
    static Transform Owner(Object value) => value is GameObject go ? go.transform : (value as Component)?.transform;
    public static LevelCatalog Catalog => AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/Scripts/CORE/LevelCatalog.asset");
    public static string PathFor(LevelDefinition level) => AssetDatabase.GetAssetPath(Ref(level, "gameplayScene.sceneAsset"));

    [MenuItem("MASSIVE/Levels/Audit All Playable Levels")]
    public static void Audit()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run the catalog audit in Edit Mode.");
        Directory.CreateDirectory(Folder);
        var allLines = new List<string> { "Unity " + Application.unityVersion + "; " + EditorUserBuildSettings.activeBuildTarget + "; " + SystemInfo.graphicsDeviceType,
            "Active scene: " + SceneManager.GetActiveScene().path + "; dirty=" + SceneManager.GetActiveScene().isDirty };
        var definitions = Catalog.Levels.Select(level => AssetDatabase.GetAssetPath(level)).ToArray();
        File.WriteAllLines(Folder + "/catalog-assets.txt", definitions);
        foreach (var definitionPath in definitions)
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(definitionPath);
            var lines = new List<string>();
            void Check(bool ok, string label) => lines.Add((ok ? "PASS " : "FAIL ") + label);
            var path = PathFor(level);
            lines.Add(level.levelTitle + " / " + level.levelNumber + " / " + path);
            Check(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == path), "Catalog scene enabled in Build Settings");
            // Read the disk value: OnValidate may silently repair only the in-memory cache.
            string raw = File.ReadAllText(definitionPath);
            Check(raw.Contains("sceneName: " + Path.GetFileNameWithoutExtension(path) + "\n") || raw.Contains("sceneName: " + Path.GetFileNameWithoutExtension(path) + "\r\n"), "Serialized scene name matches the actual scene asset");
            Check(level.iconPrefab && level.instructionsPanelPrefab && level.audioProfile, "Catalog icon, instructions and audio assigned");
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                var transforms = All<Transform>(scene);
                Check(scene.GetRootGameObjects().Select(g => g.name).SequenceEqual(Roots), "Six ordered universal roots");
                var contexts = All<LevelSceneContext>(scene); var matches = All<GameManagerScript>(scene); var scores = All<MatchScoreService>(scene); var rosters = All<PlayerRosterController>(scene);
                Check(contexts.Length == 1, "One LevelSceneContext");
                Check(matches.Length == 1 && scores.Length == 1 && rosters.Length == 1, "One match, score authority and roster");
                var context = contexts.FirstOrDefault(); var match = matches.FirstOrDefault(); var score = scores.FirstOrDefault();
                Check(context && context.level == level && context.stage && context.match == match && context.roster == rosters.FirstOrDefault(), "Explicit scene identity, stage, match and roster bindings");
                Check(context && context.inputManagerPrefab && context.spawners && context.stageTitleText && context.stageNumberText, "Input bootstrap, spawners and visible title references");
                Check(context && context.stage && context.stage.stageId == "STAGE_" + level.levelNumber.ToString("000"), "Stage profile identity matches catalog");
                Check(match && Ref(match,"scoreService") == score && Ref(match,"scoreEconomyProfile") == Ref(score,"profile") && Ref(score,"profile"), "Shared explicit score economy");
                Check(All<MatchTimerPresenter>(scene).Length == 1 && All<TeamAmplifierToastPresenter>(scene).Length == 1, "One timer and team Amplifier toast");
                Check(All<AudioListener>(scene).Count(c => c.enabled && c.gameObject.activeInHierarchy) == 1, "One active audio listener");
                Check(All<AmplifierGoalCapture>(scene).Length == 2 && All<AmplifierGoalTreatments>(scene).Length == 1, "Two team goals and shared goal treatments");
                Check(All<AmplifierGoalTreatments>(scene).All(t => !t.Preview && !t.ShowControls) && All<DynamoStormController>(scene).All(d => !new SerializedObject(d).FindProperty("showDebugUI").boolValue), "Production preview and debug overlays are hidden");
                var players = All<PlayerControllerScript>(scene).Where(p => !p.IsPseudoPlayer).ToArray();
                Check(players.Length == 4 && players.Select(p => p.playerID).Distinct().Count() == 4, "Four unique authored player slots");
                Check(players.All(p => p.GetComponent<PlayerAttackController>() && p.GetComponent<PlayerRepulsorGridPulse>() && p.GetComponentInChildren<PlayerRepulsorAOE>(true) && p.GetComponentInChildren<PlayerRepulsorFeedback>(true)), "Shared player actions and Repulsor components");
                Check(players.All(p => p.GetComponentsInChildren<PlayerRepulsorAOE>(true).Length == 1), "Exactly one Repulsor damage area per player");
                Check(match && new SerializedObject(match).FindProperty("disableGameplayOnEnd").boolValue, "Gameplay retires during match resolution");
                var pairs = All<AmplifierResonanceSpawner>(scene).Where(p => p.enabled && p.gameObject.activeInHierarchy).ToArray();
                Check(pairs.Length == 1 && pairs[0].corePrefab && pairs[0].spawnRegion && pairs[0].patternAnchor && pairs[0].scoreService == score && pairs[0].waitForScoring && pairs[0].UseSharedSettings, "One match-gated shared Core/Resonance encounter");
                var enemies = All<EnemyDirector>(scene).Where(e => e.enabled && e.gameObject.activeInHierarchy).ToArray();
                Check(enemies.Length == 1 && enemies[0].encounterTimeline && enemies[0].spawnProfile && enemies[0].spawnProfile.enabled && enemies[0].arenaBounds && enemies[0].arenaLayout && enemies[0].waitForScoring, "One shared enemy timeline with local placement and score gate");
                Check(enemies.Length == 1 && enemies[0].previewCue == -1 && enemies[0].resonanceSpawner == pairs.FirstOrDefault(), "Full production timeline and local encounter binding");
                var pickups = All<PowerUpSpawner>(scene).Where(p => p.enabled && p.gameObject.activeInHierarchy).ToArray();
                Check(pickups.Length == 1 && pickups[0].vectorGrid, "One shared pickup spawner with a placement domain");
                Check(transforms.All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), "No missing scripts");
                int refs = 0; var invalid = new List<string>();
                foreach (var component in All<Component>(scene).Where(c => c))
                {
                    var property = new SerializedObject(component).GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                        refs++;
                        if (!property.objectReferenceValue && property.objectReferenceInstanceIDValue != 0) invalid.Add(component.name + "." + property.propertyPath + " is broken");
                        var owner = Owner(property.objectReferenceValue);
                        if (owner && owner.gameObject.scene.IsValid() && owner.gameObject.scene != scene) invalid.Add(component.name + "." + property.propertyPath + " crosses scenes");
                    }
                }
                Check(invalid.Count == 0, refs + " serialized reference slots resolve locally"); lines.AddRange(invalid.Select(s=>"  " + s));
                lines.Add("Hierarchy: " + string.Join(", ", scene.GetRootGameObjects().Select(g=>g.name)));
                File.WriteAllLines(Folder + "/" + level.levelTitle + "-hierarchy.txt", transforms.Select(t => AnimationUtility.CalculateTransformPath(t, null) + " / active=" + t.gameObject.activeSelf + " / " + string.Join(",", t.GetComponents<Component>().Where(c=>c).Select(c=>c.GetType().Name))));
                File.WriteAllText(Folder + "/" + level.levelTitle + "-bindings.txt", string.Join("\n\n", All<MonoBehaviour>(scene).Where(c=>c && (c is LevelSceneContext || c is AnomalyManager || c is MatchTimerPresenter || c is GameManagerScript || c is MatchScoreService || c is AmplifierGoalCapture || c is EnemyDirector)).Select(c=>c.GetType().Name + " " + EditorJsonUtility.ToJson(c,true))));
                lines.Add("Core components:\n" + string.Join("\n", All<MonoBehaviour>(scene).Where(c=>c && (c is LevelSceneContext || c is GameManagerScript || c is MatchScoreService || c is PlayerRosterController || c is EnemyDirector || c is AmplifierResonanceSpawner || c is PowerUpSpawner || c is AnomalyManager || c.GetType().Name.Contains("LevelContext"))).Select(c=>AnimationUtility.CalculateTransformPath(c.transform,null) + " / " + c.GetType().Name + " / active=" + c.isActiveAndEnabled)));
            }
            catch(Exception e) { lines.Add("FAIL Audit exception: " + e); }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            File.WriteAllLines(Folder + "/" + level.levelTitle + "-authored.txt", lines);
            allLines.AddRange(lines); allLines.Add("");
        }
        File.WriteAllLines(Folder + "/authored.txt", allLines);
        Debug.Log("Playable catalog audit complete. " + Folder + "/authored.txt");
    }
}
#endif
