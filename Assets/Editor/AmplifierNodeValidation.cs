#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Massive.Player;
using Massive.PowerUps;
using Massive.Scoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Canonical Amplifier Node claims and economy behavior in a disposable Play Mode scene.</summary>
[InitializeOnLoad]
public static class AmplifierNodeValidation
{
    const string Key = "MASSIVE.AmplifierNodeValidation";
    const string DefinitionPath = "Assets/Power-ups/Amplifier Node/PU_AmplifierNode.asset";
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<string> checks = new(), errors = new();
    static IEnumerator routine;
    static int frame;
    static double deadline;
    public static string Result => SessionState.GetString(Key + ".Result", "Not run");

    static AmplifierNodeValidation() => EditorApplication.playModeStateChanged += State;

    [MenuItem("MASSIVE/Power-ups/Validate Amplifier Node")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Run in Edit Mode after compilation.");
        var original = SceneManager.GetActiveScene();
        string path = "Assets/AmplifierNodeValidation-" + Guid.NewGuid().ToString("N") + ".unity";
        var fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            var camera = new GameObject("Fixture camera", typeof(Camera), typeof(AudioListener));
            SceneManager.MoveGameObjectToScene(camera, fixture);
            var light = new GameObject("Fixture light", typeof(Light));
            light.GetComponent<Light>().type = LightType.Directional;
            SceneManager.MoveGameObjectToScene(light, fixture);
            if (!EditorSceneManager.SaveScene(fixture, path)) throw new Exception("Could not save fixture.");
        }
        finally { EditorSceneManager.CloseScene(fixture, true); SceneManager.SetActiveScene(original); }
        SessionState.SetString(Key + ".Previous", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetString(Key + ".Scene", path);
        SessionState.SetString(Key + ".Result", "RUNNING"); SessionState.SetBool(Key, true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        EditorApplication.isPlaying = true;
    }

    static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            checks.Clear(); errors.Clear(); frame = -1;
            deadline = EditorApplication.timeSinceStartup + 90;
            routine = Checks(); Application.logMessageReceived += Log; EditorApplication.update += Tick;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
            (routine as IDisposable)?.Dispose(); routine = null;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key + ".Previous", ""));
            string path = SessionState.GetString(Key + ".Scene", "");
            if (path.StartsWith("Assets/AmplifierNodeValidation-", StringComparison.Ordinal) && path.EndsWith(".unity", StringComparison.Ordinal))
                AssetDatabase.DeleteAsset(path);
            SessionState.SetBool(Key, false);
        }
    }

    static void Log(string message, string stack, LogType type)
    { if (type is LogType.Error or LogType.Exception or LogType.Assert) errors.Add(message + "\n" + stack); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || routine == null || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timed out.");
            if (!routine.MoveNext()) Finish(null);
        }
        catch (Exception e) { Finish(e); }
    }
    static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        (routine as IDisposable)?.Dispose(); routine = null;
        Application.logMessageReceived -= Log;
        bool pass = error == null && errors.Count == 0;
        SessionState.SetString(Key + ".Result", (pass ? "PASSED" : "FAILED") + " (" + checks.Count + " checks)\n" +
            string.Join("\n", checks) + (error == null ? "" : "\n" + error) + "\n" + string.Join("\n", errors));
        if (pass) Debug.Log(Result); else Debug.LogError(Result);
        EditorApplication.isPlaying = false;
    }
    static void Check(bool pass, string label)
    { if (!pass) throw new Exception(label); checks.Add("PASS " + label); }
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Hidden).SetValue(target, value);
    static void Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, Hidden).Invoke(target, args);

    static IEnumerator Checks()
    {
        Check(SceneManager.GetActiveScene().path == SessionState.GetString(Key + ".Scene", ""), "Tests use only the disposable scene");
        var definition = AssetDatabase.LoadAssetAtPath<AmplifierNodePowerUpDefinition>(DefinitionPath);
        var settings = PowerUpSettings.Current;
        Check(definition && definition.massNode && definition.pickupPrefab, "Definition, shared mass grant and prefab are linked");
        Check(Massive.EditorTools.PowerUpSettingsEditing.Validate(settings).Count == 0 && settings.definitions.Contains(definition), "Global catalogue includes a valid Amplifier Node");
        Check(Enumerable.Range(0, 1000).Any(i => settings.Select(i / 1000f) == definition), "Amplifier Node is eligible in the shared spawn mix");
        Check(!definition.pickupPrefab.GetComponent<ScoreRewardEmitter>(), "Node does not add claim score or extra charge");
        Check(PrefabUtility.GetPrefabAssetType(definition.pickupPrefab) == PrefabAssetType.Variant, "Node inherits Mass Node's shell and physics");
        var layers = definition.pickupPrefab.GetComponentsInChildren<ParticleSystem>(true);
        Check(layers.Length == 3 && layers.Any(p => p.name == "Prismatic Swirl"), "Three swirl layers include the prismatic material");
        Check(layers.All(p => p.main.simulationSpace == ParticleSystemSimulationSpace.Local &&
            p.colorOverLifetime.color.gradient.Evaluate(1).a == 1), "All particles follow the icon without opacity fading on death");

        var profile = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ScoreEconomyProfile>("Assets/Scripts/Scoring/ScoreEconomyProfile.asset"));
        var scene = SceneManager.CreateScene("Amplifier Node contacts", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        var scope = new GameObject("Amplifier Node fixtures"); scope.SetActive(false); SceneManager.MoveGameObjectToScene(scope, scene);
        scope.transform.position = new Vector3(4000, 0, 4000);
        var serviceGO = new GameObject("Fixture score service"); serviceGO.SetActive(false);
        var service = serviceGO.AddComponent<MatchScoreService>(); Set(service, "profile", profile); serviceGO.SetActive(true);
        var massCopy = Object.Instantiate(definition.massNode);
        var nodeCopy = Object.Instantiate(definition); nodeCopy.massNode = massCopy;
        try
        {
            var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerActor.prefab"), scope.transform);
            var player = actor.GetComponent<PlayerControllerScript>(); player.ConfigureDemonstration(scope.transform, 0, 1);
            Set(player, "isPseudoPlayer", false); player.SetControlMode(PlayerControlMode.Disabled);
            scope.SetActive(true); service.RegisterPlayer(player); service.ResetForMatch(true);
            yield return null; yield return null;
            actor.GetComponent<Rigidbody>().isKinematic = true;
            var chain = actor.GetComponent<PlayerScoreChain>();
            int notifications = 0; chain.Changed += _ => notifications++;
            foreach (float mass in new[] { .25f, player.massScoreMax - .005f, player.massScoreMax })
            {
                chain.ResetChain(); player.massScore = mass;
                definition.massNode.ApplyInstant(player, player.powerUps, null); float expected = player.massScore;
                player.massScore = mass; int before = notifications;
                definition.ApplyInstant(player, player.powerUps, null);
                Check(Mathf.Approximately(player.massScore, expected), "Matches Mass Node mass gain from " + mass);
                Check(chain.CurrentMultiplier == 2 && notifications == before + 1, "One personal step and one HUD notification at mass " + mass);
            }
            chain.ResetChain(); chain.ApplyAward(ScoreChainAwardMode.AdvanceAndRefresh, chain.ChargeRequired * .5f);
            Check(chain.CurrentMultiplier == 1.5, "Fractional multiplier fixture starts at ×1.5");
            definition.ApplyInstant(player, player.powerUps, null);
            Check(chain.CurrentMultiplier == 2 && chain.Charge == 0, "Fractional progress completes to the next milestone");
            foreach (int expected in new[] { 4, 8, 16 })
            { definition.ApplyInstant(player, player.powerUps, null); Check(chain.CurrentMultiplier == expected, "Next node reaches ×" + expected); }
            int atCap = notifications; player.massScore = .25f;
            definition.ApplyInstant(player, player.powerUps, null);
            Check(chain.CurrentMultiplier == 16 && chain.Charge == 0 && notifications == atCap && player.massScore > .25f,
                "Cap grants mass without exceeding ×16 or emitting a false multiplier change");
            chain.Configure(new ScoreChainSettings { multiplierSteps = new[] { 1, 3, 5 }, chargeRequiredPerTier = new[] { 2f, 7f } });
            definition.ApplyInstant(player, player.powerUps, null);
            Check(chain.CurrentMultiplier == 3, "Custom multiplier milestones are respected");
            chain.Configure(new ScoreChainSettings { multiplierSteps = new[] { 1 } });
            Check(!chain.AdvanceToNextStep() && chain.CurrentMultiplier == 1, "Single-step economy cannot overflow");
            chain.Configure(profile.ChainSettings);
            massCopy.growCalls = 1; massCopy.extraMass01 = .025f; massCopy.pickupSfx = ""; massCopy.spawnSmallMassBlobs = false;
            player.massScore = .25f; massCopy.ApplyInstant(player, player.powerUps, null); float tuned = player.massScore;
            player.massScore = .25f; nodeCopy.ApplyInstant(player, player.powerUps, null);
            Check(Mathf.Approximately(player.massScore, tuned), "Editing the linked mass source updates Amplifier Node's grant");
            var equipped = AssetDatabase.LoadAssetAtPath<PowerUpDefinition>("Assets/Power-ups/Time Dilation/PU_TimeDilation.asset");
            player.powerUps.Equip(equipped); float remaining = player.powerUps.RemainingSeconds;
            definition.ApplyInstant(player, player.powerUps, null);
            Check(player.powerUps.ActiveDefinition == equipped && player.powerUps.RemainingSeconds == remaining,
                "Instant node leaves equipped ability and duration intact");
            player.powerUps.Clear(); service.ResetForMatch(true);

            var attack = actor.GetComponent<PlayerAttackController>();
            var sword = actor.GetComponentInChildren<PlayerMelee>(true).GetComponent<Collider>();
            var repulsor = actor.GetComponentInChildren<PlayerRepulsorAOE>(true);
            for (int stage = 0; stage < 3; stage++)
            {
                chain.ResetChain(); attack.CancelAttack(); Invoke(attack, "StartStage", stage);
                Collider hitbox = stage == 2 ? repulsor.GetComponent<SphereCollider>() : sword;
                float until = Time.time + 3;
                while (!hitbox.enabled && attack.IsAttacking && Time.time < until) yield return null;
                Check(hitbox.enabled, "Stage " + (stage + 1) + " opens its real collider");
                Vector3 position = stage == 2 ? repulsor.OriginWorld + Vector3.right * repulsor.RadiusWorld * .7f : hitbox.bounds.center;
                var pickup = PowerUpPickup.Spawn(definition, position, Quaternion.identity, scope.transform);
                pickup.GetComponent<Rigidbody>().isKinematic = true; pickup.GetComponent<PowerUpIconTetheredBody>().enabled = false;
                int claims = 0; pickup.Claimed += _ => claims++;
                Physics.SyncTransforms(); scene.GetPhysicsScene().Simulate(.02f);
                Check(claims == 1 && chain.CurrentMultiplier == 2, "Stage " + (stage + 1) + " physics contact claims exactly one multiplier step");
                pickup.TryActivate(hitbox); scene.GetPhysicsScene().Simulate(.02f);
                Check(claims == 1 && chain.CurrentMultiplier == 2 && pickup.GetComponent<PowerUpIconManifestAnimator>().IsDespawning,
                    "Stage " + (stage + 1) + " repeated contact cannot claim again; outro starts on hit");
                Object.DestroyImmediate(pickup.gameObject); attack.CancelAttack();
            }
            var bodyPickup = PowerUpPickup.Spawn(definition, player.transform.position, Quaternion.identity, scope.transform);
            chain.ResetChain(); bodyPickup.TryActivate(player.GetComponent<SphereCollider>());
            Check(chain.CurrentMultiplier == 1 && !bodyPickup.GetComponent<PowerUpIconManifestAnimator>().IsDespawning,
                "Body contact does not claim the attack-only node");
            Object.DestroyImmediate(bodyPickup.gameObject);
            Check(service.GetTeamScore(1) == 0 && service.GetTeamScore(2) == 0 && service.GetTeamAmplifierMultiplier(1) == 1 && service.GetTeamAmplifierMultiplier(2) == 1,
                "Node changes neither team score nor team amplifier");
            Check(errors.Count == 0, "No runtime errors");
        }
        finally
        {
            Object.DestroyImmediate(scope); Object.DestroyImmediate(serviceGO);
            Object.DestroyImmediate(massCopy); Object.DestroyImmediate(nodeCopy); Object.DestroyImmediate(profile);
            if (scene.IsValid()) SceneManager.UnloadSceneAsync(scene);
        }
    }
}
#endif
