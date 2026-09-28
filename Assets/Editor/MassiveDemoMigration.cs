using System;
using System.IO;
using System.Linq;
using System.Text;
using Massive.Player;
using Massive.PowerUps;
using Massive.Demonstrations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>One-time, reference-preserving migration to the shared Player actor.</summary>
public static partial class MassiveDemoMigration
{
    public const string ActorPath = "Assets/Prefabs/PlayerActor.prefab";
    const string RosterPath = "Assets/Prefabs/Players.prefab";
    const string GameplayPath = "Assets/Prefabs/Levels/Players_Gameplay.prefab";
    const string HowToPath = "Assets/Scenes/S-0_HOW-TO-PLAY.unity";

    public static string Inspect()
    {
        var sb = new StringBuilder();
        foreach (var scene in Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt))
        {
            sb.AppendLine(scene.path + " dirty=" + scene.isDirty);
            foreach (var root in scene.GetRootGameObjects())
            {
                sb.AppendLine(root.name + " active=" + root.activeSelf + " pos=" + root.transform.position);
                foreach (var p in root.GetComponentsInChildren<PseudoPlayerDirector>(true))
                    sb.AppendLine("  " + PathOf(p.transform) + " pos=" + p.transform.position + " scale=" + p.transform.lossyScale + " active=" + p.gameObject.activeInHierarchy);
            }
        }
        var prefab = PrefabUtility.LoadPrefabContents(GameplayPath);
        try
        {
            foreach (var p in prefab.GetComponentsInChildren<PlayerControllerScript>(true))
            {
                sb.AppendLine(p.name + " slot=" + p.playerID + " team=" + p.teamID + " scale=" + p.transform.localScale);
                sb.AppendLine(string.Join(", ", p.GetComponents<Component>().Select(c => c ? c.GetType().Name : "MISSING")));
                var duplicate = p.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).Where(g => g.Count() > 1);
                sb.AppendLine("duplicate names=" + string.Join(", ", duplicate.Select(g=>g.Key)));
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        return sb.ToString();
    }

