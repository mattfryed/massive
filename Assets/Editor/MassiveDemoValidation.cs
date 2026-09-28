using System;
using System.Linq;
using System.Text;
using Massive.Demonstrations;
using Massive.Player;
using Massive.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static partial class MassiveDemoValidation
{
    [MenuItem("MASSIVE/Demonstrations/Validate Prefabs and Tuning")]
    public static void ValidateMenu() { Debug.Log(StaticChecks() + "\n" + InheritanceChecks()); }
    [MenuItem("MASSIVE/Demonstrations/Run How To Play")]
    public static void StartMenu() { Debug.Log(Start()); }
    [MenuItem("MASSIVE/Demonstrations/Report Runtime Results")]
    public static void StatusMenu() { Debug.Log(Status()); }

    public static string InheritanceChecks()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Use Edit Mode.");
        var tuning = Resources.Load<PlayerTuningProfile>("PlayerTuningProfile");
        var modifiers = PlayerGlobalModifiers.Current;
        string saved = EditorJsonUtility.ToJson(tuning), savedModifiers = EditorJsonUtility.ToJson(modifiers);
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        int checks = 0;
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
        try
        {
            tuning.sharedEnabled = true;
            tuning.movement.maxMoveSpeed = 8.75f;
            tuning.combat.attackCooldown = .37f;
            tuning.repulsor.knockbackVelocity = 9.2f;
            modifiers.ForPlayer(0).size = .83f;
            var scope = new GameObject("Inactive inheritance fixture"); scope.SetActive(false);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(MassiveDemoMigration.ActorPath);
            foreach (bool demo in new[] { false, true })
            {
                var go = UnityEngine.Object.Instantiate(source, scope.transform);
                var p = go.GetComponent<PlayerControllerScript>();
                if (demo) p.ConfigureDemonstration(scope.transform, 0, 1);
                Check(p.UsesGameplayTuning, "Shared tuning policy");
                Check(p.ParticipatesInMatch != demo, "Match participation independent of tuning");
                Check(p.Effective_maxMoveSpeed == 8.75f, "Movement inherits changed shared value");
                Check(p.attackController.Effective_attackCooldown == .37f && p.attackController.Profile == tuning.attackProfile, "Combat inherits shared profile/cooldown");
                Check(go.GetComponentInChildren<PlayerRepulsorAOE>(true).Effective_knockbackVelocity == 9.2f, "Repulsor inherits shared value");
                Check(PlayerGlobalModifiers.For(p).ForPlayer(0).size == .83f, "Size resolves changed global slot value");
                p.UseSharedSettings = false;
                Check(p.Effective_maxMoveSpeed != 8.75f, "Explicit local opt-out remains supported");
            }
        }
        finally
        {
            EditorJsonUtility.FromJsonOverwrite(saved, tuning);
            EditorJsonUtility.FromJsonOverwrite(savedModifiers, modifiers);
            EditorSceneManager.CloseScene(scene, true);
            SceneManager.SetActiveScene(previous);
        }
        return checks + " tuning inheritance checks passed; all temporary values restored.";
    }
    public static string ExistingTuningChecks() => Massive.EditorTools.PlayerTuningValidation.RunChecks();
    public static string AuditScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Use Edit Mode.");
        var sb = new StringBuilder();
        foreach (string path in new[] { "Assets/Scenes/S-8_DYNAMO.unity", "Assets/Scenes/S-9_HIGGS.unity", "Assets/Scenes/S-0_HOW-TO-PLAY.unity" })
        {
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                sb.AppendLine(path);
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var p in root.GetComponentsInChildren<PlayerControllerScript>(true))
                        sb.AppendLine("  " + p.name + " slot=" + p.playerID + " team=" + p.teamID + " goal=" + (p.goalZone ? p.goalZone.name : "null") + " source=" + PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(p.gameObject));
                    if (path.Contains("HOW-TO"))
                    {
                        if (root.name == "PLAYING FIELD" || root.name == "GameplayObjects" || root.name.Contains("sequence"))
                            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                                sb.AppendLine("    " + MassiveDemoMigration.PathOf(t));
                        foreach (var d in root.GetComponentsInChildren<PlayerDemoDirector>(true))
                        {
                            var so = new SerializedObject(d);
                            sb.AppendLine(d.name + " attackCue=" + so.FindProperty("attackButton").objectReferenceValue + " shieldCue=" + so.FindProperty("shieldButton").objectReferenceValue + " joystick=" + so.FindProperty("joystick").objectReferenceValue);
                        }
                    }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        return sb.ToString();
    }
    static MassiveDemoValidation() { EditorApplication.playModeStateChanged += OnPlayModeChanged; }
    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool("DemoValidation.Pending", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        { SessionState.SetBool("DemoValidation.Background", Application.runInBackground); Application.runInBackground = true; }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            Application.runInBackground = SessionState.GetBool("DemoValidation.Background", false);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString("DemoValidation.PreviousStart", ""));
            SessionState.SetBool("DemoValidation.Pending", false);
        }
    }
    public static string StaticChecks()
    {
        int checks = 0;
        void Check(bool condition, string label) { if (!condition) throw new Exception(label); checks++; }
        foreach (string path in new[] { MassiveDemoMigration.ActorPath, "Assets/Prefabs/Players.prefab", "Assets/Prefabs/Levels/Players_Gameplay.prefab" })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var players = root.GetComponentsInChildren<PlayerControllerScript>(true);
                Check(players.Length == (path == MassiveDemoMigration.ActorPath ? 1 : 4), path + " actor count");
                if (path != MassiveDemoMigration.ActorPath)
                {
                    var manager = root.GetComponent<PlayerManager>();
                    Check(manager && manager.ActivePlayers.Count == 4 && manager.ActivePlayers.All(p => p && players.Contains(p)), path + " roster references preserved");
                    Check(players.Select(p => p.playerID).OrderBy(i => i).SequenceEqual(new[] { 0, 1, 2, 3 }), path + " unique player slots");
                }
                foreach (var p in players)
                {
                    Check(p.attackController && p.powerUps && p.visualsController, path + " required references " + p.name);
                    Check(p.GetComponent<PlayerShieldAbility>() && p.GetComponent<PlayerScaleAdjuster>() && p.GetComponent<PlayerMovementReversal>(), path + " abilities " + p.name);
                    Check(p.GetComponentsInChildren<PlayerRepulsorAOE>(true).Length == 1, path + " exactly one Repulsor " + p.name);
                    Check(p.GetComponentsInChildren<PlayerMelee>(true).All(m=>new SerializedObject(m).FindProperty("owner").objectReferenceValue == p), path + " melee ownership " + p.name);
                    Check(p.GetComponentsInChildren<Transform>(true).All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0), path + " missing scripts " + p.name);
                    if (path != MassiveDemoMigration.ActorPath)
                        Check(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(p.gameObject) == MassiveDemoMigration.ActorPath, path + " shared actor source " + p.name);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        return checks + " serialized prefab checks passed.";
    }
    public static string Start()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Already playing.");
        StaticChecks();
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/S-0_HOW-TO-PLAY.unity") throw new Exception("Open How To Play.");
        if (scene.isDirty) throw new Exception("Scene contains unsaved changes; preserve them before running validation.");
        SessionState.SetString("DemoValidation.PreviousStart", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool("DemoValidation.Pending", true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path);
        EditorApplication.isPlaying = true;
        return "Starting actual How To Play scene in Play Mode.";
    }
    public static string Status()
    {
        var sb = new StringBuilder("playing=" + EditorApplication.isPlaying + " time=" + Time.time + " scale=" + Time.timeScale + "\n");
        foreach (var d in UnityEngine.Object.FindObjectsByType<PlayerDemoDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            sb.AppendLine(d.name + ": " + d.Phase + " loops=" + d.SuccessfulLoops + " failures=" + d.FailedLoops + " block=" + d.ConfirmedBlocks + " claims=" + d.ConfirmedClaims + " shots=" + d.ConfirmedShots + " stage=" + d.LastComboStage + " error=" + d.LastFailure);
            if (d.Primary) sb.AppendLine("  actor=" + d.Primary.transform.position + " scale=" + d.Primary.transform.lossyScale + " tuning=" + d.Primary.UsesGameplayTuning + " match=" + d.Primary.ParticipatesInMatch + " attack=" + d.Primary.attackController.IsAttacking + " mass=" + d.Primary.massScore);
        }
        return sb.ToString();
    }
    public static string Pause() { Time.timeScale = 0f; return Status(); }
    public static string Slow() { Time.timeScale = .5f; return Status(); }
    public static string Normal() { Time.timeScale = 1f; return Status(); }
    public static string Restart()
    {
        foreach (var d in UnityEngine.Object.FindObjectsByType<PlayerDemoDirector>(FindObjectsSortMode.None)) { d.Stop(); d.Play(); }
        return Status();
    }
    public static string StopDemonstrations()
    {
        foreach (var d in UnityEngine.Object.FindObjectsByType<PlayerDemoDirector>(FindObjectsSortMode.None)) d.Stop();
        return "All demonstrations stopped; check cleanup after a frame.";
    }
    public static string CheckCleanup()
    {
        int count = 0;
        foreach (var d in UnityEngine.Object.FindObjectsByType<PlayerDemoDirector>(FindObjectsSortMode.None))
        {
            if (d.Primary || d.Partner || d.transform.Find("Demo session")) throw new Exception(d.name + " retained a session after Stop.");
            count++;
        }
        if (UnityEngine.Object.FindObjectsByType<Massive.PowerUps.PowerUpPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(p => p.transform.position.x > 900f))
            throw new Exception("A demonstration pickup survived cleanup.");
        if (UnityEngine.Object.FindObjectsByType<Massive.PowerUps.ParticleAcceleratorProjectile>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(p => p.transform.position.x > 900f))
            throw new Exception("A demonstration projectile survived cleanup.");
        return count + " demonstration sessions cleaned up; no actors, pickups or projectiles remain in their stages.";
    }
    public static string Stop() { Time.timeScale = 1f; EditorApplication.isPlaying = false; return "Stopping validation; editor scene restored."; }
}