    [MenuItem("MASSIVE/Demonstrations/1 - Create Shared Player")]
    public static void CreateSharedPlayerMenu() { Debug.Log(CreateSharedPlayer()); }
    public static string CreateSharedPlayer()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Use Edit Mode.");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ActorPath)) return "Shared Player already exists; no migration repeated.";
        Backup(RosterPath); Backup(GameplayPath);
        GameObject gameplay = PrefabUtility.LoadPrefabContents(GameplayPath);
        try
        {
            // Instantiate resolved contents, then detach the P1 subtree. This includes
            // components that used to live only on the gameplay roster variant.
            var source = gameplay.GetComponentsInChildren<PlayerControllerScript>(true).Single(p => p.playerID == 0);
            var clone = Object.Instantiate(source.gameObject);
            try
            {
                if (PrefabUtility.IsPartOfPrefabInstance(clone)) PrefabUtility.UnpackPrefabInstance(clone, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                clone.name = "PlayerActor";
                clone.transform.SetParent(null, false); clone.transform.position = Vector3.zero; clone.transform.rotation = Quaternion.identity;
                clone.SetActive(true);
                var p = clone.GetComponent<PlayerControllerScript>();
                p.goalZone = null; p.playerID = 0; p.teamID = 1;
                Set(p, "tuningMode", (int)PlayerTuningMode.SharedGameplay);
                PrefabUtility.SaveAsPrefabAsset(clone, ActorPath);
            }
            finally { Object.DestroyImmediate(clone); }

            // Remove old variant-only additions before the base inherits them.
            foreach (var p in gameplay.GetComponentsInChildren<PlayerControllerScript>(true))
            {
                foreach (var c in p.GetComponents<Component>())
                    if (c && PrefabUtility.IsAddedComponentOverride(c) &&
                        (c is PlayerScaleAdjuster || c is PlayerMovementReversal || c is PlayerRepulsorFeedback || c is PlayerRepulsorGridPulse))
                        Object.DestroyImmediate(c);
                foreach (var aoe in p.GetComponentsInChildren<PlayerRepulsorAOE>(true))
                    if (PrefabUtility.IsAddedGameObjectOverride(aoe.gameObject)) Object.DestroyImmediate(aoe.gameObject);
            }
            PrefabUtility.SaveAsPrefabAsset(gameplay, GameplayPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(gameplay); }

        var canonical = AssetDatabase.LoadAssetAtPath<GameObject>(ActorPath);
        var roster = PrefabUtility.LoadPrefabContents(RosterPath);
        try
        {
            foreach (var player in roster.GetComponentsInChildren<PlayerControllerScript>(true))
            {
                int slot = player.playerID, team = player.teamID;
                var go = player.gameObject; bool active = go.activeSelf;
                PrefabUtility.ConvertToPrefabInstance(go, canonical, new ConvertToPrefabInstanceSettings
                {
                    objectMatchMode = ObjectMatchMode.ByName,
                    recordPropertyOverridesOfMatches = false,
                    changeRootNameToAssetName = false
                }, InteractionMode.AutomatedAction);
                var actor = go.GetComponent<PlayerControllerScript>(); actor.playerID = slot; actor.teamID = team;
                PrefabUtility.RecordPrefabInstancePropertyModifications(actor);
                go.SetActive(active); PrefabUtility.RecordPrefabInstancePropertyModifications(go);
            }
            PrefabUtility.SaveAsPrefabAsset(roster, RosterPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(roster); }
        return "Created PlayerActor and connected all four roster players; gameplay additions moved to the shared actor.";
    }

    public static void Set(Object target, string name, object value)
    {
        using (var so = new SerializedObject(target))
        {
            var p = so.FindProperty(name); if (p == null) throw new Exception(target.name + " missing " + name);
            if (value is bool b) p.boolValue = b;
            else if (value is int i) p.intValue = i;
            else if (value is float f) p.floatValue = f;
            else if (value is Vector3 v) p.vector3Value = v;
            else p.objectReferenceValue = value as Object;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
    [MenuItem("MASSIVE/Demonstrations/2 - Migrate How To Play")]
    public static void MigrateHowToMenu() { Debug.Log(MigrateHowTo()); }
    public static string MigrateHowTo()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Use Edit Mode.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != HowToPath) throw new Exception("Open How To Play first; other open scenes are not changed.");
        if (scene.GetRootGameObjects().Any(g => g.name == "Player demonstrations")) return "How To Play already migrated.";
        var actor = AssetDatabase.LoadAssetAtPath<GameObject>(ActorPath);
        if (!actor) throw new Exception("Create shared Player first.");
        Backup(HowToPath);
        // Also preserve any editor-only unsaved work as a copy before touching the scene.
        EditorSceneManager.SaveScene(scene, "Library/DemoMigrationBackup/HowToBefore.unity", true);
        var roots = scene.GetRootGameObjects();
        var old = roots.SelectMany(g=>g.GetComponentsInChildren<PseudoPlayerDirector>(true)).ToArray();
        var camera = roots.SelectMany(g=>g.GetComponentsInChildren<Camera>(true)).First(c=>c.CompareTag("MainCamera"));
        var attackRoot = roots.Single(g=>g.name == "Attack sequence");
        var shieldRoot = roots.Single(g=>g.name.Trim() == "Shield sequence");
        GameObject Find(GameObject parent, string name) => parent.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name == name)?.gameObject;
        var parent = new GameObject("Player demonstrations");
        string folder = "Assets/Scripts/Player/Demonstrations/Scenarios";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Scripts/Player/Demonstrations", "Scenarios");
        var definitions = new[] {
            "Assets/Power-ups/Time Dilation/PU_TimeDilation.asset",
            "Assets/Power-ups/Particle Accelerator/PU_ParticleAccelerator.asset",
            "Assets/Power-ups/Decoherence/PU_Decoherence.asset"
        };
        for (int i = 0; i < 5; i++)
        {
            string label = i == 0 ? "Movement and combo" : i == 1 ? "Block and parry" : Path.GetFileNameWithoutExtension(definitions[i-2]).Replace("PU_", "");
            var data = ScriptableObject.CreateInstance<PlayerDemoScenario>();
            data.playerPrefab = actor;
            data.kind = i == 0 ? PlayerDemoKind.MovementAndCombo : i == 1 ? PlayerDemoKind.Block : PlayerDemoKind.PowerUp;
            if (i >= 2) data.powerUp = AssetDatabase.LoadAssetAtPath<PowerUpDefinition>(definitions[i-2]);
            AssetDatabase.CreateAsset(data, folder + "/" + label + ".asset");
            var stage = new GameObject(label); stage.transform.SetParent(parent.transform);
            stage.transform.position = new Vector3(1000f + i * 100f, 0, 1000f);
            var camObject = new GameObject("Demonstration camera"); camObject.transform.SetParent(stage.transform, false);
            var cam = camObject.AddComponent<Camera>(); cam.orthographic = true; cam.orthographicSize = 4;
            cam.transform.localPosition = Vector3.up * 30; cam.transform.rotation = Quaternion.Euler(90,0,0);
            cam.nearClipPlane = .1f; cam.farClipPlane = 60f; cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black; cam.depth = camera.depth + i + 1; cam.allowHDR = camera.allowHDR;
            var view = camObject.AddComponent<PlayerDemoView>(); view.screenCamera = camera;
            float x0 = 2.9f, x1 = 11.7f;
            float z = i == 0 ? 3.37f : i == 1 ? -.05f : -3.73f;
            if (i >= 2) { float width = (x1-x0)/3; x0 += (i-2)*width; x1 = x0 + width - .1f; }
            view.lowerLeft = new Vector3(x0, 0, z-1.55f); view.upperRight = new Vector3(x1, 0, z+1.55f);
            var a = camera.WorldToViewportPoint(view.lowerLeft); var b = camera.WorldToViewportPoint(view.upperRight);
            cam.rect = new Rect(a.x,a.y,b.x-a.x,b.y-a.y);
            var director = stage.AddComponent<PlayerDemoDirector>();
            Set(director, "scenario", data); Set(director, "presentationCamera", cam);
            if (i == 0)
            {
                Set(director, "attackButton", Find(attackRoot, "Sword button center") ?? Find(attackRoot, "Attack button center"));
                Set(director, "joystick", Find(attackRoot, "Joystick")?.transform);
            }
            if (i == 1) Set(director, "shieldButton", Find(shieldRoot, "Shield button center"));
        }
        foreach (var p in old) { p.gameObject.SetActive(false); PrefabUtility.RecordPrefabInstancePropertyModifications(p.gameObject); }
        // Copy changes needed to explain the actual pickup contract, retaining typography/layout.
        foreach (var text in roots.SelectMany(g=>g.GetComponentsInChildren<TMPro.TMP_Text>(true)))
        {
            if (text.text != null && text.text.Contains("Look for power-up icons"))
            { text.text = "Attack power-up icons to claim them.\\nUse the same buttons to activate their effects.".Replace("\\n", "\n"); EditorUtility.SetDirty(text); PrefabUtility.RecordPrefabInstancePropertyModifications(text); }
        }
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Could not save migrated How To Play scene.");
        AssetDatabase.SaveAssets();
        return "Installed five scenarios using PlayerActor, shared input, owned reset groups and individually framed stages.";
    }
    public static void Backup(string path)
    {
        string folder = "Library/DemoMigrationBackup";
        string destination = folder + "/" + path;
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        if (File.Exists(path) && !File.Exists(destination)) File.Copy(path, destination);
    }
    [MenuItem("MASSIVE/Demonstrations/3 - Frame How To Play")]
    public static void PolishMenu() { Debug.Log(Polish()); }
    public static string Polish()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Use Edit Mode.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != HowToPath) throw new Exception("Open How To Play first.");
        var roots = scene.GetRootGameObjects();
        var field = roots.Single(g => g.name == "PLAYING FIELD");
        foreach (string name in new[] { "GRID", "TEAM 1 goal", "TEAM 2 goal" })
        {
            var target = field.transform.Find(name);
            if (!target) continue;
            target.gameObject.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(target.gameObject);
        }
        foreach (var d in roots.SelectMany(g => g.GetComponentsInChildren<PlayerDemoDirector>(true)))
        {
            d.Scenario.viewPadding = .2f;
            if (d.Scenario.kind == PlayerDemoKind.PowerUp) d.Scenario.movementDistance = .75f;
            EditorUtility.SetDirty(d.Scenario);
            if (d.Scenario.kind == PlayerDemoKind.MovementAndCombo)
                Set(d, "joystick", roots.Single(g => g.name == "Attack sequence").transform.Find("Controls/Joystick/Stick"));
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        return "Framing tightened; original instructions retained; unused arena grid/goals hidden in this screen only.";
    }
    public static string PathOf(Transform t) => t.parent ? PathOf(t.parent) + "/" + t.name : t.name;
}
